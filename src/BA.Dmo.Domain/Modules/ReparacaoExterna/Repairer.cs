namespace BA.Dmo.Domain.Modules.ReparacaoExterna;

/// <summary>
/// Canonical repairer (N08 <c>repairers</c>; TD-15). Repairers are deactivated,
/// never deleted (REPARACAO_EXTERNA_DESIGN_BRIEF §10; GLM-RE-05), so historical
/// movements/exit lists keep their identity. R004: the repairer MAY support multiple
/// repair types (CM/MF/BQ) — a many-to-many capability stored in
/// <c>repairer_repair_types</c>, distinct from the line-default convenience.
/// </summary>
public sealed class Repairer
{
    public Guid RepairerId { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    /// <summary>Repair types this repairer is capable of (subset of CM/MF/BQ), many-to-many.</summary>
    public IReadOnlySet<string> SupportedTypes { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}