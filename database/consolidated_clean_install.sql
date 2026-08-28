-- ============================================================================
-- BA DMO — CONSOLIDATED CLEAN-INSTALL BASELINE
-- ============================================================================
-- Purpose
--   Fresh-install baseline that produces — directly and in one pass — the
--   FINAL effective schema that results from applying the full forward-only
--   migration family N01 … N42 in order.
--
--   It is NOT a migration: it must live OUTSIDE database/migrations/ because
--   the BA DMO migration runner (MigrationDiscovery) requires every *.sql file
--   in that directory to match the fresh-build family pattern 'N##_<name>.sql';
--   placing this file there would break discovery and the
--   ShippedFreshBuildFamily tests. The historical chain N01…N42 stays intact
--   (database/migrations/) as the upgrade path.
--
-- Target environments
--   * Full PostgreSQL (local/dev)   — reproduces the exact N01–N42 catalog,
--     including the runtime role + default-privileges contract.
--   * Supabase Hosted               — the same file runs cleanly with only the
--     privileges that the project role has. All privilege-heavy statements
--     (CREATE ROLE, ALTER DEFAULT PRIVILEGES, GRANT … TO ba_dmo_app) are
--     GUARDED: on Supabase they become no-ops with a NOTICE if the role or
--     entitlement is unavailable, so nothing schema-critical is skipped and
--     nothing privilege-heavy fails the run.
--
-- Parity scope
--   Reproduces the chain end-state N01…N42 (audit CB-01…CB-05 closure,
--   reports/post_codex_database_contract_audit.md §4; N34-N42 implemented in
--   the N34-N42 implementation reports):
--     * N31 objects (access_template_profiles + ensure trigger + unique
--       actor index + profile sync) for Admin template editing (42P01 fix);
--     * N29 RLS/policy/grant stanza on article_reference_images;
--     * post-N33 security posture for the legacy access mirrors (REVOKEs +
--       column-level internal_users grants);
--     * N34 final state: the legacy access mirrors are REMOVED
--       (internal_user_access_templates + internal_users.profile_title + its
--       CHECK) — drift D-A (the inert junction policy absent here) is resolved
--       by construction on both paths;
--     * N35 final state: index `ix_bq_movements_noted_repairer` present and
--       the redundant `ix_pegamento_documentos_controlo` absent;
--     * N36 final state: the single policy-name convention `ba_dmo_app_access`
--       everywhere (access_template_profiles included);
--     * N37 final state: `peso_comparacao_anterior` removed (previous-control
--       snapshot lives in `peso_controlos.previous_control`);
--     * N38 final state: `internal_users.modules_override` and
--       `job_on_revision.image_asset_id` removed; internal_users column-level
--       grants re-issued for the seven surviving canonical columns;
--     * N39 final state: `pegamento_medicoes.contra_costura` nullable
--       (one-sided measurements non-blocking);
--     * N40 final state: `trg_peso_leituras_approved_guard` backstop
--       (approved Peso readings are not silently rewritable);
--     * N41 final state: per-position active-occupation unique index;
--     * N42 final state: `tool_check_occurrences` removed (Job-On-level
--       `job_on_verification_occurrence` is the single occurrence surface).
--   Reconciliation DML of N27/N28/N29/N31/N32 is included where meaningful on
--   a fresh (empty) database; on a populated partial database the chain
--   migrations remain the authority.
-- ============================================================================

-- ============================================================================
-- 0. Runtime roles + append-only guard function (from N01).
--    Role creation is guarded so it also works where the active role cannot
--    create roles (Supabase hosted); the function is unconditional.
-- ============================================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_app') THEN
        BEGIN
            CREATE ROLE ba_dmo_app NOLOGIN;
        EXCEPTION WHEN insufficient_privilege THEN
            RAISE NOTICE 'ba_dmo_app role creation skipped (insufficient privilege).';
        END;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_migrate') THEN
        BEGIN
            CREATE ROLE ba_dmo_migrate NOLOGIN;
        EXCEPTION WHEN insufficient_privilege THEN
            RAISE NOTICE 'ba_dmo_migrate role creation skipped (insufficient privilege).';
        END;
    END IF;
END
$$;

-- @dmo_schema_privileges_guarded
-- ALTER DEFAULT PRIVILEGES FOR ROLE ba_dmo_migrate IN SCHEMA public
--     GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ba_dmo_app;
-- ALTER DEFAULT PRIVILEGES FOR ROLE ba_dmo_migrate IN SCHEMA public
--     GRANT USAGE, SELECT ON SEQUENCES TO ba_dmo_app;
-- (Comment-kept for provenance. ALTER DEFAULT PRIVILEGES is privilege-heavy and
--  is intentionally NOT emitted unguarded in the Supabase-hosted baseline.)

CREATE OR REPLACE FUNCTION ba_dmo_guard_append_only()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'BA DMO: append-only table % cannot be updated or deleted', TG_TABLE_NAME;
END
$$;

-- ============================================================================
-- Tracking table (created by the migration runner before the first migration;
-- reproduced here first so RLS can enable on it in the security section and
-- the file is self-contained).
-- ============================================================================
CREATE TABLE IF NOT EXISTS schema_migrations (
    version           text        PRIMARY KEY,
    filename          text        NOT NULL,
    sha256            text        NOT NULL,
    applied_at        timestamptz NOT NULL DEFAULT now(),
    execution_time_ms integer     NULL
);

-- ============================================================================
-- 1. Identity / Admin / Audit (N01)
-- ============================================================================
CREATE TABLE IF NOT EXISTS access_templates (
    template_id     text        PRIMARY KEY,
    name            text        NOT NULL,
    modules         jsonb       NOT NULL DEFAULT '[]'::jsonb,
    active          boolean     NOT NULL DEFAULT TRUE,
    created_at_utc  timestamptz NOT NULL DEFAULT now(),
    created_by      text,
    updated_at_utc  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_access_templates_active
    ON access_templates (active);

CREATE TABLE IF NOT EXISTS internal_users (
    actor_id        text        PRIMARY KEY,
    auth_user_id    uuid        NULL,
    template_id     text        NOT NULL REFERENCES access_templates (template_id),
    display_name    text        NOT NULL,
    active          boolean     NOT NULL DEFAULT TRUE,
    created_at_utc  timestamptz NOT NULL DEFAULT now(),
    updated_at_utc  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_internal_users_auth_user_id
    ON internal_users (auth_user_id);
CREATE INDEX IF NOT EXISTS ix_internal_users_active
    ON internal_users (active);
CREATE INDEX IF NOT EXISTS ix_internal_users_template_id
    ON internal_users (template_id);

CREATE TABLE IF NOT EXISTS audit_events (
    audit_event_id        uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    occurred_at_utc       timestamptz NOT NULL DEFAULT now(),
    year                  integer     NOT NULL,
    actor_user_id         text        NULL,
    actor_name_snapshot   text        NULL,
    module_id             text        NOT NULL,
    action_code           text        NOT NULL,
    entity_type           text        NOT NULL,
    entity_id             text        NOT NULL,
    entity_label_snapshot text        NULL,
    result                text        NOT NULL,
    reason                text        NULL,
    correlation_id        uuid        NULL,
    job_on_id             uuid        NULL,
    revision_id           uuid        NULL,
    before_summary        jsonb       NULL,
    after_summary         jsonb       NULL,
    CONSTRAINT ck_audit_events_year_positive CHECK (year > 0),
    CONSTRAINT ck_audit_events_result CHECK (
        result IN ('succeeded', 'failed', 'denied', 'corrected'))
);

CREATE INDEX IF NOT EXISTS ix_audit_events_year
    ON audit_events (year);
CREATE INDEX IF NOT EXISTS ix_audit_events_module_action
    ON audit_events (module_id, action_code);
CREATE INDEX IF NOT EXISTS ix_audit_events_actor
    ON audit_events (actor_user_id, year);
CREATE INDEX IF NOT EXISTS ix_audit_events_entity
    ON audit_events (entity_type, entity_id);
CREATE INDEX IF NOT EXISTS ix_audit_events_occurred_at
    ON audit_events (occurred_at_utc);
CREATE INDEX IF NOT EXISTS ix_audit_events_job_on_id
    ON audit_events (job_on_id);

DROP TRIGGER IF EXISTS trg_audit_events_append_only ON audit_events;
CREATE TRIGGER trg_audit_events_append_only
    BEFORE UPDATE OR DELETE ON audit_events
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- ============================================================================
-- 2. Module catalog mirror (N02)
-- ============================================================================
CREATE TABLE IF NOT EXISTS module_catalog_mirror (
    module_id       text        PRIMARY KEY,
    display_name    text        NOT NULL,
    display_order   integer     NOT NULL,
    active          boolean     NOT NULL DEFAULT TRUE,
    synced_at_utc   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_module_catalog_mirror_order
    ON module_catalog_mirror (display_order);

-- ============================================================================
-- 3. Boquilhas (N03) — includes additive N18 column
-- ============================================================================
CREATE TABLE IF NOT EXISTS bq_lotes (
    bq_lote_id      uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    reference       text        NOT NULL,
    batch_code      text        NOT NULL,
    allowed_lines   text[]      NOT NULL DEFAULT '{}',
    lifecycle_state text        NOT NULL DEFAULT 'available',
    created_at_utc  timestamptz NOT NULL DEFAULT now(),
    created_by      text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_bq_lotes_reference_batch UNIQUE (reference, batch_code),
    CONSTRAINT ck_bq_lotes_reference CHECK (reference ~ '^[A-Z][0-9]{3}$'),
    CONSTRAINT ck_bq_lotes_lifecycle CHECK (
        lifecycle_state IN ('available', 'archived', 'scrapped'))
);

CREATE INDEX IF NOT EXISTS ix_bq_lotes_lifecycle
    ON bq_lotes (lifecycle_state);

CREATE TABLE IF NOT EXISTS bq_traces (
    bq_trace_id      uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    bq_lote_id       uuid        NOT NULL REFERENCES bq_lotes (bq_lote_id),
    status           text        NOT NULL,
    purpose          text        NOT NULL,
    start_line       text        NOT NULL,
    sap_start        numeric(5,2) NULL,
    sap_end          numeric(5,2) NULL,
    reopen_history   jsonb       NOT NULL DEFAULT '[]'::jsonb,
    deleted_movements jsonb      NOT NULL DEFAULT '[]'::jsonb,
    created_at_utc   timestamptz NOT NULL DEFAULT now(),
    created_by       text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_bq_traces_status CHECK (status IN ('active', 'closed')),
    CONSTRAINT ck_bq_traces_purpose CHECK (purpose IN ('production', 'repair')),
    CONSTRAINT ck_bq_traces_sap_start CHECK (sap_start IS NULL OR (sap_start >= 0 AND sap_start <= 100)),
    CONSTRAINT ck_bq_traces_sap_end CHECK (sap_end IS NULL OR (sap_end >= 0 AND sap_end <= 100))
);

CREATE INDEX IF NOT EXISTS ix_bq_traces_lote ON bq_traces (bq_lote_id);
CREATE INDEX IF NOT EXISTS ix_bq_traces_status ON bq_traces (status);

-- bq_movements: N03 base + N18 additive noted_repairer_id
CREATE TABLE IF NOT EXISTS bq_movements (
    bq_movement_id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    bq_trace_id             uuid        NOT NULL REFERENCES bq_traces (bq_trace_id),
    movement_type           text        NOT NULL,
    qty                     numeric(12,2) NULL,
    exceptional_received_qty numeric(12,2) NULL,
    line                    text        NULL,
    notes                   text        NULL,
    occurred_at_utc         timestamptz NOT NULL DEFAULT now(),
    actor_id                text        NULL REFERENCES internal_users (actor_id),
    noted_repairer_id       uuid        NULL,                                       -- N18 (FK added below after repairers)
    CONSTRAINT ck_bq_movements_type CHECK (
        movement_type IN ('inicio', 'saida', 'entrada', 'irreparavel', 'linha', 'contagem', 'fim')),
    CONSTRAINT ck_bq_movements_qty CHECK (qty IS NOT NULL OR movement_type = 'linha'),
    CONSTRAINT ck_bq_movements_exceptional CHECK (exceptional_received_qty IS NULL OR exceptional_received_qty >= 0)
);

CREATE INDEX IF NOT EXISTS ix_bq_movements_trace ON bq_movements (bq_trace_id);
CREATE INDEX IF NOT EXISTS ix_bq_movements_occurred ON bq_movements (occurred_at_utc);
-- N35 §1 (BQ-16): repairer-filtered Boquilhas History (ListMovementsAsync /
-- CountMovementsAsync "noted_repairer_id = @RepairerId" predicate). Not a
-- prefix of any existing composite; append-only table -> negligible write cost.
CREATE INDEX IF NOT EXISTS ix_bq_movements_noted_repairer
    ON bq_movements (noted_repairer_id);

DROP TRIGGER IF EXISTS trg_bq_movements_append_only ON bq_movements;
CREATE TRIGGER trg_bq_movements_append_only
    BEFORE UPDATE OR DELETE ON bq_movements
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

CREATE TABLE IF NOT EXISTS bq_discrepancies (
    bq_discrepancy_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    bq_lote_id        uuid        NOT NULL REFERENCES bq_lotes (bq_lote_id),
    bq_trace_id       uuid        NULL REFERENCES bq_traces (bq_trace_id),
    expected_qty      numeric(12,2) NOT NULL,
    actual_qty        numeric(12,2) NOT NULL,
    excess_qty        numeric(12,2) NOT NULL,
    status            text        NOT NULL DEFAULT 'open',
    resolution_note   text        NULL,
    resolved_by       text        NULL REFERENCES internal_users (actor_id),
    resolved_at_utc   timestamptz NULL,
    created_at_utc    timestamptz NOT NULL DEFAULT now(),
    created_by        text        NULL REFERENCES internal_users (actor_id),
    CONSTRAINT ck_bq_discrepancies_status CHECK (
        status IN ('open', 'under_review', 'resolved'))
);

CREATE INDEX IF NOT EXISTS ix_bq_discrepancies_lote ON bq_discrepancies (bq_lote_id);
CREATE INDEX IF NOT EXISTS ix_bq_discrepancies_status ON bq_discrepancies (status);

CREATE TABLE IF NOT EXISTS bq_lifecycle_history (
    bq_lifecycle_history_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    bq_lote_id              uuid        NOT NULL REFERENCES bq_lotes (bq_lote_id),
    event                   text        NOT NULL,
    reason                  text        NULL,
    actor_id                text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc         timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_bq_lifecycle_history_event CHECK (
        event IN ('archived', 'scrapped', 'restored', 'retired'))
);

CREATE INDEX IF NOT EXISTS ix_bq_lifecycle_history_lote ON bq_lifecycle_history (bq_lote_id);

DROP TRIGGER IF EXISTS trg_bq_lifecycle_history_append_only ON bq_lifecycle_history;
CREATE TRIGGER trg_bq_lifecycle_history_append_only
    BEFORE UPDATE OR DELETE ON bq_lifecycle_history
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

CREATE TABLE IF NOT EXISTS bq_utilisation_readings (
    bq_utilisation_reading_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    bq_trace_id               uuid        NOT NULL REFERENCES bq_traces (bq_trace_id),
    reading_kind              text        NOT NULL,
    value                     numeric(5,2) NOT NULL,
    actor_id                  text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc           timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_bq_utilisation_readings_kind CHECK (reading_kind IN ('initial', 'final')),
    CONSTRAINT ck_bq_utilisation_readings_value CHECK (value >= 0 AND value <= 100)
);

CREATE INDEX IF NOT EXISTS ix_bq_utilisation_readings_trace ON bq_utilisation_readings (bq_trace_id);

DROP TRIGGER IF EXISTS trg_bq_utilisation_readings_append_only ON bq_utilisation_readings;
CREATE TRIGGER trg_bq_utilisation_readings_append_only
    BEFORE UPDATE OR DELETE ON bq_utilisation_readings
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- ============================================================================
-- 4. Ferramentas (N04) — includes N19 utilisation (separate table below)
-- ============================================================================
CREATE TABLE IF NOT EXISTS tool_references (
    tool_reference_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tool_type         text        NOT NULL,
    ref_code          text        NOT NULL,
    technical_name    text        NULL,
    owner_plant       text        NULL,
    created_at_utc    timestamptz NOT NULL DEFAULT now(),
    created_by        text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_tool_references_type_code UNIQUE (tool_type, ref_code),
    CONSTRAINT ck_tool_references_type CHECK (
        tool_type IN ('CM', 'MF', 'BQ', 'PU', 'CS'))
);

CREATE TABLE IF NOT EXISTS tool_lotes (
    tool_lote_id      uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tool_reference_id uuid        NOT NULL REFERENCES tool_references (tool_reference_id),
    lote              text        NOT NULL,
    qty               integer     NULL,
    allowed_lines     text[]      NOT NULL DEFAULT '{}',
    drawing_code      text        NULL,
    drawing_revision  text        NULL,
    processo          text        NULL,
    created_at_utc    timestamptz NOT NULL DEFAULT now(),
    created_by        text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_tool_lotes_reference_lote UNIQUE (tool_reference_id, lote),
    CONSTRAINT ck_tool_lotes_qty CHECK (qty IS NULL OR qty >= 0)
);

CREATE INDEX IF NOT EXISTS ix_tool_lotes_reference ON tool_lotes (tool_reference_id);

CREATE TABLE IF NOT EXISTS physical_pieces (
    physical_piece_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tool_lote_id      uuid        NOT NULL REFERENCES tool_lotes (tool_lote_id),
    sequence          integer     NOT NULL,
    number            text        NOT NULL,
    status            text        NOT NULL DEFAULT 'operational',
    created_at_utc    timestamptz NOT NULL DEFAULT now(),
    created_by        text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_physical_pieces_lote_number UNIQUE (tool_lote_id, number),
    CONSTRAINT ck_physical_pieces_sequence CHECK (sequence >= 1)
);

CREATE INDEX IF NOT EXISTS ix_physical_pieces_lote ON physical_pieces (tool_lote_id);

CREATE TABLE IF NOT EXISTS tool_check_rules (
    tool_check_rule_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tool_lote_id       uuid        NOT NULL REFERENCES tool_lotes (tool_lote_id),
    rule_text          text        NOT NULL,
    frequency          text        NOT NULL,
    active             boolean     NOT NULL DEFAULT TRUE,
    copied_from_rule_id uuid       NULL REFERENCES tool_check_rules (tool_check_rule_id),
    created_at_utc     timestamptz NOT NULL DEFAULT now(),
    created_by         text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc     timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tool_check_rules_frequency CHECK (
        frequency IN ('uma_vez_no_lote', 'por_fabrico'))
);

CREATE INDEX IF NOT EXISTS ix_tool_check_rules_lote ON tool_check_rules (tool_lote_id);

-- ============================================================================
-- 5. Job On (N05) — mandatory revision FK is circular; added after both tables.
--    job_on includes N13 additive production_folder.
-- ============================================================================
CREATE TABLE IF NOT EXISTS job_on (
    job_on_id                   uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    production_code             text        NOT NULL,
    article_reference_id        uuid        NULL,
    article_reference_snapshot  jsonb       NULL,
    machine_code                text        NOT NULL,
    planned_start_at            timestamptz NULL,
    planned_end_at              timestamptz NULL,
    status                      text        NOT NULL DEFAULT 'rascunho',
    current_revision_id         uuid        NULL,
    copied_from_job_on_id       uuid        NULL REFERENCES job_on (job_on_id),
    closed_at_utc               timestamptz NULL,
    canceled_at_utc             timestamptz NULL,
    canceled_by                 text        NULL REFERENCES internal_users (actor_id),
    cancel_reason               text        NULL,
    created_at_utc              timestamptz NOT NULL DEFAULT now(),
    created_by                  text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc              timestamptz NOT NULL DEFAULT now(),
    production_folder           text        NULL,                                  -- N13
    CONSTRAINT ck_job_on_status CHECK (
        status IN ('rascunho', 'planeado', 'em_fabrico', 'fechado', 'cancelado'))
);

CREATE INDEX IF NOT EXISTS ix_job_on_production_code ON job_on (production_code);
CREATE INDEX IF NOT EXISTS ix_job_on_status ON job_on (status);
CREATE INDEX IF NOT EXISTS ix_job_on_machine_planned ON job_on (machine_code, planned_start_at);

CREATE TABLE IF NOT EXISTS job_on_revision (
    job_on_revision_id  uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    job_on_id           uuid        NOT NULL REFERENCES job_on (job_on_id),
    revision_number     integer     NOT NULL,
    production_snapshot jsonb       NULL,
    reference_snapshot  jsonb       NULL,
    machine_snapshot    jsonb       NULL,
    dates_snapshot      jsonb       NULL,
    sections            jsonb       NOT NULL DEFAULT '{}'::jsonb,
    drop_count          numeric(12,2) NULL,
    type_snapshot       jsonb       NULL,
    stop_snapshot       jsonb       NULL,
    weight_snapshot     jsonb       NULL,
    process_snapshot    jsonb       NULL,
    general_notes       text        NULL,
    change_reason       text        NULL,
    saved_by            text        NULL REFERENCES internal_users (actor_id),
    saved_at_utc        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_job_on_revision_number UNIQUE (job_on_id, revision_number),
    CONSTRAINT ck_job_on_revision_number CHECK (revision_number >= 1)
);

CREATE INDEX IF NOT EXISTS ix_job_on_revision_job_on ON job_on_revision (job_on_id);

-- Current master Article/Reference image association (N29). Job On consumes
-- this image by its readable reference; immutable revisions do not own it.
CREATE TABLE IF NOT EXISTS article_reference_images (
    reference_code  text        PRIMARY KEY,
    image_asset_id  text        NOT NULL,
    updated_by      text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_article_reference_images_reference CHECK (
        reference_code <> ''
        AND reference_code = upper(btrim(reference_code))),
    CONSTRAINT ck_article_reference_images_asset CHECK (
        image_asset_id <> ''
        AND image_asset_id = btrim(image_asset_id)
        AND image_asset_id NOT LIKE '%/%'
        AND position(chr(92) in image_asset_id) = 0
        AND image_asset_id NOT LIKE '%..%'
        AND image_asset_id ~* '\.(jpe?g|png|gif|webp|bmp)$')
);

CREATE INDEX IF NOT EXISTS ix_article_reference_images_updated_by
    ON article_reference_images (updated_by);

-- N29 RLS + policy + grant stanza for article_reference_images (parity with
-- database/migrations/N29_jobon_reference_images.sql §N29:139-155; audit
-- CB-02). On a consolidated-built DB the table was previously RLS-less.
ALTER TABLE article_reference_images ENABLE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_app') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE
            ON article_reference_images TO ba_dmo_app;

        DROP POLICY IF EXISTS ba_dmo_app_access ON article_reference_images;
        CREATE POLICY ba_dmo_app_access
            ON article_reference_images
            FOR ALL
            TO ba_dmo_app
            USING (true)
            WITH CHECK (true);
    END IF;
END $$;

-- Circular link job_on.current_revision_id → job_on_revision (as in N05).
ALTER TABLE job_on
    DROP CONSTRAINT IF EXISTS fk_job_on_current_revision;
ALTER TABLE job_on
    ADD CONSTRAINT fk_job_on_current_revision
    FOREIGN KEY (current_revision_id) REFERENCES job_on_revision (job_on_revision_id);

CREATE TABLE IF NOT EXISTS job_on_component (
    job_on_component_id       uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    job_on_revision_id        uuid        NOT NULL REFERENCES job_on_revision (job_on_revision_id),
    family                    text        NOT NULL,
    source_tool_id            uuid        NULL REFERENCES tool_references (tool_reference_id),
    source_lot_id             uuid        NULL REFERENCES tool_lotes (tool_lote_id),
    reference_snapshot        text        NULL,
    lot_snapshot              text        NULL,
    technical_name_snapshot   text        NULL,
    planned_quantity          numeric(12,2) NULL,
    stock_snapshot            numeric(12,2) NULL,
    usage_snapshot            numeric(12,2) NULL,
    notes                     text        NULL,
    display_order             integer     NOT NULL DEFAULT 0,
    CONSTRAINT ck_job_on_component_family CHECK (
        family IN ('MP_CM', 'MF', 'BQ', 'PU', 'CAL', 'AN', 'ARR', 'PI', 'CS', 'TP', 'FO'))
);

CREATE INDEX IF NOT EXISTS ix_job_on_component_revision ON job_on_component (job_on_revision_id);

CREATE TABLE IF NOT EXISTS job_on_component_field (
    job_on_component_field_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    job_on_component_id       uuid        NOT NULL REFERENCES job_on_component (job_on_component_id),
    field_key                 text        NOT NULL,
    value_type                text        NOT NULL,
    value_text                text        NULL,
    value_integer             integer     NULL,
    value_decimal             numeric(18,4) NULL,
    value_boolean             boolean     NULL,
    value_date                date        NULL,
    display_order             integer     NOT NULL DEFAULT 0,
    CONSTRAINT uq_job_on_component_field UNIQUE (job_on_component_id, field_key),
    CONSTRAINT ck_job_on_component_field_type CHECK (
        value_type IN ('text', 'integer', 'decimal', 'boolean', 'date', 'select'))
);

CREATE INDEX IF NOT EXISTS ix_job_on_component_field_component
    ON job_on_component_field (job_on_component_id);

CREATE TABLE IF NOT EXISTS job_on_component_row (
    job_on_component_row_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    job_on_component_id     uuid        NOT NULL REFERENCES job_on_component (job_on_component_id),
    element_label           text        NOT NULL,
    value_decimal           numeric(18,4) NULL,
    value_text              text        NULL,
    unit                    text        NULL,
    machine_quantity        numeric(12,2) NULL,
    display_order           integer     NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_job_on_component_row_component
    ON job_on_component_row (job_on_component_id);

CREATE TABLE IF NOT EXISTS job_on_verification_occurrence (
    job_on_verification_occurrence_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    job_on_component_id               uuid        NOT NULL REFERENCES job_on_component (job_on_component_id),
    source_rule_id                    uuid        NULL REFERENCES tool_check_rules (tool_check_rule_id),
    rule_text_snapshot                text        NULL,
    status                            text        NOT NULL DEFAULT 'pendente',
    completion_source                 text        NOT NULL DEFAULT 'manual_job_on',
    completed_by                      text        NULL REFERENCES internal_users (actor_id),
    completed_at_utc                  timestamptz NULL,
    created_at_utc                    timestamptz NOT NULL DEFAULT now(),
    updated_at_utc                    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_job_on_verification_status CHECK (
        status IN ('pendente', 'confirmada', 'reposta', 'desativada')),
    CONSTRAINT ck_job_on_verification_source CHECK (completion_source = 'manual_job_on')
);

CREATE INDEX IF NOT EXISTS ix_job_on_verification_component
    ON job_on_verification_occurrence (job_on_component_id);

CREATE TABLE IF NOT EXISTS job_on_audit_event (
    job_on_audit_event_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    job_on_id             uuid        NOT NULL REFERENCES job_on (job_on_id),
    job_on_revision_id    uuid        NULL REFERENCES job_on_revision (job_on_revision_id),
    event_type            text        NOT NULL,
    before_snapshot       jsonb       NULL,
    after_snapshot        jsonb       NULL,
    actor_id              text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc       timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_job_on_audit_event_job_on ON job_on_audit_event (job_on_id);

DROP TRIGGER IF EXISTS trg_job_on_audit_event_append_only ON job_on_audit_event;
CREATE TRIGGER trg_job_on_audit_event_append_only
    BEFORE UPDATE OR DELETE ON job_on_audit_event
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

CREATE TABLE IF NOT EXISTS job_on_field_option (
    job_on_field_option_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    family                 text        NOT NULL,
    field_key              text        NOT NULL,
    option_value           text        NOT NULL,
    option_label           text        NULL,
    display_order          integer     NOT NULL DEFAULT 0,
    active                 boolean     NOT NULL DEFAULT TRUE,
    created_at_utc         timestamptz NOT NULL DEFAULT now(),
    updated_at_utc         timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_job_on_field_option UNIQUE (family, field_key, option_value)
);

CREATE INDEX IF NOT EXISTS ix_job_on_field_option_lookup
    ON job_on_field_option (family, field_key, active);

-- ============================================================================
-- 6. Peso (N06)
-- ============================================================================
CREATE TABLE IF NOT EXISTS peso_references (
    peso_reference_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    mold_number       text        NOT NULL,
    neckring_number   text        NOT NULL,
    counter_mold      text        NULL,
    capacity          numeric(18,4) NULL,
    volume_neck       numeric(18,4) NULL,
    volume_pu         numeric(18,4) NULL,
    calote_tp         numeric(18,4) NULL,
    change_log        jsonb       NOT NULL DEFAULT '[]'::jsonb,
    created_at_utc    timestamptz NOT NULL DEFAULT now(),
    created_by        text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_peso_references_mold_neckring UNIQUE (mold_number, neckring_number)
);

CREATE TABLE IF NOT EXISTS peso_lotes (
    peso_lote_id      uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    peso_reference_id uuid        NOT NULL REFERENCES peso_references (peso_reference_id),
    lote              text        NOT NULL,
    processo          text        NOT NULL,
    allowed_lines     text[]      NOT NULL,
    report_subfolder  text        NOT NULL,
    nominal_weight    numeric(18,4) NULL,
    created_at_utc    timestamptz NOT NULL DEFAULT now(),
    created_by        text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_peso_lotes_reference_lote UNIQUE (peso_reference_id, lote),
    CONSTRAINT ck_peso_lotes_processo CHECK (processo IN ('NNPB', 'PS')),
    CONSTRAINT ck_peso_lotes_allowed_lines CHECK (cardinality(allowed_lines) >= 1)
);

CREATE INDEX IF NOT EXISTS ix_peso_lotes_reference ON peso_lotes (peso_reference_id);

CREATE TABLE IF NOT EXISTS peso_controlos (
    peso_controlo_id     uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    peso_reference_id    uuid        NOT NULL REFERENCES peso_references (peso_reference_id),
    peso_lote_id         uuid        NOT NULL REFERENCES peso_lotes (peso_lote_id),
    record_type          text        NOT NULL,
    mold_number          text        NOT NULL,
    neckring_number      text        NOT NULL,
    production_code      text        NOT NULL,
    line                 text        NOT NULL,
    lote                 text        NOT NULL,
    control_date         date        NOT NULL,
    job_on_id            uuid        NOT NULL REFERENCES job_on (job_on_id),
    job_on_revision_id   uuid        NOT NULL REFERENCES job_on_revision (job_on_revision_id),
    cm_snapshot          jsonb       NULL,
    status               text        NOT NULL DEFAULT 'rascunho',
    measurements_snapshot jsonb      NOT NULL DEFAULT '{}'::jsonb,
    approval_log         jsonb       NOT NULL DEFAULT '[]'::jsonb,
    previous_control     jsonb       NULL,
    comparison_decisions jsonb       NULL,
    approved_by          text        NULL REFERENCES internal_users (actor_id),
    approved_at_utc      timestamptz NULL,
    created_at_utc       timestamptz NOT NULL DEFAULT now(),
    created_by           text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_peso_controlos_identity UNIQUE (
        mold_number, neckring_number, production_code, line, lote, control_date),
    CONSTRAINT ck_peso_controlos_record_type CHECK (
        record_type IN ('novo_controlo', 'comparacao')),
    CONSTRAINT ck_peso_controlos_status CHECK (
        status IN ('rascunho', 'pendente', 'aprovado', 'nao_aprovado'))
);

CREATE INDEX IF NOT EXISTS ix_peso_controlos_reference ON peso_controlos (peso_reference_id);
CREATE INDEX IF NOT EXISTS ix_peso_controlos_job_on ON peso_controlos (job_on_id);
CREATE INDEX IF NOT EXISTS ix_peso_controlos_job_on_revision ON peso_controlos (job_on_revision_id);
CREATE INDEX IF NOT EXISTS ix_peso_controlos_status_date ON peso_controlos (status, control_date);

CREATE TABLE IF NOT EXISTS peso_leituras (
    peso_leitura_id  uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    peso_controlo_id uuid        NOT NULL REFERENCES peso_controlos (peso_controlo_id) ON DELETE CASCADE,
    cm_number        text        NOT NULL,
    readings         jsonb       NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc   timestamptz NOT NULL DEFAULT now(),
    created_by       text        NULL REFERENCES internal_users (actor_id),
    CONSTRAINT uq_peso_leituras_controlo_cm UNIQUE (peso_controlo_id, cm_number)
);

CREATE TABLE IF NOT EXISTS peso_day_approvals (
    peso_day_approval_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    mold_number          text        NOT NULL,
    neckring_number      text        NOT NULL,
    line                 text        NOT NULL,
    approval_date        date        NOT NULL,
    approved_by          text        NULL REFERENCES internal_users (actor_id),
    approved_at_utc      timestamptz NOT NULL DEFAULT now(),
    notes                text        NULL,
    CONSTRAINT uq_peso_day_approvals_identity UNIQUE (
        mold_number, neckring_number, line, approval_date)
);

CREATE TABLE IF NOT EXISTS peso_settings (
    setting_key    text        PRIMARY KEY,
    setting_value  jsonb       NOT NULL,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_by     text        NULL REFERENCES internal_users (actor_id)
);

-- ============================================================================
-- 7. Pegamentos (N07) — includes additive N14/N15/N16/N17
-- ============================================================================
CREATE TABLE IF NOT EXISTS pegamento_controlos (
    pegamento_controlo_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    job_on_id             uuid        NOT NULL REFERENCES job_on (job_on_id),
    job_on_revision_id    uuid        NOT NULL REFERENCES job_on_revision (job_on_revision_id),
    reference_snapshot    jsonb       NULL,
    production_code       text        NOT NULL,
    machine_code          text        NOT NULL,
    cm_snapshot           jsonb       NULL,
    bq_snapshot           jsonb       NULL,
    mf_snapshot           jsonb       NULL,
    nominal_average       numeric(18,4) NULL,
    tolerance             numeric(6,3) NOT NULL DEFAULT 0.20,
    status                text        NOT NULL DEFAULT 'aberto',
    created_at_utc        timestamptz NOT NULL DEFAULT now(),
    created_by            text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc        timestamptz NOT NULL DEFAULT now(),
    cm_nominal            numeric(18,4) NULL,                                      -- N16
    bq_nominal            numeric(18,4) NULL,                                      -- N16
    mf_nominal            numeric(18,4) NULL,                                      -- N16
    notas                 text        NULL,                                        -- N17
    CONSTRAINT ck_pegamento_controlos_tolerance CHECK (tolerance >= 0)
);

CREATE INDEX IF NOT EXISTS ix_pegamento_controlos_job_on ON pegamento_controlos (job_on_id);
CREATE INDEX IF NOT EXISTS ix_pegamento_controlos_job_on_revision ON pegamento_controlos (job_on_revision_id);
CREATE INDEX IF NOT EXISTS ix_pegamento_controlos_production ON pegamento_controlos (production_code, machine_code);

CREATE TABLE IF NOT EXISTS pegamento_medicoes (
    pegamento_medicao_id  uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    pegamento_controlo_id uuid        NOT NULL REFERENCES pegamento_controlos (pegamento_controlo_id),
    component_key         text        NOT NULL,
    costura               numeric(18,4) NOT NULL,
    contra_costura        numeric(18,4) NULL,
    measured_at_utc       timestamptz NOT NULL DEFAULT now(),
    actor_id              text        NULL REFERENCES internal_users (actor_id),
    tool_number           integer     NULL                                         -- N15
);

CREATE INDEX IF NOT EXISTS ix_pegamento_medicoes_controlo
    ON pegamento_medicoes (pegamento_controlo_id);

-- N15 additive composite index
CREATE INDEX IF NOT EXISTS ix_pegamento_medicoes_component_tool
    ON pegamento_medicoes (pegamento_controlo_id, component_key, tool_number);

DROP TRIGGER IF EXISTS trg_pegamento_medicoes_append_only ON pegamento_medicoes;
CREATE TRIGGER trg_pegamento_medicoes_append_only
    BEFORE UPDATE OR DELETE ON pegamento_medicoes
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- N14 document metadata
CREATE TABLE IF NOT EXISTS pegamento_documentos (
    pegamento_documento_id      uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    pegamento_controlo_id       uuid        NOT NULL UNIQUE REFERENCES pegamento_controlos (pegamento_controlo_id),
    filename                    text        NOT NULL,
    output_root_snapshot        text        NOT NULL,
    production_folder_snapshot  text        NOT NULL,
    generated_at_utc            timestamptz NOT NULL DEFAULT now(),
    generated_by                text        NULL REFERENCES internal_users (actor_id)
);
-- (N35 §2: the redundant standalone index ix_pegamento_documentos_controlo —
-- N14 created both it and UNIQUE (pegamento_controlo_id); the constraint
-- index covers the column, so the duplicate is NOT reproduced here.)

-- ============================================================================
-- 8. Repair (N08) — internal_repair_records includes N22 additive columns.
--    repair_events→internal_repair_records FK is forward; added after both.
-- ============================================================================
CREATE TABLE IF NOT EXISTS repairers (
    repairer_id    uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    name           text        NOT NULL,
    active         boolean     NOT NULL DEFAULT TRUE,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now()
);

-- N18 FK: bq_movements.noted_repairer_id -> repairers (added here now that
-- repairers exists; auto-generated constraint name matches the N18 inline
-- REFERENCES that Postgres would assign).
ALTER TABLE bq_movements
    ADD CONSTRAINT bq_movements_noted_repairer_id_fkey
    FOREIGN KEY (noted_repairer_id) REFERENCES repairers (repairer_id);

CREATE TABLE IF NOT EXISTS line_repairer_defaults (
    line           text        NOT NULL,
    tool_type      text        NOT NULL,
    repairer_id    uuid        NOT NULL REFERENCES repairers (repairer_id),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_by     text        NULL REFERENCES internal_users (actor_id),
    PRIMARY KEY (line, tool_type),
    CONSTRAINT ck_line_repairer_defaults_type CHECK (tool_type IN ('BQ', 'CM', 'MF'))
);

CREATE TABLE IF NOT EXISTS repair_exits (
    repair_exit_id      uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    repair_type         text        NOT NULL,
    repairer_id         uuid        NULL REFERENCES repairers (repairer_id),
    repairer_snapshot   jsonb       NULL,
    planned_date        date        NULL,
    status              text        NOT NULL DEFAULT 'preparacao',
    created_at_utc      timestamptz NOT NULL DEFAULT now(),
    created_by          text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_repair_exits_type CHECK (repair_type IN ('BQ', 'CM', 'MF')),
    CONSTRAINT ck_repair_exits_status CHECK (
        status IN ('preparacao', 'a_retirar', 'enviado', 'retorno_parcial', 'concluido', 'cancelado'))
);

CREATE INDEX IF NOT EXISTS ix_repair_exits_status ON repair_exits (status);
CREATE INDEX IF NOT EXISTS ix_repair_exits_planned_date ON repair_exits (planned_date);

CREATE TABLE IF NOT EXISTS repair_exit_items (
    repair_exit_item_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    repair_exit_id      uuid        NOT NULL REFERENCES repair_exits (repair_exit_id),
    bq_lote_id          uuid        NULL REFERENCES bq_lotes (bq_lote_id),
    physical_piece_id   uuid        NULL REFERENCES physical_pieces (physical_piece_id),
    qty                 numeric(12,2) NULL,
    individual_number   text        NULL,
    picked              boolean     NOT NULL DEFAULT FALSE,
    out_at_utc          timestamptz NULL,
    out_operator_id     text        NULL REFERENCES internal_users (actor_id),
    in_at_utc           timestamptz NULL,
    in_operator_id      text        NULL REFERENCES internal_users (actor_id),
    status              text        NOT NULL DEFAULT 'pendente',
    CONSTRAINT ck_repair_exit_items_qty CHECK (qty IS NULL OR qty >= 0),
    CONSTRAINT ck_repair_exit_items_kind CHECK (
        (bq_lote_id IS NOT NULL AND physical_piece_id IS NULL AND qty IS NOT NULL)
        OR (bq_lote_id IS NULL AND physical_piece_id IS NOT NULL AND individual_number IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS ix_repair_exit_items_exit ON repair_exit_items (repair_exit_id);

-- internal_repair_records: N08 base + N22 additive context columns + N28
-- CM/MF-only convergence. BQ remains production/reference context only.
CREATE TABLE IF NOT EXISTS internal_repair_records (
    internal_repair_record_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    line                      text        NOT NULL,
    job_on_id                 uuid        NULL,
    tool_type                 text        NOT NULL,
    individual_number         text        NOT NULL,
    operator_id               text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc           timestamptz NOT NULL DEFAULT now(),
    correction_of_id          uuid        NULL REFERENCES internal_repair_records (internal_repair_record_id),
    before_snapshot           jsonb       NULL,
    correction_reason         text        NULL,
    created_at_utc            timestamptz NOT NULL DEFAULT now(),
    created_by                text        NULL REFERENCES internal_users (actor_id),
    job_on_revision_id        uuid        NULL,                                     -- N22
    production_code           text        NULL,                                     -- N22
    reference                 text        NULL,                                     -- N22
    lot_id                    uuid        NULL,                                     -- N22
    CONSTRAINT ck_internal_repair_records_type CHECK (tool_type IN ('CM', 'MF')),
    CONSTRAINT ck_internal_repair_records_correction CHECK (
        (correction_of_id IS NULL) = (before_snapshot IS NULL))
);

CREATE INDEX IF NOT EXISTS ix_internal_repair_records_line ON internal_repair_records (line);
CREATE INDEX IF NOT EXISTS ix_internal_repair_records_job_on ON internal_repair_records (job_on_id);

-- N22 additive index + FK anchor to immutable revision
CREATE INDEX IF NOT EXISTS ix_internal_repair_records_revision
    ON internal_repair_records (job_on_revision_id);

ALTER TABLE internal_repair_records
    ADD CONSTRAINT fk_internal_repair_records_revision
    FOREIGN KEY (job_on_revision_id) REFERENCES job_on_revision (job_on_revision_id);

CREATE TABLE IF NOT EXISTS repair_events (
    repair_event_id        uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    repair_scope           text        NOT NULL,
    repair_exit_item_id    uuid        NULL REFERENCES repair_exit_items (repair_exit_item_id),
    internal_repair_record_id uuid     NULL,
    canceled               boolean     NOT NULL DEFAULT FALSE,
    cancel_reason          text        NULL,
    notes                  text        NULL,
    actor_id               text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_repair_events_scope CHECK (repair_scope IN ('interna', 'externa'))
);

CREATE INDEX IF NOT EXISTS ix_repair_events_exit_item ON repair_events (repair_exit_item_id);
CREATE INDEX IF NOT EXISTS ix_repair_events_internal ON repair_events (internal_repair_record_id);

DROP TRIGGER IF EXISTS trg_repair_events_append_only ON repair_events;
CREATE TRIGGER trg_repair_events_append_only
    BEFORE UPDATE OR DELETE ON repair_events
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- Forward FK: repair_events.internal_repair_record_id → internal_repair_records
ALTER TABLE repair_events
    ADD CONSTRAINT fk_repair_events_internal_record
    FOREIGN KEY (internal_repair_record_id)
    REFERENCES internal_repair_records (internal_repair_record_id);

-- ============================================================================
-- 9. Armazém (N09)
-- ============================================================================
CREATE TABLE IF NOT EXISTS warehouse_locations (
    warehouse_location_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    code                  text        NOT NULL UNIQUE,
    kind                  text        NULL,
    created_at_utc        timestamptz NOT NULL DEFAULT now(),
    created_by            text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc        timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS warehouse_stock (
    warehouse_stock_id  uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    warehouse_location_id uuid      NOT NULL REFERENCES warehouse_locations (warehouse_location_id),
    tool_lote_id        uuid        NOT NULL REFERENCES tool_lotes (tool_lote_id),
    occupied_since_utc  timestamptz NOT NULL DEFAULT now(),
    occupied_by         text        NULL REFERENCES internal_users (actor_id),
    released_at_utc     timestamptz NULL,
    released_by         text        NULL REFERENCES internal_users (actor_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_warehouse_stock_active_occupation
    ON warehouse_stock (warehouse_location_id, tool_lote_id)
    WHERE released_at_utc IS NULL;
CREATE INDEX IF NOT EXISTS ix_warehouse_stock_location ON warehouse_stock (warehouse_location_id);
CREATE INDEX IF NOT EXISTS ix_warehouse_stock_tool_lote ON warehouse_stock (tool_lote_id);

-- N41 (D-14): at most one ACTIVE occupation per position. The C# FOR UPDATE
-- locking stays the concurrency backstop; this index adds the DB invariant.
CREATE UNIQUE INDEX IF NOT EXISTS uq_warehouse_stock_active_position
    ON warehouse_stock (warehouse_location_id)
    WHERE released_at_utc IS NULL;

CREATE TABLE IF NOT EXISTS warehouse_movements (
    warehouse_movement_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    warehouse_stock_id    uuid        NULL REFERENCES warehouse_stock (warehouse_stock_id),
    direction             text        NOT NULL,
    qty                   numeric(12,2) NULL,
    destination           text        NULL,
    repair_exit_id        uuid        NULL REFERENCES repair_exits (repair_exit_id),
    actor_id              text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_warehouse_movements_direction CHECK (direction IN ('in', 'out'))
);

CREATE INDEX IF NOT EXISTS ix_warehouse_movements_stock ON warehouse_movements (warehouse_stock_id);
CREATE INDEX IF NOT EXISTS ix_warehouse_movements_occurred ON warehouse_movements (occurred_at_utc);

DROP TRIGGER IF EXISTS trg_warehouse_movements_append_only ON warehouse_movements;
CREATE TRIGGER trg_warehouse_movements_append_only
    BEFORE UPDATE OR DELETE ON warehouse_movements
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- ============================================================================
-- 10. Tampões (N10) — includes N21 multi-machine + notes (separate tables below)
-- ============================================================================
CREATE TABLE IF NOT EXISTS tampao_field_defs (
    tampao_field_def_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    field_name          text        NOT NULL UNIQUE,
    unit                text        NULL,
    precision_digits    integer     NULL,
    display_order       integer     NOT NULL DEFAULT 0,
    active              boolean     NOT NULL DEFAULT TRUE,
    created_at_utc      timestamptz NOT NULL DEFAULT now(),
    updated_at_utc      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS tampao_field_values (
    tampao_field_value_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tampao_field_def_id   uuid        NOT NULL REFERENCES tampao_field_defs (tampao_field_def_id),
    value_numeric         numeric(18,4) NOT NULL,
    value_label           text        NOT NULL,
    display_order         integer     NOT NULL DEFAULT 0,
    active                boolean     NOT NULL DEFAULT TRUE,
    created_at_utc        timestamptz NOT NULL DEFAULT now(),
    updated_at_utc        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_tampao_field_values UNIQUE (tampao_field_def_id, value_numeric)
);

CREATE INDEX IF NOT EXISTS ix_tampao_field_values_field
    ON tampao_field_values (tampao_field_def_id, active, value_numeric);

CREATE TABLE IF NOT EXISTS tampao_configurations (
    tampao_configuration_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    values_json           jsonb       NOT NULL,
    active                boolean     NOT NULL DEFAULT TRUE,
    created_at_utc        timestamptz NOT NULL DEFAULT now(),
    created_by            text        NULL REFERENCES internal_users (actor_id),
    CONSTRAINT uq_tampao_configurations_values UNIQUE (values_json)
);

CREATE TABLE IF NOT EXISTS tampao_saldos (
    tampao_saldo_id       uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tampao_configuration_id uuid      NOT NULL UNIQUE REFERENCES tampao_configurations (tampao_configuration_id),
    enchidos              integer     NOT NULL DEFAULT 0,
    por_encher            integer     NOT NULL DEFAULT 0,
    updated_at_utc        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tampao_saldos_enchidos CHECK (enchidos >= 0),
    CONSTRAINT ck_tampao_saldos_por_encher CHECK (por_encher >= 0)
);

CREATE TABLE IF NOT EXISTS tampao_movements (
    tampao_movement_id        uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    movement_type             text        NOT NULL,
    origin_configuration_id   uuid        NULL REFERENCES tampao_configurations (tampao_configuration_id),
    destination_configuration_id uuid     NULL REFERENCES tampao_configurations (tampao_configuration_id),
    qty                       integer     NOT NULL,
    balances_before           jsonb       NULL,
    balances_after            jsonb       NULL,
    actor_id                  text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc           timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tampao_movements_type CHECK (
        movement_type IN ('adicionar', 'remover', 'alterar_estado', 'alterar_configuracao')),
    CONSTRAINT ck_tampao_movements_qty CHECK (qty >= 1)
);

CREATE INDEX IF NOT EXISTS ix_tampao_movements_origin ON tampao_movements (origin_configuration_id);
CREATE INDEX IF NOT EXISTS ix_tampao_movements_occurred ON tampao_movements (occurred_at_utc);

DROP TRIGGER IF EXISTS trg_tampao_movements_append_only ON tampao_movements;
CREATE TRIGGER trg_tampao_movements_append_only
    BEFORE UPDATE OR DELETE ON tampao_movements
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

CREATE TABLE IF NOT EXISTS tampao_planos (
    tampao_plano_id       uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tampao_configuration_id uuid      NOT NULL REFERENCES tampao_configurations (tampao_configuration_id),
    planned_qty            integer     NOT NULL,
    planned_for_date      date        NULL,
    job_on_id             uuid        NULL,
    production_code       text        NULL,
    notes                 text        NULL,
    canceled              boolean     NOT NULL DEFAULT FALSE,
    created_at_utc        timestamptz NOT NULL DEFAULT now(),
    created_by            text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tampao_planos_qty CHECK (planned_qty >= 1)
);

CREATE INDEX IF NOT EXISTS ix_tampao_planos_configuration ON tampao_planos (tampao_configuration_id);
CREATE INDEX IF NOT EXISTS ix_tampao_planos_date ON tampao_planos (planned_for_date);

-- ============================================================================
-- 10b. Tampões multi-machine + comments (N21)
-- ============================================================================
CREATE TABLE IF NOT EXISTS tampao_configuration_machines (
    tampao_configuration_id uuid NOT NULL REFERENCES tampao_configurations (tampao_configuration_id),
    machine                text NOT NULL,
    PRIMARY KEY (tampao_configuration_id, machine),
    CONSTRAINT ck_tampao_configuration_machines_machine CHECK
        (machine IN ('B1', 'B2', 'B3', 'C1', 'C2', 'C3'))
);

CREATE INDEX IF NOT EXISTS ix_tampao_configuration_machines_machine
    ON tampao_configuration_machines (machine);

CREATE TABLE IF NOT EXISTS tampao_configuration_notes (
    tampao_configuration_note_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tampao_configuration_id      uuid        NOT NULL REFERENCES tampao_configurations (tampao_configuration_id),
    note                        text        NOT NULL,
    actor_id                    text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc             timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_tampao_configuration_notes_config
    ON tampao_configuration_notes (tampao_configuration_id, occurred_at_utc);

DROP TRIGGER IF EXISTS trg_tampao_configuration_notes_append_only ON tampao_configuration_notes;
CREATE TRIGGER trg_tampao_configuration_notes_append_only
    BEFORE UPDATE OR DELETE ON tampao_configuration_notes
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

CREATE TABLE IF NOT EXISTS tampao_configuration_machine_event (
    tampao_configuration_machine_event_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tampao_configuration_id               uuid        NOT NULL REFERENCES tampao_configurations (tampao_configuration_id),
    machine                               text        NOT NULL,
    action                                text        NOT NULL,
    actor_id                              text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc                       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tampao_configuration_machine_event_action CHECK (action IN ('added', 'removed')),
    CONSTRAINT ck_tampao_configuration_machine_event_machine CHECK
        (machine IN ('B1', 'B2', 'B3', 'C1', 'C2', 'C3'))
);

CREATE INDEX IF NOT EXISTS ix_tampao_configuration_machine_event_config
    ON tampao_configuration_machine_event (tampao_configuration_id, occurred_at_utc);

DROP TRIGGER IF EXISTS trg_tampao_configuration_machine_event_append_only ON tampao_configuration_machine_event;
CREATE TRIGGER trg_tampao_configuration_machine_event_append_only
    BEFORE UPDATE OR DELETE ON tampao_configuration_machine_event
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- ============================================================================
-- 11. Shared settings (N11)
-- ============================================================================
CREATE TABLE IF NOT EXISTS app_settings (
    setting_key    text        PRIMARY KEY,
    setting_value  jsonb       NOT NULL,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_by     text        NULL REFERENCES internal_users (actor_id)
);

-- ============================================================================
-- 19. Ferramentas utilisation (N19)
-- ============================================================================
CREATE TABLE IF NOT EXISTS tool_usage_records (
    tool_usage_record_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tool_lote_id         uuid        NOT NULL REFERENCES tool_lotes (tool_lote_id),
    sap_start            numeric(5,2) NULL,
    sap_end              numeric(5,2) NULL,
    percent_used         numeric(5,2) NULL,
    value_added          numeric(12,2) NULL,
    value_cumulative     numeric(12,2) NOT NULL,
    notes                text        NULL,
    actor_id             text        NULL REFERENCES internal_users (actor_id),
    reading_at_utc       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tool_usage_records_sap_start CHECK (sap_start IS NULL OR (sap_start >= 0 AND sap_start <= 100)),
    CONSTRAINT ck_tool_usage_records_sap_end CHECK (sap_end IS NULL OR (sap_end >= 0 AND sap_end <= 100)),
    CONSTRAINT ck_tool_usage_records_percent CHECK (percent_used IS NULL OR (percent_used >= 0 AND percent_used <= 100)),
    CONSTRAINT ck_tool_usage_records_cumulative CHECK (value_cumulative >= 0)
);

CREATE INDEX IF NOT EXISTS ix_tool_usage_records_lote
    ON tool_usage_records (tool_lote_id);

DROP TRIGGER IF EXISTS trg_tool_usage_records_append_only ON tool_usage_records;
CREATE TRIGGER trg_tool_usage_records_append_only
    BEFORE UPDATE OR DELETE ON tool_usage_records
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- ============================================================================
-- 20. Repairer capability (N20)
-- ============================================================================
CREATE TABLE IF NOT EXISTS repairer_repair_types (
    repairer_id uuid NOT NULL REFERENCES repairers (repairer_id),
    repair_type text NOT NULL,
    PRIMARY KEY (repairer_id, repair_type),
    CONSTRAINT ck_repairer_repair_types_type CHECK (repair_type IN ('CM', 'MF', 'BQ'))
);

-- ============================================================================
-- 23. Folha de Controlo (N23)
-- ============================================================================
CREATE TABLE IF NOT EXISTS controlo_sheets (
    controlo_sheet_id    uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    job_on_id            uuid        NOT NULL REFERENCES job_on (job_on_id),
    job_on_revision_id   uuid        NOT NULL REFERENCES job_on_revision (job_on_revision_id),
    production_code      text        NOT NULL,
    reference            text        NOT NULL,
    machine_code         text        NOT NULL,
    display_id           text        NOT NULL,
    status               text        NOT NULL DEFAULT 'rascunho',
    created_by           text        NULL REFERENCES internal_users (actor_id),
    created_at_utc       timestamptz NOT NULL DEFAULT now(),
    submitted_by         text        NULL REFERENCES internal_users (actor_id),
    submitted_at_utc     timestamptz NULL,
    submitted_note       text        NULL,
    decided_by           text        NULL REFERENCES internal_users (actor_id),
    decided_at_utc       timestamptz NULL,
    decision             text        NULL,
    decision_note        text        NULL,
    updated_at_utc       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_controlo_sheets_status CHECK (
        status IN ('rascunho', 'submetido', 'aprovado', 'rejeitado')),
    CONSTRAINT ck_controlo_sheets_decision CHECK (
        (decided_by IS NULL AND decided_at_utc IS NULL AND decision IS NULL)
        OR (decided_by IS NOT NULL AND decided_at_utc IS NOT NULL AND decision IN ('aprovado', 'rejeitado')))
);

CREATE INDEX IF NOT EXISTS ix_controlo_sheets_job_on ON controlo_sheets (job_on_id);
CREATE INDEX IF NOT EXISTS ix_controlo_sheets_revision ON controlo_sheets (job_on_revision_id);
CREATE INDEX IF NOT EXISTS ix_controlo_sheets_production ON controlo_sheets (production_code, machine_code);
CREATE INDEX IF NOT EXISTS ix_controlo_sheets_status ON controlo_sheets (status);

CREATE TABLE IF NOT EXISTS controlo_sheet_items (
    controlo_sheet_item_id   uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    controlo_sheet_id        uuid        NOT NULL REFERENCES controlo_sheets (controlo_sheet_id) ON DELETE CASCADE,
    family                   text        NOT NULL,
    source_tool_id           uuid        NULL REFERENCES tool_references (tool_reference_id),
    source_lot_id            uuid        NULL REFERENCES tool_lotes (tool_lote_id),
    reference_snapshot       text        NULL,
    lot_snapshot             text        NULL,
    technical_name_snapshot  text        NULL,
    result                   text        NULL,
    observation              text        NULL,
    mcaliper_link            text        NULL,
    CONSTRAINT ck_controlo_sheet_items_result CHECK (result IS NULL OR result IN ('OK', 'NOK'))
);

CREATE INDEX IF NOT EXISTS ix_controlo_sheet_items_sheet ON controlo_sheet_items (controlo_sheet_id);
CREATE INDEX IF NOT EXISTS ix_controlo_sheet_items_family ON controlo_sheet_items (controlo_sheet_id, family);

CREATE TABLE IF NOT EXISTS controlo_sheet_events (
    controlo_sheet_event_id  uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    controlo_sheet_id        uuid        NOT NULL REFERENCES controlo_sheets (controlo_sheet_id) ON DELETE CASCADE,
    event_type               text        NOT NULL,
    actor_id                 text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc          timestamptz NOT NULL DEFAULT now(),
    before_summary           jsonb       NULL,
    after_summary            jsonb       NULL,
    note                     text        NULL,
    CONSTRAINT ck_controlo_sheet_events_type CHECK (
        event_type IN ('criar', 'editar', 'submeter', 'reeabrir', 'decidir'))
);

CREATE INDEX IF NOT EXISTS ix_controlo_sheet_events_sheet ON controlo_sheet_events (controlo_sheet_id);

DROP TRIGGER IF EXISTS trg_controlo_sheet_events_append_only ON controlo_sheet_events;
CREATE TRIGGER trg_controlo_sheet_events_append_only
    BEFORE UPDATE OR DELETE ON controlo_sheet_events
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- ============================================================================
-- 24. Job On per-user current (N24)
-- ============================================================================
CREATE TABLE IF NOT EXISTS jobon_user_current (
    actor_id        text        PRIMARY KEY REFERENCES internal_users (actor_id),
    job_on_id       uuid        NOT NULL REFERENCES job_on (job_on_id),
    production_code text        NOT NULL,
    reference       text        NOT NULL DEFAULT '',
    machine_code    text        NOT NULL DEFAULT '',
    opened_at_utc   timestamptz NOT NULL DEFAULT now()
);

-- ============================================================================
-- 12. RLS + security contract (N12)
-- ============================================================================
-- 1. Enable RLS on every BA DMO table.
DO $$
DECLARE
    t text;
    rls_tables text[] := ARRAY[
        'internal_users', 'access_templates', 'audit_events',
        'module_catalog_mirror',
        'bq_lotes', 'bq_traces', 'bq_movements', 'bq_discrepancies',
        'bq_lifecycle_history', 'bq_utilisation_readings',
        'tool_references', 'tool_lotes', 'physical_pieces',
        'tool_check_rules',
        'job_on', 'job_on_revision', 'job_on_component',
        'job_on_component_field', 'job_on_component_row',
        'job_on_verification_occurrence', 'job_on_audit_event',
        'job_on_field_option',
        'peso_references', 'peso_lotes', 'peso_controlos', 'peso_leituras',
        'peso_day_approvals', 'peso_settings',
        'pegamento_controlos', 'pegamento_medicoes',
        'repairers', 'line_repairer_defaults', 'repair_exits',
        'repair_exit_items', 'repair_events', 'internal_repair_records',
        'warehouse_locations', 'warehouse_stock', 'warehouse_movements',
        'tampao_field_defs', 'tampao_field_values', 'tampao_configurations',
        'tampao_saldos', 'tampao_movements', 'tampao_planos',
        'app_settings',
        'schema_migrations'
    ];
BEGIN
    FOREACH t IN ARRAY rls_tables LOOP
        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
    END LOOP;
END
$$;

-- 2. anon / authenticated get no direct table access (guarded: only when the
--    Supabase roles exist).
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

-- 3. ba_dmo_app technical CRUD grants (guarded on Supabase when role absent).
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_app') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO ba_dmo_app;
        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO ba_dmo_app;
    END IF;
END
$$;

-- 4. Single technical policy per application table for ba_dmo_app.
--    schema_migrations intentionally gets NO policy (migrate CLI only).
DO $$
DECLARE
    t text;
    policy_tables text[] := ARRAY[
        'internal_users', 'access_templates', 'audit_events',
        'module_catalog_mirror',
        'bq_lotes', 'bq_traces', 'bq_movements', 'bq_discrepancies',
        'bq_lifecycle_history', 'bq_utilisation_readings',
        'tool_references', 'tool_lotes', 'physical_pieces',
        'tool_check_rules',
        'job_on', 'job_on_revision', 'job_on_component',
        'job_on_component_field', 'job_on_component_row',
        'job_on_verification_occurrence', 'job_on_audit_event',
        'job_on_field_option',
        'peso_references', 'peso_lotes', 'peso_controlos', 'peso_leituras',
        'peso_day_approvals', 'peso_settings',
        'pegamento_controlos', 'pegamento_medicoes',
        'repairers', 'line_repairer_defaults', 'repair_exits',
        'repair_exit_items', 'repair_events', 'internal_repair_records',
        'warehouse_locations', 'warehouse_stock', 'warehouse_movements',
        'tampao_field_defs', 'tampao_field_values', 'tampao_configurations',
        'tampao_saldos', 'tampao_movements', 'tampao_planos',
        'app_settings'
    ];
BEGIN
    FOREACH t IN ARRAY policy_tables LOOP
        EXECUTE format('DROP POLICY IF EXISTS ba_dmo_app_access ON %I', t);
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_app') THEN
            EXECUTE format(
                'CREATE POLICY ba_dmo_app_access ON %I FOR ALL TO ba_dmo_app USING (true) WITH CHECK (true)',
                t);
        END IF;
    END LOOP;
END
$$;

-- ============================================================================
-- N25 — deployment-readiness remediation (owner decisions D1-D7).
-- Mirrors database/migrations/N25_remediation.sql exactly.
-- (invariants + the 10 post-N12 tables' RLS/policy/REVOKE + PERF-01 index)
-- ============================================================================
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
-- N40 (D-10): approved Peso READINGS protection. N25 protects the approved
-- control ROW; this sibling guard makes any INSERT/UPDATE/DELETE on
-- peso_leituras fail while the parent control is approved. The same-release
-- service pairing confines readings DML to the draft-edit path and routes
-- submit/approve/reject/reopen/decide through header-only updates, so the
-- guard never fires on a legitimate flow (including approve/reopen).
-- Mirrors database/migrations/N40_peso_leituras_approved_guard.sql.
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION ba_dmo_guard_peso_leituras_approved()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    parent_status text;
BEGIN
    SELECT status INTO parent_status
      FROM peso_controlos
     WHERE peso_controlo_id = COALESCE(NEW.peso_controlo_id, OLD.peso_controlo_id);

    IF parent_status = 'aprovado' THEN
        RAISE EXCEPTION
            'BA DMO: readings of approved peso control % cannot be inserted, updated or deleted; reopen the control first',
            COALESCE(NEW.peso_controlo_id, OLD.peso_controlo_id);
    END IF;

    RETURN COALESCE(NEW, OLD);
END
$$;

DROP TRIGGER IF EXISTS trg_peso_leituras_approved_guard ON peso_leituras;
CREATE TRIGGER trg_peso_leituras_approved_guard
    BEFORE INSERT OR UPDATE OR DELETE ON peso_leituras
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_peso_leituras_approved();

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

-- ----------------------------------------------------------------------------
-- N26 (owner contract §6): per-user module grant override. Nullable jsonb on
-- internal_users; when non-null it replaces the template grants at identity
-- resolution (template rows untouched — per-user isolation).
-- NOT reproduced here: the column was REMOVED by N38 (dormant legacy surface;
-- N38_dormant_column_removal.sql). On an existing N01-N25 database the chain's
-- N26 adds it and N38 removes it; this final-state baseline simply never
-- creates it.
-- ----------------------------------------------------------------------------

-- ----------------------------------------------------------------------------
-- N27/N33/N34 — final access posture (mirrors removed).
-- The N27 junction table (internal_user_access_templates), its index,
-- RLS/policy/grants and the internal_users.profile_title mirror column (+ its
-- N27 CHECK) were physically REMOVED by N34 (chain: N34_legacy_access_mirror_removal.sql).
-- This final-state baseline does NOT reproduce them — a fresh install lands
-- directly in the post-N34 state (drift D-A from the N33 parity audit is
-- resolved on both paths).
-- The N33 §3 column-level grant refactor IS reproduced: ba_dmo_app's
-- table-level SELECT/INSERT/UPDATE on internal_users stays revoked and the
-- same three privileges are granted at COLUMN level for the exact canonical
-- column set (profile_title is gone and modules_override was REMOVED by N38,
-- so the explicit list is the seven surviving canonical columns).
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_app') THEN
        EXECUTE 'REVOKE SELECT, INSERT, UPDATE ON internal_users FROM ba_dmo_app';
        EXECUTE 'GRANT SELECT (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc) ON internal_users TO ba_dmo_app';
        EXECUTE 'GRANT INSERT (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc) ON internal_users TO ba_dmo_app';
        EXECUTE 'GRANT UPDATE (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc) ON internal_users TO ba_dmo_app';
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- N31 — template-owned functional profile + single effective template
-- (mirrors database/migrations/N31_template_profiles_single_assignment.sql;
-- audit CB-01). Adds access_template_profiles + the deterministic profile
-- trigger so Admin template editing works on consolidated-built databases
-- (42P01 fix). N31's mirror-sync DML (junction collapse/unique actor index and
-- the internal_users.profile_title sync) is NOT reproduced here: those mirror
-- structures were removed by N34 and are absent from this final-state
-- baseline; the template profile remains the single functional authority.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS access_template_profiles (
    template_id         text PRIMARY KEY REFERENCES access_templates (template_id) ON DELETE CASCADE,
    functional_profile  text NOT NULL,
    updated_at_utc      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_access_template_profiles_functional_profile CHECK (
        functional_profile IN ('Admin', 'Operador / Controlador', 'Responsável'))
);

-- Every newly inserted template receives a deterministic initial profile.
CREATE OR REPLACE FUNCTION ba_dmo_ensure_access_template_profile()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO access_template_profiles (template_id, functional_profile, updated_at_utc)
    VALUES (
        NEW.template_id,
        CASE
            WHEN NEW.modules @> '[{"moduleId":"admin"}]'::jsonb THEN 'Admin'
            ELSE 'Operador / Controlador'
        END,
        NEW.updated_at_utc)
    ON CONFLICT (template_id) DO NOTHING;
    RETURN NEW;
END
$$;

DROP TRIGGER IF EXISTS trg_access_templates_ensure_profile ON access_templates;
CREATE TRIGGER trg_access_templates_ensure_profile
    AFTER INSERT ON access_templates
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_ensure_access_template_profile();

-- Backfill existing templates (no-op on a fresh empty database). The
-- N31/N32-era fallback read internal_users.profile_title with MIN()/HAVING —
-- that mirror column is REMOVED by N34, so only the N31-established
-- deterministic fallback is reproduced (identical to the N32 §3 backfill).
INSERT INTO access_template_profiles (template_id, functional_profile, updated_at_utc)
SELECT
    t.template_id,
    CASE
        WHEN t.modules @> '[{"moduleId":"admin"}]'::jsonb THEN 'Admin'
        WHEN lower(t.name) LIKE '%respons%' THEN 'Responsável'
        ELSE 'Operador / Controlador'
    END,
    t.updated_at_utc
FROM access_templates t
LEFT JOIN access_template_profiles p ON p.template_id = t.template_id
WHERE p.template_id IS NULL
ON CONFLICT (template_id) DO NOTHING;

-- (N31's junction-sync steps — DELETE hybrid rows, INSERT one mirror row per
-- user, CREATE UNIQUE INDEX ux_internal_user_access_templates_actor, and the
-- UPDATE internal_users SET profile_title = p.functional_profile sync — are
-- NOT reproduced: the junction and the profile_title mirror were removed by
-- N34. On a fresh database those steps were no-ops anyway.)

ALTER TABLE access_template_profiles ENABLE ROW LEVEL SECURITY;
DO $$
DECLARE
    role_name text;
BEGIN
    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
            EXECUTE format(
                'REVOKE ALL ON TABLE access_template_profiles FROM %I', role_name);
        END IF;
    END LOOP;
END
$$;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE access_template_profiles TO ba_dmo_app;

-- N36 (D-15): policy-name convention. N31 named this policy
-- access_template_profiles_app_access; N36 renames it to the standard
-- ba_dmo_app_access with IDENTICAL semantics (FOR ALL TO ba_dmo_app USING
-- (TRUE) WITH CHECK (TRUE)); this final-state baseline creates the canonical
-- name directly.
DROP POLICY IF EXISTS ba_dmo_app_access ON access_template_profiles;
CREATE POLICY ba_dmo_app_access
    ON access_template_profiles
    FOR ALL TO ba_dmo_app
    USING (TRUE)
    WITH CHECK (TRUE);

-- END OF CONSOLIDATED CLEAN-INSTALL BASELINE (includes N25-N42)
