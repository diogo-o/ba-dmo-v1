-- ============================================================================
-- BA DMO N36 — RLS policy naming convention (D-15; SAFE NOW).
--
-- The single technical RLS policy per application table is conventionally
-- named `ba_dmo_app_access` (N12 §4 pattern, N25 §2, N29). Two deliberately
-- named exceptions existed: `internal_user_access_templates_app_access` (N27 —
-- removed with the junction table by N34) and `access_template_profiles_app_access`
-- (N31). After N34, `access_template_profiles_app_access` is the ONLY name that
-- diverges from the convention — this migration unifies it (owner decision
-- OD-10 / D-15; reports/post_codex_database_rationalization_plan.md §9.2, §13.3).
--
-- This is SECURITY NAMING / CONSISTENCY RATIONALIZATION, NOT permission
-- redesign:
--   * identical authorization semantics — policy body preserved byte-for-byte
--     (FOR ALL TO ba_dmo_app USING (TRUE) WITH CHECK (TRUE), exactly as N31);
--   * identical permissions — the ba_dmo_app DML entitlements on
--     access_template_profiles issued in N31 are untouched;
--   * identical RLS behavior — RLS stays enabled; the rename only affects the
--     policy NAME; no runtime code names policies (grep-verified: zero src
--     references to any policy name).
--
-- Idempotence: DROP IF EXISTS on the old name (pre-N36 databases) AND on the
-- new name (already-renamed databases) before CREATE — the N27/N31 DROP
-- POLICY IF EXISTS / CREATE POLICY pattern. Whole-script in its own
-- transaction (no BEGIN/COMMIT); historical migrations N01-N35 are immutable
-- and were not modified.
--
-- Post-N36 expected inventory: every application table (60 after N34) carries
-- exactly one policy named `ba_dmo_app_access`; the divergent name is gone.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- §1. Drop the divergent policy name (idempotent — absent on post-N36 DBs).
-- ----------------------------------------------------------------------------
DROP POLICY IF EXISTS access_template_profiles_app_access
    ON access_template_profiles;

-- ----------------------------------------------------------------------------
-- §2. (Re)create the convention-named policy with IDENTICAL semantics.
-- ----------------------------------------------------------------------------
DROP POLICY IF EXISTS ba_dmo_app_access
    ON access_template_profiles;
CREATE POLICY ba_dmo_app_access
    ON access_template_profiles
    FOR ALL TO ba_dmo_app
    USING (TRUE)
    WITH CHECK (TRUE);