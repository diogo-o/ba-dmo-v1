using BA.Dmo.Application.Shared.Identity;

namespace BA.Dmo.UnitTests.Shared.Identity;

/// <summary>
/// U-05 template grants parser tests (Plan-V3 GLM-ACC-02 modules jsonb
/// contract). Structural defects fail explicitly; semantic validity is the
/// GrantNormalizer's job (U-04).
/// </summary>
public class AccessTemplateGrantsParserTests
{
    [Fact]
    public void ValidModulesJson_ParsesGrants()
    {
        var result = AccessTemplateGrantsParser.Parse(
            "[{\"moduleId\":\"peso\",\"capabilities\":[\"peso.aprovar\"]}," +
            "{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("peso", result.Value[0].ModuleId);
        Assert.Equal(["peso.aprovar"], result.Value[0].Capabilities);
        Assert.Equal("boquilhas", result.Value[1].ModuleId);
        Assert.Empty(result.Value[1].Capabilities);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyModules_ParsesToNoGrants(string? json)
    {
        var result = AccessTemplateGrantsParser.Parse(json);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void MalformedJson_FailsExplicitly()
    {
        var result = AccessTemplateGrantsParser.Parse("{ not valid json");

        Assert.True(result.IsFailure);
        Assert.Equal("ACCESS_TEMPLATE_MODULES_INVALID", result.Error.Code);
    }

    [Fact]
    public void EntriesWithoutModuleId_AreSkipped()
    {
        var result = AccessTemplateGrantsParser.Parse(
            "[{\"capabilities\":[\"peso.aprovar\"]},{\"moduleId\":\"peso\",\"capabilities\":[]}]");

        Assert.True(result.IsSuccess);
        var grant = Assert.Single(result.Value);
        Assert.Equal("peso", grant.ModuleId);
    }

    [Fact]
    public void BlankCapabilities_AreDropped()
    {
        var result = AccessTemplateGrantsParser.Parse(
            "[{\"moduleId\":\"peso\",\"capabilities\":[\"peso.aprovar\",\" \",null]}]");

        Assert.True(result.IsSuccess);
        Assert.Equal(["peso.aprovar"], Assert.Single(result.Value).Capabilities);
    }

    [Fact]
    public void UnknownModulesAndCapabilities_AreLeftForNormalization()
    {
        // The parser is structural: semantic discarding belongs to the
        // GrantNormalizer (U-04), keeping responsibilities separate.
        var result = AccessTemplateGrantsParser.Parse(
            "[{\"moduleId\":\"ghost\",\"capabilities\":[\"ghost.cap\"]}]");

        Assert.True(result.IsSuccess);
        Assert.Equal("ghost", Assert.Single(result.Value).ModuleId);
    }
}
