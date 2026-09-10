using BA.Dmo.Application.Shared.Persistence;

namespace BA.Dmo.UnitTests.Shared.Persistence;

/// <summary>
/// Shared fake <see cref="IPersistenceAuthorshipAccessor"/> for unit tests of
/// gates/services that attribute actors. The actor id deliberately differs
/// from any auth-user Guid used by the fake current-user accessors so tests
/// prove the canonical internal_users.actor_id is what gets written.
/// </summary>
public sealed class CanonicalActorFakeAuthorship(string? actorId = "canonical-actor")
    : IPersistenceAuthorshipAccessor
{
    public PersistenceAuthorship Current { get; } =
        new(actorId, new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero));
}