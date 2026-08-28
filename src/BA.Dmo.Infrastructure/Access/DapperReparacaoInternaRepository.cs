using System.Data;
using BA.Dmo.Application.Modules.ReparacaoInterna;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.ReparacaoInterna;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-16 — Reparação Interna Dapper persistence (N08 <c>internal_repair_records</c>
/// + <c>repair_events</c> scope interna + global <c>audit_events</c>). Implements
/// <see cref="IReparacaoInternaRepository"/>. The coordinated register/correction
/// write participates in the shared <see cref="IDbUnitOfWork"/> so the record, its
/// repair_event and the audit_events row commit/roll back atomically
/// (GLM-DATA-07). Append-only triggers and RLS are respected.
/// </summary>
public sealed class DapperReparacaoInternaRepository : IReparacaoInternaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperReparacaoInternaRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    // ---- Records --------------------------------------------------------------

    public async Task<Guid> InsertAsync(IDbUnitOfWork uow, InternalRepairRecord record, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO internal_repair_records
    (internal_repair_record_id, line, job_on_id, job_on_revision_id, production_code,
     reference, lot_id, tool_type, individual_number,
     operator_id, occurred_at_utc, correction_of_id, before_snapshot,
     correction_reason, created_at_utc, created_by)
VALUES
    (@Id, @Line, @JobOnId, @JobOnRevisionId, @ProductionCode,
     @Reference, @LotId, @ToolType, @IndividualNumber,
     @OperatorId, @OccurredAtUtc, @CorrectionOfId, @BeforeSnapshot,
     @CorrectionReason, @CreatedAtUtc, @CreatedBy);";
        await Db.ExecuteAsync(uow.Connection, sql, new
        {
            Id = record.InternalRepairRecordId,
            Line = record.Line,
            JobOnId = (object?)record.JobOnId ?? DBNull.Value,
            JobOnRevisionId = (object?)record.JobOnRevisionId ?? DBNull.Value,
            ProductionCode = (object?)record.ProductionCode ?? DBNull.Value,
            Reference = (object?)record.Reference ?? DBNull.Value,
            LotId = (object?)record.LotId ?? DBNull.Value,
            ToolType = InternalRepairToolTypeCodec.ToStorage(record.ToolType),
            IndividualNumber = record.IndividualNumber,
            OperatorId = (object?)record.OperatorId ?? DBNull.Value,
            OccurredAtUtc = record.OccurredAtUtc,
            CorrectionOfId = (object?)record.CorrectionOfId ?? DBNull.Value,
            BeforeSnapshot = (object?)record.BeforeSnapshot ?? DBNull.Value,
            CorrectionReason = (object?)record.CorrectionReason ?? DBNull.Value,
            CreatedAtUtc = record.CreatedAtUtc,
            CreatedBy = (object?)record.CreatedBy ?? DBNull.Value
        }, uow.Transaction, ct);
        return record.InternalRepairRecordId;
    }

    public async Task<InternalRepairRecord?> GetByIdAsync(Guid recordId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT internal_repair_record_id, line, job_on_id, job_on_revision_id, production_code,
       reference, lot_id, tool_type, individual_number,
       operator_id, occurred_at_utc, correction_of_id, before_snapshot,
       correction_reason, created_at_utc, created_by
FROM internal_repair_records
WHERE internal_repair_record_id = @Id;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = recordId }, cancellationToken: ct);
            return row is null ? null : MapRecord(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<InternalRepairRecord>> GetChainAsync(Guid rootRecordId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT internal_repair_record_id, line, job_on_id, job_on_revision_id, production_code,
       reference, lot_id, tool_type, individual_number,
       operator_id, occurred_at_utc, correction_of_id, before_snapshot,
       correction_reason, created_at_utc, created_by
FROM internal_repair_records
WHERE internal_repair_record_id = @RootId OR correction_of_id = @RootId
ORDER BY created_at_utc ASC;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { RootId = rootRecordId }, cancellationToken: ct);
            return rows.Select<dynamic, InternalRepairRecord>(MapRecord).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<InternalRepairRecord>> ListAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? line, Guid? jobOnId,
        InternalRepairToolType? type, string? number, string? operatorId,
        bool onlyCorrected, CancellationToken ct = default)
    {
        // Each correction chain root shows its latest valid version (brief §10).
        var sql = @"
SELECT DISTINCT ON (root_id) *
FROM (
    SELECT r.internal_repair_record_id, r.line, r.job_on_id, r.job_on_revision_id, r.production_code,
           r.reference, r.lot_id, r.tool_type, r.individual_number,
           r.operator_id, r.occurred_at_utc, r.correction_of_id, r.before_snapshot,
           r.correction_reason, r.created_at_utc, r.created_by,
           COALESCE(r.correction_of_id, r.internal_repair_record_id) AS root_id
    FROM internal_repair_records r
    WHERE (@From IS NULL OR r.occurred_at_utc >= @From)
      AND (@To IS NULL OR r.occurred_at_utc <= @To)
      AND (@Line IS NULL OR r.line = @Line)
      AND (@JobOnId IS NULL OR r.job_on_id = @JobOnId)
      AND (@Type IS NULL OR r.tool_type = @Type)
      AND (@Number IS NULL OR r.individual_number = @Number)
      AND (@OperatorId IS NULL OR r.operator_id = @OperatorId)
      AND (@OnlyCorrected = FALSE OR r.correction_of_id IS NOT NULL)
) s
ORDER BY root_id, created_at_utc DESC;";
        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                From = from,
                To = to,
                Line = line,
                JobOnId = jobOnId,
                Type = type is null ? null : InternalRepairToolTypeCodec.ToStorage(type.Value),
                Number = number,
                OperatorId = operatorId,
                OnlyCorrected = onlyCorrected
            }, cancellationToken: ct);
            return rows.Select<dynamic, InternalRepairRecord>(MapRecord).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- History / audit facts (append-only) -----------------------------------

    public Task InsertRepairEventAsync(
        IDbUnitOfWork uow, Guid? internalRepairRecordId, string? notes,
        string actorId, DateTimeOffset occurredAtUtc, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO repair_events (repair_scope, internal_repair_record_id, canceled, notes, actor_id, occurred_at_utc)
VALUES ('interna', @InternalRecordId, FALSE, @Notes, @ActorId, @OccurredAtUtc);";
        return Db.ExecuteAsync(uow.Connection, sql, new
        {
            InternalRecordId = (object?)internalRepairRecordId ?? DBNull.Value,
            Notes = (object?)notes ?? DBNull.Value,
            ActorId = (object?)actorId ?? DBNull.Value,
            OccurredAtUtc = occurredAtUtc
        }, uow.Transaction, ct);
    }

    public Task InsertAuditEventAsync(
        IDbUnitOfWork uow, string actionCode, string entityType, string entityId,
        Guid? jobOnId, string result, string? beforeSummary, string? afterSummary,
        string actorId, DateTimeOffset occurredAtUtc, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO audit_events (occurred_at_utc, year, actor_user_id, module_id, action_code,
                          entity_type, entity_id, result, job_on_id, before_summary, after_summary)
VALUES (@OccurredAtUtc, EXTRACT(YEAR FROM @OccurredAtUtc), @Actor, 'reparacao_interna', @Action,
        @EntityType, @EntityId, @Result, @JobOnId, @Before, @After);";
        return Db.ExecuteAsync(uow.Connection, sql, new
        {
            OccurredAtUtc = occurredAtUtc,
            Actor = actorId,
            Action = actionCode,
            EntityType = entityType,
            EntityId = entityId,
            Result = result,
            JobOnId = (object?)jobOnId ?? DBNull.Value,
            Before = (object?)beforeSummary ?? DBNull.Value,
            After = (object?)afterSummary ?? DBNull.Value
        }, uow.Transaction, ct);
    }

    // ---- Mapping / helpers -----------------------------------------------------

    private static InternalRepairRecord MapRecord(dynamic row) => new()
    {
        InternalRepairRecordId = row.internal_repair_record_id,
        Line = (string)row.line,
        JobOnId = row.job_on_id as Guid?,
        JobOnRevisionId = row.job_on_revision_id as Guid?,
        ProductionCode = row.production_code as string,
        Reference = row.reference as string,
        LotId = row.lot_id as Guid?,
        ToolType = InternalRepairToolTypeCodec.FromStorage(row.tool_type as string),
        IndividualNumber = (string)row.individual_number,
        OperatorId = row.operator_id as string,
        OccurredAtUtc = (DateTimeOffset)row.occurred_at_utc,
        CorrectionOfId = row.correction_of_id as Guid?,
        BeforeSnapshot = row.before_snapshot as string,
        CorrectionReason = row.correction_reason as string,
        CreatedAtUtc = (DateTimeOffset)row.created_at_utc,
        CreatedBy = row.created_by as string
    };

    private static async Task DisposeAsync(IDbConnection connection)
    {
        if (connection is IAsyncDisposable a) await a.DisposeAsync();
        else connection.Dispose();
    }
}