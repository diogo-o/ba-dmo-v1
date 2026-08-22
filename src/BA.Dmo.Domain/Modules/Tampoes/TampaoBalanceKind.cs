namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// U-17 — The two Tampões balances (N10 <c>tampao_saldos</c>; GLM-TP-04,
/// TAMPOES_DESIGN_BRIEF §2). Exactly two states: <c>Enchidos</c> and
/// <c>Por encher</c>. The <c>Maquinado</c> third state does NOT exist without an
/// explicit functional decision.
/// </summary>
public enum TampaoBalanceKind
{
    Enchidos,
    PorEncher
}

/// <summary>Codec between the domain enum and domain identifiers.</summary>
public static class TampaoBalanceKindCodec
{
    /// <summary>Column/balance-friendly identifier (storage-agnostic).</summary>
    public static string ToKey(TampaoBalanceKind kind) => kind switch
    {
        TampaoBalanceKind.Enchidos => "enchidos",
        TampaoBalanceKind.PorEncher => "por_encher",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, $"Unknown balance kind: {kind}")
    };

    public static TampaoBalanceKind FromKey(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "enchidos" => TampaoBalanceKind.Enchidos,
        "por_encher" => TampaoBalanceKind.PorEncher,
        _ => throw new InvalidOperationException($"Unknown balance kind: {value}")
    };
}