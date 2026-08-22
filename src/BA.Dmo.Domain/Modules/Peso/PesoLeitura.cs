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
/// One per-CM decision of a comparison (GLM-PESO-06.5): keeper or set aside,
/// compared to the approved-data averages of the immutable base. Justification
/// is mandatory when at least one CM is set aside.
/// </summary>
public sealed record PesoComparisonCmDecision
{
    public string CmNumber { get; set; } = string.Empty;

    public PesoCmDecision Decision { get; set; } = PesoCmDecision.None;

    public decimal? PesoAtual { get; set; }

    public decimal? CapacidadeAtual { get; set; }
}