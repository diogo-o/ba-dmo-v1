-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N08_reparacoes.sql — Repair (Reparação Externa + Reparação Interna).
-- Authority: 06_DATA §3.7, TD-15 (canonical repairer), TD-22 (BQ by qty,
--            CM/MF by number), BT-07, modules/08 + modules/09.
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- repairers — canonical repairer registry (TD-15). Deactivated repairers are
-- never deleted.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS repairers (
    repairer_id    uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    name           text        NOT NULL,
    active         boolean     NOT NULL DEFAULT TRUE,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now()
);

-- ----------------------------------------------------------------------------
-- line_repairer_defaults — default repairer per (line, tool type) (TD-15).
-- Repair types defined by Plan-V3: BQ / CM / MF.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS line_repairer_defaults (
    line           text        NOT NULL,
    tool_type      text        NOT NULL,
    repairer_id    uuid        NOT NULL REFERENCES repairers (repairer_id),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_by     text        NULL REFERENCES internal_users (actor_id),
    PRIMARY KEY (line, tool_type),
    CONSTRAINT ck_line_repairer_defaults_type CHECK (tool_type IN ('BQ', 'CM', 'MF'))
);

-- ----------------------------------------------------------------------------
-- repair_exits — planned exit lists (external repair cycle).
-- repair_type BQ/CM/MF; repairer snapshot; planned date; status machine
-- preparacao → a_retirar → enviado → retorno_parcial → concluido | cancelado.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- repair_exit_items — items of an exit list.
-- BQ by quantity; CM/MF by individual number (TD-22). picked/out/in facts
-- with operators; per-item state. Boquilhas items reference the Boquilhas
-- operational lot (bq_lotes); CM/MF items reference Ferramentas pieces.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- repair_events — repair history facts (internal or external). Cancelled
-- events do not count; repair_count is DERIVED, never stored
-- (GLM-DATA-04.5). Append-only.
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- internal_repair_records — quick internal repair records.
-- Linha + tipo CM/MF + número individual with the resolved Job On context
-- (job_on_id logical link; active Job On is mandatory at record time —
-- enforced by the Application, modules/08). Corrections are NEW rows with
-- before/after + author + reason (GLM-DATA-07), never rewrites.
-- ----------------------------------------------------------------------------
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
    CONSTRAINT ck_internal_repair_records_type CHECK (tool_type IN ('CM', 'MF')),
    CONSTRAINT ck_internal_repair_records_correction CHECK (
        (correction_of_id IS NULL) = (before_snapshot IS NULL))
);

CREATE INDEX IF NOT EXISTS ix_internal_repair_records_line ON internal_repair_records (line);
CREATE INDEX IF NOT EXISTS ix_internal_repair_records_job_on ON internal_repair_records (job_on_id);

-- Foreign link from repair_events to internal records, added here because
-- internal_repair_records is created later in this same script.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_repair_events_internal_record') THEN
        ALTER TABLE repair_events
            ADD CONSTRAINT fk_repair_events_internal_record
            FOREIGN KEY (internal_repair_record_id)
            REFERENCES internal_repair_records (internal_repair_record_id);
    END IF;
END
$$;
