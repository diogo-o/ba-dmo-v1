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
/// HI-3 regression: the user-list "Reset" button must route through the SAME
/// working service path as the Edit page (AdminUserService
/// .RequestPasswordResetAsync → provisioning generate_link +
/// password_reset_request audit row) and surface feedback — not silently
/// redirect (the old dead stub). Also pins the ME-6 column label.
/// </summary>
public class AdminUserListResetTests : IClassFixture<AdminUserListResetTests.ResetFixture>
{
    private static readonly Guid AdminAuthUserId =
        Guid.Parse("eeeeeeee-1111-2222-3333-444444444444");

    private static readonly Guid TargetAuthUserId =
        Guid.Parse("ffffffff-2222-3333-4444-555555555555");

    private const string TargetActorId = "ffffffff-2222-3333-4444-555555555555";

    private readonly ResetFixture _fixture;

    public AdminUserListResetTests(ResetFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task ListPageReset_UsesTheExistingServicePath_AuditsAndShowsBanner()
    {
        var client = _fixture.CreateTestClient();
        await LoginAsync(client);

        var page = await client.GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains(TargetActorId, html, StringComparison.Ordinal); // row visible
        // The Utilizadores table shows the real Auth email (owner req #1); the
        // raw "Auth ID" column was removed by the owner-approved redesign, so
        // anchor on the Email column header instead of the old "<th>Auth ID</th>".
        Assert.Contains("<th>Email</th>", html, StringComparison.Ordinal);
        var token = ExtractToken(html);

        var response = await client.PostAsync("/admin/users?handler=ResetPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = TargetActorId
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // re-renders the list
        var body = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Reset de palavra-passe iniciado.", body);

        // The SAME path as the Edit page: provisioning called with the target
        // auth uuid, and the audit row recorded by the service.
        Assert.Single(_fixture.Provisioning.ResetRequests);
        Assert.Equal(TargetAuthUserId, _fixture.Provisioning.ResetRequests[0]);
        var audit = _fixture.Repository.Audits.Single(
            a => a.ActionCode == "password_reset_request");
        Assert.Equal("succeeded", audit.Result);
        Assert.Equal(TargetActorId, audit.EntityId);
    }

    [Fact]
    public async Task ListPageReset_UnknownUser_ShowsError_NoProvisioningNoAudit()
    {
        var client = _fixture.CreateTestClient();
        await LoginAsync(client);

        var html = await (await client.GetAsync("/admin/users")).Content.ReadAsStringAsync();
        var token = ExtractToken(html);

        var response = await client.PostAsync("/admin/users?handler=ResetPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = "00000000-0000-0000-0000-000000000000"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Utilizador interno não encontrado.", body);
        Assert.DoesNotContain("Reset de palavra-passe iniciado.", body);
        Assert.Empty(_fixture.Provisioning.ResetRequests);
        Assert.Empty(_fixture.Repository.Audits);
    }

    [Fact]
    public async Task EditPageReset_StillUsesTheSamePath()
    {
        // Regression guard: both entry points converge on one service path.
        var client = _fixture.CreateTestClient();
        await LoginAsync(client);

        var edit = await client.GetAsync("/admin/users/edit?id=" + TargetActorId);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var html = await edit.Content.ReadAsStringAsync();
        var token = ExtractToken(html);

        var response = await client.PostAsync(
            "/admin/users/edit?handler=ResetPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = TargetActorId
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Reset de palavra-passe iniciado.", body);
        Assert.Single(_fixture.Provisioning.ResetRequests);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var form = await client.GetAsync("/login");
        var html = await form.Content.ReadAsStringAsync();
        var values = new Dictionary<string, string>
        {
            ["email"] = "admin@ba-dmo.example",
            ["password"] = "correct"
        };
        var token = ExtractToken(html);
        if (token is not null)
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

    public sealed class ResetFixture : WebApplicationFactory<Program>
    {
        public RecordingProvisioningAdapter Provisioning { get; } = new();

        public RecordingAdminRepository Repository { get; } = new();

        public void Reset()
        {
            Provisioning.ResetRequests.Clear();
            Repository.Audits.Clear();
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
                ReplaceSingleton<IAdminProvisioningAdapter>(services, Provisioning);
                ReplaceSingleton<IModuleCatalogMirrorRepository>(services, new NoopMirror());
                services.Configure<Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions>(
                    options => options.Conventions.ConfigureFilter(
                        new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute()));
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
                    new AuthUser(AdminAuthUserId, email)));
        }

        private sealed class FakeIdentityRepository : IInternalUserRepository
        {
            public Task<InternalUserRecord?> FindByAuthUserIdAsync(
                Guid authUserId, CancellationToken cancellationToken = default) =>
                authUserId == AdminAuthUserId
                    ? Task.FromResult<InternalUserRecord?>(new InternalUserRecord(
                        "admin-actor", AdminAuthUserId, "Administrador", null,
                        UserActive: true, TemplateId: "tpl-admin", TemplateName: "Admin",
                        TemplateActive: true,
                        ModulesJson: "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\",\"audit.view\"]}]"))
                    : Task.FromResult<InternalUserRecord?>(null);

            public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(true);

            public Task CreateBootstrapAdminAsync(
                BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private sealed class NoopMirror : IModuleCatalogMirrorRepository
        {
            public Task<IReadOnlyList<ModuleCatalogMirrorRow>> GetAllAsync(
                CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<ModuleCatalogMirrorRow>>(
                    Array.Empty<ModuleCatalogMirrorRow>());

            public Task UpsertAllAsync(
                IReadOnlyList<ModuleCatalogMirrorRow> rows,
                CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    public sealed class RecordingProvisioningAdapter : IAdminProvisioningAdapter
    {
        public List<Guid> ResetRequests { get; } = [];

        public Task<Result<AuthUser, DomainError>> EnsureAuthUserAsync(
            string email, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AuthUser, DomainError>.Success(
                new AuthUser(Guid.NewGuid(), email)));

        public Task<Result<EnsuredAuthUser, DomainError>> EnsureAuthUserWithStatusAsync(
            string email, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<EnsuredAuthUser, DomainError>.Success(
                new EnsuredAuthUser(Guid.NewGuid(), email, AccountPreExisted: false)));

        public Task<Result<bool, DomainError>> RequestPasswordResetAsync(
            Guid authUserId, CancellationToken cancellationToken = default)
        {
            ResetRequests.Add(authUserId);
            return Task.FromResult(Result<bool, DomainError>.Success(true));
        }

        public Task<IReadOnlyDictionary<Guid, string>> GetUserEmailsAsync(
            IReadOnlyCollection<Guid> authUserIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                new Dictionary<Guid, string>());
        }
    }

    public sealed class RecordingAdminRepository : IAdminRepository
    {
        public List<AuditEntry> Audits { get; } = [];

        private static readonly AdminUserRow Target = new(
            TargetActorId, TargetAuthUserId, "Alvo", "Técnico",
            "tpl-op", Active: true, DateTimeOffset.UnixEpoch);

        public Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(
            string? search, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminUserRow>>([Target]);

        public Task<AdminUserRow?> GetUserAsync(
            string actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminUserRow?>(actorId == TargetActorId ? Target : null);

        public Task<bool> AuthUserIdAlreadyRegisteredAsync(
            Guid authUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task CreateInternalUserAsync(
            string actorId, Guid authUserId, string displayName, string? profileTitle,
            string templateId, bool active, DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateUserAsync(
            string actorId, string displayName, string? profileTitle,
            DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ChangeUserTemplateAsync(
            string actorId, string templateId, DateTimeOffset expectedUpdatedAt,
            DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> SetUserActiveAsync(
            string actorId, bool active, DateTimeOffset expectedUpdatedAt,
            DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task SetUserModulesOverrideAsync(
            string actorId, string modulesJson, DateTimeOffset expectedUpdatedAt,
            DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<int> CountActiveAdminsAsync(
            string? excludeActorId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<IReadOnlyList<AdminTemplateRow>> ListTemplatesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminTemplateRow>>([new AdminTemplateRow(
                "tpl-op", "Operador", "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]",
                Active: true, DateTimeOffset.UnixEpoch)]);

        public Task<AdminTemplateRow?> GetTemplateAsync(
            string templateId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminTemplateRow?>(
                templateId == "tpl-op"
                    ? new AdminTemplateRow(
                        "tpl-op", "Operador", "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]",
                        Active: true, DateTimeOffset.UnixEpoch)
                    : null);

        public Task CreateTemplateAsync(
            string templateId, string name, string modulesJson, DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> UpdateTemplateAsync(
            string templateId, string name, string modulesJson, bool active,
            DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task InsertAuditEventAsync(
            AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Audits.Add(entry);
            return Task.CompletedTask;
        }

        public Task<AuditQueryResult> QueryAuditAsync(
            AuditQueryFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuditQueryResult(
                Array.Empty<AuditEventRow>(), 0, filter.Page, filter.PageSize));
    }
}
