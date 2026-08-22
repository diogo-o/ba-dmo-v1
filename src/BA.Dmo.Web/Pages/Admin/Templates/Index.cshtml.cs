using BA.Dmo.Application.Modules.Admin;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Templates;

/// <summary>Template listing (04_ACC §9). Templates are deactivated, never
/// deleted (UD-10).</summary>
public class IndexModel : PageModel
{
    private readonly AdminTemplateService _templates;

    public IndexModel(AdminTemplateService templates)
    {
        _templates = templates;
    }

    public IReadOnlyList<AdminTemplateRow> Templates { get; private set; } = [];

    /// <summary>One-shot feedback after create/update (set by the edit page).</summary>
    public string? Feedback { get; private set; }

    public async Task OnGetAsync()
    {
        Feedback = TempData["TemplateFeedback"] as string;
        Templates = await _templates.ListAsync(HttpContext.RequestAborted);
    }
}
