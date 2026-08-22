namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// U-17 — Planned Tampões need (N10 <c>tampao_planos</c>; GLM-TP-05.4,
/// TAMPOES_DESIGN_BRIEF §8/§10). Planning does NOT add/remove/reserve stock; the
/// difference vs <c>Enchidos</c> is informational. It may optionally reference a
/// Job On/production ONLY when there is an unambiguous relation; cancelling or
/// altering a plan never changes balances.
/// </summary>
public sealed class TampaoPlano
{
    public Guid TampaoPlanoId { get; set; } = Guid.NewGuid();

    public Guid TampaoConfigurationId { get; set; }

    /// <summary>Planned quantity (≥1).</summary>
    public int PlannedQty { get; set; }

    public DateOnly? PlannedForDate { get; set; }

    /// <summary>Optional unambiguous Job On link (read-only reference; never resolved by text).</summary>
    public Guid? JobOnId { get; set; }

    public string? ProductionCode { get; set; }

    public string? Notes { get; set; }

    /// <summary>Cancelling a plan preserves the fact row and never touches balances.</summary>
    public bool Canceled { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}