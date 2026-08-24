using BA.Dmo.Web.Cli;

namespace BA.Dmo.IntegrationTests.Cli;

/// <summary>
/// CLI contract tests across roadmap units (GLM-ARCH-15).
/// U-02 implemented migrate; U-05 implemented bootstrap-admin. Both fail
/// explicitly (non-zero) without configuration — they never fake success and
/// never start the web server (Render pre-deploy semantics).
/// </summary>
public class CliCommandContractTests
{
    [Fact]
    public void BootstrapAdmin_MissingConfiguration_FailsExplicitly_UntilConfigured()
    {
        var exitCode = BootstrapAdminCommand.Run(
            _ => null, new StringWriter(), new StringWriter());

        Assert.NotEqual(0, exitCode);
        Assert.Equal(BootstrapAdminCommand.ConfigurationErrorExitCode, exitCode);
    }
}
