using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Boquilhas;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Boquilhas;

/// <summary>
/// U-19 — Server-side gate for Boquilhas (GLM-BQ-02, 05_SHL §5). Module entry
/// (<c>boquilhas</c>) grants FULL access — there is NO operator/responsável split
/// and NO functional capability in V1. Every use case re-checks the CURRENT
/// request identity and fails closed (no resolved identity = Forbidden),
/// correcting the legacy gap that had no server-side module guard. Returns the
/// canonical internal_users.actor_id for server-side attribution (BQ-RULE-009).
/// </summary>
public sealed class BqAuthorizationGate
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPersistenceAuthorshipAccessor _authorship;

    public BqAuthorizationGate(
        ICurrentUserAccessor currentUserAccessor,
        IPersistenceAuthorshipAccessor authorship)
    {
        _currentUserAccessor = currentUserAccessor;
        _authorship = authorship;
    }

    public Result<BqExecutor, DomainError> Require()
    {
        var user = _currentUserAccessor.Current;
        if (user is null)
            return Result<BqExecutor, DomainError>.Failure(DomainError.Forbidden(
                "BQ_FORBIDDEN",
                "Não existe identidade interna resolvida para este pedido."));

        if (!user.HasModule(BoquilhasModuleCatalog.ModuleId))
            return Result<BqExecutor, DomainError>.Failure(DomainError.Forbidden(
                "BQ_FORBIDDEN",
                "O módulo Boquilhas não está autorizado para esta identidade."));

        var actorId = _authorship.Current.ActorId;
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<BqExecutor, DomainError>.Failure(DomainError.Forbidden(
                "BQ_FORBIDDEN",
                "Não foi possível resolver o ator canónico para este pedido."));

        return Result<BqExecutor, DomainError>.Success(
            new BqExecutor(actorId, user.DisplayName));
    }
}

/// <summary>Executor identity of an authorized Boquilhas operation (audit attribution).</summary>
public sealed record BqExecutor(string ActorId, string DisplayName);