using BA.Dmo.Application.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

/// <summary>
/// U-07 navigation derivation tests (Plan-V3 GLM-SHL-03, GLM-CTR-02,
/// GLM-CAT-02, GLM-ACC-05): tabs = authorized modules ∩ catalog in canonical
/// order; Controlo groups only authorized children and never appears empty;
/// Peso renders ONE entry whose route resolves the Operador/Responsável
/// experience via peso.aprovar (no manual selector); Administração is
/// right-aligned and exists only with admin.gerir; zero-grant active users
/// still see Job On (UD-16). No role-name branching anywhere.
/// </summary>
public class NavigationServiceTests
{
    private static readonly AccessResolver Resolver = new(
        CanonicalModuleCatalog.Instance,
        CanonicalPageCatalog.Instance,
        CanonicalModuleCatalog.AreaChildren);

    private static readonly NavigationService Service = new(
        CanonicalPageCatalog.Instance, Resolver, CanonicalModuleCatalog.Instance);

    private static EffectiveAccess Access(params ModuleGrant[] grants) =>
        Resolver.Resolve(new AccessTemplateDefinition(
            "t-test", "Template de teste", active: true, grants));

    [Fact]
    public void EmptyTemplate_ShowsOnlyJobOn_NoControlo_NoAdmin()
    {
        // GLM-SHL-03.4: zero operational tabs → Job On (consulta) remains.
        var navigation = Service.Build(Access(), currentRoute: null);

        var tab = Assert.Single(navigation.LeftItems);
        Assert.Equal(CanonicalModuleCatalog.JobonModuleId, tab.Id);
        Assert.Equal("/jobon", tab.Route);
        Assert.Null(navigation.AdminEntry);
    }

    [Fact]
    public void MultipleModules_RenderInCanonicalOrder_OnlyAuthorized()
    {
        // GLM-SHL-03.1/03.5: canonical order + unauthorized tabs never exist.
        var navigation = Service.Build(Access(
            new ModuleGrant("historia", []),
            new ModuleGrant("boquilhas", []),
            new ModuleGrant("ferramentas", []),
            new ModuleGrant("peso", [])), currentRoute: null);

        Assert.Equal(
            new[]
            {
                CanonicalModuleCatalog.JobonModuleId,
                CanonicalModuleCatalog.BoquilhasModuleId,
                CanonicalModuleCatalog.ControloAreaId,
                CanonicalModuleCatalog.FerramentasModuleId,
                CanonicalModuleCatalog.HistoriaModuleId
            },
            navigation.LeftItems.Select(i => i.Id).ToArray());
        Assert.DoesNotContain(navigation.LeftItems,
            i => i.Id == CanonicalModuleCatalog.ArmazemModuleId);
    }

    [Theory]
    [InlineData(true, false, new[] { "/peso" })]
    [InlineData(false, true, new[] { "/pegamentos" })]
    [InlineData(true, true, new[] { "/peso", "/pegamentos" })]
    public void ControloGroup_ShowsOnlyAuthorizedChildren(
        bool peso, bool pegamentos, string[] expectedChildRoutes)
    {
        // Scenarios 4/5/6 (GLM-ACC-07): Controlo with the authorized child
        // entries only — never fused, never empty.
        var grants = new List<ModuleGrant>();
        if (peso)
            grants.Add(new ModuleGrant("peso", []));
        if (pegamentos)
            grants.Add(new ModuleGrant("pegamentos", []));

        var navigation = Service.Build(Access(grants.ToArray()), currentRoute: null);

        var area = Assert.Single(navigation.LeftItems.OfType<NavigationArea>());
        Assert.Equal(CanonicalModuleCatalog.ControloAreaId, area.Id);
        Assert.Equal(expectedChildRoutes, area.Children.Select(c => c.Route).ToArray());
        Assert.Equal(expectedChildRoutes[0], area.Route); // primeira entrada filha
        // Children never appear as top-level tabs.
        Assert.DoesNotContain(navigation.LeftItems,
            i => i.Id is CanonicalModuleCatalog.PesoModuleId
                or CanonicalModuleCatalog.PegamentosModuleId);
    }

    [Fact]
    public void NoControloChildren_NoAreaEntry()
    {
        // GLM-CTR-02.4: area without authorized children never appears.
        var navigation = Service.Build(
            Access(new ModuleGrant("boquilhas", [])), currentRoute: null);

        Assert.DoesNotContain(navigation.LeftItems.OfType<NavigationArea>(),
            a => a.Id == CanonicalModuleCatalog.ControloAreaId);
    }

    [Fact]
    public void PesoEntry_ResolvesTheExperienceByCapability()
    {
        // GLM-ACC-05/GLM-SHL-03.3: ONE Peso entry; the route resolves the
        // exclusive experience — no manual selector.
        var operador = Service.Build(
            Access(new ModuleGrant("peso", [])), currentRoute: null);
        var responsavel = Service.Build(
            Access(new ModuleGrant("peso", new[] { "peso.aprovar" })), currentRoute: null);

        var operadorArea = Assert.Single(operador.LeftItems.OfType<NavigationArea>());
        var operadorPeso = Assert.Single(operadorArea.Children);
        Assert.Equal("/peso", operadorPeso.Route);

        var responsavelArea = Assert.Single(responsavel.LeftItems.OfType<NavigationArea>());
        var responsavelPeso = Assert.Single(responsavelArea.Children);
        Assert.Equal("/peso/responsavel", responsavelPeso.Route);
    }

    [Fact]
    public void AdminEntry_RequiresAdminGerir_AndIsRightAligned()
    {
        // GLM-SHL-03.1: Administração à direita; tab only when accessible.
        var withoutAdmin = Service.Build(
            Access(new ModuleGrant("boquilhas", [])), currentRoute: null);
        Assert.Null(withoutAdmin.AdminEntry);

        var adminOnly = Service.Build(
            Access(new ModuleGrant("admin", new[] { "admin.gerir" })), currentRoute: null);
        Assert.NotNull(adminOnly.AdminEntry);
        Assert.Equal("/admin", adminOnly.AdminEntry!.Route);
        // Administração is never a left operational tab.
        Assert.DoesNotContain(adminOnly.LeftItems,
            i => i.Id == CanonicalModuleCatalog.AdminModuleId);
    }

    [Fact]
    public void ActiveState_FollowsTheCurrentRoute()
    {
        var access = Access(
            new ModuleGrant("peso", new[] { "peso.aprovar" }),
            new ModuleGrant("admin", new[] { "admin.gerir" }));

        // Both peso routes mark the same Peso entry active (one module).
        var onResponsavel = Service.Build(access, "/peso/responsavel");
        var area = Assert.Single(onResponsavel.LeftItems.OfType<NavigationArea>());
        Assert.True(area.IsActive);
        Assert.True(Assert.Single(area.Children).IsActive);

        // Module sub-pages keep the module entry active.
        var onAdminSubPage = Service.Build(access, "/admin/users");
        Assert.True(onAdminSubPage.AdminEntry!.IsActive);

        // Unknown routes leave everything inactive.
        var unknown = Service.Build(access, "/unknown-route");
        Assert.False(unknown.AdminEntry!.IsActive);
        Assert.All(unknown.LeftItems, i => Assert.False(i.IsActive));
    }

    [Fact]
    public void InactiveTemplate_ProducesNoNavigation()
    {
        // Scenario 12 (GLM-ACC-07): deactivated template → no tabs at all.
        var access = Resolver.Resolve(new AccessTemplateDefinition(
            "t-off", "Inativo", active: false,
            new[] { new ModuleGrant("boquilhas", []) }));

        var navigation = Service.Build(access, currentRoute: null);

        Assert.Empty(navigation.LeftItems);
        Assert.Null(navigation.AdminEntry);
    }
}
