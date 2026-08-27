-- ============================================================================
-- BA DMO N31 — template-owned functional profile + single effective template.
--
-- Functional target:
--   Aplicações (canonical module catalog) -> Template (title + profile + modules)
--   -> User (one template).
--
-- A template is reusable. Assigning it to a user determines both the user's
-- functional profile and the modules that appear / are authorized. Historical
-- audit rows are untouched.
-- ============================================================================

CREATE TABLE IF NOT EXISTS access_template_profiles (
    template_id         text PRIMARY KEY REFERENCES access_templates (template_id) ON DELETE CASCADE,
    functional_profile  text NOT NULL,
    updated_at_utc      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_access_template_profiles_functional_profile CHECK (
        functional_profile IN ('Admin', 'Operador / Controlador', 'Responsável'))
);

-- Every newly inserted template receives a deterministic initial profile.
-- Admin-only templates start as Admin; all other templates start as Operador
-- and can immediately be changed by the Admin template editor.
CREATE OR REPLACE FUNCTION ba_dmo_ensure_access_template_profile()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO access_template_profiles (template_id, functional_profile, updated_at_utc)
    VALUES (
        NEW.template_id,
        CASE
            WHEN NEW.modules @> '[{"moduleId":"admin"}]'::jsonb THEN 'Admin'
            ELSE 'Operador / Controlador'
        END,
        NEW.updated_at_utc)
    ON CONFLICT (template_id) DO NOTHING;
    RETURN NEW;
END
$$;

DROP TRIGGER IF EXISTS trg_access_templates_ensure_profile ON access_templates;
CREATE TRIGGER trg_access_templates_ensure_profile
    AFTER INSERT ON access_templates
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_ensure_access_template_profile();

-- Backfill existing templates. Prefer the already-stored user functional
-- profile when every user of that primary template agrees; otherwise use the
-- safe deterministic module/name fallback.
INSERT INTO access_template_profiles (template_id, functional_profile, updated_at_utc)
SELECT
    t.template_id,
    COALESCE(
        (
            SELECT MIN(u.profile_title)
            FROM internal_users u
            WHERE u.template_id = t.template_id
              AND u.profile_title IN ('Admin', 'Operador / Controlador', 'Responsável')
            HAVING COUNT(DISTINCT u.profile_title) = 1
        ),
        CASE
            WHEN t.modules @> '[{"moduleId":"admin"}]'::jsonb THEN 'Admin'
            WHEN lower(t.name) LIKE '%respons%' THEN 'Responsável'
            ELSE 'Operador / Controlador'
        END
    ),
    t.updated_at_utc
FROM access_templates t
ON CONFLICT (template_id) DO NOTHING;

-- N27 allowed one-or-more templates per user. The final functional model is
-- one effective template per user, with internal_users.template_id as the
-- compatibility/authority pointer. Collapse any old hybrid assignments to it.
DELETE FROM internal_user_access_templates ut
USING internal_users u
WHERE ut.actor_id = u.actor_id
  AND ut.template_id <> u.template_id;

INSERT INTO internal_user_access_templates (
    actor_id, template_id, assigned_at_utc, assigned_by)
SELECT u.actor_id, u.template_id, now(), NULL
FROM internal_users u
ON CONFLICT (actor_id, template_id) DO NOTHING;

-- One user can no longer accumulate Admin + Operador + Responsável templates.
CREATE UNIQUE INDEX IF NOT EXISTS ux_internal_user_access_templates_actor
    ON internal_user_access_templates (actor_id);

-- Keep the existing internal_users.profile_title compatibility column in sync
-- with the selected template so the current access resolver remains unchanged.
UPDATE internal_users u
SET profile_title = p.functional_profile,
    updated_at_utc = now()
FROM access_template_profiles p
WHERE p.template_id = u.template_id
  AND u.profile_title IS DISTINCT FROM p.functional_profile;

ALTER TABLE access_template_profiles ENABLE ROW LEVEL SECURITY;
DO $$
DECLARE
    role_name text;
BEGIN
    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
            EXECUTE format(
                'REVOKE ALL ON TABLE access_template_profiles FROM %I', role_name);
        END IF;
    END LOOP;
END
$$;

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE access_template_profiles TO ba_dmo_app;

DROP POLICY IF EXISTS access_template_profiles_app_access ON access_template_profiles;
CREATE POLICY access_template_profiles_app_access
    ON access_template_profiles
    FOR ALL TO ba_dmo_app
    USING (TRUE)
    WITH CHECK (TRUE);
