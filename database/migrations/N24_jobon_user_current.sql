-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N24_jobon_user_current.sql — R011 Universal Landing: the Job On context THIS
--   user explicitly opened/selected from the Job On landing calendar + list.
--
-- OWNER RULE (§14/§15): "current Job On" means the specific Job On this USER
-- explicitly opened/selected — NOT the globally-newest Job On, NOT the newest DB
-- row, NOT a clock/current-production derivation. It is user-scoped and only
-- records an explicit open, so a future Controlo "Carregar Job On atual" can
-- reliably consume it.
--
-- This is a small, additive, forward-only table. One current row per
-- internal user (actor_id PK). It stores the stable job_on_id + a readable
-- context snapshot (production/reference/machine) + the open timestamp. It in no
-- way duplicates or owns production planning — the Job On business tables remain
-- the single planning source (§8/§17).
-- ============================================================================

-- ----------------------------------------------------------------------------
-- jobon_user_current — one row per user: the Job On they most recently opened.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS jobon_user_current (
    actor_id        text        PRIMARY KEY REFERENCES internal_users (actor_id),
    job_on_id       uuid        NOT NULL REFERENCES job_on (job_on_id),
    production_code text        NOT NULL,
    reference       text        NOT NULL DEFAULT '',
    machine_code    text        NOT NULL DEFAULT '',
    opened_at_utc   timestamptz NOT NULL DEFAULT now()
);