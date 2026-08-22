-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N12_rls.sql — RLS and least-privilege security contract.
-- Authority: 06_DATA §6 (GLM-DATA-06), GLM-ARCH-14 (Supabase boundary),
--            PV-07 (service role never in runtime).
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
--
-- Contract implemented here, exactly as specified by Plan-V3:
--   1. RLS ENABLED on every BA DMO table.
--   2. anon/authenticated get NO direct table access (browser never touches
--      tables; the application accesses server-side via ba_dmo_app).
--   3. ba_dmo_app receives technical CRUD; FUNCTIONAL authorization is
--      ALWAYS enforced in the C# Application layer, never by RLS.
--   4. V1 has NO per-user/per-module RLS policies; capabilities never enter
--      RLS. The single per-table policy below is the technical access for
--      ba_dmo_app and nothing else.
--   5. ba_dmo_app receives no access to the auth schema (nothing is granted
--      here; Supabase Admin API listing stays server-side — U-05 adapters).
--   6. Credentials live only in user secrets/environment (never repository).
--
-- schema_migrations: RLS enabled with NO app policy — it is an operational
-- table of the migrate CLI only. The migration role owns the table and
-- manages it; RLS is not forced against the owner.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. Enable RLS on every BA DMO table (inventory of the fresh-build family).
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    t text;
    rls_tables text[] := ARRAY[
        -- N01 identity/admin/audit
        'internal_users', 'access_templates', 'audit_events',
        -- N02 catalog mirror
        'module_catalog_mirror',
        -- N03 Boquilhas (operational module domain)
        'bq_lotes', 'bq_traces', 'bq_movements', 'bq_discrepancies',
        'bq_lifecycle_history', 'bq_utilisation_readings',
        -- N04 Ferramentas (tool registry; tool types CM/MF/BQ/PU/CS)
        'tool_references', 'tool_lotes', 'physical_pieces',
        'tool_check_rules', 'tool_check_occurrences',
        -- N05 Job On family
        'job_on', 'job_on_revision', 'job_on_component',
        'job_on_component_field', 'job_on_component_row',
        'job_on_verification_occurrence', 'job_on_audit_event',
        'job_on_field_option',
        -- N06 Peso
        'peso_references', 'peso_lotes', 'peso_controlos', 'peso_leituras',
        'peso_comparacao_anterior', 'peso_day_approvals', 'peso_settings',
        -- N07 Pegamentos
        'pegamento_controlos', 'pegamento_medicoes',
        -- N08 Repair
        'repairers', 'line_repairer_defaults', 'repair_exits',
        'repair_exit_items', 'repair_events', 'internal_repair_records',
        -- N09 Armazém
        'warehouse_locations', 'warehouse_stock', 'warehouse_movements',
        -- N10 Tampões
        'tampao_field_defs', 'tampao_field_values', 'tampao_configurations',
        'tampao_saldos', 'tampao_movements', 'tampao_planos',
        -- N11 shared
        'app_settings',
        -- migration tracking (migrate CLI only; no app policy)
        'schema_migrations'
    ]
BEGIN
    FOREACH t IN ARRAY rls_tables LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
    END LOOP;
END
$$;

-- ----------------------------------------------------------------------------
-- 2. anon / authenticated: no direct access. Roles are Supabase-specific;
--    guard the REVOKE so the script also runs on plain PostgreSQL.
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    r text;
BEGIN
    FOREACH r IN ARRAY ARRAY['anon', 'authenticated'] LOOP
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
            EXECUTE format('REVOKE ALL ON ALL TABLES IN SCHEMA public FROM %I', r);
            EXECUTE format('REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM %I', r);
        END IF;
    END LOOP;
END
$$;

-- ----------------------------------------------------------------------------
-- 3. ba_dmo_app: technical CRUD grants (defense in depth alongside
--    ALTER DEFAULT PRIVILEGES from N01).
-- ----------------------------------------------------------------------------
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO ba_dmo_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO ba_dmo_app;

-- ----------------------------------------------------------------------------
-- 4. Single technical policy per application table for ba_dmo_app.
--    No per-user/per-module policies in V1 (GLM-DATA-06.3); capabilities and
--    functional authorization live in the C# Application layer, never in RLS.
--    schema_migrations intentionally gets NO policy (migrate CLI only).
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    t text;
    policy_tables text[] := ARRAY[
        'internal_users', 'access_templates', 'audit_events',
        'module_catalog_mirror',
        'bq_lotes', 'bq_traces', 'bq_movements', 'bq_discrepancies',
        'bq_lifecycle_history', 'bq_utilisation_readings',
        'tool_references', 'tool_lotes', 'physical_pieces',
        'tool_check_rules', 'tool_check_occurrences',
        'job_on', 'job_on_revision', 'job_on_component',
        'job_on_component_field', 'job_on_component_row',
        'job_on_verification_occurrence', 'job_on_audit_event',
        'job_on_field_option',
        'peso_references', 'peso_lotes', 'peso_controlos', 'peso_leituras',
        'peso_comparacao_anterior', 'peso_day_approvals', 'peso_settings',
        'pegamento_controlos', 'pegamento_medicoes',
        'repairers', 'line_repairer_defaults', 'repair_exits',
        'repair_exit_items', 'repair_events', 'internal_repair_records',
        'warehouse_locations', 'warehouse_stock', 'warehouse_movements',
        'tampao_field_defs', 'tampao_field_values', 'tampao_configurations',
        'tampao_saldos', 'tampao_movements', 'tampao_planos',
        'app_settings'
    ]
BEGIN
    FOREACH t IN ARRAY policy_tables LOOP
        EXECUTE format('DROP POLICY IF EXISTS ba_dmo_app_access ON %I', t);
        EXECUTE format(
            'CREATE POLICY ba_dmo_app_access ON %I FOR ALL TO ba_dmo_app USING (true) WITH CHECK (true)',
            t);
    END LOOP;
END
$$;
