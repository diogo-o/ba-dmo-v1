using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Armazem;

/// <summary>
/// Armazém (U-14) route surface (Plan-V3 05_SHL §5, GLM-ACC-02/04).
/// Server-side guarded by the armazem module policy. The module supports normal
/// CM/MF/BQ physical movements; interactive search / Entrada / Saída /
/// história is driven from the canonical API endpoints via armazem.js (wiring
/// only). Armazém consumes tool identity only through IToolIdentityResolver —
/// never Ferramentas internals.
/// </summary>
public class IndexModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public IndexModel(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor;
    }

    public bool CanCreateNewTool { get; private set; }

    public void OnGet()
    {
        CanCreateNewTool = _currentUserAccessor.Current?.HasModule(
            FerramentasModuleCatalog.ModuleId) == true;
    }
}
