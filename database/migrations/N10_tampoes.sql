-- ============================================================================
-- BA DMO fresh-build migration family (Plan-V3 BT-08, 06_DATA §2).
-- N10_tampoes.sql — Tampões (module owner: Tampões; tables tampao_*).
-- Authority: 06_DATA §3.9, modules/10_TAMPOES_SPEC (GLM-TP-04/05).
-- Tampões are NOT associated with tools/references (USER CONFIRMED,
-- GLM-TP-01); no individual numbers in V1.
-- Idempotent, forward-only. Executed WHOLE by the Npgsql migration runner.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- tampao_field_defs — configurable comparable fields (initially Diâmetro mm
-- and Profundidade/Calote mm): name, unit, precision, order, active.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS tampao_field_defs (
    tampao_field_def_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    field_name          text        NOT NULL UNIQUE,
    unit                text        NULL,
    precision_digits    integer     NULL,
    display_order       integer     NOT NULL DEFAULT 0,
    active              boolean     NOT NULL DEFAULT TRUE,
    created_at_utc      timestamptz NOT NULL DEFAULT now(),
    updated_at_utc      timestamptz NOT NULL DEFAULT now()
);

-- ----------------------------------------------------------------------------
-- tampao_field_values — normalized available values per field (never
-- variants like 4 / 4.0 / 4,00). Deactivating removes the value from new
-- dropdowns without deleting configurations/history (GLM-TP-05.5).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS tampao_field_values (
    tampao_field_value_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tampao_field_def_id   uuid        NOT NULL REFERENCES tampao_field_defs (tampao_field_def_id),
    value_numeric         numeric(18,4) NOT NULL,
    value_label           text        NOT NULL,
    display_order         integer     NOT NULL DEFAULT 0,
    active                boolean     NOT NULL DEFAULT TRUE,
    created_at_utc        timestamptz NOT NULL DEFAULT now(),
    updated_at_utc        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_tampao_field_values UNIQUE (tampao_field_def_id, value_numeric)
);

CREATE INDEX IF NOT EXISTS ix_tampao_field_values_field
    ON tampao_field_values (tampao_field_def_id, active, value_numeric);

-- ----------------------------------------------------------------------------
-- tampao_configurations — combination of values with its own id; UNIQUE
-- values. Existing destination configurations are REUSED by id
-- (GLM-TP-05.3); deactivation/obsolescence never rewrites history.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS tampao_configurations (
    tampao_configuration_id uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    values_json           jsonb       NOT NULL,
    active                boolean     NOT NULL DEFAULT TRUE,
    created_at_utc        timestamptz NOT NULL DEFAULT now(),
    created_by            text        NULL REFERENCES internal_users (actor_id),
    CONSTRAINT uq_tampao_configurations_values UNIQUE (values_json)
);

-- ----------------------------------------------------------------------------
-- tampao_saldos — exactly two balances per configuration: Enchidos and
-- Por encher, both >= 0. "Maquinado" does NOT exist as a third state
-- (GLM-TP-04). Balances only change through recorded movements
-- (GLM-DATA-04.4).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS tampao_saldos (
    tampao_saldo_id       uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tampao_configuration_id uuid      NOT NULL UNIQUE REFERENCES tampao_configurations (tampao_configuration_id),
    enchidos              integer     NOT NULL DEFAULT 0,
    por_encher            integer     NOT NULL DEFAULT 0,
    updated_at_utc        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tampao_saldos_enchidos CHECK (enchidos >= 0),
    CONSTRAINT ck_tampao_saldos_por_encher CHECK (por_encher >= 0)
);

-- ----------------------------------------------------------------------------
-- tampao_movements — Adicionar / Remover / Alterar estado / Alterar
-- configuração. Every movement keeps origin/destination, quantity,
-- before/after balances, operator and timestamp; transformations apply
-- origem+destino in ONE transaction (Application layer, GLM-DATA-05).
-- Append-only; no retroactive edits (GLM-TP-05.3).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS tampao_movements (
    tampao_movement_id        uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    movement_type             text        NOT NULL,
    origin_configuration_id   uuid        NULL REFERENCES tampao_configurations (tampao_configuration_id),
    destination_configuration_id uuid     NULL REFERENCES tampao_configurations (tampao_configuration_id),
    qty                       integer     NOT NULL,
    balances_before           jsonb       NULL,
    balances_after            jsonb       NULL,
    actor_id                  text        NULL REFERENCES internal_users (actor_id),
    occurred_at_utc           timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tampao_movements_type CHECK (
        movement_type IN ('adicionar', 'remover', 'alterar_estado', 'alterar_configuracao')),
    CONSTRAINT ck_tampao_movements_qty CHECK (qty >= 1)
);

CREATE INDEX IF NOT EXISTS ix_tampao_movements_origin ON tampao_movements (origin_configuration_id);
CREATE INDEX IF NOT EXISTS ix_tampao_movements_occurred ON tampao_movements (occurred_at_utc);

DROP TRIGGER IF EXISTS trg_tampao_movements_append_only ON tampao_movements;
CREATE TRIGGER trg_tampao_movements_append_only
    BEFORE UPDATE OR DELETE ON tampao_movements
    FOR EACH ROW
    EXECUTE FUNCTION ba_dmo_guard_append_only();

-- ----------------------------------------------------------------------------
-- tampao_planos — planned needs (planear ≠ reservar, GLM-TP-05.4):
-- configuration, quantity, expected date, optional Job On/production link
-- only when unambiguous. Planning never adds/removes/reserves stock;
-- cancelling a plan does not touch balances (canceled fact row preserved).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS tampao_planos (
    tampao_plano_id       uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tampao_configuration_id uuid      NOT NULL REFERENCES tampao_configurations (tampao_configuration_id),
    planned_qty            integer     NOT NULL,
    planned_for_date      date        NULL,
    job_on_id             uuid        NULL,
    production_code       text        NULL,
    notes                 text        NULL,
    canceled              boolean     NOT NULL DEFAULT FALSE,
    created_at_utc        timestamptz NOT NULL DEFAULT now(),
    created_by            text        NULL REFERENCES internal_users (actor_id),
    updated_at_utc        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_tampao_planos_qty CHECK (planned_qty >= 1)
);

CREATE INDEX IF NOT EXISTS ix_tampao_planos_configuration ON tampao_planos (tampao_configuration_id);
CREATE INDEX IF NOT EXISTS ix_tampao_planos_date ON tampao_planos (planned_for_date);
