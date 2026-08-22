using BA.Dmo.Infrastructure.Persistence.Migrations;

namespace BA.Dmo.IntegrationTests.Migrations;

/// <summary>
/// U-02 test area 1: deterministic migration discovery/order
/// (Plan-V3 PV-04, BT-08, 06_DATA §2).
/// </summary>
public sealed class MigrationDiscoveryTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ba_dmo_migrations_").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private void WriteFile(string name, string content = "-- script") =>
        File.WriteAllText(Path.Combine(_directory, name), content);

    [Fact]
    public void Discover_ReturnsCanonicalOrdinal_EvenWhenCreatedOutOfOrder()
    {
        WriteFile("N12_rls.sql");
        WriteFile("N03_bq.sql");
        WriteFile("N01_identity.sql");
        WriteFile("N10_tampoes.sql");
        WriteFile("N02_catalog.sql");

        var discovered = MigrationDiscovery.Discover(_directory);

        Assert.Equal(
            ["N01_identity.sql", "N02_catalog.sql", "N03_bq.sql", "N10_tampoes.sql", "N12_rls.sql"],
            discovered.Select(m => m.FileName).ToArray());
        Assert.Equal(
            ["N01", "N02", "N03", "N10", "N12"],
            discovered.Select(m => m.Version).ToArray());
    }

    [Fact]
    public void Discover_IsDeterministic_AcrossRepeatedCalls()
    {
        WriteFile("N01_identity.sql");
        WriteFile("N02_catalog.sql");

        var first = MigrationDiscovery.Discover(_directory);
        var second = MigrationDiscovery.Discover(_directory);

        Assert.Equal(first.Select(m => m.FileName), second.Select(m => m.FileName));
        Assert.Equal(first.Select(m => m.FullPath), second.Select(m => m.FullPath));
    }

    [Fact]
    public void Discover_RejectsFileOutsideTheFamilyPattern()
    {
        WriteFile("N01_identity.sql");
        WriteFile("README.sql");

        Assert.Throws<MigrationDiscoveryException>(() => MigrationDiscovery.Discover(_directory));
    }

    [Fact]
    public void Discover_RejectsDuplicateVersions()
    {
        WriteFile("N04_first.sql");
        WriteFile("N04_second.sql");

        var ex = Assert.Throws<MigrationDiscoveryException>(
            () => MigrationDiscovery.Discover(_directory));

        Assert.Contains("N04", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Discover_MissingDirectory_FailsExplicitly()
    {
        Assert.Throws<MigrationDiscoveryException>(
            () => MigrationDiscovery.Discover(Path.Combine(_directory, "does_not_exist")));
    }

    [Fact]
    public void Discover_IgnoresNonSqlFiles()
    {
        WriteFile("N01_identity.sql");
        File.WriteAllText(Path.Combine(_directory, ".gitkeep"), string.Empty);

        var discovered = MigrationDiscovery.Discover(_directory);

        Assert.Single(discovered);
    }

    [Fact]
    public void ShippedFreshBuildFamily_IsComplete_N01ThroughN26()
    {
        // The authoritative family from 06_DATA §2 must ship whole and ordered.
        // N25 = deployment-readiness remediation (owner decisions D1-D7);
        // N26 = per-user module grant override (owner contract §6).
        var familyDirectory = ResolveRepositoryMigrationsDirectory();

        var discovered = MigrationDiscovery.Discover(familyDirectory);

        Assert.Equal(
            [
                "N01_identity.sql", "N02_catalog.sql", "N03_bq.sql", "N04_ferramentas.sql",
                "N05_jobon.sql", "N06_peso.sql", "N07_pegamentos.sql", "N08_reparacoes.sql",
                "N09_armazem.sql", "N10_tampoes.sql", "N11_partilhado.sql", "N12_rls.sql",
                "N13_jobon_production_folder.sql", "N14_pegamentos_documents.sql",
                "N15_pegamentos_tool_number.sql", "N16_pegamentos_component_nominals.sql",
                "N17_pegamentos_notas.sql", "N18_bq_repairer.sql", "N19_tool_usage.sql", "N20_repairer_repair_types.sql", "N21_tampoes_machines.sql", "N22_reparacao_interna_context.sql", "N23_controlo_folha.sql", "N24_jobon_user_current.sql", "N25_remediation.sql", "N26_user_modules_override.sql"
            ],
            discovered.Select(m => m.FileName).ToArray());
    }

    private static string ResolveRepositoryMigrationsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "database", "migrations");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "N01_identity.sql")))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("database/migrations not found above the test base directory.");
    }
}
