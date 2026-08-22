using BA.Dmo.Web.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Auth;

/// <summary>
/// Logout (Plan-V3 05_SHL §5/§7): ends the web session and returns to login.
/// The Supabase account itself is untouched; only the application cookie is
/// cleared. No role or grant information participates in this flow.
/// </summary>
[AllowAnonymous]
public class LogoutModel : PageModel
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(SessionClaims.AuthenticationScheme);
        return Redirect("/login");
    }
}
