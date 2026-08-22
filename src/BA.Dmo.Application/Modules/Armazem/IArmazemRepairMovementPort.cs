using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Armazem;

/// <summary>
/// U-15 — Smallest Armazém-owned public contract for repair-related physical
/// movement (owner decision B). Armazém remains the SOLE owner of
/// <c>warehouse_stock</c> and <c>warehouse_movements</c>; U-15 (Reparação) consumes
/// this port and MUST NOT write Armazém tables directly, nor consume
/// <c>IArmazemRepository</c>.
/// Each method participates in the caller-provided <see cref="IDbUnitOfWork"/> so
/// the repair-cycle + physical movement succeed or fail as ONE transaction
/// (owner decision C). The physical state changes ONLY after an explicit persisted
/// confirmation (owner decision D) — no inferred release/return.
/// The occupation unit is the <c>tool_lote_id</c> (N09), resolved from the CM/MF
/// physical piece by U-15.
/// </summary>
public interface IArmazemRepairMovementPort
{
    /// <summary>
    /// Confirms the physical pickup/release of a tool lot for a repair exit:
    /// releases its active occupation and records an <c>out</c> movement carrying
    /// the <c>repair_exit_id</c> for provenance. Fails when the lot has no active
    /// occupation (nothing physically present to release).
    /// </summary>
    Task<Result<bool, DomainError>> ConfirmPickupAsync(
        IDbUnitOfWork uow,
        Guid repairExitId,
        Guid toolLoteId,
        string actorId,
        DateTimeOffset outAtUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Confirms the physical return/re-occupation of a tool lot for a repair exit:
    /// occupies the given position and records an <c>in</c> movement carrying the
    /// <c>repair_exit_id</c> for provenance. Fails when the position is already
    /// occupied by another tool.
    /// </summary>
    Task<Result<bool, DomainError>> ConfirmReturnAsync(
        IDbUnitOfWork uow,
        Guid repairExitId,
        Guid toolLoteId,
        string positionCode,
        string actorId,
        DateTimeOffset inAtUtc,
        CancellationToken ct = default);
}