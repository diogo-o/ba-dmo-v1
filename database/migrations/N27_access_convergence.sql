-- ============================================================================
-- BA DMO N27 — final access-model convergence.
-- Additive/forward-only: one-or-more templates per user, three functional
-- profiles, module-only template grants. Legacy internal_users.template_id and
-- modules_override remain physically present for compatibility/auditability.
-- ============================================================================

CREATE TABLE IF NOT EXISTS internal_user_access_templates (
    actor_id        text        NOT NULL REFERENCES internal_users (actor_id),
    template_id     text        NOT NULL REFERENCES access_templates (template_id),
    assigned_at_utc timestamptz NOT NULL DEFAULT now(),
    assigned_by     text        NULL,
    PRIMARY KEY (actor_id, template_id)
);

CREATE INDEX IF NOT EXISTS ix_internal_user_access_templates_template
    ON internal_user_access_templates (template_id, actor_id);

-- Infer the closed profile before legacy capability arrays are removed.
UPDATE internal_users u
SET profile_title = CASE
    WHEN EXISTS (
        SELECT 1 FROM access_templates t
        WHERE t.template_id = u.template_id
          AND t.modules @> '[{"moduleId":"admin"}]'::jsonb
    ) THEN 'Admin'
    WHEN EXISTS (
        SELECT 1
        FROM access_templates t,
             jsonb_array_elements(t.modules) grant_row,
             jsonb_array_elements_text(
                 COALESCE(grant_row->'capabilities', '[]'::jsonb)) capability(value)
        WHERE t.template_id = u.template_id
          AND capability.value IN ('jobon.edit', 'jobon.configure', 'peso.aprovar',
                                   'controlo.review', 'ferramentas.configure')
    ) THEN 'Responsável'
    ELSE 'Operador / Controlador'
END
WHERE profile_title IS NULL
   OR profile_title NOT IN ('Admin', 'Operador / Controlador', 'Responsável');

-- Preserve any legacy override as a private compatibility template before the
-- override is made dormant. This is idempotent and creates no parallel model.
INSERT INTO access_templates (
    template_id, name, modules, active, created_at_utc, created_by, updated_at_utc)
SELECT 'legacy-override-' || substr(md5(u.actor_id), 1, 24),
       'Compatibilidade de ' || u.display_name,
       COALESCE((
           SELECT jsonb_agg(
               jsonb_build_object('moduleId', mapped.module_id, 'capabilities', '[]'::jsonb)
               ORDER BY mapped.module_id)
           FROM (
               SELECT DISTINCT CASE grant_row->>'moduleId'
                   WHEN 'peso' THEN 'controlo'
                   WHEN 'pegamentos' THEN 'controlo'
                   ELSE grant_row->>'moduleId'
               END AS module_id
               FROM jsonb_array_elements(u.modules_override) grant_row
               WHERE grant_row->>'moduleId' IN (
                   'jobon', 'boquilhas', 'controlo', 'peso', 'pegamentos',
                   'ferramentas', 'armazem', 'reparacao_interna',
                   'reparacao_externa', 'tampoes', 'admin')
           ) mapped
       ), '[]'::jsonb),
       TRUE, now(), NULL, now()
FROM internal_users u
WHERE u.modules_override IS NOT NULL
ON CONFLICT (template_id) DO NOTHING;

-- Existing single-template assignments become the first junction assignment.
INSERT INTO internal_user_access_templates (
    actor_id, template_id, assigned_at_utc, assigned_by)
SELECT actor_id, template_id, created_at_utc, NULL
FROM internal_users
ON CONFLICT (actor_id, template_id) DO NOTHING;

INSERT INTO internal_user_access_templates (
    actor_id, template_id, assigned_at_utc, assigned_by)
SELECT actor_id,
       'legacy-override-' || substr(md5(actor_id), 1, 24),
       now(),
       NULL
FROM internal_users
WHERE modules_override IS NOT NULL
ON CONFLICT (actor_id, template_id) DO NOTHING;

-- Templates now assign top-level modules only. Controlo absorbs legacy
-- Peso/Pegamentos grants; História is derived at runtime and is not assignable.
UPDATE access_templates t
SET modules = COALESCE((
        SELECT jsonb_agg(
            jsonb_build_object('moduleId', mapped.module_id, 'capabilities', '[]'::jsonb)
            ORDER BY mapped.module_id)
        FROM (
            SELECT DISTINCT CASE grant_row->>'moduleId'
                WHEN 'peso' THEN 'controlo'
                WHEN 'pegamentos' THEN 'controlo'
                ELSE grant_row->>'moduleId'
            END AS module_id
            FROM jsonb_array_elements(t.modules) grant_row
            WHERE grant_row->>'moduleId' IN (
                'jobon', 'boquilhas', 'controlo', 'peso', 'pegamentos',
                'ferramentas', 'armazem', 'reparacao_interna',
                'reparacao_externa', 'tampoes', 'admin')
        ) mapped
    ), '[]'::jsonb),
    updated_at_utc = now();

UPDATE internal_users
SET modules_override = NULL
WHERE modules_override IS NOT NULL;

ALTER TABLE internal_users
    ALTER COLUMN profile_title SET NOT NULL;

ALTER TABLE internal_users
    DROP CONSTRAINT IF EXISTS ck_internal_users_functional_profile;
ALTER TABLE internal_users
    ADD CONSTRAINT ck_internal_users_functional_profile CHECK (
        profile_title IN ('Admin', 'Operador / Controlador', 'Responsável'));

ALTER TABLE internal_user_access_templates ENABLE ROW LEVEL SECURITY;
DO $$
DECLARE
    role_name text;
BEGIN
    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
            EXECUTE format(
                'REVOKE ALL ON TABLE internal_user_access_templates FROM %I', role_name);
        END IF;
    END LOOP;
END
$$;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE internal_user_access_templates TO ba_dmo_app;

DROP POLICY IF EXISTS internal_user_access_templates_app_access
    ON internal_user_access_templates;
CREATE POLICY internal_user_access_templates_app_access
    ON internal_user_access_templates
    FOR ALL TO ba_dmo_app
    USING (TRUE)
    WITH CHECK (TRUE);
