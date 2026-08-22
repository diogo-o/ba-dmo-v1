using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.ReparacaoExterna;

/// <summary>
/// U-15 — Server-side capability gate for Reparação Externa (Plan-V3 GLM-ACC-03/04,
/// modules/09 §13, 05_SHL §5). Every Reparação Externa use case re-checks the CURRENT
/// request identity. Module entry (module <c>reparacao_externa</c>) is enough for the
/// core external-repair workflows (04_ACC §6: module presence grants entry; no
/// capability, GLM-RE-13). Fails closed: no resolved identity = Forbidden.
/// Returns the canonical internal_users.actor_id for audit attribution.
/// </summary>
public sealed class ReparacaoExternaAuthorizationGate
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPersistenceAuthorshipAccessor _authorship;

    public ReparacaoExternaAuthorizationGate(
        ICurrentUserAccessor currentUserAccessor,
        IPersistenceAuthorshipAccessor authorship)
    {
        _currentUserAccessor = currentUserAccessor;
        _authorship = authorship;
    }

    public Result<ReparacaoExternaExecutor, DomainError> Require()
    {
        var user = _currentUserAccessor.Current;
        if (user is null)
            return Result<ReparacaoExternaExecutor, DomainError>.Failure(DomainError.Forbidden(
                "REPEXT_FORBIDDEN",
                "Não existe identidade interna resolvida para este pedido."));

        if (!user.HasModule(ReparacaoExternaModuleCatalog.ModuleId))
            return Result<ReparacaoExternaExecutor, DomainError>.Failure(DomainError.Forbidden(
                "REPEXT_FORBIDDEN",
                "O módulo Reparação Externa não está autorizado para esta identidade."));

        var actorId = _authorship.Current.ActorId;
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<ReparacaoExternaExecutor, DomainError>.Failure(DomainError.Forbidden(
                "REPEXT_FORBIDDEN",
                "Não foi possível resolver o ator canónico para este pedido."));

        return Result<ReparacaoExternaExecutor, DomainError>.Success(
            new ReparacaoExternaExecutor(actorId, user.DisplayName));
    }
}

/// <summary>Executor identity of an authorized Reparação Externa operation (audit attribution).</summary>
public sealed record ReparacaoExternaExecutor(string ActorId, string DisplayName);