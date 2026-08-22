using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.ReparacaoExterna;

/// <summary>
/// Item of an external repair exit list (N08 <c>repair_exit_items</c>; TD-22).
/// CM/MF items reference a Ferramentas <c>physical_piece</c> by its individual
/// number; BQ items reference a Boquilhas lot by quantity (BQ deferred to U-19).
/// Per-item out/in facts preserve operator + date (REPARACAO_EXTERNA_DESIGN_BRIEF
/// §41); <c>in_at_utc</c> is what supports partial returns (Retorno parcial/Concluído).
/// </summary>
public sealed class RepairExitItem
{
    public Guid RepairExitItemId { get; set; } = Guid.NewGuid();

    public Guid RepairExitId { get; set; }

    public Guid? BqLoteId { get; set; }

    public Guid? PhysicalPieceId { get; set; }

    public decimal? Qty { get; set; }

    public string? IndividualNumber { get; set; }

    public bool Picked { get; set; }

    public DateTimeOffset? OutAtUtc { get; set; }

    public string? OutOperatorId { get; set; }

    public DateTimeOffset? InAtUtc { get; set; }

    public string? InOperatorId { get; set; }

    /// <summary>Item status (N08 default 'pendente').</summary>
    public string Status { get; set; } = "pendente";

    public bool IsPickedOut => Picked || OutAtUtc.HasValue;

    public bool IsReturned => InAtUtc.HasValue;

    /// <summary>
    /// Creates a CM/MF item referencing an individual numbered piece.
    /// Requires the Ferramentas physical piece (stable id + individual number).
    /// </summary>
    public static Result<RepairExitItem, DomainError> CreateCmMf(
        Guid repairExitId,
        Guid physicalPieceId,
        string individualNumber,
        RepairType type)
    {
        if (type is not (RepairType.CM or RepairType.MF))
            return Result<RepairExitItem, DomainError>.Failure(DomainError.Validation(
                "REPEXT_ITEM_KIND",
                "Os itens por número individual aplicam-se aos tipos CM e MF."));

        var number = individualNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(number))
            return Result<RepairExitItem, DomainError>.Failure(DomainError.Validation(
                "REPEXT_ITEM_NUMBER_REQUIRED",
                "O número individual da ferramenta é obrigatório."));

        return Result<RepairExitItem, DomainError>.Success(new RepairExitItem
        {
            RepairExitItemId = Guid.NewGuid(),
            RepairExitId = repairExitId,
            PhysicalPieceId = physicalPieceId,
            IndividualNumber = number,
            Picked = false,
            Status = "pendente"
        });
    }

    /// <summary>
    /// Confirms the physical pickup of this item (out fact) with operator + timestamp.
    /// Idempotent: re-confirming an already-picked item is a no-op success.
    /// </summary>
    public Result<RepairExitItem, DomainError> ConfirmPickedOut(
        DateTimeOffset nowUtc, string? actorId)
    {
        if (IsReturned)
            return Result<RepairExitItem, DomainError>.Failure(DomainError.DomainConflict(
                "REPEXT_ITEM_ALREADY_RETURNED",
                "Este item já foi devolvido; não é possível confirmar a recolha."));

        OutAtUtc = nowUtc;
        OutOperatorId = actorId;
        Picked = true;
        Status = "em_reparacao";
        return Result<RepairExitItem, DomainError>.Success(this);
    }

    /// <summary>
    /// Confirms the physical return of this item (in fact) with operator + timestamp.
    /// Idempotent: re-confirming an already-returned item is a no-op success.
    /// </summary>
    public Result<RepairExitItem, DomainError> ConfirmReturned(
        DateTimeOffset nowUtc, string? actorId)
    {
        InAtUtc = nowUtc;
        InOperatorId = actorId;
        Status = "devolvido";
        return Result<RepairExitItem, DomainError>.Success(this);
    }
}