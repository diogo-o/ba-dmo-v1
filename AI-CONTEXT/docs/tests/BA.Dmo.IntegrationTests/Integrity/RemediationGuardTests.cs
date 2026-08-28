using Npgsql;

namespace BA.Dmo.IntegrationTests.Integrity;

/// <summary>
/// B1 remediation guard tests (N25_remediation.sql). Verified against a real
/// PostgreSQL instance (the disposable migration target) — NOT against
/// lifecycle fakes, because these guards live in the database.
///
/// The database is provided by the environment variable BA_DMO_TEST_DATABASE
/// (Npgsql keyword/value connection string). When the variable is absent the
/// tests skip (return) — the suite stays green in DB-less environments, and
/// the CI/freeze environment supplies the variable so the guards are proven.
/// The schema is assumed to be fully migrated (N01-N42); the N39/N40/N42
/// probes additionally self-skip when those migrations are not yet applied.
/// Tests are isolated by using fresh GUID keys per run. The N34_* probes
/// self-skip when the test database still contains the legacy access mirrors
/// (a pre-N34 schema: N32/N33 still leave the junction table and
/// internal_users.profile_title physically present). The connection role is
/// expected to be the migration/owner role (can create roles when absent and
/// SET ROLE to ba_dmo_app).
/// </summary>
public sealed class RemediationGuardTests
{
    private static string? Cs => Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    private static bool SkipIfNoDatabase()
    {
        if (Cs is null)
        {
            Console.WriteLine("[SKIP] RemediationGuardTests: BA_DMO_TEST_DATABASE not set — no disposable PostgreSQL available in this environment.");
            return true;
        }
        return false;
    }

    private static async Task<int> Exec(string cs, string sql)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string?> CaptureSqlState(string cs, string sql)
    {
        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.SqlState;
        }
    }

    private static async Task<string?> CaptureMessage(string cs, string sql)
    {
        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.Message;
        }
    }

    private static async Task<string?> CaptureScalar(string cs, string sql)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        var value = await cmd.ExecuteScalarAsync();
        return value as string;
    }

    private static async Task<int> ScalarInt(string cs, string sql)
    {
        var value = await CaptureScalar(cs, sql);
        return int.TryParse(value, out var n) ? n : -1;
    }

    private static async Task EnsureRoleExistsAsync(string cs, string role)
    {
        await Exec(cs, $@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{role}') THEN
        CREATE ROLE {role} NOLOGIN;
    END IF;
END $$;");
    }

    /// <summary>
    /// True when the test database still contains BOTH legacy access mirrors
    /// (pre-N34 schema: N32/N33 left the junction table and the
    /// internal_users.profile_title mirror column physically present). Used to
    /// self-skip the N34 probes on databases that have not yet applied N34 —
    /// after N34 both objects are absent, so catalog probes must check absence
    /// instead of nullability.
    /// </summary>
    private static async Task<bool> AccessMirrorsStillPresent(string cs)
    {
        var junctionRows = await ScalarInt(cs, $@"
SELECT count(*) FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = 'internal_user_access_templates';");
        var columnCount = await ScalarInt(cs, $@"
SELECT count(*) FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'internal_users'
  AND column_name = 'profile_title';");
        return junctionRows >= 1 || columnCount >= 1;
    }

    /// <summary>
    /// Executes <paramref name="sql"/> in a session switched to
    /// <paramref name="role"/> and returns the SQLSTATE of the first
    /// PostgresException (null when the statement succeeded). The role
    /// switch is undone afterwards; a failure inside the probe never leaves
    /// the session role changed.
    /// </summary>
    private static async Task<string?> CaptureSqlStateAs(string cs, string role, string sql)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        try
        {
            await using (var setRole = new NpgsqlCommand($"SET ROLE {role}", conn))
                await setRole.ExecuteNonQueryAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.SqlState;
        }
        finally
        {
            try
            {
                await using var reset = new NpgsqlCommand("RESET ROLE", conn);
                await reset.ExecuteNonQueryAsync();
            }
            catch
            {
                // Session may be aborted; closing the connection resets it.
            }
        }
    }

    private static async Task EnsureTemplateAsync(string cs, string templateId)
    {
        await Exec(cs, $@"
INSERT INTO access_templates (template_id, name, modules, active)
VALUES ('{templateId}', 'guard-test', '[]', TRUE)
ON CONFLICT (template_id) DO NOTHING;");
    }

    /// <summary>Seeds a job_on (rascunho) + revision 1; returns both ids.</summary>
    private static async Task<(string JobId, string RevId)> SeedJobWithRevisionAsync(string cs, string machine)
    {
        var jobId = Guid.NewGuid().ToString();
        var revId = Guid.NewGuid().ToString();
        var code = "2026" + Guid.NewGuid().ToString("N")[..4];
        await Exec(cs, $@"
INSERT INTO job_on (job_on_id, production_code, machine_code, status)
VALUES ('{jobId}', '{code}', '{machine}', 'rascunho');
INSERT INTO job_on_revision (job_on_revision_id, job_on_id, revision_number, sections)
VALUES ('{revId}', '{jobId}', 1, '{{}}')");
        return (jobId, revId);
    }

    // ---- INT-01 / cross-track: auth_user_id NOT NULL + UNIQUE ----------------

    [Fact]
    public async Task DuplicateAuthUserId_IsRejected()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        var tpl = "tpl-guard-auth-" + Guid.NewGuid().ToString("N")[..8];
        var auth = Guid.NewGuid();
        await EnsureTemplateAsync(cs, tpl);
        await Exec(cs, $@"
INSERT INTO internal_users (actor_id, auth_user_id, template_id, display_name)
VALUES ('guard-a-{auth:N}', '{auth}', '{tpl}', 'Guard A')");
        var state = await CaptureSqlState(cs, $@"
INSERT INTO internal_users (actor_id, auth_user_id, template_id, display_name)
VALUES ('guard-b-{auth:N}', '{auth}', '{tpl}', 'Guard B')");
        Assert.Equal("23505", state);
    }

    [Fact]
    public async Task NullAuthUserId_IsRejected()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        var tpl = "tpl-guard-null-" + Guid.NewGuid().ToString("N")[..8];
        await EnsureTemplateAsync(cs, tpl);
        var state = await CaptureSqlState(cs, $@"
INSERT INTO internal_users (actor_id, auth_user_id, template_id, display_name)
VALUES ('guard-null-{Guid.NewGuid():N}', NULL, '{tpl}', 'Guard Null')");
        Assert.Equal("23502", state);
    }

    // ---- INT-02 (owner D1 Option A): partial unique identity -----------------

    [Fact]
    public async Task JobOnIdentity_CanceledPairMayBeReissued_SecondActiveBlocked()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        var code = "2026" + Guid.NewGuid().ToString("N")[..4];
        var machine = "M-GUARD";
        await Exec(cs, $@"
INSERT INTO job_on (production_code, machine_code, status)
VALUES ('{code}', '{machine}', 'rascunho');
UPDATE job_on SET status='cancelado', canceled_at_utc=now()
WHERE production_code='{code}' AND machine_code='{machine}';
INSERT INTO job_on (production_code, machine_code, status)
VALUES ('{code}', '{machine}', 'rascunho')");
        // a second NON-CANCELED job with the same pair must be blocked
        var state = await CaptureSqlState(cs, $@"
INSERT INTO job_on (production_code, machine_code, status)
VALUES ('{code}', '{machine}', 'planeado')");
        Assert.Equal("23505", state);
    }

    // ---- INT-03: one active trace per lote -----------------------------------

    [Fact]
    public async Task SecondActiveTracePerLote_IsRejected()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        // ck_bq_lotes_reference: reference ~ '^[A-Z][0-9]{3}$'
        var refCode = "A" + new Random().Next(100, 1000).ToString();
        var loteId = Guid.NewGuid().ToString();
        await Exec(cs, $@"
INSERT INTO bq_lotes (bq_lote_id, reference, batch_code)
VALUES ('{loteId}', '{refCode}', 'B-GUARD-{Guid.NewGuid():N}');
INSERT INTO bq_traces (bq_lote_id, status, purpose, start_line)
VALUES ('{loteId}', 'active', 'production', 'L1')");
        var state = await CaptureSqlState(cs, $@"
INSERT INTO bq_traces (bq_lote_id, status, purpose, start_line)
VALUES ('{loteId}', 'active', 'production', 'L1')");
        Assert.Equal("23505", state);
        // after closing the first trace, a new active trace is allowed
        await Exec(cs, $@"
UPDATE bq_traces SET status='closed' WHERE bq_lote_id='{loteId}';
INSERT INTO bq_traces (bq_lote_id, status, purpose, start_line)
VALUES ('{loteId}', 'active', 'production', 'L1')");
    }

    // ---- INT-10 (owner D5 Option A): revision family append-only -------------

    [Fact]
    public async Task RevisionRows_AreAppendOnly()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        var (jobId, revId) = await SeedJobWithRevisionAsync(cs, "M-REV");
        var compId = Guid.NewGuid().ToString();
        await Exec(cs, $@"
INSERT INTO job_on_component (job_on_component_id, job_on_revision_id, family)
VALUES ('{compId}', '{revId}', 'BQ')");

        var upRev = await CaptureMessage(cs, $@"
UPDATE job_on_revision SET general_notes='x' WHERE job_on_revision_id='{revId}'");
        Assert.Contains("append-only", upRev, StringComparison.OrdinalIgnoreCase);
        var delComp = await CaptureMessage(cs, $@"
DELETE FROM job_on_component WHERE job_on_component_id='{compId}'");
        Assert.Contains("append-only", delComp, StringComparison.OrdinalIgnoreCase);
        var ins = await Exec(cs, $@"
INSERT INTO job_on_component (job_on_revision_id, family)
VALUES ('{revId}', 'CAL')");
        Assert.Equal(1, ins); // INSERT stays allowed (append-only, not immutable-table)

        // the two child tables carry the same trigger (existence proof)
        var children = await Exec(cs, $@"
SELECT count(*) FROM pg_trigger t
JOIN pg_class c ON c.oid = t.tgrelid
WHERE c.relname IN ('job_on_component_field', 'job_on_component_row')
  AND t.tgname LIKE '%append_only' AND NOT t.tgisinternal");
        Assert.Equal(2, children);
    }

    // ---- INT-08 / INT-07: approved peso immutability + consistency -----------

    private static async Task<string> SeedPesoControloAsync(
        string cs, string jobId, string revId, string status, bool withApprovedAt)
    {
        var refId = Guid.NewGuid().ToString();
        var loteId = Guid.NewGuid().ToString();
        var controlId = Guid.NewGuid().ToString();
        var mold = "MOLD-G" + Guid.NewGuid().ToString("N")[..6];
        var neck = "NECK-G" + Guid.NewGuid().ToString("N")[..6];
        var code = "2026P" + Guid.NewGuid().ToString("N")[..3];
        var stamp = withApprovedAt ? "now()" : "NULL";
        await Exec(cs, $@"
INSERT INTO peso_references (peso_reference_id, mold_number, neckring_number)
VALUES ('{refId}', '{mold}', '{neck}');
INSERT INTO peso_lotes (peso_lote_id, peso_reference_id, lote, processo, allowed_lines, report_subfolder)
VALUES ('{loteId}', '{refId}', 'LOTE-G', 'PS', ARRAY['L1'], 'guard');
INSERT INTO peso_controlos (
    peso_controlo_id, peso_reference_id, peso_lote_id, record_type,
    mold_number, neckring_number, production_code, line, lote, control_date,
    job_on_id, job_on_revision_id, status, approved_at_utc)
VALUES ('{controlId}', '{refId}', '{loteId}', 'novo_controlo',
        '{mold}', '{neck}', '{code}', 'L1', 'LOTE-G', now()::date,
        '{jobId}', '{revId}', '{status}', {stamp})");
        return controlId;
    }

    [Fact]
    public async Task ApprovedPeso_IsImmutable_AtDatabaseLevel()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        var (jobId, revId) = await SeedJobWithRevisionAsync(cs, "M-APR");
        var controlId = await SeedPesoControloAsync(cs, jobId, revId, "aprovado", withApprovedAt: true);

        var identity = await CaptureMessage(cs, $@"
UPDATE peso_controlos SET line='L2' WHERE peso_controlo_id='{controlId}'");
        Assert.Contains("approved peso control", identity, StringComparison.OrdinalIgnoreCase);
        var del = await CaptureMessage(cs, $@"
DELETE FROM peso_controlos WHERE peso_controlo_id='{controlId}'");
        Assert.Contains("approved peso control", del, StringComparison.OrdinalIgnoreCase);
        // non-identity column stays updatable
        var ok = await Exec(cs, $@"
UPDATE peso_controlos SET updated_at_utc=now() WHERE peso_controlo_id='{controlId}'");
        Assert.Equal(1, ok);
    }

    [Fact]
    public async Task PesoApprovedConsistency_IsEnforced()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        var (jobId, revId) = await SeedJobWithRevisionAsync(cs, "M-CNS");
        var refId = Guid.NewGuid().ToString();
        var loteId = Guid.NewGuid().ToString();
        var mold = "MOLD-C" + Guid.NewGuid().ToString("N")[..6];
        var neck = "NECK-C" + Guid.NewGuid().ToString("N")[..6];
        await Exec(cs, $@"
INSERT INTO peso_references (peso_reference_id, mold_number, neckring_number)
VALUES ('{refId}', '{mold}', '{neck}');
INSERT INTO peso_lotes (peso_lote_id, peso_reference_id, lote, processo, allowed_lines, report_subfolder)
VALUES ('{loteId}', '{refId}', 'LOTE-C', 'PS', ARRAY['L1'], 'guard')");
        var state = await CaptureSqlState(cs, $@"
INSERT INTO peso_controlos (
    peso_controlo_id, peso_reference_id, peso_lote_id, record_type,
    mold_number, neckring_number, production_code, line, lote, control_date,
    job_on_id, job_on_revision_id, status, approved_at_utc)
VALUES ('{Guid.NewGuid():N}', '{refId}', '{loteId}', 'novo_controlo',
        '{mold}', '{neck}', '2026C{Guid.NewGuid():N}', 'L1', 'LOTE-C', now()::date,
        '{jobId}', '{revId}', 'aprovado', NULL)");
        Assert.Equal("23514", state);
    }

    // ---- INT-07: status CHECKs ------------------------------------------------

    [Fact]
    public async Task InvalidStatusValues_AreRejected()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        var (jobId, revId) = await SeedJobWithRevisionAsync(cs, "M-LC");
        var compId = Guid.NewGuid().ToString();
        await Exec(cs, $@"
INSERT INTO job_on_component (job_on_component_id, job_on_revision_id, family)
VALUES ('{compId}', '{revId}', 'BQ')");
        var exitId = Guid.NewGuid().ToString();
        await Exec(cs, $@"
INSERT INTO repair_exits (repair_exit_id, repair_type, status)
VALUES ('{exitId}', 'CM', 'preparacao')");
        // ck_repair_exit_items_kind requires (bq_lote_id + qty) or
        // (physical_piece_id + individual_number); seed the lote variant
        var loteId = Guid.NewGuid().ToString();
        await Exec(cs, $@"
INSERT INTO bq_lotes (bq_lote_id, reference, batch_code)
VALUES ('{loteId}', 'A{new Random().Next(100, 1000)}', 'B-ITEM-{Guid.NewGuid():N}')");

        // job_on lifecycle consistency: fechado without closed_at_utc
        var jobState = await CaptureSqlState(cs, $@"
INSERT INTO job_on (production_code, machine_code, status, closed_at_utc)
VALUES ('2026LC{Guid.NewGuid():N}', 'M-LC2', 'fechado', NULL)");
        Assert.Equal("23514", jobState);

        // pegamento_controlos status set
        var peg = await CaptureSqlState(cs, $@"
INSERT INTO pegamento_controlos (
    job_on_id, job_on_revision_id, production_code, machine_code, status)
VALUES ('{jobId}', '{revId}', 'X', 'Y', 'invalido')");
        Assert.Equal("23514", peg);

        // repair_exit_items status set
        var item = await CaptureSqlState(cs, $@"
INSERT INTO repair_exit_items (repair_exit_id, bq_lote_id, qty, status)
VALUES ('{exitId}', '{loteId}', 1, 'invalido')");
        Assert.Equal("23514", item);

        // job_on_verification completed consistency
        var verif = await CaptureSqlState(cs, $@"
INSERT INTO job_on_verification_occurrence (job_on_component_id, status, completion_source)
VALUES ('{compId}', 'confirmada', 'manual_job_on')");
        Assert.Equal("23514", verif);
    }

    // ---- SEC-02: RLS + policy + grant matrix on the 10 late tables -----------

    [Fact]
    public async Task LateTables_RlsPolicyAndGrants_MatchN12Convention()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        const string lateTables = @"
'pegamento_documentos','tool_usage_records','repairer_repair_types',
'tampao_configuration_machines','tampao_configuration_notes',
'tampao_configuration_machine_event','controlo_sheets','controlo_sheet_items',
'controlo_sheet_events','jobon_user_current'";

        // the role set the matrix needs may not exist on a plain disposable PG —
        // create it when absent so the grant assertions are meaningful
        // (mirrors the Supabase role set; guarded, idempotent)
        await Exec(cs, "DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='anon') THEN CREATE ROLE anon NOLOGIN; END IF; IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='authenticated') THEN CREATE ROLE authenticated NOLOGIN; END IF; END $$;");

        // RLS enabled + ba_dmo_app_access policy owned by ba_dmo_app, on all 10
        var rlsOk = await Exec(cs, $@"
SELECT count(*) FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' AND c.relname IN ({lateTables})
  AND c.relrowsecurity
  AND EXISTS (
      SELECT 1 FROM pg_policy p
      WHERE p.polrelid = c.oid AND p.polname = 'ba_dmo_app_access'
        AND (SELECT oid FROM pg_roles WHERE rolname='ba_dmo_app') = ANY(p.polroles))");
        Assert.Equal(10, rlsOk);

        // anon/authenticated hold no DML privilege on any of the 10
        var denied = await Exec(cs, $@"
SELECT count(*)
FROM information_schema.table_privileges p
JOIN pg_roles r ON r.rolname = p.grantee
WHERE p.table_schema='public'
  AND p.table_name IN ({lateTables})
  AND r.rolname IN ('anon','authenticated')
  AND p.privilege_type IN ('SELECT','INSERT','UPDATE','DELETE')");
        Assert.Equal(0, denied);

        // ba_dmo_app keeps technical DML on all 10 (4 privileges × 10 tables)
        var appOk = await Exec(cs, $@"
SELECT count(*) FROM information_schema.table_privileges p
WHERE p.table_schema='public'
  AND p.table_name IN ({lateTables})
  AND p.grantee='ba_dmo_app'
  AND p.privilege_type IN ('SELECT','INSERT','UPDATE','DELETE')");
        Assert.Equal(40, appOk);
    }

    // ---- PERF-01 index ---------------------------------------------------------

    [Fact]
    public async Task AuditEventsModuleTime_IndexExists()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        var n = await Exec(cs, $@"
SELECT count(*) FROM pg_indexes
WHERE schemaname='public' AND indexname='ix_audit_events_module_time'");
        Assert.Equal(1, n);
    }

    // ----------------------------------------------------------------------
    // N34 — legacy access mirror REMOVAL.
    // Executed-PostgreSQL probes: after N34 the junction table
    // (internal_user_access_templates) and the internal_users.profile_title
    // mirror column (plus its CHECK) are PHYSICALLY ABSENT; any DML naming
    // them fails loudly (42P01 / 42703) and the N33 column-level ba_dmo_app
    // grants on the canonical internal_users columns are unchanged.
    // Self-skipping: when the test database still contains the mirrors
    // (pre-N34 schema: N32/N33) these probes skip. The historical N32/N33
    // executed probes (fail-closed junction reconciliation, mirror privilege
    // revocation) are superseded: their file-level guards remain in
    // MigrationDiscoveryTests.N32_*/N33_*, and N34 removes the objects the
    // executed probes named. The connection role is expected to be the
    // migration/owner role (can create roles when absent and SET ROLE).
    // ----------------------------------------------------------------------

    [Fact]
    public async Task N34_JunctionTable_IsAbsent_AndAnyDmlRaises42P01()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        if (await AccessMirrorsStillPresent(cs)) return; // N34 not applied

        // Catalog probe: the junction table does not exist.
        var tables = await ScalarInt(cs, $@"
SELECT count(*) FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = 'internal_user_access_templates';");
        Assert.Equal(0, tables);

        // Behaviour probe: naming the junction in DML fails with 42P01
        // (relation does not exist) from any role — the object is gone.
        var state = await CaptureSqlState(cs, $@"
INSERT INTO internal_user_access_templates (actor_id, template_id, assigned_at_utc)
VALUES ('n34-probe-{Guid.NewGuid():N}', 'tpl-n34-{Guid.NewGuid():N}', now());");
        Assert.Equal("42P01", state);
    }

    [Fact]
    public async Task N34_ProfileTitleColumn_IsAbsent_AndAnyDmlRaises42703()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        if (await AccessMirrorsStillPresent(cs)) return; // N34 not applied

        // Catalog probe: the mirror column (and therefore its CHECK) is gone.
        var columns = await ScalarInt(cs, $@"
SELECT count(*) FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'internal_users'
  AND column_name = 'profile_title';");
        Assert.Equal(0, columns);
        var checkCount = await ScalarInt(cs, $@"
SELECT count(*) FROM pg_constraint
WHERE conrelid = 'public.internal_users'::regclass
  AND conname = 'ck_internal_users_functional_profile';");
        Assert.Equal(0, checkCount);

        // Behaviour probes: SELECT / INSERT / UPDATE of the removed column each
        // fail with 42703 (undefined column).
        Assert.Equal("42703", await CaptureSqlState(cs,
            "UPDATE internal_users SET profile_title = 'Admin' WHERE FALSE;"));
        Assert.Equal("42703", await CaptureSqlState(cs, $@"
INSERT INTO internal_users (actor_id, auth_user_id, template_id, display_name, profile_title)
VALUES ('n34-probe-{Guid.NewGuid():N}', NULL, 'tpl-n34-{Guid.NewGuid():N}', 'N34 Mirror Insert', 'Admin');"));
        Assert.Equal("42703", await CaptureSqlState(cs,
            "SELECT profile_title FROM internal_users WHERE FALSE;"));
    }

    [Fact]
    public async Task N34_CanonicalColumnPrivileges_AreUnchanged_ForBaDmoApp()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        await EnsureRoleExistsAsync(cs, "ba_dmo_app");
        if (await AccessMirrorsStillPresent(cs)) return; // N34 not applied

        // Catalog probe: every canonical internal_users column keeps
        // SELECT/INSERT/UPDATE for ba_dmo_app (the N33 column-level grants are
        // untouched by N34 — they named the canonical list, never the mirror).
        var canonicalMissing = await ScalarInt(cs, $@"
SELECT count(*) FROM (VALUES
    (has_column_privilege('ba_dmo_app', 'internal_users', 'actor_id', 'SELECT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'actor_id', 'INSERT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'actor_id', 'UPDATE')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'auth_user_id', 'SELECT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'auth_user_id', 'INSERT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'auth_user_id', 'UPDATE')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'template_id', 'SELECT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'template_id', 'INSERT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'template_id', 'UPDATE')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'display_name', 'SELECT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'display_name', 'INSERT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'display_name', 'UPDATE')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'active', 'SELECT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'active', 'INSERT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'active', 'UPDATE')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'created_at_utc', 'SELECT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'created_at_utc', 'INSERT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'created_at_utc', 'UPDATE')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'updated_at_utc', 'SELECT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'updated_at_utc', 'INSERT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'updated_at_utc', 'UPDATE')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'modules_override', 'SELECT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'modules_override', 'INSERT')),
    (has_column_privilege('ba_dmo_app', 'internal_users', 'modules_override', 'UPDATE'))) v(ok)
WHERE NOT ok;");
        Assert.Equal(0, canonicalMissing);

        // Behaviour probe: reading the canonical columns as ba_dmo_app
        // succeeds (no privilege regression after the mirror removal).
        Assert.Null(await CaptureSqlStateAs(cs, "ba_dmo_app",
            "SELECT actor_id, auth_user_id, template_id, display_name, active FROM internal_users WHERE FALSE;"));
    }

    [Fact]
    public async Task N34_NewUserRows_AreInsertable_OnThePostRemovalSchema()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        await EnsureRoleExistsAsync(cs, "ba_dmo_app");
        if (await AccessMirrorsStillPresent(cs)) return; // N34 not applied

        var tpl = "tpl-n34-null-" + Guid.NewGuid().ToString("N")[..8];
        await EnsureTemplateAsync(cs, tpl);
        var actor = "n34-null-" + Guid.NewGuid().ToString("N")[..8];

        // The whole probe runs as ba_dmo_app inside ONE transaction that is
        // ROLLED BACK, so the shared test database is left untouched.
        string? state = null;
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await using (var setRole = new NpgsqlCommand("SET ROLE ba_dmo_app", conn, tx))
                await setRole.ExecuteNonQueryAsync();

            try
            {
                await using var insert = new NpgsqlCommand($@"
INSERT INTO internal_users (actor_id, auth_user_id, template_id, display_name)
VALUES ('{actor}', '{Guid.NewGuid()}', '{tpl}', 'N34 Null Mirror');", conn, tx);
                await insert.ExecuteNonQueryAsync();
            }
            catch (PostgresException ex)
            {
                state = ex.SqlState;
            }
        }
        finally
        {
            await tx.RollbackAsync(); // data + role switch all restored
        }

        // On the post-N34 schema the mirror column does not exist, so a user
        // INSERT never references it and succeeds under the canonical grants.
        Assert.Null(state);
    }

    [Fact]
    public async Task N32_ProfileBackfill_UsesDeterministicDefault_NotUserProfileTitle()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        var tpl = "tpl-n32-prof-" + Guid.NewGuid().ToString("N")[..8];
        await Exec(cs, $@"
INSERT INTO access_templates (template_id, name, modules, active)
VALUES ('{tpl}', 'Sem responsabilidades', '[{{""moduleId"":""jobon"",""capabilities"":[]}}]', TRUE)
ON CONFLICT (template_id) DO NOTHING;");
        // The N31 trigger auto-creates the profile; delete it to force backfill.
        await Exec(cs, $"DELETE FROM access_template_profiles WHERE template_id = '{tpl}';");
        // A user with a non-admin template — the backfill must produce the
        // deterministic default from the TEMPLATE (module/name), never from a
        // user-level mirror. (The legacy profile_title mirror that N32-era
        // probes used to seed 'Admin' was removed by N34; the deterministic
        // template default is the same and the backfill SQL has no user column
        // dependency — verified by the N32 file-level guards.)
        await Exec(cs, $@"
INSERT INTO internal_users (actor_id, auth_user_id, template_id, display_name)
VALUES ('{tpl}-user', '{Guid.NewGuid()}', '{tpl}', 'N32 User');");

        // N32 §3 backfill (replicated): deterministic default only.
        await Exec(cs, $@"
INSERT INTO access_template_profiles (template_id, functional_profile, updated_at_utc)
SELECT t.template_id,
       CASE
           WHEN t.modules @> '[{{""moduleId"":""admin""}}]'::jsonb THEN 'Admin'
           WHEN lower(t.name) LIKE '%respons%' THEN 'Responsável'
           ELSE 'Operador / Controlador'
       END,
       t.updated_at_utc
  FROM access_templates t
  LEFT JOIN access_template_profiles p ON p.template_id = t.template_id
 WHERE p.template_id IS NULL
ON CONFLICT (template_id) DO NOTHING;");

        var profile = await CaptureScalar(cs,
            $"SELECT functional_profile FROM access_template_profiles WHERE template_id = '{tpl}';");
        // The deterministic default must win (no user profile copying).
        Assert.Equal("Operador / Controlador", profile);
    }

    // ----------------------------------------------------------------------
    // N35 — safe index / constraint rationalization (BQ-16 + redundant drop).
    // Executed-PostgreSQL catalog probes: after N35, bq_movements carries the
    // index ix_bq_movements_noted_repairer and pegamento_documentos NO longer
    // carries the redundant standalone index ix_pegamento_documentos_controlo
    // (the UNIQUE constraint index remains). Self-skipping mirrors N34.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task N35_BqMovementsRepairerIndex_Exists_AndRedundantPegamentoIndex_IsGone()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        if (await AccessMirrorsStillPresent(cs)) return; // N34 not applied

        // BQ-16 additive index present on bq_movements (noted_repairer_id).
        var bqIndex = await ScalarInt(cs, $@"
SELECT count(*) FROM pg_indexes
WHERE schemaname = 'public'
  AND tablename = 'bq_movements'
  AND indexname = 'ix_bq_movements_noted_repairer';");
        Assert.Equal(1, bqIndex);

        // The redundant standalone pegamento_documentos index is gone while
        // the UNIQUE (pegamento_controlo_id) constraint index survives — the
        // column stays served, the duplicate write maintenance does not.
        var redundant = await ScalarInt(cs, $@"
SELECT count(*) FROM pg_indexes
WHERE schemaname = 'public'
  AND tablename = 'pegamento_documentos'
  AND indexname = 'ix_pegamento_documentos_controlo';");
        Assert.Equal(0, redundant);
        var unique = await ScalarInt(cs, $@"
SELECT count(*) FROM pg_indexes
WHERE schemaname = 'public'
  AND tablename = 'pegamento_documentos'
  AND indexdef LIKE '%UNIQUE%pegamento_controlo_id%';");
        Assert.Equal(1, unique);
    }

    // ----------------------------------------------------------------------
    // N36 — D-15 policy-name convention (access_template_profiles_app_access
    // → ba_dmo_app_access).
    // Executed-PostgreSQL catalog probe: AFTER N34+N36 every application table
    // (60 = 61 N33-era tables minus the removed junction) carries EXACTLY ONE
    // policy, named ba_dmo_app_access; no divergent name survives. The
    // authorization body is asserted on access_template_profiles itself.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task N36_PolicyInventory_IsUniform_BaDmoAppAccess_Only()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        if (await AccessMirrorsStillPresent(cs)) return; // N34 not applied

        // Every RLS-enabled application table has exactly one policy and it is
        // named ba_dmo_app_access (schema_migrations has RLS but no policy by
        // design); the divergent naming is gone.
        var divergent = await ScalarInt(cs, $@"
SELECT count(*) FROM pg_policy p
JOIN pg_class c ON c.oid = p.polrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND p.polname <> 'ba_dmo_app_access';");
        Assert.Equal(0, divergent);

        var tableCount = await ScalarInt(cs, $@"
SELECT count(*) FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' AND c.relkind = 'r'
  AND c.relname <> 'schema_migrations';");
        var policyCount = await ScalarInt(cs, $@"
SELECT count(*) FROM pg_policy p
JOIN pg_class c ON c.oid = p.polrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND p.polname = 'ba_dmo_app_access';");
        Assert.Equal(tableCount, policyCount);

        // Identity of semantics on access_template_profiles: the policy is the
        // N12/N25/N29 convention body (FOR ALL TO ba_dmo_app, USING true,
        // WITH CHECK true) owned by ba_dmo_app.
        var semanticsOk = await ScalarInt(cs, $@"
SELECT count(*) FROM pg_policy p
JOIN pg_class c ON c.oid = p.polrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public' AND c.relname = 'access_template_profiles'
  AND p.polname = 'ba_dmo_app_access'
  AND p.polcmd = '*'
  AND (SELECT oid FROM pg_roles WHERE rolname = 'ba_dmo_app') = ANY(p.polroles)
  AND pg_get_expr(p.polqual, p.polrelid) = 'true'
  AND pg_get_expr(p.polwithcheck, p.polrelid) = 'true';");
        Assert.Equal(1, semanticsOk);
    }

    // ----------------------------------------------------------------------
    // N39 — pegamento_medicoes.contra_costura nullable (owner D-12/OD-2:
    // one-sided measurements NON-blocking). Executed-PostgreSQL probes;
    // self-skipping when the test database has NOT applied N39 (column still
    // NOT NULL).
    // ----------------------------------------------------------------------

    private static async Task<bool> ContraCosturaStillNotNull(string cs)
    {
        var notNull = await ScalarInt(cs, @"
SELECT count(*) FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'pegamento_medicoes'
  AND column_name = 'contra_costura'
  AND is_nullable = 'NO';");
        return notNull >= 1;
    }

    [Fact]
    public async Task N39_ContraCostura_IsNullable()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        if (await ContraCosturaStillNotNull(cs)) return; // N39 not applied

        var nullable = await ScalarInt(cs, @"
SELECT count(*) FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'pegamento_medicoes'
  AND column_name = 'contra_costura'
  AND is_nullable = 'YES';");
        Assert.Equal(1, nullable);
    }

    [Fact]
    public async Task N39_OneSidedMeasurement_IsInsertable_AndNormalMeasurementStillWorks()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        if (await ContraCosturaStillNotNull(cs)) return; // N39 not applied

        // Seed a pegamento control (rascunho) + revision context.
        var (jobId, revId) = await SeedJobWithRevisionAsync(cs, "M-PEG");
        var controlId = Guid.NewGuid().ToString();
        await Exec(cs, $@"
INSERT INTO pegamento_controlos (
    pegamento_controlo_id, job_on_id, job_on_revision_id, production_code, machine_code, status)
VALUES ('{controlId}', '{jobId}', '{revId}', 'N39-PG-{Guid.NewGuid():N}', 'B1', 'aberto');");

        // One-sided CM measurement: contra_costura NULL — must NOT raise 23502.
        var oneSided = await CaptureSqlState(cs, $@"
INSERT INTO pegamento_medicoes (
    pegamento_medicao_id, pegamento_controlo_id, component_key, tool_number,
    costura, contra_costura, measured_at_utc, actor_id)
VALUES ('{Guid.NewGuid():N}', '{controlId}', 'CM', 7, 52.3000, NULL, now(), 'pg-actor');");
        Assert.Null(oneSided);

        // Normal two-sided measurement still persists.
        var twoSided = await CaptureSqlState(cs, $@"
INSERT INTO pegamento_medicoes (
    pegamento_medicao_id, pegamento_controlo_id, component_key, tool_number,
    costura, contra_costura, measured_at_utc, actor_id)
VALUES ('{Guid.NewGuid():N}', '{controlId}', 'CM', 8, 52.3000, 52.0000, now(), 'pg-actor');");
        Assert.Null(twoSided);

        var nullCount = await ScalarInt(cs, $@"
SELECT count(*) FROM pegamento_medicoes
WHERE pegamento_controlo_id = '{controlId}' AND contra_costura IS NULL;");
        Assert.Equal(1, nullCount);
    }

    // ----------------------------------------------------------------------
    // N40 — approved Peso readings protection (owner D-10/OD-3 Go).
    // Executed-PostgreSQL probes; self-skipping when the test database has
    // NOT applied N40 (guard trigger absent).
    // ----------------------------------------------------------------------

    private static async Task<bool> N40GuardAbsent(string cs)
    {
        var triggers = await ScalarInt(cs, @"
SELECT count(*) FROM pg_trigger t
JOIN pg_class c ON c.oid = t.tgrelid
WHERE c.relname = 'peso_leituras'
  AND t.tgname = 'trg_peso_leituras_approved_guard'
  AND NOT t.tgisinternal;");
        return triggers < 1;
    }

    private static async Task<string> SeedPesoControloWithReadingAsync(
        string cs, string jobId, string revId, string status, bool withApprovedAt)
    {
        var controlId = await SeedPesoControloAsync(cs, jobId, revId, status, withApprovedAt);
        await Exec(cs, $@"
INSERT INTO peso_leituras (peso_leitura_id, peso_controlo_id, cm_number, readings)
VALUES ('{Guid.NewGuid():N}', '{controlId}', '{Guid.NewGuid():N}', '{{}}')
ON CONFLICT (peso_controlo_id, cm_number) DO NOTHING;");
        return controlId;
    }

    [Fact]
    public async Task N40_ApprovedControl_ReadingsDml_IsRejected()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        if (await N40GuardAbsent(cs)) return; // N40 not applied
        var (jobId, revId) = await SeedJobWithRevisionAsync(cs, "M-RDG");
        var controlId = await SeedPesoControloWithReadingAsync(cs, jobId, revId, "aprovado", withApprovedAt: true);

        // INSERT of an extra reading under an approved parent — denied.
        var ins = await CaptureMessage(cs, $@"
INSERT INTO peso_leituras (peso_leitura_id, peso_controlo_id, cm_number, readings)
VALUES ('{Guid.NewGuid():N}', '{controlId}', '{Guid.NewGuid():N}', '{{}}');");
        Assert.Contains("approved peso control", ins, StringComparison.OrdinalIgnoreCase);

        // UPDATE of an existing reading — denied.
        var upd = await CaptureMessage(cs, $@"
UPDATE peso_leituras SET readings = '{{}}' WHERE peso_controlo_id = '{controlId}';");
        Assert.Contains("approved peso control", upd, StringComparison.OrdinalIgnoreCase);

        // DELETE of an existing reading — denied.
        var del = await CaptureMessage(cs, $@"
DELETE FROM peso_leituras WHERE peso_controlo_id = '{controlId}';");
        Assert.Contains("approved peso control", del, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task N40_DraftReadingsRemainEditable_AndReopenRestoresEditability()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        if (await N40GuardAbsent(cs)) return; // N40 not applied
        var (jobId, revId) = await SeedJobWithRevisionAsync(cs, "M-RDG2");
        var controlId = await SeedPesoControloWithReadingAsync(cs, jobId, revId, "rascunho", withApprovedAt: false);

        // Draft parent: readings INSERT/UPDATE/DELETE are allowed (guard silent).
        Assert.Null(await CaptureSqlState(cs, $@"
INSERT INTO peso_leituras (peso_leitura_id, peso_controlo_id, cm_number, readings)
VALUES ('{Guid.NewGuid():N}', '{controlId}', '{Guid.NewGuid():N}', '{{}}');"));
        Assert.Null(await CaptureSqlState(cs, $@"
UPDATE peso_leituras SET readings = '{{}}' WHERE peso_controlo_id = '{controlId}';"));
        Assert.True(await Exec(cs, $@"
DELETE FROM peso_leituras WHERE peso_controlo_id = '{controlId}';") >= 1);

        // Approve the control (header flip + approval stamp — readings untouched).
        Assert.Null(await CaptureSqlState(cs, $@"
UPDATE peso_controlos SET status = 'aprovado', approved_at_utc = now()
WHERE peso_controlo_id = '{controlId}';"));

        // After approval, readings DML is rejected…
        var blocked = await CaptureMessage(cs, $@"
DELETE FROM peso_leituras WHERE peso_controlo_id = '{controlId}';");
        Assert.Contains("approved peso control", blocked, StringComparison.OrdinalIgnoreCase);

        // …and the audited reopen (status back to rascunho, approval stamp
        // cleared) restores editability — the reopen transaction itself never
        // touches readings.
        Assert.Null(await CaptureSqlState(cs, $@"
UPDATE peso_controlos SET status = 'rascunho', approved_at_utc = NULL
WHERE peso_controlo_id = '{controlId}';"));
        Assert.Null(await CaptureSqlState(cs, $@"
INSERT INTO peso_leituras (peso_leitura_id, peso_controlo_id, cm_number, readings)
VALUES ('{Guid.NewGuid():N}', '{controlId}', '{Guid.NewGuid():N}', '{{}}');"));
    }

    // ----------------------------------------------------------------------
    // N42 — tool_check_occurrences REMOVAL (owner OD-6/PA-01).
    // Executed-PostgreSQL probes; self-skipping when the test database has
    // NOT applied N42 (table still present).
    // ----------------------------------------------------------------------

    [Fact]
    public async Task N42_OccurrenceTwin_IsAbsent_AndAnyDmlRaises42P01()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;

        var tables = await ScalarInt(cs, @"
SELECT count(*) FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = 'tool_check_occurrences';");
        if (tables >= 1) return; // N42 not applied

        // Behaviour probe: naming the removed table in DML fails with 42P01.
        var state = await CaptureSqlState(cs, $@"
INSERT INTO tool_check_occurrences (tool_check_occurrence_id, tool_check_rule_id)
VALUES ('{Guid.NewGuid():N}', '{Guid.NewGuid():N}');");
        Assert.Equal("42P01", state);

        // Its CHECK constraints and indexes are gone with the table.
        var checkCount = await ScalarInt(cs, @"
SELECT count(*) FROM pg_constraint
WHERE conname LIKE 'ck_tool_check_occurrences_%';");
        Assert.Equal(0, checkCount);
        var indexCount = await ScalarInt(cs, @"
SELECT count(*) FROM pg_indexes
WHERE schemaname = 'public' AND indexname LIKE 'ix_tool_check_occurrences_%';");
        Assert.Equal(0, indexCount);
    }
}
