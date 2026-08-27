using System.Net;
using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Modules.ReparacaoExterna;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.ReparacaoExterna;

/// <summary>
/// U-15 — Reparação Externa Web API endpoint + authorization guards.
/// /api/reparacao-externa/* requires the reparacao_externa module policy;
/// anonymous is denied; an authorized reparacao_externa user is admitted; a user
/// without the module is denied (access-denied). Collaborators are fakes — no live
/// Supabase/DB.
/// </summary>
public class ReparacaoExternaWebApiTests : IClassFixture<ReparacaoExternaWebApiTests.RepExtFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("cccccccc-2222-3333-4444-555555555555");

    private readonly RepExtFixture _fixture;

    public ReparacaoExternaWebApiTests(RepExtFixture fixture)
    {
        _fixture = fixture;
        _fixture.Repository.User = null;
    }

    [Theory]
    [InlineData("/api/reparacao-externa")]
    [InlineData("/api/reparacao-externa/repairers")]
    [InlineData("/api/reparacao-externa/historico")]
    [InlineData("/api/reparacao-externa/tools?type=CM")]
    public async Task Anonymous_IsDenied_RedirectsToLogin(string path)
    {
        var client = _fixture.CreateTestClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task AuthorizedRepExtUser_SearchTools_IsAdmitted()
    {
        _fixture.Repository.User = _fixture.ValidRepExtUser();
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "repx@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/api/reparacao-externa/repairers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserWithoutRepExtModule_IsDenied()
    {
        _fixture.Repository.User = _fixture.UserWithoutRepExt();
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new() { ["email"] = "other@ba-dmo.example", ["password"] = "correct" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/api/reparacao-externa/repairers");
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

    public sealed class RepExtFixture : WebApplicationFactory<Program>
    {
        public FakeIdentityRepository Repository { get; } = new();

        public InternalUserRecord ValidRepExtUser() => new(
            ActorId: "repx-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Operador Reparação",
            ProfileTitle: "Operador / Controlador",
            UserActive: true,
            TemplateId: "tpl-repx",
            TemplateName: "Reparação Externa",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"reparacao_externa\",\"capabilities\":[]}]");

        public InternalUserRecord UserWithoutRepExt() => new(
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
                Replace<IRepairRepository>(services, new FakeRepairRepo());
                Replace<IToolPieceResolver>(services, new FakeToolResolver());
                Replace<IArmazemRepairMovementPort>(services, new FakeArmazemRepair());
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

        private sealed class FakeRepairRepo : IRepairRepository
        {
            public Task<Guid> CreateExitAsync(RepairExit exit, RepairerSnapshot? snap, string? json, CancellationToken ct = default) => Task.FromResult(exit.RepairExitId);
            public Task<RepairExit?> GetExitByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RepairExit?>(null);
            public Task<IReadOnlyList<RepairExitItem>> GetExitItemsAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RepairExitItem>>(Array.Empty<RepairExitItem>());
            public Task<IReadOnlyList<RepairExit>> ListExitsAsync(RepairType? t, RepairExitStatus? s, DateOnly? f, DateOnly? to, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RepairExit>>(Array.Empty<RepairExit>());
            public Task<bool> ExistsItemInOpenExitAsync(Guid piece, CancellationToken ct = default) => Task.FromResult(false);
            public Task<Guid> AddItemAsync(RepairExitItem item, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());
            public Task<RepairExitItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default) => Task.FromResult<RepairExitItem?>(null);
            public Task DeleteItemAsync(Guid itemId, CancellationToken ct = default) => Task.CompletedTask;
            public Task ConfirmItemPickedAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default) => Task.CompletedTask;
            public Task ConfirmItemReturnedAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default) => Task.CompletedTask;
            public Task UpdateExitStatusAsync(IDbUnitOfWork uow, Guid exitId, string status, CancellationToken ct = default) => Task.CompletedTask;
            public Task InsertRepairEventAsync(IDbUnitOfWork uow, Guid itemId, string? notes, string actorId, DateTimeOffset when, CancellationToken ct = default) => Task.CompletedTask;
            public Task<Guid> CreateRepairerAsync(Repairer r, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());
            public Task UpdateRepairerAsync(Repairer r, CancellationToken ct = default) => Task.CompletedTask;
            public Task DeactivateRepairerAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
            public Task<Repairer?> GetRepairerByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Repairer?>(null);
            public Task<IReadOnlyList<Repairer>> ListRepairersAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Repairer>>(Array.Empty<Repairer>());
            public Task UpsertLineDefaultAsync(LineRepairerDefault d, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlyList<LineRepairerDefault>> ListLineDefaultsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LineRepairerDefault>>(Array.Empty<LineRepairerDefault>());
            public Task SetRepairerRepairTypesAsync(Guid repairerId, IEnumerable<string> repairTypes, CancellationToken ct = default) => Task.CompletedTask;
            public Task<IReadOnlySet<string>> ListRepairerRepairTypesAsync(Guid repairerId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
            public Task InsertAuditEventAsync(Guid? id, string type, string? b, string? a, string actor, CancellationToken ct = default) => Task.CompletedTask;
        }

        private sealed class FakeToolResolver : IToolPieceResolver
        {
            public Task<IReadOnlyList<RepairToolIdentity>> SearchAsync(RepairType t, string? r, string? l, string? n, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RepairToolIdentity>>(Array.Empty<RepairToolIdentity>());
            public Task<RepairToolIdentity?> ResolveAsync(Guid pieceId, CancellationToken ct = default) => Task.FromResult<RepairToolIdentity?>(null);
        }

        private sealed class FakeArmazemRepair : IArmazemRepairMovementPort
        {
            public Task<Result<bool, DomainError>> ConfirmPickupAsync(IDbUnitOfWork uow, Guid exitId, Guid toolLoteId, string actor, DateTimeOffset when, CancellationToken ct = default) => Task.FromResult(Result<bool, DomainError>.Success(true));
            public Task<Result<bool, DomainError>> ConfirmReturnAsync(IDbUnitOfWork uow, Guid exitId, Guid toolLoteId, string position, string actor, DateTimeOffset when, CancellationToken ct = default) => Task.FromResult(Result<bool, DomainError>.Success(true));
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
