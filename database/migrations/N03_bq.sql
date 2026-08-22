-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N03_bq.sql — Boquilhas (module owner: Boquilhas; tables bq_*).
-- Authority: 06_DATA §3.2, GLM-DATA-02/04, UD-08/UD-09 (no allow_unmatched),
--            modules/01_BOQUILHAS_SPEC.
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- bq_lotes — master lot identity (stable UUID, never reused).
-- UNIQUE(reference, batch_code); reference pattern ^[A-Z][0-9]{3}$;
-- allowed_lines text[]; lifecycle_state only stores the persisted states
-- (available/archived/scrapped) — "ativo/preparing" is DERIVED from traces
-- (GLM-DATA-04.5: no invented states).
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- bq_traces — production/repair trace per lot.
-- start_line NOT NULL (TD-14); sap_start/sap_end utilisation 0–100;
-- reopen_history/deleted_movements keep void/reopen facts (never delete).
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- bq_movements — append-only movement facts.
-- type IN (inicio, saida, entrada, irreparavel, linha, contagem, fim);
-- qty NULL only for 'linha'; exceptional_received_qty records exceptional
-- return quantities (20→25 case, UD-08). There is NO allow_unmatched column
-- (UD-09) and no heuristic block of real facts (GLM-CORE-01).
-- ----------------------------------------------------------------------------
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
    CONSTRAINT ck_bq_movements_type CHECK (
        movement_type IN ('inicio', 'saida', 'entrada', 'irreparavel', 'linha', 'contagem', 'fim')),
    CONSTRAINT ck_bq_movements_qty CHECK (qty IS NOT NULL OR movement_type = 'linha'),
    CONSTRAINT ck_bq_movements_exceptional CHECK (exceptional_received_qty IS NULL OR exceptional_received_qty >= 0)
);

CREATE INDEX IF NOT EXISTS ix_bq_movements_trace ON bq_movements (bq_trace_id);
CREATE INDEX IF NOT EXISTS ix_bq_movements_occurred ON bq_movements (occurred_at_utc);

DROP TRIGGER IF EXISTS trg_bq_movements_append_only ON bq_movements;
CREATE TRIGGER trg_bq_movements_append_only
    BEFORE UPDATE OR DELETE ON bq_movements
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- ----------------------------------------------------------------------------
-- bq_discrepancies — return excess (C27): expected/actual/excess with
-- auditable resolution (warning + record, never a block — GLM-CORE-01).
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- bq_lifecycle_history — archived/scrapped/restored/retired + reason + actor.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- bq_utilisation_readings — manual initial/final utilisation values 0–100.
-- ----------------------------------------------------------------------------
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
