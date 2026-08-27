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
    public void ShippedFreshBuildFamily_IsComplete_N01ThroughN31()
    {
        var familyDirectory = ResolveRepositoryMigrationsDirectory();

        var discovered = MigrationDiscovery.Discover(familyDirectory);

        Assert.Equal(
            [
                "N01_identity.sql", "N02_catalog.sql", "N03_bq.sql", "N04_ferramentas.sql",
                "N05_jobon.sql", "N06_peso.sql", "N07_pegamentos.sql", "N08_reparacoes.sql",
                "N09_armazem.sql", "N10_tampoes.sql", "N11_partilhado.sql", "N12_rls.sql",
                "N13_jobon_production_folder.sql", "N14_pegamentos_documents.sql",
                "N15_pegamentos_tool_number.sql", "N16_pegamentos_component_nominals.sql",
                "N17_pegamentos_notas.sql", "N18_bq_repairer.sql", "N19_tool_usage.sql", "N20_repairer_repair_types.sql", "N21_tampoes_machines.sql", "N22_reparacao_interna_context.sql", "N23_controlo_folha.sql", "N24_jobon_user_current.sql", "N25_remediation.sql", "N26_user_modules_override.sql", "N27_access_convergence.sql", "N28_reparacao_interna_cm_mf_only.sql", "N29_jobon_reference_images.sql", "N30_jobon_reference_image_updated_by_index.sql", "N31_template_profiles_single_assignment.sql"
            ],
            discovered.Select(m => m.FileName).ToArray());
    }

    [Fact]
    public void N28_FailsClosedAndNarrowsInternalRepairTypeToCmMf()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N28_reparacao_interna_cm_mf_only.sql"));

        Assert.Contains("WHERE tool_type NOT IN ('CM', 'MF')", sql, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", sql, StringComparison.Ordinal);
        Assert.Contains("CHECK (tool_type IN ('CM', 'MF'))", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CHECK (tool_type IN ('CM', 'MF', 'BQ'))", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void N29_FailsClosedAndCreatesReferenceOwnedImageAssociation()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N29_jobon_reference_images.sql"));

        Assert.Contains("CREATE TABLE IF NOT EXISTS article_reference_images", sql, StringComparison.Ordinal);
        Assert.Contains("reference_code  text        PRIMARY KEY", sql, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", sql, StringComparison.Ordinal);
        Assert.Contains("count(DISTINCT image_asset_id) > 1", sql, StringComparison.Ordinal);
        Assert.Contains("ENABLE ROW LEVEL SECURITY", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM job_on_revision", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void N30_AddsCoveringIndexForReferenceImageUpdaterForeignKey()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N30_jobon_reference_image_updated_by_index.sql"));

        Assert.Contains("ix_article_reference_images_updated_by", sql, StringComparison.Ordinal);
        Assert.Contains("ON article_reference_images (updated_by)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void N31_EnforcesSingleTemplateAndClosedProfile()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N31_template_profiles_single_assignment.sql"));

        Assert.Contains("access_template_profiles", sql, StringComparison.Ordinal);
        Assert.Contains("'Admin', 'Operador / Controlador', 'Responsável'", sql, StringComparison.Ordinal);
        Assert.Contains("ux_internal_user_access_templates_actor", sql, StringComparison.Ordinal);
        Assert.Contains("UPDATE internal_users", sql, StringComparison.Ordinal);
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
