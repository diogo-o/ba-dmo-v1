using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.IntegrationTests.Design;

/// <summary>
/// U-09 guards (Plan-V3 U-09 + GLM-DSN-05/06): ONE canonical calendar
/// implementation (CSS + behavior) consumed by the test page, shell visual
/// composition wired from the U-08 design system, and canonical responsive
/// breakpoints. No second calendar, no page-local calendar styling.
/// </summary>
public class ShellAndCalendarGuardTests : IClassFixture<ShellAndCalendarGuardTests.LabFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("eeeeeeee-2222-3333-4444-555555555555");

    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string WebRoot =
        Path.Combine(RepoRoot, "src", "BA.Dmo.Web");

    private readonly LabFixture _fixture;

    public ShellAndCalendarGuardTests(LabFixture fixture) => _fixture = fixture;

    [Fact]
    public void SingleCanonicalCalendar_NoCompetingImplementations()
    {
        // CSS: calendar visuals live only in the shared component layer.
        var cssFiles = Directory.GetFiles(
            Path.Combine(WebRoot, "wwwroot", "styles"), "*.css");
        foreach (var file in cssFiles)
        {
            var name = Path.GetFileName(file);
            var css = File.ReadAllText(file);
            if (name == "dmo-components.css")
            {
                Assert.Contains(".dmo-calendar__day", css);
                continue;
            }

            Assert.False(css.Contains(".dmo-calendar", StringComparison.Ordinal),
                $"{name} redefines calendar visuals; the calendar is implemented once.");
        }

        // Behavior: exactly one calendar script; it carries the reference
        // contract markers (Monday-first grid, no auto-select on month
        // change, ISO data-date, aria-pressed, keyboard roving).
        var scripts = Directory.GetFiles(Path.Combine(WebRoot, "wwwroot", "scripts"), "*.js");
        var calendarScripts = scripts
            .Where(s => File.ReadAllText(s).Contains("data-calendar-grid", StringComparison.Ordinal))
            .ToList();
        var calendarScript = Assert.Single(calendarScripts);
        Assert.Equal("dmo-calendar.js", Path.GetFileName(calendarScript));

        var behavior = File.ReadAllText(calendarScript);
        Assert.Contains("(firstWeekday + 6) % 7", behavior); // Monday-first
        Assert.Contains("aria-pressed", behavior);
        Assert.Contains("ArrowLeft", behavior);
        Assert.Contains("dmo:date-select", File.ReadAllText(
            Path.Combine(WebRoot, "wwwroot", "scripts", "dmo-interactions.js")));

        // No page re-implements calendar behavior locally.
        foreach (var cshtml in Directory.EnumerateFiles(
            Path.Combine(WebRoot, "Pages"), "*.cshtml", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(cshtml);
            Assert.False(
                Regex.IsMatch(markup, @"<script[^>]*>\s*[^<]*calendar\.insertAdjacentHTML"),
                $"{cshtml} re-implements calendar rendering locally.");
        }
    }

    [Fact]
    public void ShellComposition_UsesTheDesignSystem()
    {
        var layout = File.ReadAllText(
            Path.Combine(WebRoot, "Pages", "Shared", "_Layout.cshtml"));
        Assert.Contains("dmo-interactions.js", layout);
        Assert.Contains("dmo-calendar.js", layout);

        var header = File.ReadAllText(
            Path.Combine(WebRoot, "Pages", "Shared", "_Header.cshtml"));
        Assert.Contains("dmo-app-header", header);          // reference anatomy
        Assert.Contains("dmo-app-header__logo", header);
        Assert.Contains("data-user-profile-name", header);   // DMO §26
        Assert.Contains("data-user-profile-title", header);
        Assert.Contains("<partial name=\"_Navigation\"", header);

        var nav = File.ReadAllText(
            Path.Combine(WebRoot, "Pages", "Shared", "_Navigation.cshtml"));
        Assert.Contains("app-nav-left", nav);
        Assert.Contains("app-nav-right", nav); // Administração right-aligned
        Assert.Contains("nav-active", nav);    // active indication
        Assert.Contains("primary-nav", nav);   // authority header composition
        Assert.DoesNotContain("area.Children", nav); // Peso/Pegamentos are internal

        // Canonical responsive breakpoints + reference sidebar behavior.
        var layoutCss = File.ReadAllText(
            Path.Combine(WebRoot, "wwwroot", "styles", "dmo-layout.css"));
        foreach (var breakpoint in new[]
        {
            "max-width: 1200px", "max-width: 980px", "max-width: 720px"
        })
        {
            Assert.Contains(breakpoint, layoutCss);
        }
        Assert.Contains("var(--dmo-sidebar-width-compact)", layoutCss);
        Assert.Contains("var(--dmo-sidebar-gradient)", layoutCss);
        Assert.Contains(".dmo-app-header__page", layoutCss);
        Assert.Contains("flex: 0 0 auto", layoutCss);
        Assert.Contains("white-space: nowrap", layoutCss);
    }

    [Fact]
    public async Task LaboratoryPage_ConsumesTheCanonicalCalendar()
    {
        _fixture.Repository.User = _fixture.ValidUser();
        var client = _fixture.CreateTestClient();
        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "shell@ba-dmo.example",
            ["password"] = "correct"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var lab = await client.GetAsync("/design-laboratorio");
        Assert.Equal(HttpStatusCode.OK, lab.StatusCode);
        var html = await lab.Content.ReadAsStringAsync();

        // Live canonical calendar markup contract.
        Assert.Contains("data-dmo-calendar", html);
        Assert.Contains("data-calendar-grid", html);
        Assert.Contains("data-calendar-prev", html);
        Assert.Contains("data-calendar-next", html);
        Assert.Contains("data-calendar-clear", html);
        Assert.Contains("Mostrar todas as datas", html);
        Assert.Contains("dmo-calendar__week", html);
        // The page consumes the canonical event, nothing else.
        Assert.Contains("dmo:date-select", html);
        // Both canonical scripts reach the page.
        Assert.Contains("scripts/dmo-interactions.js", html);
        Assert.Contains("scripts/dmo-calendar.js", html);
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

    /// <summary>Test host with fakes — no live Supabase/DB.</summary>
    public sealed class LabFixture : WebApplicationFactory<Program>
    {
        public FakeIdentityRepository Repository { get; } = new();

        public InternalUserRecord ValidUser() => new(
            ActorId: "shell-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Utilizador Shell",
            ProfileTitle: "Operador / Controlador",
            UserActive: true,
            TemplateId: "tpl-shell",
            TemplateName: "Shell",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"jobon\",\"capabilities\":[]}]");

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
