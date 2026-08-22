-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N17_pegamentos_notas.sql — Add notas to pegamento_controlos.
-- Authority: U-11 plan §6.2 — UpdateControlAsync updates editable fields
--            (tolerance, notes); CreatePegamentoRequest includes Notes.
-- N07 pegamento_controlos lacks notas; the domain entity PegamentoControlo
--            and DapperPegamentoRepository require it for the notes field.
-- notas is NULL so pre-N17 historical rows are not fabricated with fake values.
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

ALTER TABLE pegamento_controlos
    ADD COLUMN IF NOT EXISTS notas text NULL;