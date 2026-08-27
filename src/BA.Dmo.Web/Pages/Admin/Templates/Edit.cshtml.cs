using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Templates;

/// <summary>
/// Template editor (04_ACC §9, GLM-ACC-03): creates/updates access templates
/// against the canonical catalog. The grants editor shows only canonical
/// modules; submitted grants are validated server-side by the use case
/// (unknown modules/capabilities reject the write). Optimistic concurrency
/// on update; self-lockout protection in the use case.
/// </summary>
public class EditModel : PageModel
{
    private readonly AdminTemplateService _templates;

    public EditModel(AdminTemplateService templates)
    {
        _templates = templates;
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

        var parsed = AccessTemplateGrantsParser.Parse(template.Value.ModulesJson);
        var grants = parsed.IsSuccess
            ? parsed.Value.ToDictionary(g => g.ModuleId, g => g.Capabilities)
            : new Dictionary<string, IReadOnlyList<string>>();
        Lines = CanonicalLines(grants);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        string templateId, string name, bool active, string? version, List<GrantLine> lines)
    {
        TemplateId = templateId;
        Name = name;
        Active = active;
        Lines = lines ?? [];

        var grants = (lines ?? new List<GrantLine>())
            .Where(l => l.Granted && !string.IsNullOrWhiteSpace(l.ModuleId))
            .Select(l => new TemplateGrantInput(l.ModuleId, Array.Empty<string>()))
            .ToList();

        if (string.IsNullOrWhiteSpace(version))
        {
            var created = await _templates.CreateAsync(
                new CreateTemplateRequest(templateId, name, grants),
                HttpContext.RequestAborted);
            return Finish(created.IsFailure ? created.Error.Message : null, isNew: true);
        }

        if (!DateTimeOffset.TryParse(version, out var expectedVersion))
        {
            ModelState.AddModelError(string.Empty, "Versão de concorrência inválida.");
            return Page();
        }

        var updated = await _templates.UpdateAsync(
            new UpdateTemplateRequest(templateId, name, grants, active, expectedVersion),
            HttpContext.RequestAborted);
        return Finish(updated.IsFailure ? updated.Error.Message : null, isNew: false);
    }

    private IActionResult Finish(string? error, bool isNew)
    {
        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            IsNew = isNew;
            if (isNew && Lines.Count == 0)
                Lines = CanonicalLines(new Dictionary<string, IReadOnlyList<string>>());
            return Page();
        }

        TempData["TemplateFeedback"] = isNew ? "Template criado." : "Template guardado.";
        return Redirect("/admin/templates");
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
