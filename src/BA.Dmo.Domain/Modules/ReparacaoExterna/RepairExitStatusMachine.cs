using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.ReparacaoExterna;

/// <summary>
/// Pure status machine of an external repair exit list (GLM-RE-04, GLM-RE-09;
/// REPARACAO_EXTERNA_DESIGN_BRIEF §6). Transitions result ONLY from persisted
/// explicit confirmations — never inferred from opening the page, elapsed time or
/// production heuristics.
/// State flow (V1): <c>Preparação → A retirar → Enviado → Retorno parcial → Concluído</c>.
/// <c>Cancelado</c> is schema-compat only (owner decision E).
/// </summary>
public static class RepairExitStatusMachine
{
    /// <summary>Runs a pickup confirmation and recomputes the list status.</summary>
    /// <returns>The new list status after applying the confirmations.</returns>
    public static Result<RepairExitStatus, DomainError> ConfirmPickup(
        RepairExitStatus current,
        IReadOnlyList<RepairExitItem> itemsBefore,
        RepairExitItem confirmed)
    {
        if (current is RepairExitStatus.Concluido or RepairExitStatus.Cancelado)
            return Result<RepairExitStatus, DomainError>.Failure(DomainError.DomainConflict(
                "REPEXT_CYCLE_CLOSED",
                "A lista já está fechada; não é possível confirmar recolhas."));

        if (itemsBefore.Any(i => i.RepairExitItemId != confirmed.RepairExitItemId && i.InAtUtc.HasValue))
            return Result<RepairExitStatus, DomainError>.Failure(DomainError.DomainConflict(
                "REPEXT_CYCLE_PARTIAL",
                "Não é possível confirmar recolha depois de iniciar o retorno."));

        // Transition Preparação → A retirar on the first persisted pickup; once the
        // confirmed item makes every item picked out, the list reaches Enviado.
        // Item identity is matched by id (repositories return distinct instances).
        var allPicked = itemsBefore.All(i =>
            i.Picked || i.RepairExitItemId == confirmed.RepairExitItemId);
        if (allPicked)
            return Result<RepairExitStatus, DomainError>.Success(RepairExitStatus.Enviado);

        if (current == RepairExitStatus.Preparacao)
            return Result<RepairExitStatus, DomainError>.Success(RepairExitStatus.ARetirar);

        return Result<RepairExitStatus, DomainError>.Success(current);
    }

    /// <summary>
    /// Runs a return confirmation and recomputes the list status:
    /// partial return → <c>Retorno parcial</c>; all items returned → <c>Concluído</c>.
    /// </summary>
    public static Result<RepairExitStatus, DomainError> ConfirmReturn(
        RepairExitStatus current,
        IReadOnlyList<RepairExitItem> itemsAfter)
    {
        if (current is RepairExitStatus.Cancelado)
            return Result<RepairExitStatus, DomainError>.Failure(DomainError.DomainConflict(
                "REPEXT_CYCLE_CANCELED",
                "A lista está cancelada; não é possível confirmar retornos."));

        if (itemsAfter.Count > 0 && itemsAfter.All(i => i.InAtUtc.HasValue))
            return Result<RepairExitStatus, DomainError>.Success(RepairExitStatus.Concluido);

        if (itemsAfter.Any(i => i.InAtUtc.HasValue))
            return Result<RepairExitStatus, DomainError>.Success(RepairExitStatus.RetornoParcial);

        return Result<RepairExitStatus, DomainError>.Success(current);
    }
}