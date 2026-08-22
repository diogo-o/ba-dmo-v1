namespace BA.Dmo.Domain.Shared.Access;

/// <summary>
/// Server-side view of the authenticated internal user for the current request
/// (Plan-V3 GLM-ARCH-03: identity + grants resolved per request).
/// Grants are the authorized modules and capabilities of the user's active access template.
/// This is a read-only projection: authorization decisions are always re-resolved server-side
/// and never trusted from the client (03_ARCH §14, GLM-ARCH-18).
/// </summary>
public sealed record CurrentUser
{
    public Guid InternalUserId { get; }

    public string DisplayName { get; }

    /// <summary>Module ids granted by the active access template.</summary>
    public IReadOnlySet<string> Modules { get; }

    /// <summary>Capability ids granted by the active access template ({moduleId}.{ação}).</summary>
    public IReadOnlySet<string> Capabilities { get; }

    public CurrentUser(
        Guid internalUserId,
        string displayName,
        IEnumerable<string> modules,
        IEnumerable<string> capabilities)
    {
        if (internalUserId == Guid.Empty)
            throw new ArgumentException("Internal user id must not be empty.", nameof(internalUserId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name must not be empty.", nameof(displayName));

        InternalUserId = internalUserId;
        DisplayName = displayName.Trim();
        Modules = Normalize(modules);
        Capabilities = Normalize(capabilities);
    }

    public bool HasModule(string moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId) && Modules.Contains(moduleId);

    public bool HasCapability(string capabilityId) =>
        !string.IsNullOrWhiteSpace(capabilityId) && Capabilities.Contains(capabilityId);

    private static IReadOnlySet<string> Normalize(IEnumerable<string>? values) =>
        values is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(
                values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()),
                StringComparer.Ordinal);
}
