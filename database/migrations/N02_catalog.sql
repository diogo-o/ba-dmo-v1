-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N02_catalog.sql — module catalog mirror (Admin UI only).
-- Authority: 06_DATA §3.1, TD-10, GLM-CAT (modules/00).
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- module_catalog_mirror (TD-10, GLM-CAT-02).
-- Mirror of the in-code ModuleCatalog serving ordering/display in the Admin
-- UI. It NEVER grants access: authorization is resolved server-side from the
-- catalog in code ∩ access templates (03_ARCH §7). Rows are synchronized by
-- the Application (U-04); no operational seed data here (GLM-DATA-11).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS module_catalog_mirror (
    module_id       text        PRIMARY KEY,
    display_name    text        NOT NULL,
    display_order   integer     NOT NULL,
    active          boolean     NOT NULL DEFAULT TRUE,
    synced_at_utc   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_module_catalog_mirror_order
    ON module_catalog_mirror (display_order);
