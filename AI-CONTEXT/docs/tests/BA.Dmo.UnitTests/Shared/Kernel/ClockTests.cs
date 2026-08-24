using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Shared.Kernel;

/// <summary>
/// U-01 kernel unit tests: time abstraction (Plan-V3 GLM-ARCH-03; fixed-date fakes per 09_TEST §5).
/// </summary>
public class ClockTests
{
    [Fact]
    public void SystemClock_ReportsUtcCloseToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var actual = SystemClock.Instance.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(TimeSpan.Zero, actual.Offset);
        Assert.InRange(actual, before, after);
    }

    [Fact]
    public void FixedFakeClock_SatisfiesContract_ForDeterministicTests()
    {
        var fixedInstant = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        IClock clock = new FixedClock(fixedInstant);

        Assert.Equal(fixedInstant, clock.UtcNow);
        Assert.Equal(fixedInstant, clock.UtcNow);
    }

    /// <summary>Test double confined to tests/* (allowed by GLM-ARCH-18).</summary>
    private sealed class FixedClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }
}
