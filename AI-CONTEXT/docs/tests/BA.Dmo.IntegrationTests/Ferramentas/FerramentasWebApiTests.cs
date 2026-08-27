using System.Net;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.Ferramentas;

/// <summary>
/// U-12 — Ferramentas Web API endpoint + authorization guards.
/// /api/ferramentas/* requires the ferramentas module policy; anonymous is denied;
/// an authorized ferramentas user is admitted. Rule-configuration endpoints require
/// ferramentas.configure. Collaborators are fakes — no live Supabase/DB.
/// </summary>
public class FerramentasWebApiTests : IClassFixture<FerramentasWebApiTests.FerrFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("bbbbbbbb-2222-3333-4444-555555555555");

    private readonly FerrFixture _fixture;

    public FerramentasWebApiTests(FerrFixture fixture)
    {
        _fixture = fixture;
        _fixture.Repository.User = null;
    }

    [Theory]
    [InlineData("/api/ferramentas/references")]
    [InlineData("/api/ferramentas/references/11111111-2222-3333-4444-555555555555")]
    [InlineData("/api/ferramentas/lotes/11111111-2222-3333-4444-555555555555/rules")]
    public async Task Anonymous_IsDenied_RedirectsToLogin(string path)
    {
        var client = _fixture.CreateTestClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task AuthorizedFerramentasUser_Search_IsAdmitted()
    {
        _fixture.Repository.User = _fixture.ValidFerramentasUser(canConfigure: false);
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "ferr@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/api/ferramentas/references");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserWithoutFerramentasModule_IsDenied()
    {
        _fixture.Repository.User = _fixture.UserWithoutFerramentas();
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "other@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/api/ferramentas/references");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/access-denied", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task RulesEndpoint_WithoutConfigure_IsDenied()
    {
        // Rule POST/configure endpoints require ferramentas.configure.
        _fixture.Repository.User = _fixture.ValidFerramentasUser(canConfigure: false);
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "ferr@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.PostAsync("/api/ferramentas/lotes/11111111-2222-3333-4444-555555555555/rules",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        // The user is authenticated to ferramentas but lacks ferramentas.configure,
        // so the capability policy denies with 403 Forbidden.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RulesEndpoint_WithConfigure_IsAdmitted()
    {
        _fixture.Repository.User = _fixture.ValidFerramentasUser(canConfigure: true);
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "ferr@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        // The fake repo returns NotFound for an unknown lot, producing a 400
        // (not a 401/403), proving the ferramentas.configure policy admitted the call.
        var response = await client.PostAsync("/api/ferramentas/lotes/11111111-2222-3333-4444-555555555555/rules",
            new StringContent("{\"ruleText\":\"R\",\"frequency\":\"uma_vez_no_lote\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    public sealed class FerrFixture : WebApplicationFactory<Program>
    {
        public FakeIdentityRepository Repository { get; } = new();

        public InternalUserRecord ValidFerramentasUser(bool canConfigure) => new(
            ActorId: "ferr-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Utilizador Ferramentas",
            ProfileTitle: canConfigure ? "Responsável" : "Operador / Controlador",
            UserActive: true,
            TemplateId: "tpl-ferr",
            TemplateName: "Ferramentas",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"ferramentas\",\"capabilities\":[]}]");

        public InternalUserRecord UserWithoutFerramentas() => new(
            ActorId: "other-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Outro",
            ProfileTitle: "Operador / Controlador",
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
                Replace<IFerramentasRepository>(services, new FakeRepo());
                Replace<IFerramentasRuleLookup>(services, new FakeLookup());
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

        private sealed class FakeLookup : IFerramentasRuleLookup
        {
            public Task<IReadOnlyList<Domain.Modules.JobOn.VerificationRule>> ResolveActiveRulesAsync(Guid toolLoteId, CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<Domain.Modules.JobOn.VerificationRule>>(Array.Empty<Domain.Modules.JobOn.VerificationRule>());
        }

        private sealed class FakeRepo : IFerramentasRepository
        {
            public Task<Guid> CreateReferenceAsync(ToolReference reference, CancellationToken ct = default) => Task.FromResult(reference.ToolReferenceId);
            public Task<ToolReference?> GetReferenceByIdAsync(Guid referenceId, CancellationToken ct = default) => Task.FromResult<ToolReference?>(null);
            public Task<ToolReference?> GetReferenceByTypeAndCodeAsync(FerramentasToolType type, string refCode, CancellationToken ct = default) => Task.FromResult<ToolReference?>(null);
            public Task UpdateReferenceAsync(ToolReference reference, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<ToolReference>> SearchReferencesAsync(string? reference, string? technicalName, string? lote, string? drawing, string? line, string? processo, string? ownerPlant, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ToolReference>>(Array.Empty<ToolReference>());
            public Task<Guid> CreateLoteAsync(ToolLote lote, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());
            public Task<ToolLote?> GetLoteByIdAsync(Guid loteId, CancellationToken ct = default) => Task.FromResult<ToolLote?>(null);
            public Task UpdateLoteAsync(ToolLote lote, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<ToolLote>> GetLotesByReferenceAsync(Guid referenceId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ToolLote>>(Array.Empty<ToolLote>());
            public Task<bool> LoteExistsInReferenceAsync(Guid referenceId, string lote, CancellationToken ct = default) => Task.FromResult(false);
            public Task<Guid> RegisterPieceAsync(PhysicalPiece piece, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());
            public Task UpdatePieceAsync(PhysicalPiece piece, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<PhysicalPiece>> GetPiecesByLoteAsync(Guid loteId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PhysicalPiece>>(Array.Empty<PhysicalPiece>());
            public Task<Guid> AddCheckRuleAsync(ToolCheckRule rule, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());
            public Task UpdateCheckRuleAsync(ToolCheckRule rule, CancellationToken ct = default) => Task.CompletedTask;
            public Task ToggleCheckRuleActiveAsync(Guid ruleId, bool active, CancellationToken ct = default) => Task.CompletedTask;
            public Task DeleteCheckRuleAsync(Guid ruleId, CancellationToken ct = default) => Task.CompletedTask;
            public Task<Guid?> CopyCheckRuleAsync(Guid sourceRuleId, Guid targetLoteId, CancellationToken ct = default) => Task.FromResult<Guid?>(Guid.NewGuid());
            public Task<IReadOnlyList<ToolCheckRule>> GetCheckRulesByLoteAsync(Guid loteId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ToolCheckRule>>(Array.Empty<ToolCheckRule>());
            public Task<ToolCheckRule?> GetCheckRuleByIdAsync(Guid ruleId, CancellationToken ct = default) => Task.FromResult<ToolCheckRule?>(null);
            public Task<IReadOnlyList<ToolCheckOccurrence>> GetOccurrencesByRuleAsync(Guid ruleId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ToolCheckOccurrence>>(Array.Empty<ToolCheckOccurrence>());
            public Task<(Guid ReferenceId, Guid LoteId)> CreateReferenceWithFirstLoteAsync(ToolReference reference, ToolLote lote, CancellationToken ct = default) => Task.FromResult((reference.ToolReferenceId, lote.ToolLoteId));
            public Task InsertAuditEventAsync(Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken ct = default) => Task.CompletedTask;
            public Task RecordUtilisationReadingAsync(ToolUtilisationReading reading, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<ToolUtilisationReading>> ListUtilisationReadingsAsync(Guid toolLoteId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ToolUtilisationReading>>(Array.Empty<ToolUtilisationReading>());
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
