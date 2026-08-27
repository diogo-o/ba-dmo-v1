-- ============================================================================
-- N29_jobon_reference_images.sql
-- Converges the Job On article image from per-revision metadata to one current
-- association owned by the normalized master Article/Reference context.
--
-- Legacy job_on_revision.image_asset_id remains dormant and is not dropped.
-- Any legacy current association is migrated only when its Reference and file
-- name are safe and unambiguous; otherwise the migration fails closed.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS article_reference_images (
    reference_code  text        PRIMARY KEY,
    image_asset_id  text        NOT NULL,
    updated_by      text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_article_reference_images_reference CHECK (
        reference_code <> ''
        AND reference_code = upper(btrim(reference_code))),
    CONSTRAINT ck_article_reference_images_asset CHECK (
        image_asset_id <> ''
        AND image_asset_id = btrim(image_asset_id)
        AND image_asset_id NOT LIKE '%/%'
        AND position(chr(92) in image_asset_id) = 0
        AND image_asset_id NOT LIKE '%..%'
        AND image_asset_id ~* '\.(jpe?g|png|gif|webp|bmp)$')
);

-- A legacy image without a readable Reference or with a path-like/unsupported
-- asset cannot be promoted safely to master ownership.
DO $$
DECLARE
    incompatible_count integer;
BEGIN
    SELECT count(*)
      INTO incompatible_count
      FROM job_on j
      JOIN job_on_revision r
        ON r.job_on_revision_id = j.current_revision_id
     WHERE r.image_asset_id IS NOT NULL
       AND (
           nullif(btrim(
               CASE
                   WHEN jsonb_typeof(r.reference_snapshot) = 'string'
                       THEN r.reference_snapshot #>> '{}'
                   WHEN jsonb_typeof(r.reference_snapshot) = 'object'
                       THEN coalesce(
                           r.reference_snapshot ->> 'article_reference',
                           r.reference_snapshot ->> 'reference',
                           r.reference_snapshot ->> 'code',
                           r.reference_snapshot ->> 'value')
                   ELSE NULL
               END), '') IS NULL
           OR r.image_asset_id LIKE '%/%'
           OR position(chr(92) in r.image_asset_id) > 0
           OR r.image_asset_id LIKE '%..%'
           OR r.image_asset_id !~* '\.(jpe?g|png|gif|webp|bmp)$'
       );

    IF incompatible_count > 0 THEN
        RAISE EXCEPTION
            'N29 cannot migrate % legacy Job On image association(s): missing Reference or unsafe image file name',
            incompatible_count;
    END IF;
END $$;

-- The same Reference cannot be promoted from conflicting current images.
DO $$
DECLARE
    conflicting_count integer;
BEGIN
    WITH legacy AS (
        SELECT upper(btrim(
                   CASE
                       WHEN jsonb_typeof(r.reference_snapshot) = 'string'
                           THEN r.reference_snapshot #>> '{}'
                       ELSE coalesce(
                           r.reference_snapshot ->> 'article_reference',
                           r.reference_snapshot ->> 'reference',
                           r.reference_snapshot ->> 'code',
                           r.reference_snapshot ->> 'value')
                   END)) AS reference_code,
               r.image_asset_id
          FROM job_on j
          JOIN job_on_revision r
            ON r.job_on_revision_id = j.current_revision_id
         WHERE r.image_asset_id IS NOT NULL
    )
    SELECT count(*)
      INTO conflicting_count
      FROM (
          SELECT reference_code
            FROM legacy
           GROUP BY reference_code
          HAVING count(DISTINCT image_asset_id) > 1
      ) conflicts;

    IF conflicting_count > 0 THEN
        RAISE EXCEPTION
            'N29 cannot migrate % Article/Reference(s) with conflicting legacy images',
            conflicting_count;
    END IF;
END $$;

WITH legacy AS (
    SELECT DISTINCT ON (reference_code)
           reference_code,
           image_asset_id,
           saved_by,
           saved_at_utc
      FROM (
          SELECT upper(btrim(
                     CASE
                         WHEN jsonb_typeof(r.reference_snapshot) = 'string'
                             THEN r.reference_snapshot #>> '{}'
                         ELSE coalesce(
                             r.reference_snapshot ->> 'article_reference',
                             r.reference_snapshot ->> 'reference',
                             r.reference_snapshot ->> 'code',
                             r.reference_snapshot ->> 'value')
                     END)) AS reference_code,
                 r.image_asset_id,
                 r.saved_by,
                 r.saved_at_utc
            FROM job_on j
            JOIN job_on_revision r
              ON r.job_on_revision_id = j.current_revision_id
           WHERE r.image_asset_id IS NOT NULL
      ) candidates
     ORDER BY reference_code, saved_at_utc DESC
)
INSERT INTO article_reference_images (
    reference_code, image_asset_id, updated_by, updated_at_utc)
SELECT reference_code, image_asset_id, saved_by, saved_at_utc
  FROM legacy
ON CONFLICT (reference_code) DO NOTHING;

ALTER TABLE article_reference_images ENABLE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ba_dmo_app') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE
            ON article_reference_images TO ba_dmo_app;

        DROP POLICY IF EXISTS ba_dmo_app_access ON article_reference_images;
        CREATE POLICY ba_dmo_app_access
            ON article_reference_images
            FOR ALL
            TO ba_dmo_app
            USING (true)
            WITH CHECK (true);
    END IF;
END $$;

COMMIT;
