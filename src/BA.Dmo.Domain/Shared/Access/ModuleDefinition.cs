namespace BA.Dmo.Domain.Shared.Access;

/// <summary>
/// One entry of the controlled module catalog (Plan-V3 TD-10, GLM-CAT-02):
/// moduleId, display name, kind, canonical order, initial route and declared capabilities.
/// New modules enter only through code/spec/schema/tests (GLM-CORE-07); the Administration
/// assigns entries of this catalog and never invents identifiers.
/// </summary>
public sealed record ModuleDefinition
{
    public string ModuleId { get; }

    public string DisplayName { get; }

    public ModuleKind Kind { get; }

    /// <summary>Canonical display/navigation order of the catalog (GLM-CAT-02 "Ordem").</summary>
    public int CanonicalOrder { get; }

    /// <summary>Initial route of the module (e.g. "/jobon").</summary>
    public string InitialRoute { get; }

    public IReadOnlyList<Capability> Capabilities { get; }

    /// <summary>
    /// Whether Administration may assign this entry through an access template.
    /// Technical catalog entries may remain addressable by authorization policies
    /// while being derived from a parent module instead of assigned directly.
    /// </summary>
    public bool IsAssignable { get; }

    public ModuleDefinition(
        string moduleId,
        string displayName,
        ModuleKind kind,
        int canonicalOrder,
        string initialRoute,
        IEnumerable<Capability>? capabilities = null,
        bool isAssignable = true)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            throw new ArgumentException("Module id must not be empty.", nameof(moduleId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name must not be empty.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(initialRoute))
            throw new ArgumentException("Initial route must not be empty.", nameof(initialRoute));

        var normalizedModuleId = moduleId.Trim();
        if (normalizedModuleId.Contains(' ', StringComparison.Ordinal))
            throw new ArgumentException("Module id must not contain whitespace.", nameof(moduleId));

        var normalizedRoute = initialRoute.Trim();
        if (!normalizedRoute.StartsWith('/'))
            throw new ArgumentException("Initial route must start with '/'.", nameof(initialRoute));

        ModuleId = normalizedModuleId;
        DisplayName = displayName.Trim();
        Kind = kind;
        CanonicalOrder = canonicalOrder;
        InitialRoute = normalizedRoute;
        Capabilities = capabilities is null
            ? Array.Empty<Capability>()
            : capabilities.ToList().AsReadOnly();
        IsAssignable = isAssignable;
    }
}
