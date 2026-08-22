using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

/// <summary>
/// U-04 access resolution tests (Plan-V3 GLM-ACC-02/03/06/07, GLM-SHL-03/04,
/// GLM-CTR-02, UD-16/DS-01). Landing rule (owner-confirmed design):
/// functional users land on Job On (universal jobon.view); templates holding
/// the admin module receive no jobon.view and fall back to the first
/// accessible page in canonical order (/admin for the bootstrap admin);
/// deterministic fallback only when Job On is genuinely unavailable;
/// explicit NoAccess when nothing is accessible.
/// </summary>
public class AccessResolverTests
{
    private readonly AccessResolver _resolver = new(
        CanonicalModuleCatalog.Instance,
        CanonicalPageCatalog.Instance,
        CanonicalModuleCatalog.AreaChildren);

    private static AccessTemplateDefinition Template(
        params ModuleGrant[] grants) =>
        new("t-test", "Template de teste", active: true, grants);

    [Fact]
    public void InactiveTemplate_GrantsNothing_SafeState()
    {
        // Scenario 12 (GLM-ACC-07): deactivated template → no access.
        var template = new AccessTemplateDefinition(
            "t-off", "Inativo", active: false,
            new[] { new ModuleGrant("boquilhas", []) });

        var access = _resolver.Resolve(template);

        Assert.True(access.IsEmpty);
        Assert.Empty(access.NavigationModules);
        var resolution = _resolver.ResolveFirstPage(access);
        Assert.Equal(FirstPageOutcome.NoAccess, resolution.Outcome);
        Assert.Null(resolution.Page);
    }

    [Fact]
    public void Landing_IsJobOn_ForABoquilhasOnlyUser()
    {
        // Scenario 1: Boquilhas-only user → landing Job On (consulta).
        var access = _resolver.Resolve(Template(new ModuleGrant("boquilhas", [])));

        var resolution = _resolver.ResolveFirstPage(access);

        Assert.Equal(FirstPageOutcome.Landing, resolution.Outcome);
        Assert.Equal("/jobon", resolution.Page!.Route);
    }

    [Fact]
    public void Landing_IsAdmin_ForAnAdminOnlyTemplate()
    {
        // Owner decision: an Administrator's only working area is the single
        // Admin page (users/applications/audit/definitions). It is NOT granted
        // jobon.view, so it does not land on the Job On work landing; its first
        // accessible page resolves to /admin.
        var access = _resolver.Resolve(Template(
            new ModuleGrant("admin", new[] { "admin.gerir", "audit.view" })));

        var resolution = _resolver.ResolveFirstPage(access);

        Assert.False(access.HasCapability("jobon.view"));
        Assert.False(access.HasModule("jobon"));
        Assert.Equal(FirstPageOutcome.FallbackCanonicalOrder, resolution.Outcome);
        Assert.Equal("/admin", resolution.Page!.Route);
        Assert.True(access.HasModule("admin"));
        Assert.Contains(access.AccessiblePagesFor(_resolver), p => p.Route == "/admin");
    }

    [Fact]
    public void Landing_IsJobOn_EvenWithZeroOperationalGrants()
    {
        // GLM-SHL-03.4: zero own tabs → the user still sees Job On (consulta).
        var access = _resolver.Resolve(Template());

        var resolution = _resolver.ResolveFirstPage(access);

        Assert.Equal(FirstPageOutcome.Landing, resolution.Outcome);
        Assert.Equal("/jobon", resolution.Page!.Route);
        Assert.True(access.HasCapability("jobon.view"));
    }

    [Fact]
    public void Landing_DoesNotDependOnRoleNames()
    {
        // Templates NAMED like roles but holding identical grants resolve
        // identically: behavior derives from grants, never from names.
        var operador = new AccessTemplateDefinition(
            "t-1", "Operador", active: true, new[] { new ModuleGrant("peso", []) });
        var responsavel = new AccessTemplateDefinition(
            "t-2", "Administrador", active: true, new[] { new ModuleGrant("peso", []) });

        Assert.Equal(
            _resolver.ResolveFirstPage(_resolver.Resolve(operador)).Page!.Route,
            _resolver.ResolveFirstPage(_resolver.Resolve(responsavel)).Page!.Route);
        Assert.Equal("/jobon", _resolver.ResolveFirstPage(_resolver.Resolve(operador)).Page!.Route);
    }

    [Fact]
    public void PreferredFirstPageId_IsNotUsedInV1()
    {
        // 05_SHL §4: preferred_first_page remains read-only and unused; the
        // fixed Job On policy applies regardless of the stored value.
        var template = new AccessTemplateDefinition(
            "t-pref", "Com preferência", active: true,
            new[] { new ModuleGrant("boquilhas", []) },
            preferredFirstPageId: "boquilhas.registo");

        var resolution = _resolver.ResolveFirstPage(_resolver.Resolve(template));

        Assert.Equal(FirstPageOutcome.Landing, resolution.Outcome);
        Assert.Equal("/jobon", resolution.Page!.Route);
    }

    [Fact]
    public void Fallback_WhenLandingGenuinelyUnavailable_IsFirstAccessibleInCanonicalOrder()
    {
        // Page catalog WITHOUT an active Job On landing: deterministic
        // canonical-order fallback (never Boquilhas/Peso/Admin hardcoded).
        var pagesWithoutJobon = new PageCatalog(new[]
        {
            new PageDefinition("boquilhas.registo", "boquilhas", "/boquilhas", null, 10),
            new PageDefinition("peso.operador", "peso", "/peso", null, 21),
            new PageDefinition("tampoes.quantidades", "tampoes", "/tampoes", null, 80)
        });
        var resolver = new AccessResolver(
            CanonicalModuleCatalog.Instance, pagesWithoutJobon,
            CanonicalModuleCatalog.AreaChildren);

        var access = resolver.Resolve(Template(
            new ModuleGrant("tampoes", []),
            new ModuleGrant("boquilhas", [])));

        var resolution = resolver.ResolveFirstPage(access);

        Assert.Equal(FirstPageOutcome.FallbackCanonicalOrder, resolution.Outcome);
        // Canonical order decides: boquilhas (10) before tampoes (80).
        Assert.Equal("/boquilhas", resolution.Page!.Route);
    }

    [Fact]
    public void NoAccessiblePage_YieldsExplicitNoAccess()
    {
        // Catalog without the Job On module + empty page catalog: nothing is
        // accessible → explicit NoAccess (GLM-SHL-06 safe state).
        var modules = new ModuleCatalog(new[]
        {
            new ModuleDefinition("boquilhas", "Boquilhas", ModuleKind.Module, 10, "/boquilhas")
        });
        var resolver = new AccessResolver(
            modules, new PageCatalog(Array.Empty<PageDefinition>()),
            new Dictionary<string, IReadOnlyList<string>>());

        var access = resolver.Resolve(Template());

        var resolution = resolver.ResolveFirstPage(access);

        Assert.Equal(FirstPageOutcome.NoAccess, resolution.Outcome);
        Assert.Null(resolution.Page);
    }

    [Fact]
    public void NavigationModules_FollowCanonicalOrder()
    {
        var access = _resolver.Resolve(Template(
            new ModuleGrant("tampoes", []),
            new ModuleGrant("boquilhas", []),
            new ModuleGrant("armazem", [])));

        Assert.Equal(
            ["jobon", "boquilhas", "armazem", "tampoes"],
            access.NavigationModules.Select(m => m.ModuleId).ToArray());
    }

    [Fact]
    public void ControloArea_VisibleOnlyWithAuthorizedChildren()
    {
        // Scenarios 4/5/6 (GLM-ACC-07, GLM-CTR-05).
        var pesoOnly = _resolver.Resolve(Template(new ModuleGrant("peso", [])));
        Assert.True(pesoOnly.VisibleAreaChildren.ContainsKey("controlo"));
        Assert.Equal(["peso"], pesoOnly.VisibleAreaChildren["controlo"].Select(m => m.ModuleId).ToArray());

        var pegamentosOnly = _resolver.Resolve(Template(new ModuleGrant("pegamentos", [])));
        Assert.Equal(
            ["pegamentos"],
            pegamentosOnly.VisibleAreaChildren["controlo"].Select(m => m.ModuleId).ToArray());

        var both = _resolver.Resolve(Template(
            new ModuleGrant("peso", []), new ModuleGrant("pegamentos", [])));
        Assert.Equal(
            ["peso", "pegamentos"],
            both.VisibleAreaChildren["controlo"].Select(m => m.ModuleId).ToArray());

        var neither = _resolver.Resolve(Template(new ModuleGrant("boquilhas", [])));
        Assert.False(neither.VisibleAreaChildren.ContainsKey("controlo"));
    }

    [Fact]
    public void AreaFirstPage_IsFirstAuthorizedChild_InCanonicalOrder()
    {
        // GLM-CAT-02 rule 1: área → primeira entrada filha autorizada.
        var both = _resolver.Resolve(Template(
            new ModuleGrant("pegamentos", []), new ModuleGrant("peso", [])));
        var first = _resolver.ResolveAreaFirstPage(both, "controlo");
        Assert.Equal("/peso", first!.Route); // peso (21) precedes pegamentos (22)

        var pegamentosOnly = _resolver.Resolve(Template(new ModuleGrant("pegamentos", [])));
        Assert.Equal("/pegamentos", _resolver.ResolveAreaFirstPage(pegamentosOnly, "controlo")!.Route);

        var none = _resolver.Resolve(Template(new ModuleGrant("boquilhas", [])));
        Assert.Null(_resolver.ResolveAreaFirstPage(none, "controlo"));
    }

    [Fact]
    public void PesoExperience_IsResolvedByCapability_NotByRole()
    {
        // GLM-ACC-05/UD-06: mutually exclusive experiences.
        var operador = _resolver.Resolve(Template(new ModuleGrant("peso", [])));
        var responsavel = _resolver.Resolve(Template(
            new ModuleGrant("peso", new[] { "peso.aprovar" })));

        Assert.True(_resolver.IsPageAccessible(operador, Page("peso.operador")));
        Assert.False(_resolver.IsPageAccessible(operador, Page("peso.responsavel")));

        Assert.False(_resolver.IsPageAccessible(responsavel, Page("peso.operador")));
        Assert.True(_resolver.IsPageAccessible(responsavel, Page("peso.responsavel")));
    }

    [Fact]
    public void Capabilities_ConstrainPageAccess()
    {
        // Admin page requires admin.gerir; audit tab capabilities stay grants.
        var withGerir = _resolver.Resolve(Template(
            new ModuleGrant("admin", new[] { "admin.gerir" })));
        var withoutGerir = _resolver.Resolve(Template(
            new ModuleGrant("admin", new[] { "audit.view" })));

        Assert.True(_resolver.IsPageAccessible(withGerir, Page("admin.gestao")));
        Assert.False(_resolver.IsPageAccessible(withoutGerir, Page("admin.gestao")));
        Assert.False(withoutGerir.HasCapability("admin.gerir"));
        Assert.True(withoutGerir.HasCapability("audit.view"));
    }

    [Fact]
    public void UnauthorizedModule_PagesAreNotAccessible()
    {
        var access = _resolver.Resolve(Template(new ModuleGrant("boquilhas", [])));

        Assert.False(_resolver.IsPageAccessible(access, Page("peso.operador")));
        Assert.False(_resolver.IsPageAccessible(access, Page("admin.gestao")));
        Assert.False(_resolver.IsPageAccessible(access, Page("armazem.mapa")));
    }

    [Fact]
    public void NewCatalogModule_RequiresNoNavigationChanges_Acceptance()
    {
        // U-04 acceptance (GLM-ARCH-08): adding a module = catalog entry +
        // page; navigation/derivation pick it up automatically.
        var extendedModules = new ModuleCatalog(CanonicalModuleCatalog.Instance.Modules
            .Append(new ModuleDefinition("novo", "Novo Módulo", ModuleKind.Module, 45, "/novo")));
        var extendedPages = new PageCatalog(CanonicalPageCatalog.Instance.Pages
            .Append(new PageDefinition("novo.pagina", "novo", "/novo", null, 45)));
        var resolver = new AccessResolver(
            extendedModules, extendedPages, CanonicalModuleCatalog.AreaChildren);
        CatalogValidator.Validate(extendedModules, extendedPages, CanonicalModuleCatalog.AreaChildren);

        var access = resolver.Resolve(Template(new ModuleGrant("novo", [])));

        Assert.Equal(
            new[] { "jobon", "novo" },
            access.NavigationModules.Select(m => m.ModuleId).Take(2).ToArray());
        Assert.Contains(access.NavigationModules, m => m.ModuleId == "novo");
        Assert.Contains(resolver.AccessiblePages(access), p => p.Route == "/novo");
    }

    [Fact]
    public void InactivePage_IsNeverAccessible()
    {
        var pages = new PageCatalog(new[]
        {
            new PageDefinition("jobon.folha", "jobon", "/jobon", "jobon.view", 5, isLanding: true),
            new PageDefinition("peso.operador", "peso", "/peso", null, 21, isActive: false)
        });
        var resolver = new AccessResolver(
            CanonicalModuleCatalog.Instance, pages, CanonicalModuleCatalog.AreaChildren);

        var access = resolver.Resolve(Template(new ModuleGrant("peso", [])));

        Assert.False(resolver.IsPageAccessible(access, pages.Pages.First(p => p.PageId == "peso.operador")));
    }

    private static PageDefinition Page(string pageId)
    {
        Assert.True(CanonicalPageCatalog.Instance.TryGetById(pageId, out var page), pageId);
        return page!;
    }
}

internal static class EffectiveAccessTestExtensions
{
    public static IReadOnlyList<PageDefinition> AccessiblePagesFor(
        this EffectiveAccess access, AccessResolver resolver) =>
        resolver.AccessiblePages(access);
}
