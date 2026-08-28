-- ============================================================================
-- BA DMO N42 — retire the dormant occurrence twin tool_check_occurrences
-- (owner decision OD-6 / PA-01: REMOVE; N42 design in
-- reports/post_codex_database_rationalization_plan.md §5.1/T4 + §13.9).
--
-- tool_check_occurrences (N04) is a schema-only duplicate of the live
-- materialization job_on_verification_occurrence (N05): zero writers and zero
-- readers in src (the only reader was removed by Queue A), its CHECKs and
-- indexes are dead, and the N25 completed-state rule already mirrors its
-- semantics on the N05 sibling. Functional authority is the Job-On-level
-- occurrence surface (Manual 30:312, 10:275-280, 30:660).
--
-- Removal is data-checked: any live row stops the migration (fail-closed and
-- reported) — never a silent discard. No cascading drop, no data
-- reinterpretation.
-- ============================================================================

DO $$
DECLARE
    has_rows boolean;
BEGIN
    IF to_regclass('public.tool_check_occurrences') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.tool_check_occurrences)'
           INTO has_rows;

        IF has_rows THEN
            RAISE EXCEPTION
                'N42 blocked: tool_check_occurrences contains live rows; preserve and reconcile them before removal.';
        END IF;
    END IF;
END
$$;

DROP TABLE IF EXISTS tool_check_occurrences;