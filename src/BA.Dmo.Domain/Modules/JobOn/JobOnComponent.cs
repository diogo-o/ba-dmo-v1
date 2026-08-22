namespace BA.Dmo.Domain.Modules.JobOn;

/// <summary>
/// Component per family per revision (N05). Represents one tool reference/lot combination.
/// source_tool_id/source_lot_id are physical links to Ferramentas module plus snapshots.
/// </summary>
public sealed record JobOnComponent
{
    /// <summary>Primary key.</summary>
    public Guid JobOnComponentId { get; init; }

    /// <summary>Parent revision ID.</summary>
    public Guid JobOnRevisionId { get; init; }

    /// <summary>Family (MP_CM, MF, BQ, PU, CAL, AN, ARR, PI, CS, TP, FO).</summary>
    public ComponentFamily Family { get; init; }

    /// <summary>Optional physical link to tool_references.</summary>
    public Guid? SourceToolId { get; init; }

    /// <summary>Optional physical link to tool_lotes.</summary>
    public Guid? SourceLotId { get; init; }

    /// <summary>Reference snapshot (reference number text).</summary>
    public string? ReferenceSnapshot { get; init; }

    /// <summary>Lot snapshot (lot identifier text).</summary>
    public string? LotSnapshot { get; init; }

    /// <summary>Technical name snapshot.</summary>
    public string? TechnicalNameSnapshot { get; init; }

    /// <summary>Planned quantity.</summary>
    public decimal? PlannedQuantity { get; init; }

    /// <summary>Stock snapshot.</summary>
    public decimal? StockSnapshot { get; init; }

    /// <summary>Usage percentage snapshot.</summary>
    public decimal? UsageSnapshot { get; init; }

    /// <summary>Notes field.</summary>
    public string? Notes { get; init; }

    /// <summary>Display order.</summary>
    public int DisplayOrder { get; init; }

    /// <summary>Component fields collection loaded separately.</summary>
    public IReadOnlyList<JobOnComponentField>? Fields { get; init; }

    /// <summary>CAL rows if this is a CAL family.</summary>
    public IReadOnlyList<JobOnComponentRow>? Rows { get; init; }

    /// <summary>Verifications linked to this component.</summary>
    public IReadOnlyList<JobOnVerificationOccurrence>? Verifications { get; init; }
}
