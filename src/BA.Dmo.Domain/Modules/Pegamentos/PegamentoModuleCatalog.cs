namespace BA.Dmo.Domain.Modules.Pegamentos;

/// <summary>
/// Pegamentos module constants (GLM-PEG-02).
/// No extra capabilities in V1.
/// </summary>
public static class PegamentoModuleCatalog
{
    /// <summary>Module identifier used in authorization/access control.</summary>
    public const string ModuleId = "pegamentos";

    /// <summary>Default tolerance corridor (±0.20mm).</summary>
    public const decimal DefaultTolerance = 0.20m;
}