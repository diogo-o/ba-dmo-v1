using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.ReparacaoExterna;

namespace BA.Dmo.Application.Modules.ReparacaoExterna;

/// <summary>
/// U-15 — Reparação Externa read/write port (N08; GLM-RE, TD-15/TD-22). Owns
/// Reparação persistence (repairers, line_repairer_defaults, repair_exits,
/// repair_exit_items, repair_events) ONLY.
/// Single-table writes self-manage a connection; multi-module coordinated writes
/// (pickup/return that ALSO move Armazém physical state) participate in the
/// caller-provided <see cref="IDbUnitOfWork"/> so they commit/roll back atomically
/// with the Armazém movement (owner decisions B/C).
/// </summary>
public interface IRepairRepository
{
    // ---- External exit lists (create / hydrate) ----------------------------
    Task<Guid> CreateExitAsync(RepairExit exit, RepairerSnapshot? repairerSnapshot, string? snapshotJson, CancellationToken ct = default);
    Task<RepairExit?> GetExitByIdAsync(Guid repairExitId, CancellationToken ct = default);
    Task<IReadOnlyList<RepairExitItem>> GetExitItemsAsync(Guid repairExitId, CancellationToken ct = default);
    Task<IReadOnlyList<RepairExit>> ListExitsAsync(
        RepairType? type, RepairExitStatus? status, DateOnly? from, DateOnly? to,
        CancellationToken ct = default);
    Task<bool> ExistsItemInOpenExitAsync(Guid physicalPieceId, CancellationToken ct = default);

    // ---- Exit items ---------------------------------------------------------
    Task<Guid> AddItemAsync(RepairExitItem item, CancellationToken ct = default);
    Task<RepairExitItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default);
    Task DeleteItemAsync(Guid itemId, CancellationToken ct = default);

    // ---- Coordinated writes (participate in the shared unit of work) --------
    Task ConfirmItemPickedAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default);
    Task ConfirmItemReturnedAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default);
    Task UpdateExitStatusAsync(IDbUnitOfWork uow, Guid repairExitId, string statusStorage, CancellationToken ct = default);
    Task InsertRepairEventAsync(IDbUnitOfWork uow, Guid repairExitItemId, string? notes, string actorId, DateTimeOffset occurredAtUtc, CancellationToken ct = default);

    // ---- Repairers / line defaults ------------------------------------------
    Task<Guid> CreateRepairerAsync(Repairer repairer, CancellationToken ct = default);
    Task UpdateRepairerAsync(Repairer repairer, CancellationToken ct = default);
    Task DeactivateRepairerAsync(Guid repairerId, CancellationToken ct = default);
    Task<Repairer?> GetRepairerByIdAsync(Guid repairerId, CancellationToken ct = default);
    Task<IReadOnlyList<Repairer>> ListRepairersAsync(CancellationToken ct = default);
    Task UpsertLineDefaultAsync(LineRepairerDefault lineDefault, CancellationToken ct = default);
    Task<IReadOnlyList<LineRepairerDefault>> ListLineDefaultsAsync(CancellationToken ct = default);

    // ---- Repairer capability (R004, many-to-many repairer_repair_types) ------
    Task SetRepairerRepairTypesAsync(Guid repairerId, IEnumerable<string> repairTypes, CancellationToken ct = default);
    Task<IReadOnlySet<string>> ListRepairerRepairTypesAsync(Guid repairerId, CancellationToken ct = default);

    // ---- Audit --------------------------------------------------------------
    Task InsertAuditEventAsync(Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken ct = default);
}