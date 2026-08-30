using System.Net;
using System.Net.Http.Json;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.JobOnAccess;

/// <summary>
/// NOTE: the namespace deliberately avoids "BA.Dmo.IntegrationTests.JobOn",
/// which would shadow the JobOn domain type for sibling test files under
/// BA.Dmo.IntegrationTests.* (jobon-landing tests, access tests).
///
/// PHASE 4 — Job On authorization/isolation (U-07 route + endpoint level).
/// Verifies the ACCESS RESOLVER derivation contract end-to-end through the
/// real pipeline:
///   - jobon.view is derived from Job On module presence (the operator/
///     control profile gets /jobon; the legacy capability arrays inside
///     ModulesJson are deliberately NOT authorization input);
///   - a user WITHOUT the Job On module fails Job On authorization with the
///     safe /access-denied deep-link state (never a data leak, never a loop);
///   - operation-level isolation is enforced server-side: the edit endpoints
///     (/api/jobon/{id}/image/replace) require jobon.edit, which only the
///     Responsible profile receives — 403 without it, admitted with it.
/// Mirrors the FerramentasWebApiTests authorization-guard proof style. All
/// collaborators are fakes — no live Supabase/DB.
/// </summary>
public class JobOnAuthorizationTests : IClassFixture<JobOnAuthorizationTests.AuthFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("88888888-1111-2222-3333-444444444444");

    private static readonly Guid SomeJobOnId =
        Guid.Parse("44444444-5555-6666-7777-888888888888");

    private readonly AuthFixture _fixture;

    public JobOnAuthorizationTests(AuthFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task JobOnModuleWithOperatorController_GrantsJobOnView()
    {
        // Operator / Controlador profile + Job On module grant. The resolver
        // derives jobon.view + jobon.confirmar from module presence; the
        // legacy capability arrays in ModulesJson are NOT authorization input.
        _fixture.Repository.User = _fixture.JobOnOperator();
        var client = await LoginAsync();

        var response = await client.GetAsync("/jobon");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-view=\"planeamento\"", html);

        // jobon.edit is NOT derived for the operator/control profile: the
        // privileged folha-edit surface stays hidden server-side.
        Assert.DoesNotContain("Editar folha", html);
        Assert.DoesNotContain("Criar Job On", html);
    }

    [Fact]
    public async Task WithoutJobOnModule_JobOnIsDenied_WithSafeAccessDeniedState()
    {
        // A functional user (Boquilhas module) without the Job On module has
        // no jobon.view, so the /jobon landing policy fails closed.
        _fixture.Repository.User = _fixture.JobOnlessUser();
        var client = await LoginAsync();

        var denied = await client.GetAsync("/jobon");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/access-denied", denied.Headers.Location!.PathAndQuery);

        // Deep-link rule (GLM-ACC-07 s10): the safe state resolves the user's
        // own authorized area and redirects with feedback — never a data
        // leak, never a redirect loop.
        var safe = await client.GetAsync("/access-denied");
        Assert.Equal(HttpStatusCode.Redirect, safe.StatusCode);
        Assert.Equal("/boquilhas?acesso-negado=1", safe.Headers.Location!.ToString());
    }

    [Fact]
    public async Task JobOnModuleWithoutEditCapability_EditEndpointIsForbidden()
    {
        // Operator / Controlador holds jobon.view but NOT jobon.edit: the
        // route-level capability policy must deny the edit endpoint with 403
        // (same proof as FerramentasWebApiTests capability guard).
        _fixture.Repository.User = _fixture.JobOnOperator();
        var client = await LoginAsync();

        var denied = await client.PostAsJsonAsync(
            $"/api/jobon/{SomeJobOnId}/image/replace",
            new { jobOnId = SomeJobOnId, imageAssetId = "nope/evil.png" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task ResponsibleProfileWithJobOnModule_EditEndpointIsAdmitted()
    {
        // The Responsible profile derives jobon.edit from the Job On module:
        // the route-level policy admits the call. The request then fails
        // service-level validation (the image asset id is rejected by
        // ArticleReferenceImageRules before any write) — 400, NOT 401/403,
        // proving the capability gate opened.
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/jobon/{SomeJobOnId}/image/replace",
            new { jobOnId = SomeJobOnId, imageAssetId = "nope/evil.png" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The Responsible /jobon surface exposes the edit capability.
        var html = await (await client.GetAsync("/jobon")).Content.ReadAsStringAsync();
        Assert.Contains("Editar folha", html);
        Assert.Contains("Criar Job On", html);
    }

    // ---- create flow (R011) ------------------------------------------------

    [Fact]
    public async Task OperatorWithoutEditCapability_CannotCreateJobOn()
    {
        // Test #2 — a user with only jobon.view fails closed on the WRITE
        // operation: the route-level jobon.edit policy denies POST /api/jobon
        // with 403 before any code runs.
        _fixture.Repository.User = _fixture.JobOnOperator();
        var client = await LoginAsync();

        var denied = await client.PostAsJsonAsync("/api/jobon", new
        {
            productionCode = "202699",
            machineCode = "B1",
            plannedStartAt = "2026-08-20",
            plannedEndAt = (string?)null,
            reference = "9262T288"
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task ResponsibleProfile_CreatesJobOn_AndOpensTheCreatedFolha()
    {
        // Tests #1, #4, #5 — Responsible + Job On module creates a REAL Job On
        // (header + initial revision) and the creation target resolves into the
        // newly created Folha Job On (/jobon?id={jobOnId}).
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        var created = await client.PostAsJsonAsync("/api/jobon", new
        {
            productionCode = "202620",
            machineCode = "C1",
            plannedStartAt = "2026-08-21",
            plannedEndAt = (string?)null,
            reference = "5447T173"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var payload = await created.Content.ReadFromJsonAsync<CreateJobOnResponse>();
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.JobOnId);

        // The redirect target opens the created Folha Job On (real projection).
        var folha = await client.GetAsync($"/jobon?id={payload.JobOnId}");
        Assert.Equal(HttpStatusCode.OK, folha.StatusCode);
        var html = await folha.Content.ReadAsStringAsync();
        Assert.Contains("data-initial-view=\"sheet\"", html); // folha opens, not planning
        Assert.Contains("meta name=\"jobon-id\" content=\"" + payload.JobOnId, html);
        Assert.Contains("5447T173", html); // the entered reference renders in the folha
        Assert.Contains("202620", html);   // the entered production renders in the folha
    }

    [Fact]
    public async Task ResponsibleProfile_CreateWithMissingReference_IsRejected()
    {
        // Test #3 — required creation data is validated server-side (400).
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        var rejected = await client.PostAsJsonAsync("/api/jobon", new
        {
            productionCode = "202620",
            machineCode = "B1",
            plannedStartAt = "2026-08-21",
            plannedEndAt = (string?)null,
            reference = "   "
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var body = await rejected.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("JOBON_INVALID", body?.Code);
    }

    // ---- duplicate flow (modules/05 §6.2) ----------------------------------

    [Fact]
    public async Task OperatorWithoutEditCapability_CannotDuplicateJobOn()
    {
        // A user with only jobon.view fails closed on the WRITE operation at the
        // route level: POST /api/jobon/{id}/duplicate requires jobon.edit and is
        // denied with 403 before any code runs.
        _fixture.Repository.User = _fixture.JobOnOperator();
        var client = await LoginAsync();

        var denied = await client.PostAsJsonAsync(
            $"/api/jobon/{SomeJobOnId}/duplicate",
            new { productionCode = "202699", machineCode = "B1" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task ResponsibleProfile_DuplicatesJobOn_AndOpensTheDuplicatedFolha()
    {
        // Tests #1, #3, #4, #6, #10 — Responsible + jobon.edit duplicates a REAL
        // Job On: the new production/date context is applied (new header + copied
        // initial revision), a NEW JobOnId is returned, the source Job On remains
        // untouched, and the success target opens the duplicated Folha Job On
        // (/jobon?id={newJobOnId}).
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        // 1. Create the source Job On through the real create flow.
        var created = await client.PostAsJsonAsync("/api/jobon", new
        {
            productionCode = "202608",
            machineCode = "B1",
            plannedStartAt = "2026-08-17",
            plannedEndAt = (string?)null,
            reference = "5447T173"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var creation = await created.Content.ReadFromJsonAsync<CreateJobOnResponse>();
        var sourceId = creation!.JobOnId;

        // 2. Duplicate it with the NEW production/date context.
        var duplicated = await client.PostAsJsonAsync(
            $"/api/jobon/{sourceId}/duplicate",
            new
            {
                productionCode = "202699",
                machineCode = "C1",
                plannedStartAt = "2026-08-24",
                plannedEndAt = "2026-08-25"
            });
        Assert.Equal(HttpStatusCode.OK, duplicated.StatusCode);
        var payload = await duplicated.Content.ReadFromJsonAsync<CreateJobOnResponse>();
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.JobOnId);
        Assert.NotEqual(sourceId, payload.JobOnId); // a NEW JobOnId, never the source's

        // 3. The success target opens the newly created Folha Job On.
        var newFolha = await client.GetAsync($"/jobon?id={payload.JobOnId}");
        Assert.Equal(HttpStatusCode.OK, newFolha.StatusCode);
        var newHtml = await newFolha.Content.ReadAsStringAsync();
        Assert.Contains("data-initial-view=\"sheet\"", newHtml);
        Assert.Contains("meta name=\"jobon-id\" content=\"" + payload.JobOnId, newHtml);
        Assert.Contains("202699", newHtml); // the new production renders in the folha
        Assert.Contains("5447T173", newHtml); // the source reference is reused

        // 4. The source Folha Job On remains untouched.
        var sourceFolha = await client.GetAsync($"/jobon?id={sourceId}");
        Assert.Equal(HttpStatusCode.OK, sourceFolha.StatusCode);
        var sourceHtml = await sourceFolha.Content.ReadAsStringAsync();
        Assert.Contains("meta name=\"jobon-id\" content=\"" + sourceId, sourceHtml);
        Assert.Contains("202608", sourceHtml);
    }

    [Fact]
    public async Task ResponsibleProfile_DuplicateUnknownSource_ReturnsCleanNotFound()
    {
        // An unknown source maps to the existing clean NotFound behavior, never
        // a raw 500 (the identity-conflict mapping itself is unit-proven:
        // Duplicate_IdentityDuplicate_Raw23505_MapsToCleanDomainConflict).
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        var denied = await client.PostAsJsonAsync(
            $"/api/jobon/{SomeJobOnId}/duplicate",
            new { productionCode = "202699", machineCode = "B1" });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); // unknown source: clean 404
    }

    private sealed record CreateJobOnResponse(Guid JobOnId);

    private sealed record ErrorBody(string Code, string Message);

    private async Task<HttpClient> LoginAsync()
    {
        var client = _fixture.CreateTestClient();
        // Login round-trip: anti-forgery is disabled in this test host; the
        // fake adapter signs in the fixed auth user id for any credentials.
        var login = await client.PostAsync("/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["email"] = "jobon@ba-dmo.example",
                ["password"] = "correct"
            }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    /// <summary>
    /// Test host with fakes for the provider adapter, the identity repository
    /// (switchable per test) and the Job On repository. Matches the
    /// JobOnLandingTests fixture pattern; legacy capability arrays in
    /// ModulesJson are intentionally left empty so the AccessResolver
    /// derivation is what grants capabilities.
    /// </summary>
    public sealed class AuthFixture : WebApplicationFactory<Program>
    {
        public FakeIdentityRepository Repository { get; } = new();

        public InternalUserRecord JobOnOperator() => new(
            ActorId: "jobon-operator",
            AuthUserId: AuthUserId,
            DisplayName: "Operador Job On",
            ProfileTitle: FunctionalProfileNames.OperatorController,
            UserActive: true,
            TemplateId: "tpl-jobon-op",
            TemplateName: "Job On",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"jobon\",\"capabilities\":[]}]",
            FunctionalProfile: FunctionalProfileNames.OperatorController);

        public InternalUserRecord JobOnResponsible() => new(
            ActorId: "jobon-responsavel",
            AuthUserId: AuthUserId,
            DisplayName: "Responsável Job On",
            ProfileTitle: FunctionalProfileNames.Responsible,
            UserActive: true,
            TemplateId: "tpl-jobon-resp",
            TemplateName: "Job On",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"jobon\",\"capabilities\":[]}]",
            FunctionalProfile: FunctionalProfileNames.Responsible);

        /// <summary>Functional user with NO Job On module (Boquilhas only).</summary>
        public InternalUserRecord JobOnlessUser() => new(
            ActorId: "jobonless-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Utilizador Boquilhas",
            ProfileTitle: FunctionalProfileNames.OperatorController,
            UserActive: true,
            TemplateId: "tpl-bq",
            TemplateName: "Boquilhas",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]",
            FunctionalProfile: FunctionalProfileNames.OperatorController);

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
                ReplaceSingleton<IJobOnRepository>(services, new MemoryJobOnRepository());
                ReplaceSingleton<IJobOnUserContextRepository>(services, new FakeJobOnUserContextRepository());
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
                Task.FromResult(Result<AuthUser, DomainError>.Success(new AuthUser(AuthUserId, email)));
        }

        /// <summary>
        /// In-memory Job On repository (R011 create-flow tests): atomically created
        /// Job Ons (header + initial revision) become readable through GetByIdAsync,
        /// so a successful create can open the newly created folha/redirect target.
        /// All other port members stay inert.
        /// </summary>
        private sealed class MemoryJobOnRepository : IJobOnRepository
        {
            private readonly Dictionary<Guid, Domain.Modules.JobOn.JobOn> _jobOns = new();
            private readonly List<JobOnRevision> _revisions = [];

            public Task<Guid> CreateAsync(Domain.Modules.JobOn.JobOn jobOn, CancellationToken cancellationToken = default)
            {
                var id = Guid.NewGuid();
                SetId(jobOn, id);
                _jobOns[id] = jobOn;
                return Task.FromResult(id);
            }

            public Task<Guid> CreateAtomicallyAsync(
                Domain.Modules.JobOn.JobOn jobOn,
                JobOnRevision initialRevision,
                string actorId,
                CancellationToken cancellationToken = default)
            {
                var id = Guid.NewGuid();
                SetId(jobOn, id);
                _jobOns[id] = jobOn;
                var pinned = initialRevision with { JobOnId = id };
                _revisions.Add(pinned);
                _jobOns[id].SaveRevision(pinned);
                return Task.FromResult(id);
            }

            public Task<Domain.Modules.JobOn.JobOn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            {
                if (!_jobOns.TryGetValue(id, out var stored))
                    return Task.FromResult<Domain.Modules.JobOn.JobOn?>(null);
                var revisions = _revisions
                    .Where(r => r.JobOnId == id)
                    .OrderBy(r => r.RevisionNumber)
                    .ToList();
                var jobOn = new Domain.Modules.JobOn.JobOn(
                    stored.ProductionCode,
                    stored.MachineCode,
                    stored.PlannedStartAt,
                    stored.PlannedEndAt,
                    revisions);
                SetId(jobOn, id);
                foreach (var revision in revisions)
                    jobOn.SaveRevision(revision);
                return Task.FromResult<Domain.Modules.JobOn.JobOn?>(jobOn);
            }

            private static void SetId(Domain.Modules.JobOn.JobOn jobOn, Guid id)
            {
                typeof(Domain.Modules.JobOn.JobOn)
                    .GetMethod("SetId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(jobOn, new object[] { id });
            }

            public Task<IReadOnlyList<Domain.Modules.JobOn.JobOn>> GetActiveAsync(
                string machineCode, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<Domain.Modules.JobOn.JobOn>>(Array.Empty<Domain.Modules.JobOn.JobOn>());

            public Task<Domain.Modules.JobOn.JobOn?> GetByProductionCodeAsync(
                string productionCode, CancellationToken cancellationToken = default) =>
                Task.FromResult<Domain.Modules.JobOn.JobOn?>(null);

            public Task TransitionLifecycleAsync(
                Domain.Modules.JobOn.JobOn jobOn, string actorId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task InsertRevisionAsync(JobOnRevision revision, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task<IReadOnlyList<JobOnRevision>> GetRevisionsAsync(
                Guid jobOnId, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<JobOnRevision>>(
                    _revisions.Where(r => r.JobOnId == jobOnId).ToList());

            public Task InsertComponentsAsync(
                IEnumerable<JobOnComponent> components, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task InsertFieldsAsync(
                IEnumerable<JobOnComponentField> fields, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task InsertRowsAsync(
                IEnumerable<JobOnComponentRow> rows, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task InsertVerificationsAsync(
                IEnumerable<JobOnVerificationOccurrence> verifications, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task UpdateVerificationStatusAsync(
                Guid occurrenceId, string status, string? completedBy, DateTime? completedAtUtc, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task<Guid?> GetCurrentRevisionIdAsync(Guid jobOnId, CancellationToken cancellationToken = default) =>
                Task.FromResult<Guid?>(_jobOns.TryGetValue(jobOnId, out var jobOn) ? jobOn.CurrentRevisionId : null);

            public Task UpdateCurrentRevisionAsync(Guid jobOnId, Guid revisionId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task InsertAuditEventAsync(
                Guid jobId, Guid? revisionId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task InsertImageMutationAsync(
                JobOnRevision newRevision, Guid jobOnId, string eventType, string? beforeImageAssetId, string? afterImageAssetId, string actorId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task SaveRevisionGraphAsync(
                JobOnRevision revision, string eventType, string actorId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task<Guid> DuplicateAtomicallyAsync(
                Domain.Modules.JobOn.JobOn newJobOn, JobOnRevision revision, Guid sourceJobOnId, string actorId, CancellationToken cancellationToken = default)
            {
                // Mirror the real repository: a NEW job_on row (fresh DB id) with the
                // copied revision pinned to it and the current-revision link advanced —
                // readable through GetByIdAsync so the duplicated folha opens after a
                // successful duplicate. The header context comes from the service-built
                // duplicate (constructor-visible: production/machine/dates).
                var newId = Guid.NewGuid();
                var header = new Domain.Modules.JobOn.JobOn(
                    newJobOn.ProductionCode,
                    newJobOn.MachineCode,
                    newJobOn.PlannedStartAt,
                    newJobOn.PlannedEndAt,
                    Array.Empty<JobOnRevision>());
                SetId(header, newId);
                var pinned = revision with { JobOnId = newId };
                _revisions.Add(pinned);
                header.SaveRevision(pinned);
                _jobOns[newId] = header;
                return Task.FromResult(newId);
            }

            public Task<IReadOnlyList<HistoricalProductionSummary>> GetHistoricalProductionsAsync(
                string? referenceFilter, string? machineFilter, DateTime? from, DateTime? to, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<HistoricalProductionSummary>>(
                    Array.Empty<HistoricalProductionSummary>());
        }

        /// <summary>R011 — in-memory per-user current-open Job On context (avoids a live DB).</summary>
        private sealed class FakeJobOnUserContextRepository : IJobOnUserContextRepository
        {
            public Task SetCurrentAsync(
                string actorId,
                Guid jobOnId,
                string productionCode,
                string reference,
                string machineCode,
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task<JobOnUserCurrent?> GetCurrentAsync(
                string actorId, CancellationToken cancellationToken = default) =>
                Task.FromResult<JobOnUserCurrent?>(null);
        }
    }

    public sealed class FakeIdentityRepository : IInternalUserRepository
    {
        public InternalUserRecord? User { get; set; }

        public Task<InternalUserRecord?> FindByAuthUserIdAsync(
            Guid authUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(User);

        public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task CreateBootstrapAdminAsync(
            BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}