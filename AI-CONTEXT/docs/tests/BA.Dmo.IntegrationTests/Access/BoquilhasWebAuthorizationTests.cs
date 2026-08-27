using System.Net;
using System.Net.Http.Json;
using BA.Dmo.Application.Modules.Boquilhas;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Boquilhas;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// U-19 — Boquilhas transversal web tests (01_BOQUILHAS_SPEC GLM-BQ-02, TD-24):
/// unauth /boquilhas redirects; a template without the <c>boquilhas</c> module is
/// denied; a template WITH <c>boquilhas</c> renders the page. API flows verify the
/// canonical create + the CONFIRMED 20→25 excess-return rule end to end (full
/// return accepted + open discrepancy, never a block). All collaborators are
/// fakes — no live Supabase/DB.
/// </summary>
public class BoquilhasWebAuthorizationTests :
    IClassFixture<BoquilhasWebAuthorizationTests.BoquilhasFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("eeeeeeee-1111-2222-3333-444444444444");

    /// <summary>App enums are serialized as strings (JsonStringEnumConverter); read with the same contract.</summary>
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly BoquilhasFixture _fixture;

    public BoquilhasWebAuthorizationTests(BoquilhasFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task Unauth_BoquilhasPage_RedirectsToLogin()
    {
        var client = _fixture.CreateTestClient();
        var response = await client.GetAsync("/boquilhas");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task WithoutBoquilhasModule_IsDenied()
    {
        _fixture.Modules = "controlo";
        var client = _fixture.CreateTestClient();
        await LoginAsync(client);
        var page = await client.GetAsync("/boquilhas");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.StartsWith("/access-denied", page.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task WithModule_PageRenders()
    {
        _fixture.Modules = "boquilhas,controlo";
        var client = _fixture.CreateTestClient();
        await LoginAsync(client);
        var page = await client.GetAsync("/boquilhas");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Boquilhas", html);
        Assert.DoesNotContain("value=\"scrapped\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Sucata<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenericMasterAndLifecycleSurfaces_AreNotCallable()
    {
        _fixture.Modules = "boquilhas";
        var client = _fixture.CreateTestClient();
        await LoginAsync(client);
        var lotId = Guid.NewGuid();

        var edit = await client.PutAsJsonAsync($"/api/boquilhas/lotes/{lotId}", new
        {
            reference = "T194",
            batchCode = "12",
            allowedLines = new[] { "B1" }
        });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, edit.StatusCode);

        var lifecycle = await client.PostAsJsonAsync($"/api/boquilhas/lotes/{lotId}/lifecycle", new
        {
            kind = "scrapped",
            reason = "obsolete surface"
        });
        Assert.Equal(HttpStatusCode.NotFound, lifecycle.StatusCode);

        var obsoleteFilter = await client.GetAsync(
            "/api/boquilhas/lotes?lifecycle=scrapped&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.BadRequest, obsoleteFilter.StatusCode);
    }

    [Fact]
    public async Task CreateLot_ThenReturn20To25_AcceptsFullReturnAndOpensDiscrepancy()
    {
        _fixture.Modules = "boquilhas";
        var client = _fixture.CreateTestClient();
        await LoginAsync(client);

        // Create lot (reference T194, lote 12) with initial quantity 60.
        var create = await client.PostAsJsonAsync("/api/boquilhas/lotes", new
        {
            reference = "T194",
            batchCode = "12",
            allowedLines = new[] { "B1", "C3" },
            initialQuantity = 60,
            initialUtilisation = (decimal?)10,
            notes = "criação"
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CreateLoteResponse>(options: JsonOptions);
        Assert.NotNull(created?.LotId);

        var lotId = created!.LotId;
        var summary = (await client.GetFromJsonAsync<BqLotSummaryDto>($"/api/boquilhas/lotes/{lotId}", JsonOptions))!;
        Assert.NotNull(summary.ActiveTrace);
        var traceId = summary.ActiveTrace!.BqTraceId;
        Assert.Equal(60, summary.Saldo.Prod);

        // Saída 20 → em reparação.
        var flow1 = await client.PostAsJsonAsync("/api/boquilhas/movements", new
        {
            bqLoteId = lotId, bqTraceId = traceId, movementType = "saida", qty = (decimal)20,
            repairerId = (Guid?)null, line = "B1", notes = "saída"
        });
        Assert.Equal(HttpStatusCode.OK, flow1.StatusCode);

        // Retorno 25 > repair 20 → full 25 accepted, exceptional 5, discrepancy open.
        var flow2 = await client.PostAsJsonAsync("/api/boquilhas/movements", new
        {
            bqLoteId = lotId, bqTraceId = traceId, movementType = "entrada", qty = (decimal)25,
            repairerId = (Guid?)null, line = "B1", notes = "retorno"
        });
        Assert.Equal(HttpStatusCode.OK, flow2.StatusCode);
        var returned = await flow2.Content.ReadFromJsonAsync<BqMovementRowDto>(options: JsonOptions);
        Assert.Equal(25, returned!.Qty);
        Assert.Equal(5, returned.ExceptionalReceivedQty);

        var summary2 = (await client.GetFromJsonAsync<BqLotSummaryDto>($"/api/boquilhas/lotes/{lotId}", JsonOptions))!;
        Assert.Equal(0, summary2.Saldo.Repair);
        Assert.Equal(60, summary2.Saldo.Prod);
        Assert.Equal(5, summary2.Saldo.ExceptionalReceived);

        var discs = await client.GetFromJsonAsync<List<BqDiscrepancyDto>>($"/api/boquilhas/discrepancies?lotId={lotId}", JsonOptions);
        var open = discs!.Single(d => d.Status == BqDiscrepancyStatus.Open);
        Assert.Equal(5, open.ExcessQty);
    }

    [Fact]
    public async Task DispatchExceedingProduction_IsBadRequest()
    {
        _fixture.Modules = "boquilhas";
        var client = _fixture.CreateTestClient();
        await LoginAsync(client);

        var create = await client.PostAsJsonAsync("/api/boquilhas/lotes", new
        {
            reference = "T194", batchCode = "12",
            allowedLines = new[] { "B1" }, initialQuantity = 10, initialUtilisation = (decimal?)null,
            notes = (string?)null
        });
        var created = await create.Content.ReadFromJsonAsync<CreateLoteResponse>(options: JsonOptions);
        var lotId = created!.LotId;
        var summary = (await client.GetFromJsonAsync<BqLotSummaryDto>($"/api/boquilhas/lotes/{lotId}", JsonOptions))!;
        var traceId = summary.ActiveTrace!.BqTraceId;

        var flow = await client.PostAsJsonAsync("/api/boquilhas/movements", new
        {
            bqLoteId = lotId, bqTraceId = traceId, movementType = "saida", qty = (decimal)15,
            repairerId = (Guid?)null, line = (string?)null, notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, flow.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var response = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "boquilhas@ba-dmo.example",
            ["password"] = "correct"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

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

    public sealed class CreateLoteResponse
    {
        public Guid LotId { get; set; }
    }

    public sealed class BoquilhasFixture : WebApplicationFactory<Program>
    {
        public string Modules { get; set; } = "boquilhas,peso";

        public FakeBoquilhasWebRepository Repository { get; } = new();

        public void Reset()
        {
            Modules = "boquilhas,controlo";
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
                ReplaceSingleton<IBoquilhasRepository>(services, Repository);
                ReplaceSingleton<IBoquilhasUnitOfWorkFactory>(services, new FakeBqWebUnitOfWorkFactory());
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

        private sealed class FakeIdentityRepository(BoquilhasFixture fixture) : IInternalUserRepository
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
                    "boquilhas-actor", AuthUserId, "Operador Boquilhas", "Operador / Controlador",
                    UserActive: true, TemplateId: "tpl-bq", TemplateName: "Boquilhas",
                    TemplateActive: true, ModulesJson: $"[{grants}]",
                    FunctionalProfile: "Operador / Controlador"));
            }

            public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public Task CreateBootstrapAdminAsync(
                BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }
    }

    /// <summary>No-op in-memory unit-of-work factory for the Boquilhas fixture (no DB).</summary>
    public sealed class FakeBqWebUnitOfWorkFactory : IBoquilhasUnitOfWorkFactory
    {
        public Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IDbUnitOfWork>(new FakeBqWebUnitOfWork());
    }

    public sealed class FakeBqWebUnitOfWork : IDbUnitOfWork
    {
        public System.Data.IDbConnection Connection => null!;
        public System.Data.IDbTransaction Transaction => null!;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
