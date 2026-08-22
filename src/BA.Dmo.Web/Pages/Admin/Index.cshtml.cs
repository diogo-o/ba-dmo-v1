using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin;

/// <summary>
/// Administração — entry page (Plan-V3 04_ACC §9, GLM-ACC-06). Page access
/// is enforced by the admin.gerir policy; every mutation is additionally
/// re-authorized server-side inside the Application services. For the admin
/// (no jobon.view by owner decision) this page IS the first/landing page;
/// functional users keep the Job On landing.
/// </summary>
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
