-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N18_bq_repairer.sql — Add per-movement repairer to bq_movements.
-- Authority: U-19 — Boquilhas implements the canonical repairer-vocabulary
--            (canonical `repairers` / `line_repairer_defaults` with
--            tool_type='BQ', TD-15). A Saída (repair dispatch) associates the
--            actually-chosen repairer with the MOVEMENT so later config changes
--            never rewrite the history (BOQUILHAS_INTERFACE_BEHAVIOR §9); a
--            movement may carry "Sem associação" (NULL). N03 bq_movements
--            lacked this column.
-- noted_repairer_id is NULL so pre-N18 historical rows are not fabricated.
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

ALTER TABLE bq_movements
    ADD COLUMN IF NOT EXISTS noted_repairer_id uuid NULL
        REFERENCES repairers (repairer_id);