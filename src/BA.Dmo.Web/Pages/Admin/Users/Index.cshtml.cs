using BA.Dmo.Application.Modules.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Users;

/// <summary>User listing. The assigned template is presented as the reusable
/// title/function; the functional profile is shown separately.</summary>
public class IndexModel : PageModel
{
    private readonly AdminUserService _users;
    private readonly AdminTemplateService _templates;

    public IndexModel(AdminUserService users, AdminTemplateService templates)
    {
        _users = users;
        _templates = templates;
    }

    public IReadOnlyList<AdminUserRow> Users { get; private set; } = [];
    public IReadOnlyDictionary<string, string> TemplateTitles { get; private set; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
    public string? Search { get; private set; }
    public string? StateFilter { get; private set; }
    public string? Feedback { get; set; }
    public string? ServiceErrorMessage { get; private set; }

    public async Task OnGetAsync(string? q, string? state)
    {
        Search = q;
        StateFilter = state;
        await ReloadUsersAsync();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string id)
    {
        var result = await _users.RequestPasswordResetAsync(id, HttpContext.RequestAborted);
        Feedback = result.IsSuccess
            ? "Reset de palavra-passe iniciado."
            : result.Error.Message;
        if (result.IsFailure)
            ModelState.AddModelError(string.Empty, result.Error.Message);

        await ReloadUsersAsync();
        return Page();
    }

    public string TemplateTitleFor(AdminUserRow user) =>
        TemplateTitles.TryGetValue(user.TemplateId, out var title)
            ? title
            : user.TemplateId;

    private async Task ReloadUsersAsync()
    {
        var templates = await _templates.ListAsync(HttpContext.RequestAborted);
        TemplateTitles = templates.ToDictionary(
            template => template.TemplateId,
            template => template.Name,
            StringComparer.Ordinal);

        var result = await _users.ListAsync(null, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            ServiceErrorMessage = result.Error.Message;
            Users = [];
            return;
        }

        Users = result.Value;
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            Users = Users
                .Where(u =>
                    u.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (u.ProfileTitle?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || TemplateTitleFor(u).Contains(term, StringComparison.OrdinalIgnoreCase)
                    || u.ActorId.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (u.AuthEmail?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList()
                .AsReadOnly();
        }

        if (!string.IsNullOrWhiteSpace(StateFilter) && StateFilter != "all")
        {
            var isActive = StateFilter.Equals("active", StringComparison.OrdinalIgnoreCase);
            Users = Users.Where(u => u.Active == isActive).ToList().AsReadOnly();
        }
    }
}
