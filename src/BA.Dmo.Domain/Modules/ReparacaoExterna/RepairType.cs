namespace BA.Dmo.Domain.Modules.ReparacaoExterna;

/// <summary>
/// Repair type of an external exit list (N08 <c>repair_exits.repair_type</c>;
/// TD-22). BQ items flow by quantity; CM/MF items flow by individual numbered
/// piece. CM and MF are DISTINCT types that share the external cycle but are
/// never fused in the domain (GLM-RE-11).
/// U-15 V1 functional scope is CM + MF; BQ is schema-compat only until U-19
/// (owner decision A).
/// </summary>
public enum RepairType
{
    BQ,
    CM,
    MF
}

/// <summary>Codec between the domain repair type and the N08 stored text discriminator.</summary>
public static class RepairTypeCodec
{
    public static string ToStorage(RepairType type) => type switch
    {
        RepairType.BQ => "BQ",
        RepairType.CM => "CM",
        RepairType.MF => "MF",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown repair type: {type}")
    };

    public static RepairType FromStorage(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "BQ" => RepairType.BQ,
        "CM" => RepairType.CM,
        "MF" => RepairType.MF,
        _ => throw new InvalidOperationException($"Unknown persisted repair type: {value}")
    };
}