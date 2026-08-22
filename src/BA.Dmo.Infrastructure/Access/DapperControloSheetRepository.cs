using System.Data;
using BA.Dmo.Application.Modules.Controlo;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Controlo;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// R010 — Dapper Folha de Controlo repository (N23 <c>controlo_sheets</c> +
/// <c>controlo_sheet_items</c> + <c>controlo_sheet_events</c>). A sheet insert writes the
/// header + its items atomically in the shared <see cref="IDbUnitOfWork"/>; history events
/// are append-only (trigger <c>trg_controlo_sheet_events_append_only</c>). Reads return the
/// sheet with its current items and full event history.
/// </summary>
public sealed class DapperControloSheetRepository : IControloSheetRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperControloSheetRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Guid> InsertAsync(IDbUnitOfWork uow, ControloFolha sheet, CancellationToken ct = default)
    {
        const string sheetSql = @"
INSERT INTO controlo_sheets
    (controlo_sheet_id, job_on_id, job_on_revision_id, production_code, reference,
     machine_code, display_id, status, created_by, created_at_utc, submitted_by,
     submitted_at_utc, submitted_note, decided_by, decided_at_utc, decision,
     decision_note, updated_at_utc)
VALUES
    (@Id, @JobOnId, @JobOnRevisionId, @ProductionCode, @Reference,
     @MachineCode, @DisplayId, @Status, @CreatedBy, @CreatedAtUtc, @SubmittedBy,
     @SubmittedAtUtc, @SubmittedNote, @DecidedBy, @DecidedAtUtc, @Decision,
     @DecisionNote, @UpdatedAtUtc);";
        await Db.ExecuteAsync(uow.Connection, sheetSql, new
        {
            Id = sheet.ControloSheetId,
            JobOnId = sheet.JobOnId,
            JobOnRevisionId = sheet.JobOnRevisionId,
            ProductionCode = sheet.ProductionCode,
            Reference = sheet.Reference,
            MachineCode = sheet.MachineCode,
            DisplayId = sheet.DisplayId,
            Status = ControloFolhaStateCodec.ToStorage(sheet.State),
            CreatedBy = (object?)sheet.CreatedBy ?? DBNull.Value,
            CreatedAtUtc = sheet.CreatedAtUtc,
            SubmittedBy = (object?)sheet.SubmittedBy ?? DBNull.Value,
            SubmittedAtUtc = (object?)sheet.SubmittedAtUtc ?? DBNull.Value,
            SubmittedNote = (object?)sheet.SubmittedNote ?? DBNull.Value,
            DecidedBy = (object?)sheet.DecidedBy ?? DBNull.Value,
            DecidedAtUtc = (object?)sheet.DecidedAtUtc ?? DBNull.Value,
            Decision = sheet.Decision is { } d ? (object?)ControloFolhaStateCodec.ToStorage(d) : DBNull.Value,
            DecisionNote = (object?)sheet.DecisionNote ?? DBNull.Value,
            UpdatedAtUtc = sheet.UpdatedAtUtc
        }, uow.Transaction, ct);

        await InsertItemsAsync(uow, sheet.ControloSheetId, sheet.Items, ct);
        return sheet.ControloSheetId;
    }

    public async Task<ControloFolha?> GetByIdAsync(Guid sheetId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT controlo_sheet_id, job_on_id, job_on_revision_id, production_code, reference,
       machine_code, display_id, status, created_by, created_at_utc, submitted_by,
       submitted_at_utc, submitted_note, decided_by, decided_at_utc, decision,
       decision_note, updated_at_utc
FROM controlo_sheets WHERE controlo_sheet_id = @Id;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = sheetId }, cancellationToken: ct);
            if (row is null) return null;
            var sheet = MapHeader(row);
            await LoadItemsAndEventsAsync(conn, sheet, ct);
            return sheet;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<ControloFolha?> GetForProductionAsync(
        Guid jobOnId, Guid? jobOnRevisionId = null, CancellationToken ct = default)
    {
        var sql = $@"
SELECT controlo_sheet_id, job_on_id, job_on_revision_id, production_code, reference,
       machine_code, display_id, status, created_by, created_at_utc, submitted_by,
       submitted_at_utc, submitted_note, decided_by, decided_at_utc, decision,
       decision_note, updated_at_utc
FROM controlo_sheets
WHERE job_on_id = @JobOnId
{(jobOnRevisionId.HasValue ? "AND job_on_revision_id = @RevisionId" : "")}
ORDER BY created_at_utc DESC LIMIT 1;";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql,
                new { JobOnId = jobOnId, RevisionId = jobOnRevisionId }, cancellationToken: ct);
            if (row is null) return null;
            var sheet = MapHeader(row);
            await LoadItemsAndEventsAsync(conn, sheet, ct);
            return sheet;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<ControloFolha>> ListByProductionAsync(Guid jobOnId, CancellationToken ct = default)
        => await ListAsync(null, null, null, jobOnId, null, ct);

    public async Task<IReadOnlyList<ControloFolha>> ListAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? machineCode, Guid? jobOnId, string? status, CancellationToken ct = default)
    {
        var sql = @"
SELECT controlo_sheet_id, job_on_id, job_on_revision_id, production_code, reference,
       machine_code, display_id, status, created_by, created_at_utc, submitted_by,
       submitted_at_utc, submitted_note, decided_by, decided_at_utc, decision,
       decision_note, updated_at_utc
FROM controlo_sheets
WHERE (@From IS NULL OR created_at_utc >= @From)
  AND (@To IS NULL OR created_at_utc <= @To)
  AND (@Machine IS NULL OR machine_code = @Machine)
  AND (@JobOnId IS NULL OR job_on_id = @JobOnId)
  AND (@Status IS NULL OR status = @Status)
ORDER BY created_at_utc DESC;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { From = from, To = to, Machine = machineCode, JobOnId = jobOnId, Status = status }, cancellationToken: ct);
            var sheets = new List<ControloFolha>();
            foreach (var row in rows)
            {
                var sheet = MapHeader(row);
                await LoadItemsAndEventsAsync(conn, sheet, ct);
                sheets.Add(sheet);
            }
            return sheets.AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpdateAsync(IDbUnitOfWork uow, ControloFolha sheet, IReadOnlyList<ControloFolhaItem> currentItems, CancellationToken ct = default)
    {
        const string headerSql = @"
UPDATE controlo_sheets SET
    status = @Status,
    submitted_by = @SubmittedBy,
    submitted_at_utc = @SubmittedAtUtc,
    submitted_note = @SubmittedNote,
    decided_by = @DecidedBy,
    decided_at_utc = @DecidedAtUtc,
    decision = @Decision,
    decision_note = @DecisionNote,
    updated_at_utc = @UpdatedAtUtc
WHERE controlo_sheet_id = @Id;";
        await Db.ExecuteAsync(uow.Connection, headerSql, new
        {
            Id = sheet.ControloSheetId,
            Status = ControloFolhaStateCodec.ToStorage(sheet.State),
            SubmittedBy = (object?)sheet.SubmittedBy ?? DBNull.Value,
            SubmittedAtUtc = (object?)sheet.SubmittedAtUtc ?? DBNull.Value,
            SubmittedNote = (object?)sheet.SubmittedNote ?? DBNull.Value,
            DecidedBy = (object?)sheet.DecidedBy ?? DBNull.Value,
            DecidedAtUtc = (object?)sheet.DecidedAtUtc ?? DBNull.Value,
            Decision = sheet.Decision is { } d ? (object?)ControloFolhaStateCodec.ToStorage(d) : DBNull.Value,
            DecisionNote = (object?)sheet.DecisionNote ?? DBNull.Value,
            UpdatedAtUtc = sheet.UpdatedAtUtc
        }, uow.Transaction, ct);

        // Replace the item control facts (result/observation/mcaliper_link) for the sheet.
        const string clearSql = "UPDATE controlo_sheet_items SET result = NULL, observation = NULL, mcaliper_link = NULL WHERE controlo_sheet_id = @SheetId;";
        await Db.ExecuteAsync(uow.Connection, clearSql, new { SheetId = sheet.ControloSheetId }, uow.Transaction, ct);
        foreach (var item in currentItems)
        {
            const string itemSql = @"
UPDATE controlo_sheet_items
SET result = @Result, observation = @Observation, mcaliper_link = @McaliperLink
WHERE controlo_sheet_item_id = @ItemId AND controlo_sheet_id = @SheetId;";
            await Db.ExecuteAsync(uow.Connection, itemSql, new
            {
                ItemId = item.ControloSheetItemId,
                SheetId = sheet.ControloSheetId,
                Result = (object?)item.Result ?? DBNull.Value,
                Observation = (object?)item.Observation ?? DBNull.Value,
                McaliperLink = (object?)item.McaliperLink ?? DBNull.Value
            }, uow.Transaction, ct);
        }
    }

    public async Task InsertEventAsync(IDbUnitOfWork uow, ControloFolhaEvent evt, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO controlo_sheet_events
    (controlo_sheet_event_id, controlo_sheet_id, event_type, actor_id, occurred_at_utc,
     before_summary, after_summary, note)
VALUES
    (@Id, @SheetId, @EventType, @ActorId, @OccurredAtUtc, @Before, @After, @Note);";
        await Db.ExecuteAsync(uow.Connection, sql, new
        {
            Id = evt.ControloSheetEventId,
            SheetId = evt.ControloSheetId,
            EventType = evt.EventType,
            ActorId = (object?)evt.ActorId ?? DBNull.Value,
            OccurredAtUtc = evt.OccurredAtUtc,
            Before = (object?)evt.BeforeSummary ?? DBNull.Value,
            After = (object?)evt.AfterSummary ?? DBNull.Value,
            Note = (object?)evt.Note ?? DBNull.Value
        }, uow.Transaction, ct);
    }

    // ---- Private -----------------------------------------------------------

    private async Task InsertItemsAsync(IDbUnitOfWork uow, Guid sheetId, IReadOnlyList<ControloFolhaItem> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            // Rehydrate item's sheet id so FK is consistent.
            item.ControloSheetId = sheetId;
            const string sql = @"
INSERT INTO controlo_sheet_items
    (controlo_sheet_item_id, controlo_sheet_id, family, source_tool_id, source_lot_id,
     reference_snapshot, lot_snapshot, technical_name_snapshot, result, observation, mcaliper_link)
VALUES
    (@Id, @SheetId, @Family, @SourceToolId, @SourceLotId,
     @ReferenceSnapshot, @LotSnapshot, @TechnicalNameSnapshot, @Result, @Observation, @McaliperLink);";
            await Db.ExecuteAsync(uow.Connection, sql, new
            {
                Id = item.ControloSheetItemId,
                SheetId = sheetId,
                Family = item.Family,
                SourceToolId = (object?)item.SourceToolId ?? DBNull.Value,
                SourceLotId = (object?)item.SourceLotId ?? DBNull.Value,
                ReferenceSnapshot = (object?)item.ReferenceSnapshot ?? DBNull.Value,
                LotSnapshot = (object?)item.LotSnapshot ?? DBNull.Value,
                TechnicalNameSnapshot = (object?)item.TechnicalNameSnapshot ?? DBNull.Value,
                Result = (object?)item.Result ?? DBNull.Value,
                Observation = (object?)item.Observation ?? DBNull.Value,
                McaliperLink = (object?)item.McaliperLink ?? DBNull.Value
            }, uow.Transaction, ct);
        }
    }

    private async Task LoadItemsAndEventsAsync(IDbConnection conn, ControloFolha sheet, CancellationToken ct)
    {
        const string itemsSql = @"
SELECT controlo_sheet_item_id, controlo_sheet_id, family, source_tool_id, source_lot_id,
       reference_snapshot, lot_snapshot, technical_name_snapshot, result, observation, mcaliper_link
FROM controlo_sheet_items WHERE controlo_sheet_id = @SheetId ORDER BY family, reference_snapshot;";
        var items = await Db.QueryAsync<dynamic>(conn, itemsSql, new { SheetId = sheet.ControloSheetId }, cancellationToken: ct);
        sheet.SetItems(items.Select(r => new ControloFolhaItem
        {
            ControloSheetItemId = r.controlo_sheet_item_id,
            ControloSheetId = sheet.ControloSheetId,
            Family = (string)r.family,
            SourceToolId = r.source_tool_id as Guid?,
            SourceLotId = r.source_lot_id as Guid?,
            ReferenceSnapshot = r.reference_snapshot as string,
            LotSnapshot = r.lot_snapshot as string,
            TechnicalNameSnapshot = r.technical_name_snapshot as string,
            Result = r.result as string,
            Observation = r.observation as string,
            McaliperLink = r.mcaliper_link as string
        }).ToList());

        const string eventsSql = @"
SELECT controlo_sheet_event_id, controlo_sheet_id, event_type, actor_id, occurred_at_utc,
       before_summary, after_summary, note
FROM controlo_sheet_events WHERE controlo_sheet_id = @SheetId ORDER BY occurred_at_utc, controlo_sheet_event_id;";
        var events = await Db.QueryAsync<dynamic>(conn, eventsSql, new { SheetId = sheet.ControloSheetId }, cancellationToken: ct);
        sheet.SetEvents(events.Select(r => new ControloFolhaEvent(
            (Guid)r.controlo_sheet_event_id,
            sheet.ControloSheetId,
            (string)r.event_type,
            r.actor_id as string,
            (DateTimeOffset)r.occurred_at_utc,
            r.before_summary as string,
            r.after_summary as string,
            r.note as string)).ToList());
    }

    private static ControloFolha MapHeader(dynamic row) => new()
    {
        ControloSheetId = row.controlo_sheet_id,
        JobOnId = row.job_on_id,
        JobOnRevisionId = row.job_on_revision_id,
        ProductionCode = (string)row.production_code,
        Reference = (string)row.reference,
        MachineCode = (string)row.machine_code,
        DisplayId = (string)row.display_id,
        State = ControloFolhaStateCodec.FromStorage(row.status as string),
        CreatedBy = row.created_by as string,
        CreatedAtUtc = (DateTimeOffset)row.created_at_utc,
        SubmittedBy = row.submitted_by as string,
        SubmittedAtUtc = row.submitted_at_utc as DateTimeOffset?,
        SubmittedNote = row.submitted_note as string,
        DecidedBy = row.decided_by as string,
        DecidedAtUtc = row.decided_at_utc as DateTimeOffset?,
        Decision = row.decision is null || row.decision is DBNull
            ? null
            : ControloFolhaStateCodec.FromStorageDecision(row.decision as string),
        DecisionNote = row.decision_note as string,
        UpdatedAtUtc = (DateTimeOffset)row.updated_at_utc
    };

    private static async Task DisposeAsync(IDbConnection connection)
    {
        if (connection is IAsyncDisposable a) await a.DisposeAsync();
        else connection.Dispose();
    }
}