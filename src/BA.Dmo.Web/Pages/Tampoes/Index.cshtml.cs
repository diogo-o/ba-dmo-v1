using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Tampoes;

/// <summary>
/// Tampões route surface (Plan-V3 05_SHL §5, GLM-ACC-02/04, U-17): guarded
/// server-side by the tampoes module policy (module presence grants FULL Operator
/// access — quantities, transforms, planning AND Opções fields/configurations are
/// not reserved to the Admin, GLM-TP-02). Interactive flows are driven from the
/// canonical API endpoints via tampoes.js (wiring only — no business logic in JS).
/// </summary>
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}