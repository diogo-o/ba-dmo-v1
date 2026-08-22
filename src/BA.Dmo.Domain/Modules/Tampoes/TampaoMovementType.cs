namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// U-17 — Movement kinds of a Tampões quantity change (N10 <c>tampao_movements</c>
/// CHECK; GLM-TP-05). <c>adicionar</c>/<c>remover</c> change a single balance;
/// <c>alterar_estado</c> (Enchidos↔Por encher) and <c>alterar_configuracao</c>
/// transfer quantity between origin and destination as a SINGLE atomic movement.
/// </summary>
public enum TampaoMovementType
{
    Adicionar,
    Remover,
    AlterarEstado,
    AlterarConfiguracao
}

/// <summary>Codec between the domain enum and the N10 stored text discriminator.</summary>
public static class TampaoMovementTypeCodec
{
    public static string ToStorage(TampaoMovementType type) => type switch
    {
        TampaoMovementType.Adicionar => "adicionar",
        TampaoMovementType.Remover => "remover",
        TampaoMovementType.AlterarEstado => "alterar_estado",
        TampaoMovementType.AlterarConfiguracao => "alterar_configuracao",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown movement type: {type}")
    };

    public static TampaoMovementType FromStorage(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "adicionar" => TampaoMovementType.Adicionar,
        "remover" => TampaoMovementType.Remover,
        "alterar_estado" => TampaoMovementType.AlterarEstado,
        "alterar_configuracao" => TampaoMovementType.AlterarConfiguracao,
        _ => throw new InvalidOperationException($"Unknown persisted movement type: {value}")
    };
}