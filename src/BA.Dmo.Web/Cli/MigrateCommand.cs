using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Infrastructure.Persistence;
using BA.Dmo.Infrastructure.Persistence.Migrations;

namespace BA.Dmo.Web.Cli;

/// <summary>
/// CLI entry for forward-only schema migrations (GLM-ARCH-15, CLI ONLY;
/// 06_DATA §12–13, PV-04/PV-05). Invoked as:
/// <code>
/// dotnet BA.Dmo.Web.dll migrate
/// dotnet run --project src/BA.Dmo.Web -- migrate      (development)
/// </code>
/// CLI mode never starts the web server. Exit codes: 0 = success (Render
/// pre-deploy continues), non-zero = configuration or migration failure
/// (deployment aborts). There is no HTTP migration endpoint and migrations
/// never run during normal web startup.
///
/// Connection contract (server-side only; secrets never in the repository):
/// - BA_DMO_DB_CONNECTION_STRING (preferred), else DATABASE_URL;
/// - optional BA_DMO_MIGRATIONS_DIR overrides the migrations directory.
/// A missing connection configuration fails explicitly.
/// </summary>
public static class MigrateCommand
{
    // Connection contract shared with the persistence foundation (U-03):
    // DatabaseConnectionSettings is the single source of the env-variable names.
    public const string ConnectionStringVariable =
        DatabaseConnectionSettings.ConnectionStringVariable;
    public const string FallbackConnectionStringVariable =
        DatabaseConnectionSettings.FallbackConnectionStringVariable;
    public const string MigrationsDirectoryVariable = "BA_DMO_MIGRATIONS_DIR";

    public const int SuccessExitCode = 0;
    public const int FailureExitCode = 1;
    public const int ConfigurationErrorExitCode = 2;

    public static int Run() =>
        Run(Environment.GetEnvironmentVariable, Console.Out, Console.Error);

    public static int Run(
        Func<string, string?> environment,
        TextWriter stdout,
        TextWriter stderr)
    {
        var connectionString = DatabaseConnectionSettings.ResolveConnectionString(environment);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            stderr.WriteLine(
                $"BA DMO migrate: missing database connection. Set the environment variable " +
                $"'{ConnectionStringVariable}' (or '{FallbackConnectionStringVariable}') to the " +
                "server-side connection string. No connection string is ever stored in the repository.");
            return ConfigurationErrorExitCode;
        }

        var migrationsDirectory = ResolveMigrationsDirectory(environment);
        if (migrationsDirectory is null || !Directory.Exists(migrationsDirectory))
        {
            stderr.WriteLine(
                $"BA DMO migrate: migrations directory not found ('{migrationsDirectory ?? "<unresolved>"}'). " +
                $"Provide '{MigrationsDirectoryVariable}' or run from a deployment that includes " +
                "database/migrations.");
            return ConfigurationErrorExitCode;
        }

        try
        {
            return RunMigrations(connectionString, migrationsDirectory, stdout, stderr).GetAwaiter().GetResult();
        }
        catch (MigrationChecksumMismatchException ex)
        {
            stderr.WriteLine($"BA DMO migrate: CHECKSUM MISMATCH — {ex.Message}");
            return FailureExitCode;
        }
        catch (MigrationExecutionException ex)
        {
            stderr.WriteLine($"BA DMO migrate: EXECUTION FAILED — {ex.Message}");
            stderr.WriteLine(ex.InnerException?.Message ?? string.Empty);
            return FailureExitCode;
        }
        catch (MigrationDiscoveryException ex)
        {
            stderr.WriteLine($"BA DMO migrate: DISCOVERY FAILED — {ex.Message}");
            return FailureExitCode;
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"BA DMO migrate: FAILED — {ex.Message}");
            return FailureExitCode;
        }
    }

    private static async Task<int> RunMigrations(
        string connectionString,
        string migrationsDirectory,
        TextWriter stdout,
        TextWriter stderr)
    {
        var migrations = MigrationDiscovery.Discover(migrationsDirectory);
        stdout.WriteLine($"BA DMO migrate: {migrations.Count} migration(s) discovered in canonical order.");

        await using var gateway = new NpgsqlMigrationScriptGateway(connectionString);
        try
        {
            await gateway.OpenAsync();
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"BA DMO migrate: unable to open the database connection — {ex.Message}");
            return FailureExitCode;
        }

        var runner = new MigrationRunner(gateway, SystemClock.Instance);
        var report = await runner.RunAsync(migrations);

        foreach (var applied in report.Applied)
            stdout.WriteLine($"BA DMO migrate: applied {applied.FileName}.");
        foreach (var skipped in report.Skipped)
            stdout.WriteLine($"BA DMO migrate: skipped {skipped.FileName} (already applied, checksum unchanged).");

        stdout.WriteLine(
            report.NothingToDo
                ? "BA DMO migrate: schema already up to date."
                : $"BA DMO migrate: completed — {report.Applied.Count} applied, {report.Skipped.Count} skipped.");
        return SuccessExitCode;
    }

    /// <summary>
    /// Resolves the migrations directory: explicit environment override, else
    /// 'database/migrations' at or above the application base directory
    /// (deployed output layout first, then repository layout in development).
    /// </summary>
    internal static string? ResolveMigrationsDirectory(Func<string, string?> environment)
    {
        var configured = environment(MigrationsDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        const string relativePath = "database/migrations";
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 6 && directory is not null; depth++)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (Directory.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, relativePath);
    }
}
