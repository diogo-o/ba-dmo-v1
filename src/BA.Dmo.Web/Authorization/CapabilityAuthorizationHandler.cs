using BA.Dmo.Domain.Shared.Access;
using Microsoft.AspNetCore.Authorization;

namespace BA.Dmo.Web.Authorization;

/// <summary>
/// Canonical capability requirement (Plan-V3 GLM-ACC-04 backend level).
/// Authorization resolves from the per-request internal identity (U-05) —
/// never from role names, emails or claims carrying grants.
/// </summary>
public sealed class CapabilityRequirement(params string[] anyOfCapabilityIds)
    : IAuthorizationRequirement
{
    public IReadOnlyList<string> AnyOfCapabilityIds { get; } = anyOfCapabilityIds;
}

public sealed class CapabilityAuthorizationHandler : AuthorizationHandler<CapabilityRequirement>
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public CapabilityAuthorizationHandler(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor
            ?? throw new ArgumentNullException(nameof(currentUserAccessor));
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CapabilityRequirement requirement)
    {
        var user = _currentUserAccessor.Current;
        if (user is not null &&
            requirement.AnyOfCapabilityIds.Any(user.HasCapability))
        {
            context.Succeed(requirement);
        }

        // Fail closed: no silent success, no role-name fallback.
        return Task.CompletedTask;
    }
}

/// <summary>Named policies of the Administration module (modules/00).</summary>
public static class AdminPolicies
{
    public const string AdminGerir = "BaDmo.Admin.Gerir";
    public const string AuditView = "BaDmo.Audit.View";
    public const string AuditExport = "BaDmo.Audit.Export";
}
