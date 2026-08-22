using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Boquilhas;

namespace BA.Dmo.Application.Modules.Boquilhas;

/// <summary>
/// U-19 — Boquilhas read/write port (N03 <c>bq_*</c>; GLM-BQ, GLM-DATA). Owns the
/// Boquilhas persistence ONLY (lotes, traces, movements, discrepancies, lifecycle,
/// utilisation, repairers, line defaults) plus the module's global
/// <c>audit_events</c> rows (module <c>boquilhas</c>, UD-17/TD-19). Multi-row
/// writes participate in the shared <see cref="IDbUnitOfWork"/> from
/// <see cref="IBoquilhasUnitOfWorkFactory"/> so lot+trace+movement+audit commit or
/// roll back as ONE transaction (GLM-DATA-05). Movements are append-only; edits
/// are new facts. No Ferramentas/Armazém/Reparação-Externa writes (U-19 D1/D2).
/// </summary>
public interface IBoquilhasRepository
{
    // ---- Lots ----------------------------------------------------------------
    Task<BqLote?> GetLoteByIdAsync(Guid bqLoteId, CancellationToken ct = default);
    Task<BqLote?> GetLoteByReferenceBatchAsync(string reference, string batchCode, CancellationToken ct = default);
    Task<IReadOnlyList<BqLote>> ListLotesAsync(BqLoteFilter filter, CancellationToken ct = default);
    Task<int> CountLotesAsync(BqLoteFilter filter, CancellationToken ct = default);

    /// <summary>Creates the lot row within the shared unit of work.</summary>
    Task CreateLoteAsync(IDbUnitOfWork uow, BqLote lote, CancellationToken ct = default);

    /// <summary>Updates editable lot fields (reference/batch_code/allowed_lines) within the shared UoW.</summary>
    Task UpdateLoteAsync(IDbUnitOfWork uow, BqLote lote, CancellationToken ct = default);

    /// <summary>Updates the persisted lifecycle state within the shared UoW.</summary>
    Task UpdateLifecycleStateAsync(IDbUnitOfWork uow, Guid bqLoteId, BqLifecycleState state, CancellationToken ct = default);

    /// <summary>Appends a lifecycle event within the shared UoW.</summary>
    Task InsertLifecycleEventAsync(IDbUnitOfWork uow, BqLifecycleEvent evt, CancellationToken ct = default);

    // ---- Traces ----------------------------------------------------------------
    Task<BqTrace?> GetTraceByIdAsync(Guid bqTraceId, CancellationToken ct = default);
    Task<BqTrace?> GetActiveTraceForLoteAsync(Guid bqLoteId, CancellationToken ct = default);
    Task<BqTrace?> GetLastClosedOrActiveTraceAsync(Guid bqLoteId, CancellationToken ct = default);

    /// <summary>Looks up a trace within the shared unit of work (for movement dispatch).</summary>
    Task<BqTrace?> GetTraceForMovementAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default);

    /// <summary>Creates a trace row within the shared UoW.</summary>
    Task CreateTraceAsync(IDbUnitOfWork uow, BqTrace trace, CancellationToken ct = default);

    /// <summary>Marks a trace closed within the shared UoW.</summary>
    Task CloseTraceAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default);

    /// <summary>Reopens a closed trace (status back to active) within the shared UoW.</summary>
    Task ReopenTraceAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default);

    /// <summary>Appends a reopen history entry (jsonb) within the shared UoW.</summary>
    Task AppendReopenHistoryAsync(IDbUnitOfWork uow, Guid bqTraceId, string actorId, DateTimeOffset atUtc, CancellationToken ct = default);

    // ---- Movements ----------------------------------------------------------------
    /// <summary>Inserts an append-only movement within the shared UoW.</summary>
    Task InsertMovementAsync(IDbUnitOfWork uow, BqMovement movement, CancellationToken ct = default);

    /// <summary>All non-voided movements of a trace, in occurred order (for balance computation).</summary>
    Task<IReadOnlyList<BqMovement>> ListMovementsForTraceAsync(Guid bqTraceId, CancellationToken ct = default);

    /// <summary>Paginated movements of a lot (Registo page list).</summary>
    Task<IReadOnlyList<BqMovement>> ListMovementsByLoteAsync(Guid bqLoteId, BqHistoryFilter filter, CancellationToken ct = default);

    /// <summary>Paginated movements across lots (Histórico aggregate view).</summary>
    Task<IReadOnlyList<BqMovement>> ListMovementsAsync(BqHistoryFilter filter, CancellationToken ct = default);
    Task<int> CountMovementsAsync(BqHistoryFilter filter, CancellationToken ct = default);

    /// <summary>Records a voided movement id in the trace's deleted_movements (never physical delete).</summary>
    Task VoidMovementAsync(IDbUnitOfWork uow, Guid bqTraceId, Guid bqMovementId, CancellationToken ct = default);

    /// <summary>Returns the set of movement ids voided for a trace.</summary>
    Task<IReadOnlySet<Guid>> ListVoidedMovementIdsAsync(Guid bqTraceId, CancellationToken ct = default);

    // ---- Utilisation ----------------------------------------------------------------
    Task InsertUtilisationReadingAsync(IDbUnitOfWork uow, BqUtilisationReading reading, CancellationToken ct = default);
    Task<BqUtilisationReading?> GetUtilisationReadingAsync(Guid bqTraceId, BqUtilisationReadingKind kind, CancellationToken ct = default);

    // ---- Discrepancies ----------------------------------------------------------------
    Task<BqDiscrepancy?> GetOpenDiscrepancyForTraceAsync(Guid bqLoteId, Guid? bqTraceId, CancellationToken ct = default);
    Task InsertDiscrepancyAsync(IDbUnitOfWork uow, BqDiscrepancy discrepancy, CancellationToken ct = default);
    Task UpdateDiscrepancyAsync(IDbUnitOfWork uow, BqDiscrepancy discrepancy, CancellationToken ct = default);
    Task<IReadOnlyList<BqDiscrepancy>> ListDiscrepanciesAsync(Guid? bqLoteId, CancellationToken ct = default);

    // ---- Repairers ----------------------------------------------------------------
    Task<IReadOnlyList<BqRepairer>> ListRepairersAsync(bool onlyActive, CancellationToken ct = default);
    Task<BqRepairer?> GetRepairerByIdAsync(Guid repairerId, CancellationToken ct = default);
    Task<Guid> CreateRepairerAsync(BqRepairer repairer, CancellationToken ct = default);
    Task UpdateRepairerAsync(BqRepairer repairer, CancellationToken ct = default);
    Task<BqLineRepairerDefault?> GetLineRepairerDefaultAsync(string line, CancellationToken ct = default);
    Task SetLineRepairerDefaultAsync(BqLineRepairerDefault lineDefault, CancellationToken ct = default);

    // ---- Audit ----------------------------------------------------------------
    Task InsertAuditEventAsync(IDbUnitOfWork uow, string actionCode, string entityType, string entityId,
        string result, string? beforeSummary, string? afterSummary, string actorId,
        DateTimeOffset occurredAtUtc, CancellationToken ct = default);
}

/// <summary>Lot listing filter (Boquilhas/Registo search + Boquilhas tab).</summary>
public sealed record BqLoteFilter(
    string? Search,
    bool? OnlyAvailable,
    BqLifecycleState? LifecycleState,
    int Page,
    int PageSize);

/// <summary>Movement listing filter (Registo lot view + Histórico aggregate).</summary>
public sealed record BqHistoryFilter(
    Guid? BqLoteId,
    string? Search,
    BqMovementType? MovementType,
    Guid? RepairerId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize);