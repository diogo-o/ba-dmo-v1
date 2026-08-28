using System.Net;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Modules.ReparacaoInterna;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.ReparacaoInterna;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.ReparacaoInterna;

/// <summary>
/// U-16 — Reparação Interna Web API endpoint + authorization guards.
/// /api/reparacao-interna/* requires the reparacao_interna module policy; anonymous
/// is denied; an authorized reparacao_interna user is admitted; a user without the
/// module is denied (access-denied); the correction endpoint is 403 without the
/// reparacao_interna.corrigir capability and admitted with it. Collaborators are
/// fakes — no live Supabase/DB.
/// </summary>
public class ReparacaoInternaWebApiTests : IClassFixture<ReparacaoInternaWebApiTests.RepIntFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("dddddddd-1111-2222-3333-444444444444");

    private readonly RepIntFixture _fixture;

    public ReparacaoInternaWebApiTests(RepIntFixture fixture)
    {
        _fixture = fixture;
        _fixture.Repository.User = null;
    }

    [Theory]
    [InlineData("/api/reparacao-interna/line-cards")]
    [InlineData("/api/reparacao-interna/context?line=B1")]
    [InlineData("/api/reparacao-interna/historico")]
    public async Task Anonymous_IsDenied_RedirectsToLogin(string path)
    {
        var client = _fixture.CreateTestClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task AuthorizedRepIntUser_LineCards_IsAdmitted()
    {
        _fixture.Repository.User = _fixture.ValidRepIntUser(corrigir: false);
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "repan@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/api/reparacao-interna/line-cards");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ActiveSurface_ExposesOnlyCmMf_AndApiRejectsBq()
    {
        _fixture.Repository.User = _fixture.ValidRepIntUser(corrigir: true);
        var client = _fixture.CreateTestClient();
        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "repan@ba-dmo.example",
            ["password"] = "correct"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var page = await client.GetAsync("/reparacao-interna");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("data-type=\"CM\"", html, StringComparison.Ordinal);
        Assert.Contains("data-type=\"MF\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-type=\"BQ\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"BQ\"", html, StringComparison.Ordinal);

        var response = await client.PostAsync(
            "/api/reparacao-interna",
            new StringContent(
                "{\"line\":\"B1\",\"toolType\":\"BQ\",\"numbers\":[\"1\"]}",
                System.Text.Encoding.UTF8,
                "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProductionContext_IsReadOnly_InUiAndApi()
    {
        _fixture.Repository.User = _fixture.ValidRepIntUser(corrigir: true);
        var client = _fixture.CreateTestClient();
        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "repan@ba-dmo.example",
            ["password"] = "correct"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var page = await client.GetAsync("/reparacao-interna");
        var html = await page.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Editar contexto", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-toggle-override", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-toggle-ovcorrection", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"overridePanel\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"ovCorrectionPanel\"", html, StringComparison.Ordinal);

        var register = await client.PostAsync(
            "/api/reparacao-interna",
            new StringContent(
                "{\"line\":\"B1\",\"toolType\":\"CM\",\"numbers\":[\"1\"],\"overrideProduction\":\"P-OVERRIDE\",\"overrideReference\":\"5447T173\"}",
                System.Text.Encoding.UTF8,
                "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
        Assert.Contains("REPINT_CONTEXT_READ_ONLY", await register.Content.ReadAsStringAsync());

        var correction = await client.PostAsync(
            $"/api/reparacao-interna/{Guid.NewGuid()}/corrigir",
            new StringContent(
                "{\"line\":\"B1\",\"toolType\":\"CM\",\"individualNumber\":\"1\",\"productionCode\":\"P-OVERRIDE\",\"reference\":\"5447T173\"}",
                System.Text.Encoding.UTF8,
                "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, correction.StatusCode);
        Assert.Contains("REPINT_CONTEXT_READ_ONLY", await correction.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UserWithoutRepIntModule_IsDenied()
    {
        _fixture.Repository.User = _fixture.UserWithoutRepInt();
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "other@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/api/reparacao-interna/line-cards");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/access-denied", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task Correcao_WithoutCorrigirCapability_IsForbidden()
    {
        _fixture.Repository.User = _fixture.ValidRepIntUser(corrigir: false);
        var client = _fixture.CreateTestClient();
        var login = await PostFormAsync(client, "/login", new() { ["email"] = "repan@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.PostAsync(
            $"/api/reparacao-interna/{Guid.NewGuid()}/corrigir",
            new StringContent("{\"line\":\"B1\",\"toolType\":\"CM\",\"individualNumber\":\"1\",\"reason\":null}",
                System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("REPINT_CORRIGIR_FORBIDDEN", body);
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

    public sealed class RepIntFixture : WebApplicationFactory<Program>
    {
        public FakeIdentityRepository Repository { get; } = new();

        public InternalUserRecord ValidRepIntUser(bool corrigir) => new(
            ActorId: "repan-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Reparador de Turno",
            ProfileTitle: "Operador / Controlador",
            UserActive: true,
            TemplateId: "tpl-repan",
            TemplateName: "Reparação Interna",
            TemplateActive: true,
            ModulesJson: $"[{{\"moduleId\":\"reparacao_interna\",\"capabilities\":{(corrigir ? "[\"reparacao_interna.corrigir\"]" : "[]")}}}]",
            FunctionalProfile: "Operador / Controlador");

        public InternalUserRecord UserWithoutRepInt() => new(
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
                Replace<IReparacaoInternaRepository>(services, new FakeRepIntRepo());
                Replace<IJobOnActiveContextLookup>(services, new FakeContextLookup());
                Replace<IFerramentasPieceLookup>(services, new FakePieceLookup());
                Replace<IRepairUnitOfWorkFactory>(services, new FakeUowFactory());
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

        private sealed class FakeRepIntRepo : IReparacaoInternaRepository
        {
            public Task<Guid> InsertAsync(IDbUnitOfWork uow, InternalRepairRecord record, CancellationToken ct = default) => Task.FromResult(record.InternalRepairRecordId);
            public Task<InternalRepairRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<InternalRepairRecord?>(null);
            public Task<IReadOnlyList<InternalRepairRecord>> GetChainAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<InternalRepairRecord>>(Array.Empty<InternalRepairRecord>());
            public Task<IReadOnlyList<InternalRepairRecord>> ListAsync(DateTimeOffset? a, DateTimeOffset? b, string? c, Guid? d, InternalRepairToolType? e, string? f, string? g, bool h, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<InternalRepairRecord>>(Array.Empty<InternalRepairRecord>());
            public Task InsertRepairEventAsync(IDbUnitOfWork uow, Guid? id, string? notes, string actor, DateTimeOffset when, CancellationToken ct = default) => Task.CompletedTask;
            public Task InsertAuditEventAsync(IDbUnitOfWork uow, string action, string type, string id, Guid? jobOn, string result, string? b, string? a, string actor, DateTimeOffset when, CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class FakeContextLookup : IJobOnActiveContextLookup
        {
            public Task<InternalRepairContextResolution> ResolveActiveAsync(string line, DateTimeOffset at, CancellationToken ct = default)
                => Task.FromResult(InternalRepairContextResolution.None());
        }

        private sealed class FakePieceLookup : IFerramentasPieceLookup
        {
            public Task<IReadOnlyList<FerramentasPieceHit>> SearchAsync(FerramentasToolType t, string? r, string? l, string? n, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<FerramentasPieceHit>>(Array.Empty<FerramentasPieceHit>());
            public Task<FerramentasPieceHit?> ResolveAsync(Guid id, CancellationToken ct = default) => Task.FromResult<FerramentasPieceHit?>(null);
        }

        private sealed class FakeUowFactory : IRepairUnitOfWorkFactory
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
