using System.Text.RegularExpressions;

namespace BA.Dmo.Domain.Modules.Boquilhas;

/// <summary>
/// U-19 — Boquilhas module constants (modules/01_BOQUILHAS_SPEC GLM-BQ, TD-24).
/// The <c>boquilhas</c> module is a daily, high-frequency, quantity-based
/// operational domain (reference + lot) with NO functional operator/responsável
/// split and NO capabilities in V1 (GLM-BQ-02): any user granted the module may
/// perform every operation. The BQ operational identity lives in the <c>bq_*</c>
/// schema — it is intentionally NOT the Ferramentas <c>tool_lotes</c> CM/MF
/// identity (N04 BOUNDARY NOTE) and NOT the CM/MF batch repair model (AB-03).
/// </summary>
public static class BoquilhasModuleCatalog
{
    /// <summary>Canonical module id of the Boquilhas reading/operational module.</summary>
    public const string ModuleId = "boquilhas";

    /// <summary>Canonical CSV of line ids usable in the side panel and lot creation (B1–C3).</summary>
    public static readonly string[] Lines =
    {
        "B1", "B2", "B3", "C1", "C2", "C3"
    };

    /// <summary>
    /// Canonical reference pattern <c>^[A-Z][0-9]{3}$</c> (06_DATA §3.2,
    /// N03_bq CHECK ck_bq_lotes_reference).
    /// </summary>
    public static readonly Regex ReferencePattern = new(
        "^[A-Z][0-9]{3}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Canonical pagination sizes used by the module lists (20/40/60).</summary>
    public static readonly int[] CanonicalPageSizes = { 20, 40, 60 };

    /// <summary>Reference pattern error code (server-side mirror of the DB CHECK).</summary>
    public const string ReferenceInvalidCode = "BQ_REFERENCE_INVALID";
}