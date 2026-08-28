namespace BA.Dmo.Domain.Modules.Peso;

/// <summary>
/// U-10 — Module catalog constants for Peso (Plan-V3 modules/03, 04_ACC §5,
/// TD-17/TD-25/TD-28/TD-31). The base module grants entry; <c>peso.aprovar</c>
/// separates the Responsável experience and the approval/decision commands
/// (GLM-PESO-02/GLM-ACC-05). Process constants are editable in
/// <c>peso_settings</c> (defaults below, TD-12) — single C# source of truth
/// (GLM-PESO-05); the JS preview never duplicates them (server-injected).
/// </summary>
public static class PesoModuleCatalog
{
    public const string PesoModuleId = "peso";
    public const string PesoAprovarCapabilityId = "peso.aprovar";

    /// <summary>Default NNPB glass constant (TD-12/GLM-PESO-05).</summary>
    public const decimal ConstantNnpb = 2.4027m;

    /// <summary>Default PS glass constant (TD-12/GLM-PESO-05).</summary>
    public const decimal ConstantPs = 2.4231m;

    /// <summary>Canonical allowed machine lines of a Peso lot.</summary>
    public static readonly string[] AllowedLines = ["B1", "B2", "B3", "C1", "C2", "C3"];
}

/// <summary>
/// Required minimum allowed lines for a Peso lot (N06
/// <c>ck_peso_lotes_allowed_lines</c>: cardinality &gt;= 1).
/// </summary>
public static class PesoLoteRules
{
    public const int MinAllowedLines = 1;
}