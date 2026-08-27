using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Users;

/// <summary>
/// Create internal user. The Admin chooses exactly one reusable template;
/// its title/function, functional profile and module grants are assumed
/// automatically by the new user.
/// </summary>
public class CreateModel : PageModel
{
    private readonly AdminUserService _users;
    private readonly AdminTemplateService _templates;
    private readonly TemplateProfileStore _templateProfiles;

    public CreateModel(
        AdminUserService users,
        AdminTemplateService templates,
        IDbConnectionFactory connectionFactory)
    {
        _users = users;
        _templates = templates;
        _templateProfiles = new TemplateProfileStore(connectionFactory);
    }

    public sealed class InputModel
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string TemplateId { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<AdminTemplateRow> Templates { get; private set; } = [];
    public IReadOnlyDictionary<string, string> TemplateProfiles { get; private set; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    public async Task OnGetAsync()
    {
        await LoadTemplatesAsync();
    }

    public async Task<IActionResult> OnPostAsync(
        string email,
        string password,
        string displayName,
        string? templateId,
        bool active,
        List<string>? templateIds = null)
    {
        await LoadTemplatesAsync();

        // Compatibility for pre-rework form posts/tests only. The rendered UI
        // exposes a single `templateId`; if an old `templateIds` field arrives,
        // only its first value is considered. It can never recreate a hybrid.
        var selectedTemplateId = !string.IsNullOrWhiteSpace(templateId)
            ? templateId.Trim()
            : templateIds?.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id))?.Trim()
              ?? string.Empty;

        Input = new InputModel
        {
            Email = email,
            DisplayName = displayName,
            TemplateId = selectedTemplateId,
            Active = active
        };

        if (string.IsNullOrWhiteSpace(selectedTemplateId)
            || !TemplateProfiles.TryGetValue(selectedTemplateId, out var profile))
        {
            ModelState.AddModelError(
                string.Empty,
                "Selecione um template ativo com perfil funcional configurado.");
            return Page();
        }

        var result = await _users.CreateUserAsync(
            new CreateAdminUserRequest(
                email,
                password,
                displayName,
                profile,
                selectedTemplateId,
                active,
                [selectedTemplateId]),
            HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            return Page();
        }

        return Redirect("/admin/users");
    }

    public string ProfileFor(string templateId) =>
        TemplateProfiles.TryGetValue(templateId, out var profile) ? profile : "Por configurar";

    private async Task LoadTemplatesAsync()
    {
        Templates = (await _templates.ListAsync(HttpContext.RequestAborted))
            .Where(template => template.Active)
            .ToList()
            .AsReadOnly();
        TemplateProfiles = await _templateProfiles.ListAsync(HttpContext.RequestAborted);
    }
}
