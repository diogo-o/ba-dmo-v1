using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Historia;

/// <summary>
/// U-18 — Server-side gate for the História transversal read (modules/11,
/// GLM-HIST-02, TD-24). Entry requires the <c>historia</c> module grant. On
/// success it resolves the ORIGIN scope: the identity only sees events of the
/// modules its active template grants (TD-24 — the view is limited to the
/// user's authorized modules). Admin events are additionally shown only to
/// identities holding <c>audit.view</c> (GLM-HIST-04 Administração).
///
/// Fails closed: no resolved identity or no <c>historia</c> grant yields
/// Forbidden from the view. An EMPTY visible origin scope is allowed — the view
/// then shows its empty state (GLM-HIST-06) and never reveals another module's
/// events.
/// </summary>
public sealed class HistoriaAuthorizationGate
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public HistoriaAuthorizationGate(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
    }

    /// <summary>
    /// Resolves the TD-24 visible scope for one request — the module ids whose
    /// history this identity is allowed to see. Returns a failure only when the
    /// <c>historia</c> module itself is not granted (Forbidden).
    /// </summary>
    public Result<HistoriaScope, DomainError> Require()
    {
        var user = _currentUserAccessor.Current;
        if (user is null)
            return Result<HistoriaScope, DomainError>.Failure(DomainError.Forbidden(
                "HISTORIA_FORBIDDEN",
                "Não existe identidade interna resolvida para este pedido."));

        if (!user.HasModule(HistoriaModuleCatalog.ModuleId))
            return Result<HistoriaScope, DomainError>.Failure(DomainError.Forbidden(
                "HISTORIA_FORBIDDEN",
                "O módulo História não está autorizado para esta identidade."));

        var visible = HistoriaModuleCatalog.OriginModuleIds
            .Where(user.HasModule)
            .OrderBy(m => m, StringComparer.Ordinal)
            .ToArray();

        var includeAdmin = user.HasCapability(CanonicalCapabilities.AuditView);

        return Result<HistoriaScope, DomainError>.Success(
            new HistoriaScope(visible, includeAdmin));
    }
}

/// <summary>
/// Resolved TD-24 scope for the História transversal read: the granted origin
/// modules whose events this identity may view, and whether admin events
/// (module_id = admin) are included (requires audit.view).
/// </summary>
public sealed record HistoriaScope(
    IReadOnlyCollection<string> VisibleOriginModuleIds,
    bool IncludeAdminWithAuditView);