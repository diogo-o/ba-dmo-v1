using System.Net;
using BA.Dmo.Application.Modules.Tampoes;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Tampoes;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.Tampoes;

/// <summary>
/// U-17 — Tampões Web API endpoint + authorization guards. /api/tampoes/*
/// requires the tampoes module policy; anonymous denied; an authorized tampoes user
/// (Operator full access) admitted; a user without the module denied (access-denied).
/// Collaborators are fakes — no live Supabase/DB.
/// </summary>
public class TampaoWebApiTests : IClassFixture<TampaoWebApiTests.TampoesFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("eeeeeeee-1111-2222-3333-444444444444");

    private readonly TampoesFixture _fixture;

    public TampaoWebApiTests(TampoesFixture fixture)
    {
        _fixture = fixture;
        _fixture.Repository.User = null;
    }

    [Theory]
    [InlineData("/api/tampoes/consulta")]
    [InlineData("/api/tampoes/movimentos")]
    [InlineData("/api/tampoes/opcoes/fields?onlyActive=true")]
    public async Task Anonymous_IsDenied_RedirectsToLogin(string path)
    {
        var client = _fixture.CreateTestClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task AuthorizedTampoesUser_Consulta_IsAdmitted()
    {
        _fixture.Repository.User = _fixture.ValidTampoesUser();
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "tampoes@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/api/tampoes/consulta");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Planeamento_IsAbsentFromRenderedSurface_AndEndpoints()
    {
        _fixture.Repository.User = _fixture.ValidTampoesUser();
        var client = _fixture.CreateTestClient();
        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "tampoes@ba-dmo.example",
            ["password"] = "correct"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var page = await client.GetAsync("/tampoes");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.DoesNotContain("data-view=\"planeamento\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"planeamento\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-planear", html, StringComparison.Ordinal);
        Assert.DoesNotContain("planosTable", html, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/tampoes/planos?includeCanceled=false")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync("/api/tampoes/planear", JsonBody("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/tampoes/planos/{Guid.NewGuid()}/cancelar", JsonBody("{}"))).StatusCode);
    }

    [Fact]
    public async Task UserWithoutTampoesModule_IsDenied()
    {
        _fixture.Repository.User = _fixture.UserWithoutTampoes();
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "other@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/api/tampoes/consulta");
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

    private static StringContent JsonBody(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");

    public sealed class TampoesFixture : WebApplicationFactory<Program>
    {
        public FakeIdentityRepository Repository { get; } = new();

        public InternalUserRecord ValidTampoesUser() => new(
            ActorId: "tampoes-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Operador Tampões",
            ProfileTitle: "Operador / Controlador",
            UserActive: true,
            TemplateId: "tpl-tampoes",
            TemplateName: "Tampões",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"tampoes\",\"capabilities\":[]}]",
            FunctionalProfile: "Operador / Controlador");

        public InternalUserRecord UserWithoutTampoes() => new(
            ActorId: "other-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Outro",
            ProfileTitle: "Operador / Controlador",
            UserActive: true,
            TemplateId: "tpl-other",
            TemplateName: "Outro",
            TemplateActive: true,
            ModulesJson: "[]",
            FunctionalProfile: "Operador / Controlador");

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
                Replace<ITampaoRepository>(services, new FakeRepo());
                Replace<ITampoesUnitOfWorkFactory>(services, new FakeUowFactory());
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

        private sealed class FakeRepo : ITampaoRepository
        {
            public Task<IReadOnlyList<TampaoFieldDef>> ListFieldDefsAsync(bool onlyActive, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TampaoFieldDef>>(Array.Empty<TampaoFieldDef>());
            public Task<Guid> CreateFieldDefAsync(TampaoFieldDef field, CancellationToken ct = default) => Task.FromResult(field.TampaoFieldDefId);
            public Task UpdateFieldDefAsync(TampaoFieldDef field, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<TampaoFieldValue>> ListFieldValuesAsync(Guid fieldDefId, bool onlyActive, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TampaoFieldValue>>(Array.Empty<TampaoFieldValue>());
            public Task<Guid> CreateFieldValueAsync(TampaoFieldValue value, CancellationToken ct = default) => Task.FromResult(value.TampaoFieldValueId);
            public Task UpdateFieldValueAsync(TampaoFieldValue value, CancellationToken ct = default) => Task.CompletedTask;
            public Task<TampaoConfiguration?> FindConfigurationByKeyAsync(string valuesJson, CancellationToken ct = default) => Task.FromResult<TampaoConfiguration?>(null);
            public Task<TampaoConfiguration?> GetConfigurationByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TampaoConfiguration?>(null);
            public Task<IReadOnlyList<TampaoConfiguration>> ListConfigurationsAsync(bool onlyActive, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TampaoConfiguration>>(Array.Empty<TampaoConfiguration>());
            public Task<TampaoSaldo?> GetSaldoByConfigurationAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TampaoSaldo?>(null);
            public Task<Guid> CreateConfigurationAsync(IDbUnitOfWork uow, TampaoConfiguration config, string json, CancellationToken ct = default) => Task.FromResult(config.TampaoConfigurationId);
            public Task<TampaoSaldo?> GetSaldoInTransactionAsync(IDbUnitOfWork uow, Guid id, CancellationToken ct = default) => Task.FromResult<TampaoSaldo?>(null);
            public Task SetSaldoAsync(IDbUnitOfWork uow, Guid id, int e, int p, CancellationToken ct = default) => Task.CompletedTask;
            public Task InsertMovementAsync(IDbUnitOfWork uow, TampaoMovement m, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<TampaoMovement>> ListMovementsAsync(DateTimeOffset? a, DateTimeOffset? b, Guid? c, TampaoMovementType? t, string? o, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TampaoMovement>>(Array.Empty<TampaoMovement>());
            public Task<Guid> CreatePlanoAsync(TampaoPlano p, CancellationToken ct = default) => Task.FromResult(p.TampaoPlanoId);
            public Task<TampaoPlano?> GetPlanoByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TampaoPlano?>(null);
            public Task CancelPlanoAsync(IDbUnitOfWork uow, Guid id, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<TampaoPlano>> ListPlanosAsync(bool inc, Guid? c, DateOnly? f, DateOnly? t, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TampaoPlano>>(Array.Empty<TampaoPlano>());
            public Task InsertAuditEventAsync(IDbUnitOfWork uow, string action, string type, string id, string result, string? b, string? a, string actor, DateTimeOffset when, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlySet<string>> GetMachinesByConfigurationAsync(Guid configurationId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
            public Task ReplaceConfigurationMachinesAsync(IDbUnitOfWork uow, Guid configurationId, IEnumerable<string> machines, CancellationToken ct = default) => Task.CompletedTask;
            public Task InsertMachineEventAsync(IDbUnitOfWork uow, TampaoMachineEvent evt, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<TampaoMachineEvent>> ListMachineEventsAsync(Guid configurationId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TampaoMachineEvent>>(Array.Empty<TampaoMachineEvent>());
            public Task AddConfigurationNoteAsync(IDbUnitOfWork uow, TampaoConfigurationNote note, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<TampaoConfigurationNote>> ListConfigurationNotesAsync(Guid configurationId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TampaoConfigurationNote>>(Array.Empty<TampaoConfigurationNote>());
            public Task<IReadOnlyList<TampaoConfiguration>> ListConfigurationsByMachineAsync(string machine, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TampaoConfiguration>>(Array.Empty<TampaoConfiguration>());
        }

        private sealed class FakeUowFactory : ITampoesUnitOfWorkFactory
        {
            public Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IDbUnitOfWork>(new FakeUow());
        }

        private sealed class FakeUow : IDbUnitOfWork
        {
            public System.Data.IDbConnection Connection => null!;
            public System.Data.IDbTransaction Transaction => null!;
            public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
            public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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
