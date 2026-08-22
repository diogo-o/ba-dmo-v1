namespace BA.Dmo.Domain.Modules.ReparacaoInterna;

/// <summary>
/// U-16 — Reparação Interna module constants. Module <c>reparacao_interna</c>
/// (CanonicalModuleCatalog) with the capability <c>reparacao_interna.corrigir</c>
/// for corrections (GLM-RI-02; 04_ACC §6). Reparação Interna is a distinct
/// workflow from Reparação Externa (REPARACAO_INTERNA_DESIGN_BRIEF §1/§6): quick
/// in-turn CM/MF repair records while production continues on the line.
/// </summary>
public static class ReparacaoInternaModuleCatalog
{
    /// <summary>Canonical module id (shared Access catalog).</summary>
    public const string ModuleId = "reparacao_interna";

    /// <summary>Capability required to correct an internal repair record.</summary>
    public const string CorrigirCapabilityId = "reparacao_interna.corrigir";

    /// <summary>Canonical lines of the line-card selector (B1–C3).</summary>
    public static readonly IReadOnlyList<string> Lines = new[] { "B1", "B2", "B3", "C1", "C2", "C3" };
}