using System.Data;
using System.Text.Json;
using BA.Dmo.Application.Modules.Boquilhas;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Boquilhas;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-19 — Boquilhas Dapper persistence (N03 <c>bq_*</c>; GLM-BQ). Implements
/// <see cref="IBoquilhasRepository"/>. Read-only queries self-manage a connection;
/// all multi-row writes (lot+trace+START, movement+discrepancy, close, lifecycle,
/// audit) participate in the shared <see cref="IDbUnitOfWork"/> so they commit or
/// roll back atomically (GLM-DATA-05). <c>bq_movements</c>/<c>bq_lifecycle_history</c>
/// are append-only (DB triggers); a "delete" is a void recorded in
/// <c>bq_traces.deleted_movements</c>, never a physical delete.
/// </summary>
public sealed class DapperBoquilhasRepository : IBoquilhasRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperBoquilhasRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    // ---- Lots ----------------------------------------------------------------

    public async Task<BqLote?> GetLoteByIdAsync(Guid bqLoteId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT bq_lote_id, reference, batch_code, allowed_lines, lifecycle_state,
       created_by, created_at_utc, updated_at_utc
FROM bq_lotes WHERE bq_lote_id = @Id;";
        var conn = await Open(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = bqLoteId }, cancellationToken: ct);
            return row is null ? null : MapLote(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<BqLote?> GetLoteByReferenceBatchAsync(string reference, string batchCode, CancellationToken ct = default)
    {
        const string sql = @"
SELECT bq_lote_id, reference, batch_code, allowed_lines, lifecycle_state,
       created_by, created_at_utc, updated_at_utc
FROM bq_lotes WHERE reference = @Reference AND batch_code = @BatchCode;";
        var conn = await Open(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Reference = reference, BatchCode = batchCode }, cancellationToken: ct);
            return row is null ? null : MapLote(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<BqLote>> ListLotesAsync(BqLoteFilter filter, CancellationToken ct = default)
    {
        var sql = @"
SELECT bq_lote_id, reference, batch_code, allowed_lines, lifecycle_state,
       created_by, created_at_utc, updated_at_utc
FROM bq_lotes
WHERE (@Search IS NULL OR reference ILIKE '%' || @Search || '%' OR batch_code ILIKE '%' || @Search || '%')
  AND (@OnlyAvailable = FALSE OR lifecycle_state = 'available')
  AND (@Lifecycle IS NULL OR lifecycle_state = @Lifecycle)
ORDER BY reference, batch_code
LIMIT @PageSize OFFSET @Offset;";
        var conn = await Open(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                Search = filter.Search,
                OnlyAvailable = filter.OnlyAvailable == true,
                Lifecycle = filter.LifecycleState is null ? null : BqLifecycleStateCodec.ToStorage(filter.LifecycleState.Value),
                PageSize = filter.PageSize,
                Offset = (filter.Page - 1) * filter.PageSize
            }, cancellationToken: ct);
            return rows.Select<dynamic, BqLote>(MapLote).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<int> CountLotesAsync(BqLoteFilter filter, CancellationToken ct = default)
    {
        const string sql = @"
SELECT COUNT(*) FROM bq_lotes
WHERE (@Search IS NULL OR reference ILIKE '%' || @Search || '%' OR batch_code ILIKE '%' || @Search || '%')
  AND (@OnlyAvailable = FALSE OR lifecycle_state = 'available')
  AND (@Lifecycle IS NULL OR lifecycle_state = @Lifecycle);";
        var conn = await Open(ct);
        try
        {
            return await Db.ExecuteScalarAsync<int>(conn, sql, new
            {
                Search = filter.Search,
                OnlyAvailable = filter.OnlyAvailable == true,
                Lifecycle = filter.LifecycleState is null ? null : BqLifecycleStateCodec.ToStorage(filter.LifecycleState.Value)
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public Task CreateLoteAsync(IDbUnitOfWork uow, BqLote lote, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO bq_lotes (bq_lote_id, reference, batch_code, allowed_lines, lifecycle_state,
                      created_by, created_at_utc, updated_at_utc)
VALUES (@Id, @Reference, @BatchCode, @AllowedLines, @LifecycleState,
        @CreatedBy, @CreatedAtUtc, @UpdatedAtUtc);";
        return Db.ExecuteAsync(uow.Connection, sql, new
        {
            Id = lote.BqLoteId, lote.Reference, lote.BatchCode, AllowedLines = lote.AllowedLines.ToArray(),
            LifecycleState = BqLifecycleStateCodec.ToStorage(lote.LifecycleState),
            CreatedBy = (object?)lote.CreatedBy ?? DBNull.Value, lote.CreatedAtUtc, lote.UpdatedAtUtc
        }, uow.Transaction, ct);
    }

    public Task UpdateLoteAsync(IDbUnitOfWork uow, BqLote lote, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE bq_lotes SET reference = @Reference, batch_code = @BatchCode,
       allowed_lines = @AllowedLines, updated_at_utc = @UpdatedAtUtc
WHERE bq_lote_id = @Id;";
        return Db.ExecuteAsync(uow.Connection, sql, new
        {
            Id = lote.BqLoteId, lote.Reference, lote.BatchCode, AllowedLines = lote.AllowedLines.ToArray(), lote.UpdatedAtUtc
        }, uow.Transaction, ct);
    }

    public Task UpdateLifecycleStateAsync(IDbUnitOfWork uow, Guid bqLoteId, BqLifecycleState state, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
UPDATE bq_lotes SET lifecycle_state = @State, updated_at_utc = now() WHERE bq_lote_id = @Id;",
            new { Id = bqLoteId, State = BqLifecycleStateCodec.ToStorage(state) }, uow.Transaction, ct);

    public Task InsertLifecycleEventAsync(IDbUnitOfWork uow, BqLifecycleEvent evt, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
INSERT INTO bq_lifecycle_history (bq_lifecycle_history_id, bq_lote_id, event, reason, actor_id, occurred_at_utc)
VALUES (@Id, @LoteId, @Event, @Reason, @ActorId, @OccurredAtUtc);", new
        {
            Id = evt.BqLifecycleEventId, LoteId = evt.BqLoteId, Event = BqLifecycleEventKindCodec.ToStorage(evt.Kind),
            Reason = (object?)evt.Reason ?? DBNull.Value, ActorId = (object?)evt.ActorId ?? DBNull.Value, evt.OccurredAtUtc
        }, uow.Transaction, ct);

    // ---- Traces ----------------------------------------------------------------

    public async Task<BqTrace?> GetTraceByIdAsync(Guid bqTraceId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT bq_trace_id, bq_lote_id, status, purpose, start_line, sap_start, sap_end,
       reopen_history, deleted_movements, created_by, created_at_utc, updated_at_utc
FROM bq_traces WHERE bq_trace_id = @Id;";
        var conn = await Open(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = bqTraceId }, cancellationToken: ct);
            return row is null ? null : MapTrace(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<BqTrace?> GetActiveTraceForLoteAsync(Guid bqLoteId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT bq_trace_id, bq_lote_id, status, purpose, start_line, sap_start, sap_end,
       reopen_history, deleted_movements, created_by, created_at_utc, updated_at_utc
FROM bq_traces WHERE bq_lote_id = @LoteId AND status = 'active' LIMIT 1;";
        var conn = await Open(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { LoteId = bqLoteId }, cancellationToken: ct);
            return row is null ? null : MapTrace(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<BqTrace?> GetLastClosedOrActiveTraceAsync(Guid bqLoteId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT bq_trace_id, bq_lote_id, status, purpose, start_line, sap_start, sap_end,
       reopen_history, deleted_movements, created_by, created_at_utc, updated_at_utc
FROM bq_traces WHERE bq_lote_id = @LoteId ORDER BY created_at_utc DESC LIMIT 1;";
        var conn = await Open(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { LoteId = bqLoteId }, cancellationToken: ct);
            return row is null ? null : MapTrace(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<BqTrace?> GetTraceForMovementAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default)
    {
        dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(uow.Connection, @"
SELECT bq_trace_id, bq_lote_id, status, purpose, start_line, sap_start, sap_end,
       reopen_history, deleted_movements, created_by, created_at_utc, updated_at_utc
FROM bq_traces WHERE bq_trace_id = @Id;", new { Id = bqTraceId }, uow.Transaction, cancellationToken: ct);
        return row is null ? null : MapTrace(row);
    }

    public Task CreateTraceAsync(IDbUnitOfWork uow, BqTrace trace, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
INSERT INTO bq_traces (bq_trace_id, bq_lote_id, status, purpose, start_line, sap_start, sap_end,
                       reopen_history, deleted_movements, created_by, created_at_utc, updated_at_utc)
VALUES (@Id, @LoteId, @Status, @Purpose, @StartLine, @SapStart, @SapEnd,
        @ReopenHistory, @DeletedMovements, @CreatedBy, @CreatedAtUtc, @UpdatedAtUtc);", new
        {
            Id = trace.BqTraceId, LoteId = trace.BqLoteId, Status = BqTraceStatusCodec.ToStorage(trace.Status),
            Purpose = BqTracePurposeCodec.ToStorage(trace.Purpose), StartLine = (object?)trace.StartLine ?? DBNull.Value,
            SapStart = (object?)trace.SapStart ?? DBNull.Value, SapEnd = (object?)trace.SapEnd ?? DBNull.Value,
            ReopenHistory = trace.ReopenHistory ?? "[]", DeletedMovements = trace.DeletedMovements ?? "[]",
            CreatedBy = (object?)trace.CreatedBy ?? DBNull.Value, trace.CreatedAtUtc, trace.UpdatedAtUtc
        }, uow.Transaction, ct);

    public Task CloseTraceAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
UPDATE bq_traces SET status = 'closed', updated_at_utc = now() WHERE bq_trace_id = @Id;",
            new { Id = bqTraceId }, uow.Transaction, ct);

    public Task ReopenTraceAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
UPDATE bq_traces SET status = 'active', updated_at_utc = now() WHERE bq_trace_id = @Id;",
            new { Id = bqTraceId }, uow.Transaction, ct);

    public Task AppendReopenHistoryAsync(IDbUnitOfWork uow, Guid bqTraceId, string actorId, DateTimeOffset atUtc, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
UPDATE bq_traces
SET reopen_history = coalesce(reopen_history, '[]'::jsonb) || jsonb_build_object('actor', @Actor, 'at', @At),
    updated_at_utc = now()
WHERE bq_trace_id = @Id;", new { Id = bqTraceId, Actor = actorId, At = atUtc.ToString("o") }, uow.Transaction, ct);

    // ---- Movements ----------------------------------------------------------------

    public Task InsertMovementAsync(IDbUnitOfWork uow, BqMovement movement, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
INSERT INTO bq_movements (bq_movement_id, bq_trace_id, movement_type, qty, exceptional_received_qty,
                          line, noted_repairer_id, notes, occurred_at_utc, actor_id)
VALUES (@Id, @TraceId, @Type, @Qty, @Exceptional, @Line, @RepairerId, @Notes, @OccurredAtUtc, @ActorId);", new
        {
            Id = movement.BqMovementId, TraceId = movement.BqTraceId, Type = BqMovementTypeCodec.ToStorage(movement.MovementType),
            Qty = (object?)movement.Qty ?? DBNull.Value, Exceptional = (object?)movement.ExceptionalReceivedQty ?? DBNull.Value,
            Line = (object?)movement.Line ?? DBNull.Value, RepairerId = (object?)movement.RepairerId ?? DBNull.Value,
            Notes = (object?)movement.Notes ?? DBNull.Value, movement.OccurredAtUtc, ActorId = (object?)movement.ActorId ?? DBNull.Value
        }, uow.Transaction, ct);

    public async Task<IReadOnlyList<BqMovement>> ListMovementsForTraceAsync(Guid bqTraceId, CancellationToken ct = default)
    {
        var conn = await Open(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, @"
SELECT bq_movement_id, bq_trace_id, movement_type, qty, exceptional_received_qty,
       line, noted_repairer_id, notes, occurred_at_utc, actor_id
FROM bq_movements WHERE bq_trace_id = @TraceId ORDER BY occurred_at_utc, bq_movement_id;",
                new { TraceId = bqTraceId }, cancellationToken: ct);
            return rows.Select<dynamic, BqMovement>(MapMovement).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<BqMovement>> ListMovementsByLoteAsync(Guid bqLoteId, BqHistoryFilter filter, CancellationToken ct = default)
        => await ListMovementsAsync(filter with { BqLoteId = bqLoteId }, ct);

    public async Task<IReadOnlyList<BqMovement>> ListMovementsAsync(BqHistoryFilter filter, CancellationToken ct = default)
    {
        var sql = @"
SELECT m.bq_movement_id, m.bq_trace_id, m.movement_type, m.qty, m.exceptional_received_qty,
       m.line, m.noted_repairer_id, m.notes, m.occurred_at_utc, m.actor_id
FROM bq_movements m
JOIN bq_traces t ON t.bq_trace_id = m.bq_trace_id
JOIN bq_lotes l ON l.bq_lote_id = t.bq_lote_id
WHERE (@LoteId IS NULL OR t.bq_lote_id = @LoteId)
  AND (@Type IS NULL OR m.movement_type = @Type)
  AND (@RepairerId IS NULL OR m.noted_repairer_id = @RepairerId)
  AND (@From IS NULL OR m.occurred_at_utc >= @From)
  AND (@To IS NULL OR m.occurred_at_utc <= @To)
  AND (@Search IS NULL OR l.reference ILIKE @SearchLike OR l.batch_code ILIKE @SearchLike OR m.line ILIKE @SearchLike)
ORDER BY m.occurred_at_utc DESC
LIMIT @PageSize OFFSET @Offset;";
        var conn = await Open(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                LoteId = filter.BqLoteId, Type = filter.MovementType is null ? null : BqMovementTypeCodec.ToStorage(filter.MovementType.Value),
                RepairerId = filter.RepairerId, From = filter.From, To = filter.To,
                Search = filter.Search, SearchLike = LikePattern(filter.Search),
                PageSize = filter.PageSize, Offset = (filter.Page - 1) * filter.PageSize
            }, cancellationToken: ct);
            return rows.Select<dynamic, BqMovement>(MapMovement).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<int> CountMovementsAsync(BqHistoryFilter filter, CancellationToken ct = default)
    {
        var sql = @"
SELECT COUNT(*) FROM bq_movements m
JOIN bq_traces t ON t.bq_trace_id = m.bq_trace_id
JOIN bq_lotes l ON l.bq_lote_id = t.bq_lote_id
WHERE (@LoteId IS NULL OR t.bq_lote_id = @LoteId)
  AND (@Type IS NULL OR m.movement_type = @Type)
  AND (@RepairerId IS NULL OR m.noted_repairer_id = @RepairerId)
  AND (@From IS NULL OR m.occurred_at_utc >= @From)
  AND (@To IS NULL OR m.occurred_at_utc <= @To)
  AND (@Search IS NULL OR l.reference ILIKE @SearchLike OR l.batch_code ILIKE @SearchLike OR m.line ILIKE @SearchLike);";
        var conn = await Open(ct);
        try
        {
            return await Db.ExecuteScalarAsync<int>(conn, sql, new
            {
                LoteId = filter.BqLoteId, Type = filter.MovementType is null ? null : BqMovementTypeCodec.ToStorage(filter.MovementType.Value),
                RepairerId = filter.RepairerId, From = filter.From, To = filter.To,
                Search = filter.Search, SearchLike = LikePattern(filter.Search)
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public Task VoidMovementAsync(IDbUnitOfWork uow, Guid bqTraceId, Guid bqMovementId, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
UPDATE bq_traces
SET deleted_movements = deleted_movements || to_jsonb(@MovementId::text),
    updated_at_utc = now()
WHERE bq_trace_id = @TraceId;", new { TraceId = bqTraceId, MovementId = bqMovementId.ToString() }, uow.Transaction, ct);

    public async Task<IReadOnlySet<Guid>> ListVoidedMovementIdsAsync(Guid bqTraceId, CancellationToken ct = default)
    {
        const string sql = "SELECT deleted_movements FROM bq_traces WHERE bq_trace_id = @Id;";
        var conn = await Open(ct);
        try
        {
            var raw = await Db.ExecuteScalarAsync<string>(conn, sql, new { Id = bqTraceId }, cancellationToken: ct);
            return ParseGuidJsonArray(raw);
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Utilisation ----------------------------------------------------------------

    public Task InsertUtilisationReadingAsync(IDbUnitOfWork uow, BqUtilisationReading reading, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
INSERT INTO bq_utilisation_readings (bq_utilisation_reading_id, bq_trace_id, reading_kind, value, actor_id, occurred_at_utc)
VALUES (@Id, @TraceId, @Kind, @Value, @ActorId, @OccurredAtUtc);", new
        {
            Id = reading.BqUtilisationReadingId, TraceId = reading.BqTraceId, Kind = BqUtilisationReadingKindCodec.ToStorage(reading.ReadingKind),
            Value = reading.Value, ActorId = (object?)reading.ActorId ?? DBNull.Value, reading.OccurredAtUtc
        }, uow.Transaction, ct);

    public async Task<BqUtilisationReading?> GetUtilisationReadingAsync(Guid bqTraceId, BqUtilisationReadingKind kind, CancellationToken ct = default)
    {
        const string sql = "SELECT value, occurred_at_utc FROM bq_utilisation_readings WHERE bq_trace_id = @TraceId AND reading_kind = @Kind ORDER BY occurred_at_utc DESC LIMIT 1;";
        var conn = await Open(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { TraceId = bqTraceId, Kind = BqUtilisationReadingKindCodec.ToStorage(kind) }, cancellationToken: ct);
            return row is null ? null : new BqUtilisationReading { BqTraceId = bqTraceId, ReadingKind = kind, Value = row.value, ActorId = null, OccurredAtUtc = row.occurred_at_utc };
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Discrepancies ----------------------------------------------------------------

    public async Task<BqDiscrepancy?> GetOpenDiscrepancyForTraceAsync(Guid bqLoteId, Guid? bqTraceId, CancellationToken ct = default)
    {
        var conn = await Open(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, @"
SELECT bq_discrepancy_id, bq_lote_id, bq_trace_id, expected_qty, actual_qty, excess_qty,
       status, resolution_note, resolved_by, resolved_at_utc, created_by, created_at_utc
FROM bq_discrepancies
WHERE bq_lote_id = @LoteId AND (@TraceId IS NULL OR bq_trace_id = @TraceId) AND status = 'open'
ORDER BY created_at_utc DESC LIMIT 1;", new { LoteId = bqLoteId, TraceId = bqTraceId }, cancellationToken: ct);
            return row is null ? null : MapDiscrepancy(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public Task InsertDiscrepancyAsync(IDbUnitOfWork uow, BqDiscrepancy discrepancy, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
INSERT INTO bq_discrepancies (bq_discrepancy_id, bq_lote_id, bq_trace_id, expected_qty, actual_qty, excess_qty,
                              status, resolution_note, resolved_by, resolved_at_utc, created_by, created_at_utc)
VALUES (@Id, @LoteId, @TraceId, @Expected, @Actual, @Excess, @Status, @ResolutionNote,
        @ResolvedBy, @ResolvedAtUtc, @CreatedBy, @CreatedAtUtc);", new
        {
            Id = discrepancy.BqDiscrepancyId, LoteId = discrepancy.BqLoteId, TraceId = (object?)discrepancy.BqTraceId ?? DBNull.Value,
            Expected = discrepancy.ExpectedQty, Actual = discrepancy.ActualQty, Excess = discrepancy.ExcessQty,
            Status = BqDiscrepancyStatusCodec.ToStorage(discrepancy.Status),
            ResolutionNote = (object?)discrepancy.ResolutionNote ?? DBNull.Value,
            ResolvedBy = (object?)discrepancy.ResolvedBy ?? DBNull.Value, ResolvedAtUtc = (object?)discrepancy.ResolvedAtUtc ?? DBNull.Value,
            CreatedBy = (object?)discrepancy.CreatedBy ?? DBNull.Value, discrepancy.CreatedAtUtc
        }, uow.Transaction, ct);

    public Task UpdateDiscrepancyAsync(IDbUnitOfWork uow, BqDiscrepancy discrepancy, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
UPDATE bq_discrepancies SET status = @Status, resolution_note = @ResolutionNote,
       resolved_by = @ResolvedBy, resolved_at_utc = @ResolvedAtUtc
WHERE bq_discrepancy_id = @Id;", new
        {
            Id = discrepancy.BqDiscrepancyId, Status = BqDiscrepancyStatusCodec.ToStorage(discrepancy.Status),
            ResolutionNote = (object?)discrepancy.ResolutionNote ?? DBNull.Value,
            ResolvedBy = (object?)discrepancy.ResolvedBy ?? DBNull.Value, ResolvedAtUtc = (object?)discrepancy.ResolvedAtUtc ?? DBNull.Value
        }, uow.Transaction, ct);

    public async Task<IReadOnlyList<BqDiscrepancy>> ListDiscrepanciesAsync(Guid? bqLoteId, CancellationToken ct = default)
    {
        var conn = await Open(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, @"
SELECT bq_discrepancy_id, bq_lote_id, bq_trace_id, expected_qty, actual_qty, excess_qty,
       status, resolution_note, resolved_by, resolved_at_utc, created_by, created_at_utc
FROM bq_discrepancies WHERE (@LoteId IS NULL OR bq_lote_id = @LoteId) ORDER BY created_at_utc DESC;",
                new { LoteId = bqLoteId }, cancellationToken: ct);
            return rows.Select<dynamic, BqDiscrepancy>(MapDiscrepancy).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Repairers (canonical vocabulary: repairers / line_repairer_defaults,
    //      tool_type='BQ'; TD-15, N08). Reused, not duplicated. Boquilhas never
    //      creates parallel repairer tables (U-19 shared-contract rule).

    public async Task<IReadOnlyList<BqRepairer>> ListRepairersAsync(bool onlyActive, CancellationToken ct = default)
    {
        var conn = await Open(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, @"
SELECT r.repairer_id, r.name, r.active, r.created_at_utc, r.updated_at_utc
FROM repairers r WHERE (@OnlyActive = FALSE OR r.active = TRUE) ORDER BY r.name;",
                new { OnlyActive = onlyActive }, cancellationToken: ct);

            // Bulk-load supported_types for all returned repairers in one query (UD-03).
            var ids = rows.Select(r => r.repairer_id).ToArray();
            var typesMap = ids.Length > 0
                ? await Db.QueryAsync<dynamic>(conn,
                    @"SELECT repairer_id, repair_type FROM repairer_repair_types WHERE repairer_id = ANY(@Ids);",
                    new { Ids = ids }, cancellationToken: ct)
                : Array.Empty<dynamic>();

            // Group types by repairer_id
            var grouped = new Dictionary<Guid, HashSet<string>>();
            foreach (var t in typesMap) {
                var id = (Guid)t.repairer_id;
                if (!grouped.TryGetValue(id, out var set)) {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    grouped[id] = set;
                }
                set.Add(t.repair_type.ToUpperInvariant());
            }

            return rows.Select<dynamic, BqRepairer>(r => MapRepairerWithTypes(r, grouped)).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<BqRepairer?> GetRepairerByIdAsync(Guid repairerId, CancellationToken ct = default)
    {
        var conn = await Open(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, @"
SELECT repairer_id, name, active, created_at_utc, updated_at_utc
FROM repairers WHERE repairer_id = @Id;", new { Id = repairerId }, cancellationToken: ct);

            if (row is null) return null;

            // Also load supported_types for this single repairer.
            var types = await Db.QueryAsync<dynamic>(conn,
                @"SELECT repair_type FROM repairer_repair_types WHERE repairer_id = @Id;",
                new { Id = repairerId }, cancellationToken: ct);

            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in types) set.Add(t.repair_type.ToUpperInvariant());

            var r = new BqRepairer
            {
                RepairerId = row.repairer_id,
                Name = row.name,
                Active = row.active,
                SupportedTypes = set,
                CreatedAtUtc = (DateTimeOffset)row.created_at_utc,
                UpdatedAtUtc = (DateTimeOffset)row.updated_at_utc
            };
            return r;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<Guid> CreateRepairerAsync(BqRepairer repairer, CancellationToken ct = default)
    {
        var conn = await Open(ct);
        try
        {
            await Db.ExecuteAsync(conn, @"
INSERT INTO repairers (repairer_id, name, active, created_at_utc, updated_at_utc)
VALUES (@Id, @Name, @Active, @CreatedAtUtc, @UpdatedAtUtc);", new
            {
                Id = repairer.RepairerId, repairer.Name, repairer.Active,
                repairer.CreatedAtUtc, repairer.UpdatedAtUtc
            }, cancellationToken: ct);
            return repairer.RepairerId;
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task UpdateRepairerAsync(BqRepairer repairer, CancellationToken ct = default)
    {
        var conn = await Open(ct);
        try
        {
            if (!repairer.Active)
                await Db.ExecuteAsync(conn, "UPDATE repairers SET active = FALSE, updated_at_utc = now() WHERE repairer_id = @Id;",
                    new { Id = repairer.RepairerId }, cancellationToken: ct);
            else
                await Db.ExecuteAsync(conn, "UPDATE repairers SET name = @Name, updated_at_utc = @UpdatedAtUtc WHERE repairer_id = @Id;",
                    new { Id = repairer.RepairerId, Name = repairer.Name, UpdatedAtUtc = repairer.UpdatedAtUtc }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<BqLineRepairerDefault?> GetLineRepairerDefaultAsync(string line, CancellationToken ct = default)
    {
        var conn = await Open(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, @"
SELECT line, tool_type, repairer_id
FROM line_repairer_defaults WHERE line = @Line AND tool_type = 'BQ';",
                new { Line = line }, cancellationToken: ct);
            return row is null ? null : new BqLineRepairerDefault
            {
                Line = line,
                DefaultRepairerId = row.repairer_id as Guid?
            };
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task SetLineRepairerDefaultAsync(BqLineRepairerDefault lineDefault, CancellationToken ct = default)
    {
        if (lineDefault.DefaultRepairerId is null)
        {
            // "Sem associação" for a line default is not representable in the
            // canonical NOT NULL table; removing the row restores the unsets
            // default (the per-movement repairer remains independent).
            var conn = await Open(ct);
            try
            {
                await Db.ExecuteAsync(conn,
                    "DELETE FROM line_repairer_defaults WHERE line = @Line AND tool_type = 'BQ';",
                    new { Line = lineDefault.Line }, cancellationToken: ct);
            }
            finally { await DisposeAsync(conn); }
            return;
        }

        var openConn = await Open(ct);
        try
        {
            await Db.ExecuteAsync(openConn, @"
INSERT INTO line_repairer_defaults (line, tool_type, repairer_id, updated_at_utc)
VALUES (@Line, 'BQ', @RepairerId, now())
ON CONFLICT (line, tool_type)
DO UPDATE SET repairer_id = @RepairerId, updated_at_utc = now();",
                new { Line = lineDefault.Line, RepairerId = lineDefault.DefaultRepairerId.Value }, cancellationToken: ct);
        }
        finally { await DisposeAsync(openConn); }
    }

    // ---- Audit ----------------------------------------------------------------

    public Task InsertAuditEventAsync(IDbUnitOfWork uow, string actionCode, string entityType, string entityId,
        string result, string? beforeSummary, string? afterSummary, string actorId,
        DateTimeOffset occurredAtUtc, CancellationToken ct = default)
        => Db.ExecuteAsync(uow.Connection, @"
INSERT INTO audit_events (occurred_at_utc, year, actor_user_id, module_id, action_code,
                          entity_type, entity_id, result, before_summary, after_summary)
VALUES (@OccurredAtUtc, EXTRACT(YEAR FROM @OccurredAtUtc), @Actor, 'boquilhas', @Action,
        @EntityType, @EntityId, @Result, @Before, @After);", new
        {
            OccurredAtUtc = occurredAtUtc, Actor = actorId, Action = actionCode,
            EntityType = entityType, EntityId = entityId, Result = result,
            Before = (object?)beforeSummary ?? DBNull.Value, After = (object?)afterSummary ?? DBNull.Value
        }, uow.Transaction, ct);

    // ---- Mapping ----------------------------------------------------------------

    private static BqLote MapLote(dynamic row) => new()
    {
        BqLoteId = row.bq_lote_id,
        Reference = row.reference,
        BatchCode = row.batch_code,
        AllowedLines = ((string[])row.allowed_lines)?.ToList() ?? new List<string>(),
        LifecycleState = BqLifecycleStateCodec.FromStorage(row.lifecycle_state as string),
        CreatedBy = row.created_by as string,
        CreatedAtUtc = (DateTimeOffset)row.created_at_utc,
        UpdatedAtUtc = (DateTimeOffset)row.updated_at_utc
    };

    private static BqTrace MapTrace(dynamic row) => new()
    {
        BqTraceId = row.bq_trace_id,
        BqLoteId = row.bq_lote_id,
        Status = BqTraceStatusCodec.FromStorage(row.status as string),
        Purpose = BqTracePurposeCodec.FromStorage(row.purpose as string),
        StartLine = row.start_line as string,
        SapStart = row.sap_start as decimal?,
        SapEnd = row.sap_end as decimal?,
        ReopenHistory = row.reopen_history as string,
        DeletedMovements = row.deleted_movements as string,
        CreatedBy = row.created_by as string,
        CreatedAtUtc = (DateTimeOffset)row.created_at_utc,
        UpdatedAtUtc = (DateTimeOffset)row.updated_at_utc
    };

    private static BqMovement MapMovement(dynamic row) => new()
    {
        BqMovementId = row.bq_movement_id,
        BqTraceId = row.bq_trace_id,
        MovementType = BqMovementTypeCodec.FromStorage(row.movement_type as string),
        Qty = row.qty as decimal?,
        ExceptionalReceivedQty = row.exceptional_received_qty as decimal?,
        Line = row.line as string,
        RepairerId = row.noted_repairer_id as Guid?,
        Notes = row.notes as string,
        OccurredAtUtc = (DateTimeOffset)row.occurred_at_utc,
        ActorId = row.actor_id as string
    };

    private static BqDiscrepancy MapDiscrepancy(dynamic row) => new()
    {
        BqDiscrepancyId = row.bq_discrepancy_id,
        BqLoteId = row.bq_lote_id,
        BqTraceId = row.bq_trace_id as Guid?,
        ExpectedQty = row.expected_qty,
        ActualQty = row.actual_qty,
        ExcessQty = row.excess_qty,
        Status = BqDiscrepancyStatusCodec.FromStorage(row.status as string),
        ResolutionNote = row.resolution_note as string,
        ResolvedBy = row.resolved_by as string,
        ResolvedAtUtc = row.resolved_at_utc as DateTimeOffset?,
        CreatedBy = row.created_by as string,
        CreatedAtUtc = (DateTimeOffset)row.created_at_utc
    };

    private static BqRepairer MapRepairerWithTypes(dynamic row, Dictionary<Guid, HashSet<string>> typesMap)
    {
        var id = row.repairer_id;
        HashSet<string> t;
        var set = typesMap.TryGetValue(id, out t) ? new HashSet<string>(t, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        return new BqRepairer
        {
            RepairerId = id,
            Name = row.name,
            Active = row.active,
            SupportedTypes = set,
            CreatedAtUtc = (DateTimeOffset)row.created_at_utc,
            UpdatedAtUtc = (DateTimeOffset)row.updated_at_utc
        };
    }

    private static BqRepairer MapRepairer(dynamic row) => new()
    {
        RepairerId = row.repairer_id,
        Name = row.name,
        Active = row.active,
        CreatedAtUtc = (DateTimeOffset)row.created_at_utc,
        UpdatedAtUtc = (DateTimeOffset)row.updated_at_utc
    };

    private static IReadOnlySet<Guid> ParseGuidJsonArray(string? json)
    {
        var set = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(json)) return set;
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
                if (el.ValueKind == JsonValueKind.String && Guid.TryParse(el.GetString(), out var g))
                    set.Add(g);
        }
        catch (JsonException)
        {
            // Ignore malformed history; never fails the read.
        }
        return set;
    }

    private async Task<System.Data.IDbConnection> Open(CancellationToken ct) =>
        await _connectionFactory.OpenConnectionAsync(ct);

    /// <summary>Escapes a free-text search term into a case-insensitive SQL LIKE pattern.</summary>
    private static string? LikePattern(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return null;
        var escaped = search.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        return "%" + escaped + "%";
    }

    private static async Task DisposeAsync(System.Data.IDbConnection connection)
    {
        if (connection is IAsyncDisposable a) await a.DisposeAsync();
        else connection.Dispose();
    }
}