namespace BA.Dmo.Application.Shared.Identity;

/// <summary>
/// HI-2: thrown by the identity repository when more than one
/// <c>internal_users</c> row maps to the same <c>auth_user_id</c>
/// (ambiguous internal identity). This is a data-integrity condition, NOT a
/// backend outage: it must surface as the distinct <c>IDENTITY_AMBIGUOUS</c>
/// resolution failure (fail closed, plain /no-access) — never as
/// <c>BackendUnavailable</c>/<c>indisponivel=1</c>, which would send
/// diagnostics chasing a healthy database. The durable fix (partial UNIQUE
/// constraint on <c>auth_user_id</c>) is owned by the database track (INT-01);
/// this type is the application-side typed diagnostic.
/// </summary>
public sealed class AmbiguousIdentityException : Exception
{
    public Guid AuthUserId { get; }

    public AmbiguousIdentityException(Guid authUserId)
        : base($"More than one internal identity maps to auth user {authUserId}.")
    {
        AuthUserId = authUserId;
    }
}
