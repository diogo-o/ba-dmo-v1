using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

public class NavigationServiceTests
{
    private static readonly AccessResolver Resolver = new(
        CanonicalModuleCatalog.Instance,
        CanonicalPageCatalog.Instance,
        CanonicalModuleCatalog.AreaChildren);

    private static readonly NavigationService Service = new(
        CanonicalPageCatalog.Instance, Resolver, CanonicalModuleCatalog.Instance);

    private static EffectiveAccess Access(
        FunctionalProfile profile,
        params ModuleGrant[] grants) =>
        Resolver.Resolve(
            new AccessTemplateDefinition("t-test", "Template", active: true, grants),
            profile);

    [Fact]
    public void EmptyTemplate_ProducesNoNavigation()
    {
        var navigation = Service.Build(
            Access(FunctionalProfile.OperatorController), currentRoute: null);

        Assert.Empty(navigation.LeftItems);
        Assert.Null(navigation.AdminEntry);
    }

    [Fact]
    public void MultipleAssignedModules_RenderInCanonicalOrder()
    {
        var navigation = Service.Build(Access(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.JobonModuleId, []),
            new ModuleGrant(CanonicalModuleCatalog.BoquilhasModuleId, []),
            new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, []),
            new ModuleGrant(CanonicalModuleCatalog.FerramentasModuleId, [])), null);

        Assert.Equal(
            ["jobon", "boquilhas", "controlo", "ferramentas", "historia"],
            navigation.LeftItems.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void Controlo_RendersOneTopLevelEntry_WithoutInternalChildrenInTheShell()
    {
        var navigation = Service.Build(Access(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, [])), null);

        var tab = Assert.Single(navigation.LeftItems,
            item => item.Id == CanonicalModuleCatalog.ControloAreaId);
        Assert.IsType<NavigationTab>(tab);
        Assert.Equal("/controlo", tab.Route);
        Assert.DoesNotContain(navigation.LeftItems,
            item => item.Id is CanonicalModuleCatalog.PesoModuleId
                or CanonicalModuleCatalog.PegamentosModuleId);
    }

    [Fact]
    public void ControloEntry_IsStableAcrossFunctionalProfiles()
    {
        var operatorNavigation = Service.Build(Access(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, [])), null);
        var responsibleNavigation = Service.Build(Access(
            FunctionalProfile.Responsible,
            new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, [])), null);

        var operatorTab = Assert.Single(operatorNavigation.LeftItems,
            item => item.Id == CanonicalModuleCatalog.ControloAreaId);
        var responsibleTab = Assert.Single(responsibleNavigation.LeftItems,
            item => item.Id == CanonicalModuleCatalog.ControloAreaId);
        Assert.Equal("/controlo", operatorTab.Route);
        Assert.Equal("/controlo", responsibleTab.Route);
    }

    [Fact]
    public void Historia_IsDerivedAsAReadSurface()
    {
        var navigation = Service.Build(Access(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.BoquilhasModuleId, [])), null);

        Assert.Equal(["boquilhas", "historia"],
            navigation.LeftItems.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void AdminEntry_IsRightAligned_ForAdminProfileOnly()
    {
        var admin = Service.Build(Access(
            FunctionalProfile.Admin,
            new ModuleGrant(CanonicalModuleCatalog.AdminModuleId, [])), null);
        var operational = Service.Build(Access(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.AdminModuleId, [])), null);

        Assert.NotNull(admin.AdminEntry);
        Assert.Equal("/admin", admin.AdminEntry!.Route);
        Assert.Empty(admin.LeftItems);
        Assert.Null(operational.AdminEntry);
    }

    [Fact]
    public void ActiveState_FollowsCurrentRoute()
    {
        var controloAccess = Access(
            FunctionalProfile.Responsible,
            new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, []));
        var onPeso = Service.Build(controloAccess, "/peso/responsavel");
        var controlo = Assert.Single(onPeso.LeftItems,
            item => item.Id == CanonicalModuleCatalog.ControloAreaId);
        Assert.True(controlo.IsActive);

        var adminAccess = Access(
            FunctionalProfile.Admin,
            new ModuleGrant(CanonicalModuleCatalog.AdminModuleId, []));
        Assert.True(Service.Build(adminAccess, "/admin/users").AdminEntry!.IsActive);
    }

    [Fact]
    public void InactiveTemplate_ProducesNoNavigation()
    {
        var access = Resolver.Resolve(
            new AccessTemplateDefinition(
                "t-off", "Inativo", active: false,
                [new ModuleGrant(CanonicalModuleCatalog.JobonModuleId, [])]),
            FunctionalProfile.OperatorController);

        var navigation = Service.Build(access, null);

        Assert.Empty(navigation.LeftItems);
        Assert.Null(navigation.AdminEntry);
    }
}
