-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N19_tool_usage.sql — Ferramentas utilisation history (R003).
-- Authority: 02_DEC C8/C17 (utilisation = recorded manual fact, before/added/
--            after/cumulative; sap_start/sap_end kept as utilisation readings,
--            not SAP integration) + owner clarification: the % use is taken
--            MANUALLY from SAP by the operator — NO auto formula is applied.
-- Each reading snapshots sap_start/sap_end/percent_used per tool_lote so a later
-- change never reinterprets recorded history. Append-only: older readings are
-- never overwritten. Idempotent, forward-only. Executed WHOLE by the Npgsql
-- migration runner.
-- ============================================================================

CREATE TABLE IF NOT EXISTS tool_usage_records (
    tool_usage_record_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tool_lote_id         uuid        NOT NULL REFERENCES tool_lotes (tool_lote_id),
    sap_start            numeric(5,2) NULL,
    sap_end              numeric(5,2) NULL,
    percent_used         numeric(5,2) NULL,
    value_added          numeric(12,2) NULL,
    value_cumulative     numeric(12,2) NOT NULL,
    notes                text        NULL,
    actor_id             text        NULL REFERENCES internal_users (actor_id),
    reading_at_utc       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tool_usage_records_sap_start CHECK (sap_start IS NULL OR (sap_start >= 0 AND sap_start <= 100)),
    CONSTRAINT ck_tool_usage_records_sap_end CHECK (sap_end IS NULL OR (sap_end >= 0 AND sap_end <= 100)),
    CONSTRAINT ck_tool_usage_records_percent CHECK (percent_used IS NULL OR (percent_used >= 0 AND percent_used <= 100)),
    CONSTRAINT ck_tool_usage_records_cumulative CHECK (value_cumulative >= 0)
);

CREATE INDEX IF NOT EXISTS ix_tool_usage_records_lote
    ON tool_usage_records (tool_lote_id);

DROP TRIGGER IF EXISTS trg_tool_usage_records_append_only ON tool_usage_records;
CREATE TRIGGER trg_tool_usage_records_append_only
    BEFORE UPDATE OR DELETE ON tool_usage_records
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();