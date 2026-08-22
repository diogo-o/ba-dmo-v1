using System.Net;
using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// U-06 web authorization tests (Plan-V3 GLM-ACC-04/06, U-06 acceptance
/// scenarios 7/8/13/14/15, UD-16): capability-based page access, forged
/// mutation denial, audit capability separation, and the preserved Job On
/// landing for administrators. All collaborators are fakes — no live
/// Supabase/DB is touched.
/// </summary>
public class AdminWebAuthorizationTests : IClassFixture<AdminWebAuthorizationTests.AdminFixture>
{
    private static readonly Guid AdminAuthUserId =
        Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");

    private static readonly Guid OperatorAuthUserId =
        Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");

    private readonly AdminFixture _fixture;

    public AdminWebAuthorizationTests(AdminFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task Unauthenticated_AdminPage_RedirectsToLogin()
    {
        var client = _fixture.CreateTestClient();

        var response = await client.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task AuthenticatedWithoutAdminCapability_IsDenied_AndForgedPostWritesNothing()
    {
        _fixture.IdentityMode = AdminFixture.Mode.Operator;
        var client = _fixture.CreateTestClient();
        await LoginAsync(client, "operator@ba-dmo.example");

        // Page access denied by the admin.gerir policy.
        var page = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.StartsWith("/access-denied", page.Headers.Location!.PathAndQuery);

        // Forged mutation POST is denied server-side before any handler runs.
        var forged = await PostFormAsync(client, "/admin/users/edit?handler=Save", new()
        {
            ["id"] = "victim-actor",
            ["displayName"] = "Nome Forjado",
            ["templateId"] = "tpl-1",
            ["active"] = "true",
            ["version"] = DateTimeOffset.UtcNow.ToString("O")
        });
        Assert.Equal(HttpStatusCode.Redirect, forged.StatusCode);
        Assert.StartsWith("/access-denied", forged.Headers.Location!.PathAndQuery);
        Assert.Empty(_fixture.AdminRepository.Writes);
    }

    [Fact]
    public async Task AdminCapability_AllowsAdminPages_AndLoginLandsOnAdmin()
    {
        _fixture.IdentityMode = AdminFixture.Mode.Admin;
        var client = _fixture.CreateTestClient();

        // Owner decision: an Administrator lands on the single Admin page —
        // never on the Job On work landing (no universal jobon.view for admin).
        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "admin@ba-dmo.example",
            ["password"] = "correct"
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/admin", login.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/templates")).StatusCode);
    }

    [Fact]
    public async Task AdminWithOnlyAdminGerir_LoginRedirectsToAdmin()
    {
        // Owner rule with the exact bootstrap grants
        // [{"moduleId":"admin","capabilities":["admin.gerir"]}] (no audit,
        // no jobon): login resolves identity → session cookie → 302 /admin.
        _fixture.IdentityMode = AdminFixture.Mode.AdminWithoutAudit;
        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "admin@ba-dmo.example",
            ["password"] = "correct"
        });

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/admin", login.Headers.Location!.ToString());
        Assert.True(login.Headers.Contains("Set-Cookie"));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task AdminWithOnlyAdminGerir_DoesNotRequireJobOnAccess()
    {
        // admin.gerir grants /admin; jobon.view is NOT granted, so /jobon is
        // denied server-side (deep-link rule → /access-denied). No extra
        // permission is ever added to make the login work.
        _fixture.IdentityMode = AdminFixture.Mode.AdminWithoutAudit;
        var client = _fixture.CreateTestClient();
        await LoginAsync(client, "admin@ba-dmo.example");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin")).StatusCode);

        var jobon = await client.GetAsync("/jobon");
        Assert.Equal(HttpStatusCode.Redirect, jobon.StatusCode);
        Assert.StartsWith("/access-denied", jobon.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task AuditPage_RequiresAuditView()
    {
        // admin.gerir without audit.view → denied on the Auditoria tab
        // (scenario 17; distinct capabilities).
        _fixture.IdentityMode = AdminFixture.Mode.AdminWithoutAudit;
        var client = _fixture.CreateTestClient();
        await LoginAsync(client, "admin@ba-dmo.example");

        var denied = await client.GetAsync("/admin/audit");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/access-denied", denied.Headers.Location!.PathAndQuery);

        _fixture.IdentityMode = AdminFixture.Mode.Admin;
        var allowed = await client.GetAsync("/admin/audit");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await PostFormAsync(client, "/login", new()
        {
            ["email"] = email,
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

    public sealed class AdminFixture : WebApplicationFactory<Program>
    {
        public enum Mode
        {
            Admin,
            AdminWithoutAudit,
            Operator
        }

        public Mode IdentityMode { get; set; } = Mode.Admin;

        public FakeAdminWritesRepository AdminRepository { get; } = new();

        public void Reset()
        {
            IdentityMode = Mode.Admin;
            AdminRepository.Writes.Clear();
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
                ReplaceSingleton<IAdminRepository>(services, AdminRepository);
                ReplaceSingleton<IModuleCatalogMirrorRepository>(
                    services, new FakeMirrorRepository());
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
                string email, string password, CancellationToken cancellationToken = default)
            {
                var authUserId = email.StartsWith("admin@", StringComparison.Ordinal)
                    ? AdminAuthUserId
                    : OperatorAuthUserId;
                return Task.FromResult(Result<AuthUser, DomainError>.Success(
                    new AuthUser(authUserId, email)));
            }

            public Task<Result<bool, DomainError>> RequestPasswordResetAsync(
                Guid authUserId, CancellationToken cancellationToken = default) =>
                Task.FromResult(Result<bool, DomainError>.Success(true));
        }

        private sealed class FakeIdentityRepository(AdminFixture fixture) : IInternalUserRepository
        {
            public Task<InternalUserRecord?> FindByAuthUserIdAsync(
                Guid authUserId, CancellationToken cancellationToken = default)
            {
                if (authUserId == AdminAuthUserId)
                {
                    var capabilities = fixture.IdentityMode == Mode.AdminWithoutAudit
                        ? "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\"]}]"
                        : "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\",\"audit.view\",\"audit.export\"]}]";
                    return Task.FromResult<InternalUserRecord?>(new InternalUserRecord(
                        "admin-actor", AdminAuthUserId, "Administrador", null,
                        UserActive: true, TemplateId: "tpl-admin", TemplateName: "Admin",
                        TemplateActive: true, ModulesJson: capabilities));
                }

                if (authUserId == OperatorAuthUserId)
                    return Task.FromResult<InternalUserRecord?>(new InternalUserRecord(
                        "operator-actor", OperatorAuthUserId, "Operador", null,
                        UserActive: true, TemplateId: "tpl-op", TemplateName: "Operador",
                        TemplateActive: true,
                        ModulesJson: "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]"));

                return Task.FromResult<InternalUserRecord?>(null);
            }

            public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public Task CreateBootstrapAdminAsync(
                BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private sealed class FakeMirrorRepository : IModuleCatalogMirrorRepository
        {
            public Task<IReadOnlyList<ModuleCatalogMirrorRow>> GetAllAsync(
                CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ModuleCatalogMirrorRow>>(
                    Array.Empty<ModuleCatalogMirrorRow>());

            public Task UpsertAllAsync(
                IReadOnlyList<ModuleCatalogMirrorRow> rows,
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }
    }

    /// <summary>Tracks writes so forged-POST denial can be proven.</summary>
    public sealed class FakeAdminWritesRepository : IAdminRepository
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
            string actorId, Guid authUserId, string displayName, string? profileTitle,
            string templateId, bool active, DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken = default)
        {
            Writes.Add("create");
            return Task.CompletedTask;
        }

        public Task UpdateUserAsync(
            string actorId, string displayName, string? profileTitle,
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
            Writes.Add("change_template");
            return Task.FromResult(true);
        }

        public Task<bool> SetUserActiveAsync(
            string actorId, bool active, DateTimeOffset expectedUpdatedAt,
            DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
        {
            Writes.Add("set_active");
            return Task.FromResult(true);
        }

        public Task SetUserModulesOverrideAsync(
            string actorId, string modulesJson, DateTimeOffset expectedUpdatedAt,
            DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
        {
            Writes.Add("set_modules_override");
            return Task.CompletedTask;
        }

        public Task<int> CountActiveAdminsAsync(
            string? excludeActorId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<IReadOnlyList<AdminTemplateRow>> ListTemplatesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminTemplateRow>>(Array.Empty<AdminTemplateRow>());

        public Task<AdminTemplateRow?> GetTemplateAsync(
            string templateId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminTemplateRow?>(null);

        public Task CreateTemplateAsync(
            string templateId, string name, string modulesJson, DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken = default)
        {
            Writes.Add("create_template");
            return Task.CompletedTask;
        }

        public Task<bool> UpdateTemplateAsync(
            string templateId, string name, string modulesJson, bool active,
            DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Writes.Add("update_template");
            return Task.FromResult(true);
        }

        public Task InsertAuditEventAsync(
            AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AuditQueryResult> QueryAuditAsync(
            AuditQueryFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuditQueryResult(
                Array.Empty<AuditEventRow>(), 0, filter.Page, filter.PageSize));
    }
}
