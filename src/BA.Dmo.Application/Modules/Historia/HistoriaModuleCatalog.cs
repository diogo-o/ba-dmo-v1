namespace BA.Dmo.Application.Modules.Historia;

/// <summary>
/// U-18 — História constants (modules/11_HISTORIA_E_AUDITORIA_SPEC, TD-24).
/// The module <c>historia</c> is a read-only transversal view: presentation of
/// persisted append-only audit facts from the modules the user's active template
/// grants. It has NO own capabilities in V1 (GLM-HIST-02) and never writes.
///
/// Origin modules are the domains whose events appear in the transversal view.
/// <c>admin</c> and the reading module itself (<c>historia</c>) are excluded from
/// the general scope; admin events are only shown to identities holding
/// <c>audit.view</c> (GLM-HIST-04 Administração row).
/// </summary>
public static class HistoriaModuleCatalog
{
    /// <summary>Canonical module id of the História reading module.</summary>
    public const string ModuleId = "historia";

    /// <summary>
    /// The origin/domain module ids surfaced by the transversal view (excluding
    /// the <c>admin</c> and <c>historia</c> reading modules). Events of these
    /// modules are shown to an identity that is granted the same module (TD-24).
    /// </summary>
    public static readonly string[] OriginModuleIds =
    {
        Shared.Access.CanonicalModuleCatalog.JobonModuleId,
        Shared.Access.CanonicalModuleCatalog.BoquilhasModuleId,
        Shared.Access.CanonicalModuleCatalog.PesoModuleId,
        Shared.Access.CanonicalModuleCatalog.PegamentosModuleId,
        Shared.Access.CanonicalModuleCatalog.FerramentasModuleId,
        Shared.Access.CanonicalModuleCatalog.ArmazemModuleId,
        Shared.Access.CanonicalModuleCatalog.ReparacaoInternaModuleId,
        Shared.Access.CanonicalModuleCatalog.ReparacaoExternaModuleId,
        Shared.Access.CanonicalModuleCatalog.TampoesModuleId
    };

    /// <summary>Canonical pagination sizes used by the História view (20/40/60).</summary>
    public static readonly int[] CanonicalPageSizes = { 20, 40, 60 };
}