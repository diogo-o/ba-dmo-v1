-- ============================================================================
-- BA DMO N40 — approved Peso readings protection (owner decision D-10/OD-3
-- Go; refined design in reports/controlo_schema_alignment_prebaseline_audit.md
-- §15 and reports/final_baseline_owner_decision_pack_N39_N40_N42.md).
--
-- N25 (ba_dmo_guard_peso_approved) protects the approved peso_controlos ROW
-- (delete + identity change) but deliberately leaves peso_leituras fully
-- rewritable — the silent-rewrite path an approved baseline must not have
-- (Manual 20:263, 20:477, 20:481, 20:485). This migration adds the intended
-- DB backstop: no INSERT/UPDATE/DELETE on peso_leituras whose parent control
-- has status = 'aprovado'.
--
-- The service pairing shipped in the same change set confines readings DML to
-- the draft-edit path (rascunho/nao_aprovado) and routes
-- submit/approve/reject/reopen/decide through header-only updates that never
-- touch peso_leituras — so this guard NEVER fires on a legitimate flow,
-- including the approval itself (the header UPDATE flips the parent to
-- 'aprovado' in the same transaction, but no readings statement follows it).
-- Reopen (aprovado/nao_aprovado → rascunho, header first) remains compatible:
-- the reopen transaction touches no readings, and any later draft edit runs
-- against a rascunho parent.
--
-- A new sibling function keeps the proven N25 function byte-identical live.
-- Additive and reversible (drop trigger + function).
-- ============================================================================

CREATE OR REPLACE FUNCTION ba_dmo_guard_peso_leituras_approved()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    parent_status text;
BEGIN
    SELECT status INTO parent_status
      FROM peso_controlos
     WHERE peso_controlo_id = COALESCE(NEW.peso_controlo_id, OLD.peso_controlo_id);

    IF parent_status = 'aprovado' THEN
        RAISE EXCEPTION
            'BA DMO: readings of approved peso control % cannot be inserted, updated or deleted; reopen the control first',
            COALESCE(NEW.peso_controlo_id, OLD.peso_controlo_id);
    END IF;

    RETURN COALESCE(NEW, OLD);
END
$$;

DROP TRIGGER IF EXISTS trg_peso_leituras_approved_guard ON peso_leituras;
CREATE TRIGGER trg_peso_leituras_approved_guard
    BEFORE INSERT OR UPDATE OR DELETE ON peso_leituras
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_peso_leituras_approved();