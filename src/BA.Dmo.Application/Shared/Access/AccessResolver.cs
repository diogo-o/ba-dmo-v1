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
/// Effective access resolved from assigned modules plus the user's functional
/// profile. Templates decide WHAT is accessible; the profile decides HOW the
/// user behaves inside confirmed profile-dependent modules.
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
    /// Resolves one active template for a functional profile. Capability arrays
    /// stored in legacy template JSON are deliberately not authorization input:
    /// only the normalized assignable module ids are used.
    /// </summary>
    public EffectiveAccess Resolve(
        AccessTemplateDefinition template,
        FunctionalProfile profile) =>
        Resolve([template], profile);

    /// <summary>
    /// Resolves the union of one-or-more associated templates. Inactive
    /// templates contribute nothing; duplicate module grants collapse to one.
    /// </summary>
    public EffectiveAccess Resolve(
        IEnumerable<AccessTemplateDefinition> templates,
        FunctionalProfile profile)
    {
        ArgumentNullException.ThrowIfNull(templates);

        var activeTemplates = templates.Where(template => template is not null && template.Active).ToList();
        if (activeTemplates.Count == 0)
            return new EffectiveAccess(
                Array.Empty<ModuleDefinition>(),
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<ModuleDefinition>>());

        var modules = new HashSet<string>(StringComparer.Ordinal);
        var capabilities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var template in activeTemplates)
        {
            var normalized = _normalizer.Normalize(template.Grants);
            foreach (var grant in normalized.Grants)
                modules.Add(grant.ModuleId);
        }

        // Admin is a pure functional profile. Mixing it with operational
        // modules cannot manufacture a fourth profile or widen access.
        if (profile == FunctionalProfile.Admin)
        {
            modules.RemoveWhere(id => id != CanonicalModuleCatalog.AdminModuleId);
        }
        else
        {
            modules.Remove(CanonicalModuleCatalog.AdminModuleId);
        }

        // Controlo is the single grant. Peso and Pegamentos remain technical
        // policy/page ids so existing internal routes keep working.
        if (modules.Contains(CanonicalModuleCatalog.ControloAreaId))
        {
            modules.Add(CanonicalModuleCatalog.PesoModuleId);
            modules.Add(CanonicalModuleCatalog.PegamentosModuleId);
        }

        // História is a derived transversal read surface, never an assignable
        // module. It exists only when the profile has an operational module.
        if (profile != FunctionalProfile.Admin && modules.Count > 0)
            modules.Add(CanonicalModuleCatalog.HistoriaModuleId);

        ProjectProfileCapabilities(profile, modules, capabilities);

        // Defensive catalog intersection for derived technical ids.
        modules.RemoveWhere(id => !_catalog.ContainsModule(id));

        var navigationModules = _catalog.Modules
            .Where(m => m.Kind == ModuleKind.Module && modules.Contains(m.ModuleId))
            .ToList();

        var visibleAreaChildren = new Dictionary<string, IReadOnlyList<ModuleDefinition>>();
        foreach (var (areaId, childIds) in _areaChildren)
        {
            if (!modules.Contains(areaId) || !_catalog.TryGetModule(areaId, out _))
                continue;

            var authorizedChildren = childIds
                .Where(modules.Contains)
                .Select(id => _catalog.TryGetModule(id, out var child) ? child : null)
                .Where(child => child is not null)
                .Cast<ModuleDefinition>()
                .ToList();

            if (authorizedChildren.Count > 0)
                visibleAreaChildren[areaId] = authorizedChildren;
        }

        return new EffectiveAccess(navigationModules, modules, capabilities, visibleAreaChildren);
    }

    private static void ProjectProfileCapabilities(
        FunctionalProfile profile,
        IReadOnlySet<string> modules,
        ISet<string> capabilities)
    {
        if (profile == FunctionalProfile.Admin &&
            modules.Contains(CanonicalModuleCatalog.AdminModuleId))
        {
            capabilities.Add(CanonicalModuleCatalog.AdminGerirCapabilityId);
            capabilities.Add(CanonicalModuleCatalog.AuditViewCapabilityId);
            capabilities.Add(CanonicalModuleCatalog.AuditExportCapabilityId);
            return;
        }

        if (modules.Contains(CanonicalModuleCatalog.JobonModuleId))
        {
            capabilities.Add(CanonicalModuleCatalog.JobonViewCapabilityId);
            capabilities.Add(CanonicalModuleCatalog.JobonConfirmarCapabilityId);
            if (profile == FunctionalProfile.Responsible)
            {
                capabilities.Add(CanonicalModuleCatalog.JobonEditCapabilityId);
                capabilities.Add(CanonicalModuleCatalog.JobonConfigureCapabilityId);
            }
        }

        // Confirmed Ferramentas variant: the responsible profile owns master
        // data edits; the operator/control profile remains operational read/use.
        if (profile == FunctionalProfile.Responsible &&
            modules.Contains(CanonicalModuleCatalog.FerramentasModuleId))
        {
            capabilities.Add(CanonicalModuleCatalog.FerramentasConfigureCapabilityId);
        }

        if (!modules.Contains(CanonicalModuleCatalog.ControloAreaId))
            return;

        capabilities.Add(CanonicalModuleCatalog.ControloViewCapabilityId);
        if (profile == FunctionalProfile.OperatorController)
        {
            capabilities.Add(CanonicalModuleCatalog.ControloEditCapabilityId);
            capabilities.Add(CanonicalModuleCatalog.ControloSubmitCapabilityId);
        }
        else if (profile == FunctionalProfile.Responsible)
        {
            capabilities.Add(CanonicalModuleCatalog.ControloReviewCapabilityId);
            capabilities.Add(CanonicalModuleCatalog.PesoAprovarCapabilityId);
        }
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
