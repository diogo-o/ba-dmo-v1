namespace BA.Dmo.Domain.Modules.ReparacaoInterna;

/// <summary>
/// Tool types of the Reparação Interna workflow (N22 <c>internal_repair_records</c>
/// CHECK <c>tool_type IN ('CM','MF','BQ')</c>; OWNER DECISION R009). CM, MF and BQ are
/// distinct and never fused — an internal repair targets exactly one type (TD-22 number
/// model). BQ is a third recordable type: the production reference is common to all three;
/// the type-specific lot/context and repaired numbers may vary.
/// </summary>
public enum InternalRepairToolType
{
    CM,
    MF,
    BQ
}

/// <summary>Codec between the domain enum and the stored text discriminator.</summary>
public static class InternalRepairToolTypeCodec
{
    public static string ToStorage(InternalRepairToolType type) => type switch
    {
        InternalRepairToolType.CM => "CM",
        InternalRepairToolType.MF => "MF",
        InternalRepairToolType.BQ => "BQ",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown internal repair tool type: {type}")
    };

    public static InternalRepairToolType FromStorage(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "CM" => InternalRepairToolType.CM,
        "MF" => InternalRepairToolType.MF,
        "BQ" => InternalRepairToolType.BQ,
        _ => throw new InvalidOperationException($"Unknown persisted internal repair tool type: {value}")
    };
}