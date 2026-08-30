namespace BA.Dmo.Domain.Modules.JobOn;

/// <summary>
/// Canonical factory production lines (B1–C3) — the SINGLE source of truth for
/// every line list that represents the factory's actual production lines.
///
/// Convergence point (SHARED PRODUCTION CONTEXT — Phase 2): the Reparação
/// Interna module catalog (<c>ReparacaoInternaModuleCatalog.Lines</c>) and the
/// shared current-production reader (<c>CurrentProductionLines.Canonical</c>,
/// the default line set of <c>ResolveAllLinesAsync</c>) both alias this list,
/// so the production rail, the Reparação Interna line cards and the module
/// validation can never drift apart.
///
/// The canonical operational order (B1–B3, C1–C3) is part of the contract:
/// consumers present lines in this order and must not reorder it.
/// </summary>
public static class ProductionLines
{
    /// <summary>Canonical factory production lines (B1–C3).</summary>
    public static readonly IReadOnlyList<string> Canonical = ["B1", "B2", "B3", "C1", "C2", "C3"];
}
