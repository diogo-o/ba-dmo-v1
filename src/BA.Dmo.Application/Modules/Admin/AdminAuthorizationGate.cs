using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Admin;

/// <summary>
/// Server-side capability gate for Administration (Plan-V3 GLM-ACC-03/04,
/// U-06 authorization rule): every Admin operation re-checks the canonical
/// capability of the CURRENT request identity. Authorization derives only
/// from internal identity → template → catalog grants — never from email,
/// template name, provider role name or a hardcoded Admin branch. Fails
/// closed: no resolved identity = Forbidden.
/// </summary>
public sealed class AdminAuthorizationGate
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AdminAuthorizationGate(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
    }

    /// <summary>
    /// Requires any of the given capabilities. On success returns the actor
    /// identity (actor id + display name) for audit attribution.
    /// </summary>
    public Result<AdminExecutor, DomainError> Require(params string[] anyOfCapabilityIds)
    {
        var user = _currentUserAccessor.Current;
        if (user is null)
            return Result<AdminExecutor, DomainError>.Failure(DomainError.Forbidden(
                "ADMIN_FORBIDDEN",
                "No resolved internal identity for this request."));

        foreach (var capabilityId in anyOfCapabilityIds)
        {
            if (user.HasCapability(capabilityId))
                return Result<AdminExecutor, DomainError>.Success(new AdminExecutor(
                    user.InternalUserId.ToString(),
                    user.DisplayName));
        }

        return Result<AdminExecutor, DomainError>.Failure(DomainError.Forbidden(
            "ADMIN_FORBIDDEN",
            "The required capability is not granted."));
    }
}

/// <summary>Executor identity of an authorized Admin operation (audit attribution).</summary>
public sealed record AdminExecutor(string ActorId, string DisplayName);
