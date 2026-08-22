namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// U-17 — Tampões module constants (GLM-TP-01..13). Module <c>tampoes</c> is a
/// single assignable module with tabs Registo/Consulta/Planeamento/Histórico and
/// Opções (right); the Operator has full access (no capability, GLM-TP-02).
/// V1 controls aggregate quantities by technical configuration with NO individual
/// numbers and NO tool/reference association (USER CONFIRMED, GLM-TP-01).
/// </summary>
public static class TampoesModuleCatalog
{
    /// <summary>Canonical module id (shared Access catalog).</summary>
    public const string ModuleId = "tampoes";

    /// <summary>Initial comparable fields (brief §2).</summary>
    public const string DefaultDiameterField = "Diâmetro";
    public const string DefaultCaloteField = "Profundidade/Calote";
}