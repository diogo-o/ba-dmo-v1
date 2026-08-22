using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Authorization;

namespace BA.Dmo.Web.Authorization;

/// <summary>
/// Module-entry requirement (Plan-V3 GLM-ACC-04 backend level, 05_SHL §5
/// route guards): the resolved internal identity must hold the module in its
/// active template grants. Presence of the module grants ENTRY; capabilities
/// grant specific operations (GLM-ACC-02).
/// </summary>
public sealed class ModuleRequirement(string moduleId) : IAuthorizationRequirement
{
    public string ModuleId { get; } = moduleId;
}

public sealed class ModuleAuthorizationHandler : AuthorizationHandler<ModuleRequirement>
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ModuleAuthorizationHandler(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ModuleRequirement requirement)
    {
        var user = _currentUserAccessor.Current;
        if (user is not null && user.HasModule(requirement.ModuleId))
        {
            context.Succeed(requirement);
        }

        // Fail closed: no silent success, no role-name fallback.
        return Task.CompletedTask;
    }
}

/// <summary>
/// Canonical module policy names (05_SHL §5 route table). Built ONLY from
/// canonical module ids — never role names, emails or template names
/// (GLM-ACC-03/04). Registered for every canonical module at the
/// composition root.
/// </summary>
public static class ModulePolicies
{
    public const string Prefix = "BaDmo.Module.";

    public const string Jobon = Prefix + CanonicalModuleCatalog.JobonModuleId;
    public const string Boquilhas = Prefix + CanonicalModuleCatalog.BoquilhasModuleId;
    public const string Controlo = Prefix + CanonicalModuleCatalog.ControloAreaId;
    public const string Peso = Prefix + CanonicalModuleCatalog.PesoModuleId;
    public const string Pegamentos = Prefix + CanonicalModuleCatalog.PegamentosModuleId;
    public const string Ferramentas = Prefix + CanonicalModuleCatalog.FerramentasModuleId;
    public const string Armazem = Prefix + CanonicalModuleCatalog.ArmazemModuleId;
    public const string ReparacaoInterna = Prefix + CanonicalModuleCatalog.ReparacaoInternaModuleId;
    public const string ReparacaoExterna = Prefix + CanonicalModuleCatalog.ReparacaoExternaModuleId;
    public const string Tampoes = Prefix + CanonicalModuleCatalog.TampoesModuleId;
    public const string Historia = Prefix + CanonicalModuleCatalog.HistoriaModuleId;
}

/// <summary>
/// Canonical capability policy names for route-level guards (05_SHL §5).
/// Capability operation-level checks additionally happen inside use cases
/// (GLM-ACC-04); these policies serve page/route entry only.
/// </summary>
public static class CapabilityPolicies
{
    public const string Prefix = "BaDmo.Capability.";

    public const string JobonView = Prefix + CanonicalModuleCatalog.JobonViewCapabilityId;
    public const string JobonEdit = Prefix + CanonicalModuleCatalog.JobonEditCapabilityId;
    public const string JobonConfigure = Prefix + CanonicalModuleCatalog.JobonConfigureCapabilityId;
    public const string JobonConfirmar = Prefix + CanonicalModuleCatalog.JobonConfirmarCapabilityId;
    public const string PesoAprovar = Prefix + CanonicalModuleCatalog.PesoAprovarCapabilityId;
    public const string FerramentasConfigure = Prefix + CanonicalModuleCatalog.FerramentasConfigureCapabilityId;
    public const string ControloView = Prefix + CanonicalModuleCatalog.ControloViewCapabilityId;
    public const string ControloEdit = Prefix + CanonicalModuleCatalog.ControloEditCapabilityId;
    public const string ControloSubmit = Prefix + CanonicalModuleCatalog.ControloSubmitCapabilityId;
    public const string ControloReview = Prefix + CanonicalModuleCatalog.ControloReviewCapabilityId;
}
