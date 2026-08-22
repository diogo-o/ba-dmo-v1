-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N21_tampoes_machines.sql — Tampões multi-machine + comments (R008).
-- Authority: OWNER DECISION — Tampões uses a dedicated record/detail sheet with
--            multi-machine assignment (B1–C3) and persisted comments; a Tampões
--            configuration/record is NEVER duplicated per machine.
-- Normalized relationship (not CSV). Comment/association events are append-only
-- so history is never silently lost (actor + timestamp). Server-side allowed
-- machines: B1, B2, B3, C1, C2, C3.
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

-- One configuration → zero/one/many machines (normalized; no CSV, no per-machine copy).
CREATE TABLE IF NOT EXISTS tampao_configuration_machines (
    tampao_configuration_id uuid NOT NULL REFERENCES tampao_configurations (tampao_configuration_id),
    machine                text NOT NULL,
    PRIMARY KEY (tampao_configuration_id, machine),
    CONSTRAINT ck_tampao_configuration_machines_machine CHECK
        (machine IN ('B1', 'B2', 'B3', 'C1', 'C2', 'C3'))
);

CREATE INDEX IF NOT EXISTS ix_tampao_configuration_machines_machine
    ON tampao_configuration_machines (machine);

-- Append-only comments/notes per configuration (latest = current; older preserved).
CREATE TABLE IF NOT EXISTS tampao_configuration_notes (
    tampao_configuration_note_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tampao_configuration_id      uuid        NOT NULL REFERENCES tampao_configurations (tampao_configuration_id),
    note                        text        NOT NULL,
    actor_id                    text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc             timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_tampao_configuration_notes_config
    ON tampao_configuration_notes (tampao_configuration_id, occurred_at_utc);

DROP TRIGGER IF EXISTS trg_tampao_configuration_notes_append_only ON tampao_configuration_notes;
CREATE TRIGGER trg_tampao_configuration_notes_append_only
    BEFORE UPDATE OR DELETE ON tampao_configuration_notes
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- Audit trail of machine association changes (added/removed; never silent loss).
CREATE TABLE IF NOT EXISTS tampao_configuration_machine_event (
    tampao_configuration_machine_event_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tampao_configuration_id               uuid        NOT NULL REFERENCES tampao_configurations (tampao_configuration_id),
    machine                               text        NOT NULL,
    action                                text        NOT NULL,
    actor_id                              text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc                       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tampao_configuration_machine_event_action CHECK (action IN ('added', 'removed')),
    CONSTRAINT ck_tampao_configuration_machine_event_machine CHECK
        (machine IN ('B1', 'B2', 'B3', 'C1', 'C2', 'C3'))
);

CREATE INDEX IF NOT EXISTS ix_tampao_configuration_machine_event_config
    ON tampao_configuration_machine_event (tampao_configuration_id, occurred_at_utc);

DROP TRIGGER IF EXISTS trg_tampao_configuration_machine_event_append_only ON tampao_configuration_machine_event;
CREATE TRIGGER trg_tampao_configuration_machine_event_append_only
    BEFORE UPDATE OR DELETE ON tampao_configuration_machine_event
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();