using BA.Dmo.Web.Cli;

namespace BA.Dmo.IntegrationTests.Cli;

/// <summary>
/// U-02 CLI migrate contract tests (Plan-V3 PV-05, 06_DATA §13, GLM-ARCH-15).
/// CLI routing itself is covered by CliRoutingTests; the web-vs-CLI separation
/// is guaranteed by Program.cs returning from the migrate verb BEFORE any
/// WebApplication is built (no web server in CLI mode).
/// </summary>
public sealed class MigrateCliTests
{
    private static (int ExitCode, string StdOut, string StdErr) Run(
        IReadOnlyDictionary<string, string?> environment)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = MigrateCommand.Run(
            name => environment.TryGetValue(name, out var value) ? value : null,
            stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void MissingConnectionConfiguration_FailsExplicitly_NonZero()
    {
        var result = Run(new Dictionary<string, string?>());

        Assert.Equal(MigrateCommand.ConfigurationErrorExitCode, result.ExitCode);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(MigrateCommand.ConnectionStringVariable, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingMigrationsDirectory_FailsExplicitly_NonZero()
    {
        var result = Run(new Dictionary<string, string?>
        {
            [MigrateCommand.ConnectionStringVariable] = "Host=localhost;Database=ba_dmo",
            [MigrateCommand.MigrationsDirectoryVariable] =
                Path.Combine(Path.GetTempPath(), "ba_dmo_missing_migrations_" + Guid.NewGuid().ToString("N"))
        });

        Assert.Equal(MigrateCommand.ConfigurationErrorExitCode, result.ExitCode);
        Assert.Contains("migrations directory not found", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void UnusableConnection_FailsNonZero_WithoutWebServer()
    {
        // An unparseable connection string fails the CLI fast and explicitly —
        // proof that migrate never falls back to web startup and never fakes
        // success (Render pre-deploy aborts on exit != 0).
        var result = Run(new Dictionary<string, string?>
        {
            [MigrateCommand.ConnectionStringVariable] = "not a connection string"
        });

        Assert.Equal(MigrateCommand.FailureExitCode, result.ExitCode);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("unable to open the database connection", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseUrlFallback_IsHonored_WhenPrimaryVariableAbsent()
    {
        // With only DATABASE_URL set and an unusable connection, the CLI must
        // get PAST configuration (reaching the connection attempt) — proving
        // the fallback was read — and still fail non-zero.
        var result = Run(new Dictionary<string, string?>
        {
            [MigrateCommand.FallbackConnectionStringVariable] = "not a connection string"
        });

        Assert.Equal(MigrateCommand.FailureExitCode, result.ExitCode);
        Assert.DoesNotContain("missing database connection", result.StdErr, StringComparison.Ordinal);
    }
}
