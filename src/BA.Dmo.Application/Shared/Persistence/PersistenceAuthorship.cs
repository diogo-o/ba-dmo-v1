namespace BA.Dmo.Application.Shared.Persistence;

/// <summary>
/// Authorship/timestamp context of one write (Plan-V3 06_DATA §2, GLM-DATA-02,
/// GLM-DATA-04.3): timestamps are UTC (<c>timestamptz</c>); authorship is the
/// server-side resolved <c>actor_id</c> of internal_users — NEVER accepted
/// from the client.
/// </summary>
public sealed record PersistenceAuthorship
{
    public string? ActorId { get; init; }

    public DateTimeOffset NowUtc { get; init; }

    public PersistenceAuthorship(string? actorId, DateTimeOffset nowUtc)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "Persistence timestamps must be UTC.", nameof(nowUtc));

        ActorId = actorId;
        NowUtc = nowUtc;
    }
}

/// <summary>
/// Support port resolving the authorship for writes. The concrete resolution
/// (authenticated Supabase user → internal_users → actor_id) belongs to the
/// identity unit (U-05); the persistence foundation only consumes the
/// resolved result. Test doubles are confined to tests/* (GLM-ARCH-18).
/// </summary>
public interface IPersistenceAuthorshipAccessor
{
    PersistenceAuthorship Current { get; }
}
