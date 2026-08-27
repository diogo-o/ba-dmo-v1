using BA.Dmo.Application.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

/// <summary>
/// U-04 page catalog tests (Plan-V3 05_SHL §5): page/route uniqueness, route
/// grammar, page→module and required-capability consistency, single landing.
/// </summary>
public class CanonicalPageCatalogTests
{
    [Fact]
    public void PageCatalog_ContainsExactlyTheCanonicalPages()
    {
        var pages = CanonicalPageCatalog.Instance;

        Assert.Equal(
            [
                "jobon.folha", "boquilhas.registo", "controlo.resumo", "peso.operador", "peso.responsavel",
                "pegamentos.folha", "ferramentas.lista", "armazem.mapa",
                "reparacao_interna.registo", "reparacao_externa.listas",
                "tampoes.quantidades", "historia.consulta", "admin.gestao"
            ],
            pages.Pages.Select(p => p.PageId).ToArray());
    }

    [Fact]
    public void PageIds_AreUnique_AndRoutesAreUnique()
    {
        var pages = CanonicalPageCatalog.Instance;

        Assert.Equal(pages.Count, pages.Pages.Select(p => p.PageId).Distinct().Count());
        Assert.Equal(pages.Count, pages.Pages.Select(p => p.Route).Distinct().Count());
    }

    [Fact]
    public void Routes_MatchTheCanonicalGrammar()
    {
        var pages = CanonicalPageCatalog.Instance;

        Assert.All(pages.Pages, p => Assert.True(
            PageDefinition.IsValidRoute(p.Route), $"route '{p.Route}' must match the grammar"));
    }

    [Theory]
    [InlineData("/jobon")]
    [InlineData("/peso/responsavel")]
    [InlineData("/reparacao-externa")]
    [InlineData("/a/b/c")]
    public void RouteGrammar_AcceptsCanonicalShapes(string route)
    {
        Assert.True(PageDefinition.IsValidRoute(route));
    }

    [Theory]
    [InlineData("")]
    [InlineData("jobon")]            // missing leading slash
    [InlineData("/Jobon")]            // uppercase segment
    [InlineData("/jobon/")]           // trailing slash
    [InlineData("/-jobon")]           // segment must start with a letter
    [InlineData("/jobon/x y")]        // whitespace
    [InlineData("/jobon/{id}")]       // dynamic segments are not catalog routes
    [InlineData("/job_on")]           // underscore not in grammar
    public void RouteGrammar_RejectsInvalidShapes(string route)
    {
        Assert.False(PageDefinition.IsValidRoute(route));
    }

    [Fact]
    public void PageDefinition_Constructor_RejectsInvalidRoute()
    {
        Assert.Throws<ArgumentException>(() =>
            new PageDefinition("x.page", "jobon", "/Invalid", null, 1));
    }

    [Fact]
    public void EveryPage_ReferencesAKnownModule()
    {
        var pages = CanonicalPageCatalog.Instance;
        var modules = CanonicalModuleCatalog.Instance;

        Assert.All(pages.Pages, p =>
            Assert.True(modules.ContainsModule(p.ModuleId), $"page '{p.PageId}' → module '{p.ModuleId}'"));
    }

    [Fact]
    public void RequiredCapabilities_AreKnownAndOwnedByThePageModule()
    {
        var pages = CanonicalPageCatalog.Instance;
        var modules = CanonicalModuleCatalog.Instance;

        foreach (var page in pages.Pages.Where(p => p.RequiredCapabilityId is not null))
        {
            Assert.True(modules.TryGetModule(page.ModuleId, out var module), page.PageId);
            Assert.Contains(
                module.Capabilities,
                c => c.Id == page.RequiredCapabilityId);
        }
    }

    [Fact]
    public void CapabilityGatedPages_AreExactlyTheCanonicalOnes()
    {
        var pages = CanonicalPageCatalog.Instance;

        Assert.True(pages.TryGetById("jobon.folha", out var jobon));
        Assert.Equal("jobon.view", jobon.RequiredCapabilityId);

        Assert.True(pages.TryGetById("peso.responsavel", out var responsavel));
        Assert.Equal("peso.aprovar", responsavel.RequiredCapabilityId);

        Assert.True(pages.TryGetById("controlo.resumo", out var controlo));
        Assert.Equal("controlo.view", controlo.RequiredCapabilityId);

        Assert.True(pages.TryGetById("admin.gestao", out var admin));
        Assert.Equal("admin.gerir", admin.RequiredCapabilityId);

        // Module-entry pages carry no extra capability requirement.
        Assert.True(pages.TryGetById("peso.operador", out var operador));
        Assert.Null(operador.RequiredCapabilityId);
    }

    [Fact]
    public void ExactlyOneLandingPage_AndItIsJobOn()
    {
        var pages = CanonicalPageCatalog.Instance;

        var landing = pages.LandingPage;
        Assert.NotNull(landing);
        Assert.Equal("jobon.folha", landing!.PageId);
        Assert.Equal("/jobon", landing.Route);
        Assert.Single(pages.Pages, p => p.IsLanding);
    }

    [Fact]
    public void DuplicatePageIds_AndDuplicateRoutes_AreRejected()
    {
        var page = new PageDefinition("a.page", "jobon", "/jobon", null, 1);

        Assert.Throws<ArgumentException>(() => new PageCatalog(new[]
        {
            page,
            new PageDefinition("a.page", "peso", "/peso", null, 2)
        }));

        Assert.Throws<ArgumentException>(() => new PageCatalog(new[]
        {
            page,
            new PageDefinition("b.page", "peso", "/jobon", null, 2)
        }));
    }
}
