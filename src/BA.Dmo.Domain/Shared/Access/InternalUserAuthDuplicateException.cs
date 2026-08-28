namespace BA.Dmo.Domain.Shared.Access;

/// <summary>
/// Raised by the persistence layer when an internal user create hits
/// <c>uq_internal_users_auth_user</c> (N25): the same Auth account is already
/// linked to a different internal user under concurrency (the actor_id
/// ON CONFLICT does not absorb this arbiter). The service maps this to the
/// same structured domain conflict as its pre-check
/// (ADMIN_USER_ALREADY_REGISTERED) so both paths report the same clean error
/// (audit ADM-06 / ON-02).
/// </summary>
public sealed class InternalUserAuthDuplicateException : Exception
{
    public InternalUserAuthDuplicateException(string message)
        : base(message)
    {
    }
}