using BA.Dmo.Domain.Modules.Peso;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Peso;

/// <summary>
/// Server-side capability gate for Peso (Plan-V3 GLM-ACC-03/04/05, modules/03
/// §2, UD-06/UD-15): every Peso operation re-checks the canonical capability of
/// the CURRENT request identity. Hiding UI never substitutes server-side
/// validation. Entrada no módulo Peso é concedida pelo grant do módulo; as
/// operações de aprovação/decisão exigem <c>peso.aprovar</c>. Fails closed: no
/// resolved identity = Forbidden.
/// </summary>
public sealed class PesoAuthorizationGate
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public PesoAuthorizationGate(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
    }

    /// <summary>
    /// Requires the Peso module grant and any of the given capabilities. Module
    /// entry is enough for core (create/edit/submit) use cases; <see
    /// cref="PesoAprovarCapabilityId"/> is required for approval/decision/
    /// reopen/delete-as-Responsável. On success returns the actor identity
    /// (actor id + display name) for audit attribution.
    /// </summary>
    public Result<PesoExecutor, DomainError> Require(params string[] anyOfCapabilityIds)
    {
        var user = _currentUserAccessor.Current;
        if (user is null)
            return Result<PesoExecutor, DomainError>.Failure(DomainError.Forbidden(
                "PESO_FORBIDDEN",
                "No resolved internal identity for this request."));

        if (!user.HasModule(PesoModuleCatalog.PesoModuleId))
            return Result<PesoExecutor, DomainError>.Failure(DomainError.Forbidden(
                "PESO_FORBIDDEN",
                "O módulo Peso não está autorizado para esta identidade."));

        if (anyOfCapabilityIds is null || anyOfCapabilityIds.Length == 0)
            return Result<PesoExecutor, DomainError>.Success(new PesoExecutor(
                user.InternalUserId.ToString(), user.DisplayName)
            {
                HasAprovarRole = user.HasCapability(PesoModuleCatalog.PesoAprovarCapabilityId)
            });

        if (anyOfCapabilityIds.Any(user.HasCapability))
            return Result<PesoExecutor, DomainError>.Success(new PesoExecutor(
                user.InternalUserId.ToString(), user.DisplayName)
            {
                HasAprovarRole = user.HasCapability(PesoModuleCatalog.PesoAprovarCapabilityId)
            });

        return Result<PesoExecutor, DomainError>.Failure(DomainError.Forbidden(
            "PESO_FORBIDDEN",
            "A capability Peso necessária não está atribuída."));
    }
}

/// <summary>Executor identity of an authorized Peso operation (audit attribution).</summary>
public sealed record PesoExecutor(string ActorId, string DisplayName)
{
    /// <summary>True when the executor holds the Responsável approver capability.</summary>
    public bool HasAprovarRole { get; init; }
}