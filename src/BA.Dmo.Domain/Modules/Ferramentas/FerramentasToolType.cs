namespace BA.Dmo.Domain.Modules.Ferramentas;

/// <summary>
/// Tool types for the Ferramentas master register (N04, GLM-FERR-01/02/12).
/// CM and MF are DISTINCT types with separate identities and histories — never fused.
/// The schema (N04) also admits BQ, PU, CS as tool identities; this unit implements
/// the CM/MF registo flow per the roadmap. BQ remains a separate operational module.
/// </summary>
public enum FerramentasToolType
{
    CM,
    MF,
    BQ,
    PU,
    CS
}

/// <summary>Codec between the domain enum and the N04 stored text discriminator.</summary>
public static class FerramentasToolTypeCodec
{
    public static string ToStorage(FerramentasToolType type) => type switch
    {
        FerramentasToolType.CM => "CM",
        FerramentasToolType.MF => "MF",
        FerramentasToolType.BQ => "BQ",
        FerramentasToolType.PU => "PU",
        FerramentasToolType.CS => "CS",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown tool type: {type}")
    };

    public static FerramentasToolType FromStorage(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "CM" => FerramentasToolType.CM,
        "MF" => FerramentasToolType.MF,
        "BQ" => FerramentasToolType.BQ,
        "PU" => FerramentasToolType.PU,
        "CS" => FerramentasToolType.CS,
        _ => throw new InvalidOperationException($"Unknown persisted Ferramentas tool type: {value}")
    };
}