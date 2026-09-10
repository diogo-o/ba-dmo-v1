-- ============================================================================
-- BA DMO N43 — Reparação Interna auditable annulment + Armazém Saída→Reparação
-- repairer association (owner corrections, 2026-09-10).
--
-- 1. internal_repair_records: Manual 60 §8 «Anulação» — `Apagar registo` is an
--    auditable annulation that REMOVES the record from the active operational
--    view and NEVER hard-deletes the historical fact. The chain ROOT row gains
--    the annulled marker (annulled_at_utc + annulled_by, server-side actor);
--    active lists exclude annulled chains, detail/audit remain readable.
--    Additive, NULL-able (legacy rows stay valid), forward-safe.
--
-- 2. warehouse_movements: Manual 40 §10.2 — Saída → Reparação associates the
--    canonical repairer (repairers.repairer_id, TD-15 shared directory) with
--    the physical movement, historically traceable. Append-only table: the
--    column is written at INSERT time only; NULL for every other destination.
--    No duplicate repairer field is created — the canonical identity is used.
--
-- Idempotent, forward-only, additive. Existing rows are preserved; no column
-- is dropped or renamed.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. Reparação Interna annulment markers (Manual 60 §8).
-- ----------------------------------------------------------------------------
ALTER TABLE internal_repair_records
    ADD COLUMN IF NOT EXISTS annulled_at_utc timestamptz,
    ADD COLUMN IF NOT EXISTS annulled_by     text        NULL REFERENCES internal_users (actor_id);

CREATE INDEX IF NOT EXISTS ix_internal_repair_records_annulled
    ON internal_repair_records (annulled_at_utc)
    WHERE annulled_at_utc IS NOT NULL;

-- ----------------------------------------------------------------------------
-- 2. Armazém movement repairer (Manual 40 §10.2; canonical directory repairers).
-- ----------------------------------------------------------------------------
ALTER TABLE warehouse_movements
    ADD COLUMN IF NOT EXISTS repairer_id uuid NULL REFERENCES repairers (repairer_id);

CREATE INDEX IF NOT EXISTS ix_warehouse_movements_repairer
    ON warehouse_movements (repairer_id);