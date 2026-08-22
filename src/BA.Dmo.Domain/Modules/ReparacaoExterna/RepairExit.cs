using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.ReparacaoExterna;

/// <summary>
/// External repair exit list aggregate (N08 <c>repair_exits</c>; REPARACAO_EXTERNA
/// DESIGN_BRIEF §6, GLM-RE-04). A list is a batch/shipment of CM/MF tools (V1) sent to
/// a repairer and returned item-by-item. Ownership: Reparação owns the plan/reparador/
/// ciclo; Armazém owns the physical movements (GLM-RE-01).
/// The list status only changes via persisted explicit confirmations.
/// </summary>
public sealed class RepairExit
{
    public Guid RepairExitId { get; set; } = Guid.NewGuid();

    public RepairType RepairType { get; set; }

    public Guid? RepairerId { get; set; }

    public RepairerSnapshot? RepairerSnapshot { get; set; }

    public DateOnly? PlannedDate { get; set; }

    public RepairExitStatus Status { get; set; } = RepairExitStatus.Preparacao;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Items of this list (hydrated for state computation).</summary>
    public IReadOnlyList<RepairExitItem> Items { get; set; } = Array.Empty<RepairExitItem>();

    public static Result<RepairExit, DomainError> Create(
        RepairType type,
        RepairerSnapshot? repairerSnapshot,
        DateOnly? plannedDate,
        DateTimeOffset nowUtc,
        string? createdBy)
    {
        return Result<RepairExit, DomainError>.Success(new RepairExit
        {
            RepairExitId = Guid.NewGuid(),
            RepairType = type,
            RepairerId = repairerSnapshot?.RepairerId,
            RepairerSnapshot = repairerSnapshot,
            PlannedDate = plannedDate,
            Status = RepairExitStatus.Preparacao,
            CreatedAtUtc = nowUtc,
            CreatedBy = createdBy,
            UpdatedAtUtc = nowUtc
        });
    }

    /// <summary>
    /// True when the list is still editable (module/domain rule: new items may be
    /// added/removed only while it is not being physically handled). Hard block for
    /// duplicate-in-open-exit is enforced on the EXISTING open lists, not on this one.
    /// </summary>
    public bool IsPreparing => Status == RepairExitStatus.Preparacao;

    public bool IsOpen => Status is RepairExitStatus.Preparacao
        or RepairExitStatus.ARetirar
        or RepairExitStatus.Enviado
        or RepairExitStatus.RetornoParcial;

    /// <summary>
    /// Domain rule (GLM-RE-09, hard block): an item may not belong to more than one
    /// open exit. The service enforces this by checking other open exits; this helper
    /// is the business classification.
    /// </summary>
    public static Result<bool, DomainError> ValidateNotAlreadyInOpenExit(
        bool alreadyInAnotherOpenExit)
    {
        if (alreadyInAnotherOpenExit)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "REPEXT_ITEM_IN_OPEN_EXIT",
                "Esta ferramenta já está incluída numa saída programada aberta."));
        return Result<bool, DomainError>.Success(true);
    }
}