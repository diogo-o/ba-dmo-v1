using System.Net.Http.Json;
using System.Text.Json;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Infrastructure.Auth;

/// <summary>
/// Supabase Auth adapter via direct server-side REST (GoTrue). Plan-V3
/// GLM-ARCH-14/PV-06 leaves the concrete provider open: direct REST keeps
/// the runtime dependency-free of provider SDKs and of service_role
/// (PV-07 — the normal request pipeline uses only the anon endpoint and the
/// user's own credentials). Provider types never leave this class.
/// </summary>
public sealed class SupabaseAuthAdapter : ISupabaseAuthAdapter
{
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _anonKey;

    public SupabaseAuthAdapter(HttpClient httpClient, string? supabaseUrl, string? anonKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _supabaseUrl = string.IsNullOrWhiteSpace(supabaseUrl) ? null : supabaseUrl.TrimEnd('/');
        _anonKey = string.IsNullOrWhiteSpace(anonKey) ? null : anonKey;
    }

    public async Task<Result<AuthUser, DomainError>> SignInWithPasswordAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (_supabaseUrl is null || _anonKey is null)
        {
            // Distinguish misconfiguration from bad credentials: the operator
            // must be able to tell that no ANON key / URL was provided (the
            // bootstrap CLI uses the SERVICE-ROLE key, which is NOT enough
            // for the normal login path). Variable names are not secrets.
            var missing = new List<string>();
            if (_supabaseUrl is null) missing.Add(SupabaseSettings.UrlVariable);
            if (_anonKey is null) missing.Add(SupabaseSettings.AnonKeyVariable);
            return Result<AuthUser, DomainError>.Failure(DomainError.BackendUnavailable(
                "AUTH_PROVIDER_MISCONFIGURED",
                $"Authentication provider is not configured; missing environment variable(s): " +
                $"{string.Join(", ", missing)}. The password was not even tested."));
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return Result<AuthUser, DomainError>.Failure(InvalidCredentials());

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_supabaseUrl}/auth/v1/token?grant_type=password")
        {
            Content = JsonContent.Create(new { email, password })
        };
        request.Headers.Add("apikey", _anonKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Server-side diagnostic (logged by the caller): the exception
            // text may contain the public project URL — never the key.
            return Result<AuthUser, DomainError>.Failure(DomainError.BackendUnavailable(
                "AUTH_PROVIDER_UNAVAILABLE",
                $"The authentication provider could not be reached ({ex.GetType().Name}: {ex.Message})."));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string body;
                try
                {
                    body = await response.Content.ReadAsStringAsync(cancellationToken);
                }
                catch (Exception)
                {
                    body = string.Empty;
                }

                // Capture the REAL provider reason (status + GoTrue
                // error/error_description) into the domain error so the Web
                // layer can log it. Never secrets: the request body (email +
                // password) and the apikey are never echoed.
                var (errorCode, errorDescription) = ParseGoTrueError(body);
                var status = (int)response.StatusCode;
                var detail = $"GoTrue token request rejected (HTTP {status}):" +
                             (string.IsNullOrEmpty(errorCode) ? "" : $" error={errorCode};") +
                             (string.IsNullOrEmpty(errorDescription) ? "" : $" error_description={errorDescription}.") +
                             (string.IsNullOrEmpty(errorCode) && string.IsNullOrEmpty(errorDescription)
                                 ? " No machine-readable reason in the response body." : "");

                // ME-1: classify 4xx precisely instead of blanket-mapping to
                // "bad credentials". 429 = provider rate limiting (transient
                // provider condition, NOT a user credential failure); 401/403
                // = the provider rejected the apikey itself (a present but
                // wrong or rotated key) — configuration suspect; remaining
                // 4xx (400, ...) = credentials/authorization problem;
                // 5xx/other = provider-side problem.
                if (status == 429)
                    return Result<AuthUser, DomainError>.Failure(DomainError.BackendUnavailable(
                        "AUTH_PROVIDER_UNAVAILABLE",
                        detail + " The provider is rate-limiting requests; try again shortly."));
                if (status is 401 or 403)
                    return Result<AuthUser, DomainError>.Failure(DomainError.BackendUnavailable(
                        "AUTH_PROVIDER_MISCONFIGURED",
                        detail + " The apikey was rejected by the provider; it may be invalid, rotated or wrong."));
                return status is >= 400 and < 500
                    ? Result<AuthUser, DomainError>.Failure(DomainError.Unauthorized(
                        "INVALID_CREDENTIALS", detail))
                    : Result<AuthUser, DomainError>.Failure(DomainError.BackendUnavailable(
                        "AUTH_PROVIDER_UNAVAILABLE", detail));
            }

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<SignInResponse>(
                    cancellationToken: cancellationToken);
                if (payload?.User?.Id is not Guid authUserId || authUserId == Guid.Empty)
                    return Result<AuthUser, DomainError>.Failure(DomainError.BackendUnavailable(
                        "AUTH_PROVIDER_UNAVAILABLE",
                        "The provider accepted the credentials but the response did not carry a user id."));

                return Result<AuthUser, DomainError>.Success(
                    new AuthUser(authUserId, email));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<AuthUser, DomainError>.Failure(DomainError.BackendUnavailable(
                    "AUTH_PROVIDER_UNAVAILABLE",
                    $"The provider response could not be read ({ex.GetType().Name}: {ex.Message})."));
            }
        }
    }

    /// <summary>Extracts GoTrue's machine-readable reason from an error body.</summary>
    private static (string Error, string Description) ParseGoTrueError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (string.Empty, string.Empty);

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var error = doc.RootElement.TryGetProperty("error", out var e)
                            && e.ValueKind == JsonValueKind.String
                    ? e.GetString() ?? string.Empty
                    : string.Empty;
                var description = doc.RootElement.TryGetProperty("error_description", out var d)
                            && d.ValueKind == JsonValueKind.String
                    ? d.GetString() ?? string.Empty
                    : string.Empty;
                return (error, description);
            }
        }
        catch (JsonException)
        {
            // Non-JSON body (e.g. proxy error page) — no machine reason.
        }

        return (string.Empty, string.Empty);
    }

    private static DomainError InvalidCredentials() => DomainError.Unauthorized(
        "INVALID_CREDENTIALS",
        "Credenciais inválidas.");

    private sealed class SignInResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("user")]
        public UserPayload? User { get; set; }
    }

    private sealed class UserPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public Guid Id { get; set; }
    }
}
