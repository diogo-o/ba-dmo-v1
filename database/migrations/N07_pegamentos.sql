-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N07_pegamentos.sql — Pegamentos (module owner: Pegamentos; tables pegamento_*).
-- Authority: 06_DATA §3.4, DS-05 (mandatory Job On context, no fallback),
--            TD-32 (measurement rules), modules/04_PEGAMENTOS_SPEC.
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- pegamento_controlos — measurement controls.
-- job_on_id + job_on_revision_id MANDATORY (DS-05): referência, produção,
-- máquina and the CM/BQ/MF instances/lots are INHERITED from the Job On —
-- no alternative selection, no fallback, no local base. MF comes from its
-- own domain through the Job On. Tolerance ±0.20 is CONFIGURABLE DATA, not
-- a hard block (warnings never prevent recording — GLM-CORE-01).
--
-- HISTORICAL FERRAMENTA ATTRIBUTION (owner clarification + TD-18):
-- the record is pinned to job_on_revision_id — an IMMUTABLE revision whose
-- job_on_component rows identify every Ferramenta involved (CM/BQ/MF via
-- source_tool_id/source_lot_id, with reference/lot snapshots). A control
-- spans several tools, so attribution uses the revision anchor rather than
-- one duplicated direct tool FK (TD-26: no duplicated attributes). Later
-- Job On edits/tool substitutions create a NEW revision; this control
-- remains attributable to the Ferramentas of the pinned revision. The
-- job_on_id column alone is context/grouping and is never used for tool
-- attribution. The cm/bq/mf snapshots below are read-only display context
-- captured from that same revision.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS pegamento_controlos (
    pegamento_controlo_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    job_on_id             uuid        NOT NULL REFERENCES job_on (job_on_id),
    job_on_revision_id    uuid        NOT NULL REFERENCES job_on_revision (job_on_revision_id),
    reference_snapshot    jsonb       NULL,
    production_code       text        NOT NULL,
    machine_code          text        NOT NULL,
    cm_snapshot           jsonb       NULL,
    bq_snapshot           jsonb       NULL,
    mf_snapshot           jsonb       NULL,
    nominal_average       numeric(18,4) NULL,
    tolerance             numeric(6,3) NOT NULL DEFAULT 0.20,
    status                text        NOT NULL DEFAULT 'aberto',
    created_at_utc        timestamptz NOT NULL DEFAULT now(),
    created_by            text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_pegamento_controlos_tolerance CHECK (tolerance >= 0)
);

CREATE INDEX IF NOT EXISTS ix_pegamento_controlos_job_on ON pegamento_controlos (job_on_id);
CREATE INDEX IF NOT EXISTS ix_pegamento_controlos_job_on_revision ON pegamento_controlos (job_on_revision_id);
CREATE INDEX IF NOT EXISTS ix_pegamento_controlos_production ON pegamento_controlos (production_code, machine_code);

-- ----------------------------------------------------------------------------
-- pegamento_medicoes — one row per measurement (append-only facts).
-- Costura / Contra costura raw values; ovalização (c − n) and média ((c+n)/2)
-- are deterministic C# calculations presented from these facts (TD-32).
-- Old records preserve their values (GLM-DATA-04.1).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS pegamento_medicoes (
    pegamento_medicao_id  uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    pegamento_controlo_id uuid        NOT NULL REFERENCES pegamento_controlos (pegamento_controlo_id),
    component_key         text        NOT NULL,
    costura               numeric(18,4) NOT NULL,
    contra_costura        numeric(18,4) NOT NULL,
    measured_at_utc       timestamptz NOT NULL DEFAULT now(),
    actor_id              text        NULL REFERENCES internal_users (actor_id)
);

CREATE INDEX IF NOT EXISTS ix_pegamento_medicoes_controlo
    ON pegamento_medicoes (pegamento_controlo_id);

DROP TRIGGER IF EXISTS trg_pegamento_medicoes_append_only ON pegamento_medicoes;
CREATE TRIGGER trg_pegamento_medicoes_append_only
    BEFORE UPDATE OR DELETE ON pegamento_medicoes
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();
