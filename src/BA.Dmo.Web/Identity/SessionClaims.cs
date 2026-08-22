namespace BA.Dmo.Web.Identity;

/// <summary>
/// Session cookie claim contract (Plan-V3 GLM-ACC-01.5): the cookie carries
/// ONLY the Supabase auth user id. Grants are never persisted in the cookie;
/// they are resolved server-side per request from internal_users/templates.
/// No role claims, no display claims, no debug claims (GLM-ARCH-18).
/// </summary>
public static class SessionClaims
{
    public const string AuthenticationScheme = "BaDmo.Session";

    public const string AuthUserIdClaimType = "ba_dmo.auth_user_id";
}
