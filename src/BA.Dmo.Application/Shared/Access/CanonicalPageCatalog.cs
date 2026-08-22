namespace BA.Dmo.Application.Shared.Access;

/// <summary>
/// Canonical page catalog (Plan-V3 05_SHL §5 module routes). Auth/session
/// routes (/login, /logout, /access-denied, /no-access, /) belong to the
/// auth/shell units (U-05/U-07), not to the module page catalog.
/// Dynamic segments (e.g. /jobon/{id}) are page-implementation detail, not
/// catalog routes; query strings carry filter state (GLM-SHL-05).
/// </summary>
public static class CanonicalPageCatalog
{
    public const string JobonFolhaPageId = "jobon.folha";
    public const string BoquilhasRegistoPageId = "boquilhas.registo";
    public const string PesoOperadorPageId = "peso.operador";
    public const string PesoResponsavelPageId = "peso.responsavel";
    public const string PegamentosFolhaPageId = "pegamentos.folha";
    public const string FerramentasListaPageId = "ferramentas.lista";
    public const string ArmazemMapaPageId = "armazem.mapa";
    public const string ReparacaoInternaRegistoPageId = "reparacao_interna.registo";
    public const string ReparacaoExternaListasPageId = "reparacao_externa.listas";
    public const string TampoesQuantidadesPageId = "tampoes.quantidades";
    public const string HistoriaConsultaPageId = "historia.consulta";
    public const string AdminGestaoPageId = "admin.gestao";

    public static PageCatalog Instance { get; } = Build();

    public static PageCatalog Build() => new(new[]
    {
        // UD-16/DS-01: Job On is the landing of functional users; entry
        // requires jobon.view (universal for active users EXCEPT templates
        // holding the admin module — owner decision: the admin never
        // receives jobon.view and lands on /admin instead).
        new PageDefinition(
            JobonFolhaPageId, CanonicalModuleCatalog.JobonModuleId, "/jobon",
            CanonicalModuleCatalog.JobonViewCapabilityId,
            displayOrder: 5, isLanding: true),
        new PageDefinition(
            BoquilhasRegistoPageId, CanonicalModuleCatalog.BoquilhasModuleId, "/boquilhas",
            requiredCapabilityId: null, displayOrder: 10),
        // Peso experience (UD-06/UD-15): Operador = module entry WITHOUT
        // peso.aprovar; Responsável = peso.aprovar. Exclusivity guards are
        // enforced by the shell/routing unit (U-07); the catalog states the
        // required capability.
        new PageDefinition(
            PesoOperadorPageId, CanonicalModuleCatalog.PesoModuleId, "/peso",
            requiredCapabilityId: null, displayOrder: 21),
        new PageDefinition(
            PesoResponsavelPageId, CanonicalModuleCatalog.PesoModuleId, "/peso/responsavel",
            CanonicalModuleCatalog.PesoAprovarCapabilityId, displayOrder: 21),
        new PageDefinition(
            PegamentosFolhaPageId, CanonicalModuleCatalog.PegamentosModuleId, "/pegamentos",
            requiredCapabilityId: null, displayOrder: 22),
        new PageDefinition(
            FerramentasListaPageId, CanonicalModuleCatalog.FerramentasModuleId, "/ferramentas",
            requiredCapabilityId: null, displayOrder: 40),
        new PageDefinition(
            ArmazemMapaPageId, CanonicalModuleCatalog.ArmazemModuleId, "/armazem",
            requiredCapabilityId: null, displayOrder: 50),
        new PageDefinition(
            ReparacaoInternaRegistoPageId, CanonicalModuleCatalog.ReparacaoInternaModuleId,
            "/reparacao-interna", requiredCapabilityId: null, displayOrder: 60),
        new PageDefinition(
            ReparacaoExternaListasPageId, CanonicalModuleCatalog.ReparacaoExternaModuleId,
            "/reparacao-externa", requiredCapabilityId: null, displayOrder: 70),
        new PageDefinition(
            TampoesQuantidadesPageId, CanonicalModuleCatalog.TampoesModuleId, "/tampoes",
            requiredCapabilityId: null, displayOrder: 80),
        new PageDefinition(
            HistoriaConsultaPageId, CanonicalModuleCatalog.HistoriaModuleId, "/historia",
            requiredCapabilityId: null, displayOrder: 90),
        // Administração (GLM-ACC-06): the Admin area requires admin.gerir.
        new PageDefinition(
            AdminGestaoPageId, CanonicalModuleCatalog.AdminModuleId, "/admin",
            CanonicalModuleCatalog.AdminGerirCapabilityId, displayOrder: 99)
    });
}
