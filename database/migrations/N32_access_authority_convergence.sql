-- ============================================================================
-- BA DMO N32 — access-authority convergence (SCHEMA-RAT-03A, D-1 + D-2 only).
--
-- Approved target authority (owner decisions D-1/D-2, reports/
-- schema_rationalization_owner_decisions.md):
--
--   USER → TEMPLATE (D-2, Option A)
--     internal_users.template_id is the CANONICAL direct FK for the user's
--     single effective template. internal_user_access_templates is NOT the
--     target authority for current assignment; it remains physically present
--     in this phase ONLY as an optional one-way legacy mirror (written in the
--     same transaction as the direct FK by the Application, max one row per
--     user — ux_internal_user_access_templates_actor from N31).
--
--   TEMPLATE → FUNCTIONAL PROFILE (D-1, Option A)
--     access_template_profiles.functional_profile is the CANONICAL functional
--     profile (one profile per template). internal_users.profile_title is NOT
--     the functional-access authority; it remains physically present only as
--     a one-way compatibility mirror of the template profile.
--
-- MODULE ACCESS (D-6)
--     Unchanged by this migration. Template-owned module selection in
--     access_templates.modules remains the module-access source; this file
--     does NOT touch the module catalog architecture.
--
-- This migration is SAFE and NON-DESTRUCTIVE:
--   * removes/renames no table, column, index or constraint of any legacy
--     object (legacy objects are only kept or read);
--   * historical migrations N01-N31 are immutable and untouched;
--   * data reconciliation is FAIL-CLOSED — it never silently chooses among
--     conflicting legacy assignments (no smallest/largest/"first"/"latest"
--     heuristics used as authority) and never copies user profile_title back
--     into templates.
--
-- Reconciliation rules implemented here:
--   A) representations agree  -> keep internal_users.template_id (no-op).
--   B) only one valid assignment exists -> backfill internal_users.template_id
--      from it. In practice template_id is NOT NULL with a hard FK to
--      access_templates (N01), so there is no reachable state where the FK is
--      the missing representation while a junction row is the only claim; the
--      only single-representation state is "junction empty", which needs no
--      backfill. Therefore NO backfill-from-junction DML is emitted.
--   C) multiple/conflicting junction assignments -> the migration FAILS with a
--      clear diagnostic listing the offending actor ids. It never collapses,
--      merges or picks one (product model forbids ambiguity; no smallest/
--      largest/earliest/latest selection is ever applied).
--      The only allowed convergence is: junction row(s) equal to the direct FK
--      (which is a no-op) and exactly-zero junction rows (optional mirror).
--   D) zero effective template -> structurally impossible for the FK (NOT NULL
--      + FK); resolution fails closed on inactive/missing templates at
--      runtime, unchanged. No invented template is assigned here.
--
-- PROFILE RECONCILIATION:
--   The only profile repair performed is backfilling a MISSING
--   access_template_profiles row per template using the N31-established
--   deterministic default (admin module -> Admin; name '%respons%' ->
--   Responsável; else Operador / Controlador) — the exact fallback of N31's
--   own backfill, i.e. an already-established deterministic authority.
--   User profile_title is NEVER copied back into the template as a source of
--   truth. Direction of authority: template profile -> derived user effective
--   profile (one-way).
--
-- Conventions: idempotent, guarded, forward-only; executed WHOLE by the
-- Npgsql migration runner inside its own per-script transaction (no explicit
-- BEGIN/COMMIT here — N28/N29/N30 transaction-control debt is not repeated).
-- ============================================================================

-- ----------------------------------------------------------------------------
-- §1. FAIL-CLOSED: multiple legacy junction assignments per user.
-- N31's ux_internal_user_access_templates_actor already makes this
-- impossible in a chain-migrated database; this guard exists for databases
-- that reached N32 by another path. Never silently pick one.
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    v_sample text;
BEGIN
    SELECT string_agg(actor_id, ', ' ORDER BY actor_id)
      INTO v_sample
      FROM (
          SELECT actor_id
            FROM internal_user_access_templates
           GROUP BY actor_id
          HAVING COUNT(*) > 1
      ) multi;

    IF v_sample IS NOT NULL THEN
        RAISE EXCEPTION
            'N32 blocked: internal_user_access_templates carries MULTIPLE assignments for actor(s): % — the product model allows exactly ONE effective template per user. Corrigir manualmente (não escolher automaticamente) antes de reaplicar a N32.',
            v_sample;
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- §2. FAIL-CLOSED: single junction assignment CONFLICTS with the direct FK.
-- Both claims reference existing templates (hard FKs), so neither is
-- "invalid": this is ambiguous legacy state. The direct FK is the approved
-- authority, but a surviving contradictory junction row would keep a second
-- live claim alive; fail with a diagnostic instead of silently rewriting.
-- (Deterministic reconciliation IS allowed only for the empty-junction state,
-- which needs no write.)
-- ----------------------------------------------------------------------------
DO $$
DECLARE
    v_sample text;
BEGIN
    SELECT string_agg(u.actor_id, ', ' ORDER BY u.actor_id)
      INTO v_sample
      FROM internal_users u
      JOIN internal_user_access_templates ut ON ut.actor_id = u.actor_id
     WHERE ut.template_id IS DISTINCT FROM u.template_id
       AND ut.template_id IS NOT NULL;

    IF v_sample IS NOT NULL THEN
        RAISE EXCEPTION
            'N32 blocked: internal_user_access_templates disputes the canonical internal_users.template_id for actor(s): % — the direct FK is the authority and the junction is a one-way mirror; a conflicting mirror row must be reconciled manually before re-applying N32.',
            v_sample;
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- §3. PROFILE COMPLETENESS: every template gets exactly one functional profile.
-- Uses ONLY the N31-established deterministic default. No user profile_title
-- is consulted. ON CONFLICT DO NOTHING keeps existing (authoritative) rows.
-- ----------------------------------------------------------------------------
INSERT INTO access_template_profiles (template_id, functional_profile, updated_at_utc)
SELECT t.template_id,
       CASE
           WHEN t.modules @> '[{"moduleId":"admin"}]'::jsonb THEN 'Admin'
           WHEN lower(t.name) LIKE '%respons%' THEN 'Responsável'
           ELSE 'Operador / Controlador'
       END,
       t.updated_at_utc
  FROM access_templates t
  LEFT JOIN access_template_profiles p ON p.template_id = t.template_id
 WHERE p.template_id IS NULL
ON CONFLICT (template_id) DO NOTHING;

-- ----------------------------------------------------------------------------
-- §4. RESTATEMENT OF NON-DESTRUCTIVE BOUNDS (self-documenting, no DDL).
-- Nothing below removes, renames or reshapes:
--   internal_user_access_templates      (legacy mirror, stays until parity)
--   internal_users.profile_title        (legacy mirror, stays until parity)
--   internal_users.modules_override     (dormant N26 column, untouched)
--   job_on_revision.image_asset_id      (dormant N29 column, untouched)
-- Historical migrations N01-N31 are immutable and were not modified.
-- ============================================================================