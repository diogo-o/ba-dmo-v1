using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Armazem;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Armazem;

/// <summary>
/// U-14 — Server-side gate for Armazém (GLM-ACC-03/04, modules/07 §2). Module
/// entry (module <c>armazem</c>) grants the workflows; no operational capability
/// in V1. Fails closed: no resolved identity = Forbidden.
/// </summary>
public sealed class ArmazemAuthorizationGate
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPersistenceAuthorshipAccessor _authorship;

    public ArmazemAuthorizationGate(
        ICurrentUserAccessor currentUserAccessor,
        IPersistenceAuthorshipAccessor authorship)
    {
        _currentUserAccessor = currentUserAccessor;
        _authorship = authorship;
    }

    public Result<ArmazemExecutor, DomainError> Require()
    {
        var user = _currentUserAccessor.Current;
        if (user is null)
            return Result<ArmazemExecutor, DomainError>.Failure(DomainError.Forbidden(
                "ARMAZEM_FORBIDDEN",
                "Não existe identidade interna resolvida para este pedido."));

        if (!user.HasModule(ArmazemModuleCatalog.ModuleId))
            return Result<ArmazemExecutor, DomainError>.Failure(DomainError.Forbidden(
                "ARMAZEM_FORBIDDEN",
                "O módulo Armazém não está autorizado para esta identidade."));

        var actorId = _authorship.Current.ActorId;
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<ArmazemExecutor, DomainError>.Failure(DomainError.Forbidden(
                "ARMAZEM_FORBIDDEN",
                "Não foi possível resolver o ator canónico para este pedido."));

        return Result<ArmazemExecutor, DomainError>.Success(
            new ArmazemExecutor(actorId, user.DisplayName));
    }
}

/// <summary>Executor identity of an authorized Armazém operation (audit attribution).</summary>
public sealed record ArmazemExecutor(string ActorId, string DisplayName);