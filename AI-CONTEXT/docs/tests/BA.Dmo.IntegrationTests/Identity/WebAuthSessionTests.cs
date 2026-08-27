using System.Net;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.Identity;

/// <summary>
/// U-05 session/authentication flow tests (Plan-V3 GLM-ACC-01, 05_SHL
/// §5–6): login, logout, protected pages, safe states, Job On landing.
/// Runs against the real web pipeline with fakes for the Supabase adapter
/// and the identity repository — no live Supabase/DB is used (GLM-ARCH-18).
/// </summary>
public class WebAuthSessionTests : IClassFixture<WebAuthSessionTests.AuthTestFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly AuthTestFixture _fixture;

    public WebAuthSessionTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task UnauthenticatedRequest_IsRedirectedToLogin()
    {
        var client = _fixture.CreateTestClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task LoginPage_IsPublic()
    {
        var client = _fixture.CreateTestClient();

        var response = await client.GetAsync("/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SafeStatePages_ArePublic()
    {
        var client = _fixture.CreateTestClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/no-access")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/access-denied")).StatusCode);
    }

    [Fact]
    public async Task SuccessfulLogin_RedirectsToTheJobOnLanding_WithSessionCookie()
    {
        // Scenario 1: the landing is the first page of the user's effective
        // access surface — Job On for functional users (universal jobon.view).
        // (Admins, which hold no jobon.view by owner decision, land on
        // /admin — covered by AdminWebAuthorizationTests.)
        _fixture.Repository.User = _fixture.ValidUser();

        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = "correct"
        });

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/jobon", login.Headers.Location!.ToString());
        Assert.True(login.Headers.Contains("Set-Cookie"));

        // The session reaches the protected surface: "/" resolves the fixed
        // global landing (05_SHL section 5: "/" redirects to landing).
        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Equal("/jobon", home.Headers.Location!.ToString());
    }

    [Fact]
    public async Task ExternalOrSuppliedReturnUrl_CanNeverOverrideTrustedRouting()
    {
        // Requirement: an external or client-supplied ReturnUrl must NEVER
        // override the trusted post-login destination. The Login handler
        // binds no ReturnUrl at all — the destination is always resolved from
        // the internal identity. A supplied ReturnUrl (query or form) is
        // therefore ignored: an open-redirect surface must not exist.
        _fixture.Repository.User = _fixture.ValidUser();
        var client = _fixture.CreateTestClient();

        // Same POST content, but with an attacker-supplied ReturnUrl present
        // in BOTH the query string and the form body. Two separate posts so a
        // binding on either route is exercised.
        foreach (var url in new[]
        {
            "/login?ReturnUrl=https://evil.example/phish",
            "/login"
        })
        {
            var login = await PostFormAsync(client, url, new()
            {
                ["email"] = "user@ba-dmo.example",
                ["password"] = "correct",
                ["ReturnUrl"] = "https://evil.example/phish"
            });

            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            // Trusted routing wins: the canonical Job On landing, never the
            // supplied external address and never any relative open redirect.
            Assert.Equal("/jobon", login.Headers.Location!.ToString());
            Assert.DoesNotContain("evil.example", login.Headers.Location!.ToString(), StringComparison.Ordinal);
        }

        // After login the protected surface still routes to the canonical
        // landing — the supplied ReturnUrl was never honored.
        var home = await client.GetAsync("/");
        Assert.Equal("/jobon", home.Headers.Location!.ToString());
    }

    [Fact]
    public async Task InvalidCredentials_ShowGenericError_AndNoSession()
    {
        _fixture.AuthAdapter.Mode = FakeAuthAdapter.AuthMode.InvalidCredentials;

        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = "wrong"
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode); // stays on the form
        var body = System.Net.WebUtility.HtmlDecode(await login.Content.ReadAsStringAsync());
        Assert.Contains("Credenciais inválidas.", body);

        // No session was created: protected pages still redirect to login.
        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.StartsWith("/login", home.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task AuthenticatedWithoutInternalMapping_GoesToNoAccessSafeState()
    {
        // GLM-ACC-01.6: INTERNAL_USER_INACTIVE → safe session without access.
        _fixture.Repository.User = null;

        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = "correct"
        });

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/no-access", login.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AuthenticatedWithInactiveTemplate_GoesToNoAccessSafeState()
    {
        _fixture.Repository.User = _fixture.ValidUser() with { TemplateActive = false };

        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = "correct"
        });

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/no-access", login.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Logout_ClearsTheSession()
    {
        _fixture.Repository.User = _fixture.ValidUser();
        var client = _fixture.CreateTestClient();

        await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = "correct"
        });
        var logout = await PostFormAsync(client, "/logout", []);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/login", logout.Headers.Location!.ToString());

        // Session gone: protected surface redirects again.
        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.StartsWith("/login", home.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task ProviderFailure_ShowsGenericError_NoSession()
    {
        // A provider outage must NOT masquerade as bad credentials — it is
        // shown as a temporary unavailability (no session, no disclosure).
        _fixture.AuthAdapter.Mode = FakeAuthAdapter.AuthMode.ProviderUnavailable;

        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = "anything"
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = System.Net.WebUtility.HtmlDecode(await login.Content.ReadAsStringAsync());
        Assert.Contains("Autenticação temporariamente indisponível.", body);
        Assert.DoesNotContain("Credenciais inválidas.", body);
    }

    [Fact]
    public async Task FailedLogin_PreservesSubmittedEmail_AndDoesNotRenderPassword()
    {
        // After a rejected credential the submitted email must remain in the
        // field (bound model re-render); the password must NOT be re-shown,
        // stored or echoed anywhere in the HTML.
        _fixture.AuthAdapter.Mode = FakeAuthAdapter.AuthMode.InvalidCredentials;

        var client = _fixture.CreateTestClient();

        var resp = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = "S3cret-wrong-pass"
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = System.Net.WebUtility.HtmlDecode(await resp.Content.ReadAsStringAsync());
        Assert.Contains("Credenciais inválidas.", body);
        // Email preserved in the form field.
        Assert.Contains("value=\"user@ba-dmo.example\"", body);
        // Password never rendered, bound or echoed.
        Assert.DoesNotContain("S3cret-wrong-pass", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderUnavailable_PreservesSubmittedEmail()
    {
        // The login page re-renders (not a redirect) on provider outage —
        // the submitted email must survive that re-render as well.
        _fixture.AuthAdapter.Mode = FakeAuthAdapter.AuthMode.ProviderUnavailable;

        var client = _fixture.CreateTestClient();

        var resp = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = "anything"
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = System.Net.WebUtility.HtmlDecode(await resp.Content.ReadAsStringAsync());
        Assert.Contains("Autenticação temporariamente indisponível.", body);
        Assert.Contains("value=\"user@ba-dmo.example\"", body);
    }

    [Fact]
    public async Task BlankPassword_ValidationFailure_PreservesSubmittedEmail()
    {
        // Validation failure (empty password) re-renders the page; the email
        // the user already typed must remain.
        var client = _fixture.CreateTestClient();

        var resp = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = ""
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = System.Net.WebUtility.HtmlDecode(await resp.Content.ReadAsStringAsync());
        Assert.Contains("Credenciais inválidas.", body);
        Assert.Contains("value=\"user@ba-dmo.example\"", body);
    }

    [Fact]
    public async Task AuthenticatedUser_WhenIdentityDatabaseUnavailable_ShowsBackendUnavailableState()
    {
        // Supabase accepted the credentials, but the identity backend is
        // unavailable (repository throws): the user must NOT see "no modules
        // assigned" (that would claim their mapping is missing), no fallback
        // access may be granted, and no backend detail may leak.
        _fixture.Repository.User = _fixture.ValidUser();
        _fixture.Repository.ThrowOnFind = true;

        var client = _fixture.CreateTestClient();

        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = "correct"
        });

        // Authentication succeeded (cookie set) but resolution failed:
        // the distinct transient safe state, not the "no modules" one.
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/no-access?indisponivel=1", login.Headers.Location!.ToString());
        Assert.True(login.Headers.Contains("Set-Cookie"));

        var page = await client.GetAsync(login.Headers.Location!.ToString());
        var body = System.Net.WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Não foi possível carregar o acesso à aplicação neste momento.", body);
        Assert.DoesNotContain("não tem módulos atribuídos", body);
        // No leak of backend details (connection info, SQL, stack traces).
        Assert.DoesNotContain("Host=", body, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);

        // Zero access granted by fallback: every module route is denied.
        var jobon = await client.GetAsync("/jobon");
        Assert.Equal(HttpStatusCode.Redirect, jobon.StatusCode);
        Assert.StartsWith("/access-denied", jobon.Headers.Location!.PathAndQuery);
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

    /// <summary>
    /// Test host with fakes for the provider adapter and the identity
    /// repository; anti-forgery disabled for scripted form posts only.
    /// </summary>
    public sealed class AuthTestFixture : WebApplicationFactory<Program>
    {
        public FakeAuthAdapter AuthAdapter { get; } = new();

        public FakeIdentityRepository Repository { get; } = new();

        public void Reset()
        {
            AuthAdapter.Mode = FakeAuthAdapter.AuthMode.Success;
            Repository.User = null;
            Repository.ThrowOnFind = false;
        }

        public InternalUserRecord ValidUser() => new(
            ActorId: "actor-1",
            AuthUserId: AuthUserId,
            DisplayName: "Utilizador Um",
            ProfileTitle: "Operador / Controlador",
            UserActive: true,
            TemplateId: "tpl-1",
            TemplateName: "Template 1",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]",
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
                ReplaceSingleton<ISupabaseAuthAdapter>(services, AuthAdapter);
                ReplaceSingleton<IInternalUserRepository>(services, Repository);
                services.Configure<Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions>(
                    options => options.Conventions.ConfigureFilter(
                        new IgnoreAntiforgeryTokenAttribute()));
            });
        }

        private static void ReplaceSingleton<TService>(
            IServiceCollection services, TService implementation)
            where TService : class
        {
            var descriptors = services.Where(d => d.ServiceType == typeof(TService)).ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);
            services.AddSingleton(implementation);
        }
    }

    public sealed class FakeAuthAdapter : ISupabaseAuthAdapter
    {
        public enum AuthMode
        {
            Success,
            InvalidCredentials,
            ProviderUnavailable
        }

        public AuthMode Mode { get; set; } = AuthMode.Success;

        public Task<Result<AuthUser, DomainError>> SignInWithPasswordAsync(
            string email, string password, CancellationToken cancellationToken = default) =>
            Mode switch
            {
                AuthMode.Success => Task.FromResult(Result<AuthUser, DomainError>.Success(
                    new AuthUser(AuthUserId, email))),
                AuthMode.ProviderUnavailable => Task.FromResult(
                    Result<AuthUser, DomainError>.Failure(DomainError.BackendUnavailable(
                        "AUTH_PROVIDER_UNAVAILABLE", "Provider down."))),
                _ => Task.FromResult(Result<AuthUser, DomainError>.Failure(
                    DomainError.Unauthorized("INVALID_CREDENTIALS", "Credenciais inválidas.")))
            };
    }

    public sealed class FakeIdentityRepository : IInternalUserRepository
    {
        public InternalUserRecord? User { get; set; }

        public bool ThrowOnFind { get; set; }

        public Task<InternalUserRecord?> FindByAuthUserIdAsync(
            Guid authUserId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnFind)
                throw new InvalidOperationException("Simulated database failure.");
            return Task.FromResult(User);
        }

        public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task CreateBootstrapAdminAsync(
            BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
