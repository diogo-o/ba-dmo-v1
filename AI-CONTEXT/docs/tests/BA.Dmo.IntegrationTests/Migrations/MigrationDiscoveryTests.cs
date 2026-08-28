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
    public void ShippedFreshBuildFamily_ContainsApprovedHeadIncludingN42()
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
                "N17_pegamentos_notas.sql", "N18_bq_repairer.sql", "N19_tool_usage.sql", "N20_repairer_repair_types.sql", "N21_tampoes_machines.sql", "N22_reparacao_interna_context.sql", "N23_controlo_folha.sql", "N24_jobon_user_current.sql", "N25_remediation.sql", "N26_user_modules_override.sql", "N27_access_convergence.sql", "N28_reparacao_interna_cm_mf_only.sql", "N29_jobon_reference_images.sql", "N30_jobon_reference_image_updated_by_index.sql", "N31_template_profiles_single_assignment.sql", "N32_access_authority_convergence.sql", "N33_legacy_access_mirror_quiescence.sql", "N34_legacy_access_mirror_removal.sql", "N35_index_rationalization.sql", "N36_ba_dmo_app_access_policy_rename.sql", "N37_peso_previous_comparison_removal.sql", "N38_dormant_column_removal.sql", "N39_pegamentos_contra_costura_nullable.sql", "N40_peso_leituras_approved_guard.sql", "N41_warehouse_active_position_unique.sql", "N42_tool_check_occurrences_removal.sql"
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

    // ------------------------------------------------------------------
    // N34 — legacy access mirror REMOVAL (SCHEMA-RAT-03B → N34).
    // Source guards only: they read the migration FILE. Executed
    // PostgreSQL behaviour (catalog absence + 42P01/42703) is covered by the
    // env-guarded RemediationGuardTests.N34_* probes (BA_DMO_TEST_DATABASE).
    // ------------------------------------------------------------------

    [Fact]
    public void N34_RemovesBothLegacyAccessMirrors_Explicitly_NoCascade()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N34_legacy_access_mirror_removal.sql"));

        // Option A statement set (N34 audit §5): junction drop, explicit
        // constraint drop BEFORE the column drop, then the column drop.
        Assert.Contains(
            "DROP TABLE IF EXISTS internal_user_access_templates",
            sql, StringComparison.Ordinal);
        var tableDrop = sql.IndexOf(
            "DROP TABLE IF EXISTS internal_user_access_templates", StringComparison.Ordinal);
        var constraintDrop = sql.IndexOf(
            "DROP CONSTRAINT IF EXISTS ck_internal_users_functional_profile",
            StringComparison.Ordinal);
        var columnDrop = sql.IndexOf(
            "DROP COLUMN IF EXISTS profile_title", StringComparison.Ordinal);
        Assert.True(tableDrop >= 0 && constraintDrop > tableDrop && columnDrop > constraintDrop,
            "N34 must drop the junction, then the CHECK constraint, then the column (explicit Option A order).");

        // No CASCADE anywhere; the column drop follows the explicit constraint
        // drop (same-table dependent-object removal is never relied on).
        Assert.DoesNotContain("CASCADE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE IF EXISTS internal_user_access_templates CASCADE",
            sql, StringComparison.OrdinalIgnoreCase);

        // The authority chain is NOT touched: no DROP targets
        // access_template_profiles / access_templates and no incidental
        // policy/function/trigger drops exist.
        Assert.DoesNotContain("DROP TABLE IF EXISTS access_template_profiles", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE access_templates", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP POLICY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP FUNCTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TRIGGER", sql, StringComparison.OrdinalIgnoreCase);

        // No transaction-control debt; no data rewrites.
        Assert.DoesNotContain("BEGIN;", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("COMMIT;", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // N35 — safe index / constraint rationalization (BQ-16 + redundant drop).
    // Source guards only: they read the migration FILE. Executed catalog
    // verification (index present/absent) is covered by the env-guarded
    // RemediationGuardTests.N35_* probes (BA_DMO_TEST_DATABASE).
    // ------------------------------------------------------------------

    [Fact]
    public void N35_AddsBqRepairerIndex_AndDropsRedundantPegamentoDocumentosIndex()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N35_index_rationalization.sql"));

        // BQ-16 additive index on bq_movements (noted_repairer_id).
        Assert.Contains("ix_bq_movements_noted_repairer", sql, StringComparison.Ordinal);
        Assert.Contains("ON bq_movements (noted_repairer_id)", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX IF NOT EXISTS", sql, StringComparison.Ordinal);

        // Redundant pegamento_documentos index removal (duplicates the UNIQUE).
        Assert.Contains(
            "DROP INDEX IF EXISTS ix_pegamento_documentos_controlo",
            sql, StringComparison.Ordinal);

        // N35 must NOT widen into owner-gated items: no table/column drops, no
        // CHECK rewrites (the optional BQ-10 'fim' CHECK trim is OD-16, not
        // SAFE NOW, and is deliberately excluded).
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP CONSTRAINT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ck_bq_movements_type", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("'fim'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CASCADE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN;", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("COMMIT;", sql, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // N36 — D-15 RLS policy-name convention (access_template_profiles_app_access
    // → ba_dmo_app_access). Source guards only: they read the migration FILE.
    // Executed policy-inventory equality is covered by the env-guarded
    // RemediationGuardTests.N36_* probes (BA_DMO_TEST_DATABASE).
    // ------------------------------------------------------------------

    [Fact]
    public void N36_UnifiesPolicyNaming_WithIdenticalSemantics()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N36_ba_dmo_app_access_policy_rename.sql"));

        // Old divergent name dropped, canonical name (re)created.
        Assert.Contains(
            "DROP POLICY IF EXISTS access_template_profiles_app_access",
            sql, StringComparison.Ordinal);
        Assert.Contains("CREATE POLICY ba_dmo_app_access", sql, StringComparison.Ordinal);

        // Semantics preserved byte-for-byte: FOR ALL TO ba_dmo_app, USING
        // (TRUE), WITH CHECK (TRUE) — the N12/N25/N29 convention.
        Assert.Contains("FOR ALL TO ba_dmo_app", sql, StringComparison.Ordinal);
        Assert.Contains("USING (TRUE)", sql, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK (TRUE)", sql, StringComparison.Ordinal);

        // Security naming rationalization ONLY: no permission surface is
        // touched (DML entitlements were issued by N31) and no table/object
        // drops occur (the junction policy died with its table in N34, so N36
        // never renames it).
        Assert.DoesNotContain("GRANT", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("REVOKE", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CASCADE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN;", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("COMMIT;", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void N37_RemovesOnlyEmptyLegacyPesoComparisonTable_NoCascade()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N37_peso_previous_comparison_removal.sql"));

        Assert.Contains("SELECT EXISTS (SELECT 1 FROM public.peso_comparacao_anterior)", sql, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", sql, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE IF EXISTS peso_comparacao_anterior", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CASCADE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("previous_control", sql.Split("DROP TABLE IF EXISTS", StringSplitOptions.None)[1], StringComparison.Ordinal);
    }

    [Fact]
    public void N41_AddsOnlyPerPositionActiveOccupationUniqueIndex()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N41_warehouse_active_position_unique.sql"));

        Assert.Contains("CREATE UNIQUE INDEX IF NOT EXISTS uq_warehouse_stock_active_position", sql, StringComparison.Ordinal);
        Assert.Contains("ON warehouse_stock (warehouse_location_id)", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE released_at_utc IS NULL", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void N38_DropsOnlyDormantColumns_AndReissuesCanonicalGrants()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N38_dormant_column_removal.sql"));

        Assert.Contains("WHERE modules_override IS NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE image_asset_id IS NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("DROP COLUMN IF EXISTS modules_override", sql, StringComparison.Ordinal);
        Assert.Contains("DROP COLUMN IF EXISTS image_asset_id", sql, StringComparison.Ordinal);
        Assert.Contains("GRANT SELECT (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("modules_override) ON internal_users TO ba_dmo_app", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // N39 — pegamento_medicoes.contra_costura nullable (one-sided
    // measurements NON-blocking; owner D-12/OD-2). Source guards only;
    // executed behavior is covered by the env-guarded probes in
    // RemediationGuardTests.N39_* and PegamentoPersistencePostgresTests.
    // ------------------------------------------------------------------

    [Fact]
    public void N39_WidensContraCosturaOnly_NeverTouchesData()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N39_pegamentos_contra_costura_nullable.sql"));

        Assert.Contains("ALTER TABLE pegamento_medicoes", sql, StringComparison.Ordinal);
        Assert.Contains("ALTER COLUMN contra_costura DROP NOT NULL", sql, StringComparison.Ordinal);

        // Absence must never become a DB blocker: the widening is the WHOLE
        // change — no new CHECK, no other column/tables touched, no data
        // rewrites, no transaction-control debt.
        Assert.DoesNotContain("CHECK", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN;", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("COMMIT;", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // N40 — approved Peso readings protection (owner D-10/OD-3 Go; refined
    // design). Source guards only; executed behavior is covered by the
    // env-guarded RemediationGuardTests.N40_* probes.
    // ------------------------------------------------------------------

    [Fact]
    public void N40_AddsReadingsApprovedGuard_WithoutTouchingOtherObjects()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N40_peso_leituras_approved_guard.sql"));

        Assert.Contains("ba_dmo_guard_peso_leituras_approved", sql, StringComparison.Ordinal);
        Assert.Contains("BEFORE INSERT OR UPDATE OR DELETE ON peso_leituras", sql, StringComparison.Ordinal);
        Assert.Contains("trg_peso_leituras_approved_guard", sql, StringComparison.Ordinal);
        Assert.Contains("parent_status = 'aprovado'", sql, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", sql, StringComparison.Ordinal);

        // The guard must not be a naive trigger that breaks approve/reopen:
        // it is the readings backstop only — no DDL on peso_controlos, no
        // policy/grant changes, no drops of unrelated objects.
        Assert.DoesNotContain("ALTER TABLE peso_controlos", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP COLUMN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GRANT", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("REVOKE", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN;", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("COMMIT;", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // N42 — tool_check_occurrences removal (owner OD-6/PA-01: REMOVE).
    // Source guards only; executed catalog absence is covered by the
    // env-guarded RemediationGuardTests.N42_* probes.
    // ------------------------------------------------------------------

    [Fact]
    public void N42_RemovesOnlyTheOccurrenceTwin_WithRowCountGuard()
    {
        var sql = File.ReadAllText(Path.Combine(
            ResolveRepositoryMigrationsDirectory(),
            "N42_tool_check_occurrences_removal.sql"));

        // Fail-closed data guard before any drop.
        Assert.Contains("SELECT EXISTS (SELECT 1 FROM public.tool_check_occurrences)", sql, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", sql, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE IF EXISTS tool_check_occurrences", sql, StringComparison.Ordinal);

        // Only the twin is removed: no CASCADE, no other DROP targets, no
        // grants/policies touched, no transaction-control debt.
        Assert.DoesNotContain("CASCADE", sql, StringComparison.OrdinalIgnoreCase);
        var drops = sql.Split('\n')
            .Where(line => line.TrimStart().StartsWith("DROP TABLE", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Trim())
            .ToList();
        Assert.Single(drops);
        Assert.Contains("DROP TABLE IF EXISTS tool_check_occurrences", drops[0], StringComparison.Ordinal);
        Assert.DoesNotContain("GRANT", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("REVOKE", sql, StringComparison.Ordinal);
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
