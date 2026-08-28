using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.Application.Shared.Access;

/// <summary>
/// One derived navigation item (Plan-V3 GLM-SHL-03): a module tab or a
/// legacy functional-area group. Items are DERIVED from the
/// resolved grants ∩ canonical catalog in canonical order; unauthorized
/// entries are never produced, so they can never be rendered (GLM-SHL-03.6).
/// </summary>
public abstract record NavigationItem(string Id, string Label, string Route, bool IsActive);

/// <summary>Module tab (left navigation or right-aligned Administração).</summary>
public sealed record NavigationTab(string Id, string Label, string Route, bool IsActive)
    : NavigationItem(Id, Label, Route, IsActive);

/// <summary>
/// Derived shell navigation (GLM-SHL-03): operational tabs on the left in
/// canonical catalog order; Administração is a separate right-aligned entry.
/// </summary>
public sealed record ShellNavigation(
    IReadOnlyList<NavigationItem> LeftItems,
    NavigationTab? AdminEntry);

/// <summary>
/// Derives the shell navigation from the effective access surface
/// (Plan-V3 GLM-SHL-01.3: navigation does not live in markup; GLM-SHL-03).
/// Pure and deterministic: no role-name branching — only catalog order and
/// resolved grants decide what exists.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Builds the navigation for an access surface. <paramref name="currentRoute"/>
    /// (best-effort, query-free path) marks the active item; unknown routes
    /// leave every item inactive.
    /// </summary>
    ShellNavigation Build(EffectiveAccess access, string? currentRoute);
}

/// <summary>
/// Canonical navigation derivation (GLM-SHL-03, GLM-CTR-02, GLM-CAT-02):
/// tabs = authorized top-level modules ∩ catalog in canonical order. Controlo
/// is one global entry; Peso/Pegamentos are technical pages inside it and make
/// that parent entry active when visited. Administração is right-aligned and
/// only exists when the admin page is accessible (admin.gerir).
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly PageCatalog _pages;
    private readonly AccessResolver _resolver;
    private readonly ModuleCatalog _catalog;

    public NavigationService(PageCatalog pages, AccessResolver resolver, ModuleCatalog catalog)
    {
        _pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public ShellNavigation Build(EffectiveAccess access, string? currentRoute)
    {
        ArgumentNullException.ThrowIfNull(access);

        var activeModuleId = ResolveActiveModuleId(access, currentRoute);
        var areaChildIds = AreaChildIds();

        var leftItems = new List<NavigationItem>();
        foreach (var module in access.NavigationModules)
        {
            // Administração is right-aligned (GLM-SHL-03.1), never a left tab;
            // internal area children never render as global shell tabs.
            if (module.ModuleId == CanonicalModuleCatalog.AdminModuleId ||
                areaChildIds.Contains(module.ModuleId))
                continue;

            var tab = BuildTab(access, module.ModuleId, activeModuleId);
            if (tab is not null)
                leftItems.Add(tab);
        }

        // Tabs are always emitted in canonical catalog order.
        leftItems.Sort((a, b) => OrderOf(a.Id).CompareTo(OrderOf(b.Id)));

        NavigationTab? adminEntry = null;
        if (_pages.TryGetById(CanonicalPageCatalog.AdminGestaoPageId, out var adminPage) &&
            _resolver.IsPageAccessible(access, adminPage))
        {
            adminEntry = new NavigationTab(
                adminPage.ModuleId,
                ModuleName(adminPage.ModuleId),
                adminPage.Route,
                ActiveFor(adminPage.ModuleId));
        }

        return new ShellNavigation(leftItems, adminEntry);

        bool ActiveFor(string moduleId) =>
            activeModuleId is not null &&
            string.Equals(activeModuleId, moduleId, StringComparison.Ordinal);
    }

    private NavigationTab? BuildTab(
        EffectiveAccess access, string moduleId, string? activeModuleId)
    {
        // Entry route = FIRST accessible page of the module. For Peso this
        // resolves the experience automatically: peso.operador is inaccessible
        // to peso.aprovar holders and peso.responsavel requires the capability
        // (GLM-ACC-05 — one entry, no manual selector).
        var firstPage = _pages.Pages
            .Where(p => p.ModuleId == moduleId && _resolver.IsPageAccessible(access, p))
            .FirstOrDefault();
        if (firstPage is null)
            return null;

        return new NavigationTab(
            moduleId,
            ModuleName(moduleId),
            firstPage.Route,
            IsModuleOrAreaActive(moduleId, activeModuleId));
    }

    private static bool IsModuleOrAreaActive(string moduleId, string? activeModuleId)
    {
        if (activeModuleId is null)
            return false;

        if (string.Equals(activeModuleId, moduleId, StringComparison.Ordinal))
            return true;

        return CanonicalModuleCatalog.AreaChildren.TryGetValue(moduleId, out var childIds)
            && childIds.Contains(activeModuleId, StringComparer.Ordinal);
    }

    private string? ResolveActiveModuleId(EffectiveAccess access, string? currentRoute)
    {
        if (string.IsNullOrWhiteSpace(currentRoute))
            return null;

        var path = currentRoute.Split('?')[0].TrimEnd('/');
        if (path.Length == 0)
            path = "/";

        if (_pages.TryGetByRoute(path, out var page))
            return page.ModuleId;

        // Sub-pages of a module (e.g. /admin/users) keep the module entry
        // active: they are all guarded by the same module authorization.
        foreach (var candidate in _pages.Pages)
        {
            if (path.StartsWith(candidate.Route + "/", StringComparison.Ordinal) &&
                access.HasModule(candidate.ModuleId))
                return candidate.ModuleId;
        }

        return null;
    }

    private static HashSet<string> AreaChildIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var children in CanonicalModuleCatalog.AreaChildren.Values)
            foreach (var child in children)
                ids.Add(child);
        return ids;
    }

    private int OrderOf(string moduleId) =>
        _catalog.TryGetModule(moduleId, out var module) ? module.CanonicalOrder : int.MaxValue;

    private string ModuleName(string moduleId) =>
        _catalog.TryGetModule(moduleId, out var module) ? module.DisplayName : moduleId;
}
