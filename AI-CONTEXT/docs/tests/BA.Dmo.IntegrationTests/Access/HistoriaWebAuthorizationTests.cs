using System.Net;
using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Modules.Historia;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// U-18 — História transversal web tests (modules/11 GLM-HIST-07 E2E subset,
/// TD-24). Verify: unauthenticated /historia redirects; operational module
/// assignment derives the História surface; only events of granted origin modules reach
/// the read projection (TD-24) — administration events are excluded without
/// audit.view. All collaborators are fakes — no live Supabase/DB.
/// </summary>
public class HistoriaWebAuthorizationTests :
    IClassFixture<HistoriaWebAuthorizationTests.HistoriaFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("eeeeeeee-1111-2222-3333-444444444444");

    private readonly HistoriaFixture _fixture;

    public HistoriaWebAuthorizationTests(HistoriaFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task Unauth_HistoriaPage_RedirectsToLogin()
    {
        var client = _fixture.CreateTestClient();
        var response = await client.GetAsync("/historia");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Historia_IsDerivedFromAnOperationalModule()
    {
        _fixture.Modules = "controlo";
        var client = _fixture.CreateTestClient();
        await LoginAsync(client);

        var page = await client.GetAsync("/historia");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task WithHistoria_OnlyGrantedOriginModulesReachTheProjection()
    {
        // TD-24: the identity holds the `historia` module (page policy) plus the
        // origin modules whose events we assert reach the projection.
        _fixture.Modules = "controlo,tampoes";
        _fixture.Repository.Groups =
        [
            Group("armazem|lote-arm", "Lote Armazém AR-1", "armazem", "lote", "lote-arm"),
            Group("peso|ctl-1", "Controlo CTL-1", "peso", "controlo", "ctl-1"),
            Group("tampoes|cfg-1", "Config T-1", "tampoes", "configuracao", "cfg-1")
        ];
        var client = _fixture.CreateTestClient();
        await LoginAsync(client);

        var page = await client.GetAsync("/historia");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("História transversal", html);

        // TD-24: grant peso + tampoes → only their events appear; the armazem
        // group (not granted) must not reach the projection.
        Assert.Contains("Controlo CTL-1", html);
        Assert.Contains("Config T-1", html);
        Assert.DoesNotContain("Lote Armazém AR-1", html);

        // TD-24: the repository received the granted origin modules (peso +
        // tampoes); Controlo also derives its other internal technical area.
        var lastVisible = _fixture.Repository.LastVisibleModules
            ?? Array.Empty<string>();
        Assert.Equal(new[] { "pegamentos", "peso", "tampoes" }, lastVisible);
    }

    [Fact]
    public async Task WithHistoria_AdminEventsExcludedWithoutAuditView()
    {
        _fixture.Modules = "controlo";
        _fixture.Repository.Groups =
        [
            Group("admin|usr-1", "Utilizador Admin", "admin", "utilizador", "usr-1"),
            Group("peso|ctl-2", "Controlo CTL-2", "peso", "controlo", "ctl-2")
        ];
        var client = _fixture.CreateTestClient();
        await LoginAsync(client);

        var page = await client.GetAsync("/historia");
        var html = await page.Content.ReadAsStringAsync();

        // No audit.view → admin group must not reach the projection.
        Assert.DoesNotContain("Utilizador Admin", html);
        Assert.False(_fixture.Repository.LastIncludeAdmin);
        Assert.Contains("Controlo CTL-2", html);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var response = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "historia@ba-dmo.example",
            ["password"] = "correct"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static HistoriaGroupRow Group(
        string key, string label, string module,
        string entityType, string entityId) =>
        new(key, label, module, entityType, entityId,
            new[]
            {
                new HistoriaEntryRow(
                    OccurredAtUtc: new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero),
                    Year: 2026,
                    ActorUserId: "actor-1",
                    ActorNameSnapshot: "Operador",
                    ModuleId: module,
                    ActionCode: module + ".record.created",
                    EntityType: entityType,
                    EntityId: entityId,
                    EntityLabelSnapshot: label,
                    Result: "succeeded",
                    Reason: null,
                    JobOnId: null,
                    RevisionId: null,
                    BeforeSummary: null,
                    AfterSummary: null)
            });

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string url, Dictionary<string, string> fields)
    {
        var path = url.Split('?')[0];
        var form = await client.GetAsync(path);
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

    public sealed class HistoriaFixture : WebApplicationFactory<Program>
    {
        /// <summary>Comma-separated module ids granted to the user (TD-24 origin scope).</summary>
        public string Modules { get; set; } = "peso,tampoes";

        public FakeHistoriaReadRepository Repository { get; } = new();

        public void Reset()
        {
            Modules = "controlo,tampoes";
            Repository.Reset();
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
                ReplaceSingleton<IHistoriaRepository>(services, Repository);
                ReplaceSingleton<IAdminRepository>(services, new FakeAdminRepo());
                ReplaceSingleton<IJobOnRepository>(services, new FakeJobOnRepo());
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

        private sealed class FakeIdentityRepository(HistoriaFixture fixture) : IInternalUserRepository
        {
            public Task<InternalUserRecord?> FindByAuthUserIdAsync(
                Guid authUserId, CancellationToken cancellationToken = default)
            {
                if (authUserId != AuthUserId)
                    return Task.FromResult<InternalUserRecord?>(null);

                var moduleIds = fixture.Modules.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var grants = string.Join(",", moduleIds.Select(m =>
                    $"{{\"moduleId\":\"{m}\",\"capabilities\":[]}}"));
                return Task.FromResult<InternalUserRecord?>(new InternalUserRecord(
                    "historia-actor", AuthUserId, "Operador História", "Operador / Controlador",
                    UserActive: true, TemplateId: "tpl-hist", TemplateName: "História",
                    TemplateActive: true, ModulesJson: $"[{grants}]"));
            }

            public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public Task CreateBootstrapAdminAsync(
                BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private sealed class FakeAdminRepo : IAdminRepository
        {
            public Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(string? search, CancellationToken ct = default) =>
                Task.FromResult<IReadOnlyList<AdminUserRow>>(Array.Empty<AdminUserRow>());
            public Task<AdminUserRow?> GetUserAsync(string actorId, CancellationToken ct = default) =>
                Task.FromResult<AdminUserRow?>(null);
            public Task<bool> AuthUserIdAlreadyRegisteredAsync(Guid authUserId, CancellationToken ct = default) =>
                Task.FromResult(false);
            public Task CreateInternalUserAsync(string actorId, Guid authUserId, string displayName, string? profileTitle, string templateId, bool active, DateTimeOffset createdAtUtc, CancellationToken ct = default) =>
                Task.CompletedTask;
            public Task UpdateUserAsync(string actorId, string displayName, string? profileTitle, DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc, CancellationToken ct = default) =>
                Task.CompletedTask;
            public Task<bool> ChangeUserTemplateAsync(string actorId, string templateId, DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc, CancellationToken ct = default) =>
                Task.FromResult(true);
            public Task<bool> ReplaceUserAccessTemplatesAsync(string actorId, IReadOnlyList<string> templateIds, DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc, CancellationToken ct = default) =>
                Task.FromResult(true);
            public Task<bool> SetUserActiveAsync(string actorId, bool active, DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc, CancellationToken ct = default) =>
                Task.FromResult(true);
            public Task SetUserModulesOverrideAsync(string actorId, string modulesJson, DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc, CancellationToken ct = default) =>
                Task.CompletedTask;
            public Task<int> CountActiveAdminsAsync(string? excludeActorId = null, CancellationToken ct = default) =>
                Task.FromResult(1);
            public Task<IReadOnlyList<AdminTemplateRow>> ListTemplatesAsync(CancellationToken ct = default) =>
                Task.FromResult<IReadOnlyList<AdminTemplateRow>>(Array.Empty<AdminTemplateRow>());
            public Task<AdminTemplateRow?> GetTemplateAsync(string templateId, CancellationToken ct = default) =>
                Task.FromResult<AdminTemplateRow?>(null);
            public Task CreateTemplateAsync(string templateId, string name, string modulesJson, DateTimeOffset createdAtUtc, CancellationToken ct = default) =>
                Task.CompletedTask;
            public Task<bool> UpdateTemplateAsync(string templateId, string name, string modulesJson, bool active, DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc, CancellationToken ct = default) =>
                Task.FromResult(true);
            public Task InsertAuditEventAsync(AuditEntry entry, CancellationToken ct = default) =>
                Task.CompletedTask;
            public Task<AuditQueryResult> QueryAuditAsync(AuditQueryFilter filter, CancellationToken ct = default) =>
                Task.FromResult(new AuditQueryResult(Array.Empty<AuditEventRow>(), 0, filter.Page, filter.PageSize));
        }
    }

    /// <summary>Fake História read port recording the TD-24 scope it received.</summary>
    public sealed class FakeHistoriaReadRepository : IHistoriaRepository
    {
        public IReadOnlyList<HistoriaGroupRow> Groups { get; set; } = Array.Empty<HistoriaGroupRow>();
        public IReadOnlyCollection<string>? LastVisibleModules { get; private set; }
        public bool LastIncludeAdmin { get; private set; }

        public void Reset()
        {
            Groups = Array.Empty<HistoriaGroupRow>();
            LastVisibleModules = null;
            LastIncludeAdmin = false;
        }

        public Task<HistoriaQueryResult> QueryAsync(
            HistoriaFilter filter,
            IReadOnlyCollection<string> visibleModuleIds,
            bool includeAdminWithAuditView,
            CancellationToken cancellationToken = default)
        {
            LastVisibleModules = visibleModuleIds;
            LastIncludeAdmin = includeAdminWithAuditView;

            // TD-24: only serve groups whose module is visible (double safety —
            // the gate already scopes; the projection also filters).
            var allowed = Groups.Where(g => visibleModuleIds.Contains(g.ModuleId)).ToList();
            return Task.FromResult(new HistoriaQueryResult(
                allowed, allowed.Count, filter.Page, filter.PageSize));
        }

        public Task<IReadOnlyList<HistoriaEntryRow>> QueryFlatAsync(
            HistoriaFilter filter,
            IReadOnlyCollection<string> visibleModuleIds,
            bool includeAdminWithAuditView,
            CancellationToken cancellationToken = default)
        {
            LastVisibleModules = visibleModuleIds;
            LastIncludeAdmin = includeAdminWithAuditView;
            return Task.FromResult<IReadOnlyList<HistoriaEntryRow>>(Array.Empty<HistoriaEntryRow>());
        }
    }

    private sealed class FakeJobOnRepo : IJobOnRepository
    {
        public Task<Guid> CreateAsync(JobOn jobOn, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());
        public Task<JobOn?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<JobOn?>(null);
        public Task<IReadOnlyList<JobOn>> GetActiveAsync(string machineCode, DateTime? from = null, DateTime? to = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<JobOn>>(Array.Empty<JobOn>());
        public Task<JobOn?> GetByProductionCodeAsync(string productionCode, CancellationToken ct = default) => Task.FromResult<JobOn?>(null);
        public Task UpdateLifecycleStateAsync(Guid id, JobOnLifecycleState newState, string actorId, CancellationToken ct = default) => Task.CompletedTask;
        public Task InsertRevisionAsync(JobOnRevision revision, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<JobOnRevision>> GetRevisionsAsync(Guid jobOnId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<JobOnRevision>>(Array.Empty<JobOnRevision>());
        public Task InsertComponentsAsync(IEnumerable<JobOnComponent> components, CancellationToken ct = default) => Task.CompletedTask;
        public Task InsertFieldsAsync(IEnumerable<JobOnComponentField> fields, CancellationToken ct = default) => Task.CompletedTask;
        public Task InsertRowsAsync(IEnumerable<JobOnComponentRow> rows, CancellationToken ct = default) => Task.CompletedTask;
        public Task InsertVerificationsAsync(IEnumerable<JobOnVerificationOccurrence> verifications, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateVerificationStatusAsync(Guid occurrenceId, string status, string? completedBy, DateTime? completedAtUtc, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Guid?> GetCurrentRevisionIdAsync(Guid jobOnId, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
        public Task UpdateCurrentRevisionAsync(Guid jobOnId, Guid revisionId, CancellationToken ct = default) => Task.CompletedTask;
        public Task InsertAuditEventAsync(Guid jobId, Guid? revisionId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken ct = default) => Task.CompletedTask;
        public Task InsertImageMutationAsync(JobOnRevision newRevision, Guid jobOnId, string eventType, string? beforeImageAssetId, string? afterImageAssetId, string actorId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<HistoricalProductionSummary>> GetHistoricalProductionsAsync(string? referenceFilter, string? machineFilter, DateTime? from, DateTime? to, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<HistoricalProductionSummary>>(Array.Empty<HistoricalProductionSummary>());
        public Task SaveRevisionGraphAsync(JobOnRevision revision, string eventType, string actorId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Guid> DuplicateAtomicallyAsync(JobOn newJobOn, JobOnRevision revision, Guid sourceJobOnId, string actorId, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());
    }
}
