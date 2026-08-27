using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.Application.Shared.Access;

/// <summary>
/// Canonical configuration is never silently repaired: any inconsistency fails
/// explicitly and deterministically with the full violation list.
/// </summary>
public sealed class CatalogValidationException(IReadOnlyList<string> violations)
    : Exception(
        "Catalog validation failed: " + string.Join(" | ", violations))
{
    public IReadOnlyList<string> Violations { get; } = violations;
}

/// <summary>
/// Validators of the canonical catalog configuration (Plan-V3 U-04,
/// GLM-ACC-03, GLM-CAT): module/capability/page uniqueness, route grammar,
/// cross-references (page→module, capability→module, required capability),
/// single landing, area children existence. Runs at composition time; an
/// invalid canonical build must fail loudly, never self-repair.
/// </summary>
public static class CatalogValidator
{
    public static void Validate(
        ModuleCatalog modules,
        PageCatalog pages,
        IReadOnlyDictionary<string, IReadOnlyList<string>> areaChildren)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(areaChildren);

        var violations = new List<string>();

        ValidateCapabilityOwnership(modules, violations);
        ValidateModuleRoutes(modules, violations);
        ValidatePageReferences(modules, pages, violations);
        ValidateLanding(pages, violations);
        ValidateAreaChildren(modules, areaChildren, violations);

        if (violations.Count > 0)
            throw new CatalogValidationException(violations);
    }

    private static void ValidateCapabilityOwnership(ModuleCatalog modules, List<string> violations)
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var module in modules.Modules)
        {
            foreach (var capability in module.Capabilities)
            {
                if (owners.TryGetValue(capability.Id, out var existingOwner))
                {
                    violations.Add(
                        $"capability '{capability.Id}' declared by both '{existingOwner}' " +
                        $"and '{module.ModuleId}' (capability ids must be unique)");
                }
                else
                {
                    owners[capability.Id] = module.ModuleId;
                }
            }
        }
    }

    private static void ValidateModuleRoutes(ModuleCatalog modules, List<string> violations)
    {
        var routes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in modules.Modules)
        {
            if (!PageDefinition.IsValidRoute(module.InitialRoute))
                violations.Add(
                    $"module '{module.ModuleId}' initial route '{module.InitialRoute}' " +
                    "violates the canonical route grammar");
            if (!routes.Add(module.InitialRoute))
                violations.Add($"duplicate initial route '{module.InitialRoute}' in module catalog");
        }
    }

    private static void ValidatePageReferences(
        ModuleCatalog modules, PageCatalog pages, List<string> violations)
    {
        // Duplicate capability declarations are reported by
        // ValidateCapabilityOwnership; here we map first-owner so validation
        // keeps collecting every violation instead of crashing.
        var capabilityOwners = modules.Modules
            .SelectMany(m => m.Capabilities.Select(c => (Capability: c.Id, Module: m.ModuleId)))
            .GroupBy(x => x.Capability, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Module, StringComparer.Ordinal);

        foreach (var page in pages.Pages)
        {
            if (!modules.ContainsModule(page.ModuleId))
            {
                violations.Add($"page '{page.PageId}' references unknown module '{page.ModuleId}'");
                continue;
            }

            if (page.RequiredCapabilityId is null)
                continue;

            if (!capabilityOwners.TryGetValue(page.RequiredCapabilityId, out var owner))
            {
                violations.Add(
                    $"page '{page.PageId}' requires unknown capability '{page.RequiredCapabilityId}'");
            }
            else if (!string.Equals(owner, page.ModuleId, StringComparison.Ordinal))
            {
                violations.Add(
                    $"page '{page.PageId}' (module '{page.ModuleId}') requires capability " +
                    $"'{page.RequiredCapabilityId}' owned by module '{owner}'");
            }
        }
    }

    private static void ValidateLanding(PageCatalog pages, List<string> violations)
    {
        var landings = pages.Pages.Where(p => p.IsLanding).ToList();
        if (landings.Count == 0)
            violations.Add("page catalog defines no landing page (UD-16 requires exactly one)");
        if (landings.Count > 1)
            violations.Add(
                "page catalog defines more than one landing page: " +
                string.Join(", ", landings.Select(p => p.PageId)));
        if (landings.Count == 1 && !landings[0].IsActive)
            violations.Add($"landing page '{landings[0].PageId}' is inactive");
    }

    private static void ValidateAreaChildren(
        ModuleCatalog modules,
        IReadOnlyDictionary<string, IReadOnlyList<string>> areaChildren,
        List<string> violations)
    {
        foreach (var (areaId, childIds) in areaChildren)
        {
            if (!modules.TryGetModule(areaId, out var area))
            {
                violations.Add($"area '{areaId}' is not a catalog entry");
                continue;
            }

            if (area.Kind != ModuleKind.Module || !area.IsAssignable)
                violations.Add($"area parent '{areaId}' is not an assignable module");

            foreach (var childId in childIds)
            {
                if (!modules.TryGetModule(childId, out var child))
                    violations.Add($"area '{areaId}' references unknown child '{childId}'");
                else if (child.IsAssignable)
                    violations.Add(
                        $"area '{areaId}' child '{childId}' must not be independently assignable");
            }
        }
    }
}
