using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Pegamentos;

/// <summary>
/// Pegamentos control sheet (U-11). Displays a single controlo with its
/// inherited historical production context and measurements. Measurement entry,
/// save, close and PDF save-confirmation are driven from the canonical API
/// endpoints via pegamentos.js (wiring only — calculations are C#-only).
/// </summary>
public class DetailModel : PageModel
{
    /// <summary>Resolved controlo id from the route.</summary>
    public Guid? ControloId { get; private set; }

    public void OnGet(Guid id)
    {
        ControloId = id;
    }
}