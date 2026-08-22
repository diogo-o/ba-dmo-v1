namespace BA.Dmo.Domain.Modules.Ferramentas;

/// <summary>
/// R003 — An APPEND-ONLY utilisation reading of a CM/MF tool lot
/// (reference + lot), per 02_DEC C8/C17 and the owner clarification: the `% use`
/// is taken MANUALLY from SAP by the operator — it is NOT auto-calculated and no
/// formula is invented/guessed. <c>sap_start</c>/<c>sap_end</c> and <c>percent_used</c>
/// are utilisation-life readings recorded AT THE MOMENT of the reading, snapshot per
/// reading so a later change never reinterprets recorded history. The reading also
/// stores the cycles/value used (<see cref="ValueCumulative"/>) + the delta
/// (<see cref="ValueAdded"/>) since the previous reading. Older readings are never
/// overwritten (append-only). "before" is recoverable from the prior record (the
/// smallest faithful model of C8's before/added/after/cumulative shape).
/// </summary>
public sealed class ToolUtilisationReading
{
    public Guid ToolUsageRecordId { get; set; } = Guid.NewGuid();

    public Guid ToolLoteId { get; set; }

    /// <summary>SAP start life reading snapshot at this reading (0–100), nullable.</summary>
    public decimal? SapStart { get; set; }

    /// <summary>SAP end / life-bound reading snapshot at this reading (0–100), nullable.</summary>
    public decimal? SapEnd { get; set; }

    /// <summary>% use recordered MANUALLY from SAP by the operator (0–100), nullable.</summary>
    public decimal? PercentUsed { get; set; }

    /// <summary>Cycles/value added since the previous reading (nullable when not known).</summary>
    public decimal? ValueAdded { get; set; }

    /// <summary>Cycles/value used (cumulative) at the time of this reading.</summary>
    public decimal ValueCumulative { get; set; }

    public string? Notes { get; set; }

    public string? ActorId { get; set; }

    public DateTimeOffset ReadingAtUtc { get; set; }
}

/// <summary>
/// R003 — Utilisation history + latest reading of a tool lot. <see cref="PercentUsed"/>
/// is simply the <b>recorded</b> <c>percent_used</c> of the LATEST reading (manual, from
/// SAP). No formula is applied. Empty history → null latest and null percent.
/// </summary>
public sealed record ToolUtilisationStatus(
    IReadOnlyList<ToolUtilisationReading> History,
    ToolUtilisationReading? Latest,
    decimal? PercentUsed);