using BA.Dmo.Application.Modules.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Users;

/// <summary>
/// Create internal user (04_ACC §9, TD-16): provisions the Auth account
/// through the privileged adapter and registers the internal user. The
/// password is used only by the provider call — never persisted, echoed or
/// audited. The functional profile is one of the three closed values.
/// </summary>
public class CreateModel : PageModel
{
    private readonly AdminUserService _users;
    private readonly AdminTemplateService _templates;

    public CreateModel(AdminUserService users, AdminTemplateService templates)
    {
        _users = users;
        _templates = templates;
    }

    public sealed class InputModel
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ProfileTitle { get; set; } = "Operador / Controlador";
        public List<string> TemplateIds { get; set; } = [];
        public bool Active { get; set; } = true;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<AdminTemplateRow> Templates { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Templates = await _templates.ListAsync(HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostAsync(
        string email, string password, string displayName, string profileTitle,
        List<string> templateIds, bool active)
    {
        Templates = await _templates.ListAsync(HttpContext.RequestAborted);
        Input = new InputModel
        {
            Email = email,
            DisplayName = displayName,
            ProfileTitle = profileTitle,
            TemplateIds = templateIds ?? [],
            Active = active
        };

        var selected = (templateIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        var result = await _users.CreateUserAsync(
            new CreateAdminUserRequest(
                email, password, displayName, profileTitle,
                selected.FirstOrDefault() ?? string.Empty, active, selected),
            HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            return Page();
        }

        return Redirect("/admin/users");
    }
}
