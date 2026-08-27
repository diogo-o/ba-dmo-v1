using BA.Dmo.Application.Modules.Admin;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Templates;

/// <summary>Template listing. Each template is one reusable title/function,
/// one functional profile (template-owned, SCHEMA-RAT-03A D-1) and one
/// canonical module set.</summary>
public class IndexModel : PageModel
{
    private readonly AdminTemplateService _templates;

    public IndexModel(AdminTemplateService templates)
    {
        _templates = templates;
    }

    public IReadOnlyList<AdminTemplateRow> Templates { get; private set; } = [];
    public IReadOnlyDictionary<string, string> Profiles { get; private set; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>One-shot feedback after create/update (set by the edit page).</summary>
    public string? Feedback { get; private set; }

    public async Task OnGetAsync()
    {
        Feedback = TempData["TemplateFeedback"] as string;
        Templates = await _templates.ListAsync(HttpContext.RequestAborted);
        Profiles = await _templates.ListFunctionalProfilesAsync(HttpContext.RequestAborted);
    }

    public string ProfileFor(string templateId) =>
        Profiles.TryGetValue(templateId, out var profile) ? profile : "Por configurar";
}