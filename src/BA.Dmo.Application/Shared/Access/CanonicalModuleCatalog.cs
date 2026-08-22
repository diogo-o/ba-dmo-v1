using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.Application.Shared.Access;

/// <summary>
/// Canonical module/capability catalog (Plan-V3 TD-10, GLM-CAT-02/03,
/// modules/00_MODULE_CATALOG.md). The catalog in code is the source of truth;
/// the DB mirror (module_catalog_mirror, U-02) serves Admin ordering/display
/// only and never grants access (GLM-ACC-03).
///
/// Entries reproduce modules/00 exactly: 12 entries, canonical order, initial
/// routes and declared capabilities. Nothing is invented: no extra modules,
/// capabilities, orders or routes.
/// </summary>
public static class CanonicalModuleCatalog
{
    public const string JobonModuleId = "jobon";
    public const string BoquilhasModuleId = "boquilhas";
    public const string ControloAreaId = "controlo";
    public const string PesoModuleId = "peso";
    public const string PegamentosModuleId = "pegamentos";
    public const string FerramentasModuleId = "ferramentas";
    public const string ArmazemModuleId = "armazem";
    public const string ReparacaoInternaModuleId = "reparacao_interna";
    public const string ReparacaoExternaModuleId = "reparacao_externa";
    public const string TampoesModuleId = "tampoes";
    public const string HistoriaModuleId = "historia";
    public const string AdminModuleId = "admin";

    public const string JobonViewCapabilityId = "jobon.view";
    public const string JobonEditCapabilityId = "jobon.edit";
    public const string JobonConfigureCapabilityId = "jobon.configure";
    public const string JobonConfirmarCapabilityId = "jobon.confirmar";
    public const string PesoAprovarCapabilityId = "peso.aprovar";
    public const string FerramentasConfigureCapabilityId = "ferramentas.configure";
    public const string ReparacaoInternaCorrigirCapabilityId = "reparacao_interna.corrigir";
    public const string AdminGerirCapabilityId = "admin.gerir";
    public const string AuditViewCapabilityId = "audit.view";
    public const string AuditExportCapabilityId = "audit.export";

    // Folha de Controlo (production-level summary sheet) capabilities —
    // OWNER DECISION R010: the sheet is a workflow INSIDE the Controlo area, not
    // a separate top-level module. These capabilities gate the sheet operations
    // (view/edit/submit/review-approve).
    public const string ControloViewCapabilityId = "controlo.view";
    public const string ControloEditCapabilityId = "controlo.edit";
    public const string ControloSubmitCapabilityId = "controlo.submit";
    public const string ControloReviewCapabilityId = "controlo.review";

    /// <summary>The canonical catalog with all modules/00 entries.</summary>
    public static ModuleCatalog Instance { get; } = Build();

    /// <summary>
    /// Functional-area children (GLM-CAT-02 rule 1 / GLM-CTR-02): Controlo is
    /// visible when at least one child is authorized; children are assignable
    /// separately and never fused.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> AreaChildren { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [ControloAreaId] = new[] { PesoModuleId, PegamentosModuleId }
        };

    /// <summary>
    /// Short PT-PT descriptions for the Administration "Aplicações" cards
    /// (admin design reference: module name + one-line description). Display
    /// only — no authorization semantics.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Descriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [JobonModuleId] = "Controlo de produção e verificações",
            [BoquilhasModuleId] = "Gestão de lotes e reparações",
            [ControloAreaId] = "Função de controlo (área com Peso e Pegamentos)",
            [PesoModuleId] = "Aprovação de peso e volume",
            [PegamentosModuleId] = "Registo de pegamentos",
            [FerramentasModuleId] = "Gestão de ferramentas",
            [ArmazemModuleId] = "Gestão de armazém",
            [ReparacaoInternaModuleId] = "Reparações internas",
            [ReparacaoExternaModuleId] = "Reparações externas",
            [TampoesModuleId] = "Gestão de tampões",
            [HistoriaModuleId] = "Histórico de registos",
            [AdminModuleId] = "Administração do portal"
        };

    /// <summary>Builds a catalog equal to <see cref="Instance"/> (for tests/extension).</summary>
    public static ModuleCatalog Build() => new(new[]
    {
        new ModuleDefinition(
            JobonModuleId, "Job On", ModuleKind.Module, 5, "/jobon",
            new[]
            {
                new Capability(JobonViewCapabilityId),
                new Capability(JobonEditCapabilityId),
                new Capability(JobonConfigureCapabilityId),
                new Capability(JobonConfirmarCapabilityId)
            }),
        new ModuleDefinition(BoquilhasModuleId, "Boquilhas", ModuleKind.Module, 10, "/boquilhas"),
        new ModuleDefinition(
            ControloAreaId, "Controlo", ModuleKind.FunctionalArea, 20, "/controlo",
            new[]
            {
                new Capability(ControloViewCapabilityId),
                new Capability(ControloEditCapabilityId),
                new Capability(ControloSubmitCapabilityId),
                new Capability(ControloReviewCapabilityId)
            }),
        new ModuleDefinition(
            PesoModuleId, "Peso", ModuleKind.Module, 21, "/peso",
            new[] { new Capability(PesoAprovarCapabilityId) }),
        new ModuleDefinition(PegamentosModuleId, "Pegamentos", ModuleKind.Module, 22, "/pegamentos"),
        new ModuleDefinition(
            FerramentasModuleId, "Ferramentas", ModuleKind.Module, 40, "/ferramentas",
            new[] { new Capability(FerramentasConfigureCapabilityId) }),
        new ModuleDefinition(ArmazemModuleId, "Armazém", ModuleKind.Module, 50, "/armazem"),
        new ModuleDefinition(
            ReparacaoInternaModuleId, "Reparação Interna", ModuleKind.Module, 60, "/reparacao-interna",
            new[] { new Capability(ReparacaoInternaCorrigirCapabilityId) }),
        new ModuleDefinition(
            ReparacaoExternaModuleId, "Reparação Externa", ModuleKind.Module, 70, "/reparacao-externa"),
        new ModuleDefinition(TampoesModuleId, "Tampões", ModuleKind.Module, 80, "/tampoes"),
        new ModuleDefinition(HistoriaModuleId, "História", ModuleKind.Module, 90, "/historia"),
        new ModuleDefinition(
            AdminModuleId, "Administração", ModuleKind.Module, 99, "/admin",
            new[]
            {
                new Capability(AdminGerirCapabilityId),
                new Capability(AuditViewCapabilityId),
                new Capability(AuditExportCapabilityId)
            })
    });
}
