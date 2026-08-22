using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Armazem;

/// <summary>
/// Armazém (U-14) route surface (Plan-V3 05_SHL §5, GLM-ACC-02/04).
/// Server-side guarded by the armazem module policy. The module is CM/MF in V1
/// (owner decision C); interactive search / Entrada / Saída / Substituir /
/// história is driven from the canonical API endpoints via armazem.js (wiring
/// only). Armazém consumes tool identity only through IToolIdentityResolver —
/// never Ferramentas internals.
/// </summary>
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}