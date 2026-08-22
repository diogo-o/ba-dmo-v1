using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BA.Dmo.Web.Pages.Admin.Users;

/// <summary>
/// Edit internal user (04_ACC §9, contract §3/§6): display name/profile title
/// (display-only text — UD-02), template assignment, activation, the read-only
/// auth email, the per-user "Módulos associados" grant editor, and the explicit
/// password reset initiation. Optimistic concurrency via the posted version
/// (GLM-ACC-12); the invariant guards are enforced in the use cases. The
/// module section posts as "Modules[moduleId]" checkbox groups and is
/// persisted through the composite save (profile + template + state + modules)
/// reusing the canonical grant validation and the server-side Job On guard.
/// </summary>
public class EditModel : PageModel
{
    private readonly AdminUserService _users;
    private readonly AdminTemplateService _templates;

    public EditModel(AdminUserService users, AdminTemplateService templates)
    {
        _users = users;
        _templates = templates;
    }

    public AdminUserRow? Entry { get; private set; }

    public IReadOnlyList<AdminTemplateRow> Templates { get; private set; } = [];

    public string? Feedback { get; private set; }

    /// <summary>
    /// User-safe service/configuration error (e.g. a required schema migration
    /// not applied). Rendered distinctly from a genuine "not found": a schema
    /// failure is NEVER a false 404. No technical detail leaks to the UI.
    /// </summary>
    public string? ServiceErrorMessage { get; private set; }

    /// <summary>The canonical module catalog in display order (for the grant editor rows).</summary>
    public IReadOnlyList<ModuleDefinition> CatalogModules => CanonicalModuleCatalog.Instance.Modules;

    /// <summary>True for functional-area catalog entries (Controlo): not a grantable module — grant its children.</summary>
    public static bool IsFunctionalArea(ModuleDefinition module) =>
        module.Kind == ModuleKind.FunctionalArea;

    /// <summary>
    /// A functional area (e.g. Controlo) is considered granted when at least one
    /// of its children (per CanonicalModuleCatalog.AreaChildren) is granted.
    /// </summary>
    public bool IsAreaGranted(string areaId) =>
        CanonicalModuleCatalog.AreaChildren.TryGetValue(areaId, out var children)
        && children.Any(c => EffectiveGrants.ContainsKey(c));

    /// <summary>
    /// Effective grant map for display: moduleId → granted capability id set.
    /// Sourced from the stored override (if set) else the user's template on GET;
    /// rebuilt from the posted module checkboxes on a failed POST re-render.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> EffectiveGrants { get; private set; }
        = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

    /// <summary>True when the user's persisted template grants the admin module (Job On excluded).</summary>
    public bool TemplateGrantsAdmin { get; private set; }

    /// <summary>Posted module checkboxes: moduleId → posted values (capability ids, or "true" for grant-only rows).</summary>
    [BindProperty]
    public Dictionary<string, string[]> Modules { get; set; } = new(StringComparer.Ordinal);

    /// <summary>True when the posted (re-rendered) set includes the admin module.</summary>
    public bool PostedGrantsAdmin => Modules.ContainsKey(CanonicalModuleCatalog.AdminModuleId);

    public async Task<IActionResult> OnGetAsync(string id)
    {
        await LoadAsync(id);
        if (Entry is null)
            return Page();

        EffectiveGrants = ResolveEffectiveDisplayGrants(Entry);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(
        string id, string displayName, string? profileTitle, string templateId,
        bool active, string version)
    {
        if (!DateTimeOffset.TryParse(version, out var expectedVersion))
        {
            ModelState.AddModelError(string.Empty, "Versão de concorrência inválida.");
            await LoadAsync(id);
            return Page();
        }

        var result = await _users.SaveUserWithModulesAsync(
            id, displayName, profileTitle, templateId, active, expectedVersion,
            ReadPostedGrants(),
            HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message);
            await LoadAsync(id);
            EffectiveGrants = PostedToGrantMap(Modules);
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

    /// <summary>
    /// The grant entries to persist, derived from the posted checkbox groups:
    /// one entry per posted module; capabilities are the posted values that are
    /// owned by that canonical module (the "true" grant-only marker contributes
    /// no capability). Modules with none of their capabilities posted (and not
    /// posted at all) are simply absent — i.e. ungranted. Returns a possibly
    /// EMPTY list so the composite save always persists the module override
    /// (an all-unchecked form intentionally clears module access); never null.
    /// </summary>
    private List<TemplateGrantInput> ReadPostedGrants()
    {
        var grants = new List<TemplateGrantInput>();
        if (Modules.Count == 0)
            return grants;

        foreach (var (moduleId, values) in Modules)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                continue;
            var module = CanonicalModuleCatalog.Instance.Modules
                .FirstOrDefault(m => m.ModuleId == moduleId);
            if (module is null || module.Kind == ModuleKind.FunctionalArea)
                continue;   // functional areas are not grantable — grant their children

            var owned = module.Capabilities.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
            var capabilities = (values ?? [])
                .Where(v => owned.Contains(v))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            grants.Add(new TemplateGrantInput(moduleId, capabilities));
        }
        return grants;
    }

    /// <summary>Rebuilds the display grant map from posted checkbox groups.</summary>
    private static IReadOnlyDictionary<string, IReadOnlySet<string>> PostedToGrantMap(
        Dictionary<string, string[]> modules)
    {
        var map = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var (moduleId, values) in modules)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                continue;
            var caps = (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v) && v != "true")
                .ToHashSet(StringComparer.Ordinal);
            map[moduleId] = caps;
        }
        return map;
    }

    /// <summary>
    /// Resolves the grants shown on load: the stored per-user override when set
    /// (and non-empty), otherwise the user's template grants.
    /// </summary>
    private IReadOnlyDictionary<string, IReadOnlySet<string>> ResolveEffectiveDisplayGrants(
        AdminUserRow user)
    {
        var template = Templates.FirstOrDefault(t => t.TemplateId == user.TemplateId);
        var source = !string.IsNullOrWhiteSpace(user.ModulesOverrideJson)
            ? user.ModulesOverrideJson
            : template?.ModulesJson;

        var parsed = AccessTemplateGrantsParser.Parse(source);
        if (parsed.IsFailure)
            return new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        return parsed.Value
            .Where(g => !string.IsNullOrWhiteSpace(g.ModuleId))
            .ToDictionary(
                g => g.ModuleId,
                g => (IReadOnlySet<string>)new HashSet<string>(
                    g.Capabilities ?? [], StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private async Task LoadAsync(string id)
    {
        Templates = await _templates.ListAsync(HttpContext.RequestAborted);
        var user = await _users.GetAsync(id, HttpContext.RequestAborted);
        ServiceErrorMessage = user.IsFailure
            && user.Error.Category == ErrorCategory.BackendUnavailable
            ? user.Error.Message
            : null;
        // A BackendUnavailable (schema-migration missing) is a service error,
        // NOT a missing user — never collapse it to a false 404.
        Entry = user.IsSuccess ? user.Value : null;
        if (Entry is not null)
        {
            // Job On exclusion keyed to the PERSISTED template (the server guard
            // evaluates existing.TemplateId on save), matching contract §6.7.
            var template = Templates.FirstOrDefault(t => t.TemplateId == Entry.TemplateId);
            TemplateGrantsAdmin = false;
            if (template is not null)
            {
                var parsedTemplate = AccessTemplateGrantsParser.Parse(template.ModulesJson);
                TemplateGrantsAdmin = parsedTemplate.IsSuccess
                    && parsedTemplate.Value.Any(g => g.ModuleId == CanonicalModuleCatalog.AdminModuleId);
            }
        }
    }
}