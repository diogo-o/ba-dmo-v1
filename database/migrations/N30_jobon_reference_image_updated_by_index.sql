-- N30 — covering index for the article_reference_images.updated_by FK.
-- Additive performance convergence identified by the post-N29 advisor check.

BEGIN;

CREATE INDEX IF NOT EXISTS ix_article_reference_images_updated_by
    ON article_reference_images (updated_by);

COMMIT;
