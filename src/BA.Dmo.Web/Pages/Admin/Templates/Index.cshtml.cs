using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Templates;

/// <summary>Template listing. Each template is one reusable title/function,
/// one functional profile and one canonical module set.</summary>
public class IndexModel : PageModel
{
    private readonly AdminTemplateService _templates;
    private readonly TemplateProfileStore _templateProfiles;

    public IndexModel(AdminTemplateService templates, IDbConnectionFactory connectionFactory)
    {
        _templates = templates;
        _templateProfiles = new TemplateProfileStore(connectionFactory);
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
        Profiles = await _templateProfiles.ListAsync(HttpContext.RequestAborted);
    }

    public string ProfileFor(string templateId) =>
        Profiles.TryGetValue(templateId, out var profile) ? profile : "Por configurar";
}
