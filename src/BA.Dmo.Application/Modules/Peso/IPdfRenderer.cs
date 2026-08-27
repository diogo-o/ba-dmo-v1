namespace BA.Dmo.Application.Modules.Peso;

/// <summary>
/// PDF renderer port (Plan-V3 06_DATA §16, 09_TEST §10.5; GLM-PESO-09). The
/// backend generates PDF bytes in memory from the approved snapshot; the
/// concrete library is an implementation decision (QuestPDF NOT required).
/// Application/domain code depends only on this interface — never on a concrete
/// PDF library.
/// </summary>
public interface IPdfRenderer
{
    /// <summary>
    /// Renders a Peso production document (folha de produção) as PDF bytes
    /// from the approved snapshot. Deterministic output for deterministic input.
    /// </summary>
    byte[] RenderPesoFolha(PesoFolhaPdf data);
}

/// <summary>
/// Structured data of a Peso production document derived from the APPROVED
/// snapshot (GLM-PESO-06.9/09). Never from live values changed later.
/// </summary>
public sealed record PesoFolhaPdf
{
    public bool IsComparison { get; init; }
    public string MoldNumber { get; init; } = string.Empty;
    public string NeckringNumber { get; init; } = string.Empty;
    public string ProductionCode { get; init; } = string.Empty;
    public string Line { get; init; } = string.Empty;
    public string Lote { get; init; } = string.Empty;
    public int Revision { get; init; }
    public decimal? PesoMedio { get; init; }
    public decimal? CapacidadeMedia { get; init; }
    public string? EstadoMolde { get; init; }
    public string? Processo { get; init; }
    public string? LoteIdentified { get; init; }
    public decimal? PesoNominal { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTimeOffset? ApprovedAtUtc { get; init; }

    // Explicitly confirmed previous-production identity (comparison records only)
    public string? PreviousProductionCode { get; init; }

    // Per-CM comparison
    public IReadOnlyList<PesoCmComparisonRow> CmRows { get; init; } = Array.Empty<PesoCmComparisonRow>();

    // Nominal / new-mould comparison
    public decimal? DeltaNominal { get; init; }
    public decimal? DeltaNominalPct { get; init; }

    // Other reference data
    public decimal? SapPesoMedio { get; init; }
    public string? SapPeriodo { get; init; }
    public decimal? TemperaturaC { get; init; }
    public decimal? Densidade { get; init; }
    public decimal? ConstanteGlassUsada { get; init; }
}

/// <summary>One per-CM comparison row for the PDF.</summary>
public sealed record PesoCmComparisonRow
{
    public string CurrentCmNumber { get; init; } = string.Empty;
    public string? PreviousCmNumber { get; init; }
    public decimal? PesoAtual { get; init; }
    public decimal? PesoAnterior { get; init; }
    public decimal? DeltaPeso { get; init; }
    public decimal? DeltaPesoPct { get; init; }
}
