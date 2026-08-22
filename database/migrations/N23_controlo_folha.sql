-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N23_controlo_folha.sql — Folha de Controlo (production-level control summary
--   sheet), an OWNER-DECISION workflow inside the Controlo functional area.
-- Authority: OWNER DECISION "TARGET CONTROLO" + "MODULE IDENTITY" (R010).
--
-- The Folha de Controlo is a production-associated record/document INSIDE
-- Controlo (distinct from Peso and Pegamentos/Ferramentas; no schema or logic
-- merge). It is anchored to job_on_id + the EXACT immutable job_on_revision_id
-- and snapshots the production components/tools of that revision.
--
-- Audit contract (owner): from the sheet it must always be provable WHICH
-- production/revision/machine/reference/tool components/lots it belonged to,
-- their OK/NOK + observation + MCaliper links, the actors/timestamps, and the
-- submit/edit/approval-rejection history. A later Job On revision must NOT
-- reinterpret an old submitted sheet (immutable revision anchor + snapshot).
--
-- Editing after submission is traceable via the append-only events table; the
-- current sheet version is the live record and history never silently rewrites
-- the audit trail.
--
-- Forward-only, additive, idempotent. New tables follow the additive-migration
-- convention (no RLS stanza here, matching N18–N22 additive tables).
-- ============================================================================

-- ----------------------------------------------------------------------------
-- controlo_sheets — one production control summary sheet.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS controlo_sheets (
    controlo_sheet_id    uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    job_on_id            uuid        NOT NULL REFERENCES job_on (job_on_id),
    job_on_revision_id   uuid        NOT NULL REFERENCES job_on_revision (job_on_revision_id),
    production_code      text        NOT NULL,
    reference            text        NOT NULL,
    machine_code         text        NOT NULL,
    display_id           text        NOT NULL, -- Controlo_<PROD>_<REF>_<MAQ> (display/document id)
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

-- ----------------------------------------------------------------------------
-- controlo_sheet_items — per-component/tool snapshot + control result.
-- Copied from the pinned Job On revision's job_on_component rows at creation.
-- Later Job On changes must not change these snapshots.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- controlo_sheet_events — append-only audit of create/edit/submit/reopen/decide.
-- ----------------------------------------------------------------------------
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