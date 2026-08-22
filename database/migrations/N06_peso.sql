-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N06_peso.sql — Peso (module owner: Peso; tables peso_*).
-- Authority: 06_DATA §3.3, TD-17 (processo no lote), TD-13/TD-28/TD-30,
--            DS-04 (Job On context, no second CM/lote selection),
--            modules/03_PESO_SPEC.
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- peso_references — master identity: UNIQUE(mold_number, neckring_number).
-- counter_mold; volumes used by the deterministic C# calculations (TD-28);
-- calote_tp numeric; change_log keeps the justification trail of approved
-- reference edits (edition withdraws approval and creates a new revision —
-- never a silent rewrite).
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- peso_lotes — Peso lots per reference.
-- processo NNPB/PS lives in the LOT (TD-17); allowed_lines minimum one;
-- report_subfolder is a RELATIVE folder name (never a free absolute path —
-- 06_DATA §9); nominal weight. UNIQUE(reference, lote).
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- peso_controlos — controls and comparisons.
-- UNIQUE(mold, neckring, production, line, lote, date); record_type
-- novo_controlo/comparacao; job_on_id + job_on_revision_id MANDATORY
-- (DS-04: CM/lote inherited from Job On, no own selection); states
-- rascunho/pendente/aprovado/nao_aprovado; snapshots; approval_log;
-- previous_control; comparison decisions per CM.
--
-- HISTORICAL FERRAMENTA ATTRIBUTION (owner clarification + TD-18/TD-26):
-- the record is pinned to job_on_revision_id — an IMMUTABLE revision whose
-- job_on_component rows identify the Ferramenta (source_tool_id/source_lot_id)
-- that was in use. Later Job On edits/tool substitutions create a NEW
-- revision and never rewrite the pinned one, so this control remains
-- attributable to the original Ferramenta. peso_lote_id adds the stable Peso
-- control identity of the same CM (TD-26 correspondence by mold code + lot,
-- no duplicated attributes). The job_on_id column alone is context/grouping
-- and is never used for tool attribution.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- peso_leituras — readings per control/CM. UNIQUE(controlo, cm_number).
-- CASCADE: physical delete is only possible while the control itself can be
-- deleted (rascunho/nao_aprovado policy — 06_DATA §2; approved controls are
-- immutable facts and never deleted).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS peso_leituras (
    peso_leitura_id  uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    peso_controlo_id uuid        NOT NULL REFERENCES peso_controlos (peso_controlo_id) ON DELETE CASCADE,
    cm_number        text        NOT NULL,
    readings         jsonb       NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc   timestamptz NOT NULL DEFAULT now(),
    created_by       text        NULL REFERENCES internal_users (actor_id),
    CONSTRAINT uq_peso_leituras_controlo_cm UNIQUE (peso_controlo_id, cm_number)
);

-- ----------------------------------------------------------------------------
-- peso_comparacao_anterior — persisted read path of the previous approved
-- control (TD-13/TD-30): most recent approved control of the same
-- mold+neckring with earlier production/date, resolved CROSS-LINE; null
-- when none exists (deltas stay null — no invented comparison).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS peso_comparacao_anterior (
    peso_controlo_id           uuid        PRIMARY KEY REFERENCES peso_controlos (peso_controlo_id) ON DELETE CASCADE,
    previous_peso_controlo_id  uuid        NULL REFERENCES peso_controlos (peso_controlo_id),
    previous_snapshot          jsonb       NULL,
    deltas                     jsonb       NULL,
    resolved_at_utc            timestamptz NOT NULL DEFAULT now()
);

-- ----------------------------------------------------------------------------
-- peso_day_approvals — day approvals. UNIQUE(mold, neckring, line, date).
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- peso_settings — Peso settings (key PK): constants (constant_nnpb/ps),
-- recipients per line group, main_output_folder_name (06_DATA §9).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS peso_settings (
    setting_key    text        PRIMARY KEY,
    setting_value  jsonb       NOT NULL,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_by     text        NULL REFERENCES internal_users (actor_id)
);
