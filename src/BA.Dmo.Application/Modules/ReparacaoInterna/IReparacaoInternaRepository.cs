using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.ReparacaoInterna;

namespace BA.Dmo.Application.Modules.ReparacaoInterna;

/// <summary>
/// U-16 — Reparação Interna read/write port (N08 <c>internal_repair_records</c> +
/// <c>repair_events</c> scope interna + global <c>audit_events</c> module
/// <c>reparacao_interna</c>; GLM-RI-07, GLM-DATA-07). Single-row writes
/// self-manage a connection; the coordinated register/correction write also
/// inserts a repair_event and an audit_events row in the same
/// <see cref="IDbUnitOfWork"/> so they commit/roll back atomically.
/// </summary>
public interface IReparacaoInternaRepository
{
    // ---- Primary register + corrections --------------------------------------
    Task<Guid> InsertAsync(IDbUnitOfWork uow, InternalRepairRecord record, CancellationToken ct = default);

    /// <summary>Loads a record by its primary key (any node in a correction chain).</summary>
    Task<InternalRepairRecord?> GetByIdAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Loads all records of a correction chain (oldest first).</summary>
    Task<IReadOnlyList<InternalRepairRecord>> GetChainAsync(Guid rootRecordId, CancellationToken ct = default);

    /// <summary>
    /// Loads the latest valid record (primary or most recent correction) for each
    /// chain root, filtered by the supplied criteria.
    /// </summary>
    Task<IReadOnlyList<InternalRepairRecord>> ListAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? line, Guid? jobOnId,
        InternalRepairToolType? type, string? number, string? operatorId,
        bool onlyCorrected, CancellationToken ct = default);

    // ---- History / audit facts (append-only) ----------------------------------
    /// <summary>Inserts a <c>repair_events</c> row with scope 'interna' (append-only).</summary>
    Task InsertRepairEventAsync(IDbUnitOfWork uow, Guid? internalRepairRecordId, string? notes, string actorId, DateTimeOffset occurredAtUtc, CancellationToken ct = default);

    /// <summary>Inserts a global <c>audit_events</c> row for module reparacao_interna.</summary>
    Task InsertAuditEventAsync(IDbUnitOfWork uow, string actionCode, string entityType, string entityId,
        Guid? jobOnId, string result, string? beforeSummary, string? afterSummary,
        string actorId, DateTimeOffset occurredAtUtc, CancellationToken ct = default);
}