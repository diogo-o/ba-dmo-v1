using System.Data;
using System.Text.Json;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Infrastructure.Persistence;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Persistence;
using Dapper;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-13 — Job On Dapper persistence (N05, TD-18). Implements IJobOnRepository port.
/// All CRUD operations map exactly to job_on* tables from migration N05.
/// </summary>
public sealed class DapperJobOnRepository : IJobOnRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperJobOnRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Guid> CreateAsync(JobOn jobOn, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO job_on (
    production_code, machine_code, planned_start_at, planned_end_at,
    status, copied_from_job_on_id, article_reference_id,
    created_at_utc)
VALUES (
    @ProductionCode, @MachineCode, @PlannedStartAt, @PlannedEndAt,
    @LifecycleState, @CopiedFromJobOnId, @ArticleReferenceId,
    @CreatedAtUtc)
RETURNING job_on_id;";

        var parameters = new DynamicParameters();
        parameters.Add("@ProductionCode", jobOn.ProductionCode);
        parameters.Add("@MachineCode", jobOn.MachineCode);
        parameters.Add("@PlannedStartAt", (object?)jobOn.PlannedStartAt ?? DBNull.Value);
        parameters.Add("@PlannedEndAt", (object?)jobOn.PlannedEndAt ?? DBNull.Value);
        parameters.Add("@LifecycleState", JobOnLifecycleStateCodec.ToStorage(jobOn.LifecycleState));
        parameters.Add("@CopiedFromJobOnId", (object?)jobOn.CopiedFromJobOnId ?? DBNull.Value);
        parameters.Add("@ArticleReferenceId", (object?)jobOn.ArticleReferenceId ?? DBNull.Value);
        parameters.Add("@CreatedAtUtc", DateTime.UtcNow);

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var id = await Db.ExecuteScalarAsync<Guid>(connection, sql, parameters, cancellationToken: cancellationToken);
            return id;
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<JobOn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT 
    job_on_id,
    production_code,
    machine_code,
    planned_start_at,
    planned_end_at,
    status,
    current_revision_id,
    copied_from_job_on_id,
    article_reference_id,
    closed_at_utc,
    canceled_at_utc,
    canceled_by,
    cancel_reason,
    created_at_utc,
    production_folder
FROM job_on 
WHERE job_on_id = @Id;";
        
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var row = await Db.QuerySingleOrDefaultAsync<dynamic>(connection, sql, new { Id = id }, cancellationToken: cancellationToken);
            
            if (row == null) return null;

            var revisions = await GetRevisionsAsyncInternal(id, cancellationToken);
            
            var jobOn = new JobOnEntity(
                row.production_code!,
                row.machine_code!,
                row.planned_start_at?.ToDateTimeOffset(),
                row.planned_end_at?.ToDateTimeOffset(),
                revisions);
            jobOn.FromRow(row);
            
            return jobOn;
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<IReadOnlyList<JobOn>> GetActiveAsync(string machineCode, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var sql = @"
SELECT 
    job_on_id,
    production_code,
    machine_code,
    planned_start_at,
    planned_end_at,
    status,
    current_revision_id,
    copied_from_job_on_id,
    article_reference_id,
    closed_at_utc,
    canceled_at_utc,
    canceled_by,
    cancel_reason,
    created_at_utc
FROM job_on 
WHERE machine_code = @MachineCode 
  AND status IN ('planeado', 'em_fabrico')
" + (from.HasValue ? "AND planned_start_at >= @From" : "") +
  (to.HasValue ? "AND (planned_end_at IS NULL OR planned_end_at <= @To)" : "");

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(connection, sql, new { MachineCode = machineCode, From = from, To = to }, cancellationToken: cancellationToken);

            return rows.Select(r => (JobOn)MapJobOn(r)).ToList().AsReadOnly();
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<JobOn?> GetByProductionCodeAsync(string productionCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT 
    job_on_id,
    production_code,
    machine_code,
    planned_start_at,
    planned_end_at,
    status,
    current_revision_id,
    copied_from_job_on_id,
    article_reference_id,
    closed_at_utc,
    canceled_at_utc,
    canceled_by,
    cancel_reason,
    created_at_utc
FROM job_on 
WHERE production_code = @ProductionCode;";
        
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var row = await Db.QuerySingleOrDefaultAsync<dynamic>(connection, sql, new { ProductionCode = productionCode }, cancellationToken: cancellationToken);
            
            if (row == null) return null;

            var revisions = await GetRevisionsAsyncInternal(row.job_on_id, cancellationToken);
            return MapJobOn(row, revisions);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task UpdateLifecycleStateAsync(Guid id, JobOnLifecycleState newState, string actorId, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE job_on SET status = @NewState WHERE job_on_id = @Id;";
        
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            await Db.ExecuteAsync(connection, sql, new { NewState = JobOnLifecycleStateCodec.ToStorage(newState), Id = id }, cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task InsertRevisionAsync(JobOnRevision revision, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO job_on_revision (
    job_on_revision_id, job_on_id, revision_number,
    production_snapshot, reference_snapshot, machine_snapshot, dates_snapshot,
    sections, drop_count, type_snapshot, stop_snapshot, weight_snapshot, process_snapshot,
    general_notes, image_asset_id, change_reason, saved_by, saved_at_utc)
VALUES (
    @JobOnRevisionId, @JobOnId, @RevisionNumber,
    @ProductionSnapshot, @ReferenceSnapshot, @MachineSnapshot, @DatesSnapshot,
    @Sections, @DropCount, @TypeSnapshot, @StopSnapshot, @WeightSnapshot, @ProcessSnapshot,
    @GeneralNotes, @ImageAssetId, @ChangeReason, @SavedBy, @SavedAtUtc);";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            await Db.ExecuteAsync(connection, sql, new
            {
                revision.JobOnRevisionId,
                revision.JobOnId,
                revision.RevisionNumber,
                ProductionSnapshot = (object?)revision.ProductionSnapshot ?? DBNull.Value,
                ReferenceSnapshot = (object?)revision.ReferenceSnapshot ?? DBNull.Value,
                MachineSnapshot = (object?)revision.MachineSnapshot ?? DBNull.Value,
                DatesSnapshot = (object?)revision.DatesSnapshot ?? DBNull.Value,
                Sections = revision.Sections,
                DropCount = (object?)revision.DropCount ?? DBNull.Value,
                TypeSnapshot = (object?)revision.TypeSnapshot ?? DBNull.Value,
                StopSnapshot = (object?)revision.StopSnapshot ?? DBNull.Value,
                WeightSnapshot = SerializeWeight(revision.WeightSnapshot),
                ProcessSnapshot = (object?)revision.ProcessSnapshot ?? DBNull.Value,
                GeneralNotes = revision.GeneralNotes,
                ImageAssetId = (object?)revision.ImageAssetId ?? DBNull.Value,
                ChangeReason = revision.ChangeReason,
                SavedBy = revision.SavedBy,
                SavedAtUtc = revision.SavedAtUtc
            }, cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<IReadOnlyList<JobOnRevision>> GetRevisionsAsync(Guid jobOnId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT 
    job_on_revision_id,
    job_on_id,
    revision_number,
    production_snapshot,
    reference_snapshot,
    machine_snapshot,
    dates_snapshot,
    sections,
    drop_count,
    type_snapshot,
    stop_snapshot,
    weight_snapshot,
    process_snapshot,
    general_notes,
    image_asset_id,
    change_reason,
    saved_by,
    saved_at_utc
FROM job_on_revision 
WHERE job_on_id = @JobOnId 
ORDER BY revision_number ASC;";
        
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(connection, sql, new { JobOnId = jobOnId }, cancellationToken: cancellationToken);

            var revisions = rows.Select(r => (JobOnRevision)MapRevision(r)).ToList();
            var hydrated = await HydrateRevisionChildrenAsync(revisions, cancellationToken);
            return hydrated.ToList();
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task InsertComponentsAsync(IEnumerable<JobOnComponent> components, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO job_on_component (
    job_on_component_id, job_on_revision_id, family, source_tool_id, source_lot_id,
    reference_snapshot, lot_snapshot, technical_name_snapshot, planned_quantity,
    stock_snapshot, usage_snapshot, notes, display_order)
VALUES (
    @JobOnComponentId, @JobOnRevisionId, @Family, @SourceToolId, @SourceLotId,
    @ReferenceSnapshot, @LotSnapshot, @TechnicalNameSnapshot, @PlannedQuantity,
    @StockSnapshot, @UsageSnapshot, @Notes, @DisplayOrder);";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            foreach (var component in components)
            {
                await Db.ExecuteAsync(connection, sql, new
                {
                    component.JobOnComponentId,
                    component.JobOnRevisionId,
                    Family = component.Family.ToString(),
                    SourceToolId = (object?)component.SourceToolId ?? DBNull.Value,
                    SourceLotId = (object?)component.SourceLotId ?? DBNull.Value,
                    ReferenceSnapshot = component.ReferenceSnapshot,
                    LotSnapshot = component.LotSnapshot,
                    TechnicalNameSnapshot = component.TechnicalNameSnapshot,
                    PlannedQuantity = (object?)component.PlannedQuantity ?? DBNull.Value,
                    StockSnapshot = (object?)component.StockSnapshot ?? DBNull.Value,
                    UsageSnapshot = (object?)component.UsageSnapshot ?? DBNull.Value,
                    Notes = component.Notes,
                    DisplayOrder = component.DisplayOrder
                }, cancellationToken: cancellationToken);
            }
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task InsertFieldsAsync(IEnumerable<JobOnComponentField> fields, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO job_on_component_field (
    job_on_component_field_id, job_on_component_id, field_key, value_type,
    value_text, value_integer, value_decimal, value_boolean, value_date, display_order)
VALUES (
    @JobOnComponentFieldId, @JobOnComponentId, @FieldKey, @ValueType,
    @ValueText, @ValueInteger, @ValueDecimal, @ValueBoolean, @ValueDate, @DisplayOrder);";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            foreach (var field in fields)
            {
                await Db.ExecuteAsync(connection, sql, new
                {
                    field.JobOnComponentFieldId,
                    field.JobOnComponentId,
                    FieldKey = field.FieldKey,
                    ValueType = field.ValueType,
                    ValueText = (object?)field.ValueText ?? DBNull.Value,
                    ValueInteger = (object?)field.ValueInteger ?? DBNull.Value,
                    ValueDecimal = (object?)field.ValueDecimal ?? DBNull.Value,
                    ValueBoolean = (object?)field.ValueBoolean ?? DBNull.Value,
                    ValueDate = (object?)field.ValueDate ?? DBNull.Value,
                    DisplayOrder = field.DisplayOrder
                }, cancellationToken: cancellationToken);
            }
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task InsertRowsAsync(IEnumerable<JobOnComponentRow> rows, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO job_on_component_row (
    job_on_component_row_id, job_on_component_id, element_label, value_decimal,
    value_text, unit, machine_quantity, display_order)
VALUES (
    @JobOnComponentRowId, @JobOnComponentId, @ElementLabel, @ValueDecimal,
    @ValueText, @Unit, @MachineQuantity, @DisplayOrder);";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            foreach (var rowEntity in rows)
            {
                await Db.ExecuteAsync(connection, sql, new
                {
                    rowEntity.JobOnComponentRowId,
                    rowEntity.JobOnComponentId,
                    ElementLabel = rowEntity.ElementLabel,
                    ValueDecimal = (object?)rowEntity.ValueDecimal ?? DBNull.Value,
                    ValueText = (object?)rowEntity.ValueText ?? DBNull.Value,
                    Unit = rowEntity.Unit,
                    MachineQuantity = (object?)rowEntity.MachineQuantity ?? DBNull.Value,
                    DisplayOrder = rowEntity.DisplayOrder
                }, cancellationToken: cancellationToken);
            }
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task InsertVerificationsAsync(IEnumerable<JobOnVerificationOccurrence> verifications, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO job_on_verification_occurrence (
    job_on_verification_occurrence_id, job_on_component_id, source_rule_id,
    rule_text_snapshot, status, completed_by, completed_at_utc, created_at_utc)
VALUES (
    @JobOnVerificationOccurrenceId, @JobOnComponentId, @SourceRuleId,
    @RuleTextSnapshot, @Status, @CompletedBy, @CompletedAtUtc, @CreatedAtUtc);";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
try
        {
            foreach (var v in verifications)
            {
                await Db.ExecuteAsync(connection, sql, new
                {
                    v.JobOnVerificationOccurrenceId,
                    v.JobOnComponentId,
                    SourceRuleId = (object?)v.SourceRuleId ?? DBNull.Value,
                    RuleTextSnapshot = v.RuleTextSnapshot,
                    Status = v.Status,
                    CompletedBy = (object?)v.CompletedBy ?? DBNull.Value,
                    CompletedAtUtc = (object?)v.CompletedAtUtc ?? DBNull.Value,
                    CreatedAtUtc = v.CreatedAtUtc
                }, cancellationToken: cancellationToken);
            }
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task UpdateVerificationStatusAsync(Guid occurrenceId, string status, string? completedBy, DateTime? completedAtUtc, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE job_on_verification_occurrence 
SET status = @Status, completed_by = @CompletedBy, completed_at_utc = @CompletedAtUtc, updated_at_utc = @UpdatedUtc
WHERE job_on_verification_occurrence_id = @OccurrenceId;";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            await Db.ExecuteAsync(connection, sql, new
            {
                OccurrenceId = occurrenceId,
                Status = status,
                CompletedBy = (object?)completedBy ?? DBNull.Value,
                CompletedAtUtc = (object?)completedAtUtc ?? DBNull.Value,
                UpdatedUtc = DateTime.UtcNow
            }, cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<Guid?> GetCurrentRevisionIdAsync(Guid jobOnId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT current_revision_id FROM job_on WHERE job_on_id = @JobOnId;";
        
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var result = await Db.ExecuteScalarAsync<Guid?>(connection, sql, new { JobOnId = jobOnId }, cancellationToken: cancellationToken);
            return result;
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task UpdateCurrentRevisionAsync(Guid jobOnId, Guid revisionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE job_on SET current_revision_id = @RevisionId WHERE job_on_id = @JobOnId;";
        
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            await Db.ExecuteAsync(connection, sql, new { RevisionId = revisionId, JobOnId = jobOnId }, cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task InsertAuditEventAsync(Guid jobId, Guid? revisionId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO job_on_audit_event (job_on_id, job_on_revision_id, event_type, before_snapshot, after_snapshot, actor_id, occurred_at_utc)
VALUES (@JobId, @RevisionId, @EventType, @BeforeSnapshot::jsonb, @AfterSnapshot::jsonb, @ActorId, @OccurredAtUtc);";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            await Db.ExecuteAsync(connection, sql, new
            {
                JobId = jobId,
                RevisionId = (object?)revisionId ?? DBNull.Value,
                EventType = eventType,
                BeforeSnapshot = (object?)AuditJson.Normalize(beforeSnapshot) ?? DBNull.Value,
                AfterSnapshot = (object?)AuditJson.Normalize(afterSnapshot) ?? DBNull.Value,
                ActorId = actorId,
                OccurredAtUtc = DateTime.UtcNow
            }, cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    /// <summary>
    /// Atomic image mutation: INSERT revision + UPDATE current_revision_id +
    /// INSERT audit event in ONE database transaction (TD-23).
    /// Uses DapperUnitOfWork.RunAsync for commit/rollback determinism.
    /// </summary>
    public async Task InsertImageMutationAsync(
        JobOnRevision newRevision,
        Guid jobOnId,
        string eventType,
        string? beforeImageAssetId,
        string? afterImageAssetId,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        await DapperUnitOfWork.RunAsync<int>(_connectionFactory, async (connection, transaction, ct) =>
        {
// 1. INSERT revision — preserves the complete prior/current snapshot
            //    (image mutations change only the intended image state/metadata).
            const string insertRevisionSql = @"
INSERT INTO job_on_revision (
    job_on_revision_id, job_on_id, revision_number,
    production_snapshot, reference_snapshot, machine_snapshot, dates_snapshot,
    sections, drop_count, type_snapshot, stop_snapshot, weight_snapshot, process_snapshot,
    general_notes, image_asset_id, change_reason, saved_by, saved_at_utc)
VALUES (
    @JobOnRevisionId, @JobOnId, @RevisionNumber,
    @ProductionSnapshot, @ReferenceSnapshot, @MachineSnapshot, @DatesSnapshot,
    @Sections, @DropCount, @TypeSnapshot, @StopSnapshot, @WeightSnapshot, @ProcessSnapshot,
    @GeneralNotes, @ImageAssetId, @ChangeReason, @SavedBy, @SavedAtUtc);";

            await Db.ExecuteAsync(connection, insertRevisionSql, new
            {
                newRevision.JobOnRevisionId,
                newRevision.JobOnId,
                newRevision.RevisionNumber,
                ProductionSnapshot = (object?)newRevision.ProductionSnapshot ?? DBNull.Value,
                ReferenceSnapshot = (object?)newRevision.ReferenceSnapshot ?? DBNull.Value,
                MachineSnapshot = (object?)newRevision.MachineSnapshot ?? DBNull.Value,
                DatesSnapshot = (object?)newRevision.DatesSnapshot ?? DBNull.Value,
                Sections = newRevision.Sections,
                DropCount = (object?)newRevision.DropCount ?? DBNull.Value,
                TypeSnapshot = (object?)newRevision.TypeSnapshot ?? DBNull.Value,
                StopSnapshot = (object?)newRevision.StopSnapshot ?? DBNull.Value,
                WeightSnapshot = SerializeWeight(newRevision.WeightSnapshot),
                ProcessSnapshot = (object?)newRevision.ProcessSnapshot ?? DBNull.Value,
                GeneralNotes = newRevision.GeneralNotes,
                ImageAssetId = (object?)newRevision.ImageAssetId ?? DBNull.Value,
                ChangeReason = newRevision.ChangeReason,
                SavedBy = newRevision.SavedBy,
                SavedAtUtc = newRevision.SavedAtUtc
            }, transaction, ct);

            // 2. UPDATE current_revision_id — must affect exactly 1 row.
            const string updateCurrentSql = @"
UPDATE job_on SET current_revision_id = @RevisionId WHERE job_on_id = @JobOnId;";

            var updatedRows = await Db.ExecuteAsync(connection, updateCurrentSql, new
            {
                RevisionId = newRevision.JobOnRevisionId,
                JobOnId = jobOnId
            }, transaction, ct);

            if (updatedRows != 1)
                throw new InvalidOperationException(
                    $"Expected exactly 1 row updated for job_on.current_revision_id, got {updatedRows}.");

            // 3. INSERT audit event
            const string insertAuditSql = @"
INSERT INTO job_on_audit_event (job_on_id, job_on_revision_id, event_type, before_snapshot, after_snapshot, actor_id, occurred_at_utc)
VALUES (@JobId, @RevisionId, @EventType, @BeforeSnapshot::jsonb, @AfterSnapshot::jsonb, @ActorId, @OccurredAtUtc);";

            await Db.ExecuteAsync(connection, insertAuditSql, new
            {
                JobId = jobOnId,
                RevisionId = (object?)newRevision.JobOnRevisionId ?? DBNull.Value,
                EventType = eventType,
                BeforeSnapshot = (object?)AuditJson.Normalize(beforeImageAssetId) ?? DBNull.Value,
                AfterSnapshot = (object?)AuditJson.Normalize(afterImageAssetId) ?? DBNull.Value,
                ActorId = actorId,
                OccurredAtUtc = DateTime.UtcNow
            }, transaction, ct);

            return 0;
        }, cancellationToken);
    }

    /// <summary>
    /// Atomically persists a NEW immutable revision with its complete child graph
    /// (components + fields + CAL rows + verification occurrences), advances
    /// <c>job_on.current_revision_id</c>, and records the audit event — all in ONE
    /// database transaction (U-13 / TD-18). Either every write commits or none does;
    /// a current revision can never become partially persisted.
    /// </summary>
    public async Task SaveRevisionGraphAsync(
        JobOnRevision revision,
        string eventType,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        await DapperUnitOfWork.RunAsync<int>(_connectionFactory, async (connection, transaction, ct) =>
        {
            await InsertRevisionGraphCoreAsync(connection, transaction, revision, ct);

            var updatedRows = await UpdateCurrentRevisionCoreAsync(
                connection, transaction, revision.JobOnId, revision.JobOnRevisionId, ct);
            if (updatedRows != 1)
                throw new InvalidOperationException(
                    $"Expected exactly 1 row updated for job_on.current_revision_id, got {updatedRows}.");

            await InsertAuditEventCoreAsync(
                connection, transaction, revision.JobOnId, revision.JobOnRevisionId,
                eventType, null, null, actorId, ct);

            return 0;
        }, cancellationToken);
    }

    /// <summary>
    /// Atomically duplicates a Job On: inserts the new <c>job_on</c> header, the copied
    /// revision (revision number 1) with its complete child graph, advances the new
    /// current_revision_id, and records the audit event — all in ONE transaction. On any
    /// failure nothing is persisted, so no partially duplicated Job On can remain.
    /// Returns the newly created <c>job_on</c> id.
    /// </summary>
    public async Task<Guid> DuplicateAtomicallyAsync(
        JobOn newJobOn,
        JobOnRevision revision,
        Guid sourceJobOnId,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        return await DapperUnitOfWork.RunAsync<Guid>(_connectionFactory, async (connection, transaction, ct) =>
        {
            var newJobOnId = await InsertJobOnCoreAsync(connection, transaction, newJobOn, ct);

            // Re-pin the copied revision (and its children) to the new job_on id.
            var pinnedRevision = revision with { JobOnId = newJobOnId };
            if (revision.Components is not null)
            {
                // Children are immutable records already pinned to the source revision.
                // Persist them as-is; their JobOnRevisionId must be the new revision id,
                // which equals this revision's id (the new revision preserves the new id).
                pinnedRevision = pinnedRevision with
                {
                    Components = MapComponentsToRevision(pinnedRevision.JobOnRevisionId, revision.Components)
                };
            }

            await InsertRevisionGraphCoreAsync(connection, transaction, pinnedRevision, ct);

            var updatedRows = await UpdateCurrentRevisionCoreAsync(
                connection, transaction, newJobOnId, pinnedRevision.JobOnRevisionId, ct);
            if (updatedRows != 1)
                throw new InvalidOperationException(
                    $"Expected exactly 1 row updated for job_on.current_revision_id, got {updatedRows}.");

            await InsertAuditEventCoreAsync(
                connection, transaction, newJobOnId, null, "jobon.duplicar",
                null, sourceJobOnId.ToString(), actorId, ct);

            return newJobOnId;
        }, cancellationToken);
    }

    /// <summary>
    /// Inserts a revision and its full child graph (components + fields + CAL rows +
    /// verifications) on the given connection/transaction.
    /// </summary>
    private static async Task InsertRevisionGraphCoreAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        JobOnRevision revision,
        CancellationToken ct)
    {
        const string insertRevisionSql = @"
INSERT INTO job_on_revision (
    job_on_revision_id, job_on_id, revision_number,
    production_snapshot, reference_snapshot, machine_snapshot, dates_snapshot,
    sections, drop_count, type_snapshot, stop_snapshot, weight_snapshot, process_snapshot,
    general_notes, image_asset_id, change_reason, saved_by, saved_at_utc)
VALUES (
    @JobOnRevisionId, @JobOnId, @RevisionNumber,
    @ProductionSnapshot, @ReferenceSnapshot, @MachineSnapshot, @DatesSnapshot,
    @Sections, @DropCount, @TypeSnapshot, @StopSnapshot, @WeightSnapshot, @ProcessSnapshot,
    @GeneralNotes, @ImageAssetId, @ChangeReason, @SavedBy, @SavedAtUtc);";

        await Db.ExecuteAsync(connection, insertRevisionSql, new
        {
            revision.JobOnRevisionId,
            revision.JobOnId,
            revision.RevisionNumber,
            ProductionSnapshot = (object?)revision.ProductionSnapshot ?? DBNull.Value,
            ReferenceSnapshot = (object?)revision.ReferenceSnapshot ?? DBNull.Value,
            MachineSnapshot = (object?)revision.MachineSnapshot ?? DBNull.Value,
            DatesSnapshot = (object?)revision.DatesSnapshot ?? DBNull.Value,
            Sections = revision.Sections,
            DropCount = (object?)revision.DropCount ?? DBNull.Value,
            TypeSnapshot = (object?)revision.TypeSnapshot ?? DBNull.Value,
            StopSnapshot = (object?)revision.StopSnapshot ?? DBNull.Value,
            WeightSnapshot = SerializeWeight(revision.WeightSnapshot),
            ProcessSnapshot = (object?)revision.ProcessSnapshot ?? DBNull.Value,
            GeneralNotes = revision.GeneralNotes,
            ImageAssetId = (object?)revision.ImageAssetId ?? DBNull.Value,
            ChangeReason = revision.ChangeReason,
            SavedBy = revision.SavedBy,
            SavedAtUtc = revision.SavedAtUtc
        }, transaction, ct);

        foreach (var component in revision.Components ?? Array.Empty<JobOnComponent>())
        {
            await InsertComponentCoreAsync(connection, transaction, component, ct);
            await InsertComponentChildrenCoreAsync(connection, transaction, component, ct);
        }
    }

    private static async Task InsertComponentCoreAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        JobOnComponent component,
        CancellationToken ct)
    {
        const string sql = @"
INSERT INTO job_on_component (
    job_on_component_id, job_on_revision_id, family, source_tool_id, source_lot_id,
    reference_snapshot, lot_snapshot, technical_name_snapshot, planned_quantity,
    stock_snapshot, usage_snapshot, notes, display_order)
VALUES (
    @JobOnComponentId, @JobOnRevisionId, @Family, @SourceToolId, @SourceLotId,
    @ReferenceSnapshot, @LotSnapshot, @TechnicalNameSnapshot, @PlannedQuantity,
    @StockSnapshot, @UsageSnapshot, @Notes, @DisplayOrder);";

        await Db.ExecuteAsync(connection, sql, new
        {
            component.JobOnComponentId,
            component.JobOnRevisionId,
            Family = component.Family.ToString(),
            SourceToolId = (object?)component.SourceToolId ?? DBNull.Value,
            SourceLotId = (object?)component.SourceLotId ?? DBNull.Value,
            ReferenceSnapshot = component.ReferenceSnapshot,
            LotSnapshot = component.LotSnapshot,
            TechnicalNameSnapshot = component.TechnicalNameSnapshot,
            PlannedQuantity = (object?)component.PlannedQuantity ?? DBNull.Value,
            StockSnapshot = (object?)component.StockSnapshot ?? DBNull.Value,
            UsageSnapshot = (object?)component.UsageSnapshot ?? DBNull.Value,
            Notes = component.Notes,
            DisplayOrder = component.DisplayOrder
        }, transaction, ct);
    }

    private static async Task InsertComponentChildrenCoreAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        JobOnComponent component,
        CancellationToken ct)
    {
        foreach (var field in component.Fields ?? Array.Empty<JobOnComponentField>())
        {
            const string fieldSql = @"
INSERT INTO job_on_component_field (
    job_on_component_field_id, job_on_component_id, field_key, value_type,
    value_text, value_integer, value_decimal, value_boolean, value_date, display_order)
VALUES (
    @JobOnComponentFieldId, @JobOnComponentId, @FieldKey, @ValueType,
    @ValueText, @ValueInteger, @ValueDecimal, @ValueBoolean, @ValueDate, @DisplayOrder);";
            await Db.ExecuteAsync(connection, fieldSql, new
            {
                field.JobOnComponentFieldId,
                field.JobOnComponentId,
                FieldKey = field.FieldKey,
                ValueType = field.ValueType,
                ValueText = (object?)field.ValueText ?? DBNull.Value,
                ValueInteger = (object?)field.ValueInteger ?? DBNull.Value,
                ValueDecimal = (object?)field.ValueDecimal ?? DBNull.Value,
                ValueBoolean = (object?)field.ValueBoolean ?? DBNull.Value,
                ValueDate = (object?)field.ValueDate ?? DBNull.Value,
                DisplayOrder = field.DisplayOrder
            }, transaction, ct);
        }

        foreach (var rowEntity in component.Rows ?? Array.Empty<JobOnComponentRow>())
        {
            const string rowSql = @"
INSERT INTO job_on_component_row (
    job_on_component_row_id, job_on_component_id, element_label, value_decimal,
    value_text, unit, machine_quantity, display_order)
VALUES (
    @JobOnComponentRowId, @JobOnComponentId, @ElementLabel, @ValueDecimal,
    @ValueText, @Unit, @MachineQuantity, @DisplayOrder);";
            await Db.ExecuteAsync(connection, rowSql, new
            {
                rowEntity.JobOnComponentRowId,
                rowEntity.JobOnComponentId,
                ElementLabel = rowEntity.ElementLabel,
                ValueDecimal = (object?)rowEntity.ValueDecimal ?? DBNull.Value,
                ValueText = (object?)rowEntity.ValueText ?? DBNull.Value,
                Unit = rowEntity.Unit,
                MachineQuantity = (object?)rowEntity.MachineQuantity ?? DBNull.Value,
                DisplayOrder = rowEntity.DisplayOrder
            }, transaction, ct);
        }

        foreach (var v in component.Verifications ?? Array.Empty<JobOnVerificationOccurrence>())
        {
            const string verifSql = @"
INSERT INTO job_on_verification_occurrence (
    job_on_verification_occurrence_id, job_on_component_id, source_rule_id,
    rule_text_snapshot, status, completed_by, completed_at_utc, created_at_utc)
VALUES (
    @JobOnVerificationOccurrenceId, @JobOnComponentId, @SourceRuleId,
    @RuleTextSnapshot, @Status, @CompletedBy, @CompletedAtUtc, @CreatedAtUtc);";
            await Db.ExecuteAsync(connection, verifSql, new
            {
                v.JobOnVerificationOccurrenceId,
                v.JobOnComponentId,
                SourceRuleId = (object?)v.SourceRuleId ?? DBNull.Value,
                RuleTextSnapshot = v.RuleTextSnapshot,
                Status = v.Status,
                CompletedBy = (object?)v.CompletedBy ?? DBNull.Value,
                CompletedAtUtc = (object?)v.CompletedAtUtc ?? DBNull.Value,
                CreatedAtUtc = v.CreatedAtUtc
            }, transaction, ct);
        }
    }

    /// <summary>
    /// Maps a duplicated revision's child components (and their fields/rows/verifications)
    /// onto the NEW revision id by returning copies pinned to the given revision id.
    /// </summary>
    private static IReadOnlyList<JobOnComponent> MapComponentsToRevision(
        Guid revisionId, IEnumerable<JobOnComponent> components) =>
        components
            .Select(c => c with { JobOnRevisionId = revisionId })
            .ToList();

    private static async Task<Guid> InsertJobOnCoreAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        JobOn jobOn,
        CancellationToken ct)
    {
        const string sql = @"
INSERT INTO job_on (
    production_code, machine_code, planned_start_at, planned_end_at,
    status, copied_from_job_on_id, article_reference_id,
    created_at_utc)
VALUES (
    @ProductionCode, @MachineCode, @PlannedStartAt, @PlannedEndAt,
    @LifecycleState, @CopiedFromJobOnId, @ArticleReferenceId,
    @CreatedAtUtc)
RETURNING job_on_id;";

        var parameters = new DynamicParameters();
        parameters.Add("@ProductionCode", jobOn.ProductionCode);
        parameters.Add("@MachineCode", jobOn.MachineCode);
        parameters.Add("@PlannedStartAt", (object?)jobOn.PlannedStartAt ?? DBNull.Value);
        parameters.Add("@PlannedEndAt", (object?)jobOn.PlannedEndAt ?? DBNull.Value);
        parameters.Add("@LifecycleState", JobOnLifecycleStateCodec.ToStorage(jobOn.LifecycleState));
        parameters.Add("@CopiedFromJobOnId", (object?)jobOn.CopiedFromJobOnId ?? DBNull.Value);
        parameters.Add("@ArticleReferenceId", (object?)jobOn.ArticleReferenceId ?? DBNull.Value);
        parameters.Add("@CreatedAtUtc", DateTime.UtcNow);

        var id = await Db.ExecuteScalarAsync<Guid>(connection, sql, parameters, transaction, ct);
        return id;
    }

    private static async Task<int> UpdateCurrentRevisionCoreAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid jobOnId,
        Guid revisionId,
        CancellationToken ct)
    {
        const string sql = @"UPDATE job_on SET current_revision_id = @RevisionId WHERE job_on_id = @JobOnId;";
        return await Db.ExecuteAsync(connection, sql, new { RevisionId = revisionId, JobOnId = jobOnId }, transaction, ct);
    }

    private static async Task InsertAuditEventCoreAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid jobId,
        Guid? revisionId,
        string eventType,
        string? beforeSnapshot,
        string? afterSnapshot,
        string actorId,
        CancellationToken ct)
    {
        const string sql = @"
INSERT INTO job_on_audit_event (job_on_id, job_on_revision_id, event_type, before_snapshot, after_snapshot, actor_id, occurred_at_utc)
VALUES (@JobId, @RevisionId, @EventType, @BeforeSnapshot::jsonb, @AfterSnapshot::jsonb, @ActorId, @OccurredAtUtc);";
        await Db.ExecuteAsync(connection, sql, new
        {
            JobId = jobId,
            RevisionId = (object?)revisionId ?? DBNull.Value,
            EventType = eventType,
            BeforeSnapshot = (object?)AuditJson.Normalize(beforeSnapshot) ?? DBNull.Value,
            AfterSnapshot = (object?)AuditJson.Normalize(afterSnapshot) ?? DBNull.Value,
            ActorId = actorId,
            OccurredAtUtc = DateTime.UtcNow
        }, transaction, ct);
    }

    public async Task<IReadOnlyList<HistoricalProductionSummary>> GetHistoricalProductionsAsync(string? referenceFilter, string? machineFilter, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        // The lifecycle column is "status" (N05, TD-18/TD-27). The status text is
        // mapped to the enum through the canonical codec below (Dapper cannot bind
        // the plain int-backed enum from the status text directly).
        var sql = @"
SELECT 
    jo.job_on_id,
    jo.production_code,
    jo.machine_code,
    jo.planned_start_at,
    jo.planned_end_at,
    jc.reference_snapshot as reference_code,
    jr.revision_number as current_revision_number,
    COUNT(jr2.job_on_revision_id) as total_revision_count,
    jo.status
FROM job_on jo
LEFT JOIN job_on_component jc ON jc.job_on_revision_id = (
    SELECT job_on_revision_id FROM job_on_revision WHERE job_on_id = jo.job_on_id ORDER BY revision_number DESC LIMIT 1
)
LEFT JOIN job_on_revision jr ON jr.job_on_id = jo.job_on_id AND jr.revision_number = (
    SELECT MAX(revision_number) FROM job_on_revision WHERE job_on_id = jo.job_on_id
)
LEFT JOIN job_on_revision jr2 ON jr2.job_on_id = jo.job_on_id
WHERE 1=1" +
        (string.IsNullOrWhiteSpace(referenceFilter) ? "" : " AND jc.reference_snapshot ILIKE @RefFilter") +
        (string.IsNullOrWhiteSpace(machineFilter) ? "" : " AND jo.machine_code = @MachineFilter") +
        (from.HasValue ? " AND jo.planned_start_at >= @From" : "") +
        (to.HasValue ? " AND jo.planned_start_at <= @To" : "") +
        @"
GROUP BY
    jo.job_on_id,
    jo.production_code,
    jo.machine_code,
    jo.planned_start_at,
    jo.planned_end_at,
    jo.status,
    jc.reference_snapshot,
    jr.revision_number";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(connection, sql, new
            {
                RefFilter = $"%{referenceFilter}%",
                MachineFilter = machineFilter,
                From = from,
                To = to
            }, cancellationToken: cancellationToken);

            return rows
                .Select(r => new HistoricalProductionSummary(
                    JobOnId: r.job_on_id,
                    ProductionCode: (string)r.production_code,
                    ReferenceCode: (string?)(r.reference_code),
                    MachineCode: (string)r.machine_code,
                    PlannedStartAt: r.planned_start_at?.ToDateTimeOffset() ?? null,
                    PlannedEndAt: r.planned_end_at?.ToDateTimeOffset() ?? null,
                    CurrentRevisionNumber: r.current_revision_number is null ? 0 : (int)r.current_revision_number,
                    TotalRevisionCount: r.total_revision_count is null ? 0 : (int)r.total_revision_count,
                    LifecycleState: JobOnLifecycleStateCodec.Parse((string)r.status)))
                .ToList();
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    private JobOn MapJobOn(dynamic row, IReadOnlyList<JobOnRevision>? revisions = null)
    {
        var rvs = revisions ?? new List<JobOnRevision>();
                var jobOn = new JobOnEntity(
            row.production_code!,
            row.machine_code!,
            row.planned_start_at?.ToDateTimeOffset(),
            row.planned_end_at?.ToDateTimeOffset(),
            rvs);
        jobOn.FromRow(row);
        return jobOn;
    }

    private async Task<IReadOnlyList<JobOnRevision>> GetRevisionsAsyncInternal(Guid jobOnId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT 
    job_on_revision_id,
    job_on_id,
    revision_number,
    production_snapshot,
    reference_snapshot,
    machine_snapshot,
    dates_snapshot,
    sections,
    drop_count,
    type_snapshot,
    stop_snapshot,
    weight_snapshot,
    process_snapshot,
    general_notes,
    image_asset_id,
    change_reason,
    saved_by,
    saved_at_utc
FROM job_on_revision 
WHERE job_on_id = @JobOnId 
ORDER BY revision_number ASC;";
        
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(connection, sql, new { JobOnId = jobOnId }, cancellationToken: cancellationToken);
            var revisions = rows.Select(r => (JobOnRevision)MapRevision(r)).ToList();

            // Hydrate the complete persisted aggregate graph: components (+ fields +
            // CAL rows) and flattened verification occurrences. A Job On loaded after
            // process restart therefore exposes the same operational state as one just
            // saved (U-13 aggregate hydration / TD-18).
            var hydrated = await HydrateRevisionChildrenAsync(revisions, cancellationToken);
            return hydrated;
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    /// <summary>
    /// Returns each revision's fully hydrated copy: <see cref="JobOnRevision.Components"/>
    /// (with their typed fields / CAL rows / per-component verifications) and the flattened
    /// <see cref="JobOnRevision.Verifications"/> across all of the revision's components.
    /// </summary>
    private async Task<IReadOnlyList<JobOnRevision>> HydrateRevisionChildrenAsync(
        IReadOnlyList<JobOnRevision> revisions,
        CancellationToken cancellationToken)
    {
        if (revisions.Count == 0)
            return revisions;

        var content = await GetHydratedRevisionContent(revisions, cancellationToken);

        var hydrated = new List<JobOnRevision>(revisions.Count);
        foreach (var revision in revisions)
        {
            content.TryGetValue(revision.JobOnRevisionId, out var grouped);

            var components = grouped?.Components ?? new List<JobOnComponent>();
            var verifications = grouped?.Verifications ?? new List<JobOnVerificationOccurrence>();

            hydrated.Add(revision with
            {
                Components = components,
                Verifications = verifications
            });
        }
        return hydrated.AsReadOnly();
    }

    /// <summary>
    /// Loads the full component/field/CAL-row/verification graph for a set of revisions in
    /// a small number of round-trips, grouped by revision id.
    /// </summary>
    private async Task<Dictionary<Guid, HydratedRevisionChildren>> GetHydratedRevisionContent(
        IReadOnlyList<JobOnRevision> revisions,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, HydratedRevisionChildren>();

        var revisionIds = revisions.Select(r => r.JobOnRevisionId).ToList();

        const string componentsSql = @"
SELECT
    job_on_component_id, job_on_revision_id, family, source_tool_id, source_lot_id,
    reference_snapshot, lot_snapshot, technical_name_snapshot, planned_quantity,
    stock_snapshot, usage_snapshot, notes, display_order
FROM job_on_component
WHERE job_on_revision_id = ANY(@RevisionIds)
ORDER BY display_order ASC;";

        const string fieldsSql = @"
SELECT
    job_on_component_field_id, job_on_component_id, field_key, value_type,
    value_text, value_integer, value_decimal, value_boolean, value_date, display_order
FROM job_on_component_field
WHERE job_on_component_id = ANY(@ComponentIds)
ORDER BY display_order ASC;";

        const string rowsSql = @"
SELECT
    job_on_component_row_id, job_on_component_id, element_label, value_decimal,
    value_text, unit, machine_quantity, display_order
FROM job_on_component_row
WHERE job_on_component_id = ANY(@ComponentIds)
ORDER BY display_order ASC;";

        const string verificationsSql = @"
SELECT
    job_on_verification_occurrence_id, job_on_component_id, source_rule_id,
    rule_text_snapshot, status, completed_by, completed_at_utc, created_at_utc
FROM job_on_verification_occurrence
WHERE job_on_component_id = ANY(@ComponentIds)
ORDER BY created_at_utc ASC;";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var componentRows = await Db.QueryAsync<dynamic>(
                connection, componentsSql, new { RevisionIds = revisionIds.ToArray() }, cancellationToken: cancellationToken);

            var componentRowsList = componentRows.ToList();
            var componentIds = componentRowsList.Select(r => (Guid)r.job_on_component_id).ToList();

            var fieldRows = new List<dynamic>();
            var calRows = new List<dynamic>();
            var verificationRows = new List<dynamic>();
            if (componentIds.Count > 0)
            {
                fieldRows.AddRange(await Db.QueryAsync<dynamic>(
                    connection, fieldsSql, new { ComponentIds = componentIds.ToArray() }, cancellationToken: cancellationToken));
                calRows.AddRange(await Db.QueryAsync<dynamic>(
                    connection, rowsSql, new { ComponentIds = componentIds.ToArray() }, cancellationToken: cancellationToken));
                verificationRows.AddRange(await Db.QueryAsync<dynamic>(
                    connection, verificationsSql, new { ComponentIds = componentIds.ToArray() }, cancellationToken: cancellationToken));
            }

            var fieldsByComponent = fieldRows
                .GroupBy(f => (Guid)f.job_on_component_id)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<JobOnComponentField>)g.Select(MapField).ToList());
            var calRowsByComponent = calRows
                .GroupBy(r => (Guid)r.job_on_component_id)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<JobOnComponentRow>)g.Select(MapComponentRow).ToList());
            var verificationsByComponent = verificationRows
                .GroupBy(v => (Guid)v.job_on_component_id)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<JobOnVerificationOccurrence>)g.Select(MapVerificationOccurrence).ToList());

            foreach (var componentRow in componentRowsList)
            {
                var componentId = (Guid)componentRow.job_on_component_id;
                var revisionId = (Guid)componentRow.job_on_revision_id;

                var component = new JobOnComponent
                {
                    JobOnComponentId = componentId,
                    JobOnRevisionId = revisionId,
                    Family = ParseComponentFamily((string)componentRow.family),
                    SourceToolId = componentRow.source_tool_id as Guid?,
                    SourceLotId = componentRow.source_lot_id as Guid?,
                    ReferenceSnapshot = (string?)componentRow.reference_snapshot,
                    LotSnapshot = (string?)componentRow.lot_snapshot,
                    TechnicalNameSnapshot = (string?)componentRow.technical_name_snapshot,
                    PlannedQuantity = componentRow.planned_quantity as decimal?,
                    StockSnapshot = componentRow.stock_snapshot as decimal?,
                    UsageSnapshot = componentRow.usage_snapshot as decimal?,
                    Notes = (string?)componentRow.notes,
                    DisplayOrder = componentRow.display_order,
                    Fields = fieldsByComponent.TryGetValue(componentId, out var fs) ? fs : Array.Empty<JobOnComponentField>(),
                    Rows = calRowsByComponent.TryGetValue(componentId, out var rs) ? rs : Array.Empty<JobOnComponentRow>(),
                    Verifications = verificationsByComponent.TryGetValue(componentId, out var vs) ? vs : Array.Empty<JobOnVerificationOccurrence>()
                };

                if (!result.TryGetValue(revisionId, out var grouped))
                {
                    grouped = new HydratedRevisionChildren();
                    result[revisionId] = grouped;
                }
                grouped.Components.Add(component);
            }

            // Flatten each component's verifications onto the revision-level collection.
            foreach (var verifGroup in verificationsByComponent)
            {
                var ownerRevision = componentRowsList
                    .FirstOrDefault(c => (Guid)c.job_on_component_id == verifGroup.Key as Guid?);
                if (ownerRevision is null)
                    continue;
                var revisionId = (Guid)ownerRevision.job_on_revision_id;
                if (!result.TryGetValue(revisionId, out var grouped))
                {
                    grouped = new HydratedRevisionChildren();
                    result[revisionId] = grouped;
                }
                grouped.Verifications.AddRange(verifGroup.Value);
            }
        }
        finally
        {
            await DisposeAsync(connection);
        }

        return result;
    }

    private static object SerializeWeight(decimal? weight) =>
        weight is null
            ? DBNull.Value
            : JsonSerializer.Serialize(new { value = weight.Value });

    private static decimal? ParseWeight(object? raw)
    {
        if (raw is null || raw is DBNull)
            return null;

        var text = raw switch
        {
            string s => s,
            _ => raw.ToString()
        };
        if (string.IsNullOrWhiteSpace(text))
            return null;

        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetDecimal();

        return null;
    }

    private JobOnRevision MapRevision(dynamic row)
    {
        return new JobOnRevision
        {
            JobOnRevisionId = row.job_on_revision_id,
            JobOnId = row.job_on_id,
            RevisionNumber = row.revision_number,
            ProductionSnapshot = (string?)row.production_snapshot,
            ReferenceSnapshot = (string?)row.reference_snapshot,
            MachineSnapshot = (string?)row.machine_snapshot,
            DatesSnapshot = (string?)row.dates_snapshot,
            Sections = row.sections ?? "{}",
            DropCount = row.drop_count,
TypeSnapshot = (string?)row.type_snapshot,
            StopSnapshot = (string?)row.stop_snapshot,
            WeightSnapshot = ParseWeight(row.weight_snapshot),
            ProcessSnapshot = (string?)row.process_snapshot,
            GeneralNotes = row.general_notes,
            ImageAssetId = row.image_asset_id,
            ChangeReason = row.change_reason,
            SavedBy = row.saved_by,
            SavedAtUtc = row.saved_at_utc
        };
    }

    private static async Task DisposeAsync(System.Data.IDbConnection connection)
    {
        if (connection is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            connection.Dispose();
    }

    private static ComponentFamily ParseComponentFamily(string stored)
    {
        if (Enum.TryParse<ComponentFamily>(stored, ignoreCase: false, out var family))
            return family;
        throw new InvalidOperationException($"Unknown persisted component family: {stored}");
    }

    private static JobOnComponentField MapField(dynamic row) => new()
    {
        JobOnComponentFieldId = row.job_on_component_field_id,
        JobOnComponentId = row.job_on_component_id,
        FieldKey = (string)row.field_key,
        ValueType = (string)row.value_type,
        ValueText = row.value_text as string,
        ValueInteger = row.value_integer as int?,
        ValueDecimal = row.value_decimal as decimal?,
        ValueBoolean = row.value_boolean as bool?,
        ValueDate = row.value_date as DateTime?,
        DisplayOrder = row.display_order
    };

    private static JobOnComponentRow MapComponentRow(dynamic row) => new()
    {
        JobOnComponentRowId = row.job_on_component_row_id,
        JobOnComponentId = row.job_on_component_id,
        ElementLabel = (string)row.element_label,
        ValueDecimal = row.value_decimal as decimal?,
        ValueText = row.value_text as string,
        Unit = row.unit as string,
        MachineQuantity = row.machine_quantity as decimal?,
        DisplayOrder = row.display_order
    };

    private static JobOnVerificationOccurrence MapVerificationOccurrence(dynamic row) => new()
    {
        JobOnVerificationOccurrenceId = row.job_on_verification_occurrence_id,
        JobOnComponentId = row.job_on_component_id,
        SourceRuleId = row.source_rule_id as Guid?,
        RuleTextSnapshot = (string?)row.rule_text_snapshot,
        Status = (string)row.status,
        CompletedBy = row.completed_by as string,
        CompletedAtUtc = row.completed_at_utc as DateTime?,
        CreatedAtUtc = row.created_at_utc
    };
}

/// <summary>
/// Mutable accumulation holder used while an internal read hydrates the component /
/// verification graph of one Job On revision (confined to this file).
/// </summary>
internal sealed class HydratedRevisionChildren
{
    public List<JobOnComponent> Components { get; } = new();
    public List<JobOnVerificationOccurrence> Verifications { get; } = new();
}
