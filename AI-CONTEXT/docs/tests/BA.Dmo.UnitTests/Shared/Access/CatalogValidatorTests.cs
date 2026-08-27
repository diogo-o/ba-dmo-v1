using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

/// <summary>
/// U-04 catalog validation tests: invalid canonical configuration fails
/// explicitly and deterministically — never silently repaired.
/// </summary>
public class CatalogValidatorTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoAreas =
        new Dictionary<string, IReadOnlyList<string>>();

    private static ModuleCatalog ModuleCatalogOf(params ModuleDefinition[] modules) => new(modules);

    private static PageCatalog PageCatalogOf(params PageDefinition[] pages) => new(pages);

    [Fact]
    public void CanonicalConfiguration_IsValid()
    {
        CatalogValidator.Validate(
            CanonicalModuleCatalog.Instance,
            CanonicalPageCatalog.Instance,
            CanonicalModuleCatalog.AreaChildren);
    }

    [Fact]
    public void PageReferencingUnknownModule_Fails()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition("jobon", "Job On", ModuleKind.Module, 5, "/jobon"));
        var pages = PageCatalogOf(
            new PageDefinition("ghost.page", "ghost", "/ghost", null, 1, isLanding: true));

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, NoAreas));

        Assert.Contains(ex.Violations, v => v.Contains("unknown module 'ghost'", StringComparison.Ordinal));
    }

    [Fact]
    public void PageRequiringUnknownCapability_Fails()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition("jobon", "Job On", ModuleKind.Module, 5, "/jobon"));
        var pages = PageCatalogOf(
            new PageDefinition("jobon.folha", "jobon", "/jobon", "jobon.missing", 1, isLanding: true));

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, NoAreas));

        Assert.Contains(ex.Violations, v => v.Contains("unknown capability", StringComparison.Ordinal));
    }

    [Fact]
    public void PageRequiringCapabilityOfAnotherModule_Fails()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition(
                "peso", "Peso", ModuleKind.Module, 21, "/peso",
                new[] { new Capability("peso.aprovar") }),
            new ModuleDefinition("admin", "Administração", ModuleKind.Module, 99, "/admin"));
        var pages = PageCatalogOf(
            new PageDefinition("admin.gestao", "admin", "/admin", "peso.aprovar", 99, isLanding: true));

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, NoAreas));

        Assert.Contains(ex.Violations, v => v.Contains("owned by module 'peso'", StringComparison.Ordinal));
    }

    [Fact]
    public void CapabilityDeclaredByTwoModules_Fails()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition(
                "peso", "Peso", ModuleKind.Module, 21, "/peso",
                new[] { new Capability("peso.aprovar") }),
            new ModuleDefinition(
                "pegamentos", "Pegamentos", ModuleKind.Module, 22, "/pegamentos",
                new[] { new Capability("peso.aprovar") }));
        var pages = PageCatalogOf(
            new PageDefinition("peso.pagina", "peso", "/peso", null, 21, isLanding: true));

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, NoAreas));

        Assert.Contains(ex.Violations, v => v.Contains("must be unique", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingLandingPage_Fails()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition("peso", "Peso", ModuleKind.Module, 21, "/peso"));
        var pages = PageCatalogOf(
            new PageDefinition("peso.pagina", "peso", "/peso", null, 21));

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, NoAreas));

        Assert.Contains(ex.Violations, v => v.Contains("no landing page", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoLandingPages_Fail()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition("peso", "Peso", ModuleKind.Module, 21, "/peso"),
            new ModuleDefinition("tampoes", "Tampões", ModuleKind.Module, 80, "/tampoes"));
        var pages = PageCatalogOf(
            new PageDefinition("peso.pagina", "peso", "/peso", null, 21, isLanding: true),
            new PageDefinition("tampoes.pagina", "tampoes", "/tampoes", null, 80, isLanding: true));

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, NoAreas));

        Assert.Contains(ex.Violations, v => v.Contains("more than one landing", StringComparison.Ordinal));
    }

    [Fact]
    public void InactiveLandingPage_Fails()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition("peso", "Peso", ModuleKind.Module, 21, "/peso"));
        var pages = PageCatalogOf(
            new PageDefinition("peso.pagina", "peso", "/peso", null, 21, isActive: false, isLanding: true));

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, NoAreas));

        Assert.Contains(ex.Violations, v => v.Contains("inactive", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateModuleInitialRoutes_Fail()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition("peso", "Peso", ModuleKind.Module, 21, "/peso"),
            new ModuleDefinition("peso2", "Peso 2", ModuleKind.Module, 22, "/peso"));
        var pages = PageCatalogOf(
            new PageDefinition("peso.pagina", "peso", "/peso", null, 21, isLanding: true));

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, NoAreas));

        Assert.Contains(ex.Violations, v => v.Contains("duplicate initial route", StringComparison.Ordinal));
    }

    [Fact]
    public void AreaWithUnknownChild_Fails()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition("controlo", "Controlo", ModuleKind.Module, 20, "/controlo"),
            new ModuleDefinition(
                "peso", "Peso", ModuleKind.Module, 21, "/peso", isAssignable: false));
        var pages = PageCatalogOf(
            new PageDefinition("peso.pagina", "peso", "/peso", null, 21, isLanding: true));
        var areas = new Dictionary<string, IReadOnlyList<string>>
        {
            ["controlo"] = new[] { "peso", "ghost_child" }
        };

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, areas));

        Assert.Contains(ex.Violations, v => v.Contains("unknown child", StringComparison.Ordinal));
    }

    [Fact]
    public void AreaParentThatIsNotAssignableModule_Fails()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition(
                "peso", "Peso", ModuleKind.Module, 21, "/peso", isAssignable: false));
        var pages = PageCatalogOf(
            new PageDefinition("peso.pagina", "peso", "/peso", null, 21, isLanding: true));
        var areas = new Dictionary<string, IReadOnlyList<string>>
        {
            ["peso"] = new[] { "peso" }
        };

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, areas));

        Assert.Contains(ex.Violations,
            v => v.Contains("not an assignable module", StringComparison.Ordinal));

    }

    [Fact]
    public void InternalChildThatIsIndependentlyAssignable_Fails()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition("controlo", "Controlo", ModuleKind.Module, 20, "/controlo"),
            new ModuleDefinition("peso", "Peso", ModuleKind.Module, 21, "/peso"));
        var pages = PageCatalogOf(
            new PageDefinition("peso.pagina", "peso", "/peso", null, 21, isLanding: true));
        var areas = new Dictionary<string, IReadOnlyList<string>>
        {
            ["controlo"] = new[] { "peso" }
        };

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, areas));

        Assert.Contains(ex.Violations,
            v => v.Contains("must not be independently assignable", StringComparison.Ordinal));
    }

    [Fact]
    public void AllViolations_AreReportedTogether()
    {
        var modules = ModuleCatalogOf(
            new ModuleDefinition("peso", "Peso", ModuleKind.Module, 21, "/peso"));
        var pages = PageCatalogOf(
            new PageDefinition("a.page", "ghost", "/ghost", "ghost.cap", 1),
            new PageDefinition("b.page", "peso", "/peso", "unknown.cap", 2));

        var ex = Assert.Throws<CatalogValidationException>(
            () => CatalogValidator.Validate(modules, pages, NoAreas));

        Assert.True(ex.Violations.Count >= 3, string.Join(" | ", ex.Violations));
    }
}
