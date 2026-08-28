using System.Net;
using System.Net.Http.Json;
using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Modules.Peso;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Modules.Armazem;
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
    public async Task Scenario1_JobOnAndBoquilhas_LandsOnJobOn_WithDerivedHistoria()
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
            "/reparacao-interna", "/reparacao-externa", "/tampoes", "/admin"
        })
        {
            var denied = await client.GetAsync(route);
            Assert.True(
                denied.StatusCode == HttpStatusCode.Redirect,
                $"{route} expected denial redirect but was {(int)denied.StatusCode}");
            Assert.StartsWith("/access-denied", denied.Headers.Location!.PathAndQuery);
        }

        // Assigned modules plus derived História; nothing else renders.
        var shellHtml = await (await client.GetAsync("/jobon")).Content.ReadAsStringAsync();
        AssertNav(shellHtml, present: new[] { "jobon", "boquilhas", "historia" });
        AssertNav(shellHtml, present: null, absent: new[]
        {
            "controlo", "peso", "pegamentos", "ferramentas", "armazem",
            "reparacao_interna", "reparacao_externa", "tampoes", "admin"
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

        // The global shell exposes only the single Controlo parent entry.
        var shellHtml = await operador.Content.ReadAsStringAsync();
        Assert.Contains("nav-item-controlo", shellHtml);
        Assert.Contains("href=\"/controlo\"", shellHtml);
        Assert.DoesNotContain("nav-item-peso", shellHtml);
        Assert.DoesNotContain("/peso/responsavel", shellHtml);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/controlo")).StatusCode);
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

        // Profile changes behavior inside Controlo, never the global route.
        var shellHtml = await responsavel.Content.ReadAsStringAsync();
        Assert.Contains("href=\"/controlo\"", shellHtml);
        Assert.DoesNotContain("nav-item-peso", shellHtml);
    }

    [Theory]
    [InlineData(ShellFixture.UserProfile.PegamentosOnly)]
    [InlineData(ShellFixture.UserProfile.PesoOperador)]
    [InlineData(ShellFixture.UserProfile.PesoPlusPegamentos)]
    public async Task Scenarios4To6_ControloGrantShowsOneGlobalEntry(
        ShellFixture.UserProfile profile)
    {
        _fixture.Profile = profile;
        var client = await LoginAsync();

        var html = await (await client.GetAsync("/jobon")).Content.ReadAsStringAsync();

        Assert.Contains("nav-item-controlo", html);
        Assert.Contains("href=\"/controlo\"", html);
        Assert.DoesNotContain("nav-item-peso", html);
        Assert.DoesNotContain("nav-item-pegamentos", html);
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
    public async Task JobOnResponsible_OpenedProduction_RendersAuthorizedIdentityPreservingReadLinks()
    {
        _fixture.Profile = ShellFixture.UserProfile.JobOnResponsible;
        var client = await LoginAsync();

        var response = await client.GetAsync($"/jobon?id={ShellFixture.CrossModuleJobOnId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Ver Controlo", html);
        Assert.Contains("Ver Peso", html);
        Assert.Contains("Ver Pegamentos", html);
        Assert.Contains("Ver Resumo", html);
        Assert.Contains("Ver reparações", html);
        Assert.Contains($"jobOn={ShellFixture.CrossModuleJobOnId}", html);
        Assert.Contains($"revision={ShellFixture.CrossModuleRevisionId}", html);
        Assert.Contains("section=peso", html);
        Assert.Contains("section=pegamentos", html);
        Assert.Contains("section=resumo", html);
        Assert.Contains($"jobOnId={ShellFixture.CrossModuleJobOnId}", html);
        Assert.Contains("line=B1", html);
    }

    [Fact]
    public async Task JobOnUser_WithoutTargetModules_DoesNotReceiveCrossModuleReadLinks()
    {
        _fixture.Profile = ShellFixture.UserProfile.BoquilhasOnly;
        var client = await LoginAsync();

        var html = await (await client.GetAsync($"/jobon?id={ShellFixture.CrossModuleJobOnId}"))
            .Content.ReadAsStringAsync();

        Assert.DoesNotContain("Ver Controlo", html);
        Assert.DoesNotContain("Ver Peso", html);
        Assert.DoesNotContain("Ver Pegamentos", html);
        Assert.DoesNotContain("Ver Resumo", html);
        Assert.DoesNotContain("Ver reparações", html);
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
    public async Task Armazem_Substituir_IsAbsentFromRenderedSurface_AndHasNoEndpoint()
    {
        _fixture.Profile = ShellFixture.UserProfile.ArmazemOnly;
        var client = await LoginAsync();

        var page = await client.GetAsync("/armazem");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.DoesNotContain("data-open=\"substituir\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"substituirForm\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Escolha Entrada ou Saída para começar.", html);

        var obsoleteApi = await client.PostAsJsonAsync(
            "/api/armazem/substituir",
            new { positionCode = "2421", newToolType = "CM", newReference = "CM-150", newLot = "2" });
        Assert.Equal(HttpStatusCode.NotFound, obsoleteApi.StatusCode);
    }

    [Fact]
    public async Task Armazem_CreateNew_IsVisibleOnlyWithFerramentasMasterAccess()
    {
        _fixture.Profile = ShellFixture.UserProfile.ArmazemOnly;
        var warehouseOnly = await LoginAsync();
        var withoutMasterAccess = await warehouseOnly.GetStringAsync("/armazem");
        Assert.DoesNotContain("data-open=\"novo\"", withoutMasterAccess, StringComparison.Ordinal);

        _fixture.Profile = ShellFixture.UserProfile.ArmazemWithFerramentas;
        var withMaster = await LoginAsync();
        var withMasterAccess = await withMaster.GetStringAsync("/armazem");
        Assert.Contains("data-open=\"novo\"", withMasterAccess, StringComparison.Ordinal);
        Assert.Contains("id=\"novoForm\"", withMasterAccess, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Armazem_ConsultaWithoutFilters_ReturnsSeededCmMfBqRows()
    {
        _fixture.Profile = ShellFixture.UserProfile.ArmazemOnly;
        var client = await LoginAsync();

        var response = await client.GetAsync("/api/armazem/consulta");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<ArmazemConsultationRow[]>();

        Assert.NotNull(rows);
        Assert.Equal(new[] { "CM", "MF", "BQ" }, rows.Select(row => row.Type));
        Assert.Equal(new[] { "26", "18", "24/33" }, rows.Select(row => row.Lot));
    }

    [Fact]
    public async Task Armazem_MovementFeed_IsNewestFirstAndKeepsRawLotValues()
    {
        _fixture.Profile = ShellFixture.UserProfile.ArmazemOnly;
        var client = await LoginAsync();

        var response = await client.GetAsync("/api/armazem/movimentos?limit=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<ArmazemMovementRow[]>();

        Assert.NotNull(rows);
        Assert.Equal(3, rows.Length);
        Assert.Equal(new[] { "26", "24/33", "18" }, rows.Select(row => row.Lot));
        Assert.True(rows[0].OccurredAtUtc > rows[1].OccurredAtUtc);
        Assert.True(rows[1].OccurredAtUtc > rows[2].OccurredAtUtc);
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
        public static readonly Guid CrossModuleJobOnId =
            Guid.Parse("55555555-1111-2222-3333-444444444444");
        public static readonly Guid CrossModuleRevisionId =
            Guid.Parse("66666666-1111-2222-3333-444444444444");

        public enum UserProfile
        {
            BoquilhasOnly,
            JobOnResponsible,
            PesoOperador,
            PesoResponsavel,
            PegamentosOnly,
            PesoPlusPegamentos,
            AdminOnly,
            ArmazemOnly,
            ArmazemWithFerramentas,
            ReparacaoInternaOnly,
            TampoesOnly,
            NoInternalUser,
            TemplateInactive
        }

        public UserProfile Profile { get; set; } = UserProfile.BoquilhasOnly;

        private readonly FakeArmazemRepository _armazemRepository = new();
        private readonly FakeArmazemToolIdentityResolver _armazemResolver = new();

        public void Reset()
        {
            Profile = UserProfile.BoquilhasOnly;
            _armazemRepository.Reset();
        }

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
                ReplaceSingleton<IJobOnUserContextRepository>(services, new FakeJobOnUserContextRepository());
                ReplaceSingleton<IPesoRepository>(services, new FakePesoRepository());
                ReplaceSingleton<IArmazemRepository>(services, _armazemRepository);
                ReplaceSingleton<IToolIdentityResolver>(services, _armazemResolver);
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
                Task.FromResult(id == CrossModuleJobOnId ? BuildCrossModuleJobOn() : null);

            private static Domain.Modules.JobOn.JobOn BuildCrossModuleJobOn()
            {
                var revision = new JobOnRevision
                {
                    JobOnRevisionId = CrossModuleRevisionId,
                    JobOnId = CrossModuleJobOnId,
                    RevisionNumber = 3,
                    ReferenceSnapshot = "{\"article_reference\":\"5447T173\"}",
                    SavedAtUtc = new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc)
                };
                var jobOn = new Domain.Modules.JobOn.JobOn(
                    "202608", "B1",
                    new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 8, 22, 8, 0, 0, TimeSpan.Zero),
                    new[] { revision });
                jobOn.SaveRevision(revision);
                typeof(Domain.Modules.JobOn.JobOn)
                    .GetMethod("SetId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(jobOn, new object[] { CrossModuleJobOnId });
                return jobOn;
            }

            public Task<IReadOnlyList<Domain.Modules.JobOn.JobOn>> GetActiveAsync(string machineCode, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<Domain.Modules.JobOn.JobOn>>(Array.Empty<Domain.Modules.JobOn.JobOn>());

            public Task<Domain.Modules.JobOn.JobOn?> GetByProductionCodeAsync(string productionCode, CancellationToken cancellationToken = default) =>
                Task.FromResult<Domain.Modules.JobOn.JobOn?>(null);

            public Task TransitionLifecycleAsync(BA.Dmo.Domain.Modules.JobOn.JobOn jobOn, string actorId, CancellationToken cancellationToken = default) =>
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

            public Task SaveRevisionGraphAsync(JobOnRevision revision, string eventType, string actorId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task<Guid> DuplicateAtomicallyAsync(Domain.Modules.JobOn.JobOn newJobOn, JobOnRevision revision, Guid sourceJobOnId, string actorId, CancellationToken cancellationToken = default) =>
                Task.FromResult(Guid.NewGuid());
        }

        private sealed class FakeJobOnUserContextRepository : IJobOnUserContextRepository
        {
            private JobOnUserCurrent? _current;

            public Task SetCurrentAsync(
                string actorId,
                Guid jobOnId,
                string productionCode,
                string reference,
                string machineCode,
                CancellationToken cancellationToken = default)
            {
                _current = new JobOnUserCurrent(
                    jobOnId, productionCode, reference, machineCode,
                    new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.Zero));
                return Task.CompletedTask;
            }

            public Task<JobOnUserCurrent?> GetCurrentAsync(
                string actorId,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(_current);
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
            public Task UpdateControlAsync(Domain.Modules.Peso.PesoControl control, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
            public Task UpdateControlHeaderAsync(Domain.Modules.Peso.PesoControl control, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
            public Task DeleteControlAsync(Guid id, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
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

        /// <summary>
        /// Test-only Armazém persistence used by shell/visual verification. It
        /// keeps all facts in memory and deliberately mirrors the visual
        /// authority's CM/MF/BQ examples without touching a live database.
        /// </summary>
        private sealed class FakeArmazemRepository : IArmazemRepository
        {
            private readonly Dictionary<Guid, WarehouseLocation> _locations = new();
            private readonly List<WarehouseStock> _stocks = new();
            private readonly List<WarehouseMovement> _movements = new();

            public FakeArmazemRepository() => Reset();

            public void Reset()
            {
                _locations.Clear();
                _stocks.Clear();
                _movements.Clear();

                Seed(
                    FakeArmazemToolIdentityResolver.CmId,
                    "5126",
                    released: false,
                    WarehouseMovementDirection.In,
                    destination: null,
                    actor: "Ana Martins",
                    occurredAtUtc: new DateTimeOffset(2026, 8, 14, 10, 42, 0, TimeSpan.Zero));
                Seed(
                    FakeArmazemToolIdentityResolver.BqId,
                    "3108",
                    released: true,
                    WarehouseMovementDirection.Out,
                    destination: "Moldin",
                    actor: "João Silva",
                    occurredAtUtc: new DateTimeOffset(2026, 8, 14, 9, 16, 0, TimeSpan.Zero));
                Seed(
                    FakeArmazemToolIdentityResolver.MfId,
                    "2124",
                    released: true,
                    WarehouseMovementDirection.Out,
                    destination: "Orlando",
                    actor: "Ana Martins",
                    occurredAtUtc: new DateTimeOffset(2026, 8, 13, 15, 8, 0, TimeSpan.Zero));
            }

            private void Seed(
                Guid toolId,
                string position,
                bool released,
                WarehouseMovementDirection direction,
                string? destination,
                string actor,
                DateTimeOffset occurredAtUtc)
            {
                var location = new WarehouseLocation
                {
                    WarehouseLocationId = Guid.NewGuid(),
                    Code = position,
                    Kind = "tool"
                };
                _locations[location.WarehouseLocationId] = location;
                var stock = new WarehouseStock
                {
                    WarehouseStockId = Guid.NewGuid(),
                    WarehouseLocationId = location.WarehouseLocationId,
                    ToolId = toolId,
                    OccupiedSinceUtc = occurredAtUtc.AddHours(-1),
                    OccupiedBy = actor,
                    ReleasedAtUtc = released ? occurredAtUtc : null,
                    ReleasedBy = released ? actor : null
                };
                _stocks.Add(stock);
                _movements.Add(new WarehouseMovement
                {
                    WarehouseMovementId = Guid.NewGuid(),
                    WarehouseStockId = stock.WarehouseStockId,
                    Direction = direction,
                    Destination = destination,
                    ActorId = actor,
                    OccurredAtUtc = occurredAtUtc
                });
            }

            public Task<Guid> GetOrCreateLocationAsync(string code, string? kind, CancellationToken ct = default)
            {
                var existing = _locations.Values.FirstOrDefault(l => l.Code == code);
                if (existing is not null) return Task.FromResult(existing.WarehouseLocationId);
                var location = new WarehouseLocation
                {
                    WarehouseLocationId = Guid.NewGuid(),
                    Code = code,
                    Kind = kind
                };
                _locations[location.WarehouseLocationId] = location;
                return Task.FromResult(location.WarehouseLocationId);
            }

            public Task<WarehouseLocation?> GetLocationByCodeAsync(string code, CancellationToken ct = default) =>
                Task.FromResult(_locations.Values.FirstOrDefault(l => l.Code == code));

            public Task<WarehouseLocation?> GetLocationByIdAsync(Guid warehouseLocationId, CancellationToken ct = default) =>
                Task.FromResult(_locations.GetValueOrDefault(warehouseLocationId));

            public Task<WarehouseStock?> GetActiveStockByLocationAsync(Guid warehouseLocationId, CancellationToken ct = default) =>
                Task.FromResult(_stocks.FirstOrDefault(s => s.WarehouseLocationId == warehouseLocationId && s.IsActive));

            public Task<WarehouseStock?> GetActiveStockByToolIdAsync(Guid toolId, CancellationToken ct = default) =>
                Task.FromResult(_stocks.FirstOrDefault(s => s.ToolId == toolId && s.IsActive));

            public Task<IReadOnlyList<WarehouseStock>> GetStockByLocationAsync(Guid warehouseLocationId, CancellationToken ct = default) =>
                Task.FromResult<IReadOnlyList<WarehouseStock>>(_stocks.Where(s => s.WarehouseLocationId == warehouseLocationId).ToList());

            public Task<Guid> RegisterEntradaAsync(WarehouseStock stock, WarehouseMovement movement, CancellationToken ct = default)
            {
                if (_stocks.Any(s => s.WarehouseLocationId == stock.WarehouseLocationId && s.IsActive))
                    throw new ArmazemLocationOccupiedException("A posição já está ocupada.");
                _stocks.Add(stock);
                movement.WarehouseStockId = stock.WarehouseStockId;
                _movements.Add(movement);
                return Task.FromResult(stock.WarehouseStockId);
            }

            public Task RegisterSaidaAsync(
                Guid stockId,
                string? releasedBy,
                DateTimeOffset releasedAtUtc,
                WarehouseMovement movement,
                CancellationToken ct = default)
            {
                var stock = _stocks.Single(s => s.WarehouseStockId == stockId);
                stock.ReleasedAtUtc = releasedAtUtc;
                stock.ReleasedBy = releasedBy;
                movement.WarehouseStockId = stockId;
                _movements.Add(movement);
                return Task.CompletedTask;
            }

            public Task CorrectLocationAsync(
                Guid? currentStockId,
                WarehouseStock? correctedStock,
                WarehouseMovement? outMovement,
                WarehouseMovement? inMovement,
                CancellationToken ct = default)
            {
                if (currentStockId.HasValue)
                {
                    var current = _stocks.Single(s => s.WarehouseStockId == currentStockId.Value);
                    current.ReleasedAtUtc = outMovement!.OccurredAtUtc;
                    current.ReleasedBy = outMovement.ActorId;
                    outMovement.WarehouseStockId = currentStockId;
                    _movements.Add(outMovement);
                }
                if (correctedStock is not null)
                {
                    if (_stocks.Any(s => s.WarehouseLocationId == correctedStock.WarehouseLocationId && s.IsActive))
                        throw new ArmazemLocationOccupiedException("A posição já está ocupada.");
                    inMovement!.WarehouseStockId = correctedStock.WarehouseStockId;
                    _stocks.Add(correctedStock);
                    _movements.Add(inMovement);
                }
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<WarehouseMovement>> GetMovementHistoryAsync(Guid toolId, CancellationToken ct = default) =>
                Task.FromResult<IReadOnlyList<WarehouseMovement>>(
                    _movements.Where(m => _stocks.Any(s => s.WarehouseStockId == m.WarehouseStockId && s.ToolId == toolId))
                        .OrderBy(m => m.OccurredAtUtc).ToList());

            public Task<IReadOnlyList<WarehouseMovementFact>> ListMovementFactsAsync(
                DateTimeOffset? fromUtc,
                DateTimeOffset? toUtc,
                int limit,
                CancellationToken ct = default) =>
                Task.FromResult<IReadOnlyList<WarehouseMovementFact>>(
                    _movements
                        .Where(m => (!fromUtc.HasValue || m.OccurredAtUtc >= fromUtc.Value) &&
                                    (!toUtc.HasValue || m.OccurredAtUtc < toUtc.Value))
                        .Select(m =>
                        {
                            var stock = _stocks.Single(s => s.WarehouseStockId == m.WarehouseStockId);
                            return new WarehouseMovementFact(
                                stock.ToolId,
                                _locations[stock.WarehouseLocationId].Code,
                                m);
                        })
                        .OrderByDescending(f => f.Movement.OccurredAtUtc)
                        .Take(limit)
                        .ToList());

            public Task InsertAuditEventAsync(
                Guid? entityId,
                string eventType,
                string? beforeSnapshot,
                string? afterSnapshot,
                string actorId,
                CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class FakeArmazemToolIdentityResolver : IToolIdentityResolver
        {
            public static readonly Guid CmId = Guid.Parse("11111111-aaaa-4000-8000-000000000001");
            public static readonly Guid MfId = Guid.Parse("11111111-aaaa-4000-8000-000000000002");
            public static readonly Guid BqId = Guid.Parse("11111111-aaaa-4000-8000-000000000003");

            private static readonly IReadOnlyList<WarehouseToolIdentity> Identities =
            [
                new(CmId, WarehouseToolDomain.Ferramentas, "CM", "9389T194", "26", "Contra-molde 9389"),
                new(MfId, WarehouseToolDomain.Ferramentas, "MF", "5447T173", "18", "Molde 5447"),
                new(BqId, WarehouseToolDomain.Ferramentas, "BQ", "T173", "24/33", "Boquilha T173")
            ];

            public Task<IReadOnlyList<WarehouseToolIdentity>> SearchAsync(
                string type,
                string? reference,
                string? lot,
                CancellationToken ct = default) =>
                Task.FromResult<IReadOnlyList<WarehouseToolIdentity>>(Identities.Where(i =>
                    i.Type.Equals(type, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(reference) || i.Reference.Contains(reference, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(lot) || i.Lot.Contains(lot, StringComparison.OrdinalIgnoreCase))).ToList());

            public Task<WarehouseToolIdentity?> ResolveAsync(Guid toolId, CancellationToken ct = default) =>
                Task.FromResult(Identities.FirstOrDefault(i => i.ToolId == toolId));
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
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]",
                    UserProfile.JobOnResponsible =>
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[\"jobon.view\",\"jobon.edit\",\"jobon.configure\",\"jobon.confirmar\"]},{\"moduleId\":\"controlo\",\"capabilities\":[\"controlo.view\"]},{\"moduleId\":\"reparacao_interna\",\"capabilities\":[]}]",
                    UserProfile.PesoOperador =>
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"controlo\",\"capabilities\":[]}]",
                    UserProfile.PesoResponsavel =>
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"controlo\",\"capabilities\":[]}]",
                    UserProfile.PegamentosOnly =>
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"controlo\",\"capabilities\":[]}]",
                    UserProfile.PesoPlusPegamentos =>
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"controlo\",\"capabilities\":[]}]",
                    UserProfile.AdminOnly =>
                        "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\",\"audit.view\",\"audit.export\"]}]",
                    UserProfile.ArmazemOnly =>
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"armazem\",\"capabilities\":[]}]",
                    UserProfile.ArmazemWithFerramentas =>
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"armazem\",\"capabilities\":[]},{\"moduleId\":\"ferramentas\",\"capabilities\":[]}]",
                    UserProfile.ReparacaoInternaOnly =>
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"reparacao_interna\",\"capabilities\":[]}]",
                    UserProfile.TampoesOnly =>
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"tampoes\",\"capabilities\":[]}]",
                    UserProfile.TemplateInactive =>
                        "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]",
                    _ => "[]"
                };

                return Task.FromResult<InternalUserRecord?>(new InternalUserRecord(
                    "shell-actor", AuthUserId, "Utilizador Shell", ProfileName(fixture.Profile),
                    UserActive: true, TemplateId: "tpl-shell", TemplateName: "Shell",
                    TemplateActive: fixture.Profile != UserProfile.TemplateInactive,
                    ModulesJson: modulesJson,
                    FunctionalProfile: ProfileName(fixture.Profile)));
            }

            public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public Task CreateBootstrapAdminAsync(
                BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            private static string ProfileName(UserProfile profile) => profile switch
            {
                UserProfile.AdminOnly => "Admin",
                UserProfile.JobOnResponsible or UserProfile.PesoResponsavel => "Responsável",
                _ => "Operador / Controlador"
            };
        }
    }
}
