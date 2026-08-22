namespace BA.Dmo.Domain.Modules.Peso;

/// <summary>
/// Peso control workflow state (N06 <c>peso_controlos.status</c>; GLM-PESO-06.6).
/// rascunho → (submit) pendente → aprovar/nao_aprovado; nao_aprovado → edit+submit
/// (revision+1); aprovado/nao_aprovado → reopen(reason) → rascunho (revision+1).
/// Enviar para aprovação is NEVER automatic.
/// </summary>
public enum PesoControlState
{
    Rascunho,
    Pendente,
    Aprovado,
    NaoAprovado
}

/// <summary>Peso control state persistence/text helpers.</summary>
public static class PesoControlStateCodec
{
    public static PesoControlState Parse(string value) => value?.Trim().ToLowerInvariant() switch
    {
        "rascunho" => PesoControlState.Rascunho,
        "pendente" => PesoControlState.Pendente,
        "aprovado" => PesoControlState.Aprovado,
        "nao_aprovado" => PesoControlState.NaoAprovado,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Peso control state.")
    };

    public static string ToStorage(PesoControlState value) => value switch
    {
        PesoControlState.Rascunho => "rascunho",
        PesoControlState.Pendente => "pendente",
        PesoControlState.Aprovado => "aprovado",
        PesoControlState.NaoAprovado => "nao_aprovado",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Peso control state.")
    };

    /// <summary>User-facing state label (Histórico/Responsável).</summary>
    public static string ToDisplay(PesoControlState value) => value switch
    {
        PesoControlState.Rascunho => "Rascunho",
        PesoControlState.Pendente => "Pendente",
        PesoControlState.Aprovado => "Aprovado",
        PesoControlState.NaoAprovado => "Não aprovado",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Peso control state.")
    };
}

/// <summary>The per-CM decision of a comparison (GLM-PESO-06.5).</summary>
public enum PesoCmDecision
{
    None,
    Manter,
    ColocarDeParte
}

/// <summary>Per-CM decision persistence/text helpers.</summary>
public static class PesoCmDecisionCodec
{
    public static PesoCmDecision Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" => PesoCmDecision.None,
        "manter" => PesoCmDecision.Manter,
        "colocar_de_parte" or "aside" => PesoCmDecision.ColocarDeParte,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Peso CM decision.")
    };

    public static string? ToStorage(PesoCmDecision value) => value switch
    {
        PesoCmDecision.None => null,
        PesoCmDecision.Manter => "manter",
        PesoCmDecision.ColocarDeParte => "colocar_de_parte",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Peso CM decision.")
    };
}