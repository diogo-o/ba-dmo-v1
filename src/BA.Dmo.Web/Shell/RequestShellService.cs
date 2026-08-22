using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Shell;
using BA.Dmo.Web.Identity;

namespace BA.Dmo.Web.Shell;

/// <summary>
/// Per-request shell state (Plan-V3 GLM-SHL-01/02/03, U-07): reads ONLY the
/// auth user id from the session cookie, resolves the internal identity
/// server-side (IdentityResolutionService — memoized per request) and derives
/// the navigation from the resolved grants. Null = no resolved internal
/// identity → the shell renders the minimal fail-closed frame (no tabs, no
/// identity presentation, no role fallback — GLM-ARCH-18).
/// </summary>
public sealed class RequestShellService : IShellService
{
    private readonly IdentityResolutionService _resolutionService;
    private readonly INavigationService _navigationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private ShellState? _cached;
    private bool _resolved;

    public RequestShellService(
        IdentityResolutionService resolutionService,
        INavigationService navigationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _resolutionService = resolutionService
            ?? throw new ArgumentNullException(nameof(resolutionService));
        _navigationService = navigationService
            ?? throw new ArgumentNullException(nameof(navigationService));
        _httpContextAccessor = httpContextAccessor
            ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public ShellState? Current
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

    private ShellState? ResolveForCurrentRequest()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return null;

        var rawClaim = httpContext.User.FindFirst(SessionClaims.AuthUserIdClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(rawClaim) || !Guid.TryParse(rawClaim, out var authUserId))
            return null;

        var resolution = _resolutionService
            .ResolveAsync(authUserId, httpContext.RequestAborted)
            .GetAwaiter()
            .GetResult();
        if (resolution.IsFailure)
            return null;

        var identity = resolution.Value;
        var navigation = _navigationService.Build(
            identity.Access, httpContext.Request.Path.Value);
        return new ShellState(identity.User.DisplayName, identity.ProfileTitle, navigation);
    }
}
