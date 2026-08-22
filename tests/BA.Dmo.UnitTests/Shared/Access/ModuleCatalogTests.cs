using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

/// <summary>
/// U-01 kernel unit tests: module catalog foundation
/// (Plan-V3 TD-10/GLM-CAT; roadmap U-01 requires "catálogo vazio válido").
/// The canonical entries of modules/00 are registered in U-04, not here.
/// </summary>
public class ModuleCatalogTests
{
    [Fact]
    public void EmptyCatalog_IsValid()
    {
        var catalog = ModuleCatalog.Empty;

        Assert.Equal(0, catalog.Count);
        Assert.Empty(catalog.Modules);
        Assert.False(catalog.ContainsModule("jobon"));
        Assert.False(catalog.TryGetModule("jobon", out _));
        Assert.False(catalog.IsCapabilityKnown("jobon.view"));
    }

    [Fact]
    public void EmptyCatalog_NullOrBlankQueries_NeverThrow()
    {
        var catalog = ModuleCatalog.Empty;

        Assert.False(catalog.ContainsModule(null!));
        Assert.False(catalog.ContainsModule("  "));
        Assert.False(catalog.TryGetModule(null!, out _));
        Assert.False(catalog.IsCapabilityKnown(null!));
    }

    [Fact]
    public void Catalog_ExposesEntriesInCanonicalOrder()
    {
        var later = new ModuleDefinition("peso", "Peso", ModuleKind.Module, 21, "/peso");
        var earlier = new ModuleDefinition("jobon", "Job On", ModuleKind.Module, 5, "/jobon");

        var catalog = new ModuleCatalog([later, earlier]);

        Assert.Equal(["jobon", "peso"], catalog.Modules.Select(m => m.ModuleId).ToArray());
    }

    [Fact]
    public void Catalog_SameCanonicalOrder_FallsBackToModuleIdOrder()
    {
        var b = new ModuleDefinition("pegamentos", "Pegamentos", ModuleKind.Module, 22, "/pegamentos");
        var a = new ModuleDefinition("peso", "Peso", ModuleKind.Module, 22, "/peso");

        var catalog = new ModuleCatalog([b, a]);

        Assert.Equal(["pegamentos", "peso"], catalog.Modules.Select(m => m.ModuleId).ToArray());
    }

    [Fact]
    public void Catalog_LookupFindsRegisteredModule_AndItsCapabilities()
    {
        var jobon = new ModuleDefinition(
            "jobon", "Job On", ModuleKind.Module, 5, "/jobon",
            [new Capability("jobon.view"), new Capability("jobon.edit")]);

        var catalog = new ModuleCatalog([jobon]);

        Assert.True(catalog.ContainsModule("jobon"));
        Assert.True(catalog.TryGetModule("jobon", out var found));
        Assert.Equal("Job On", found.DisplayName);
        Assert.True(catalog.IsCapabilityKnown("jobon.view"));
        Assert.False(catalog.IsCapabilityKnown("peso.aprovar"));
    }

    [Fact]
    public void Catalog_DuplicateModuleId_IsRejected()
    {
        var first = new ModuleDefinition("peso", "Peso", ModuleKind.Module, 21, "/peso");
        var duplicate = new ModuleDefinition("peso", "Peso (duplicado)", ModuleKind.Module, 22, "/peso");

        Assert.Throws<ArgumentException>(() => new ModuleCatalog([first, duplicate]));
    }

    [Fact]
    public void Catalog_NullEntries_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ModuleCatalog(null!));
        Assert.Throws<ArgumentNullException>(() => new ModuleCatalog([null!]));
    }

    [Fact]
    public void FunctionalAreaKind_IsRepresented()
    {
        var controlo = new ModuleDefinition("controlo", "Controlo", ModuleKind.FunctionalArea, 20, "/controlo");

        Assert.Equal(ModuleKind.FunctionalArea, controlo.Kind);
        Assert.Empty(controlo.Capabilities);
    }
}
