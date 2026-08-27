-- ============================================================================
-- BA DMO N33 — legacy access mirror quiescence (SCHEMA-RAT-03B).
--
-- After N32 (SCHEMA-RAT-03A, D-1/D-2), runtime identity/authorization reads
-- exclusively:
--
--   internal_users.template_id
--      -> access_templates
--      -> access_template_profiles.functional_profile   (functional authority)
--
-- while two legacy MIRROR structures were still physically present and still
-- written by the Application as one-way compatibility mirrors:
--
--   internal_user_access_templates   (actor_id, template_id junction)
--   internal_users.profile_title     (user-level profile mirror)
--
-- N33 RETIRES both mirrors as RUNTIME objects (SCHEMA-RAT-03B): after this
-- migration the Application has zero runtime writers and zero runtime readers
-- of either structure. Both structures remain PHYSICALLY PRESENT here —
-- no drops, no renames, no data rewrites — for a later, separately designed
-- destructive removal phase (outside 03B).
--
-- What this migration does (idempotent, non-destructive, forward-only):
--   1. Relaxes internal_users.profile_title to NULLABLE (N27 made it NOT
--      NULL). The mirror is retired, so new user rows no longer carry a
--      value; a NULL profile_title is unambiguous ("mirror retired" — the
--      template-owned profile is the only source), never a missing value.
--      Existing rows keep their fossil values untouched. The N27 CHECK
--      constraint (ck_internal_users_functional_profile) is NULL-tolerant and
--      therefore inert on NULL rows — it is not modified.
--   2. REVOKEs ALL privileges on internal_user_access_templates FROM
--      ba_dmo_app: no runtime reader or writer can touch the junction anymore;
--      any residual access fails LOUDLY (permission denied) instead of
--      silently maintaining the mirror. Migration-owner / Supabase-admin
--      roles are unaffected, so future migrations and the N31/N32-style
--      guards still run.
--   3. Severs ba_dmo_app access to internal_users.profile_title: ba_dmo_app
--      held TABLE-LEVEL SELECT/INSERT/UPDATE on internal_users, which by
--      PostgreSQL semantics implies every column — including the retired
--      mirror; table-level INSERT would also still permit writing
--      profile_title. N33 therefore revokes those three table-level grants
--      and re-issues them at COLUMN level for every current internal_users
--      column EXCEPT profile_title (explicit list, no dynamic discovery;
--      DELETE untouched). Any residual read/write/insert of the retired
--      column fails loudly.
--
-- Rules honoured (SCHEMA-RAT-03B owner brief):
--   * neither mirror structure is dropped, renamed or rewritten here;
--   * module-access authority (D-6), Job On, Armazém and Reparação fixes are
--     untouched by design;
--   * historical migrations N01-N32 are immutable and were not modified.
--
-- Conventions: idempotent, guarded, forward-only; executed WHOLE by the
-- Npgsql migration runner inside its own per-script transaction (no explicit
-- BEGIN/COMMIT here — N28/N29/N30 transaction-control debt is not repeated).
-- ============================================================================

-- ----------------------------------------------------------------------------
-- §1. profile_title becomes NULLABLE (mirror retired; the column itself stays
-- physically present with its fossil values; the CHECK constraint is
-- NULL-tolerant and remains inert on NULL).
-- ----------------------------------------------------------------------------
ALTER TABLE internal_users
    ALTER COLUMN profile_title DROP NOT NULL;

-- ----------------------------------------------------------------------------
-- §2. Junction kill switch: ba_dmo_app loses ALL privileges on
-- internal_user_access_templates. The role may not exist on every target
-- database — the guarded DO block keeps the migration idempotent anywhere.
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_app') THEN
        EXECUTE 'REVOKE ALL PRIVILEGES ON TABLE internal_user_access_templates FROM ba_dmo_app';
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- §3. profile_title kill switch (privilege REFACTOR). The original
-- column-level REVOKE approach cannot work here: ba_dmo_app holds
-- TABLE-LEVEL SELECT/INSERT/UPDATE on internal_users, and a table-level
-- grant implies access to EVERY column — including the retired mirror —
-- so has_column_privilege('ba_dmo_app', 'internal_users', 'profile_title',
-- 'SELECT'|'UPDATE') stays TRUE and table-level INSERT still permits
-- writing profile_title. The correction:
--   3a. REVOKE the table-level SELECT/INSERT/UPDATE from ba_dmo_app;
--   3b. GRANT the same three privileges back at COLUMN level for every
--       current internal_users column EXCEPT profile_title — explicit
--       column list, no dynamic discovery, no profile_title grant through
--       any path;
--   3c. DELETE is untouched: it stays exactly as it currently exists
--       (table-level, unchanged).
-- After this block has_column_privilege('ba_dmo_app', 'internal_users',
-- 'profile_title', 'SELECT'|'UPDATE'|'INSERT') is FALSE for all three for
-- ba_dmo_app, while canonical columns (template_id, display_name, active,
-- …) keep the intended privileges. Guarded the same way as §2.
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_app') THEN
        EXECUTE 'REVOKE SELECT, INSERT, UPDATE ON internal_users FROM ba_dmo_app';
        EXECUTE 'GRANT SELECT (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc, modules_override) ON internal_users TO ba_dmo_app';
        EXECUTE 'GRANT INSERT (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc, modules_override) ON internal_users TO ba_dmo_app';
        EXECUTE 'GRANT UPDATE (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc, modules_override) ON internal_users TO ba_dmo_app';
    END IF;
END
$$;

-- ----------------------------------------------------------------------------
-- §4. RESTATEMENT OF NON-DESTRUCTIVE BOUNDS (self-documenting, no DDL).
-- Nothing in this migration removes, renames or reshapes:
--   internal_user_access_templates   (legacy mirror — physically present, dead)
--   internal_users.profile_title     (legacy mirror — physically present, dead)
--   internal_users.modules_override  (dormant N26 column, untouched)
-- The destructive removal phase is designed SEPARATELY, only after 03B is
-- deployed and parity-validated; N33 is exclusively the quiescence step
-- (relax NOT NULL + revoke runtime privileges).
-- Historical migrations N01-N32 are immutable and were not modified.
-- ============================================================================