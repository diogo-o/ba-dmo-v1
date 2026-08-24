using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

/// <summary>
/// U-04 canonical module catalog tests (Plan-V3 modules/00 GLM-CAT-02/03):
/// complete catalog, stable IDs, canonical order, capability uniqueness and
/// ownership. Nothing invented beyond modules/00.
/// </summary>
public class CanonicalModuleCatalogTests
{
    [Fact]
    public void Catalog_ContainsExactlyTheCanonicalModules()
    {
        var catalog = CanonicalModuleCatalog.Instance;

        Assert.Equal(
            [
                "jobon", "boquilhas", "controlo", "peso", "pegamentos", "ferramentas",
                "armazem", "reparacao_interna", "reparacao_externa", "tampoes",
                "historia", "admin"
            ],
            catalog.Modules.Select(m => m.ModuleId).ToArray());
        Assert.Equal(12, catalog.Count);
    }

    [Fact]
    public void Catalog_CanonicalOrder_MatchesModules00()
    {
        var catalog = CanonicalModuleCatalog.Instance;

        Assert.Equal(
            [5, 10, 20, 21, 22, 40, 50, 60, 70, 80, 90, 99],
            catalog.Modules.Select(m => m.CanonicalOrder).ToArray());
    }

    [Fact]
    public void Catalog_InitialRoutes_MatchModules00()
    {
        var catalog = CanonicalModuleCatalog.Instance;

        string Route(string moduleId)
        {
            Assert.True(catalog.TryGetModule(moduleId, out var module), moduleId);
            return module.InitialRoute;
        }

        Assert.Equal("/jobon", Route("jobon"));
        Assert.Equal("/boquilhas", Route("boquilhas"));
        Assert.Equal("/peso", Route("peso"));
        Assert.Equal("/pegamentos", Route("pegamentos"));
        Assert.Equal("/ferramentas", Route("ferramentas"));
        Assert.Equal("/armazem", Route("armazem"));
        Assert.Equal("/reparacao-interna", Route("reparacao_interna"));
        Assert.Equal("/reparacao-externa", Route("reparacao_externa"));
        Assert.Equal("/tampoes", Route("tampoes"));
        Assert.Equal("/historia", Route("historia"));
        Assert.Equal("/admin", Route("admin"));
    }

    [Fact]
    public void Controlo_IsAFunctionalArea_WithFolhaControloCapabilities()
    {
        var catalog = CanonicalModuleCatalog.Instance;

        Assert.True(catalog.TryGetModule("controlo", out var controlo));
        Assert.Equal(ModuleKind.FunctionalArea, controlo.Kind);
        // R010: the Folha de Controlo is a workflow INSIDE the Controlo area — the area
        // carries its sheet capabilities (view/edit/submit/review) rather than a new module.
        Assert.Equal(
            [
                "controlo.edit", "controlo.review", "controlo.submit", "controlo.view"
            ],
            controlo.Capabilities.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray());

        Assert.Contains("controlo", CanonicalModuleCatalog.AreaChildren.Keys);
        Assert.Equal(
            ["peso", "pegamentos"],
            CanonicalModuleCatalog.AreaChildren["controlo"].ToArray());
    }

    [Fact]
    public void AllOtherEntries_AreModules()
    {
        var catalog = CanonicalModuleCatalog.Instance;

        Assert.All(
            catalog.Modules.Where(m => m.ModuleId != "controlo"),
            m => Assert.Equal(ModuleKind.Module, m.Kind));
    }

    [Fact]
    public void Capabilities_AreExactlyTheCanonicalSet_WithExactOwnership()
    {
        var catalog = CanonicalModuleCatalog.Instance;

        var ownership = catalog.Modules
            .SelectMany(m => m.Capabilities.Select(c => (Capability: c.Id, Module: m.ModuleId)))
            .OrderBy(x => x.Capability, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                ("admin.gerir", "admin"),
                ("audit.export", "admin"),
                ("audit.view", "admin"),
                ("controlo.edit", "controlo"),
                ("controlo.review", "controlo"),
                ("controlo.submit", "controlo"),
                ("controlo.view", "controlo"),
                ("ferramentas.configure", "ferramentas"),
                ("jobon.configure", "jobon"),
                ("jobon.confirmar", "jobon"),
                ("jobon.edit", "jobon"),
                ("jobon.view", "jobon"),
                ("peso.aprovar", "peso"),
                ("reparacao_interna.corrigir", "reparacao_interna")
            ],
            ownership);
    }

    [Fact]
    public void CapabilityIds_AreUnique_AcrossTheCatalog()
    {
        var catalog = CanonicalModuleCatalog.Instance;

        var ids = catalog.Modules.SelectMany(m => m.Capabilities.Select(c => c.Id)).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(14, ids.Count);
    }

    [Fact]
    public void ModulesWithoutCapabilities_HaveNoneDeclared()
    {
        var catalog = CanonicalModuleCatalog.Instance;

        foreach (var moduleId in new[]
                 {
                     "boquilhas", "pegamentos", "armazem", "reparacao_externa",
                     "tampoes", "historia"
                 })
        {
            Assert.True(catalog.TryGetModule(moduleId, out var module), moduleId);
            Assert.Empty(module.Capabilities);
        }
    }
}
