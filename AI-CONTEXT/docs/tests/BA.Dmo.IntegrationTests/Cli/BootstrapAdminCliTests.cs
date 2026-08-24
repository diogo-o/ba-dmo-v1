using BA.Dmo.Infrastructure.Auth;
using BA.Dmo.Web.Cli;

namespace BA.Dmo.IntegrationTests.Cli;

/// <summary>
/// U-05 bootstrap-admin CLI contract tests (Plan-V3 GLM-ARCH-15, PV-08,
/// 06_DATA §15): CLI only, explicit configuration, fail-closed, no web
/// server, no defaults, no hardcoded credentials. No live system is touched.
/// </summary>
public class BootstrapAdminCliTests
{
    private static (int ExitCode, string StdOut, string StdErr) Run(
        IReadOnlyDictionary<string, string?> environment)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = BootstrapAdminCommand.Run(
            name => environment.TryGetValue(name, out var value) ? value : null,
            stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void NoConfiguration_FailsExplicitly_WithExitCode2()
    {
        var result = Run(new Dictionary<string, string?>());

        Assert.Equal(BootstrapAdminCommand.ConfigurationErrorExitCode, result.ExitCode);
        Assert.Contains(SupabaseSettings.UrlVariable, result.StdErr, StringComparison.Ordinal);
        Assert.Contains(SupabaseSettings.ServiceRoleKeyVariable, result.StdErr, StringComparison.Ordinal);
        Assert.Contains(SupabaseSettings.BootstrapEmailVariable, result.StdErr, StringComparison.Ordinal);
        Assert.Contains(SupabaseSettings.BootstrapPasswordVariable, result.StdErr, StringComparison.Ordinal);
        Assert.Contains(SupabaseSettings.BootstrapDisplayNameVariable, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialConfiguration_ListsOnlyTheMissingVariables()
    {
        var result = Run(new Dictionary<string, string?>
        {
            [SupabaseSettings.UrlVariable] = "https://project.supabase.example",
            [SupabaseSettings.ServiceRoleKeyVariable] = "service-role",
            [SupabaseSettings.BootstrapEmailVariable] = "admin@ba-dmo.example"
            // password + display name missing
        });

        Assert.Equal(BootstrapAdminCommand.ConfigurationErrorExitCode, result.ExitCode);
        Assert.Contains(SupabaseSettings.BootstrapPasswordVariable, result.StdErr, StringComparison.Ordinal);
        Assert.Contains(SupabaseSettings.BootstrapDisplayNameVariable, result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain(SupabaseSettings.UrlVariable, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingDatabaseConfiguration_FailsBeforeAnyProvisioning()
    {
        // Bootstrap config complete, but no DB connection configured: the CLI
        // fails with the configuration exit code and never starts the web
        // server or provisions anything.
        var result = Run(new Dictionary<string, string?>
        {
            [SupabaseSettings.UrlVariable] = "https://project.supabase.example",
            [SupabaseSettings.ServiceRoleKeyVariable] = "service-role",
            [SupabaseSettings.BootstrapEmailVariable] = "admin@ba-dmo.example",
            [SupabaseSettings.BootstrapPasswordVariable] = "explicit-password",
            [SupabaseSettings.BootstrapDisplayNameVariable] = "Primeiro Admin"
        });

        Assert.Equal(BootstrapAdminCommand.ConfigurationErrorExitCode, result.ExitCode);
        Assert.Contains("database connection", result.StdErr, StringComparison.Ordinal);
        // The service-role value must never be echoed.
        Assert.DoesNotContain("service-role", result.StdErr, StringComparison.Ordinal);
    }
}
