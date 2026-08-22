-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N09_armazem.sql — Armazém (module owner: Armazém; tables warehouse_*).
-- Authority: 06_DATA §3.8, GLM-DATA-05 (atomic position+occupation writes).
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- warehouse_locations — positions. code UNIQUE; kind.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS warehouse_locations (
    warehouse_location_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    code                  text        NOT NULL UNIQUE,
    kind                  text        NULL,
    created_at_utc        timestamptz NOT NULL DEFAULT now(),
    created_by            text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc        timestamptz NOT NULL DEFAULT now()
);

-- ----------------------------------------------------------------------------
-- warehouse_stock — occupation 1:1 per position.
-- A position is occupied by at most one tool lot at a time; releases keep
-- the fact row (released_at_utc) so the partial unique index allows the
-- same position/lot pair to be occupied again later. "fora" is CALCULATED
-- from facts, never stored (GLM-DATA-04.4/5).
-- ----------------------------------------------------------------------------
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

-- ----------------------------------------------------------------------------
-- warehouse_movements — in/out facts with destination and the link to the
-- planned exit (saída programada) when applicable. Append-only.
-- ----------------------------------------------------------------------------
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
