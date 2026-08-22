using System.Net;
using System.Net.Http.Json;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Infrastructure.Auth;

/// <summary>
/// PRIVILEGED provisioning adapter (Plan-V3 GLM-ARCH-14/PV-07, 06_DATA §14–15).
/// The single component allowed to use the service_role credential, and only
/// for explicit privileged operations (bootstrap-admin and admin-initiated user
/// creation / password reset). It is registered server-side ONLY — in the Web
/// request pipeline for admin.gerir-gated use cases (TD-16) and by the bootstrap
/// CLI path — and is never exposed to the browser. The service-role value is
/// resolved from the server environment at construction and never appears in
/// messages, logs, claims or browser assets; it only travels on the wire between
/// the server and the Supabase Auth provider.
/// </summary>
public sealed class SupabaseAdminProvisioningAdapter : IAdminProvisioningAdapter
{
    private readonly HttpClient _httpClient;
    private readonly string? _supabaseUrl;
    private readonly string? _serviceRoleKey;

    public SupabaseAdminProvisioningAdapter(
        HttpClient httpClient,
        string? supabaseUrl,
        string? serviceRoleKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _supabaseUrl = string.IsNullOrWhiteSpace(supabaseUrl) ? null : supabaseUrl.TrimEnd('/');
        _serviceRoleKey = string.IsNullOrWhiteSpace(serviceRoleKey) ? null : serviceRoleKey;
    }

    public Task<Result<AuthUser, DomainError>> EnsureAuthUserAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Behavior preserved for existing consumers (TD-16 admin user create,
        // bootstrap): identical Result semantics; the pre-existed flag is
        // simply not surfaced here.
        return EnsureAuthUserInternalAsync(
            email, password, cancellationToken,
            ensure => new AuthUser(ensure.AuthUserId, ensure.Email));
    }

    public Task<Result<EnsuredAuthUser, DomainError>> EnsureAuthUserWithStatusAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default) =>
        EnsureAuthUserInternalAsync(
            email, password, cancellationToken, ensure => ensure);

    private async Task<Result<T, DomainError>> EnsureAuthUserInternalAsync<T>(
        string email,
        string password,
        CancellationToken cancellationToken,
        Func<EnsuredAuthUser, T> project)
    {
        if (_supabaseUrl is null || _serviceRoleKey is null)
            return Result<T, DomainError>.Failure(MissingConfiguration());

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return Result<T, DomainError>.Failure(DomainError.Validation(
                "BOOTSTRAP_CONFIGURATION_MISSING",
                "Provisioning requires an explicit email and password; nothing is defaulted."));

        var created = await SendCreateAsync(email, password, cancellationToken);
        if (created.IsSuccess)
            return Result<T, DomainError>.Success(
                project(new EnsuredAuthUser(created.Value.AuthUserId, email, AccountPreExisted: false)));

        // Idempotent path: the account already exists → look it up.
        if (created.Error.Code == "PROVISIONING_CONFLICT")
        {
            var existing = await FindExistingAsync(email, cancellationToken);
            if (existing.IsFailure)
                return Result<T, DomainError>.Failure(existing.Error);
            return Result<T, DomainError>.Success(
                project(new EnsuredAuthUser(existing.Value.AuthUserId, email, AccountPreExisted: true)));
        }

        return Result<T, DomainError>.Failure(created.Error);
    }

    private async Task<Result<AuthUser, DomainError>> SendCreateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{_supabaseUrl}/auth/v1/admin/users")
        {
            Content = JsonContent.Create(new
            {
                email,
                password,
                email_confirm = true
            })
        };
        AddPrivilegedHeaders(request);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<AuthUser, DomainError>.Failure(ProviderUnavailable());
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.UnprocessableEntity
                or HttpStatusCode.Conflict)
            {
                return Result<AuthUser, DomainError>.Failure(
                    DomainError.DomainConflict(
                        "PROVISIONING_CONFLICT",
                        "The authentication account already exists."));
            }

            if (!response.IsSuccessStatusCode)
                return Result<AuthUser, DomainError>.Failure(ProvisioningFailed());

            var payload = await ReadUserPayloadAsync(response, cancellationToken);
            return payload is null
                ? Result<AuthUser, DomainError>.Failure(ProvisioningFailed())
                : Result<AuthUser, DomainError>.Success(new AuthUser(payload.Value, email));
        }
    }

    public async Task<Result<bool, DomainError>> RequestPasswordResetAsync(
        Guid authUserId,
        CancellationToken cancellationToken = default)
    {
        if (_supabaseUrl is null || _serviceRoleKey is null)
            return Result<bool, DomainError>.Failure(MissingConfiguration());

        if (authUserId == Guid.Empty)
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "BOOTSTRAP_CONFIGURATION_MISSING",
                "Password reset requires an explicit auth user id."));

        // 1. Resolve the account email (privileged lookup).
        using (var lookup = new HttpRequestMessage(
                   HttpMethod.Get, $"{_supabaseUrl}/auth/v1/admin/users/{authUserId}"))
        {
            AddPrivilegedHeaders(lookup);

            HttpResponseMessage lookupResponse;
            try
            {
                lookupResponse = await _httpClient.SendAsync(lookup, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<bool, DomainError>.Failure(ProviderUnavailable());
            }

            string email;
            using (lookupResponse)
            {
                if (!lookupResponse.IsSuccessStatusCode)
                    return Result<bool, DomainError>.Failure(ProvisioningFailed());

                try
                {
                    var user = await lookupResponse.Content.ReadFromJsonAsync<UserPayload>(
                        cancellationToken: cancellationToken);
                    if (string.IsNullOrWhiteSpace(user?.Email))
                        return Result<bool, DomainError>.Failure(ProvisioningFailed());
                    email = user.Email;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return Result<bool, DomainError>.Failure(ProvisioningFailed());
                }
            }

            // 2. Request a recovery link for that account. The link/secret
            //    material is never returned, logged or audited.
            using var reset = new HttpRequestMessage(
                HttpMethod.Post, $"{_supabaseUrl}/auth/v1/admin/generate_link")
            {
                Content = JsonContent.Create(new { type = "recovery", email })
            };
            AddPrivilegedHeaders(reset);

            HttpResponseMessage resetResponse;
            try
            {
                resetResponse = await _httpClient.SendAsync(reset, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<bool, DomainError>.Failure(ProviderUnavailable());
            }

            using (resetResponse)
            {
                return resetResponse.IsSuccessStatusCode
                    ? Result<bool, DomainError>.Success(true)
                    : Result<bool, DomainError>.Failure(ProvisioningFailed());
            }
        }
    }

    private async Task<Result<AuthUser, DomainError>> FindExistingAsync(
        string email,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_supabaseUrl}/auth/v1/admin/users?email={Uri.EscapeDataString(email)}");
        AddPrivilegedHeaders(request);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<AuthUser, DomainError>.Failure(ProviderUnavailable());
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                return Result<AuthUser, DomainError>.Failure(ProvisioningFailed());

            try
            {
                var listing = await response.Content.ReadFromJsonAsync<UserListing>(
                    cancellationToken: cancellationToken);
                var match = listing?.Users?.FirstOrDefault(u =>
                    string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
                return match is not null && match.Id != Guid.Empty
                    ? Result<AuthUser, DomainError>.Success(new AuthUser(match.Id, email))
                    : Result<AuthUser, DomainError>.Failure(ProvisioningFailed());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<AuthUser, DomainError>.Failure(ProvisioningFailed());
            }
        }
    }

    private void AddPrivilegedHeaders(HttpRequestMessage request)
    {
        // Service role stays on the wire between server and provider only.
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        request.Headers.Add("apikey", _serviceRoleKey);
    }

    private async Task<Guid?> ReadUserPayloadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await response.Content.ReadFromJsonAsync<UserPayload>(
                cancellationToken: cancellationToken);
            return user?.Id is Guid id && id != Guid.Empty ? id : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Batched admin-auth email lookup that paginates the Supabase Admin users
    /// endpoint (GET /auth/v1/admin/users?page=N&amp;per_page=100) so users beyond
    /// the first page are not silently missed. Requests are batched per page —
    /// never one request per user. It stops as soon as every requested ID has a
    /// non-empty email, when a page returns fewer than 100 users (short final
    /// page), or at a defensive maximum-page bound. Configuration, HTTP and
    /// parsing failures degrade to an empty result (email stays null on the
    /// caller side); no service-role value or response secret is ever exposed.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, string>> GetUserEmailsAsync(
        IReadOnlyCollection<Guid> authUserIds,
        CancellationToken cancellationToken = default)
    {
        // Page size used by the Supabase Admin users list. Kept as configured by
        // the caller contract (page + per_page=100).
        const int pageSize = 100;
        // Defensive upper bound so an unexpectedly large directory cannot cause
        // unbounded paging; far exceeds any realistic operator directory.
        const int maxPages = 100;

        if (authUserIds is null || authUserIds.Count == 0)
            return new Dictionary<Guid, string>();

        if (_supabaseUrl is null || _serviceRoleKey is null)
            return new Dictionary<Guid, string>();

        try
        {
            var requested = new HashSet<Guid>(authUserIds);
            var found = new HashSet<Guid>(requested.Count);
            var result = new Dictionary<Guid, string>();

            for (var page = 1; page <= maxPages; page++)
            {
                // Stop early: every requested ID already has a non-empty email.
                if (found.Count == requested.Count)
                    break;

                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{_supabaseUrl}/auth/v1/admin/users?page={page}&per_page={pageSize}");
                request.Headers.Add("apikey", _serviceRoleKey);
                request.Headers.Add("Authorization", $"Bearer {_serviceRoleKey}");

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    break;

                var listing = await response.Content
                    .ReadFromJsonAsync<UserListing>(cancellationToken: cancellationToken);

                if (listing?.Users is null || listing.Users.Count == 0)
                    break;

                foreach (var user in listing.Users)
                {
                    if (requested.Contains(user.Id) && !string.IsNullOrWhiteSpace(user.Email))
                    {
                        result[user.Id] = user.Email;
                        found.Add(user.Id);
                    }
                }

                // Stop on a short final page (< pageSize users returned).
                if (listing.Users.Count < pageSize)
                    break;
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Dictionary<Guid, string>();
        }
    }

    private static DomainError MissingConfiguration() => DomainError.Validation(
        "PROVISIONING_CONFIGURATION_MISSING",
        "Privileged provisioning configuration is missing (Supabase URL / service-role). " +
        "Provide explicit environment configuration; nothing is defaulted.");

    private static DomainError ProvisioningFailed() => DomainError.BackendUnavailable(
        "PROVISIONING_FAILED",
        "The privileged provisioning operation failed. No user was provisioned.");

    private static DomainError ProviderUnavailable() => DomainError.BackendUnavailable(
        "AUTH_PROVIDER_UNAVAILABLE",
        "The authentication provider is unavailable. Try again later.");

    private sealed class UserPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public Guid Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    private sealed class UserListing
    {
        [System.Text.Json.Serialization.JsonPropertyName("users")]
        public List<UserPayload>? Users { get; set; }
    }
}
