-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N01_identity.sql — roles, internal identity, access templates, global audit.
-- Authority: 06_DATA §1 (roles), §3.1 (Shell/Identity/Admin), §7 (audit),
--            GLM-DATA-02 (conventions), TD-19 (audit_events).
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Database roles (least privilege, 06_DATA §1).
-- ba_dmo_app: runtime role used by the application request pipeline.
-- ba_dmo_migrate: DDL role used exclusively by the CLI migration runner.
-- Login credentials are never part of this script (environment only, §14/§6.5).
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_app') THEN
        CREATE ROLE ba_dmo_app NOLOGIN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_migrate') THEN
        CREATE ROLE ba_dmo_migrate NOLOGIN;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA public TO ba_dmo_app;
GRANT USAGE ON SCHEMA public TO ba_dmo_migrate;

-- Future objects created by the migrate role automatically grant DML to the
-- runtime role (06_DATA §1: ALTER DEFAULT PRIVILEGES para objetos futuros).
ALTER DEFAULT PRIVILEGES FOR ROLE ba_dmo_migrate IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ba_dmo_app;
ALTER DEFAULT PRIVILEGES FOR ROLE ba_dmo_migrate IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO ba_dmo_app;

-- ----------------------------------------------------------------------------
-- Immutability guard for append-only fact tables (GLM-DATA-04.1, GLM-DATA-07:
-- movimentos, leituras, eventos e auditoria nunca sofrem UPDATE/DELETE;
-- correções são registos novos).
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION ba_dmo_guard_append_only()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'BA DMO: append-only table % cannot be updated or deleted', TG_TABLE_NAME;
END
$$;

-- ----------------------------------------------------------------------------
-- access_templates (06_DATA §3.1).
-- PK text; modules jsonb NOT NULL default '[]'; active; catalog validation is
-- performed in the Application layer against the ModuleCatalog (U-04), never
-- by free identifiers accepted from the client.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- internal_users (06_DATA §3.1, TD-09).
-- PK actor_id text; auth_user_id uuid NULL (logical reference to Supabase
-- Auth; NO foreign key to auth.users); template_id FK; active; profile_title.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS internal_users (
    actor_id        text        PRIMARY KEY,
    auth_user_id    uuid        NULL,
    template_id     text        NOT NULL REFERENCES access_templates (template_id),
    display_name    text        NOT NULL,
    profile_title   text        NULL,
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

-- ----------------------------------------------------------------------------
-- audit_events (06_DATA §3.1, TD-19, GLM-DATA-07).
-- Canonical single global audit table: append-only; never partitioned by
-- module/year; no UPDATE/DELETE; no secrets/binaries. job_on_id kept as a
-- plain uuid (denormalized audit fact; no cross-domain FK coupling).
-- ----------------------------------------------------------------------------
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
