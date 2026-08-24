using System.Reflection;

namespace BA.Dmo.IntegrationTests.Migrations;

/// <summary>
/// U-02 architecture guards for the migration subsystem (Plan-V3 PV-04/PV-05,
/// GLM-DATA-12/13): no SQL statement splitting/parsing machinery and no HTTP
/// migration surface anywhere in production code. Whole-script execution is
/// additionally proven behaviorally by MigrationRunnerTests.
/// </summary>
public class MigrationArchitectureGuardTests
{
    private static readonly string[] ForbiddenTypeMarkers =
    [
        // SQL parsing/splitting machinery (prohibited by GLM-DATA-12)
        "sqlparser",
        "sqlsplitter",
        "statementsplit",
        "sqlstatementreader",
        // HTTP migration surfaces (prohibited by GLM-DATA-13/PV-05)
        "migrationendpoint",
        "httpmigration",
        "adminmigration",
        // Prohibited frameworks (EF Core migrations, DbUp)
        "efmigration",
        "dbup"
    ];

    [Fact]
    public void ProductionAssemblies_ContainNoMigrationParsingOrHttpSurface()
    {
        var productionAssemblies = new[]
        {
            typeof(BA.Dmo.Infrastructure.Persistence.Migrations.MigrationRunner).Assembly,
            typeof(Program).Assembly
        };

        var offenders = productionAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => ForbiddenTypeMarkers.Any(marker =>
                type.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Select(type => $"{type.Assembly.GetName().Name}: {type.FullName}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Migration subsystem must stay full-script Npgsql execution with no HTTP surface. " +
            $"Offenders: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void WebProgram_HasNoMigrationHookBeyondCliVerb()
    {
        // The only migration entry point in the web assembly is the CLI command
        // resolved before startup; no hosted service, endpoint or page may add
        // another migration hook (GLM-DATA-13, PV-05).
        var programAssembly = typeof(Program).Assembly;

        var migrationTypes = programAssembly.GetTypes()
            .Where(type => type.Name.Contains("Migration", StringComparison.OrdinalIgnoreCase))
            .Where(type => !IsCompilerGeneratedStateMachine(type))
            .Where(type => type != typeof(BA.Dmo.Web.Cli.MigrateCommand))
            .ToList();

        Assert.Empty(migrationTypes);
    }

    private static bool IsCompilerGeneratedStateMachine(Type type) =>
        type.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is not null;
}
