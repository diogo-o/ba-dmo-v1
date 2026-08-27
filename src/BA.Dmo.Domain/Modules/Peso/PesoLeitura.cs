namespace BA.Dmo.Domain.Modules.Peso;

/// <summary>
/// A single Peso reading: CM number + weight in water (N06 <c>peso_leituras</c>;
/// UNIQUE(control, cm_number); GLM-PESO-04/06). Reading rows are append-only
/// facts (06_DATA §4.1); corrections create new readings.
/// </summary>
public sealed record PesoLeitura
{
    public Guid PesoLeituraId { get; set; } = Guid.NewGuid();

    public Guid PesoControloId { get; set; }

    public string CmNumber { get; set; } = string.Empty;

    public decimal? PesoEmAgua { get; set; }

    /// <summary>Computed glass weight (est. via <see cref="WeightCalculator"/>). Presentation only.</summary>
    public decimal? PesoVidro { get; set; }
}

/// <summary>
/// One per-CM decision of a comparison. The comparable value is the immutable,
/// server-calculated glass-weight snapshot; capacity and water weight are not
/// comparison values. Justification is mandatory when at least one CM is set aside.
/// </summary>
public sealed record PesoComparisonCmDecision
{
    public string CmNumber { get; set; } = string.Empty;

    public PesoCmDecision Decision { get; set; } = PesoCmDecision.None;

    public decimal? PesoAtual { get; set; }
}

/// <summary>Explicit current-CM to previous-CM glass-weight association.</summary>
public sealed record PesoComparisonCmSnapshot
{
    public string CurrentCmNumber { get; init; } = string.Empty;

    public string PreviousCmNumber { get; init; } = string.Empty;

    public decimal CurrentGlassWeight { get; init; }

    public decimal PreviousGlassWeight { get; init; }

    public decimal Difference { get; init; }

    public decimal DifferencePercent { get; init; }
}

/// <summary>
/// Immutable identity and value snapshot stored in peso_controlos.previous_control.
/// Both Job On identities are pinned so reference text is never used as identity.
/// </summary>
public sealed record PesoComparisonSnapshot
{
    public Guid CurrentControlId { get; init; }
    public Guid CurrentJobOnId { get; init; }
    public Guid CurrentJobOnRevisionId { get; init; }
    public string CurrentProductionCode { get; init; } = string.Empty;
    public string CurrentLine { get; init; } = string.Empty;
    public string CurrentLote { get; init; } = string.Empty;

    public Guid PreviousControlId { get; init; }
    public Guid PreviousJobOnId { get; init; }
    public Guid PreviousJobOnRevisionId { get; init; }
    public string PreviousProductionCode { get; init; } = string.Empty;
    public string PreviousLine { get; init; } = string.Empty;
    public string PreviousLote { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public IReadOnlyList<PesoComparisonCmSnapshot> Rows { get; init; } = Array.Empty<PesoComparisonCmSnapshot>();
}

/// <summary>Responsável decisions bound to every current CM in the snapshot.</summary>
public sealed record PesoComparisonDecisionSnapshot
{
    public string? Justification { get; init; }

    public IReadOnlyList<PesoComparisonCmDecision> Decisions { get; init; } = Array.Empty<PesoComparisonCmDecision>();
}
