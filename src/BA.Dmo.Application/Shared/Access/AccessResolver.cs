using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.Application.Shared.Access;

/// <summary>
/// Outcome of resolving the first accessible page.
/// </summary>
public enum FirstPageOutcome
{
    /// <summary>Global landing policy applied (UD-16: Job On).</summary>
    Landing,

    /// <summary>
    /// Landing genuinely unavailable under the canonical contract; the first
    /// accessible page in canonical display order was used.
    /// </summary>
    FallbackCanonicalOrder,

    /// <summary>Safe "no access" state (GLM-SHL-06; no data, no redirect loop).</summary>
    NoAccess
}

/// <summary>Result of first-page resolution: page (when accessible) + outcome.</summary>
public sealed record FirstPageResolution(FirstPageOutcome Outcome, PageDefinition? Page);

/// <summary>
/// Effective access resolved from an ACTIVE template against the catalogs
/// (Plan-V3 GLM-ACC-02/03, GLM-SHL-03/04). Pure and deterministic: no
/// role-name branching anywhere — behavior derives only from catalog entries
/// and template grants.
/// </summary>
public sealed class EffectiveAccess
{
    private readonly IReadOnlySet<string> _modules;
    private readonly IReadOnlySet<string> _capabilities;

    internal EffectiveAccess(
        IReadOnlyList<ModuleDefinition> navigationModules,
        IReadOnlySet<string> modules,
        IReadOnlySet<string> capabilities,
        IReadOnlyDictionary<string, IReadOnlyList<ModuleDefinition>> visibleAreaChildren)
    {
        NavigationModules = navigationModules;
        _modules = modules;
        _capabilities = capabilities;
        VisibleAreaChildren = visibleAreaChildren;
    }

    /// <summary>True when the template produced no usable access surface.</summary>
    public bool IsEmpty => NavigationModules.Count == 0 && _capabilities.Count == 0;

    /// <summary>Authorized MODULE entries in canonical catalog order (tabs).</summary>
    public IReadOnlyList<ModuleDefinition> NavigationModules { get; }

    /// <summary>
    /// Functional areas visible because at least one child is authorized,
    /// with ONLY the authorized children, in canonical order (GLM-CTR-02).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ModuleDefinition>> VisibleAreaChildren { get; }

    public bool HasModule(string moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId) && _modules.Contains(moduleId);

    public bool HasCapability(string capabilityId) =>
        !string.IsNullOrWhiteSpace(capabilityId) && _capabilities.Contains(capabilityId);

    internal IReadOnlySet<string> Modules => _modules;

    internal IReadOnlySet<string> Capabilities => _capabilities;

    /// <summary>Authorized module ids (consumed by identity/session wiring, U-05).</summary>
    public IReadOnlyCollection<string> AuthorizedModuleIds => _modules;

    /// <summary>Granted capability ids (consumed by identity/session wiring, U-05).</summary>
    public IReadOnlyCollection<string> GrantedCapabilityIds => _capabilities;
}

/// <summary>
/// Resolves the effective access surface and the first accessible page of an
/// access template (U-04 resolver contract; consumed by auth/shell from U-05).
/// </summary>
public sealed class AccessResolver
{
    private readonly ModuleCatalog _catalog;
    private readonly PageCatalog _pages;
    private readonly GrantNormalizer _normalizer;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _areaChildren;

    public AccessResolver(
        ModuleCatalog catalog,
        PageCatalog pages,
        IReadOnlyDictionary<string, IReadOnlyList<string>> areaChildren)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _areaChildren = areaChildren ?? throw new ArgumentNullException(nameof(areaChildren));
        _normalizer = new GrantNormalizer(catalog);
    }

    /// <summary>
    /// Resolves the effective access of a template. Inactive templates grant
    /// nothing (GLM-ACC-01.6 → safe state). Job On query access (jobon.view)
    /// is universal for every active user (UD-16, GLM-SHL-03.1/03.4) and is
    /// added by the resolver, never by role-name branching — except that
    /// templates holding the admin module never receive it (owner decision:
    /// an admin's working area is Administração).
    /// </summary>
    public EffectiveAccess Resolve(AccessTemplateDefinition template)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (!template.Active)
            return new EffectiveAccess(
                Array.Empty<ModuleDefinition>(),
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<ModuleDefinition>>());

        var normalized = _normalizer.Normalize(template.Grants);

        var modules = new HashSet<string>(StringComparer.Ordinal);
        var capabilities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grant in normalized.Grants)
        {
            modules.Add(grant.ModuleId);
            foreach (var capability in grant.Capabilities)
                capabilities.Add(capability);
        }

        // UD-16 / GLM-SHL-03: Job On (consulta) is present for ALL active
        // users via jobon.view — including users whose template has zero
        // operational tabs.
        // EXCEPTION (owner decision): any template holding the admin module
        // is excluded from the universal jobon.view: an admin never receives
        // Job On (even read-only), and the first page falls back to the first
        // accessible module in canonical order (for the standard bootstrap
        // admin that is /admin).
        if (!modules.Contains(CanonicalModuleCatalog.AdminModuleId) &&
            _catalog.ContainsModule(CanonicalModuleCatalog.JobonModuleId))
        {
            modules.Add(CanonicalModuleCatalog.JobonModuleId);
            capabilities.Add(CanonicalModuleCatalog.JobonViewCapabilityId);
        }

        var navigationModules = _catalog.Modules
            .Where(m => m.Kind == ModuleKind.Module && modules.Contains(m.ModuleId))
            .ToList();

        var visibleAreaChildren = new Dictionary<string, IReadOnlyList<ModuleDefinition>>();
        foreach (var (areaId, childIds) in _areaChildren)
        {
            if (!_catalog.TryGetModule(areaId, out _))
                continue;

            var authorizedChildren = childIds
                .Where(modules.Contains)
                .Select(id => _catalog.TryGetModule(id, out var child) ? child : null)
                .Where(child => child is not null)
                .Cast<ModuleDefinition>()
                .ToList();

            // Area without authorized children never appears (GLM-CTR-02.4).
            if (authorizedChildren.Count > 0)
                visibleAreaChildren[areaId] = authorizedChildren;
        }

        return new EffectiveAccess(navigationModules, modules, capabilities, visibleAreaChildren);
    }

    /// <summary>Whether a catalog page is accessible with the resolved access.</summary>
    public bool IsPageAccessible(EffectiveAccess access, PageDefinition page)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(page);

        if (!page.IsActive || !access.HasModule(page.ModuleId))
            return false;

        // Peso experience exclusivity (UD-06/GLM-ACC-05): the Operador page is
        // never shown to holders of peso.aprovar (and vice versa — the
        // Responsável page requires the capability). Capability-driven, never
        // role-name driven.
        if (page.PageId == CanonicalPageCatalog.PesoOperadorPageId &&
            access.HasCapability(CanonicalModuleCatalog.PesoAprovarCapabilityId))
            return false;

        return page.RequiredCapabilityId is null
            || access.HasCapability(page.RequiredCapabilityId);
    }

    /// <summary>All accessible pages in canonical display order.</summary>
    public IReadOnlyList<PageDefinition> AccessiblePages(EffectiveAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return _pages.Pages.Where(p => IsPageAccessible(access, p)).ToList();
    }

    /// <summary>
    /// First-page resolution (UD-16/DS-01 + owner-confirmed design rule):
    /// an authenticated functional user lands on the Job On landing; a
    /// template holding the admin module (no jobon.view by owner decision)
    /// falls back to the first accessible page in canonical display order —
    /// for the bootstrap admin that is /admin. Only when Job On is genuinely
    /// unavailable under the canonical contract (landing page absent/inactive,
    /// or not accessible) does the deterministic fallback apply. With no
    /// accessible page the result is the explicit NoAccess state (GLM-SHL-06).
    /// Template.PreferredFirstPageId is intentionally NOT consulted in V1
    /// (05_SHL §4).
    /// </summary>
    public FirstPageResolution ResolveFirstPage(EffectiveAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        var landing = _pages.LandingPage;
        if (landing is not null && IsPageAccessible(access, landing))
            return new FirstPageResolution(FirstPageOutcome.Landing, landing);

        var fallback = AccessiblePages(access).FirstOrDefault();
        return fallback is not null
            ? new FirstPageResolution(FirstPageOutcome.FallbackCanonicalOrder, fallback)
            : new FirstPageResolution(FirstPageOutcome.NoAccess, null);
    }

    /// <summary>
    /// First entry of a functional area: the first AUTHORIZED child in
    /// canonical order (GLM-CAT-02 rule 1: área → primeira entrada filha
    /// autorizada). Null when the area is not visible.
    /// </summary>
    public PageDefinition? ResolveAreaFirstPage(EffectiveAccess access, string areaId)
    {
        ArgumentNullException.ThrowIfNull(access);

        if (!access.VisibleAreaChildren.TryGetValue(areaId, out var children))
            return null;

        foreach (var child in children)
        {
            var firstPage = _pages.Pages
                .Where(p => p.ModuleId == child.ModuleId && IsPageAccessible(access, p))
                .FirstOrDefault();
            if (firstPage is not null)
                return firstPage;
        }

        return null;
    }
}
