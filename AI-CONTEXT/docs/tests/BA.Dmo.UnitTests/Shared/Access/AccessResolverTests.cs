using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

public class AccessResolverTests
{
    private readonly AccessResolver _resolver = new(
        CanonicalModuleCatalog.Instance,
        CanonicalPageCatalog.Instance,
        CanonicalModuleCatalog.AreaChildren);

    private static AccessTemplateDefinition Template(params ModuleGrant[] grants) =>
        new("t-test", "Template de teste", active: true, grants);

    private EffectiveAccess Resolve(
        FunctionalProfile profile = FunctionalProfile.OperatorController,
        params ModuleGrant[] grants) =>
        _resolver.Resolve(Template(grants), profile);

    [Fact]
    public void InactiveTemplate_GrantsNothing()
    {
        var template = new AccessTemplateDefinition(
            "t-off", "Inativo", active: false,
            [new ModuleGrant(CanonicalModuleCatalog.JobonModuleId, [])]);

        var access = _resolver.Resolve(template, FunctionalProfile.OperatorController);

        Assert.True(access.IsEmpty);
        Assert.Equal(FirstPageOutcome.NoAccess, _resolver.ResolveFirstPage(access).Outcome);
    }

    [Fact]
    public void JobOn_MustBeAssigned_AndIsTheOperationalLanding()
    {
        var assigned = Resolve(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.JobonModuleId, []));
        var notAssigned = Resolve(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.BoquilhasModuleId, []));

        Assert.Equal(FirstPageOutcome.Landing, _resolver.ResolveFirstPage(assigned).Outcome);
        Assert.True(assigned.HasCapability(CanonicalModuleCatalog.JobonViewCapabilityId));
        Assert.False(notAssigned.HasModule(CanonicalModuleCatalog.JobonModuleId));
        Assert.Equal("/boquilhas", _resolver.ResolveFirstPage(notAssigned).Page!.Route);
    }

    [Fact]
    public void EmptyOperationalTemplate_HasNoAccess()
    {
        var access = Resolve();

        Assert.True(access.IsEmpty);
        Assert.Equal(FirstPageOutcome.NoAccess, _resolver.ResolveFirstPage(access).Outcome);
    }

    [Fact]
    public void MultipleTemplates_UnionTheirAssignedModules_UnderOneProfile()
    {
        var access = _resolver.Resolve(
            [
                new AccessTemplateDefinition(
                    "t-jobon", "Job On", active: true,
                    [new ModuleGrant(CanonicalModuleCatalog.JobonModuleId, [])]),
                new AccessTemplateDefinition(
                    "t-controlo", "Controlo", active: true,
                    [new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, [])]),
                new AccessTemplateDefinition(
                    "t-inactive", "Ignorado", active: false,
                    [new ModuleGrant(CanonicalModuleCatalog.BoquilhasModuleId, [])])
            ],
            FunctionalProfile.Responsible);

        Assert.True(access.HasModule(CanonicalModuleCatalog.JobonModuleId));
        Assert.True(access.HasModule(CanonicalModuleCatalog.ControloAreaId));
        Assert.True(access.HasModule(CanonicalModuleCatalog.PesoModuleId));
        Assert.True(access.HasModule(CanonicalModuleCatalog.PegamentosModuleId));
        Assert.False(access.HasModule(CanonicalModuleCatalog.BoquilhasModuleId));
        Assert.True(access.HasCapability(CanonicalModuleCatalog.JobonEditCapabilityId));
        Assert.True(access.HasCapability(CanonicalModuleCatalog.ControloReviewCapabilityId));
    }

    [Fact]
    public void AdminProfile_IsPure_AndGetsConfirmedAdminCapabilities()
    {
        var access = Resolve(
            FunctionalProfile.Admin,
            new ModuleGrant(CanonicalModuleCatalog.AdminModuleId, []),
            new ModuleGrant(CanonicalModuleCatalog.JobonModuleId, []));

        Assert.True(access.HasModule(CanonicalModuleCatalog.AdminModuleId));
        Assert.False(access.HasModule(CanonicalModuleCatalog.JobonModuleId));
        Assert.True(access.HasCapability(CanonicalModuleCatalog.AdminGerirCapabilityId));
        Assert.True(access.HasCapability(CanonicalModuleCatalog.AuditViewCapabilityId));
        Assert.True(access.HasCapability(CanonicalModuleCatalog.AuditExportCapabilityId));
        Assert.Equal("/admin", _resolver.ResolveFirstPage(access).Page!.Route);
    }

    [Theory]
    [InlineData(FunctionalProfile.OperatorController)]
    [InlineData(FunctionalProfile.Responsible)]
    public void OperationalProfiles_CannotReceiveAdmin(FunctionalProfile profile)
    {
        var access = Resolve(profile, new ModuleGrant(CanonicalModuleCatalog.AdminModuleId, []));

        Assert.True(access.IsEmpty);
        Assert.False(access.HasCapability(CanonicalModuleCatalog.AdminGerirCapabilityId));
    }

    [Fact]
    public void Profile_DerivesJobOnBehavior_AndLegacyCapabilityArraysDoNot()
    {
        var legacyCapabilities = new[]
        {
            CanonicalModuleCatalog.JobonViewCapabilityId,
            CanonicalModuleCatalog.JobonEditCapabilityId,
            CanonicalModuleCatalog.JobonConfigureCapabilityId
        };
        var operatorAccess = Resolve(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.JobonModuleId, legacyCapabilities));
        var responsibleAccess = Resolve(
            FunctionalProfile.Responsible,
            new ModuleGrant(CanonicalModuleCatalog.JobonModuleId, []));

        Assert.True(operatorAccess.HasCapability(CanonicalModuleCatalog.JobonViewCapabilityId));
        Assert.True(operatorAccess.HasCapability(CanonicalModuleCatalog.JobonConfirmarCapabilityId));
        Assert.False(operatorAccess.HasCapability(CanonicalModuleCatalog.JobonEditCapabilityId));
        Assert.False(operatorAccess.HasCapability(CanonicalModuleCatalog.JobonConfigureCapabilityId));

        Assert.True(responsibleAccess.HasCapability(CanonicalModuleCatalog.JobonViewCapabilityId));
        Assert.True(responsibleAccess.HasCapability(CanonicalModuleCatalog.JobonConfirmarCapabilityId));
        Assert.True(responsibleAccess.HasCapability(CanonicalModuleCatalog.JobonEditCapabilityId));
        Assert.True(responsibleAccess.HasCapability(CanonicalModuleCatalog.JobonConfigureCapabilityId));
    }

    [Fact]
    public void Controlo_IsOneGrant_AndExpandsItsInternalTechnicalAreas()
    {
        var access = Resolve(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, []));

        Assert.True(access.HasModule(CanonicalModuleCatalog.ControloAreaId));
        Assert.True(access.HasModule(CanonicalModuleCatalog.PesoModuleId));
        Assert.True(access.HasModule(CanonicalModuleCatalog.PegamentosModuleId));
        Assert.Equal(
            [CanonicalModuleCatalog.PesoModuleId, CanonicalModuleCatalog.PegamentosModuleId],
            access.VisibleAreaChildren[CanonicalModuleCatalog.ControloAreaId]
                .Select(module => module.ModuleId).ToArray());
    }

    [Fact]
    public void ControloBehavior_ComesFromProfile()
    {
        var operatorAccess = Resolve(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, []));
        var responsibleAccess = Resolve(
            FunctionalProfile.Responsible,
            new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, []));

        Assert.True(operatorAccess.HasCapability(CanonicalModuleCatalog.ControloViewCapabilityId));
        Assert.True(operatorAccess.HasCapability(CanonicalModuleCatalog.ControloEditCapabilityId));
        Assert.True(operatorAccess.HasCapability(CanonicalModuleCatalog.ControloSubmitCapabilityId));
        Assert.False(operatorAccess.HasCapability(CanonicalModuleCatalog.ControloReviewCapabilityId));
        Assert.False(operatorAccess.HasCapability(CanonicalModuleCatalog.PesoAprovarCapabilityId));

        Assert.True(responsibleAccess.HasCapability(CanonicalModuleCatalog.ControloViewCapabilityId));
        Assert.False(responsibleAccess.HasCapability(CanonicalModuleCatalog.ControloEditCapabilityId));
        Assert.False(responsibleAccess.HasCapability(CanonicalModuleCatalog.ControloSubmitCapabilityId));
        Assert.True(responsibleAccess.HasCapability(CanonicalModuleCatalog.ControloReviewCapabilityId));
        Assert.True(responsibleAccess.HasCapability(CanonicalModuleCatalog.PesoAprovarCapabilityId));
    }

    [Fact]
    public void FerramentasMasterEdit_IsResponsibleBehavior()
    {
        var operatorAccess = Resolve(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.FerramentasModuleId, []));
        var responsibleAccess = Resolve(
            FunctionalProfile.Responsible,
            new ModuleGrant(CanonicalModuleCatalog.FerramentasModuleId, []));

        Assert.False(operatorAccess.HasCapability(CanonicalModuleCatalog.FerramentasConfigureCapabilityId));
        Assert.True(responsibleAccess.HasCapability(CanonicalModuleCatalog.FerramentasConfigureCapabilityId));
    }

    [Fact]
    public void Historia_IsDerivedForOperationalAccess_AndNeverForPureAdmin()
    {
        var operational = Resolve(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.BoquilhasModuleId, []));
        var admin = Resolve(
            FunctionalProfile.Admin,
            new ModuleGrant(CanonicalModuleCatalog.AdminModuleId, []));

        Assert.True(operational.HasModule(CanonicalModuleCatalog.HistoriaModuleId));
        Assert.False(admin.HasModule(CanonicalModuleCatalog.HistoriaModuleId));
    }

    [Fact]
    public void PesoPageVariant_FollowsProfileDerivedCapability()
    {
        var operatorAccess = Resolve(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, []));
        var responsibleAccess = Resolve(
            FunctionalProfile.Responsible,
            new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, []));

        Assert.True(_resolver.IsPageAccessible(operatorAccess, Page("peso.operador")));
        Assert.False(_resolver.IsPageAccessible(operatorAccess, Page("peso.responsavel")));
        Assert.False(_resolver.IsPageAccessible(responsibleAccess, Page("peso.operador")));
        Assert.True(_resolver.IsPageAccessible(responsibleAccess, Page("peso.responsavel")));
    }

    [Fact]
    public void NavigationModules_FollowCanonicalOrder()
    {
        var access = Resolve(
            FunctionalProfile.OperatorController,
            new ModuleGrant(CanonicalModuleCatalog.TampoesModuleId, []),
            new ModuleGrant(CanonicalModuleCatalog.JobonModuleId, []),
            new ModuleGrant(CanonicalModuleCatalog.ControloAreaId, []));

        Assert.Equal(
            ["jobon", "controlo", "peso", "pegamentos", "tampoes", "historia"],
            access.NavigationModules.Select(module => module.ModuleId).ToArray());
    }

    [Fact]
    public void NewAssignableCatalogModule_RequiresNoResolverChange()
    {
        var modules = new ModuleCatalog(CanonicalModuleCatalog.Instance.Modules.Append(
            new ModuleDefinition("novo", "Novo", ModuleKind.Module, 45, "/novo")));
        var pages = new PageCatalog(CanonicalPageCatalog.Instance.Pages.Append(
            new PageDefinition("novo.index", "novo", "/novo", null, 45)));
        var resolver = new AccessResolver(modules, pages, CanonicalModuleCatalog.AreaChildren);

        var access = resolver.Resolve(
            Template(new ModuleGrant("novo", [])),
            FunctionalProfile.OperatorController);

        Assert.True(access.HasModule("novo"));
        Assert.Contains(resolver.AccessiblePages(access), page => page.Route == "/novo");
    }

    private static PageDefinition Page(string pageId)
    {
        Assert.True(CanonicalPageCatalog.Instance.TryGetById(pageId, out var page));
        return page!;
    }
}
