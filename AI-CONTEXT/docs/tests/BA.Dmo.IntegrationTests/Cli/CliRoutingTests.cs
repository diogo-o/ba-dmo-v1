using BA.Dmo.Web.Cli;

namespace BA.Dmo.IntegrationTests.Cli;

/// <summary>
/// U-01 technical contract test: CLI routing (Plan-V3 09_TEST §10.3, GLM-ARCH-15).
/// migrate / bootstrap-admin / normal web startup are distinguished by process arguments;
/// there is no separate CLI project.
/// </summary>
public class CliRoutingTests
{
    [Theory]
    [InlineData("migrate", CliMode.Migrate)]
    [InlineData("MIGRATE", CliMode.Migrate)]
    [InlineData("Migrate", CliMode.Migrate)]
    [InlineData("bootstrap-admin", CliMode.BootstrapAdmin)]
    [InlineData("BOOTSTRAP-ADMIN", CliMode.BootstrapAdmin)]
    public void OperationalVerbs_AreDistinguished(string verb, CliMode expected)
    {
        Assert.Equal(expected, CliModeResolver.Resolve([verb]));
    }

    [Fact]
    public void NoArguments_MeansNormalWebStartup()
    {
        Assert.Equal(CliMode.Web, CliModeResolver.Resolve([]));
        Assert.Equal(CliMode.Web, CliModeResolver.Resolve(null));
    }

    [Fact]
    public void BlankFirstArgument_MeansNormalWebStartup()
    {
        Assert.Equal(CliMode.Web, CliModeResolver.Resolve(["   "]));
    }

    [Theory]
    [InlineData("--urls")]
    [InlineData("unknown-verb")]
    [InlineData("web")]
    public void NonVerbLeadingArgument_FallsBackToWebStartup(string argument)
    {
        // Hosting parameters (e.g. --urls http://...) must keep working;
        // unknown verbs never trigger privileged CLI behavior.
        Assert.Equal(CliMode.Web, CliModeResolver.Resolve([argument, "extra"]));
    }

    [Fact]
    public void OnlyTheFirstArgument_SelectsTheMode()
    {
        Assert.Equal(CliMode.Web, CliModeResolver.Resolve(["--verbose", "migrate"]));
        Assert.Equal(CliMode.Migrate, CliModeResolver.Resolve(["migrate", "--verbose"]));
    }
}
