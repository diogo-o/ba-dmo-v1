using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Pegamentos;

/// <summary>
/// Pegamentos route surface (Plan-V3 05_SHL §5, GLM-ACC-02/04, U-11).
/// Server-side guarded by the pegamentos module policy; the page lays out the
/// module landing (list/consultation). Interactive entry/editing is driven from
/// the canonical API endpoints via pegamentos.js (wiring only — no duplicated
/// calculation in JS). Edit capability comes from jobon.edit (fixing tools on a
/// Job On, opening a control sheet for measurement entry).
/// </summary>
public class IndexModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public IndexModel(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
    }

    /// <summary>Whether the user may fix/edit tools on a Job On and open a control sheet.</summary>
    public bool CanEdit { get; private set; }

    public void OnGet()
    {
        var user = _currentUserAccessor.Current;
        CanEdit = user?.HasCapability(CanonicalModuleCatalog.JobonEditCapabilityId) == true;
    }
}