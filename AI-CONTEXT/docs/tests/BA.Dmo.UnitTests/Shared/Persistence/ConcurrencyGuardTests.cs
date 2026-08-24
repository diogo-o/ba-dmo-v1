using BA.Dmo.Application.Shared.Persistence;

namespace BA.Dmo.UnitTests.Shared.Persistence;

/// <summary>
/// U-03 acceptance test: concurrency helper (Plan-V3 06_DATA §8, BT-06).
/// </summary>
public class ConcurrencyGuardTests
{
    [Fact]
    public void SingleRowUpdated_Passes()
    {
        ConcurrencyGuard.EnsureSingleRowUpdated(1, "access template 'T1'");
    }

    [Fact]
    public void ZeroRows_ThrowsConcurrencyConflict_WithReloadMessage()
    {
        var ex = Assert.Throws<ConcurrencyConflictException>(
            () => ConcurrencyGuard.EnsureSingleRowUpdated(0, "access template 'T1'"));

        Assert.Equal("access template 'T1'", ex.EntityDescription);
        Assert.Contains("Reload", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MoreThanOneRow_AlsoConflicts()
    {
        Assert.Throws<ConcurrencyConflictException>(
            () => ConcurrencyGuard.EnsureSingleRowUpdated(2, "peso lote"));
    }

    [Fact]
    public void BlankDescription_IsRejectedOnConflict()
    {
        Assert.Throws<ArgumentException>(
            () => ConcurrencyGuard.EnsureSingleRowUpdated(0, "  "));
    }
}
