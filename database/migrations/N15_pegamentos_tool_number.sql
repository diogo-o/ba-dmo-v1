-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N15_pegamentos_tool_number.sql — Add tool_number to pegamento_medicoes.
-- Authority: Real production report evidence (202601_9389T194_C1.pdf) shows
--            N.º column = tool/cavity number being measured (e.g. CM: 42,51,34).
-- N07 pegamento_medicoes lacks this field; owner clarification requires it.
--
-- tool_number is NULL so pre-N15 historical rows are not fabricated with fake
-- values. Domain/Application/API enforce non-null for all NEW measurements.
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

ALTER TABLE pegamento_medicoes
    ADD COLUMN IF NOT EXISTS tool_number integer NULL;

CREATE INDEX IF NOT EXISTS ix_pegamento_medicoes_component_tool
    ON pegamento_medicoes (pegamento_controlo_id, component_key, tool_number);