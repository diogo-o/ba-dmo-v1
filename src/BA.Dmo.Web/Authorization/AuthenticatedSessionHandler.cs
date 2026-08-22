using BA.Dmo.Web.Identity;
using Microsoft.AspNetCore.Authorization;

namespace BA.Dmo.Web.Authorization;

/// <summary>
/// Minimum U-05 authorization contract: an authenticated session is required
/// everywhere by default (fallback policy). Module/capability guards belong
/// to U-07; this handler only distinguishes session/no-session. It never
/// grants anything by role name (GLM-ACC-02/03).
/// </summary>
public sealed class AuthenticatedSessionRequirement : IAuthorizationRequirement;

public sealed class AuthenticatedSessionHandler : AuthorizationHandler<AuthenticatedSessionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AuthenticatedSessionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim(c => c.Type == SessionClaims.AuthUserIdClaimType))
        {
            context.Succeed(requirement);
        }

        // Fail closed: no silent success path exists.
        return Task.CompletedTask;
    }
}
