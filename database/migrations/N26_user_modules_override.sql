-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 owner decision #4 verified in
-- ADMIN_BACKEND_INVENTORY_B2: per-user module grants reuse the shared template
-- resolution funnel via a per-user override, GLM-ACC-02/ACC-03, U-02 N01).
-- N26_user_modules_override.sql — add a nullable per-user grant override.
--
-- OWNER DECISION (contract §6): grants live ONLY in access_templates.modules
-- jsonb ([{moduleId, capabilities[]}]); users link via internal_users.template_id.
-- This migration adds an OPTIONAL nullable modules_override jsonb on
-- internal_users so Administration can associate extra modules per user without
-- touching any shared template row (other users on the same template are
-- unaffected). When non-null it REPLACES the template grants at identity
-- resolution; when NULL the template path is unchanged. It in no way duplicates
-- template grants nor grants access on its own — it is consumed only through the
-- canonical parser + AccessResolver at runtime (GLM-ACC-03).
--
-- Additive / idempotent / forward-only: re-running is a no-op.
-- ============================================================================

ALTER TABLE internal_users
    ADD COLUMN IF NOT EXISTS modules_override jsonb;