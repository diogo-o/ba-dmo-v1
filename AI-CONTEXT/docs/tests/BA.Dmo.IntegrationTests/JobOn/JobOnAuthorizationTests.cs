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
    }

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
                ReplaceSingleton<IJobOnRepository>(services, new EmptyJobOnRepository());
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

        private sealed class EmptyJobOnRepository : IJobOnRepository
        {
            public Task<Guid> CreateAsync(Domain.Modules.JobOn.JobOn jobOn, CancellationToken cancellationToken = default) =>
                Task.FromResult(Guid.NewGuid());

            public Task<Domain.Modules.JobOn.JobOn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
                Task.FromResult<Domain.Modules.JobOn.JobOn?>(null);

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
                Task.FromResult<IReadOnlyList<JobOnRevision>>(Array.Empty<JobOnRevision>());

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
                Task.FromResult<Guid?>(null);

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
                Domain.Modules.JobOn.JobOn newJobOn, JobOnRevision revision, Guid sourceJobOnId, string actorId, CancellationToken cancellationToken = default) =>
                Task.FromResult(Guid.NewGuid());

            public Task<IReadOnlyList<HistoricalProductionSummary>> GetHistoricalProductionsAsync(
                string? referenceFilter, string? machineFilter, DateTime? from, DateTime? to, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<HistoricalProductionSummary>>(
                    Array.Empty<HistoricalProductionSummary>());
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