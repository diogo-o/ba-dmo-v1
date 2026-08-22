-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N11_partilhado.sql — shared settings (06_DATA §3.10).
-- Authority: 06_DATA §3.10 (app_settings), GLM-DATA-11 (no operational seeds).
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- app_settings — shared key/value settings (value jsonb). Example defined
-- by Plan-V3: recipients per line group (B1–B3 → Linha B, C1–C3 → Linha C).
-- Each setting is written only by the owner of that setting (GLM-ARCH-05);
-- NO rows are seeded here.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS app_settings (
    setting_key    text        PRIMARY KEY,
    setting_value  jsonb       NOT NULL,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_by     text        NULL REFERENCES internal_users (actor_id)
);
