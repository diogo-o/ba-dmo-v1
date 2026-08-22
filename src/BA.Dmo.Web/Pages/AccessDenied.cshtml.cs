using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Web.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages;

/// <summary>
/// Safe state reached after a server-side 403 (Plan-V3 GLM-SHL-05/06).
/// Deep-link rule: an unauthorized URL is denied server-side and the shell
/// redirects safely to an area still authorized, with adequate feedback —
/// never a data leak, never a redirect loop (GLM-ACC-07 scenario 10).
/// Direct access without a session renders the static safe page (no data).
/// </summary>
public class AccessDeniedModel : PageModel
{
    private readonly IdentityResolutionService _resolutionService;

    public AccessDeniedModel(IdentityResolutionService resolutionService)
    {
        _resolutionService = resolutionService;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var rawClaim = User.FindFirst(SessionClaims.AuthUserIdClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(rawClaim) && Guid.TryParse(rawClaim, out var authUserId))
        {
            var resolution = await _resolutionService.ResolveAsync(authUserId, HttpContext.RequestAborted);
            if (resolution.IsSuccess && resolution.Value.FirstPage.Page is not null)
            {
                // Safe redirect to an authorized area; the fixed feedback
                // message is rendered by the shell from the flag below —
                // the client can trigger the message, never its content.
                return Redirect(resolution.Value.FirstPage.Page.Route + "?acesso-negado=1");
            }

            // Authenticated session with no resolvable access: safe state.
            // Backend-unavailable resolutions get the "try again" variant —
            // the user has a mapping that could not be loaded, not "no modules".
            return Redirect(resolution.IsFailure &&
                resolution.Error.Category == ErrorCategory.BackendUnavailable
                ? "/no-access?indisponivel=1"
                : "/no-access");
        }

        return Page();
    }
}
