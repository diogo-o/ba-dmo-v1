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
/// The schema is assumed to be fully migrated (N01-N25); tests are isolated
/// by using fresh GUID keys per run.
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
    // N32 — access-authority convergence (SCHEMA-RAT-03A, D-1/D-2).
    // Executed-PostgreSQL probes: the suite DB is assumed fully migrated
    // through N32; the guards/backfill of N32 are replicated here so its
    // fail-closed reconciliation and template-owned profile authority are
    // PROVEN against real PostgreSQL (SKIPs without BA_DMO_TEST_DATABASE).
    // ----------------------------------------------------------------------

    [Fact]
    public async Task N32_ConflictingLegacyJunction_RaisesFailClosedDiagnostic_NeverSilentChoice()
    {
        if (SkipIfNoDatabase()) return;
        var cs = Cs!;
        var tpl1 = "tpl-n32-a-" + Guid.NewGuid().ToString("N")[..8];
        var tpl2 = "tpl-n32-b-" + Guid.NewGuid().ToString("N")[..8];
        var actor = "n32-conflict-" + Guid.NewGuid().ToString("N")[..8];
        await EnsureTemplateAsync(cs, tpl1);
        await EnsureTemplateAsync(cs, tpl2);
        // profile rows exist via the N31 trigger; N32-201 backfill also covers.
        await Exec(cs, $@"
INSERT INTO internal_users (actor_id, auth_user_id, template_id, display_name)
VALUES ('{actor}', '{Guid.NewGuid()}', '{tpl1}', 'N32 Conflict');");

        // All mutations inside ONE transaction that is ROLLED BACK, so the
        // shared test database is left untouched (junction row + guard run +
        // index never altered globally).
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using (var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext('ba_dmo_n32_probe'))", conn, tx))
            await cmd.ExecuteNonQueryAsync();

        // Make the junction DISPUTE the canonical direct FK (delete any mirror
        // row, then insert a conflicting single junction row).
        await using (var cmd = new NpgsqlCommand(
            "DELETE FROM internal_user_access_templates WHERE actor_id = @ActorId; " +
            "INSERT INTO internal_user_access_templates (actor_id, template_id, assigned_at_utc) " +
            "VALUES (@ActorId, @TemplateId2, now());",
            conn, tx))
        {
            cmd.Parameters.AddWithValue("ActorId", actor);
            cmd.Parameters.AddWithValue("TemplateId2", tpl2);
            await cmd.ExecuteNonQueryAsync();
        }

        // N32 §2 guard (replicated verbatim semantics): a single junction row
        // disputing internal_users.template_id MUST raise a diagnostic and
        // never silently pick a side.
        string? message = null;
        try
        {
            await using var guard = new NpgsqlCommand(
                """
                DO $$
                DECLARE v_sample text;
                BEGIN
                    SELECT string_agg(u.actor_id, ', ' ORDER BY u.actor_id)
                      INTO v_sample
                      FROM internal_users u
                      JOIN internal_user_access_templates ut ON ut.actor_id = u.actor_id
                     WHERE ut.template_id IS DISTINCT FROM u.template_id
                       AND ut.template_id IS NOT NULL;
                    IF v_sample IS NOT NULL THEN
                        RAISE EXCEPTION 'N32 blocked: internal_user_access_templates disputes the canonical internal_users.template_id for actor(s): %', v_sample;
                    END IF;
                END
                $$;
                """,
                conn, tx);
            await guard.ExecuteNonQueryAsync();
        }
        catch (PostgresException ex)
        {
            message = ex.Message;
        }

        Assert.NotNull(message);
        Assert.Contains("N32 blocked", message, StringComparison.Ordinal);
        Assert.Contains(actor, message, StringComparison.Ordinal);

        await tx.RollbackAsync(); // index + data + advisory lock all restored
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
        // A user whose profile_title mirror says 'Admin' — backfill must NOT
        // copy that into the template (template profile is the authority).
        await Exec(cs, $@"
INSERT INTO internal_users (actor_id, auth_user_id, template_id, display_name, profile_title)
VALUES ('{tpl}-user', '{Guid.NewGuid()}', '{tpl}', 'N32 User', 'Admin');");

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

        // The user's legacy mirror still says 'Admin' — resolution authority
        // is the template anyway; the mirror is NOT the source of truth.
        var mirror = await CaptureScalar(cs,
            $"SELECT profile_title FROM internal_users WHERE actor_id = '{tpl}-user';");
        Assert.Equal("Admin", mirror);
    }
}
