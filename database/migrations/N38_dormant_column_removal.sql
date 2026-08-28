-- ============================================================================
-- BA DMO N38 — remove dormant legacy columns approved by D-11.
--
-- Authority remains access_templates/modules plus article_reference_images.
-- Both legacy columns must be NULL before removal. No cascading drop or data
-- reinterpretation is permitted. N33's internal_users column grants are
-- re-issued for the seven surviving canonical columns.
-- ============================================================================

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM internal_users WHERE modules_override IS NOT NULL) THEN
        RAISE EXCEPTION
            'N38 blocked: internal_users.modules_override contains live data.';
    END IF;

    IF EXISTS (SELECT 1 FROM job_on_revision WHERE image_asset_id IS NOT NULL) THEN
        RAISE EXCEPTION
            'N38 blocked: job_on_revision.image_asset_id contains live data.';
    END IF;
END
$$;

ALTER TABLE internal_users
    DROP COLUMN IF EXISTS modules_override;

ALTER TABLE job_on_revision
    DROP COLUMN IF EXISTS image_asset_id;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_app') THEN
        EXECUTE 'REVOKE SELECT, INSERT, UPDATE ON internal_users FROM ba_dmo_app';
        EXECUTE 'GRANT SELECT (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc) ON internal_users TO ba_dmo_app';
        EXECUTE 'GRANT INSERT (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc) ON internal_users TO ba_dmo_app';
        EXECUTE 'GRANT UPDATE (actor_id, auth_user_id, template_id, display_name, active, created_at_utc, updated_at_utc) ON internal_users TO ba_dmo_app';
    END IF;
END
$$;
