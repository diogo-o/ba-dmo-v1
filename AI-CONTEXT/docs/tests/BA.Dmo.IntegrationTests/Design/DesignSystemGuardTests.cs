using System.Net;
using System.Text.RegularExpressions;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.Design;

/// <summary>
/// U-08 design-foundation guards (Plan-V3 U-08 tests: contract §21 checklist
/// automated subset + GLM-DSN-03 architecture): required token groups exist,
/// the canonical load order is wired, exactly ONE dmo-design-system file set
/// exists (no competing legacy CSS), the shared component layer consumes
/// tokens only (no raw hex/brightness), no page carries local design CSS,
/// and the laboratory page renders the component catalog behind the session
/// gate. All collaborators are fakes — no live Supabase/DB.
/// </summary>
public class DesignSystemGuardTests : IClassFixture<DesignSystemGuardTests.DesignFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("dddddddd-2222-3333-4444-555555555555");

    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] DesignSystemFiles =
    {
        "dmo-tokens.css",
        "dmo-foundation.css",
        "dmo-components.css",
        "dmo-layout.css",
        "dmo-utilities.css"
    };

    /// <summary>
    /// Module-composition layouts (GLM-DSN-03): only grid/order/widths,
    /// no color/radius/shadow redefinition. One per module.
    /// </summary>
    private static readonly string[] AllowedModuleLayouts =
    {
        "admin-layout.css",
        "armazem-layout.css",
        "controlo-layout.css",
        "ferramentas-layout.css",
        "jobon-layout.css",
        "pegamentos-layout.css",
        "peso-layout.css",
        "reparacao-externa-layout.css",
        "reparacao-interna-layout.css",
        "tampoes-layout.css"
    };

    private readonly DesignFixture _fixture;

    public DesignSystemGuardTests(DesignFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public void TokenFile_DefinesAllRequiredTokenGroups()
    {
        // Contract §21 Foundation: all visual values come from approved tokens.
        var tokens = ReadStyles("dmo-tokens.css");

        string[] required =
        {
            // Brand scale + surfaces + text (DMO v2.7 §4)
            "--dmo-brand-950", "--dmo-brand-600", "--dmo-brand-050",
            "--dmo-surface-page", "--dmo-surface-card", "--dmo-surface-subtle",
            "--dmo-text", "--dmo-text-muted", "--dmo-text-on-color",
            // Semantic states including info alias (contract §2.1)
            "--dmo-success", "--dmo-success-soft",
            "--dmo-warning", "--dmo-warning-soft",
            "--dmo-danger", "--dmo-danger-soft",
            "--dmo-pending", "--dmo-pending-soft",
            "--dmo-info", "--dmo-info-soft", "--dmo-disabled",
            // Spacing, radius, shadows
            "--dmo-space-1", "--dmo-space-8",
            "--dmo-radius-control", "--dmo-radius-card",
            "--dmo-radius-modal", "--dmo-radius-pill",
            "--dmo-shadow-card", "--dmo-shadow-menu", "--dmo-shadow-modal",
            // Sizing incl. GLM-DSN-01 P0-4 button API
            "--dmo-control-height", "--dmo-control-height-compact",
            "--dmo-button-height", "--dmo-button-height-compact",
            "--dmo-button-height-form", "--dmo-pagination-size",
            "--dmo-touch-target", "--dmo-row-height",
            "--dmo-header-height", "--dmo-tabs-height", "--dmo-sidebar-width",
            // Borders + focus (GLM-DSN-01 P0-6; reference field halo)
            "--dmo-border-width", "--dmo-border-width-strong", "--dmo-focus-halo",
            // Typography — exact scale, no ambiguous intervals (P0-1)
            "--dmo-font-family", "--dmo-font-size-xs", "--dmo-font-size-sm",
            "--dmo-font-size-md", "--dmo-font-size-lg", "--dmo-font-size-xl",
            "--dmo-line-height-tight", "--dmo-line-height-normal",
            "--dmo-line-height-relaxed", "--dmo-letter-spacing-caps",
            // Layers (P0-2) and page width/gutters (P0-3)
            "--dmo-z-base", "--dmo-z-sticky", "--dmo-z-dropdown",
            "--dmo-z-overlay", "--dmo-z-modal", "--dmo-z-toast",
            "--dmo-page-max-width", "--dmo-page-gutter-desktop",
            "--dmo-page-gutter-tablet", "--dmo-page-gutter-mobile",
            // Icons + motion (contract §2.2)
            "--dmo-icon-sm", "--dmo-icon-md", "--dmo-icon-lg",
            "--dmo-duration-fast", "--dmo-ease-standard", "--dmo-transition-fast"
        };

        foreach (var token in required)
            Assert.True(tokens.Contains(token, StringComparison.Ordinal),
                $"Required design token missing: {token}");
    }

    [Fact]
    public void ReducedMotion_IsImplemented()
    {
        // GLM-DSN-01 P0-6 / DMO §23.
        var foundation = ReadStyles("dmo-foundation.css");
        Assert.Contains("prefers-reduced-motion: reduce", foundation);
    }

    [Fact]
    public void SemanticTokens_MatchTheDesignReferenceExactly()
    {
        // Owner instruction (U-08 correction pass): the Design-Reference is
        // followed 100% — no substituted/adjusted colors. Locks the exact
        // reference dmo-design-system.css values (incl. the pill.approved
        // text #3f7765, distinct from --dmo-success in the reference).
        var tokens = ParseTokens(ReadStyles("dmo-tokens.css"));

        Assert.Equal("#527c72", tokens["--dmo-success"]);
        Assert.Equal("#e5f0eb", tokens["--dmo-success-soft"]);
        Assert.Equal("#a97943", tokens["--dmo-warning"]);
        Assert.Equal("#f7f0e7", tokens["--dmo-warning-soft"]);
        Assert.Equal("#9a625d", tokens["--dmo-danger"]);
        Assert.Equal("#f3e9e7", tokens["--dmo-danger-soft"]);
        Assert.Equal("#315d88", tokens["--dmo-pending"]);
        Assert.Equal("#e7eef5", tokens["--dmo-pending-soft"]);
        Assert.Equal("#3f7765", tokens["--dmo-pill-approved-text"]);
        Assert.Equal("#edf2f6", tokens["--dmo-pill-record-type-bg"]);
        Assert.Equal("#536b80", tokens["--dmo-pill-record-type-text"]);

        // Pill variants consume the reference pairs.
        var components = ReadStyles("dmo-components.css");
        Assert.Contains("color: var(--dmo-pill-approved-text)", components);
        Assert.Contains("color: var(--dmo-pill-record-type-text)", components);
    }

    [Fact]
    public void Layout_WiresTheCanonicalLoadOrder_ExactlyOnce()
    {
        // Contract §3.2: single load order tokens → foundation → components
        // → layout → utilities; one design-system source, no competitors.
        var layout = File.ReadAllText(
            Path.Combine(RepoRoot, "src", "BA.Dmo.Web", "Pages", "Shared", "_Layout.cshtml"));

        var previous = -1;
        foreach (var file in DesignSystemFiles)
        {
            var index = layout.IndexOf($"styles/{file}", StringComparison.Ordinal);
            Assert.True(index > previous,
                $"Stylesheet {file} missing or out of canonical load order.");
            previous = index;
        }
    }

    [Fact]
    public void SingleDesignSystem_NoCompetingLegacyCss()
    {
        // Contract §21 CSS architecture: no site.css legacy, no second system.
        var stylesDir = Path.Combine(RepoRoot, "src", "BA.Dmo.Web", "wwwroot", "styles");
        var cssFiles = Directory.GetFiles(stylesDir, "*.css", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var allowed = DesignSystemFiles.Concat(AllowedModuleLayouts).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(allowed, cssFiles);

        var webRoot = Path.Combine(RepoRoot, "src", "BA.Dmo.Web");
        Assert.DoesNotContain(
            Directory.GetFiles(webRoot, "*.css", SearchOption.AllDirectories),
            f => Path.GetFileName(f).Equals("site.css", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SharedComponentLayer_ConsumesTokensOnly()
    {
        // Contract §3.2: no hardcoded hex/rgb or brightness outside tokens.
        var hex = new Regex(@"#[0-9a-fA-F]{3,8}\b");
        foreach (var file in DesignSystemFiles.Skip(1)) // tokens define values
        {
            var css = ReadStyles(file);
            Assert.False(hex.IsMatch(css),
                $"{file} contains a raw hex color; visual values must come from tokens.");
            Assert.DoesNotContain("brightness(", css);
            Assert.DoesNotContain("rgb(", css.Replace("var(", string.Empty));
        }
    }

    [Fact]
    public void ButtonStateMachine_FilledRestInvertedHover()
    {
        // Reference .dmo-button: filled rest via --button-color; hover/focus
        // inverted to white with the variant color (no brightness).
        var components = ReadStyles("dmo-components.css");
        var baseRule = ExtractRule(components, ".dmo-button {");
        Assert.Contains("var(--button-color, var(--dmo-brand-600))", baseRule);
        var hoverBlock = ExtractRule(components, ".dmo-button:hover");
        Assert.Contains("var(--dmo-card)", hoverBlock);
        Assert.Contains("var(--button-color", hoverBlock);
        Assert.DoesNotContain("brightness(", components);
    }

    [Fact]
    public void Buttons_UseCanonicalTypographyAndCenteredLabels()
    {
        var components = ReadStyles("dmo-components.css");
        var baseRule = ExtractRule(components, ".dmo-button {");

        Assert.Contains("align-items: center", baseRule);
        Assert.Contains("justify-content: center", baseRule);
        Assert.Contains("font-family: var(--dmo-font-family)", baseRule);
        Assert.Contains("font-size: var(--dmo-font-size-md)", baseRule);
        Assert.Contains("line-height: var(--dmo-line-height-normal)", baseRule);
        Assert.Contains("text-align: center", baseRule);
    }

    [Fact]
    public void Boquilhas_UsesCanonicalContextualSidebar()
    {
        var markup = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "BA.Dmo.Web", "Pages", "Boquilhas", "Index.cshtml"));
        var behavior = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "BA.Dmo.Web", "wwwroot", "scripts", "boquilhas.js"));

        Assert.Contains("dmo-work-split boquilhas-layout", markup);
        Assert.Contains("dmo-sidebar boquilhas-side", markup);
        Assert.Contains("dmo-sidebar__head", markup);
        Assert.Contains("dmo-sidebar__cards boquilhas-lines", markup);
        Assert.Contains("dmo-sidebar__card boquilhas-line", behavior);
    }

    [Fact]
    public void ReparacaoInterna_TypeChoice_PersistsAccessibleSelectedState()
    {
        var markup = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "BA.Dmo.Web", "Pages", "ReparacaoInterna", "Index.cshtml"));
        var behavior = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "BA.Dmo.Web", "wwwroot", "scripts", "reparacao-interna.js"));
        var css = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "BA.Dmo.Web", "wwwroot", "styles", "modules",
            "reparacao-interna-layout.css"));

        Assert.Contains("aria-label=\"Tipo de ferramenta\"", markup);
        Assert.Contains("class=\"reparacao-interna-type-choice\"", markup);
        Assert.Equal(2, Regex.Matches(markup, "data-type=\\\"(?:CM|MF)\\\" aria-pressed=\\\"false\\\"").Count);
        Assert.Contains("setAttribute('aria-pressed', String(selected))", behavior);
        Assert.Contains("setAttribute('aria-pressed', 'false')", behavior);
        Assert.DoesNotContain("let openCorrection", behavior);
        Assert.Single(Regex.Matches(behavior, "function openCorrection\\(").Cast<Match>());
        Assert.Contains("if (correctionTrigger)", behavior);
        Assert.Contains("[data-type][aria-pressed=\"true\"]", css);
        Assert.Contains("var(--dmo-brand-800)", css);
        Assert.Contains("background: var(--dmo-card)", css);
    }

    [Fact]
    public void Logout_UsesTheCanonicalButtonAndStylesheets()
    {
        var markup = File.ReadAllText(Path.Combine(
            RepoRoot, "src", "BA.Dmo.Web", "Pages", "Auth", "Logout.cshtml"));

        Assert.Contains("styles/dmo-components.css", markup);
        Assert.Contains("class=\"logout-body\"", markup);
        Assert.Contains("class=\"dmo-button\" type=\"submit\"", markup);
    }

    [Fact]
    public void ModuleTabs_UseOneSharedTypographyAndSizingContract()
    {
        var components = ReadStyles("dmo-components.css");
        Assert.Contains(".dmo-module-tabs.dmo-module-tabs {", components);
        Assert.Contains("min-height: var(--dmo-tabs-height)", components);
        Assert.Contains(".dmo-module-tab {", components);
        Assert.Contains("font-family: var(--dmo-font-family)", components);
        Assert.Contains("font-size: var(--dmo-font-size-md)", components);
        Assert.Contains("font-weight: 650", components);
        Assert.Contains("text-align: center", components);

        string[] pages =
        {
            "Armazem/Index.cshtml",
            "Boquilhas/Index.cshtml",
            "Controlo/Index.cshtml",
            "Ferramentas/Index.cshtml",
            "JobOn/Index.cshtml",
            "Pegamentos/Index.cshtml",
            "Peso/Index.cshtml",
            "Peso/Responsavel.cshtml",
            "ReparacaoExterna/Index.cshtml",
            "ReparacaoInterna/Index.cshtml",
            "Tampoes/Index.cshtml"
        };

        foreach (var page in pages)
        {
            var markup = File.ReadAllText(Path.Combine(
                RepoRoot, "src", "BA.Dmo.Web", "Pages",
                page.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains("dmo-module-tabs", markup);
            Assert.Contains("dmo-module-tab", markup);
        }
    }

    [Fact]
    public void StylesheetLinks_AreFingerprintVersioned()
    {
        foreach (var cshtml in Directory.EnumerateFiles(
            Path.Combine(RepoRoot, "src", "BA.Dmo.Web", "Pages"),
            "*.cshtml", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(cshtml);
            foreach (Match link in Regex.Matches(
                markup, "<link\\s+rel=\\\"stylesheet\\\"\\s+href=\\\"~/styles/[^\\\"]+\\\"[^>]*>"))
            {
                Assert.Contains("asp-append-version=\"true\"", link.Value);
            }
        }
    }

    [Fact]
    public void Pages_ContainNoLocalDesignCss()
    {
        // GLM-DSN-09 / contract §21: no <style> and no design inline styles.
        foreach (var cshtml in Directory.EnumerateFiles(
            Path.Combine(RepoRoot, "src", "BA.Dmo.Web", "Pages"),
            "*.cshtml", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(cshtml);
            Assert.False(markup.Contains("<style", StringComparison.OrdinalIgnoreCase),
                $"{cshtml} contains a local <style> block.");
            Assert.False(markup.Contains("style=\"", StringComparison.OrdinalIgnoreCase),
                $"{cshtml} contains an inline design style.");
        }
    }

    [Fact]
    public async Task LaboratoryPage_RequiresASession_AndRendersTheCatalog()
    {
        var anonymous = _fixture.CreateTestClient();
        var denied = await anonymous.GetAsync("/design-laboratorio");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/login", denied.Headers.Location!.PathAndQuery);

        _fixture.Repository.User = _fixture.ValidUser();
        var client = _fixture.CreateTestClient();
        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "design@ba-dmo.example",
            ["password"] = "correct"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var lab = await client.GetAsync("/design-laboratorio");
        Assert.Equal(HttpStatusCode.OK, lab.StatusCode);
        var html = await lab.Content.ReadAsStringAsync();

        // The catalog presents the component families and states.
        foreach (var marker in new[]
        {
            "dmo-button", "dmo-field", "dmo-card", "dmo-pill", "dmo-table",
            "dmo-pagination", "dmo-menu", "dmo-alert", "dmo-toast",
            "dmo-skeleton", "dmo-empty-state", "dmo-error-state", "dmo-modal",
            "dmo-tooltip", "dmo-calendar__week", "dmo-sidebar",
            "dmo-history-entry__compare", "dmo-path-readonly", "dmo-segmented",
            "data-dmo-list", "data-dmo-row"
        })
        {
            Assert.Contains(marker, html);
        }

        // Shell keeps serving the design system on module pages.
        var shellHtml = await (await client.GetAsync("/jobon")).Content.ReadAsStringAsync();
        Assert.Contains("styles/dmo-tokens.css", shellHtml);
        Assert.Contains("styles/dmo-components.css", shellHtml);
    }

    private static string ReadStyles(string fileName) =>
        File.ReadAllText(Path.Combine(
            RepoRoot, "src", "BA.Dmo.Web", "wwwroot", "styles", fileName));

    /// <summary>Parses `--name: #hex;` declarations from the token file.</summary>
    private static Dictionary<string, string> ParseTokens(string css)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(css, @"(--[a-z0-9-]+):\s*(#[0-9a-fA-F]{6});"))
            tokens[match.Groups[1].Value] = match.Groups[2].Value;
        return tokens;
    }

    private static string ExtractRule(string css, string selectorStart)
    {
        var start = css.IndexOf(selectorStart, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Rule '{selectorStart}' not found.");
        var open = css.IndexOf('{', start);
        var close = css.IndexOf('}', open);
        return css[(open + 1)..close];
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BA-DMO.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Repository root (BA-DMO.sln) not found.");
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string url, Dictionary<string, string> fields)
    {
        var form = await client.GetAsync(url);
        var html = await form.Content.ReadAsStringAsync();

        var values = new Dictionary<string, string>(fields);
        var tokenStart = html.IndexOf("name=\"__RequestVerificationToken\"", StringComparison.Ordinal);
        if (tokenStart >= 0)
        {
            var valueAttr = html.IndexOf("value=\"", tokenStart, StringComparison.Ordinal);
            if (valueAttr >= 0)
            {
                var tokenValueStart = valueAttr + "value=\"".Length;
                var tokenEnd = html.IndexOf('"', tokenValueStart);
                values["__RequestVerificationToken"] = html[tokenValueStart..tokenEnd];
            }
        }

        return await client.PostAsync(url, new FormUrlEncodedContent(values));
    }

    /// <summary>Test host with fakes — no live Supabase/DB (GLM-ARCH-18).</summary>
    public sealed class DesignFixture : WebApplicationFactory<Program>
    {
        public FakeIdentityRepository Repository { get; } = new();

        public void Reset() => Repository.User = null;

        public InternalUserRecord ValidUser() => new(
            ActorId: "design-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Utilizador Design",
            ProfileTitle: "Operador / Controlador",
            UserActive: true,
            TemplateId: "tpl-design",
            TemplateName: "Design",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]");

        public HttpClient CreateTestClient() => CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                ReplaceSingleton<ISupabaseAuthAdapter>(services, new FakeAuthAdapter());
                ReplaceSingleton<IInternalUserRepository>(services, Repository);
                ReplaceSingleton<IJobOnRepository>(services, new FakeJobOnRepository());
                services.Configure<Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions>(
                    options => options.Conventions.ConfigureFilter(
                        new IgnoreAntiforgeryTokenAttribute()));
            });
        }

        private static void ReplaceSingleton<TService>(
            IServiceCollection services, TService implementation)
            where TService : class
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(TService)).ToList())
                services.Remove(descriptor);
            services.AddSingleton(implementation);
        }

        private sealed class FakeAuthAdapter : ISupabaseAuthAdapter
        {
            public Task<Result<AuthUser, DomainError>> SignInWithPasswordAsync(
                string email, string password, CancellationToken cancellationToken = default) =>
                Task.FromResult(Result<AuthUser, DomainError>.Success(
                    new AuthUser(AuthUserId, email)));
        }
    }

    private sealed class FakeJobOnRepository : IJobOnRepository
    {
        public Task<Guid> CreateAsync(Domain.Modules.JobOn.JobOn jobOn, CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.NewGuid());

        public Task<Domain.Modules.JobOn.JobOn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Domain.Modules.JobOn.JobOn?>(null);

        public Task<IReadOnlyList<Domain.Modules.JobOn.JobOn>> GetActiveAsync(string machineCode, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Domain.Modules.JobOn.JobOn>>(Array.Empty<Domain.Modules.JobOn.JobOn>());

        public Task<Domain.Modules.JobOn.JobOn?> GetByProductionCodeAsync(string productionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Domain.Modules.JobOn.JobOn?>(null);

        public Task UpdateLifecycleStateAsync(Guid id, JobOnLifecycleState newState, string actorId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InsertRevisionAsync(JobOnRevision revision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<JobOnRevision>> GetRevisionsAsync(Guid jobOnId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JobOnRevision>>(Array.Empty<JobOnRevision>());

        public Task InsertComponentsAsync(IEnumerable<JobOnComponent> components, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InsertFieldsAsync(IEnumerable<JobOnComponentField> fields, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InsertRowsAsync(IEnumerable<JobOnComponentRow> rows, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InsertVerificationsAsync(IEnumerable<JobOnVerificationOccurrence> verifications, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateVerificationStatusAsync(Guid occurrenceId, string status, string? completedBy, DateTime? completedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Guid?> GetCurrentRevisionIdAsync(Guid jobOnId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);

        public Task UpdateCurrentRevisionAsync(Guid jobOnId, Guid revisionId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InsertAuditEventAsync(Guid jobId, Guid? revisionId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InsertImageMutationAsync(JobOnRevision newRevision, Guid jobOnId, string eventType, string? beforeImageAssetId, string? afterImageAssetId, string actorId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveRevisionGraphAsync(JobOnRevision revision, string eventType, string actorId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Guid> DuplicateAtomicallyAsync(Domain.Modules.JobOn.JobOn newJobOn, JobOnRevision revision, Guid sourceJobOnId, string actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.NewGuid());

        public Task<IReadOnlyList<HistoricalProductionSummary>> GetHistoricalProductionsAsync(string? referenceFilter, string? machineFilter, DateTime? from, DateTime? to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<HistoricalProductionSummary>>(Array.Empty<HistoricalProductionSummary>());
    }

    public sealed class FakeIdentityRepository : IInternalUserRepository
    {
        public InternalUserRecord? User { get; set; }

        public Task<InternalUserRecord?> FindByAuthUserIdAsync(
            Guid authUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(User);

        public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task CreateBootstrapAdminAsync(
            BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
