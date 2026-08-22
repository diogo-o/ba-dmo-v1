namespace BA.Dmo.Domain.Modules.Peso;

/// <summary>
/// Peso lot process (GLM-PESO-06, TD-17). The operational process NNPB/PS is
/// chosen when creating the Peso lot and inherited by Job On, Novo controlo and
/// Comparação — never re-asked to the Operador.
/// </summary>
public enum PesoProcesso
{
    Nnpb,
    Ps
}

/// <summary>
/// Peso lot process persistence helpers (N06 <c>peso_lotes.processo</c>).
/// </summary>
public static class PesoProcessoCodec
{
    public static PesoProcesso Parse(string value) => value?.Trim().ToUpperInvariant() switch
    {
        "NNPB" => PesoProcesso.Nnpb,
        "PS" => PesoProcesso.Ps,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Peso process.")
    };

    public static string ToStorage(PesoProcesso value) => value switch
    {
        PesoProcesso.Nnpb => "NNPB",
        PesoProcesso.Ps => "PS",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Peso process.")
    };
}