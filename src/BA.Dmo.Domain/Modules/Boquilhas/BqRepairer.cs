namespace BA.Dmo.Domain.Modules.Boquilhas;

/// <summary>
/// U-19 — A BQ repairer (shared repair vocabulary, N08/TD-15). Deactivated
/// (inactive) repairers are preserved for history, never deleted. The movement
/// stores the actually-chosen repairer; later config changes never rewrite the
/// history (BOQUILHAS_INTERFACE_BEHAVIOR §9, GLM-BQ-05).
/// </summary>
public sealed class BqRepairer
{
    public Guid RepairerId { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    /// <summary>Repair types this repairer is capable of (CM/MF/BQ), many-to-many.</summary>
    public IReadOnlySet<string> SupportedTypes { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    public string? CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// U-19 — The default and permitted repairers associated with one line
/// (B1–C3). The default repairer is suggested when creating a Saída but can be
/// changed per movement; "Sem associação" is allowed. If the default is
/// deactivated the line requires a new association (BOQUILHAS_INTERFACE_BEHAVIOR §9).
/// </summary>
public sealed class BqLineRepairerDefault
{
    public string Line { get; set; } = string.Empty;

    public Guid? DefaultRepairerId { get; set; }

    public IReadOnlyList<Guid> AllowedRepairerIds { get; set; } = Array.Empty<Guid>();
}