using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.JobOn;

/// <summary>
/// Server-side capability gate for Job On (Plan-V3 GLM-ACC-03/04, modules/05
/// §2, TD-20): every Job On operation re-checks the canonical capability of the
/// CURRENT request identity. Hiding UI never substitutes server-side
/// validation. Fails closed: no resolved identity = Forbidden.
/// </summary>
public sealed class JobOnAuthorizationGate
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public JobOnAuthorizationGate(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
    }

    /// <summary>
    /// Requires any of the given Job On capabilities. On success returns the
    /// actor identity (actor id + display name) for audit attribution.
    /// </summary>
    public Result<JobOnExecutor, DomainError> Require(params string[] anyOfCapabilityIds)
    {
        var user = _currentUserAccessor.Current;
        if (user is null)
            return Result<JobOnExecutor, DomainError>.Failure(DomainError.Forbidden(
                "JOBON_FORBIDDEN",
                "No resolved internal identity for this request."));

        foreach (var capabilityId in anyOfCapabilityIds)
        {
            if (user.HasCapability(capabilityId))
                return Result<JobOnExecutor, DomainError>.Success(new JobOnExecutor(
                    user.InternalUserId.ToString(),
                    user.DisplayName));
        }

        return Result<JobOnExecutor, DomainError>.Failure(DomainError.Forbidden(
            "JOBON_FORBIDDEN",
            "The required Job On capability is not granted."));
    }
}

/// <summary>Executor identity of an authorized Job On operation (audit attribution).</summary>
public sealed record JobOnExecutor(string ActorId, string DisplayName);
