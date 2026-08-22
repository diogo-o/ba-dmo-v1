using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Ferramentas;

/// <summary>
/// Ferramentas "Criar novo registo" page (U-12, GLM-FERR-05.1/brief §4).
/// A dedicated page (not a modal) building a reference + first lot atomically via
/// the API (ferramentas.js). Gated by the ferramentas module policy.
/// </summary>
public class CriarModel : PageModel
{
    /// <summary>Pre-selected tool type from the landing area (CM / MF).</summary>
    public string? ToolType { get; private set; }

    public void OnGet(string type = "CM")
    {
        ToolType = (type == "MF") ? "MF" : "CM";
    }
}