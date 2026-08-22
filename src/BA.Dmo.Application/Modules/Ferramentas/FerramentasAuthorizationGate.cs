using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Ferramentas;

/// <summary>
/// Server-side capability gate for Ferramentas (Plan-V3 GLM-ACC-03/04, modules/06
/// §3, TD-33): every Ferramentas use case re-checks the CURRENT request identity.
/// Module entry (module <c>ferramentas</c>) is enough for core create/edit/duplicate/query
/// use cases; verification-rule configuration requires <c>ferramentas.configure</c>.
/// Fails closed: no resolved identity = Forbidden.
/// </summary>
public sealed class FerramentasAuthorizationGate
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPersistenceAuthorshipAccessor _authorship;

    public FerramentasAuthorizationGate(
        ICurrentUserAccessor currentUserAccessor,
        IPersistenceAuthorshipAccessor authorship)
    {
        _currentUserAccessor = currentUserAccessor;
        _authorship = authorship;
    }

    /// <summary>
    /// Requires the <c>ferramentas</c> module grant (and, when given, at least one of the
    /// required capabilities). Returns the canonical internal_users.actor_id for audit
    /// attribution (never a technical UUID substitution, GLM-DATA-02/GLM-ACC-01).
    /// </summary>
    public Result<FerramentasExecutor, DomainError> Require(params string[] anyOfCapabilityIds)
    {
        var user = _currentUserAccessor.Current;
        if (user is null)
            return Result<FerramentasExecutor, DomainError>.Failure(DomainError.Forbidden(
                "FERRAMENTAS_FORBIDDEN",
                "Não existe identidade interna resolvida para este pedido."));

        if (!user.HasModule(FerramentasModuleCatalog.ModuleId))
            return Result<FerramentasExecutor, DomainError>.Failure(DomainError.Forbidden(
                "FERRAMENTAS_FORBIDDEN",
                "O módulo Ferramentas não está autorizado para esta identidade."));

        if (anyOfCapabilityIds is not null && anyOfCapabilityIds.Length > 0 &&
            !anyOfCapabilityIds.Any(user.HasCapability))
        {
            return Result<FerramentasExecutor, DomainError>.Failure(DomainError.Forbidden(
                "FERRAMENTAS_FORBIDDEN",
                "A capacidade de configuração do módulo Ferramentas não está atribuída."));
        }

        var actorId = _authorship.Current.ActorId;
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<FerramentasExecutor, DomainError>.Failure(DomainError.Forbidden(
                "FERRAMENTAS_FORBIDDEN",
                "Não foi possível resolver o ator canónico para este pedido."));

        return Result<FerramentasExecutor, DomainError>.Success(new FerramentasExecutor(
            actorId, user.DisplayName)
        {
            CanConfigure = user.HasCapability(CanonicalModuleCatalog.FerramentasConfigureCapabilityId)
        });
    }
}

/// <summary>Executor identity of an authorized Ferramentas operation (audit attribution).</summary>
public sealed record FerramentasExecutor(string ActorId, string DisplayName)
{
    /// <summary>True when the executor holds the <c>ferramentas.configure</c> capability.</summary>
    public bool CanConfigure { get; init; }
}