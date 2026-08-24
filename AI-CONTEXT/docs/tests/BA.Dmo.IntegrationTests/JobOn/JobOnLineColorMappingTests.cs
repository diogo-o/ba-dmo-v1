using BA.Dmo.Web.Pages.JobOn;

namespace BA.Dmo.IntegrationTests.JobOnColors;

/// <summary>
/// R011 â€” Deterministic machine/line â†’ colour-key mapping (Owner Â§4/Â§5/Â§22).
/// The six production lines B1..C3 each resolve to ONE stable colour key and that
/// key identifies the MACHINE/LINE only â€” never a semantic status. Same line â†’ same
/// key; different lines â†’ different keys; all six lines are supported.
/// </summary>
public class JobOnLineColorMappingTests
{
    [Theory]
    [InlineData("B1", "b1")]
    [InlineData("B2", "b2")]
    [InlineData("B3", "b3")]
    [InlineData("C1", "c1")]
    [InlineData("C2", "c2")]
    [InlineData("C3", "c3")]
    public void AllSixLines_ResolveTo_AStableKey(string line, string expectedKey)
    {
        var key = JobOnLineColor.GetColorKey(line);
        Assert.Equal(expectedKey, key);
        Assert.NotNull(JobOnLineColor.GetColorToken(line));
        Assert.NotNull(JobOnLineColor.GetLineClass(line));
    }

    [Fact]
    public void SameLine_AlwaysResolvesToTheSameKey()
    {
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal("b1", JobOnLineColor.GetColorKey("B1"));
            Assert.Equal("c3", JobOnLineColor.GetColorKey("C3"));
            Assert.Equal("b2", JobOnLineColor.GetColorKey("b2"));
        }
    }

    [Fact]
    public void DifferentLines_ResolveToDifferentKeys()
    {
        var keys = new[] { "B1", "B2", "B3", "C1", "C2", "C3" }
            .Select(JobOnLineColor.GetColorKey)
            .ToArray();
        Assert.Equal(6, keys.Distinct().Count());
        Assert.All(keys, k => Assert.False(string.IsNullOrWhiteSpace(k)));
    }

    [Fact]
    public void UnknownLine_ResolvesToNull_AndIsNotValid()
    {
        Assert.Null(JobOnLineColor.GetColorKey("LINHA-1"));
        Assert.Null(JobOnLineColor.GetColorToken("X9"));
        Assert.Null(JobOnLineColor.GetLineClass(""));
        Assert.False(JobOnLineColor.IsValid("NOPE"));
        Assert.True(JobOnLineColor.IsValid("B1"));
    }

    [Fact]
    public void CanonicalSixLineSet_MatchesThePlatformCatalog()
    {
        // The mapping supports exactly the six platform lines (mirrors the canonical catalog).
        var canonical = BA.Dmo.Domain.Modules.ReparacaoInterna.ReparacaoInternaModuleCatalog.Lines;
        Assert.Equal(6, canonical.Count);
        Assert.Equal(
            JobOnLineColor.Lines.OrderBy(x => x),
            canonical.OrderBy(x => x));
    }
}

