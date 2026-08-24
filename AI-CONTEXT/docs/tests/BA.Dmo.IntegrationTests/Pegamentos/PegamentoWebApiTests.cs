using System.Net;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Application.Shared;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Modules.Pegamentos;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.Pegamentos;

/// <summary>
/// U-11 — Pegamentos Web API endpoint + authorization guards.
/// All /api/pegamentos endpoints require the pegamentos module policy;
/// anonymous access is denied, an authorized pegamentos user is admitted.
/// Collaborators are fakes — no live Supabase/DB.
/// </summary>
public class PegamentoWebApiTests : IClassFixture<PegamentoWebApiTests.PegFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("aaaaaaaa-2222-3333-4444-555555555555");

    private readonly PegFixture _fixture;

    public PegamentoWebApiTests(PegFixture fixture)
    {
        _fixture = fixture;
        _fixture.Repository.User = null;
    }

    [Theory]
    [InlineData("/api/pegamentos/search")]
    [InlineData("/api/pegamentos/context/11111111-2222-3333-4444-555555555555")]
    [InlineData("/api/pegamentos/revision/11111111-2222-3333-4444-555555555555")]
    public async Task Anonymous_IsDenied_RedirectsToLogin(string path)
    {
        var client = _fixture.CreateTestClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task AuthorizedPegamentosUser_Search_IsAdmitted()
    {
        _fixture.Repository.User = _fixture.ValidPegamentosUser();
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "peg@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/api/pegamentos/search");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserWithoutPegamentosModule_IsDenied()
    {
        _fixture.Repository.User = _fixture.UserWithoutPegamentos();
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "other@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/api/pegamentos/search");
        // The user is authenticated but lacks the pegamentos grant → the module
        // policy denies access with an access-denied redirect (fail-closed),
        // never returning the data.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/access-denied", response.Headers.Location?.PathAndQuery);
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

    public sealed class PegFixture : WebApplicationFactory<Program>
    {
        public FakeIdentityRepository Repository { get; } = new();

        public InternalUserRecord ValidPegamentosUser() => new(
            ActorId: "peg-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Utilizador Pegamentos",
            ProfileTitle: "Metrologia",
            UserActive: true,
            TemplateId: "tpl-peg",
            TemplateName: "Pegamentos",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"pegamentos\",\"capabilities\":[]}]");

        public InternalUserRecord UserWithoutPegamentos() => new(
            ActorId: "other-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Outro",
            ProfileTitle: null,
            UserActive: true,
            TemplateId: "tpl-other",
            TemplateName: "Outro",
            TemplateActive: true,
            ModulesJson: "[]");

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
                Replace<IJobOnProductionFolderResolver>(services, new FakeResolver());
                Replace<IAppSettingsReader>(services, new FakeSettings());
                Replace<IPegamentoRepository>(services, new FakePegRepo());
                Replace<IJobOnProductionContextLookup>(services, new FakeContextLookup());
                services.Configure<Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions>(
                    options => options.Conventions.ConfigureFilter(
                        new IgnoreAntiforgeryTokenAttribute()));
            });
        }

        private static void Replace<TService>(IServiceCollection services, TService implementation)
            where TService : class
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(TService)).ToList())
                services.Remove(descriptor);
            services.AddScoped(_ => implementation);
        }

        private static void ReplaceSingleton<TService>(IServiceCollection services, TService implementation)
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

        // ---- Collaborator fakes (no live DB) ----

        private sealed class FakeResolver : IJobOnProductionFolderResolver
        {
            public Task<string?> ResolveAsync(Guid jobOnId, CancellationToken ct = default)
                => Task.FromResult<string?>("5447T173");
        }

        private sealed class FakeSettings : IAppSettingsReader
        {
            public Task<string?> GetOutputRootAsync(CancellationToken ct = default)
                => Task.FromResult<string?>("D:\\Documentos");
        }

        private sealed class FakeContextLookup : IJobOnProductionContextLookup
        {
            public Task<PegamentoProductionContext?> ResolveAsync(Guid jobOnRevisionId, CancellationToken ct = default)
                => Task.FromResult<PegamentoProductionContext?>(null);
        }

        private sealed class FakePegRepo : IPegamentoRepository
        {
            public Task<Guid> CreateAsync(PegamentoControlo control, CancellationToken ct = default) => Task.FromResult(control.PegamentoControloId);
            public Task<PegamentoControlo?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<PegamentoControlo?>(null);
            public Task<IReadOnlyList<PegamentoControlo>> GetByRevisionAsync(Guid jobOnRevisionId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PegamentoControlo>>(Array.Empty<PegamentoControlo>());
            public Task<IReadOnlyList<PegamentoControlo>> GetByJobOnAsync(Guid jobOnId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PegamentoControlo>>(Array.Empty<PegamentoControlo>());
            public Task<IReadOnlyList<PegamentoControlo>> SearchAsync(string? reference, string? productionCode, string? machine, DateTime? from, DateTime? to, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PegamentoControlo>>(Array.Empty<PegamentoControlo>());
            public Task UpdateAsync(PegamentoControlo control, CancellationToken ct = default) => Task.CompletedTask;
            public Task<Guid> AddMeasurementAsync(Guid controloId, PegamentoMedicao medicao, string actorId, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());
            public Task<IReadOnlyList<PegamentoMedicao>> GetMeasurementsAsync(Guid controloId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PegamentoMedicao>>(Array.Empty<PegamentoMedicao>());
            public Task UpsertDocumentAsync(PegamentoDocumento document, CancellationToken ct = default) => Task.CompletedTask;
            public Task<PegamentoDocumento?> GetDocumentAsync(Guid controloId, CancellationToken ct = default) => Task.FromResult<PegamentoDocumento?>(null);
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
