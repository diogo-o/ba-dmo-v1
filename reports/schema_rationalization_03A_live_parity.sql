-- ============================================================================
-- BA DMO — SCHEMA-RAT-03A post-deploy LIVE PARITY CHECKS (READ-ONLY).
--
-- Scope: verify the deployed main (df67e46, "Converge access template and
-- profile authority") against the live Supabase database, per the owner brief:
--   1. direct template vs junction mirror parity;
--   2. template profile vs profile_title mirror parity;
--   3. zero users with multiple effective templates;
--   4. zero templates without a profile;
--   5. Admin profile/module invariants;
--   6. effective access evidence for Admin / Operador-Controlador / Responsável.
--
-- This file is STRICTLY READ-ONLY: SELECT-only, no DDL, no DML, no functions,
-- no transaction control. Every check follows the same convention:
--   * a labelled query returns ZERO rows  -> PASS
--   * a labelled query returns 1+ rows    -> FAIL — rows enumerate the offenders
--   * §6 (behavioural probes) is a REVIEW checklist for the owner, NOT SQL.
--
-- Run as any role able to read application tables (ba_dmo_app, a read-only
-- service role, or the migration owner). RLS is bypassed for owner/service
-- roles; ba_dmo_app passes its permissive policy on these tables.
--
-- Usage (owner):
--   psql "$BA_DMO_DB_CONNECTION_STRING" -f reports/schema_rationalization_03A_live_parity.sql
-- (or paste sections into the Supabase SQL editor — the editor enforces
--  read-only for the authed role only if the role has no write grants).
-- ============================================================================

-- ============================================================================
-- §0  ENVIRONMENT / PROVENANCE (informational — no pass/fail)
-- ============================================================================

-- 0.1  Forward-only runner bookkeeping: every applied file is recorded. The
--      N32 record *proves* the N32 script was executed by the runner (whole
--      script, one transaction, recorded after success). N32 itself is
--      DML-only (guards + profile backfill), so the bookkeeping row + the
--      guard-effect checks below are the only direct evidence it was applied.
SELECT version,
       filename,
       applied_at,
       execution_time_ms
  FROM schema_migrations
 WHERE filename IN ('N31_template_profiles_single_assignment.sql',
                    'N32_access_authority_convergence.sql')
 ORDER BY version;

-- 0.2  Tail of the bookkeeping (last 5 applied files).
SELECT version, filename, applied_at
  FROM schema_migrations
 ORDER BY version DESC
 LIMIT 5;

-- 0.3  Object presence of the converged locus (all four rows must exist).
SELECT 'access_templates'                      AS object,
       to_regclass('public.access_templates')                      AS present
UNION ALL
SELECT 'access_template_profiles',             to_regclass('public.access_template_profiles')
UNION ALL
SELECT 'internal_users',                       to_regclass('public.internal_users')
UNION ALL
SELECT 'internal_user_access_templates',       to_regclass('public.internal_user_access_templates');

-- 0.4  N31 single-assignment guard index present (unique junction per actor).
SELECT indexname, indexdef
  FROM pg_indexes
 WHERE tablename = 'internal_user_access_templates'
   AND indexname = 'ux_internal_user_access_templates_actor';

-- ============================================================================
-- §1  GUARD-EFFECT PARITY (every labelled check returns 0 rows = PASS)
--     These are exactly the states N32 §§1-3 refuse to accept, and the
--     states the N31 unique index makes illegal in a chain-migrated DB.
-- ============================================================================

-- 1.1  N32 §1 guard: ZERO users with MULTIPLE junction (legacy mirror) rows.
SELECT actor_id,
       COUNT(*) AS junction_rows
  FROM internal_user_access_templates
 GROUP BY actor_id
HAVING COUNT(*) > 1
 ORDER BY actor_id;

-- 1.2  N32 §2 guard: ZERO junction rows that DISPUTE the canonical direct FK
--      (internal_users.template_id is the authority; the junction is a
--      one-way mirror).
SELECT u.actor_id,
       u.template_id       AS direct_fk_template,
       ut.template_id      AS junction_template
  FROM internal_users u
  JOIN internal_user_access_templates ut
    ON ut.actor_id = u.actor_id
 WHERE ut.template_id IS DISTINCT FROM u.template_id
 ORDER BY u.actor_id;

-- 1.3  ZERO junction rows with a NULL or dangling template reference
--      (hard FK already makes this impossible; kept as belt-and-braces).
SELECT ut.actor_id, ut.template_id
  FROM internal_user_access_templates ut
  LEFT JOIN access_templates t ON t.template_id = ut.template_id
 WHERE t.template_id IS NULL
 ORDER BY ut.actor_id;

-- 1.4  ZERO users whose junction mirror is MISSING (post-N31 every user was
--      backfilled; the 03A app keeps the mirror in the same transaction, so a
--      healthy DB has one junction row per user). Informational at 03A —
--      becomes the "mirror fully dead" baseline at 03B.
SELECT u.actor_id, u.template_id
  FROM internal_users u
  LEFT JOIN internal_user_access_templates ut
    ON ut.actor_id = u.actor_id
 WHERE ut.actor_id IS NULL
 ORDER BY u.actor_id;

-- 1.5  ZERO templates WITHOUT a functional profile (N31 trigger/backfill +
--      N32 §3 backfill; the N31 table row is NOT NULL).
SELECT t.template_id, t.name, t.modules::text AS modules_json
  FROM access_templates t
  LEFT JOIN access_template_profiles p
    ON p.template_id = t.template_id
 WHERE p.template_id IS NULL
 ORDER BY t.template_id;

-- 1.6  ZERO internal_users whose profile_title MIRROR disagrees with the
--      template-owned functional profile (D-1 authority direction:
--      access_template_profiles -> profile_title, one-way).
SELECT u.actor_id,
       u.display_name,
       u.profile_title         AS mirror_profile_title,
       p.functional_profile    AS template_profile
  FROM internal_users u
  JOIN access_templates t ON t.template_id = u.template_id
  LEFT JOIN access_template_profiles p ON p.template_id = t.template_id
 WHERE u.profile_title IS DISTINCT FROM p.functional_profile
 ORDER BY u.actor_id;

-- 1.7  ZERO values outside the closed functional-profile domain in either
--      store (CHECK constraints should already exclude these).
SELECT 'internal_users.profile_title' AS store, u.actor_id, u.profile_title AS value
  FROM internal_users u
 WHERE u.profile_title IS NULL
    OR u.profile_title NOT IN ('Admin', 'Operador / Controlador', 'Responsável')
UNION ALL
SELECT 'access_template_profiles.functional_profile', p.template_id, p.functional_profile
  FROM access_template_profiles p
 WHERE p.functional_profile NOT IN ('Admin', 'Operador / Controlador', 'Responsável');

-- 1.8  ZERO users with a redundant effective-template route: the direct FK is
--      NOT NULL with a hard FK, so there is no reachable zero-template state;
--      list any user whose template is MISSING or INACTIVE (runtime resolves
--      ACCESS_TEMPLATE_INACTIVE for those — fail-closed, not data corruption,
--      but worth listing for the owner).
SELECT u.actor_id,
       u.display_name,
       u.template_id,
       t.active AS template_active
  FROM internal_users u
  LEFT JOIN access_templates t ON t.template_id = u.template_id
 WHERE t.template_id IS NULL
    OR t.active = FALSE
 ORDER BY u.actor_id;

-- ============================================================================
-- §2  MIRROR COVERAGE SUMMARY (informational)
-- ============================================================================

-- 2.1  Users vs junction-mirror rows (expect equal counts at 03A).
SELECT (SELECT COUNT(*) FROM internal_users)                          AS internal_users,
       (SELECT COUNT(*) FROM internal_user_access_templates)          AS junction_rows,
       (SELECT COUNT(*) FROM access_templates)                        AS templates,
       (SELECT COUNT(*) FROM access_template_profiles)                AS template_profiles;

-- 2.2  Junction provenance: assigned_by usage (NULL = app/system; a value =
--      bootstrap). Informational only — 03B will freeze this table.
SELECT assigned_by, COUNT(*) AS rows
  FROM internal_user_access_templates
 GROUP BY assigned_by
 ORDER BY assigned_by NULLS LAST;

-- ============================================================================
-- §3  ADMIN INVARIANTS (owner brief #5; 0 rows = PASS)
-- ============================================================================

-- 3.1  Admin-profile templates must carry the admin module ONLY (product rule
--      enforced in AdminTemplateService.ValidateProfileModuleRule; the
--      resolver mirrors it at runtime by stripping non-admin modules).
SELECT t.template_id,
       t.name,
       p.functional_profile,
       t.modules::text AS modules_json
  FROM access_templates t
  JOIN access_template_profiles p ON p.template_id = t.template_id
 WHERE p.functional_profile = 'Admin'
   AND (
       NOT t.modules @> '[{"moduleId":"admin"}]'::jsonb
       OR EXISTS (
           SELECT 1
             FROM jsonb_array_elements(t.modules) g
            WHERE g->>'moduleId' <> 'admin')
       )
 ORDER BY t.template_id;

-- 3.2  Operational profiles (Operador / Responsável) must NOT carry the
--      admin module.
SELECT t.template_id,
       t.name,
       p.functional_profile,
       t.modules::text AS modules_json
  FROM access_templates t
  JOIN access_template_profiles p ON p.template_id = t.template_id
 WHERE p.functional_profile <> 'Admin'
   AND t.modules @> '[{"moduleId":"admin"}]'::jsonb
 ORDER BY t.template_id;

-- 3.3  AT LEAST ONE active admin path must survive (GLM-ACC-10 self-lockout):
--      active user + active template + Admin profile + admin module. This is
--      the repository's own CountActiveAdmins definition (DapperAdminRepository
--      / AdminExistsSql). Expect >= 1. This row is informational; a value of 0
--      is a CRITICAL finding.
SELECT COUNT(*) AS active_admin_paths
  FROM internal_users u
  JOIN access_templates t ON t.template_id = u.template_id
  JOIN access_template_profiles p ON p.template_id = t.template_id
 WHERE u.active
   AND p.functional_profile = 'Admin'
   AND t.active
   AND t.modules @> '[{"moduleId":"admin"}]'::jsonb;

-- 3.4  Template module lists must be well-formed JSONB arrays of objects
--      whose only key of interest is moduleId (report parse anomalies).
SELECT t.template_id, t.name, t.modules::text AS modules_json
  FROM access_templates t
 WHERE jsonb_typeof(t.modules) <> 'array'
    OR EXISTS (
        SELECT 1
          FROM jsonb_array_elements(t.modules) g
         WHERE jsonb_typeof(g) <> 'object'
            OR g->>'moduleId' IS NULL
            OR g->>'moduleId' = '')
 ORDER BY t.template_id;

-- ============================================================================
-- §4  EFFECTIVE-ACCESS EVIDENCE (owner brief #4 — review output, not pass/fail)
--     The capability projection happens IN CODE (AccessResolver +
--     ProjectProfileCapabilities at df67e46). These queries supply the raw
--     inputs (user -> template -> modules -> profile) to validate against the
--     expected surface below.
--
--   EXPECTED SURFACE (code-derived, df67e46):
--   * Admin            : modules = [admin]; capabilities = {admin.gerir,
--                        audit.view, audit.export}; no História.
--   * Operador/Control : modules = template modules (minus admin) + controlo
--                        expands to peso+pegamentos + história if any module;
--                        capabilities = jobon.view/jobon.confirmar (if jobon),
--                        controlo.view/controlo.edit/controlo.submit (if controlo);
--                        NO peso.aprovar, NO jobon.edit/configure, NO
--                        ferramentas.configure.
--   * Responsável      : same module derivation; capabilities additionally =
--                        jobon.edit/jobon.configure (if jobon),
--                        ferramentas.configure (if ferramentas),
--                        controlo.review + peso.aprovar (if controlo).
-- ============================================================================

-- 4.1  Every user's effective-access inputs (the review table).
SELECT u.actor_id,
       u.display_name,
       u.active                       AS user_active,
       u.template_id,
       t.name                         AS template_name,
       t.active                       AS template_active,
       COALESCE(p.functional_profile, '<MISSING>') AS functional_profile,
       t.modules::text                AS template_modules_json
  FROM internal_users u
  JOIN access_templates t ON t.template_id = u.template_id
  LEFT JOIN access_template_profiles p ON p.template_id = t.template_id
 ORDER BY p.functional_profile NULLS LAST, u.display_name, u.actor_id;

-- 4.2  Distribution: users/templates per functional profile.
SELECT COALESCE(p.functional_profile, '<MISSING>') AS functional_profile,
       COUNT(DISTINCT u.actor_id)                  AS users,
       COUNT(DISTINCT t.template_id)               AS templates
  FROM internal_users u
  JOIN access_templates t ON t.template_id = u.template_id
  LEFT JOIN access_template_profiles p ON p.template_id = t.template_id
 GROUP BY p.functional_profile
 ORDER BY p.functional_profile NULLS LAST;

-- 4.3  All distinct moduleIds assigned by ACTIVE templates (eyeball against
--      the canonical catalog: jobon, boquilhas, controlo, ferramentas,
--      armazem, reparacao_interna, reparacao_externa, tampoes, admin).
SELECT DISTINCT g->>'moduleId' AS module_id,
       COUNT(DISTINCT t.template_id) AS templates
  FROM access_templates t,
       jsonb_array_elements(t.modules) g
 WHERE t.active
 GROUP BY g->>'moduleId'
 ORDER BY g->>'moduleId';

-- ============================================================================
-- §5  TEMPLATE-REPLACEMENT / PROFILE-PROPAGATION READ-ONLY PRECURSORS
--     (evidence that the propagation write-paths have a consistent surface to
--      act on; the flows themselves are 03A code — repository methods
--      ChangeUserTemplateAsync / UpdateTemplateAsync — and require WRITE
--      probes; see §6 for the owner-executed protocol.)
-- ============================================================================

-- 5.1  Users sharing a template (population affected by a profile update —
--      the profile_title re-derivation target set).
SELECT t.template_id, t.name, COUNT(*) AS users_on_template
  FROM internal_users u
  JOIN access_templates t ON t.template_id = u.template_id
 GROUP BY t.template_id, t.name
HAVING COUNT(*) > 1
 ORDER BY COUNT(*) DESC;

-- 5.2  A sample operational template + its users (read-only fixture for the
--      §6 probes; pick a template with >= 2 users).
SELECT t.template_id, t.name, p.functional_profile
  FROM access_templates t
  JOIN access_template_profiles p ON p.template_id = t.template_id
 WHERE t.active
   AND p.functional_profile <> 'Admin'
 ORDER BY (SELECT COUNT(*) FROM internal_users u WHERE u.template_id = t.template_id) DESC
 LIMIT 5;

-- ============================================================================
-- §6  OWNER-EXECUTED BEHAVIOURAL PROBES (WRITE OPS — NOT PART OF THE
--     READ-ONLY SCRIPT. Run on the DEPLOYED app or against a disposable copy;
--     every probe is immediately reversible, but still a live write.)
--
--   PROBE A — template replacement (D-2):
--     Admin -> Utilizadores -> edit a NON-admin user -> change their template
--     to another active template -> save.
--     VERIFY (read-only afterwards):
--       a. internal_users.template_id now = new template (canonical FK);
--       b. internal_user_access_templates row for that actor follows (mirror);
--       c. the user's modules/landing change to the NEW template (previous
--          access does NOT accumulate) — confirm by logging in as that user.
--     REVERT: change the template back.
--
--   PROBE B — template-profile propagation (D-1):
--     Admin -> Acessos -> edit an operational template -> change its
--     functional profile -> save.
--     VERIFY (read-only afterwards):
--       a. access_template_profiles.functional_profile = new value (authority);
--       b. internal_users.profile_title re-derived for EVERY user of that
--          template (mirror; updated_at_utc of users untouched);
--       c. a user of the template now resolves the new profile at login
--          (same-template users' effective access changes together).
--     REVERT: set the profile back.
--
--   PROBE C — user create (D-2):
--     Admin -> Utilizadores -> create a user with one template.
--     VERIFY: internal_users.template_id AND the junction mirror row AND
--     profile_title all equal the template's profile in the same transaction.
--     REVERT: deactivate the created user.
-- ============================================================================
-- End of read-only parity script.
-- ============================================================================