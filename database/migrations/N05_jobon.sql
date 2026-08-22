-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N05_jobon.sql — Job On family (module owner: JobOn; tables job_on*, TD-18).
-- Authority: 06_DATA §3.6, modules/05_JOB_ON_SPEC (TD-27 lifecycle/Resolve),
--            GLM-DATA-04 (snapshot ≠ live), 06_DATA §9/TD-23 (image).
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- job_on — operational production sheet; central production context.
-- Stable id; production_code indexed; article reference is a nullable logical
-- link plus snapshot (snapshot ≠ live; resolved through the Peso lookup
-- contract — 03_ARCH §6); machine_code is also the calendar line
-- (Resolve(line, at): machine = line, modules/05 §5); planned dates are the
-- single calendar source; copied_from_job_on_id for duplications.
-- ----------------------------------------------------------------------------
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
    CONSTRAINT ck_job_on_status CHECK (
        status IN ('rascunho', 'planeado', 'em_fabrico', 'fechado', 'cancelado'))
);

CREATE INDEX IF NOT EXISTS ix_job_on_production_code ON job_on (production_code);
CREATE INDEX IF NOT EXISTS ix_job_on_status ON job_on (status);
CREATE INDEX IF NOT EXISTS ix_job_on_machine_planned ON job_on (machine_code, planned_start_at);

-- ----------------------------------------------------------------------------
-- job_on_revision — saved revisions; snapshots are immutable facts.
-- "Guardar alterações" inserts a NEW revision (no destructive UPDATE of saved
-- revisions); change_reason is mandatory when editing a closed revision
-- (enforced by the Application, modules/05 §4). image_asset_id is a stable
-- LOGICAL association/metadata for the revision image — NEVER image binary
-- (06_DATA §9, TD-23; binary stays on the user's filesystem via File System
-- Access API, client-side only — U-13).
--
-- ATTRIBUTION ANCHOR (TD-18 + owner clarification): downstream records
-- (Peso, Pegamentos) pin job_on_revision_id to stay historically
-- attributable to the Ferramentas identified by this revision's
-- job_on_component rows (source_tool_id/source_lot_id). Editing the Job On
-- or substituting a tool creates a NEW revision; saved revisions are never
-- rewritten, so pinned records keep their original tool attribution.
-- ----------------------------------------------------------------------------
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
    image_asset_id      text        NULL,
    change_reason       text        NULL,
    saved_by            text        NULL REFERENCES internal_users (actor_id),
    saved_at_utc        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_job_on_revision_number UNIQUE (job_on_id, revision_number),
    CONSTRAINT ck_job_on_revision_number CHECK (revision_number >= 1)
);

CREATE INDEX IF NOT EXISTS ix_job_on_revision_job_on ON job_on_revision (job_on_id);

-- Circular link: job_on.current_revision_id → job_on_revision. Added here
-- (idempotently) because both tables are created in this same script.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_job_on_current_revision') THEN
        ALTER TABLE job_on
            ADD CONSTRAINT fk_job_on_current_revision
            FOREIGN KEY (current_revision_id) REFERENCES job_on_revision (job_on_revision_id);
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- job_on_component — one component per family per revision
-- (MP/CM, MF, BQ, PU, CAL, AN, ARR, PI, CS, TP, FO). Source tool/lot are
-- physical links to Ferramentas (created earlier in this family) plus
-- snapshots (snapshot ≠ live).
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- job_on_component_field — typed fields with dedicated columns per value
-- type (text/integer/decimal/boolean/date/select). Values used in
-- calculation/filter never live only in JSON/text (06_DATA §3.6).
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- job_on_component_row — repeatable CAL rows (element, values, unit,
-- quantity in machine).
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- job_on_verification_occurrence — verification checks materialized in the
-- Job On (origin rule from Ferramentas). completion_source fixed to
-- manual_job_on; operator/date-hour only after persistence.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- job_on_audit_event — module-level audit facts (creation, duplication, edit
-- open, save, tool substitution, date changes, checks). before/after are
-- audit-only; they never replace revision snapshots. Append-only.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- job_on_field_option — data-driven catalogs per family/field for evolvable
-- business dropdowns (managed in Definições). Deactivating preserves values
-- stored in older revisions.
-- ----------------------------------------------------------------------------
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
