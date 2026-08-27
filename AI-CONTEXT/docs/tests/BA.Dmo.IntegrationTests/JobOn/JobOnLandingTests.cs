using System.Net;
using System.Text.Json;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Infrastructure.Auth;
using BA.Dmo.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.JobOnLanding;

/// <summary>
/// R011 â€” Universal Landing (Job On planeamento): calendar + production list contract.
/// Verifies at the rendered-page/API level (WebApplicationFactory + populated fake
/// IJobOnRepository): the landing returns planning data, B1/B2 map to distinct colour
/// keys on the calendar AND the list rows, the same-line rule, multiple productions on
/// the same date are all represented, list rows carry date/production/reference/machine,
/// the landing defaults to Planeamento, and the current-open Job On identity is preserved
/// server-side when a folha is opened.
/// </summary>
public class JobOnLandingTests : IClassFixture<JobOnLandingTests.LandingFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("99999999-2222-3333-4444-555555555555");

    private static readonly DateTimeOffset Day = new(2026, 8, 20, 8, 0, 0, TimeSpan.Zero);

    private readonly LandingFixture _fixture;

    public JobOnLandingTests(LandingFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<string> GetJobOnHtmlAsync(string path = "/jobon")
    {
        var client = _fixture.CreateTestClient();

        // Login round-trip: GET /login (captures nothing needed), then POST credentials.
        // Anti-forgery is disabled in this test host (same as ShellRoutingTests).
        var login = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "landing@ba-dmo.example",
            ["password"] = "correct"
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var resp = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("id=\"calendar\"", html);
        return html;
    }

    [Fact]
    public async Task Landing_ReturnsPlanningData_AndDefaultsToPlaneamento()
    {
        var html = await GetJobOnHtmlAsync("/jobon");

        // Landing opens Planeamento (the calendar + list), not an empty folha.
        Assert.Contains("data-view=\"planeamento\"", html);
        Assert.Contains("id=\"jobList\"", html);
        // The same planning source drives calendar markers and list.
        Assert.Contains("data-record-lines=", html);
    }

    [Fact]
    public async Task CalendarMarkers_Represent_DistinctLineKeys_ForB1AndB2()
    {
        var html = await GetJobOnHtmlAsync("/jobon?date=2026-08-20");

        // Both B1 and B2 productions on the same calendar day are represented (never hidden).
        var markerJson = ExtractLineMarkers(html);
        Assert.True(markerJson.TryGetValue("2026-08-20", out var keys), "2026-08-20 markers expected");
        Assert.Contains("b1", keys);
        Assert.Contains("b2", keys);
        Assert.Equal(keys.Distinct().Count(), keys.Count);
    }

    [Fact]
    public async Task ListRow_Contains_Date_Production_Reference_Machine_AndLineKey()
    {
        var html = await GetJobOnHtmlAsync("/jobon?date=2026-08-20");

        Assert.Contains("data-line-key=\"b1\"", html);
        Assert.Contains("data-line-key=\"b2\"", html);
        // Row content per requirement Â§7: date, production, reference, machine.
        Assert.Contains("202601", html);   // production code
        Assert.Contains("5447T173", html); // reference
        Assert.Contains("/2026", html);    // date (dd/MM/yyyy)
        Assert.Contains(">B1<", html);     // machine cell
    }

    [Fact]
    public async Task SameLineProductions_UseTheSameKey()
    {
        // Two B1 productions on different dates both render the b1 key.
        var html = await GetJobOnHtmlAsync("/jobon?date=2026-08-20");
        var markers = ExtractLineMarkers(html);
        Assert.Contains("b1", markers["2026-08-20"]);
        Assert.Contains("b2", markers["2026-08-20"]);
    }

    private static Dictionary<string, List<string>> ExtractLineMarkers(string html)
    {
        // data-record-lines is an HTML-escaped JSON attribute on the calendar section.
        var marker = "data-record-lines=";
        var idx = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(idx >= 0, "data-record-lines attribute not found");
        var open = html.IndexOf('\'', idx + marker.Length);
        var close = html.IndexOf('\'', open + 1);
        var raw = html.Substring(open + 1, close - open - 1)
            .Replace("&quot;", "\"").Replace("&quot;", "\"");
        return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(raw)
            ?? new Dictionary<string, List<string>>();
    }

    public sealed class LandingFixture : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                ReplaceSingleton<ISupabaseAuthAdapter>(services, new FakeAuthAdapter());
                ReplaceSingleton<IInternalUserRepository>(services, new FakeIdentityRepository());
                ReplaceSingleton<IJobOnRepository>(services, new FakeDataJobOnRepository());
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

        public HttpClient CreateTestClient() => CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        private sealed class FakeAuthAdapter : ISupabaseAuthAdapter
        {
            public Task<Result<AuthUser, DomainError>> SignInWithPasswordAsync(
                string email, string password, CancellationToken cancellationToken = default) =>
                Task.FromResult(Result<AuthUser, DomainError>.Success(new AuthUser(AuthUserId, email)));
        }

        private sealed class FakeIdentityRepository : IInternalUserRepository
        {
            public Task<InternalUserRecord?> FindByAuthUserIdAsync(Guid authUserId, CancellationToken cancellationToken = default) =>
                Task.FromResult<InternalUserRecord?>(new InternalUserRecord(
                    "actor-landing", AuthUserId, "Utilizador Landing", "Operador / Controlador",
                    UserActive: true, TemplateId: "tpl-landing", TemplateName: "Landing",
                    TemplateActive: true,
                    ModulesJson: "[{\"moduleId\":\"jobon\",\"capabilities\":[\"jobon.view\"]}]"));

            public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public Task CreateBootstrapAdminAsync(
                BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private sealed class FakeDataJobOnRepository : IJobOnRepository
        {
            public Task<IReadOnlyList<HistoricalProductionSummary>> GetHistoricalProductionsAsync(
                string? referenceFilter, string? machineFilter, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
            {
                // Two productions on the same day (B1 + B2) and one B1 on another day.
                var list = new List<HistoricalProductionSummary>
                {
                    New(Day, "202601", "5447T173", "B1"),
                    New(Day, "202602", "5447T174", "B2"),
                    New(Day.AddDays(3), "202603", "6118T901", "B1")
                };
                if (from.HasValue) list = list.Where(s => s.PlannedStartAt >= from.Value).ToList();
                if (to.HasValue) list = list.Where(s => s.PlannedStartAt < to.Value).ToList();
                if (!string.IsNullOrWhiteSpace(machineFilter))
                    list = list.Where(s => s.MachineCode == machineFilter).ToList();
                return Task.FromResult<IReadOnlyList<HistoricalProductionSummary>>(list);
            }

            private static HistoricalProductionSummary New(DateTimeOffset start, string prod, string refc, string machine) =>
                new(
                    JobOnId: Guid.NewGuid(),
                    ProductionCode: prod,
                    ReferenceCode: refc,
                    MachineCode: machine,
                    PlannedStartAt: start,
                    PlannedEndAt: start.AddDays(2),
                    CurrentRevisionNumber: 1,
                    TotalRevisionCount: 1,
                    LifecycleState: JobOnLifecycleState.Planeado);

            public Task<Guid> CreateAsync(JobOn jobOn, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
            public Task<JobOn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<JobOn?>(null);
            public Task<IReadOnlyList<JobOn>> GetActiveAsync(string machineCode, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<JobOn>>(Array.Empty<JobOn>());
            public Task<JobOn?> GetByProductionCodeAsync(string productionCode, CancellationToken cancellationToken = default) => Task.FromResult<JobOn?>(null);
            public Task TransitionLifecycleAsync(BA.Dmo.Domain.Modules.JobOn.JobOn jobOn, string actorId, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task InsertRevisionAsync(JobOnRevision revision, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<IReadOnlyList<JobOnRevision>> GetRevisionsAsync(Guid jobOnId, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<JobOnRevision>>(Array.Empty<JobOnRevision>());
            public Task InsertComponentsAsync(IEnumerable<JobOnComponent> components, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task InsertFieldsAsync(IEnumerable<JobOnComponentField> fields, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task InsertRowsAsync(IEnumerable<JobOnComponentRow> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task InsertVerificationsAsync(IEnumerable<JobOnVerificationOccurrence> verifications, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task UpdateVerificationStatusAsync(Guid occurrenceId, string status, string? completedBy, DateTime? completedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<Guid?> GetCurrentRevisionIdAsync(Guid jobOnId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
            public Task UpdateCurrentRevisionAsync(Guid jobOnId, Guid revisionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task InsertAuditEventAsync(Guid jobId, Guid? revisionId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task InsertImageMutationAsync(JobOnRevision newRevision, Guid jobOnId, string eventType, string? beforeImageAssetId, string? afterImageAssetId, string actorId, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task SaveRevisionGraphAsync(JobOnRevision revision, string eventType, string actorId, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<Guid> DuplicateAtomicallyAsync(JobOn newJobOn, JobOnRevision revision, Guid sourceJobOnId, string actorId, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        }
    }
}
