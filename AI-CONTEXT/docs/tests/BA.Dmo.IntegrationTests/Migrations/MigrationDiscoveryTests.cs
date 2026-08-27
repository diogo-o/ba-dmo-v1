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
    public void ShippedFreshBuildFamily_IsComplete_N01ThroughN33()
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
                "N17_pegamentos_notas.sql", "N18_bq_repairer.sql", "N19_tool_usage.sql", "N20_repairer_repair_types.sql", "N21_tampoes_machines.sql", "N22_reparacao_interna_context.sql", "N23_controlo_folha.sql", "N24_jobon_user_current.sql", "N25_remediation.sql", "N26_user_modules_override.sql", "N27_access_convergence.sql", "N28_reparacao_interna_cm_mf_only.sql", "N29_jobon_reference_images.sql", "N30_jobon_reference_image_updated_by_index.sql", "N31_template_profiles_single_assignment.sql", "N32_access_authority_convergence.sql", "N33_legacy_access_mirror_quiescence.sql"
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

    // ------------------------------------------------------------------
    // N32 — access-authority convergence (SCHEMA-RAT-03A, D-1/D-2)
    // Source guards only: they read the migration FILE. Executed
    // PostgreSQL behaviour is covered by the env-guarded
    // RemediationGuardTests.N32_* probes (BA_DMO_TEST_DATABASE).
    // ------------------------------------------------------------------

    [Fact]
    public void N32_IsNonDestructive_AndLeavesLegacyObjectsInPlace()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N32_access_authority_convergence.sql"));

        // No destructive DDL anywhere in N32.
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP INDEX", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
        // Legacy objects stay physically present this phase.
        Assert.Contains("internal_user_access_templates", sql, StringComparison.Ordinal);
        Assert.Contains("profile_title", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void N32_FailsClosed_OnConflictingOrMultipleLegacyAssignments()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N32_access_authority_convergence.sql"));

        // Multiple junction rows per user are never silently collapsed.
        Assert.Contains("HAVING COUNT(*) > 1", sql, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", sql, StringComparison.Ordinal);
        // A single junction row disputing the canonical direct FK fails too.
        Assert.Contains("ut.template_id IS DISTINCT FROM u.template_id", sql, StringComparison.Ordinal);
        // No MIN()/MAX()/first/latest invention of authority.
        Assert.DoesNotContain("MIN(", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("MAX(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void N32_KeepsProfileAuthorityTemplateOwned_WithoutCopyingUserProfiles()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N32_access_authority_convergence.sql"));

        // Profile repair uses ONLY the N31-established deterministic default.
        Assert.Contains("lower(t.name) LIKE '%respons%'", sql, StringComparison.Ordinal);
        Assert.Contains("'Operador / Controlador'", sql, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT (template_id) DO NOTHING", sql, StringComparison.Ordinal);
        // User profile_title is NEVER copied back into access_template_profiles.
        string head;
        var insertStart = sql.IndexOf(
            "INSERT INTO access_template_profiles", StringComparison.Ordinal);
        Assert.True(insertStart >= 0);
        head = sql[..insertStart];
        Assert.DoesNotContain("SELECT p.functional_profile", head, StringComparison.Ordinal);
        // The INSERT selects from access_templates, not from internal_users.
        Assert.Contains("FROM access_templates t", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void N32_DoesNotRepeatInnerTransactionControlDebt()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N32_access_authority_convergence.sql"));

        Assert.DoesNotContain("BEGIN;", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("COMMIT;", sql, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // N33 — legacy access mirror quiescence (SCHEMA-RAT-03B)
    // Source guards only: they read the migration FILE. Executed
    // PostgreSQL behaviour is covered by the env-guarded
    // RemediationGuardTests.N33_* probes (BA_DMO_TEST_DATABASE).
    // ------------------------------------------------------------------

    [Fact]
    public void N33_IsNonDestructive_AndQuiescesBothLegacyMirrors()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N33_legacy_access_mirror_quiescence.sql"));

        // 1. Relaxes the retired mirror column to NULLABLE (N27 made it NOT
        // NULL) — the column itself stays physical with its fossil values.
        Assert.Contains("ALTER TABLE internal_users", sql, StringComparison.Ordinal);
        Assert.Contains("ALTER COLUMN profile_title DROP NOT NULL", sql, StringComparison.Ordinal);
        // 2. Junction kill switch: ba_dmo_app loses ALL privileges.
        Assert.Contains(
            "REVOKE ALL PRIVILEGES ON TABLE internal_user_access_templates FROM ba_dmo_app",
            sql, StringComparison.Ordinal);
        // 3. profile_title kill switch (privilege REFACTOR): ba_dmo_app held
        // TABLE-LEVEL SELECT/INSERT/UPDATE on internal_users — a table-level
        // grant implies every column, so a column-level REVOKE alone could
        // never close the mirror. The correction revokes those three
        // table-level grants and restores them at COLUMN level for every
        // current internal_users column EXCEPT profile_title (explicit
        // list; DELETE stays table-level, untouched).
        Assert.Contains(
            "REVOKE SELECT, INSERT, UPDATE ON internal_users FROM ba_dmo_app",
            sql, StringComparison.Ordinal);
        var grantColumns =
            "actor_id, auth_user_id, template_id, display_name, "
            + "active, created_at_utc, updated_at_utc, modules_override";
        foreach (var privilege in new[] { "SELECT", "INSERT", "UPDATE" })
        {
            Assert.Contains(
                $"GRANT {privilege} ({grantColumns}) ON internal_users TO ba_dmo_app",
                sql, StringComparison.Ordinal);
        }
        // The insufficient column-level REVOKE approach is gone; no GRANT
        // names the retired mirror column; DELETE is never revoked.
        Assert.DoesNotContain("REVOKE SELECT (profile_title)", sql, StringComparison.Ordinal);
        foreach (var line in sql.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("GRANT ", StringComparison.Ordinal)
                || trimmed.StartsWith("EXECUTE 'GRANT ", StringComparison.Ordinal))
            {
                Assert.DoesNotContain("profile_title", trimmed, StringComparison.Ordinal);
            }
        }
        Assert.DoesNotContain("REVOKE DELETE", sql, StringComparison.Ordinal);

        // Non-destructive bounds: no drops/renames of tables, columns or
        // indexes; no data rewrites; no transaction-control debt.
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP INDEX", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP SCHEMA", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN;", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("COMMIT;", sql, StringComparison.Ordinal);
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
