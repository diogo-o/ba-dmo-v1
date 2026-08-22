namespace BA.Dmo.Domain.Modules.Pegamentos;

/// <summary>
/// Record of the resolved historical production context for a Pegamento.
/// Provides the exact CM/BQ/MF tool snapshots from the pinned revision,
/// plus production/machine/reference identifiers for display and filename generation.
/// Per-component nominal values are frozen historical data from the exact revision.
/// </summary>
public sealed record PegamentoProductionContext(
    Guid JobOnId,
    Guid JobOnRevisionId,
    string ProductionCode,
    string MachineCode,
    string Reference,
    PegamentoToolSnapshot CmSnapshot,
    PegamentoToolSnapshot BqSnapshot,
    PegamentoToolSnapshot MfSnapshot,
    decimal? CmNominal,
    decimal? BqNominal,
    decimal? MfNominal)
{
    /// <summary>Gets all tool snapshots as a dictionary keyed by component.</summary>
    public IReadOnlyDictionary<PegamentoComponentKey, PegamentoToolSnapshot> ToolSnapshots => new Dictionary<PegamentoComponentKey, PegamentoToolSnapshot>
    {
        [PegamentoComponentKey.CM] = CmSnapshot,
        [PegamentoComponentKey.BQ] = BqSnapshot,
        [PegamentoComponentKey.MF] = MfSnapshot,
    };
}