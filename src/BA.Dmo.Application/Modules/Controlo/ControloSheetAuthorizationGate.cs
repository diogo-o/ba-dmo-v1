using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Controlo;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Controlo;

/// <summary>
/// R010 — Server-side capability gate for the Folha de Controlo (OWNER DECISION:
/// the sheet is a workflow INSIDE the Controlo functional area). The sheet is reached from
/// the production-control surface (Peso area), so module entry is granted by the surrounding
/// <c>peso</c> module presence; sheet operations additionally require the corresponding
/// <c>controlo.*</c> capability (view/edit/submit/review). Every use case re-checks the
/// CURRENT request identity and fails closed. Returns the canonical internal_users.actor_id.
/// </summary>
public sealed class ControloSheetAuthorizationGate
{
    // The Folha de Controlo surface lives inside the Peso production-control area.
    private const string SurfaceModuleId = "peso";

    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPersistenceAuthorshipAccessor _authorship;

    public ControloSheetAuthorizationGate(
        ICurrentUserAccessor currentUserAccessor,
        IPersistenceAuthorshipAccessor authorship)
    {
        _currentUserAccessor = currentUserAccessor;
        _authorship = authorship;
    }

    /// <summary>Requires the sheet surface (controlo area reached via Peso).</summary>
    public Result<ControloSheetExecutor, DomainError> RequireSurface()
        => RequireCapability(null);

    /// <summary>Requires the surface + the given <c>controlo.*</c> capability.</summary>
    public Result<ControloSheetExecutor, DomainError> RequireCapability(string? capabilityId)
    {
        var user = _currentUserAccessor.Current;
        if (user is null)
            return Result<ControloSheetExecutor, DomainError>.Failure(DomainError.Forbidden(
                "CONTROLO_FORBIDDEN",
                "Não existe identidade interna resolvida para este pedido."));

        if (!user.HasModule(SurfaceModuleId))
            return Result<ControloSheetExecutor, DomainError>.Failure(DomainError.Forbidden(
                "CONTROLO_FORBIDDEN",
                "A área Controlo não está autorizada para esta identidade."));

        if (capabilityId is not null && !user.HasCapability(capabilityId))
            return Result<ControloSheetExecutor, DomainError>.Failure(DomainError.Forbidden(
                $"CONTROLO_CAPABILITY_{capabilityId.ToUpperInvariant()}_FORBIDDEN",
                $"A capacidade {capabilityId} não está autorizada para esta identidade."));

        var actorId = _authorship.Current.ActorId;
        if (string.IsNullOrWhiteSpace(actorId))
            return Result<ControloSheetExecutor, DomainError>.Failure(DomainError.Forbidden(
                "CONTROLO_FORBIDDEN",
                "Não foi possível resolver o ator canónico para este pedido."));

        return Result<ControloSheetExecutor, DomainError>.Success(
            new ControloSheetExecutor(actorId, user.DisplayName));
    }
}

/// <summary>Executor identity of an authorized Folha de Controlo operation.</summary>
public sealed record ControloSheetExecutor(string ActorId, string DisplayName);