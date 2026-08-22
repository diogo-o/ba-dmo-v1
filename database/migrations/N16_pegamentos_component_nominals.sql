-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N16_pegamentos_component_nominals.sql — Per-component historical nominals.
-- Authority: Owner clarification — CM/BQ/MF have different nominal values;
--            a single nominal_average must NOT validate all three components.
-- N07 only has nominal_average; per-component nominals are required for correct
-- tolerance evaluation. Existing rows may remain NULL (legacy); NEW controls
-- require all three nominals from the exact Job On revision context.
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

ALTER TABLE pegamento_controlos
    ADD COLUMN IF NOT EXISTS cm_nominal numeric(18,4) NULL;

ALTER TABLE pegamento_controlos
    ADD COLUMN IF NOT EXISTS bq_nominal numeric(18,4) NULL;

ALTER TABLE pegamento_controlos
    ADD COLUMN IF NOT EXISTS mf_nominal numeric(18,4) NULL;