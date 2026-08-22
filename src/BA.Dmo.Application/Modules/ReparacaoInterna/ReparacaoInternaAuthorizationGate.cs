using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.ReparacaoInterna;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.ReparacaoInterna;

/// <summary>
/// U-16 — Server-side capability gate for Reparação Interna (Plan-V3 GLM-ACC-03/04,
/// modules/08 §12, 05_SHL §5). Module entry (<c>reparacao_interna</c>) grants
/// registering and consulting; correcting additionally requires the capability
/// <c>reparacao_interna.corrigir</c> (GLM-RI-02). Every use case re-checks the
/// CURRENT request identity and fails closed (no resolved identity = Forbidden).
/// Returns the canonical internal_users.actor_id for audit attribution.
/// </summary>
public sealed class ReparacaoInternaAuthorizationGate
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPersistenceAuthorshipAccessor _authorship;

    public ReparacaoInternaAuthorizationGate(
        ICurrentUserAccessor currentUserAccessor,
        IPersistenceAuthorshipAccessor authorship)
    {
        _currentUserAccessor = currentUserAccessor;
        _authorship = authorship;
    }

    public Result<ReparacaoInternaExecutor, DomainError> Require()
    {
        var user = _currentUserAccessor.Current;
        if (user is null)
            return Result<ReparacaoInternaExecutor, DomainError>.Failure(DomainError.Forbidden(
                "REPINT_FORBIDDEN",
                "Não existe identidade interna resolvida para este pedido."));

        if (!user.HasModule(ReparacaoInternaModuleCatalog.ModuleId))
            return Result<ReparacaoInternaExecutor, DomainError>.Failure(DomainError.Forbidden(
                "REPINT_FORBIDDEN",
                "O módulo Reparação Interna não está autorizado para esta identidade."));

        var actorId = _authorship.Current.ActorId;
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<ReparacaoInternaExecutor, DomainError>.Failure(DomainError.Forbidden(
                "REPINT_FORBIDDEN",
                "Não foi possível resolver o ator canónico para este pedido."));

        return Result<ReparacaoInternaExecutor, DomainError>.Success(
            new ReparacaoInternaExecutor(actorId, user.DisplayName));
    }

    /// <summary>Requires module entry AND the <c>reparacao_interna.corrigir</c> capability.</summary>
    public Result<ReparacaoInternaExecutor, DomainError> RequireCorrigir(string actorId)
    {
        var user = _currentUserAccessor.Current;
        if (user is null || !user.HasModule(ReparacaoInternaModuleCatalog.ModuleId))
            return Result<ReparacaoInternaExecutor, DomainError>.Failure(DomainError.Forbidden(
                "REPINT_FORBIDDEN",
                "O módulo Reparação Interna não está autorizado para esta identidade."));

        if (!user.HasCapability(ReparacaoInternaModuleCatalog.CorrigirCapabilityId))
            return Result<ReparacaoInternaExecutor, DomainError>.Failure(DomainError.Forbidden(
                "REPINT_CORRIGIR_FORBIDDEN",
                "A capacidade reparacao_interna.corrigir não está autorizada para esta identidade."));

        if (string.IsNullOrWhiteSpace(actorId))
            return Result<ReparacaoInternaExecutor, DomainError>.Failure(DomainError.Forbidden(
                "REPINT_FORBIDDEN",
                "Não foi possível resolver o ator canónico para este pedido."));

        return Result<ReparacaoInternaExecutor, DomainError>.Success(
            new ReparacaoInternaExecutor(actorId, user.DisplayName));
    }
}

/// <summary>Executor identity of an authorized Reparação Interna operation (audit attribution).</summary>
public sealed record ReparacaoInternaExecutor(string ActorId, string DisplayName);