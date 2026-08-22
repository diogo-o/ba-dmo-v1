using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Web.Identity;

/// <summary>
/// Persistence authorship binding (Plan-V3 U-05 scope, GLM-DATA-02):
/// authorship is the server-side resolved internal actor_id of the current
/// session — never accepted from the client. Timestamps remain UTC via
/// IClock (U-03). System/background operations without a session resolve
/// actor = null.
/// </summary>
public sealed class CurrentUserAuthorshipAccessor : IPersistenceAuthorshipAccessor
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IdentityResolutionService _resolutionService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    private PersistenceAuthorship? _cached;

    public CurrentUserAuthorshipAccessor(
        ICurrentUserAccessor currentUserAccessor,
        IdentityResolutionService resolutionService,
        IHttpContextAccessor httpContextAccessor,
        IClock clock)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
        _resolutionService = resolutionService
            ?? throw new ArgumentNullException(nameof(resolutionService));
        _httpContextAccessor = httpContextAccessor
            ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public PersistenceAuthorship Current =>
        _cached ??= Resolve();

    private PersistenceAuthorship Resolve()
    {
        // The CurrentUser carries the auth user id; the authorship column is
        // the internal actor_id, so resolution provides the authoritative id.
        if (_currentUserAccessor.Current is not null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var rawClaim = httpContext?.User.FindFirst(SessionClaims.AuthUserIdClaimType)?.Value;
            if (!string.IsNullOrWhiteSpace(rawClaim) && Guid.TryParse(rawClaim, out var authUserId))
            {
                var resolved = _resolutionService
                    .ResolveAsync(authUserId, httpContext?.RequestAborted ?? CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                if (resolved.IsSuccess)
                    return new PersistenceAuthorship(resolved.Value.ActorId, _clock.UtcNow);
            }
        }

        return new PersistenceAuthorship(null, _clock.UtcNow);
    }
}
