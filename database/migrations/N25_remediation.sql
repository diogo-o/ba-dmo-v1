-- ============================================================================
-- BA DMO fresh-build migration family (remediation pass, owner decisions D1-D7).
-- N25_remediation.sql — deployment-readiness remediation (single idempotent file).
--
-- Authority: DATABASE DEPLOYMENT READINESS REPORT (verified findings) + owner
--            decisions D1..D7 (reports/database_owner_decisions.md, recorded
--            in the implementation authorization):
--              D1/INT-02  Option A — at most one NON-CANCELED job per
--                         (production_code, machine_code); canceled rows
--                         remain historical records.
--              D2/INT-06  Option C — dual emit (code-side; no DDL here).
--              D4/SEC-01  (ops; no DDL here).
--              D5/INT-10  Option A — append-only on ALL FOUR revision-family
--                         tables (TD-18/R006 immutability anchor).
--              D6         migration layout: this file = the N25 remediation.
--            Cross-track decision: internal_users.auth_user_id NOT NULL +
--            UNIQUE (DDL owned by the database track).
--
-- Content: §1 invariants (INT-01, INT-02-D1, INT-03, INT-07, INT-08, INT-10),
--          §2 security coverage (SEC-02: the 10 post-N12 tables),
--          §3 index (PERF-01).
--
-- Every statement is idempotent and guarded; the file is forward-only and is
-- executed WHOLE by the Npgsql migration runner (one explicit transaction).
-- Re-running on an already-remediated database is a no-op.
-- NOTE: PostgreSQL has no ADD CONSTRAINT IF NOT EXISTS; constraint creation
--       is idempotent through a pg_constraint guard (DO block) per constraint.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- §1.1 INT-01 + cross-track: internal_users.auth_user_id NOT NULL + UNIQUE.
-- Guard: on any EXISTING database, stop and report if invalid NULL rows
--        exist (never backfill an auth identity — owner decision).
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM internal_users WHERE auth_user_id IS NULL) THEN
        RAISE EXCEPTION 'N25: internal_users contains rows with NULL auth_user_id. STOP: do not backfill an auth identity; report the rows (SELECT actor_id FROM internal_users WHERE auth_user_id IS NULL) before re-applying N25.';
    END IF;
END
$$;

ALTER TABLE internal_users ALTER COLUMN auth_user_id SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_internal_users_auth_user') THEN
        ALTER TABLE internal_users
            ADD CONSTRAINT uq_internal_users_auth_user
            UNIQUE (auth_user_id);
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- §1.2 INT-02 (owner D1 Option A): at most one NON-CANCELED Job On per
-- (production_code, machine_code). Canceled rows opt out (historical records);
-- their identity MAY be re-issued. Machine-scoped lookup (code) is unchanged.
-- ----------------------------------------------------------------------------
CREATE UNIQUE INDEX IF NOT EXISTS uq_job_on_identity
    ON job_on (production_code, machine_code)
    WHERE canceled_at_utc IS NULL;

-- ----------------------------------------------------------------------------
-- §1.3 APP-02/INT-07: job_on lifecycle/consistency — a closed job must carry
-- closed_at_utc; a canceled job must carry canceled_at_utc (and vice versa).
-- Rejects the silent reopen-with-timestamp rows the unguarded TransitionTo
-- could otherwise produce.
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_on_lifecycle_consistent') THEN
        ALTER TABLE job_on
            ADD CONSTRAINT ck_job_on_lifecycle_consistent
            CHECK (
                (status = 'fechado')   = (closed_at_utc   IS NOT NULL)
                AND
                (status = 'cancelado') = (canceled_at_utc IS NOT NULL)
            );
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- §1.4 INT-03: one active trace per lote, enforced at the database level
-- (the C# check-then-create race is closed by this partial unique index).
-- ----------------------------------------------------------------------------
CREATE UNIQUE INDEX IF NOT EXISTS uq_bq_traces_active
    ON bq_traces (bq_lote_id)
    WHERE status = 'active';

-- ----------------------------------------------------------------------------
-- §1.5 INT-07: pegamento_controlos.status — status set from the C# codec
-- (DapperPegamentoRepository: Aberto/aberto, Fechado/fechado).
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_pegamento_controlos_status') THEN
        ALTER TABLE pegamento_controlos
            ADD CONSTRAINT ck_pegamento_controlos_status
            CHECK (status IN ('aberto', 'fechado'));
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- §1.6 INT-07: repair_exit_items.status — the three values written by the
-- domain (RepairExitItem: pendente → em_reparacao → devolvido).
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_repair_exit_items_status') THEN
        ALTER TABLE repair_exit_items
            ADD CONSTRAINT ck_repair_exit_items_status
            CHECK (status IN ('pendente', 'em_reparacao', 'devolvido'));
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- §1.7 INT-07: peso_controlos approved-state consistency.
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_peso_controlos_approved_consistent') THEN
        ALTER TABLE peso_controlos
            ADD CONSTRAINT ck_peso_controlos_approved_consistent
            CHECK ((status = 'aprovado') = (approved_at_utc IS NOT NULL));
    END IF;
END
$$;

-- §1.7b INT-08: approved peso controls are immutable (DB-ified
-- GLM-PESO-06.7). Identity columns (uq_peso_controlos_identity members) may
-- not be updated, and the row may not be deleted, once approved.
-- Non-identity columns (snapshots, notes, updated_at_utc) remain updatable.
CREATE OR REPLACE FUNCTION ba_dmo_guard_peso_approved()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.status = 'aprovado' THEN
        IF TG_OP = 'DELETE' THEN
            RAISE EXCEPTION 'BA DMO: approved peso control % cannot be deleted', OLD.peso_controlo_id;
        END IF;
        IF (TG_OP = 'UPDATE'
            AND (OLD.mold_number IS DISTINCT FROM NEW.mold_number
              OR OLD.neckring_number IS DISTINCT FROM NEW.neckring_number
              OR OLD.production_code IS DISTINCT FROM NEW.production_code
              OR OLD.line IS DISTINCT FROM NEW.line
              OR OLD.lote IS DISTINCT FROM NEW.lote
              OR OLD.control_date IS DISTINCT FROM NEW.control_date))
        THEN
            RAISE EXCEPTION 'BA DMO: approved peso control % identity cannot be updated', OLD.peso_controlo_id;
        END IF;
    END IF;
    RETURN NEW;
END
$$;

DROP TRIGGER IF EXISTS trg_peso_controlos_approved_guard ON peso_controlos;
CREATE TRIGGER trg_peso_controlos_approved_guard
    BEFORE UPDATE OR DELETE ON peso_controlos
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_peso_approved();

-- ----------------------------------------------------------------------------
-- §1.8 INT-07: job_on_verification_occurrence completed-state consistency
-- (mirrors ck_tool_check_occurrences_completed from the N04 sibling table).
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_job_on_verification_completed') THEN
        ALTER TABLE job_on_verification_occurrence
            ADD CONSTRAINT ck_job_on_verification_completed
            CHECK ((status IN ('confirmada', 'reposta')) = (completed_at_utc IS NOT NULL));
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- §1.9 INT-10 (owner D5 Option A): append-only on ALL FOUR revision-family
-- tables. TD-18/R006: the pinned revision is an immutable snapshot; the
-- attribution anchor for Peso/Pegamentos/Controlo must not be rewritable.
-- INSERT remains allowed (new revisions and component copies are appends).
-- ----------------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_job_on_revision_append_only ON job_on_revision;
CREATE TRIGGER trg_job_on_revision_append_only
    BEFORE UPDATE OR DELETE ON job_on_revision
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

DROP TRIGGER IF EXISTS trg_job_on_component_append_only ON job_on_component;
CREATE TRIGGER trg_job_on_component_append_only
    BEFORE UPDATE OR DELETE ON job_on_component
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

DROP TRIGGER IF EXISTS trg_job_on_component_field_append_only ON job_on_component_field;
CREATE TRIGGER trg_job_on_component_field_append_only
    BEFORE UPDATE OR DELETE ON job_on_component_field
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

DROP TRIGGER IF EXISTS trg_job_on_component_row_append_only ON job_on_component_row;
CREATE TRIGGER trg_job_on_component_row_append_only
    BEFORE UPDATE OR DELETE ON job_on_component_row
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- ----------------------------------------------------------------------------
-- §2. SEC-02: RLS + technical policy + anon/authenticated denial for the 10
-- tables created after N12 ran. N12's REVOKE covered only the tables that
-- existed at N12 time; Supabase default privileges make new postgres-owned
-- tables reachable via the Data API. Mirrors N12 conventions exactly:
--   * single technical-scope policy ba_dmo_app_access (functional
--     authorization stays in the C# Application layer — GLM-DATA-06.3);
--   * REVOKE guarded so the script also runs on plain PostgreSQL;
--   * explicit ba_dmo_app DML grants (defense in depth, N12 §3 pattern —
--     default privileges depend on the creating role, which the Supabase
--     migration path does not guarantee).
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    t text;
    late_tables text[] := ARRAY[
        'pegamento_documentos',            -- N14
        'tool_usage_records',              -- N19
        'repairer_repair_types',           -- N20
        'tampao_configuration_machines',   -- N21
        'tampao_configuration_notes',      -- N21
        'tampao_configuration_machine_event', -- N21
        'controlo_sheets',                 -- N23
        'controlo_sheet_items',            -- N23
        'controlo_sheet_events',           -- N23
        'jobon_user_current'               -- N24
    ];
BEGIN
    FOREACH t IN ARRAY late_tables LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
        EXECUTE format('DROP POLICY IF EXISTS ba_dmo_app_access ON %I', t);
        EXECUTE format(
            'CREATE POLICY ba_dmo_app_access ON %I FOR ALL TO ba_dmo_app USING (true) WITH CHECK (true)',
            t);
    END LOOP;
END
$$;

DO $$
DECLARE
    r text;
    t text;
    late_tables text[] := ARRAY[
        'pegamento_documentos', 'tool_usage_records', 'repairer_repair_types',
        'tampao_configuration_machines', 'tampao_configuration_notes',
        'tampao_configuration_machine_event', 'controlo_sheets',
        'controlo_sheet_items', 'controlo_sheet_events', 'jobon_user_current'
    ];
BEGIN
    FOREACH r IN ARRAY ARRAY['anon', 'authenticated'] LOOP
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
            FOREACH t IN ARRAY late_tables LOOP
                EXECUTE format('REVOKE ALL ON TABLE %I FROM %I', t, r);
            END LOOP;
        END IF;
    END LOOP;
END
$$;

GRANT SELECT, INSERT, UPDATE, DELETE
    ON pegamento_documentos, tool_usage_records, repairer_repair_types,
       tampao_configuration_machines, tampao_configuration_notes,
       tampao_configuration_machine_event, controlo_sheets,
       controlo_sheet_items, controlo_sheet_events, jobon_user_current
    TO ba_dmo_app;

-- ----------------------------------------------------------------------------
-- §3. PERF-01: dominant História pattern (module + time range + ordering).
-- ----------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_audit_events_module_time
    ON audit_events (module_id, occurred_at_utc);
