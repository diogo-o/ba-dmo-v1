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
        string templateId,
        bool active)
    {
        await LoadTemplatesAsync();
        Input = new InputModel
        {
            Email = email,
            DisplayName = displayName,
            TemplateId = templateId,
            Active = active
        };

        if (string.IsNullOrWhiteSpace(templateId)
            || !TemplateProfiles.TryGetValue(templateId, out var profile))
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
                templateId,
                active,
                [templateId]),
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
