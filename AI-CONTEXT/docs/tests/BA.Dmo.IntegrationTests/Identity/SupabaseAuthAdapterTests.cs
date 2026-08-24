using System.Net;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Infrastructure.Auth;

namespace BA.Dmo.IntegrationTests.Identity;

/// <summary>
/// U-05 Supabase auth adapter tests (Plan-V3 GLM-ARCH-14, PV-06): normal
/// pipeline uses only the anon endpoint + the user's own credentials;
/// failures are generic and fail closed; secrets never leak.
/// </summary>
public class SupabaseAuthAdapterTests
{
    private const string SupabaseUrl = "https://project.supabase.example";
    private const string AnonKey = "anon-test-key";

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task ValidCredentials_ReturnTheAuthUserId()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json(
            "{\"access_token\":\"token\",\"user\":{\"id\":\"11111111-2222-3333-4444-555555555555\"}}"));
        var adapter = new SupabaseAuthAdapter(new HttpClient(handler), SupabaseUrl, AnonKey);

        var result = await adapter.SignInWithPasswordAsync("user@ba-dmo.example", "password");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            result.Value.AuthUserId);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            $"{SupabaseUrl}/auth/v1/token?grant_type=password",
            request.RequestUri!.ToString());
        Assert.Equal(AnonKey, request.Headers.GetValues("apikey").Single());
        // The normal pipeline never sends a service-role credential.
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task InvalidCredentials_FailWithProviderReason_ForServerSideLogging()
    {
        // The domain error carries the machine-readable provider reason so
        // the Web layer can LOG the concrete cause (the browser only ever
        // sees the generic message — asserted in WebAuthSessionTests).
        // Invariants that must hold: no email, no password, no apikey in
        // the error.
        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json(
            "{\"error\":\"invalid_grant\",\"error_description\":\"user not found\"}",
            HttpStatusCode.BadRequest));
        var adapter = new SupabaseAuthAdapter(new HttpClient(handler), SupabaseUrl, AnonKey);

        var result = await adapter.SignInWithPasswordAsync("user@ba-dmo.example", "wrong");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Unauthorized, result.Error.Category);
        Assert.Equal("INVALID_CREDENTIALS", result.Error.Code);
        Assert.Contains("invalid_grant", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("user not found", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("user@ba-dmo.example", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(AnonKey, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RateLimited429_IsProviderUnavailable_NeverInvalidCredentials()
    {
        // ME-1: a 429 must never tell the user their password is wrong — it
        // is a transient provider condition (the Web layer renders it as
        // "temporariamente indisponível").
        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json(
            "{\"error\":\"rate_limit\",\"error_description\":\"too many requests\"}",
            HttpStatusCode.TooManyRequests));
        var adapter = new SupabaseAuthAdapter(new HttpClient(handler), SupabaseUrl, AnonKey);

        var result = await adapter.SignInWithPasswordAsync("user@ba-dmo.example", "password");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.BackendUnavailable, result.Error.Category);
        Assert.Equal("AUTH_PROVIDER_UNAVAILABLE", result.Error.Code);
        Assert.DoesNotContain("INVALID_CREDENTIALS", result.Error.Code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiKeyRejected401_IsConfigSuspect_NeverInvalidCredentials()
    {
        // ME-1: a well-formed but wrong/rotated apikey makes GoTrue answer
        // 401 — that is a configuration problem (operator must fix the key),
        // not a user credential failure.
        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json(
            "{\"error\":\"api_error\",\"error_description\":\"invalid API key\"}",
            HttpStatusCode.Unauthorized));
        var adapter = new SupabaseAuthAdapter(new HttpClient(handler), SupabaseUrl, AnonKey);

        var result = await adapter.SignInWithPasswordAsync("user@ba-dmo.example", "password");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.BackendUnavailable, result.Error.Category);
        Assert.Equal("AUTH_PROVIDER_MISCONFIGURED", result.Error.Code);
        Assert.DoesNotContain(AnonKey, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiKeyRejected403_IsConfigSuspect_NeverInvalidCredentials()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json(
            "{\"error\":\"forbidden\",\"error_description\":\"JWT verification failed\"}",
            HttpStatusCode.Forbidden));
        var adapter = new SupabaseAuthAdapter(new HttpClient(handler), SupabaseUrl, AnonKey);

        var result = await adapter.SignInWithPasswordAsync("user@ba-dmo.example", "password");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.BackendUnavailable, result.Error.Category);
        Assert.Equal("AUTH_PROVIDER_MISCONFIGURED", result.Error.Code);
    }

    [Fact]
    public async Task ServerError503_IsProviderUnavailable()
    {
        // M-4: 5xx stays a provider-side condition (previously untested).
        var handler = new FakeHttpMessageHandler();
        handler.Responders.Enqueue(_ => Json(
            "{\"error\":\"server_error\"}", HttpStatusCode.ServiceUnavailable));
        var adapter = new SupabaseAuthAdapter(new HttpClient(handler), SupabaseUrl, AnonKey);

        var result = await adapter.SignInWithPasswordAsync("user@ba-dmo.example", "password");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.BackendUnavailable, result.Error.Category);
        Assert.Equal("AUTH_PROVIDER_UNAVAILABLE", result.Error.Code);
    }

    [Fact]
    public async Task NetworkFailure_FailsClosed_AsBackendUnavailable()
    {
        var handler = new FakeHttpMessageHandler { Throw = new HttpRequestException("down") };
        var adapter = new SupabaseAuthAdapter(new HttpClient(handler), SupabaseUrl, AnonKey);

        var result = await adapter.SignInWithPasswordAsync("user@ba-dmo.example", "password");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.BackendUnavailable, result.Error.Category);
    }

    [Fact]
    public async Task UnconfiguredAdapter_FailsClosed_WithoutHttpCalls()
    {
        var handler = new FakeHttpMessageHandler();
        var adapter = new SupabaseAuthAdapter(new HttpClient(handler), null, null);

        var result = await adapter.SignInWithPasswordAsync("user@ba-dmo.example", "password");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.BackendUnavailable, result.Error.Category);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task BlankCredentials_FailWithoutHttpCalls()
    {
        var handler = new FakeHttpMessageHandler();
        var adapter = new SupabaseAuthAdapter(new HttpClient(handler), SupabaseUrl, AnonKey);

        var result = await adapter.SignInWithPasswordAsync(" ", " ");

        Assert.True(result.IsFailure);
        Assert.Equal("INVALID_CREDENTIALS", result.Error.Code);
        Assert.Empty(handler.Requests);
    }
}
