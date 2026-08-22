using BA.Dmo.Application.Shared.Persistence;

namespace BA.Dmo.UnitTests.Shared.Persistence;

/// <summary>
/// U-03 timestamp/authorship policy tests (Plan-V3 06_DATA §2:
/// timestamptz UTC; authorship resolved server-side, never from the client).
/// </summary>
public class PersistenceAuthorshipTests
{
    [Fact]
    public void Authorship_CarriesActorAndUtcTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var authorship = new PersistenceAuthorship("actor-1", now);

        Assert.Equal("actor-1", authorship.ActorId);
        Assert.Equal(now, authorship.NowUtc);
    }

    [Fact]
    public void Authorship_AllowsNullActor_ForSystemOperations()
    {
        var authorship = new PersistenceAuthorship(null, DateTimeOffset.UtcNow);

        Assert.Null(authorship.ActorId);
    }

    [Fact]
    public void NonUtcTimestamp_IsRejected()
    {
        var local = new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>(() => new PersistenceAuthorship("actor-1", local));
    }

    [Fact]
    public void AccessorPort_ResolvesCurrentAuthorship()
    {
        var fixedAuthorship = new PersistenceAuthorship("actor-9", DateTimeOffset.UtcNow);
        IPersistenceAuthorshipAccessor accessor = new FixedAccessor(fixedAuthorship);

        Assert.Same(fixedAuthorship, accessor.Current);
    }

    /// <summary>Test double confined to tests/* (GLM-ARCH-18).</summary>
    private sealed class FixedAccessor(PersistenceAuthorship authorship) : IPersistenceAuthorshipAccessor
    {
        public PersistenceAuthorship Current => authorship;
    }
}
