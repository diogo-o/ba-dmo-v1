using System.Net;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.Identity;

/// <summary>
/// HI-2 landing-mapping regression: an ambiguous internal identity (more
/// than one row for the same auth_user_id) must land on the PLAIN
/// /no-access safe state — never on /no-access?indisponivel=1, which is
/// reserved for genuine backend unavailability. A real repository failure
/// must keep landing on indisponivel=1 (no classification regression).
/// </summary>
public class IdentityAmbiguityLandingTests : IClassFixture<IdentityAmbiguityLandingTests.AmbiguityFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("33333333-2222-3333-4444-555555555555");

    private readonly AmbiguityFixture _fixture;

    public IdentityAmbiguityLandingTests(AmbiguityFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public async Task AmbiguousIdentity_LoginLandsOnPlainNoAccess_NeverIndisponivel()
    {
        _fixture.Repository.ThrowAmbiguous = true;

        var client = _fixture.CreateTestClient();
        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = "correct"
        });

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/no-access", login.Headers.Location!.ToString());
        Assert.True(login.Headers.Contains("Set-Cookie")); // auth succeeded

        var page = await client.GetAsync("/no-access");
        var body = System.Net.WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        // The "backend unavailable" text must NOT appear for a data-integrity
        // condition — that text claims the mapping is intact but unloadable.
        Assert.DoesNotContain("Não foi possível carregar o acesso à aplicação neste momento.", body);
        Assert.DoesNotContain("indisponivel=1", body, StringComparison.Ordinal);
        // Positive: the plain "no modules" safe state is what rendered.
        Assert.Contains("não tem módulos atribuídos", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenuineRepositoryFailure_StillLandsOnIndisponivel()
    {
        // Control: a real DB failure keeps its distinct transient state.
        _fixture.Repository.ThrowOnFind = true;

        var client = _fixture.CreateTestClient();
        var login = await PostFormAsync(client, "/login", new()
        {
            ["email"] = "user@ba-dmo.example",
            ["password"] = "correct"
        });

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/no-access?indisponivel=1", login.Headers.Location!.ToString());

        var page = await client.GetAsync("/no-access?indisponivel=1");
        var body = System.Net.WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        Assert.Contains("Serviço temporariamente indisponível", body, StringComparison.Ordinal);
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
                var valueStart = valueAttr + "value=\"".Length;
                var valueEnd = html.IndexOf('"', valueStart);
                values["__RequestVerificationToken"] = html[valueStart..valueEnd];
            }
        }

        return await client.PostAsync(url, new FormUrlEncodedContent(values));
    }

    public sealed class AmbiguityFixture : WebApplicationFactory<Program>
    {
        public FakeIdentityRepository Repository { get; } = new();

        public void Reset()
        {
            Repository.ThrowAmbiguous = false;
            Repository.ThrowOnFind = false;
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

        // public: exposed through the public Repository property (CS0053 fix).
        public sealed class FakeIdentityRepository : IInternalUserRepository
        {
            public bool ThrowAmbiguous { get; set; }

            public bool ThrowOnFind { get; set; }

            public Task<InternalUserRecord?> FindByAuthUserIdAsync(
                Guid authUserId, CancellationToken cancellationToken = default)
            {
                if (ThrowAmbiguous)
                    throw new AmbiguousIdentityException(authUserId);
                if (ThrowOnFind)
                    throw new InvalidOperationException("Simulated database failure.");
                return Task.FromResult<InternalUserRecord?>(null);
            }

            public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(false);

            public Task CreateBootstrapAdminAsync(
                BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }
    }
}
