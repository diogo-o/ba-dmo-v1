namespace BA.Dmo.Application.Shared.Access;

/// <summary>
/// One grant entry of an access template (Plan-V3 GLM-ACC-02):
/// { moduleId, capabilities: [] }. The presence of the module grants ENTRY;
/// capabilities grant specific operations. Grants are per template — no
/// per-user overrides in V1.
/// </summary>
public sealed record ModuleGrant
{
    public string ModuleId { get; init; }

    public IReadOnlyList<string> Capabilities { get; init; }

    public ModuleGrant(string moduleId, IReadOnlyList<string> capabilities)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            throw new ArgumentException("Grant module id must not be empty.", nameof(moduleId));

        ModuleId = moduleId.Trim();
        Capabilities = capabilities ?? Array.Empty<string>();
    }
}

/// <summary>
/// Access template model (Plan-V3 06_DATA §3.1 access_templates, GLM-ACC-02).
/// Templates define the user's allowed application surface; they are editable
/// in Administration (U-06) and validated server-side against the catalog on
/// every write (GLM-ACC-03). Role names never branch behavior: only grants do.
/// </summary>
public sealed record AccessTemplateDefinition
{
    public string TemplateId { get; }

    public string Name { get; }

    /// <summary>Inactive templates grant no access (GLM-ACC-01.6: ACCESS_TEMPLATE_INACTIVE).</summary>
    public bool Active { get; }

    public IReadOnlyList<ModuleGrant> Grants { get; }

    /// <summary>
    /// READ-ONLY, NOT USED in V1 (05_SHL §4): the landing is the fixed global
    /// Job On policy (UD-16) and is never configurable per user/template.
    /// Kept as data so future decisions can use it without schema churn.
    /// </summary>
    public string? PreferredFirstPageId { get; }

    public AccessTemplateDefinition(
        string templateId,
        string name,
        bool active,
        IEnumerable<ModuleGrant> grants,
        string? preferredFirstPageId = null)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            throw new ArgumentException("Template id must not be empty.", nameof(templateId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name must not be empty.", nameof(name));

        TemplateId = templateId.Trim();
        Name = name.Trim();
        Active = active;
        Grants = (grants ?? Enumerable.Empty<ModuleGrant>()).ToList().AsReadOnly();
        PreferredFirstPageId = string.IsNullOrWhiteSpace(preferredFirstPageId)
            ? null
            : preferredFirstPageId.Trim();
    }
}
