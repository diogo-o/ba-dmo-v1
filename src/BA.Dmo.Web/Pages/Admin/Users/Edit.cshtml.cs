using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Users;

/// <summary>
/// Edits user identity, one reusable template and activation. The selected
/// template supplies both the functional profile and module access.
/// </summary>
public class EditModel : PageModel
{
    private readonly AdminUserService _users;
    private readonly AdminTemplateService _templates;
    private readonly TemplateProfileStore _templateProfiles;

    public EditModel(
        AdminUserService users,
        AdminTemplateService templates,
        IDbConnectionFactory connectionFactory)
    {
        _users = users;
        _templates = templates;
        _templateProfiles = new TemplateProfileStore(connectionFactory);
    }

    public AdminUserRow? Entry { get; private set; }
    public IReadOnlyList<AdminTemplateRow> Templates { get; private set; } = [];
    public IReadOnlyDictionary<string, string> TemplateProfiles { get; private set; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
    public string? Feedback { get; private set; }
    public string? ServiceErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(
        string id,
        string displayName,
        string templateId,
        bool active,
        string version)
    {
        if (!DateTimeOffset.TryParse(version, out var expectedVersion))
        {
            ModelState.AddModelError(string.Empty, "Versão de concorrência inválida.");
            await LoadAsync(id);
            return Page();
        }

        TemplateProfiles = await _templateProfiles.ListAsync(HttpContext.RequestAborted);
        if (string.IsNullOrWhiteSpace(templateId)
            || !TemplateProfiles.TryGetValue(templateId, out var profile))
        {
            ModelState.AddModelError(
                string.Empty,
                "Selecione um template com perfil funcional configurado.");
            await LoadAsync(id);
            return Page();
        }

        var result = await _users.SaveUserAsync(
            id,
            displayName,
            profile,
            [templateId],
            active,
            expectedVersion,
            HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            await LoadAsync(id);
            return Page();
        }

        return Redirect("/admin/users");
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string id)
    {
        var result = await _users.RequestPasswordResetAsync(id, HttpContext.RequestAborted);

        await LoadAsync(id);
        Feedback = result.IsSuccess
            ? "Reset de palavra-passe iniciado."
            : result.Error.Message;
        if (result.IsFailure)
            ModelState.AddModelError(string.Empty, result.Error.Message);

        return Page();
    }

    public string ProfileFor(string templateId) =>
        TemplateProfiles.TryGetValue(templateId, out var profile) ? profile : "Por configurar";

    private async Task LoadAsync(string id)
    {
        Templates = await _templates.ListAsync(HttpContext.RequestAborted);
        TemplateProfiles = await _templateProfiles.ListAsync(HttpContext.RequestAborted);
        var user = await _users.GetAsync(id, HttpContext.RequestAborted);
        ServiceErrorMessage = user.IsFailure
            && user.Error.Category == ErrorCategory.BackendUnavailable
            ? user.Error.Message
            : null;
        Entry = user.IsSuccess ? user.Value : null;
    }
}
