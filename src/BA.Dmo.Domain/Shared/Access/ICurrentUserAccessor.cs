namespace BA.Dmo.Domain.Shared.Access;

/// <summary>
/// Per-request access point to the resolved internal identity (Plan-V3 GLM-ARCH-03).
/// The identity and its grants (modules + capabilities) are resolved server-side per request
/// from the authenticated Supabase user → internal_users → active access template
/// (04_ACC; implemented from U-05). Grants are never read from the client or the cookie itself.
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>
    /// The resolved current user, or <c>null</c> when no authenticated internal user
    /// is resolved for the current request.
    /// </summary>
    CurrentUser? Current { get; }
}
