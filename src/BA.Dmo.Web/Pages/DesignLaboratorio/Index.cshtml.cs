using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.DesignLaboratorio;

/// <summary>
/// Design system laboratory (Plan-V3 U-08 mandatory gate, contract §20):
/// presents every universal component and state using ONLY the global
/// dmo-design-system CSS. Session-gated like every application page
/// (fallback policy); it is not a module route — it exists nowhere in the
/// canonical catalogs and grants nothing. Removed with the module units
/// once visual regression baselines supersede it (U-09/U-20).
/// </summary>
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
