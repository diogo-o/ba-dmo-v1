namespace BA.Dmo.Domain.Shared.Access;

/// <summary>
/// Controlled module catalog foundation (Plan-V3 TD-10, GLM-CAT; 03_ARCH §2 Shared/Access).
/// The catalog in code is the source of truth for modules and capabilities; the DB mirror
/// (<c>module_catalog_mirror</c>, U-04) serves ordering/display for the Administration.
/// U-01 delivers the empty-functional foundation; the canonical entries of modules/00 are
/// registered in U-04 together with the mirror and server-side validation.
/// An empty catalog is a valid state (no module assigned yet).
/// </summary>
public sealed class ModuleCatalog
{
    private readonly Dictionary<string, ModuleDefinition> _byModuleId;
    private readonly List<ModuleDefinition> _inCanonicalOrder;
    private readonly HashSet<string> _capabilityIds;

    /// <summary>A catalog without entries. Valid: all queries answer "unknown"/empty.</summary>
    public static ModuleCatalog Empty { get; } = new(Array.Empty<ModuleDefinition>());

    public ModuleCatalog(IEnumerable<ModuleDefinition> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        _byModuleId = new Dictionary<string, ModuleDefinition>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            ArgumentNullException.ThrowIfNull(module);
            if (!_byModuleId.TryAdd(module.ModuleId, module))
                throw new ArgumentException(
                    $"Duplicate module id in catalog: '{module.ModuleId}'.");
        }

        _inCanonicalOrder = _byModuleId.Values
            .OrderBy(m => m.CanonicalOrder)
            .ThenBy(m => m.ModuleId, StringComparer.Ordinal)
            .ToList();

        _capabilityIds = _byModuleId.Values
            .SelectMany(m => m.Capabilities)
            .Select(c => c.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>All entries, ordered canonically (order, then module id).</summary>
    public IReadOnlyList<ModuleDefinition> Modules => _inCanonicalOrder;

    public int Count => _inCanonicalOrder.Count;

    public bool ContainsModule(string moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId) && _byModuleId.ContainsKey(moduleId);

    public bool TryGetModule(string moduleId, out ModuleDefinition module)
    {
        if (!string.IsNullOrWhiteSpace(moduleId))
            return _byModuleId.TryGetValue(moduleId, out module!);

        module = null!;
        return false;
    }

    /// <summary>Whether the capability id is declared by any catalog entry.</summary>
    public bool IsCapabilityKnown(string capabilityId) =>
        !string.IsNullOrWhiteSpace(capabilityId) && _capabilityIds.Contains(capabilityId);
}
