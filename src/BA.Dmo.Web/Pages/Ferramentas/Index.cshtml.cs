using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Ferramentas;

/// <summary>
/// Ferramentas (CM/MF) route surface (Plan-V3 05_SHL §5, GLM-ACC-02/04, U-12).
/// Server-side guarded by the ferramentas module policy. The landing holds the
/// reference list/consultation; interactive create/duplicate/configure is driven
/// from the canonical API endpoints via ferramentas.js (wiring only). Configuration
/// UI requires the <c>ferramentas.configure</c> ability.
/// </summary>
public class IndexModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public IndexModel(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
    }

    /// <summary>Whether the user may configure verification rules (ferramentas.configure).</summary>
    public bool CanConfigure { get; private set; }

    public void OnGet()
    {
        var user = _currentUserAccessor.Current;
        CanConfigure = user?.HasCapability(CanonicalModuleCatalog.FerramentasConfigureCapabilityId) == true;
    }
}

/// <summary>View model for the shared CM/MF reference list partial.</summary>
public sealed record FerramentasListModel(string ToolType);