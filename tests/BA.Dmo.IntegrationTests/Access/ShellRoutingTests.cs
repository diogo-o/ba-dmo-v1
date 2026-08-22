using System.Net;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Modules.Peso;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// U-07 shell/routing tests (Plan-V3 GLM-ACC-07 scenarios 1–12 at route
/// level, 05_SHL §4–6, GLM-ACC-05, GLM-CTR-02, UD-16): derived tabs, Job On
/// landing for every active identity, Peso Operador/Responsável exclusivity
/// in both directions, Controlo children, safe no-access states, deep-link
/// denial with safe redirect + feedback, and per-request grant re-resolution
/// (GLM-ACC-08). All collaborators are fakes — no live Supabase/DB.
/// </summary>
public class ShellRoutingTests : IClassFixture<ShellRoutingTests.ShellFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("99999999-2222-3333-4444-555555555555");

    private readonly ShellFixture _fixture;

    public ShellRoutingTests(ShellFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task Scenario1_BoquilhasOnly_LandsOnJobOn_AllOtherModulesDenied()
    {
        _fixture.Profile = ShellFixture.UserProfile.BoquilhasOnly;
        var client = await LoginAsync();

        // Landing: "/" resolves the fixed global Job On landing (UD-16).
        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Equal("/jobon", home.Headers.Location!.ToString());

        // Authorized surface.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/jobon")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/boquilhas")).StatusCode);

        // Every other module route is denied server-side.
        foreach (var route in new[]
        {
            "/peso", "/peso/responsavel", "/pegamentos", "/ferramentas", "/armazem",
            "/reparacao-interna", "/reparacao-externa", "/tampoes", "/historia", "/admin"
        })
        {
            var denied = await client.GetAsync(route);
            Assert.True(
                denied.StatusCode == HttpStatusCode.Redirect,
                $"{route} expected denial redirect but was {(int)denied.StatusCode}");
            Assert.StartsWith("/access-denied", denied.Headers.Location!.PathAndQuery);
        }

        // Derived tabs: Job On + Boquilhas only — nothing unauthorized renders.
        var shellHtml = await (await client.GetAsync("/jobon")).Content.ReadAsStringAsync();
        AssertNav(shellHtml, present: new[] { "jobon", "boquilhas" });
        AssertNav(shellHtml, present: null, absent: new[]
        {
            "controlo", "peso", "pegamentos", "ferramentas", "armazem",
            "reparacao_interna", "reparacao_externa", "tampoes", "historia", "admin"
        });
    }

    [Fact]
    public async Task Scenario10_DeepLinkDenied_RedirectsToAuthorizedAreaWithFeedback()
    {
        _fixture.Profile = ShellFixture.UserProfile.BoquilhasOnly;
        var client = await LoginAsync();

        var denied = await client.GetAsync("/ferramentas");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/access-denied", denied.Headers.Location!.PathAndQuery);

        // The shell redirects safely to an authorized area with feedback.
        var accessDenied = await client.GetAsync("/access-denied");
        Assert.Equal(HttpStatusCode.Redirect, accessDenied.StatusCode);
        Assert.Equal("/jobon?acesso-negado=1", accessDenied.Headers.Location!.ToString());

        var landingHtml = await (await client.GetAsync("/jobon?acesso-negado=1"))
            .Content.ReadAsStringAsync();
        Assert.Contains("Não tem acesso a esta área", landingHtml);
    }

    [Fact]
    public async Task Scenario2_PesoOperador_CannotReachResponsavelRoutes()
    {
        _fixture.Profile = ShellFixture.UserProfile.PesoOperador;
        var client = await LoginAsync();

        var operador = await client.GetAsync("/peso");
        Assert.Equal(HttpStatusCode.OK, operador.StatusCode);
        Assert.Contains("page-peso-operador", await operador.Content.ReadAsStringAsync());

        // Operador in /peso/responsavel → safe redirect to /peso (both ways,
        // GLM-ACC-05.2 — no cross-exposure).
        var cross = await client.GetAsync("/peso/responsavel");
        Assert.Equal(HttpStatusCode.Redirect, cross.StatusCode);
        Assert.Equal("/peso", cross.Headers.Location!.ToString());

        // The single Peso entry points at the Operador experience.
        var shellHtml = await operador.Content.ReadAsStringAsync();
        Assert.Contains("nav-item-peso", shellHtml);
        Assert.Contains("href=\"/peso\"", shellHtml);
        Assert.DoesNotContain("/peso/responsavel", shellHtml);
    }

    [Fact]
    public async Task Scenario3_PesoResponsavel_IsRedirectedFromOperadorRoute()
    {
        _fixture.Profile = ShellFixture.UserProfile.PesoResponsavel;
        var client = await LoginAsync();

        var responsavel = await client.GetAsync("/peso/responsavel");
        Assert.Equal(HttpStatusCode.OK, responsavel.StatusCode);
        Assert.Contains("page-peso-responsavel", await responsavel.Content.ReadAsStringAsync());

        // Responsável in /peso → redirect to /peso/responsavel.
        var cross = await client.GetAsync("/peso");
        Assert.Equal(HttpStatusCode.Redirect, cross.StatusCode);
        Assert.Equal("/peso/responsavel", cross.Headers.Location!.ToString());

        // The single Peso entry points at the Responsável experience.
        var shellHtml = await responsavel.Content.ReadAsStringAsync();
        Assert.Contains("href=\"/peso/responsavel\"", shellHtml);
    }

    [Theory]
    [InlineData(ShellFixture.UserProfile.PegamentosOnly, new[] { "pegamentos" })]
    [InlineData(ShellFixture.UserProfile.PesoOperador, new[] { "peso" })]
    [InlineData(ShellFixture.UserProfile.PesoPlusPegamentos, new[] { "peso", "pegamentos" })]
    public async Task Scenarios4To6_ControloShowsOnlyAuthorizedChildren(
        ShellFixture.UserProfile profile, string[] expectedChildren)
    {
        _fixture.Profile = profile;
        var client = await LoginAsync();

        var html = await (await client.GetAsync("/jobon")).Content.ReadAsStringAsync();

        Assert.Contains("nav-item-controlo", html);
        foreach (var child in new[] { "peso", "pegamentos" })
        {
            if (expectedChildren.Contains(child))
                Assert.Contains($"nav-item-{child}", html);
            else
                Assert.DoesNotContain($"nav-item-{child}", html);
        }
    }

    [Fact]
    public async Task Scenario7_AdminOnly_LandsOnAdmin_AndCannotOpenJobOn()
    {
        _fixture.Profile = ShellFixture.UserProfile.AdminOnly;
        var client = await LoginAsync();

        // Owner decision: an Administrator's only working area is the single
        // Admin page. It is NOT granted jobon.view, so the root "/" does not
        // land on the Job On work landing — it resolves to the Admin page.
        var home = await client.GetAsync("/");
        Assert.Equal("/admin", home.Headers.Location!.ToString());
        Assert.NotEqual("/jobon", home.Headers.Location!.ToString());

        var admin = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);

        // Job On is denied to the administrator (no jobon.view).
        var jobon = await client.GetAsync("/jobon");
        Assert.NotEqual(HttpStatusCode.OK, jobon.StatusCode);

        // Navigation shows only the Admin area.
        var adminHtml = await admin.Content.ReadAsStringAsync();
        AssertNav(adminHtml, present: new[] { "admin" });
        AssertNav(adminHtml, present: null, absent: new[] { "jobon", "boquilhas", "controlo" });
    }

    [Fact]
    public async Task JobOnPage_RendersTheU13Surface_InsideTheAuthorizedShell()
    {
        _fixture.Profile = ShellFixture.UserProfile.JobOnResponsible;
        var client = await LoginAsync();

        var response = await client.GetAsync("/jobon");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-view=\"planeamento\"", html);
        Assert.Contains("data-view=\"folha\"", html);
        Assert.Contains("data-view=\"historico\"", html);
        Assert.Contains("data-view=\"definicoes\"", html);
        Assert.Contains("id=\"calendar\"", html);
        Assert.Contains("id=\"jobSheet\"", html);
        Assert.Contains("Editar folha", html);
        Assert.Contains("Guardar nova revisão", html);
        Assert.Contains("Carregar imagem", html);
        Assert.Contains("Folha de ferramentas", html);
        Assert.Contains("Alterar CM associado", html);
        Assert.Contains("Opções dos campos", html);
        Assert.Contains("scripts/jobon.js", html);
    }

    [Fact]
    public async Task JobOnPage_WithoutEditOrConfigure_HidesPrivilegedControls()
    {
        _fixture.Profile = ShellFixture.UserProfile.BoquilhasOnly;
        var client = await LoginAsync();

        var html = await (await client.GetAsync("/jobon")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("jobon-tab-definicoes", html);
        Assert.DoesNotContain("Editar folha", html);
        Assert.DoesNotContain("Ligar diretório da imagem", html);
        Assert.DoesNotContain("Confirmar verificações", html);
    }

    [Fact]
    public async Task Scenario9_NoInternalIdentity_NoAccessSafeState_NoLoop()
    {
        _fixture.Profile = ShellFixture.UserProfile.NoInternalUser;
        var client = await LoginAsync(); // login itself redirects to /no-access

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/no-access")).StatusCode);
        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Equal("/no-access", home.Headers.Location!.ToString());
        // The authenticated-but-unresolved session never reaches module data.
        var module = await client.GetAsync("/jobon");
        Assert.StartsWith("/access-denied", module.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Scenario12_TemplateDeactivated_SessionAuthenticatedWithoutAccess()
    {
        _fixture.Profile = ShellFixture.UserProfile.TemplateInactive;
        var client = await LoginAsync();

        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Equal("/no-access", home.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/no-access")).StatusCode);
        var module = await client.GetAsync("/boquilhas");
        Assert.StartsWith("/access-denied", module.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Scenario11_GrantsRemovedMidSession_ReResolvedPerRequest()
    {
        _fixture.Profile = ShellFixture.UserProfile.BoquilhasOnly;
        var client = await LoginAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/boquilhas")).StatusCode);

        // GLM-ACC-08: grants re-resolve per request; a lost area is denied
        // on the very next request of the same session.
        _fixture.Profile = ShellFixture.UserProfile.PesoOperador;
        var lostArea = await client.GetAsync("/boquilhas");
        Assert.Equal(HttpStatusCode.Redirect, lostArea.StatusCode);
        Assert.StartsWith("/access-denied", lostArea.Headers.Location!.PathAndQuery);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/peso")).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_ModuleRoutes_RedirectToLogin()
    {
        var client = _fixture.CreateTestClient();

        foreach (var route in new[] { "/", "/jobon", "/boquilhas", "/peso", "/admin" })
        {
            var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/login", response.Headers.Location!.PathAndQuery);
        }
    }

    private async Task<HttpClient> LoginAsync()
    {
        var client = _fixture.CreateTestClient();
        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "shell@ba-dmo.example",
            ["password"] = "correct"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    private static void AssertNav(
        string html, string[]? present, string[]? absent = null)
    {
        foreach (var id in present ?? Array.Empty<string>())
            Assert.Contains($"nav-item-{id}", html);
        foreach (var id in absent ?? Array.Empty<string>())
            Assert.DoesNotContain($"nav-item-{id}", html);
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

    /// <summary>
    /// Test host with fakes for the provider adapter and the identity
    /// repository; the identity record is switchable per test to exercise
    /// grant re-resolution (GLM-ACC-08). Anti-forgery disabled for scripted
    /// form posts only.
    /// </summary>
    public sealed class ShellFixture : WebApplicationFactory<Program>
    {
        public enum UserProfile
        {
            BoquilhasOnly,
            JobOnResponsible,
            PesoOperador,
            PesoResponsavel,
            PegamentosOnly,
            PesoPlusPegamentos,
            AdminOnly,
            NoInternalUser,
            TemplateInactive
        }

        public UserProfile Profile { get; set; } = UserProfile.BoquilhasOnly;

        public void Reset() => Profile = UserProfile.BoquilhasOnly;

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
                ReplaceSingleton<IInternalUserRepository>(services, new FakeIdentityRepository(this));
                ReplaceSingleton<IJobOnRepository>(services, new FakeJobOnRepository());
                ReplaceSingleton<IPesoRepository>(services, new FakePesoRepository());
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

            public Task<IReadOnlyList<HistoricalProductionSummary>> GetHistoricalProductionsAsync(string? referenceFilter, string? machineFilter, DateTime? from, DateTime? to, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<HistoricalProductionSummary>>(Array.Empty<HistoricalProductionSummary>());
        }

        private sealed class FakePesoRepository : IPesoRepository
        {
            public Task<Guid> CreateReferenceAsync(Domain.Modules.Peso.PesoReference reference, CancellationToken cancellationToken = default) =>
                Task.FromResult(reference.PesoReferenceId);
            public Task<Domain.Modules.Peso.PesoReference?> GetReferenceByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
                Task.FromResult<Domain.Modules.Peso.PesoReference?>(null);
            public Task<IReadOnlyList<Domain.Modules.Peso.PesoReference>> GetReferencesAsync(string? search, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<Domain.Modules.Peso.PesoReference>>(Array.Empty<Domain.Modules.Peso.PesoReference>());
            public Task<Domain.Modules.Peso.PesoReference?> GetReferenceByMoldNeckringAsync(string mold, string neckring, CancellationToken cancellationToken = default) =>
                Task.FromResult<Domain.Modules.Peso.PesoReference?>(null);
            public Task UpdateReferenceAsync(Domain.Modules.Peso.PesoReference reference, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
            public Task<Guid> CreateLoteAsync(PesoLote lote, CancellationToken cancellationToken = default) =>
                Task.FromResult(lote.PesoLoteId);
            public Task<PesoLote?> GetLoteByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
                Task.FromResult<PesoLote?>(null);
            public Task<IReadOnlyList<PesoLote>> GetLotesAsync(Guid referenceId, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<PesoLote>>(Array.Empty<PesoLote>());
            public Task<Guid> CreateControlAsync(Domain.Modules.Peso.PesoControl control, CancellationToken cancellationToken = default) =>
                Task.FromResult(control.PesoControloId);
            public Task<Domain.Modules.Peso.PesoControl?> GetControlByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
                Task.FromResult<Domain.Modules.Peso.PesoControl?>(null);
            public Task<IReadOnlyList<Domain.Modules.Peso.PesoControl>> GetControlsAsync(
                Guid? referenceId, string? search, string? status, Domain.Modules.Peso.PesoRecordType? type,
                DateTime? from, DateTime? to, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<Domain.Modules.Peso.PesoControl>>(Array.Empty<Domain.Modules.Peso.PesoControl>());
            public Task<IReadOnlyList<Domain.Modules.Peso.PesoControl>> GetApprovedControlsForJobOnAsync(
                Guid jobOnId, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<Domain.Modules.Peso.PesoControl>>(Array.Empty<Domain.Modules.Peso.PesoControl>());
            public Task UpdateControlAsync(Domain.Modules.Peso.PesoControl control, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
            public Task DeleteControlAsync(Guid id, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
            public Task<Domain.Modules.Peso.PesoControloAnterior?> GetPreviousApprovedAsync(
                string mold, string neckring, string productionCode, DateTime countrolDate,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<Domain.Modules.Peso.PesoControloAnterior?>(null);
            public Task SaveDayApprovalAsync(
                string mold, string neckring, string line, DateTime approvalDate,
                string approvedBy, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
            public Task<IReadOnlyList<string>> GetRecordDatesAsync(int year, int month, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            public Task SaveSettingAsync(string key, string json, string updatedBy, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
            public Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default) =>
                Task.FromResult<string?>(null);
            public Task InsertAuditEventAsync(
                Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot,
                string actorId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private sealed class FakeIdentityRepository(ShellFixture fixture) : IInternalUserRepository
        {
            public Task<InternalUserRecord?> FindByAuthUserIdAsync(
                Guid authUserId, CancellationToken cancellationToken = default)
            {
                if (authUserId != AuthUserId || fixture.Profile == UserProfile.NoInternalUser)
                    return Task.FromResult<InternalUserRecord?>(null);

                var modulesJson = fixture.Profile switch
                {
                    UserProfile.BoquilhasOnly =>
                        "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]",
                    UserProfile.JobOnResponsible =>
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[\"jobon.view\",\"jobon.edit\",\"jobon.configure\",\"jobon.confirmar\"]}]",
                    UserProfile.PesoOperador =>
                        "[{\"moduleId\":\"peso\",\"capabilities\":[]}]",
                    UserProfile.PesoResponsavel =>
                        "[{\"moduleId\":\"peso\",\"capabilities\":[\"peso.aprovar\"]}]",
                    UserProfile.PegamentosOnly =>
                        "[{\"moduleId\":\"pegamentos\",\"capabilities\":[]}]",
                    UserProfile.PesoPlusPegamentos =>
                        "[{\"moduleId\":\"peso\",\"capabilities\":[]},{\"moduleId\":\"pegamentos\",\"capabilities\":[]}]",
                    UserProfile.AdminOnly =>
                        "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\",\"audit.view\",\"audit.export\"]}]",
                    UserProfile.TemplateInactive =>
                        "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]",
                    _ => "[]"
                };

                return Task.FromResult<InternalUserRecord?>(new InternalUserRecord(
                    "shell-actor", AuthUserId, "Utilizador Shell", "Título Visual",
                    UserActive: true, TemplateId: "tpl-shell", TemplateName: "Shell",
                    TemplateActive: fixture.Profile != UserProfile.TemplateInactive,
                    ModulesJson: modulesJson));
            }

            public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public Task CreateBootstrapAdminAsync(
                BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }
    }
}
