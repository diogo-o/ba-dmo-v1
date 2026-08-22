-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N20_repairer_repair_types.sql — Repairer capability (many-to-many) (R004).
-- Authority: TD-15 (repairer canónico) + owner: a repairer may repair CM, MF, BQ
--            or any valid combination; `line_repairer_defaults` stays a pure
--            convenience default and does NOT define capability; never duplicate
--            a repairer to represent multiple types.
-- repairer_repair_types is a join table (UNIQUE(repairer_id, repair_type)).
-- Historical references/snapshots are preserved (repairers are never hard-deleted).
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

CREATE TABLE IF NOT EXISTS repairer_repair_types (
    repairer_id uuid NOT NULL REFERENCES repairers (repairer_id),
    repair_type text NOT NULL,
    PRIMARY KEY (repairer_id, repair_type),
    CONSTRAINT ck_repairer_repair_types_type CHECK (repair_type IN ('CM', 'MF', 'BQ'))
);