-- ============================================================================
-- BA DMO N37 — remove the obsolete Peso previous-comparison mirror (D-9).
--
-- Canonical comparison history is the immutable peso_controlos.previous_control
-- JSON snapshot. The legacy table has no runtime reader/writer and must be
-- empty before removal. No cascading drop and no data migration are permitted.
-- ============================================================================

DO $$
DECLARE
    has_rows boolean;
BEGIN
    IF to_regclass('public.peso_comparacao_anterior') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.peso_comparacao_anterior)'
           INTO has_rows;

        IF has_rows THEN
            RAISE EXCEPTION
                'N37 blocked: peso_comparacao_anterior contains live rows; preserve and reconcile them before removal.';
        END IF;
    END IF;
END
$$;

DROP TABLE IF EXISTS peso_comparacao_anterior;
