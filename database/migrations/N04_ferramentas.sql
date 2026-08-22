-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N04_ferramentas.sql — Ferramentas (module owner: Ferramentas).
-- Authority: 06_DATA §3.5, TD-17 (processo no lote, não na referência),
--            modules/06_FERRAMENTAS_CM_MF_SPEC; GLM-FERR-13 left tool types
--            beyond CM/MF UNRESOLVED — resolved by current explicit owner
--            decision: Ferramentas tool types are CM, MF, BQ, PU, CS.
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
--
-- BOUNDARY NOTE (owner clarification + modules/06 §1): the 'BQ' tool TYPE
-- registered here is the Ferramentas tool identity only. The separate
-- Boquilhas OPERATIONAL module/domain (bq_lotes/bq_traces/bq_movements/
-- balances, N03) is NOT merged with it: no cross-identity FKs, separate
-- ownership, history and lifecycle.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- tool_references — master identity for tools.
-- tool_type discriminator: CM, MF, BQ, PU, CS (owner decision resolving
-- GLM-FERR-13). CM and MF remain distinct types with separate identities
-- and histories (GLM-FERR-01/12: never fused). NO processo column here:
-- processo belongs to the lote in the Peso flow (TD-17). Stable UUID id;
-- business identity (tool_type, ref_code) unique.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- tool_lotes — lots per tool reference.
-- lote; qty; allowed_lines; desenho + revisão; processo when applicable to
-- the flow (TD-17: processo NNPB/PS no lote). UNIQUE(tool_reference_id, lote).
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- physical_pieces — individual numbered pieces of a lot (CM/MF per number,
-- TD-22; BQ flows move by quantity). Immutable id; operational status;
-- identity preserved across duplications.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- tool_check_rules — verification rules configured on the lot card
-- (ferramentas.configure, TD-33). Edits apply to the future; copies keep
-- their origin.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- tool_check_occurrences — rules materialized in the Job On (created later
-- in this family). job_on_id/job_on_component_id are stable logical links
-- (uuid) without physical FKs because the Job On family is defined in a
-- later script and module coupling stays at the contract level (03_ARCH §4).
-- State machine: pendente → confirmada | reposta | desativada. Reset keeps
-- previous confirmations (new occurrence rows; history never rewritten).
-- completion_source is fixed: manual_job_on.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS tool_check_occurrences (
    tool_check_occurrence_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tool_check_rule_id       uuid        NOT NULL REFERENCES tool_check_rules (tool_check_rule_id),
    job_on_id                uuid        NULL,
    job_on_component_id      uuid        NULL,
    status                   text        NOT NULL DEFAULT 'pendente',
    completion_source        text        NOT NULL DEFAULT 'manual_job_on',
    completed_by             text        NULL REFERENCES internal_users (actor_id),
    completed_at_utc         timestamptz NULL,
    created_at_utc           timestamptz NOT NULL DEFAULT now(),
    created_by               text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc           timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tool_check_occurrences_status CHECK (
        status IN ('pendente', 'confirmada', 'reposta', 'desativada')),
    CONSTRAINT ck_tool_check_occurrences_source CHECK (completion_source = 'manual_job_on'),
    CONSTRAINT ck_tool_check_occurrences_completed CHECK (
        (status IN ('confirmada', 'reposta')) = (completed_at_utc IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS ix_tool_check_occurrences_rule ON tool_check_occurrences (tool_check_rule_id);
CREATE INDEX IF NOT EXISTS ix_tool_check_occurrences_job_on ON tool_check_occurrences (job_on_id);
