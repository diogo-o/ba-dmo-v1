using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.Web.Identity;

/// <summary>
/// Per-request current-user accessor (Plan-V3 GLM-ARCH-03, GLM-ACC-01.5).
/// Reads ONLY the auth user id from the session cookie, then resolves the
/// internal identity server-side (internal_users → template → U-04 access).
/// Resolution is cached for the duration of the request; null = no resolved
/// internal user (fail-closed safe state).
/// </summary>
public sealed class RequestCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IdentityResolutionService _resolutionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private CurrentUser? _cached;
    private bool _resolved;

    public RequestCurrentUserAccessor(
        IdentityResolutionService resolutionService,
        IHttpContextAccessor httpContextAccessor)
    {
        _resolutionService = resolutionService
            ?? throw new ArgumentNullException(nameof(resolutionService));
        _httpContextAccessor = httpContextAccessor
            ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public CurrentUser? Current
    {
        get
        {
            if (_resolved)
                return _cached;

            _resolved = true;
            _cached = ResolveForCurrentRequest();
            return _cached;
        }
    }

    private CurrentUser? ResolveForCurrentRequest()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return null;

        var rawClaim = httpContext.User.FindFirst(SessionClaims.AuthUserIdClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(rawClaim) || !Guid.TryParse(rawClaim, out var authUserId))
            return null;

        var result = _resolutionService
            .ResolveAsync(authUserId, httpContext.RequestAborted)
            .GetAwaiter()
            .GetResult();

        return result.IsSuccess ? result.Value.User : null;
    }
}
