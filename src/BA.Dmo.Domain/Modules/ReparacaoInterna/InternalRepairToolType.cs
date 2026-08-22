namespace BA.Dmo.Domain.Modules.ReparacaoInterna;

/// <summary>
/// Tool types of the Reparação Interna workflow (N22 <c>internal_repair_records</c>
/// CHECK <c>tool_type IN ('CM','MF','BQ')</c>; OWNER DECISION R009 / 34_REPARACAO_INTERNA
/// 03_OWNER_DECISION_CM_MF_ONLY). Only <c>CM</c> and <c>MF</c> are recordable internal
/// repair types — the settled functional rule is that BQ is NOT selectable, accepted,
/// parsed, persisted or corrected as an internal repair type. BQ remains present only as
/// production/reference CONTEXT (e.g. a full reference like <c>5447T173</c> where <c>T173</c>
/// is context-only) and inside other modules (Job On, Ferramentas, Boquilhas, production
/// context) — it is simply not an internal-repair recordable type here.
/// </summary>
public enum InternalRepairToolType
{
    CM,
    MF
}

/// <summary>
/// Codec between the domain enum and the stored text discriminator. Only CM/MF are
/// recordable. <see cref="FromStorage"/> rejects any other persisted value (a legacy
/// <c>'BQ'</c> internal-repair row is invalid under CM/MF-only and must be reconciled at
/// the clean baseline — it is never reinterpreted as a recordable value here).
/// </summary>
public static class InternalRepairToolTypeCodec
{
    public static string ToStorage(InternalRepairToolType type) => type switch
    {
        InternalRepairToolType.CM => "CM",
        InternalRepairToolType.MF => "MF",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unknown internal repair tool type: {type}")
    };

    public static InternalRepairToolType FromStorage(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "CM" => InternalRepairToolType.CM,
        "MF" => InternalRepairToolType.MF,
        _ => throw new InvalidOperationException($"Invalid internal repair tool type: {value} (only CM/MF are recordable; BQ is not an internal repair type)")
    };
}