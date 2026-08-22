using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.ReparacaoInterna;

/// <summary>
/// Reparação Interna route surface (Plan-V3 05_SHL §5, GLM-ACC-02/04, U-16):
/// guarded server-side by the reparacao_interna module policy (module presence grants
/// entry; corrections additionally require the reparacao_interna.corrigir capability,
/// GLM-RI-02). The interactive flows are driven from the canonical API endpoints via
/// reparacao-interna.js (wiring only — no business logic duplicated in JS).
/// </summary>
public class IndexModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public IndexModel(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
    }

    /// <summary>Whether the user may correct internal repair records.</summary>
    public bool CanCorrigir { get; private set; }

    public void OnGet()
    {
        var user = _currentUserAccessor.Current;
        CanCorrigir = user?.HasCapability(CanonicalModuleCatalog.ReparacaoInternaCorrigirCapabilityId) == true;
    }
}