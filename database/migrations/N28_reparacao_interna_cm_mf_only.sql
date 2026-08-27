-- ============================================================================
-- N28 — Reparação Interna CM/MF-only database convergence
--
-- Functional authority: BQ may appear only inside production/reference context;
-- it is never a Reparação Interna recordable tool type. N22 temporarily widened
-- this CHECK to BQ, while Domain/Application/Web now accept only CM/MF.
--
-- Fail closed if unexpected legacy data exists. This migration never deletes or
-- rewrites a repair record; reconciliation would require separate Owner approval.
-- ============================================================================

BEGIN;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM internal_repair_records
        WHERE tool_type NOT IN ('CM', 'MF')) THEN
        RAISE EXCEPTION
            'N28 blocked: internal_repair_records contains a non-CM/MF tool_type';
    END IF;
END
$$;

ALTER TABLE internal_repair_records
    DROP CONSTRAINT IF EXISTS ck_internal_repair_records_type;

ALTER TABLE internal_repair_records
    ADD CONSTRAINT ck_internal_repair_records_type
    CHECK (tool_type IN ('CM', 'MF'))
    NOT VALID;

ALTER TABLE internal_repair_records
    VALIDATE CONSTRAINT ck_internal_repair_records_type;

COMMIT;
