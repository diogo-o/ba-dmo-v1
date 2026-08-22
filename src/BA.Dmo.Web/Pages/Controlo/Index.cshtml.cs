using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BA.Dmo.Application.Modules.Controlo;
using BA.Dmo.Domain.Modules.Controlo;

namespace BA.Dmo.Web.Pages.Controlo;

/// <summary>
/// R010 — Folha de Controlo route surface. The sheet lives INSIDE the Controlo area and is
/// reached from the Peso production-control area (same place as the Peso PDF/action), so the
/// page is gated by the Peso module policy; sheet operations are gated server-side by the
/// controlo.* capabilities via the API service gate. This page acts as the Folha de Controlo
/// detail/edit sheet for a production whose job_on context was already selected upstream.
/// </summary>
public class IndexModel : PageModel
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public IndexModel(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor;
    }

    /// <summary>Selected job_on id deep-linked from the production-control area (may be empty → list/choose).</summary>
    public Guid ProjectedJobOnId { get; private set; }

    public bool CanEdit { get; private set; }
    public bool CanSubmit { get; private set; }
    public bool CanReview { get; private set; }

    public void OnGet(Guid? jobOn)
    {
        var user = _currentUserAccessor.Current;
        ProjectedJobOnId = jobOn ?? Guid.Empty;
        CanEdit = user?.HasCapability(ControloSheetModuleCatalog.EditCapabilityId) == true;
        CanSubmit = user?.HasCapability(ControloSheetModuleCatalog.SubmitCapabilityId) == true;
        CanReview = user?.HasCapability(ControloSheetModuleCatalog.ReviewCapabilityId) == true;
    }
}