using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Tampoes;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Tampoes;

/// <summary>
/// U-17 — Server-side gate for Tampões (GLM-TP-02, 04_ACC §6, 05_SHL §5). Module
/// entry (<c>tampoes</c>) grants FULL Operator access — quantities, transforms,
/// planning and Opções (fields/configurations) are NOT reserved to the Admin.
/// Every use case re-checks the CURRENT request identity and fails closed (no
/// resolved identity = Forbidden). Returns the canonical internal_users.actor_id.
/// </summary>
public sealed class TampaoAuthorizationGate
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPersistenceAuthorshipAccessor _authorship;

    public TampaoAuthorizationGate(
        ICurrentUserAccessor currentUserAccessor,
        IPersistenceAuthorshipAccessor authorship)
    {
        _currentUserAccessor = currentUserAccessor;
        _authorship = authorship;
    }

    public Result<TampaoExecutor, DomainError> Require()
    {
        var user = _currentUserAccessor.Current;
        if (user is null)
            return Result<TampaoExecutor, DomainError>.Failure(DomainError.Forbidden(
                "TAMPAO_FORBIDDEN",
                "Não existe identidade interna resolvida para este pedido."));

        if (!user.HasModule(TampoesModuleCatalog.ModuleId))
            return Result<TampaoExecutor, DomainError>.Failure(DomainError.Forbidden(
                "TAMPAO_FORBIDDEN",
                "O módulo Tampões não está autorizado para esta identidade."));

        var actorId = _authorship.Current.ActorId;
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<TampaoExecutor, DomainError>.Failure(DomainError.Forbidden(
                "TAMPAO_FORBIDDEN",
                "Não foi possível resolver o ator canónico para este pedido."));

        return Result<TampaoExecutor, DomainError>.Success(
            new TampaoExecutor(actorId, user.DisplayName));
    }
}

/// <summary>Executor identity of an authorized Tampões operation (audit attribution).</summary>
public sealed record TampaoExecutor(string ActorId, string DisplayName);