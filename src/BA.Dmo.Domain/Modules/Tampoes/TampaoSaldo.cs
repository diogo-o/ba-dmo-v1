namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// U-17 — The two balances of a Tampões configuration
/// (N10 <c>tampao_saldos</c>; GLM-TP-04). Both are ≥ 0; a third "Maquinado" state
/// is intentionally absent. Balances change ONLY through recorded movements
/// (GLM-DATA-04.4) and never go negative (GLM-TP-08 hard block).
/// </summary>
public sealed class TampaoSaldo
{
    public Guid TampaoSaldoId { get; set; } = Guid.NewGuid();

    public Guid TampaoConfigurationId { get; set; }

    /// <summary>Enchidos balance (≥ 0).</summary>
    public int Enchidos { get; set; }

    /// <summary>Por encher balance (≥ 0).</summary>
    public int PorEncher { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Reads a balance by kind.</summary>
    public int Get(TampaoBalanceKind kind) =>
        kind == TampaoBalanceKind.Enchidos ? Enchidos : PorEncher;

    /// <summary>Returns true when every balance is ≥ 0.</summary>
    public bool IsNonNegative => Enchidos >= 0 && PorEncher >= 0;
}