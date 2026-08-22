namespace BA.Dmo.Domain.Shared.Access;

/// <summary>
/// A capability granted by a module: "{moduleId}.{ação}" (Plan-V3 GLM-CAT-01/GLM-CAT-03).
/// A capability concedes specific operations beyond module entry; it never concedes module
/// entry by itself, and displayed titles never grant power (GLM-CORE-07).
/// </summary>
public sealed record Capability
{
    public string Id { get; }

    /// <summary>The module id segment of the capability id (text before the first '.').</summary>
    public string ModuleSegment { get; }

    public Capability(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Capability id must not be empty.", nameof(id));

        var normalized = id.Trim();
        if (normalized.Contains(' ', StringComparison.Ordinal))
            throw new ArgumentException("Capability id must not contain whitespace.", nameof(id));

        var separator = normalized.IndexOf('.');
        if (separator <= 0 || separator == normalized.Length - 1)
            throw new ArgumentException(
                "Capability id must use the '{moduleId}.{ação}' format.", nameof(id));

        Id = normalized;
        ModuleSegment = normalized[..separator];
    }

    public override string ToString() => Id;
}
