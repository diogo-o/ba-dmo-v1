using System.Net;
using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// HI-1 regression: the admin management forms (Users/Create, Templates/Edit,
/// Applications/Index) must carry an antiforgery token. This suite runs the
/// REAL web pipeline with antiforgery ENFORCED (no IgnoreAntiforgeryToken
/// convention — the opposite of the other fixtures, which disable it for
/// scripted posts) and proves: (a) forms render the token; (b) a tokenless
/// browser-style POST is rejected with 400 and writes nothing; (c) a valid
/// token POST succeeds and the write is observed in the fakes; (d) a
/// cross-session token is rejected; (e) anonymous POSTs go to login and
/// non-admin sessions are denied by policy before any write.
/// </summary>
public class AdminFormAntiforgeryTests : IClassFixture<AdminFormAntiforgeryTests.AfFixture>
{
    private static readonly Guid AdminAuthUserId =
        Guid.Parse("cccccccc-1111-2222-3333-444444444444");

    private static readonly Guid OperatorAuthUserId =
        Guid.Parse("dddddddd-1111-2222-3333-444444444444");

    private readonly AfFixture _fixture;

    public AdminFormAntiforgeryTests(AfFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task AdminForms_RenderAnAntiForgeryToken()
    {
        _fixture.IdentityMode = AfFixture.Mode.Admin;
        var client = _fixture.CreateTestClient();
        await LoginAsync(client, "admin@ba-dmo.example");

        foreach (var url in new[] { "/admin/users/create", "/admin/templates/edit", "/admin/applications" })
        {
            var page = await client.GetAsync(url);
            Assert.True(
                page.StatusCode == HttpStatusCode.OK,
                $"GET {url} failed (got {page.StatusCode}).");
            var html = await page.Content.ReadAsStringAsync();
            Assert.True(
                html.Contains("name=\"__RequestVerificationToken\"", StringComparison.Ordinal),
                $"{url} must render an antiforgery token inside its form.");
        }
    }

    [Theory]
    [InlineData("/admin/users/create",
        "email=new-user@ba-dmo.example", "password=P@ssw0rd-123", "displayName=Novo")]
    [InlineData("/admin/templates/edit",
        "templateId=tpl-af-test", "name=Template AF", "active=true")]
    [InlineData("/admin/applications",
        "entries[0].ModuleId=boquilhas", "entries[0].DisplayOrder=1", "entries[0].Active=true")]
    public async Task TokenlessPost_IsRejected400_AndWritesNothing(
        string url, params string[] fieldPairs)
    {
        _fixture.IdentityMode = AfFixture.Mode.Admin;
        var client = _fixture.CreateTestClient();
        await LoginAsync(client, "admin@ba-dmo.example");

        var fields = fieldPairs
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1]);
        var response = await client.PostAsync(
            url, new FormUrlEncodedContent(fields));

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            $"POST {url} without token returned {response.StatusCode} (expected 400).");
        Assert.Empty(_fixture.Repository.Writes);
        Assert.Empty(_fixture.Mirror.Upserts);
    }

    [Fact]
    public async Task UserCreate_WithToken_CreatesTheUser()
    {
        _fixture.IdentityMode = AfFixture.Mode.Admin;
        var client = _fixture.CreateTestClient();
        await LoginAsync(client, "admin@ba-dmo.example");

        var page = await client.GetAsync("/admin/users/create");
        var html = await page.Content.ReadAsStringAsync();
        var token = ExtractToken(html)
            ?? throw new InvalidOperationException("Form did not render an antiforgery token.");

        var response = await client.PostAsync("/admin/users/create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["email"] = "new-user@ba-dmo.example",
            ["password"] = "P@ssw0rd-123",
            ["displayName"] = "Novo Utilizador",
            ["templateId"] = "tpl-admin",
            ["active"] = "true"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/users", response.Headers.Location!.ToString());
        Assert.Contains("create", _fixture.Repository.Writes);
    }

    [Fact]
    public async Task TemplateEdit_WithToken_CreatesTheTemplate()
    {
        _fixture.IdentityMode = AfFixture.Mode.Admin;
        var client = _fixture.CreateTestClient();
        await LoginAsync(client, "admin@ba-dmo.example");

        var page = await client.GetAsync("/admin/templates/edit");
        var html = await page.Content.ReadAsStringAsync();
        var token = ExtractToken(html)
            ?? throw new InvalidOperationException("Form did not render an antiforgery token.");

        var response = await client.PostAsync("/admin/templates/edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["templateId"] = "tpl-af-test",
            ["name"] = "Template AF",
            ["functionalProfile"] = "Operador / Controlador",
            ["active"] = "true",
            ["lines[0].ModuleId"] = "jobon",
            ["lines[0].DisplayName"] = "Job On",
            ["lines[0].Granted"] = "true"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/templates", response.Headers.Location!.ToString());
        Assert.Contains("create_template", _fixture.Repository.Writes);
    }

    [Fact]
    public async Task Applications_WithToken_SavesTheMirror()
    {
        _fixture.IdentityMode = AfFixture.Mode.Admin;
        var client = _fixture.CreateTestClient();
        await LoginAsync(client, "admin@ba-dmo.example");

        var page = await client.GetAsync("/admin/applications");
        var html = await page.Content.ReadAsStringAsync();
        var token = ExtractToken(html)
            ?? throw new InvalidOperationException("Form did not render an antiforgery token.");

        var response = await client.PostAsync("/admin/applications", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["entries[0].ModuleId"] = "boquilhas",
            ["entries[0].DisplayOrder"] = "1",
            ["entries[0].Active"] = "true"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/applications", response.Headers.Location!.ToString());
        var lastUpsert = _fixture.Mirror.Upserts.Last();
        Assert.Single(lastUpsert);
        Assert.Equal("boquilhas", lastUpsert[0].ModuleId);
    }

    [Fact]
    public async Task CrossSessionToken_IsRejected400()
    {
        _fixture.IdentityMode = AfFixture.Mode.Admin;
        var clientA = _fixture.CreateTestClient();
        var clientB = _fixture.CreateTestClient();
        await LoginAsync(clientA, "admin@ba-dmo.example");
        await LoginAsync(clientB, "admin@ba-dmo.example");

        var htmlA = await (await clientA.GetAsync("/admin/users/create")).Content.ReadAsStringAsync();
        var tokenA = ExtractToken(htmlA)
            ?? throw new InvalidOperationException("Form did not render an antiforgery token.");

        var response = await clientB.PostAsync("/admin/users/create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenA,
            ["email"] = "forged@ba-dmo.example",
            ["password"] = "P@ssw0rd-123",
            ["displayName"] = "Forged",
            ["templateId"] = "tpl-admin"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_fixture.Repository.Writes);
    }

    [Fact]
    public async Task AnonymousPost_RedirectsToLogin_AndWritesNothing()
    {
        var client = _fixture.CreateTestClient();

        var response = await client.PostAsync("/admin/users/create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "anon@ba-dmo.example",
            ["password"] = "P@ssw0rd-123",
            ["displayName"] = "Anon"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location!.PathAndQuery);
        Assert.Empty(_fixture.Repository.Writes);
    }

    [Fact]
    public async Task OperatorSession_Post_IsDeniedByPolicy_AndWritesNothing()
    {
        _fixture.IdentityMode = AfFixture.Mode.Operator;
        var client = _fixture.CreateTestClient();
        await LoginAsync(client, "operator@ba-dmo.example");

        var page = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.StartsWith("/access-denied", page.Headers.Location!.PathAndQuery);

        var createPage = await client.GetAsync("/admin/users/create");
        Assert.Equal(HttpStatusCode.Redirect, createPage.StatusCode);

        var response = await client.PostAsync("/admin/users/create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "x@ba-dmo.example",
            ["password"] = "P@ssw0rd-123",
            ["displayName"] = "X",
            ["templateId"] = "tpl-admin"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/access-denied", response.Headers.Location!.PathAndQuery);
        Assert.Empty(_fixture.Repository.Writes);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var form = await client.GetAsync("/login");
        var html = await form.Content.ReadAsStringAsync();
        var values = new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = "correct"
        };
        var token = ExtractToken(html)
            ?? throw new InvalidOperationException("Form did not render an antiforgery token.");
        values["__RequestVerificationToken"] = token;

        var response = await client.PostAsync("/login", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string? ExtractToken(string html)
    {
        var tokenStart = html.IndexOf("name=\"__RequestVerificationToken\"", StringComparison.Ordinal);
        if (tokenStart < 0)
            return null;
        var valueAttr = html.IndexOf("value=\"", tokenStart, StringComparison.Ordinal);
        if (valueAttr < 0)
            return null;
        var valueStart = valueAttr + "value=\"".Length;
        var valueEnd = html.IndexOf('"', valueStart);
        return html[valueStart..valueEnd];
    }

    public sealed class AfFixture : WebApplicationFactory<Program>
    {
        public enum Mode
        {
            Admin,
            Operator
        }

        public Mode IdentityMode { get; set; } = Mode.Admin;
        public FakeAdminRepository Repository { get; } = new();
        public FakeMirrorRepository Mirror { get; } = new();

        public void Reset()
        {
            IdentityMode = Mode.Admin;
            Repository.Writes.Clear();
            Mirror.Upserts.Clear();
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
                ReplaceSingleton<IInternalUserRepository>(services, new FakeIdentityRepository());
                ReplaceSingleton<IAdminRepository>(services, Repository);
                ReplaceSingleton<IModuleCatalogMirrorRepository>(services, Mirror);
                ReplaceSingleton<IAdminProvisioningAdapter>(services, new FakeProvisioningAdapter());
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
                string email, string password, CancellationToken cancellationToken = default)
            {
                var authUserId = email.StartsWith("admin@", StringComparison.Ordinal)
                    ? AdminAuthUserId
                    : OperatorAuthUserId;
                return Task.FromResult(Result<AuthUser, DomainError>.Success(
                    new AuthUser(authUserId, email)));
            }
        }

        private sealed class FakeIdentityRepository : IInternalUserRepository
        {
            public Task<InternalUserRecord?> FindByAuthUserIdAsync(
                Guid authUserId, CancellationToken cancellationToken = default)
            {
                if (authUserId == AdminAuthUserId)
                    return Task.FromResult<InternalUserRecord?>(new InternalUserRecord(
                        "admin-actor", AdminAuthUserId, "Administrador", "Admin",
                        UserActive: true, TemplateId: "tpl-admin", TemplateName: "Admin",
                        TemplateActive: true,
                        ModulesJson: "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\",\"audit.view\",\"audit.export\"]}]",
                        FunctionalProfile: "Admin"));

                if (authUserId == OperatorAuthUserId)
                    return Task.FromResult<InternalUserRecord?>(new InternalUserRecord(
                        "operator-actor", OperatorAuthUserId, "Operador", "Operador / Controlador",
                        UserActive: true, TemplateId: "tpl-op", TemplateName: "Operador",
                        TemplateActive: true,
                        ModulesJson: "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]",
                        FunctionalProfile: "Operador / Controlador"));

                return Task.FromResult<InternalUserRecord?>(null);
            }

            public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public Task CreateBootstrapAdminAsync(
                BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private sealed class FakeProvisioningAdapter : IAdminProvisioningAdapter
        {
            public Task<Result<AuthUser, DomainError>> EnsureAuthUserAsync(
                string email, string password, CancellationToken cancellationToken = default) =>
                Task.FromResult(Result<AuthUser, DomainError>.Success(
                    new AuthUser(Guid.NewGuid(), email)));

            public Task<Result<EnsuredAuthUser, DomainError>> EnsureAuthUserWithStatusAsync(
                string email, string password, CancellationToken cancellationToken = default) =>
                Task.FromResult(Result<EnsuredAuthUser, DomainError>.Success(
                    new EnsuredAuthUser(Guid.NewGuid(), email, AccountPreExisted: false)));

            public Task<Result<bool, DomainError>> RequestPasswordResetAsync(
                Guid authUserId, CancellationToken cancellationToken = default) =>
                Task.FromResult(Result<bool, DomainError>.Success(true));

            public Task<IReadOnlyDictionary<Guid, string>> GetUserEmailsAsync(
                IReadOnlyCollection<Guid> authUserIds,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                    new Dictionary<Guid, string>());
            }
        }
    }

    public sealed class FakeAdminRepository : IAdminRepository
    {
        public List<string> Writes { get; } = [];

        public Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(
            string? search, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminUserRow>>(Array.Empty<AdminUserRow>());

        public Task<AdminUserRow?> GetUserAsync(
            string actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminUserRow?>(null);

        public Task<bool> AuthUserIdAlreadyRegisteredAsync(
            Guid authUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task CreateInternalUserAsync(
            string actorId, Guid authUserId, string displayName,
            string templateId, bool active, DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken = default)
        {
            Writes.Add("create");
            return Task.CompletedTask;
        }

        public Task UpdateUserAsync(
            string actorId, string displayName,
            DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Writes.Add("update");
            return Task.CompletedTask;
        }

        public Task<bool> ChangeUserTemplateAsync(
            string actorId, string templateId, DateTimeOffset expectedUpdatedAt,
            DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
        {
            Writes.Add("change_templates");
            return Task.FromResult(true);
        }

        public Task<bool> SetUserActiveAsync(
            string actorId, bool active, DateTimeOffset expectedUpdatedAt,
            DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
        {
            Writes.Add("set_active");
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<AdminTemplateRow>> ListTemplatesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminTemplateRow>>(
            [
                new AdminTemplateRow(
                    "tpl-admin", "Admin",
                    "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\"]}]",
                    Active: true, DateTimeOffset.UnixEpoch)
            ]);

        public Task<AdminTemplateRow?> GetTemplateAsync(
            string templateId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminTemplateRow?>(
                templateId == "tpl-admin"
                    ? new AdminTemplateRow(
                        "tpl-admin", "Admin",
                        "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\"]}]",
                        Active: true, DateTimeOffset.UnixEpoch)
                    : null);

        public Task CreateTemplateAsync(
            string templateId, string name, string modulesJson, string functionalProfile,
            DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default)
        {
            Writes.Add("create_template");
            return Task.CompletedTask;
        }

        public Task<bool> UpdateTemplateAsync(
            string templateId, string name, string modulesJson, bool active, string functionalProfile,
            DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Writes.Add("update_template");
            return Task.FromResult(true);
        }

        public Task<string?> GetTemplateFunctionalProfileAsync(
            string templateId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(
                templateId == "tpl-admin" ? "Admin" : "Operador / Controlador");

        public Task<IReadOnlyDictionary<string, string>> ListTemplateFunctionalProfilesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["tpl-admin"] = "Admin"
                });

        public Task InsertAuditEventAsync(
            AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AuditQueryResult> QueryAuditAsync(
            AuditQueryFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuditQueryResult(
                Array.Empty<AuditEventRow>(), 0, filter.Page, filter.PageSize));
    }

    public sealed class FakeMirrorRepository : IModuleCatalogMirrorRepository
    {
        public List<IReadOnlyList<ModuleCatalogMirrorRow>> Upserts { get; } = [];

        public Task<IReadOnlyList<ModuleCatalogMirrorRow>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ModuleCatalogMirrorRow>>(
                Array.Empty<ModuleCatalogMirrorRow>());

        public Task UpsertAllAsync(
            IReadOnlyList<ModuleCatalogMirrorRow> rows,
            CancellationToken cancellationToken = default)
        {
            Upserts.Add(rows);
            return Task.CompletedTask;
        }
    }
}
