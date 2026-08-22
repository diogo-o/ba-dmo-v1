namespace BA.Dmo.Domain.Modules.ReparacaoExterna;

/// <summary>
/// Status of an external repair exit list (N08 <c>repair_exits.status</c>;
/// GLM-RE-04 / REPARACAO_EXTERNA_DESIGN_BRIEF §6). Transitions happen ONLY
/// from persisted explicit confirmations — never inferred from opening a page,
/// elapsed time or production heuristics (GLM-RE-09).
/// Storage: <c>preparacao, a_retirar, enviado, retorno_parcial, concluido, cancelado</c>.
/// <c>Cancelado</c> is schema-compat only in U-15 V1 (owner decision E): the
/// source does not define enough transition/authorization rules, so the
/// functional cancel command is deferred.
/// </summary>
public enum RepairExitStatus
{
    Preparacao,
    ARetirar,
    Enviado,
    RetornoParcial,
    Concluido,
    Cancelado
}

/// <summary>Codec between the domain status and the N08 stored text discriminator.</summary>
public static class RepairExitStatusCodec
{
    public static string ToStorage(RepairExitStatus status) => status switch
    {
        RepairExitStatus.Preparacao => "preparacao",
        RepairExitStatus.ARetirar => "a_retirar",
        RepairExitStatus.Enviado => "enviado",
        RepairExitStatus.RetornoParcial => "retorno_parcial",
        RepairExitStatus.Concluido => "concluido",
        RepairExitStatus.Cancelado => "cancelado",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, $"Unknown repair exit status: {status}")
    };

    public static RepairExitStatus FromStorage(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "preparacao" => RepairExitStatus.Preparacao,
        "a_retirar" => RepairExitStatus.ARetirar,
        "enviado" => RepairExitStatus.Enviado,
        "retorno_parcial" => RepairExitStatus.RetornoParcial,
        "concluido" => RepairExitStatus.Concluido,
        "cancelado" => RepairExitStatus.Cancelado,
        _ => throw new InvalidOperationException($"Unknown persisted repair exit status: {value}")
    };
}