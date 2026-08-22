-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N22_reparacao_interna_context.sql — Reparação Interna production-context +
--   no-hard-block refinement (OWNER DECISION, R009).
-- Owner decisions that supersede earlier wording for this module:
--   1. BQ is a THIRD recordable type (CM | MF | BQ) in Reparação Interna.
--   2. Repeated individual numbers are VALID occurrences (5,5,7 = 3 rows);
--      never deduplicate or unique-constrain the individual number.
--   3. NO operational hard blocks: the record persists what actually happened.
--      Automatic production context is assistance; absence/mismatch of context
--      must NOT prevent recording (context may be unknown + output as empty).
--   4. Persist the exact historical production context used at save time so
--      history never depends on current_revision_id: job_on_revision_id anchor
--      + production/reference snapshot (GAP 2 fix).
-- Idempotent, forward-only, additive. Existing RI data is preserved; existing
-- columns are not dropped or renamed.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. Widen tool_type to include BQ (CM | MF | BQ). Drop/recreate the CHECK so it
--    is forward-only and preserves existing rows (CM/MF remain valid).
-- ----------------------------------------------------------------------------
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_internal_repair_records_type') THEN
        ALTER TABLE internal_repair_records DROP CONSTRAINT ck_internal_repair_records_type;
    END IF;
END
$$;

ALTER TABLE internal_repair_records
    ADD CONSTRAINT ck_internal_repair_records_type
    CHECK (tool_type IN ('CM', 'MF', 'BQ'));

-- ----------------------------------------------------------------------------
-- 2. Historical production-context snapshot (GAP 2). Additive columns kept
--    NULL-able so legacy rows stay valid and readable. job_on_id remains the
--    existing logical Job On link (plain uuid, as before); we add the exact
--    immutable-revision anchor + production/reference/lot facts captured at
--    save time. lot_id is a logical link (uuid) — enrichment only, no hard
--    block — and intentionally has NO foreign key because the effective lot
--    for CM/MF comes from Ferramentas tool_lotes while BQ derives from the
--    Job On BQ component (also tool_lotes) or the Boquilhas operational lot.
-- ----------------------------------------------------------------------------
ALTER TABLE internal_repair_records
    ADD COLUMN IF NOT EXISTS job_on_revision_id uuid,
    ADD COLUMN IF NOT EXISTS production_code    text,
    ADD COLUMN IF NOT EXISTS reference          text,
    ADD COLUMN IF NOT EXISTS lot_id             uuid;

CREATE INDEX IF NOT EXISTS ix_internal_repair_records_revision
    ON internal_repair_records (job_on_revision_id);

-- Historical attribution anchor to the immutable Job On revision (TD-18,
-- the established pattern already used/proven by Peso and Pegamentos, R005/R006).
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_internal_repair_records_revision') THEN
        ALTER TABLE internal_repair_records
            ADD CONSTRAINT fk_internal_repair_records_revision
            FOREIGN KEY (job_on_revision_id)
            REFERENCES job_on_revision (job_on_revision_id);
    END IF;
END
$$;