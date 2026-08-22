using System.Text.RegularExpressions;

namespace BA.Dmo.Application.Shared.Access;

/// <summary>
/// One page of the canonical page catalog (Plan-V3 05_SHL §5 route contract).
/// Route grammar (canonical pattern): ^/[a-z][a-z0-9-]*(?:/[a-z][a-z0-9-]*)*$
/// A page with no RequiredCapabilityId requires module entry (module granted
/// in the template); with one, the capability is additionally required
/// (e.g. /peso/responsavel → peso.aprovar, /admin → admin.gerir).
/// </summary>
public sealed record PageDefinition
{
    private static readonly Regex RouteGrammar = new(
        @"^/[a-z][a-z0-9-]*(?:/[a-z][a-z0-9-]*)*$",
        RegexOptions.Compiled);

    public string PageId { get; }

    public string ModuleId { get; }

    public string Route { get; }

    /// <summary>Capability required beyond module entry; null = module entry only.</summary>
    public string? RequiredCapabilityId { get; }

    /// <summary>Catalog display order (navigation derivation uses canonical order).</summary>
    public int DisplayOrder { get; }

    /// <summary>Inactive pages are never resolved or rendered.</summary>
    public bool IsActive { get; }

    /// <summary>
    /// Global landing page (UD-16/DS-01): Job On is the landing of every
    /// authenticated user; it is not configurable per user/template.
    /// </summary>
    public bool IsLanding { get; }

    public PageDefinition(
        string pageId,
        string moduleId,
        string route,
        string? requiredCapabilityId,
        int displayOrder,
        bool isActive = true,
        bool isLanding = false)
    {
        if (string.IsNullOrWhiteSpace(pageId))
            throw new ArgumentException("Page id must not be empty.", nameof(pageId));
        if (string.IsNullOrWhiteSpace(moduleId))
            throw new ArgumentException("Module id must not be empty.", nameof(moduleId));
        if (!IsValidRoute(route))
            throw new ArgumentException(
                $"Route '{route}' does not match the canonical route grammar " +
                "^/[a-z][a-z0-9-]*(?:/[a-z][a-z0-9-]*)*$.",
                nameof(route));

        PageId = pageId.Trim();
        ModuleId = moduleId.Trim();
        Route = route;
        RequiredCapabilityId = string.IsNullOrWhiteSpace(requiredCapabilityId)
            ? null
            : requiredCapabilityId.Trim();
        DisplayOrder = displayOrder;
        IsActive = isActive;
        IsLanding = isLanding;
    }

    public static bool IsValidRoute(string? route) =>
        !string.IsNullOrWhiteSpace(route) && RouteGrammar.IsMatch(route);
}

/// <summary>
/// Catalog of pages with unique ids and routes, exposed in display order.
/// </summary>
public sealed class PageCatalog
{
    private readonly Dictionary<string, PageDefinition> _byPageId;
    private readonly Dictionary<string, PageDefinition> _byRoute;
    private readonly List<PageDefinition> _inOrder;

    public PageCatalog(IEnumerable<PageDefinition> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        _byPageId = new Dictionary<string, PageDefinition>(StringComparer.Ordinal);
        _byRoute = new Dictionary<string, PageDefinition>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            ArgumentNullException.ThrowIfNull(page);
            if (!_byPageId.TryAdd(page.PageId, page))
                throw new ArgumentException($"Duplicate page id in catalog: '{page.PageId}'.");
            if (!_byRoute.TryAdd(page.Route, page))
                throw new ArgumentException($"Duplicate route in catalog: '{page.Route}'.");
        }

        _inOrder = _byPageId.Values
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.PageId, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<PageDefinition> Pages => _inOrder;

    public int Count => _inOrder.Count;

    public bool TryGetById(string pageId, out PageDefinition page)
    {
        if (!string.IsNullOrWhiteSpace(pageId))
            return _byPageId.TryGetValue(pageId, out page!);

        page = null!;
        return false;
    }

    public bool TryGetByRoute(string route, out PageDefinition page)
    {
        if (!string.IsNullOrWhiteSpace(route))
            return _byRoute.TryGetValue(route, out page!);

        page = null!;
        return false;
    }

    /// <summary>The single landing page when one is active; null otherwise.</summary>
    public PageDefinition? LandingPage =>
        _inOrder.FirstOrDefault(p => p.IsLanding && p.IsActive);
}
