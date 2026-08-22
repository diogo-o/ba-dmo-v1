using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Web.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages;

/// <summary>
/// Root route (Plan-V3 05_SHL section 5): "/" redirects to the first page
/// resolved from the user's EFFECTIVE access surface (U-04): the Job On
/// landing for functional users (universal jobon.view); /admin for the
/// admin (owner decision: the admin module is excluded from jobon.view).
/// Deterministic fallback to the first accessible page in canonical order
/// only when the landing is genuinely unavailable; /no-access safe state
/// otherwise (GLM-SHL-06, no redirect loop).
/// </summary>
public class IndexModel : PageModel
{
    private readonly IdentityResolutionService _resolutionService;

    public IndexModel(IdentityResolutionService resolutionService)
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
                return Redirect(resolution.Value.FirstPage.Page.Route);

            // Resolved but no authorized page, or backend unavailable:
            // safe state, no loop. Backend failures get the "try again"
            // variant (the mapping exists but could not be loaded).
            if (resolution.IsFailure &&
                resolution.Error.Category == ErrorCategory.BackendUnavailable)
                return Redirect("/no-access?indisponivel=1");
        }

        // Session without a resolvable identity/access: safe state, no loop.
        return Redirect("/no-access");
    }
}
