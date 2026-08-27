using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Templates;

/// <summary>
/// Template editor: one reusable title/function + exactly one functional profile
/// + canonical module grants. The module catalog remains the single source of
/// assignable modules; N31 stores the template-owned functional profile.
/// </summary>
public class EditModel : PageModel
{
    private readonly AdminTemplateService _templates;
    private readonly TemplateProfileStore _templateProfiles;

    public EditModel(AdminTemplateService templates, IDbConnectionFactory connectionFactory)
    {
        _templates = templates;
        _templateProfiles = new TemplateProfileStore(connectionFactory);
    }

    public sealed class GrantLine
    {
        public string ModuleId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Granted { get; set; }
    }

    public bool IsNew { get; private set; }
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FunctionalProfile { get; set; } = "Operador / Controlador";
    public bool Active { get; set; } = true;
    public DateTimeOffset Version { get; set; }
    public List<GrantLine> Lines { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            IsNew = true;
            Lines = CanonicalLines(new Dictionary<string, IReadOnlyList<string>>());
            return Page();
        }

        var template = await _templates.GetAsync(id, HttpContext.RequestAborted);
        if (template.IsFailure)
        {
            ModelState.AddModelError(string.Empty, template.Error.Message);
            IsNew = true;
            Lines = CanonicalLines(new Dictionary<string, IReadOnlyList<string>>());
            return Page();
        }

        IsNew = false;
        TemplateId = template.Value.TemplateId;
        Name = template.Value.Name;
        Active = template.Value.Active;
        Version = template.Value.UpdatedAtUtc;
        FunctionalProfile = await _templateProfiles.GetAsync(
            template.Value.TemplateId, HttpContext.RequestAborted)
            ?? "Operador / Controlador";

        var parsed = AccessTemplateGrantsParser.Parse(template.Value.ModulesJson);
        var grants = parsed.IsSuccess
            ? parsed.Value.ToDictionary(g => g.ModuleId, g => g.Capabilities)
            : new Dictionary<string, IReadOnlyList<string>>();
        Lines = CanonicalLines(grants);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        string templateId,
        string name,
        string functionalProfile,
        bool active,
        string? version,
        List<GrantLine> lines)
    {
        TemplateId = templateId;
        Name = name;
        FunctionalProfile = functionalProfile;
        Active = active;
        Lines = lines ?? [];

        if (!FunctionalProfileNames.TryParse(functionalProfile, out var profile))
        {
            ModelState.AddModelError(string.Empty, "Selecione um perfil funcional válido.");
            IsNew = string.IsNullOrWhiteSpace(version);
            EnsureLines();
            return Page();
        }

        var grants = (lines ?? new List<GrantLine>())
            .Where(l => l.Granted && !string.IsNullOrWhiteSpace(l.ModuleId))
            .Select(l => new TemplateGrantInput(l.ModuleId, Array.Empty<string>()))
            .ToList();

        if (grants.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Selecione pelo menos um módulo para o template.");
            IsNew = string.IsNullOrWhiteSpace(version);
            EnsureLines();
            return Page();
        }

        var hasAdmin = grants.Any(g => g.ModuleId == CanonicalModuleCatalog.AdminModuleId);
        if (profile == BA.Dmo.Domain.Shared.Access.FunctionalProfile.Admin)
        {
            if (!hasAdmin || grants.Any(g => g.ModuleId != CanonicalModuleCatalog.AdminModuleId))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "O perfil Admin deve usar apenas o módulo Administração.");
                IsNew = string.IsNullOrWhiteSpace(version);
                EnsureLines();
                return Page();
            }
        }
        else if (hasAdmin)
        {
            ModelState.AddModelError(
                string.Empty,
                "Perfis Operador e Responsável não podem incluir o módulo Administração.");
            IsNew = string.IsNullOrWhiteSpace(version);
            EnsureLines();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            var created = await _templates.CreateAsync(
                new CreateTemplateRequest(templateId, name, grants),
                HttpContext.RequestAborted);
            if (created.IsFailure)
                return Finish(created.Error.Message, isNew: true);

            await _templateProfiles.UpsertAsync(
                created.Value.TemplateId,
                profile.DisplayName(),
                HttpContext.RequestAborted);
            return Finish(null, isNew: true);
        }

        if (!DateTimeOffset.TryParse(version, out var expectedVersion))
        {
            ModelState.AddModelError(string.Empty, "Versão de concorrência inválida.");
            IsNew = false;
            EnsureLines();
            return Page();
        }

        var updated = await _templates.UpdateAsync(
            new UpdateTemplateRequest(templateId, name, grants, active, expectedVersion),
            HttpContext.RequestAborted);
        if (updated.IsFailure)
            return Finish(updated.Error.Message, isNew: false);

        await _templateProfiles.UpsertAsync(
            updated.Value.TemplateId,
            profile.DisplayName(),
            HttpContext.RequestAborted);
        return Finish(null, isNew: false);
    }

    private IActionResult Finish(string? error, bool isNew)
    {
        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            IsNew = isNew;
            EnsureLines();
            return Page();
        }

        TempData["TemplateFeedback"] = isNew
            ? "Template criado. Perfil e módulos ficaram associados ao título."
            : "Template guardado. Os utilizadores associados assumem esta configuração.";
        return Redirect("/admin/templates");
    }

    private void EnsureLines()
    {
        if (Lines.Count == 0)
            Lines = CanonicalLines(new Dictionary<string, IReadOnlyList<string>>());
    }

    private static List<GrantLine> CanonicalLines(
        IReadOnlyDictionary<string, IReadOnlyList<string>> granted)
    {
        var lines = new List<GrantLine>();
        foreach (var module in CanonicalModuleCatalog.Instance.Modules)
        {
            if (!module.IsAssignable)
                continue;

            lines.Add(new GrantLine
            {
                ModuleId = module.ModuleId,
                DisplayName = module.DisplayName,
                Granted = granted.ContainsKey(module.ModuleId)
            });
        }

        return lines;
    }
}
