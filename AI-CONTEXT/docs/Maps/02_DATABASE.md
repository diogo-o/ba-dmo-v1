# BA DMO — Database Technical Map

Pure technical database map (MAP-02R2).
Scope: what exists in the **current database schema** and where it is defined.

- This map states only database facts: tables, columns, types, defaults, PKs, FKs, UNIQUE / CHECK constraints, indexes, functions, triggers, roles/grants, RLS/policies, direct database relationships, and exact SQL source locations.
- It does **not** explain Domain behavior, Design/SOT, business rules unless literally encoded as SQL constraints, application behavior, Dapper/repository behavior, ownership, intent, reconciliation, gaps, or migration chronology. Those belong to each layer's own map/manual.
- Primary SQL source authority: `database\consolidated_clean_install.sql` (full current schema). Per-object source-location pointers also name the introducing migration file; migration chronology is mapped separately in `03_MIGRATIONS.md`.

---

## Navigation Index

- [1. Purpose](#1-purpose)
- [2. Database Source Structure](#2-database-source-structure)
- [3. Global Database Inventory](#3-global-database-inventory)
- [4. Shared / Identity / Access](#4-shared--identity--access)
- [5. Job On](#5-job-on)
- [6. Controlo](#6-controlo)
- [7. Ferramentas](#7-ferramentas)
- [8. Armazém](#8-armazém)
- [9. Boquilhas](#9-boquilhas)
- [10. Reparação Interna](#10-reparação-interna)
- [11. Peso](#11-peso)
- [12. Pegamentos](#12-pegamentos)
- [13. Reparação Externa](#13-reparação-externa)
- [14. Tampões](#14-tampões)
- [15. Modules With No Dedicated Database Tables](#15-modules-with-no-dedicated-database-tables)
- [Primary Key Index](#primary-key-index)
- [Foreign Key Index](#foreign-key-index)
- [Identifier-like Columns Without FK Constraints](#identifier-like-columns-without-fk-constraints)
- [Unique Constraints](#unique-constraints)
- [Check Constraints](#check-constraints)
- [Indexes](#indexes)
- [Functions and Triggers](#functions-and-triggers)
- [RLS / Policies](#rls--policies)
- [Database Relationships](#database-relationships)
- [Cross-Area Database Relationships](#cross-area-database-relationships)
- [Database Objects / Special-Purpose Tables](#database-objects--special-purpose-tables)
- [Sources Verified](#sources-verified)

---

## 1. Purpose

Answer: *"Which database object am I looking for, what is it, what columns/types/constraints does it contain, which objects reference it directly, and which SQL file do I open?"*

- `02_DATABASE.md` is a global transversal inventory of the current database schema: tables, columns, PK/FK/UNIQUE/CHECK constraints, indexes, functions, triggers, roles/grants, RLS/policies, direct database relationships, and exact SQL source locations.
- It is technical navigation only. Domain, Design/SOT, Application, Dapper/repository and test behavior belong to their own layer maps.

---

## 2. Database Source Structure

```
database\
├─ consolidated_clean_install.sql   ← current full-schema SQL source
└─ migrations\
   ├─ N01_identity.sql
   ├─ N02_catalog.sql
   ├─ N03_bq.sql
   ├─ N04_ferramentas.sql
   ├─ N05_jobon.sql
   ├─ N06_peso.sql
   ├─ N07_pegamentos.sql
   ├─ N08_reparacoes.sql
   ├─ N09_armazem.sql
   ├─ N10_tampoes.sql
   ├─ N11_partilhado.sql
   ├─ N12_rls.sql
   ├─ N13_jobon_production_folder.sql
   ├─ N14_pegamentos_documents.sql
   ├─ N15_pegamentos_tool_number.sql
   ├─ N16_pegamentos_component_nominals.sql
   ├─ N17_pegamentos_notas.sql
   ├─ N18_bq_repairer.sql
   ├─ N19_tool_usage.sql
   ├─ N20_repairer_repair_types.sql
   ├─ N21_tampoes_machines.sql
   ├─ N22_reparacao_interna_context.sql
   ├─ N23_controlo_folha.sql
   ├─ N24_jobon_user_current.sql
   ├─ N25_remediation.sql
   └─ N26_user_modules_override.sql
```

- `database\consolidated_clean_install.sql` = the current full-schema SQL definition (all tables, columns, constraints, indexes, functions, triggers, RLS/policies in one pass).
- `database\migrations\` = individual migration files; each object's source pointer may name its introducing migration file.
- Migration file chronology and per-migration content are mapped separately in `03_MIGRATIONS.md`.
- The schema lives in the PostgreSQL `public` schema. Role/privilege statements are guarded for Supabase-hosted compatibility.

**Global object counts (current schema):**

| Object | Count |
|---|---|
| Tables | 59 |
| Primary key constraints | 59 |
| Foreign key constraints | 118 |
| UNIQUE constraints | 18 |
| Partial UNIQUE indexes | 3 |
| Total UNIQUE structures | 21 |
| CHECK constraints | 64 |
| Non-unique indexes | 75 |
| Total indexes | 78 |
| Functions | 2 |
| Triggers | 18 |
| RLS-enabled tables | 59 |
| RLS policies | 58 |
| Views | 0 |
| Explicit sequences | 0 |
| Custom enum types | 0 |

> PKs use `gen_random_uuid()` (48 tables); text/composite PKs are used by the tables noted in the Primary Key Index. No explicit sequence, custom enum type, or view exists in the current schema. Enum-like value sets are enforced via CHECK constraints on text columns.

---

## 3. Global Database Inventory

Every current table, grouped by database area (navigation only). `Source` = current-schema file + introducing migration.

| Table | Database Area | PK | Main Relationships | Notes | Source |
|---|---|---|---|---:|---|
| `schema_migrations` | Infrastructure | `version text` | — | RLS-enabled, no policy; tracking table | `consolidated_clean_install.sql` (introduced by migration runner) |
| `access_templates` | Shared / Access | `template_id text` | → `internal_users` | grant-template table | `N01_identity.sql` |
| `internal_users` | Shared / Access | `actor_id text` | → `access_templates`; referenced by many actor FKs | `auth_user_id` NOT NULL + UNIQUE; `modules_override` | `N01_identity.sql`, `N25_remediation.sql`, `N26_user_modules_override.sql` |
| `audit_events` | Shared (audit) | `audit_event_id uuid` | — (`job_on_id`/`revision_id` no FK) | global audit table; append-only trigger | `N01_identity.sql` |
| `module_catalog_mirror` | Shared / Access | `module_id text` | — | catalog mirror table | `N02_catalog.sql` |
| `bq_lotes` | Boquilhas | `bq_lote_id uuid` | → `internal_users`; ← `bq_traces`, `bq_discrepancies`, `bq_lifecycle_history` | reference+batch UNIQUE; regex CHECK | `N03_bq.sql` |
| `bq_traces` | Boquilhas | `bq_trace_id uuid` | → `bq_lotes`; ← `bq_movements`, `bq_discrepancies`, `bq_utilisation_readings` | one active trace per lot (partial UNIQUE) | `N03_bq.sql` |
| `bq_movements` | Boquilhas | `bq_movement_id uuid` | → `bq_traces`, `internal_users`, `repairers` (`noted_repairer_id`) | append-only trigger | `N03_bq.sql`, `N18_bq_repairer.sql` |
| `bq_discrepancies` | Boquilhas | `bq_discrepancy_id uuid` | → `bq_lotes`, `bq_traces`, `internal_users` | — | `N03_bq.sql` |
| `bq_lifecycle_history` | Boquilhas | `bq_lifecycle_history_id uuid` | → `bq_lotes`, `internal_users` | append-only trigger | `N03_bq.sql` |
| `bq_utilisation_readings` | Boquilhas | `bq_utilisation_reading_id uuid` | → `bq_traces`, `internal_users` | append-only trigger | `N03_bq.sql` |
| `tool_references` | Ferramentas | `tool_reference_id uuid` | → `internal_users`; ← `tool_lotes` | type+code UNIQUE; tool_type CHECK | `N04_ferramentas.sql` |
| `tool_lotes` | Ferramentas | `tool_lote_id uuid` | → `tool_references`; ← `physical_pieces`, `tool_check_rules`, `tool_usage_records`, `job_on_component.source_lot_id`, `warehouse_stock.tool_lote_id`, `controlo_sheet_items.source_lot_id` | reference+lote UNIQUE | `N04_ferramentas.sql` |
| `physical_pieces` | Ferramentas | `physical_piece_id uuid` | → `tool_lotes`; ← `repair_exit_items` | lote+number UNIQUE | `N04_ferramentas.sql` |
| `tool_check_rules` | Ferramentas | `tool_check_rule_id uuid` | → `tool_lotes`; self `copied_from_rule_id`; ← `tool_check_occurrences`, `job_on_verification_occurrence.source_rule_id` | frequency CHECK | `N04_ferramentas.sql` |
| `tool_check_occurrences` | Ferramentas | `tool_check_occurrence_id uuid` | → `tool_check_rules`, `internal_users`; `job_on_id`/`job_on_component_id` no FK | separate from `job_on_verification_occurrence` | `N04_ferramentas.sql` |
| `tool_usage_records` | Ferramentas | `tool_usage_record_id uuid` | → `tool_lotes`, `internal_users` | append-only trigger | `N19_tool_usage.sql` |
| `job_on` | Job On | `job_on_id uuid` | self `copied_from_job_on_id`; → `internal_users`, `job_on_revision.current` (circular); ← revision family, `peso_controlos`, `pegamento_controlos`, `controlo_sheets`, `jobon_user_current`; `article_reference_id` no FK | partial UNIQUE `(production_code, machine_code)` | `N05_jobon.sql`, `N25_remediation.sql` |
| `job_on_revision` | Job On | `job_on_revision_id uuid` | → `job_on`, `internal_users`; ← `job_on_component`, `job_on_audit_event`; pinned by Peso/Pegamentos/Controlo/RI FKs | append-only trigger | `N05_jobon.sql`, `N25_remediation.sql` |
| `job_on_component` | Job On | `job_on_component_id uuid` | → `job_on_revision`, `tool_references` (source_tool), `tool_lotes` (source_lot); ← component children | family CHECK; append-only trigger | `N05_jobon.sql`, `N25_remediation.sql` |
| `job_on_component_field` | Job On | `job_on_component_field_id uuid` | → `job_on_component` | component+key UNIQUE; append-only trigger | `N05_jobon.sql`, `N25_remediation.sql` |
| `job_on_component_row` | Job On | `job_on_component_row_id uuid` | → `job_on_component` | CAL row table; append-only trigger | `N05_jobon.sql`, `N25_remediation.sql` |
| `job_on_verification_occurrence` | Job On | `job_on_verification_occurrence_id uuid` | → `job_on_component`, `tool_check_rules` (source_rule), `internal_users` | separate from `tool_check_occurrences` | `N05_jobon.sql`, `N25_remediation.sql` |
| `job_on_audit_event` | Job On | `job_on_audit_event_id uuid` | → `job_on`, `job_on_revision`, `internal_users` | append-only trigger | `N05_jobon.sql` |
| `job_on_field_option` | Job On | `job_on_field_option_id uuid` | — | family+key+value UNIQUE | `N05_jobon.sql` |
| `jobon_user_current` | Job On | `actor_id text` (FK) | → `internal_users`, `job_on` | per-user current table | `N24_jobon_user_current.sql` |
| `peso_references` | Peso | `peso_reference_id uuid` | → `internal_users`; ← `peso_lotes`, `peso_controlos` | mold+neckring UNIQUE; `change_log` jsonb | `N06_peso.sql` |
| `peso_lotes` | Peso | `peso_lote_id uuid` | → `peso_references`, `internal_users`; ← `peso_controlos` | reference+lote UNIQUE; `processo` CHECK; `allowed_lines` text[] | `N06_peso.sql` |
| `peso_controlos` | Peso | `peso_controlo_id uuid` | → `peso_references`, `peso_lotes`, `job_on`, `job_on_revision`, `internal_users`; ← `peso_leituras`, `peso_comparacao_anterior` | identity UNIQUE; status/record_type CHECK; jsonb snapshots; approved-guard trigger | `N06_peso.sql`, `N25_remediation.sql` |
| `peso_leituras` | Peso | `peso_leitura_id uuid` | → `peso_controlos` (CASCADE), `internal_users` | controlo+cm UNIQUE; `readings` jsonb | `N06_peso.sql` |
| `peso_comparacao_anterior` | Peso | `peso_controlo_id uuid` (FK, CASCADE) | → `peso_controlos`; self `previous_peso_controlo_id` | jsonb snapshot/deltas | `N06_peso.sql` |
| `peso_day_approvals` | Peso | `peso_day_approval_id uuid` | → `internal_users` | identity UNIQUE | `N06_peso.sql` |
| `peso_settings` | Peso | `setting_key text` | → `internal_users` | settings table | `N06_peso.sql` |
| `pegamento_controlos` | Pegamentos | `pegamento_controlo_id uuid` | → `job_on`, `job_on_revision`, `internal_users`; ← `pegamento_medicoes`, `pegamento_documentos` | status CHECK; jsonb snaps; note/nominal columns | `N07_pegamentos.sql`, `N16_pegamentos_component_nominals.sql`, `N17_pegamentos_notas.sql` |
| `pegamento_medicoes` | Pegamentos | `pegamento_medicao_id uuid` | → `pegamento_controlos`, `internal_users` | append-only trigger; `tool_number` | `N07_pegamentos.sql`, `N15_pegamentos_tool_number.sql` |
| `pegamento_documentos` | Pegamentos | `pegamento_documento_id uuid` | → `pegamento_controlos` (1:1 UNIQUE), `internal_users` | document-metadata table | `N14_pegamentos_documents.sql` |
| `repairers` | Reparação Externa | `repairer_id uuid` | ← `repair_exits`, `line_repairer_defaults`, `repairer_repair_types`, `bq_movements.noted_repairer_id` | referenced table | `N08_reparacoes.sql` |
| `line_repairer_defaults` | Reparação Externa | `(line, tool_type)` | → `repairers`, `internal_users` | composite PK; tool_type CHECK | `N08_reparacoes.sql` |
| `repair_exits` | Reparação Externa | `repair_exit_id uuid` | → `repairers`, `internal_users`; ← `repair_exit_items`, `warehouse_movements` | type/status CHECK | `N08_reparacoes.sql` |
| `repair_exit_items` | Reparação Externa | `repair_exit_item_id uuid` | → `repair_exits`, `bq_lotes`, `physical_pieces`, `internal_users`; ← `repair_events` | kind CHECK (CM/MF piece XOR BQ lot); status CHECK | `N08_reparacoes.sql`, `N25_remediation.sql` |
| `repair_events` | Reparação Externa | `repair_event_id uuid` | → `repair_exit_items`, `internal_repair_records`, `internal_users` | append-only trigger; scope CHECK | `N08_reparacoes.sql` |
| `internal_repair_records` | Reparação Interna | `internal_repair_record_id uuid` | self `correction_of_id`; → `internal_users`, `job_on_revision`; `job_on_id`/`lot_id` no FK | tool_type CHECK CM/MF/BQ | `N08_reparacoes.sql`, `N22_reparacao_interna_context.sql` |
| `repairer_repair_types` | Reparação Externa | `(repairer_id, repair_type)` | → `repairers` | composite PK; repair_type CHECK | `N20_repairer_repair_types.sql` |
| `warehouse_locations` | Armazém | `warehouse_location_id uuid` | → `internal_users`; ← `warehouse_stock` | `code` UNIQUE | `N09_armazem.sql` |
| `warehouse_stock` | Armazém | `warehouse_stock_id uuid` | → `warehouse_locations`, `tool_lotes`, `internal_users`; ← `warehouse_movements` | partial UNIQUE active occupation | `N09_armazem.sql` |
| `warehouse_movements` | Armazém | `warehouse_movement_id uuid` | → `warehouse_stock`, `repair_exits`, `internal_users` | append-only trigger; direction CHECK | `N09_armazem.sql` |
| `tampao_field_defs` | Tampões | `tampao_field_def_id uuid` | ← `tampao_field_values` | `field_name` UNIQUE | `N10_tampoes.sql` |
| `tampao_field_values` | Tampões | `tampao_field_value_id uuid` | → `tampao_field_defs` | field+value UNIQUE | `N10_tampoes.sql` |
| `tampao_configurations` | Tampões | `tampao_configuration_id uuid` | → `internal_users`; ← saldos/movements/planos/machines/notes/events | `values_json` UNIQUE | `N10_tampoes.sql` |
| `tampao_saldos` | Tampões | `tampao_saldo_id uuid` | → `tampao_configurations` (1:1 UNIQUE) | balance table; >= 0 CHECK | `N10_tampoes.sql` |
| `tampao_movements` | Tampões | `tampao_movement_id uuid` | → `tampao_configurations` (origin/dest), `internal_users` | append-only trigger; type/qty CHECK | `N10_tampoes.sql` |
| `tampao_planos` | Tampões | `tampao_plano_id uuid` | → `tampao_configurations`, `internal_users`; `job_on_id` no FK | planned-qty CHECK | `N10_tampoes.sql` |
| `tampao_configuration_machines` | Tampões | `(tampao_configuration_id, machine)` | → `tampao_configurations` | composite PK; machine CHECK | `N21_tampoes_machines.sql` |
| `tampao_configuration_notes` | Tampões | `tampao_configuration_note_id uuid` | → `tampao_configurations`, `internal_users` | append-only trigger | `N21_tampoes_machines.sql` |
| `tampao_configuration_machine_event` | Tampões | `tampao_configuration_machine_event_id uuid` | → `tampao_configurations`, `internal_users` | append-only trigger; action/machine CHECK | `N21_tampoes_machines.sql` |
| `app_settings` | Shared (settings) | `setting_key text` | → `internal_users` | settings table | `N11_partilhado.sql` |
| `controlo_sheets` | Controlo | `controlo_sheet_id uuid` | → `job_on`, `job_on_revision`, `internal_users`; ← items, events | status/decision CHECK | `N23_controlo_folha.sql` |
| `controlo_sheet_items` | Controlo | `controlo_sheet_item_id uuid` | → `controlo_sheets` (CASCADE), `tool_references`, `tool_lotes` | result CHECK | `N23_controlo_folha.sql` |
| `controlo_sheet_events` | Controlo | `controlo_sheet_event_id uuid` | → `controlo_sheets` (CASCADE), `internal_users` | append-only trigger; event_type CHECK | `N23_controlo_folha.sql` |

---

## 4. Shared / Identity / Access

### `access_templates`
- **AREA:** Shared / Access
- **PK:** `template_id text`
- **IMPORTANT COLUMNS:** `name text NOT NULL`, `modules jsonb NOT NULL DEFAULT '[]'`, `active boolean NOT NULL DEFAULT TRUE`, `created_at_utc timestamptz`, `created_by text`, `updated_at_utc timestamptz`
- **INDEXES:** `ix_access_templates_active (active)`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N01_identity.sql`

### `internal_users`
- **AREA:** Shared / Access
- **PK:** `actor_id text`
- **IMPORTANT COLUMNS:** `auth_user_id uuid NOT NULL UNIQUE`, `template_id text NOT NULL`, `display_name text NOT NULL`, `profile_title text`, `active boolean NOT NULL DEFAULT TRUE`, `modules_override jsonb`, `created_at_utc`, `updated_at_utc`
- **FK:** `template_id → access_templates.template_id`
- **INDEXES:** `ix_internal_users_auth_user_id`, `ix_internal_users_active`, `ix_internal_users_template_id`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · `database\migrations\N01_identity.sql` (`. N25_remediation.sql`, `N26_user_modules_override.sql`)

### `audit_events`
- **AREA:** Shared (audit)
- **PK:** `audit_event_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `occurred_at_utc timestamptz NOT NULL`, `year integer NOT NULL`, `actor_user_id text`, `actor_name_snapshot text`, `module_id text`, `action_code text`, `entity_type text`, `entity_id text`, `entity_label_snapshot text`, `result text`, `reason text`, `correlation_id uuid`, `job_on_id uuid` (no FK), `revision_id uuid` (no FK), `before_summary jsonb`, `after_summary jsonb`
- **CHECK:** `ck_audit_events_year_positive` (`year > 0`); `ck_audit_events_result` (`result IN ('succeeded','failed','denied','corrected')`)
- **TRIGGER:** `trg_audit_events_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_audit_events_year`, `ix_audit_events_module_action (module_id, action_code)`, `ix_audit_events_actor (actor_user_id, year)`, `ix_audit_events_entity (entity_type, entity_id)`, `ix_audit_events_occurred_at`, `ix_audit_events_job_on_id`, `ix_audit_events_module_time (module_id, occurred_at_utc)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N01_identity.sql`

### `module_catalog_mirror`
- **AREA:** Shared / Access
- **PK:** `module_id text`
- **IMPORTANT COLUMNS:** `display_name text NOT NULL`, `display_order integer NOT NULL`, `active boolean NOT NULL DEFAULT TRUE`, `synced_at_utc timestamptz`
- **INDEXES:** `ix_module_catalog_mirror_order (display_order)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N02_catalog.sql`

### `app_settings`
- **AREA:** Shared (settings)
- **PK:** `setting_key text`
- **IMPORTANT COLUMNS:** `setting_value jsonb NOT NULL`, `updated_at_utc timestamptz NOT NULL`, `updated_by text`
- **FK:** `updated_by → internal_users.actor_id`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N11_partilhado.sql`

---

## 5. Job On

Database hierarchy (`job_on` → `job_on_revision` → `job_on_component` → `job_on_component_field` / `job_on_component_row` / `job_on_verification_occurrence`), plus `job_on_audit_event`, `job_on_field_option`, `jobon_user_current`.

### `job_on`
- **AREA:** Job On
- **PK:** `job_on_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `production_code text NOT NULL`, `article_reference_id uuid` (no FK), `article_reference_snapshot jsonb`, `machine_code text NOT NULL`, `planned_start_at timestamptz`, `planned_end_at timestamptz`, `status text NOT NULL DEFAULT 'rascunho'`, `current_revision_id uuid`, `copied_from_job_on_id uuid`, `closed_at_utc timestamptz`, `canceled_at_utc timestamptz`, `canceled_by text`, `cancel_reason text`, `created_at_utc`, `created_by text`, `updated_at_utc`, `production_folder text`
- **FK:** `copied_from_job_on_id → job_on.job_on_id` (self); `canceled_by → internal_users.actor_id`; `created_by → internal_users.actor_id`; `current_revision_id → job_on_revision.job_on_revision_id` (circular)
- **CHECK:** `ck_job_on_status` (`status IN ('rascunho','planeado','em_fabrico','fechado','cancelado')`); `ck_job_on_lifecycle_consistent` (`(status='fechado')=(closed_at_utc IS NOT NULL) AND (status='cancelado')=(canceled_at_utc IS NOT NULL)`)
- **UNIQUE INDEX:** `uq_job_on_identity` on `(production_code, machine_code)` `WHERE canceled_at_utc IS NULL`
- **INDEXES:** `ix_job_on_production_code`, `ix_job_on_status`, `ix_job_on_machine_planned (machine_code, planned_start_at)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · `database\migrations\N05_jobon.sql` (`. N13_jobon_production_folder.sql`, `N25_remediation.sql`)

### `job_on_revision`
- **AREA:** Job On
- **PK:** `job_on_revision_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `job_on_id uuid NOT NULL`, `revision_number integer NOT NULL`, `production_snapshot jsonb`, `reference_snapshot jsonb`, `machine_snapshot jsonb`, `dates_snapshot jsonb`, `sections jsonb NOT NULL DEFAULT '{}'`, `drop_count numeric(12,2)`, `type_snapshot jsonb`, `stop_snapshot jsonb`, `weight_snapshot jsonb`, `process_snapshot jsonb`, `general_notes text`, `image_asset_id text`, `change_reason text`, `saved_by text`, `saved_at_utc timestamptz NOT NULL`
- **FK:** `job_on_id → job_on.job_on_id`; `saved_by → internal_users.actor_id`; (inbound) `job_on.current_revision_id`
- **UNIQUE:** `uq_job_on_revision_number (job_on_id, revision_number)`
- **CHECK:** `ck_job_on_revision_number` (`revision_number >= 1`)
- **TRIGGER:** `trg_job_on_revision_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_job_on_revision_job_on (job_on_id)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N05_jobon.sql`

### `job_on_component`
- **AREA:** Job On
- **PK:** `job_on_component_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `job_on_revision_id uuid NOT NULL`, `family text NOT NULL`, `source_tool_id uuid`, `source_lot_id uuid`, `reference_snapshot text`, `lot_snapshot text`, `technical_name_snapshot text`, `planned_quantity numeric(12,2)`, `stock_snapshot numeric(12,2)`, `usage_snapshot numeric(12,2)`, `notes text`, `display_order integer NOT NULL DEFAULT 0`
- **FK:** `job_on_revision_id → job_on_revision.job_on_revision_id`; `source_tool_id → tool_references.tool_reference_id` (cross-area → Ferramentas); `source_lot_id → tool_lotes.tool_lote_id` (cross-area → Ferramentas)
- **CHECK:** `ck_job_on_component_family` (`family IN ('MP_CM','MF','BQ','PU','CAL','AN','ARR','PI','CS','TP','FO')`)
- **TRIGGER:** `trg_job_on_component_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_job_on_component_revision (job_on_revision_id)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N05_jobon.sql`

### `job_on_component_field`
- **AREA:** Job On
- **PK:** `job_on_component_field_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `job_on_component_id uuid NOT NULL`, `field_key text NOT NULL`, `value_type text NOT NULL`, `value_text text`, `value_integer integer`, `value_decimal numeric(18,4)`, `value_boolean boolean`, `value_date date`, `display_order integer NOT NULL DEFAULT 0`
- **FK:** `job_on_component_id → job_on_component.job_on_component_id`
- **UNIQUE:** `uq_job_on_component_field (job_on_component_id, field_key)`
- **CHECK:** `ck_job_on_component_field_type` (`value_type IN ('text','integer','decimal','boolean','date','select')`)
- **TRIGGER:** `trg_job_on_component_field_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_job_on_component_field_component (job_on_component_id)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N05_jobon.sql`

### `job_on_component_row`
- **AREA:** Job On
- **PK:** `job_on_component_row_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `job_on_component_id uuid NOT NULL`, `element_label text NOT NULL`, `value_decimal numeric(18,4)`, `value_text text`, `unit text`, `machine_quantity numeric(12,2)`, `display_order integer NOT NULL DEFAULT 0`
- **FK:** `job_on_component_id → job_on_component.job_on_component_id`
- **TRIGGER:** `trg_job_on_component_row_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_job_on_component_row_component (job_on_component_id)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N05_jobon.sql`

### `job_on_verification_occurrence`
- **AREA:** Job On
- **PK:** `job_on_verification_occurrence_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `job_on_component_id uuid NOT NULL`, `source_rule_id uuid`, `rule_text_snapshot text`, `status text NOT NULL DEFAULT 'pendente'`, `completion_source text NOT NULL DEFAULT 'manual_job_on'`, `completed_by text`, `completed_at_utc timestamptz`, `created_at_utc`, `updated_at_utc`
- **FK:** `job_on_component_id → job_on_component.job_on_component_id`; `source_rule_id → tool_check_rules.tool_check_rule_id` (cross-area → Ferramentas); `completed_by → internal_users.actor_id`
- **CHECK:** `ck_job_on_verification_status` (`status IN ('pendente','confirmada','reposta','desativada')`); `ck_job_on_verification_source` (`completion_source = 'manual_job_on'`); `ck_job_on_verification_completed` (`(status IN ('confirmada','reposta')) = (completed_at_utc IS NOT NULL)`)
- **INDEXES:** `ix_job_on_verification_component (job_on_component_id)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N05_jobon.sql`

### `job_on_audit_event`
- **AREA:** Job On
- **PK:** `job_on_audit_event_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `job_on_id uuid NOT NULL`, `job_on_revision_id uuid`, `event_type text NOT NULL`, `before_snapshot jsonb`, `after_snapshot jsonb`, `actor_id text`, `occurred_at_utc timestamptz NOT NULL`
- **FK:** `job_on_id → job_on.job_on_id`; `job_on_revision_id → job_on_revision.job_on_revision_id`; `actor_id → internal_users.actor_id`
- **TRIGGER:** `trg_job_on_audit_event_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_job_on_audit_event_job_on (job_on_id)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N05_jobon.sql`

### `job_on_field_option`
- **AREA:** Job On
- **PK:** `job_on_field_option_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `family text NOT NULL`, `field_key text NOT NULL`, `option_value text NOT NULL`, `option_label text`, `display_order integer NOT NULL DEFAULT 0`, `active boolean NOT NULL DEFAULT TRUE`, `created_at_utc`, `updated_at_utc`
- **UNIQUE:** `uq_job_on_field_option (family, field_key, option_value)`
- **INDEXES:** `ix_job_on_field_option_lookup (family, field_key, active)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N05_jobon.sql`

### `jobon_user_current`
- **AREA:** Job On
- **PK:** `actor_id text` (also FK `→ internal_users.actor_id`)
- **IMPORTANT COLUMNS:** `job_on_id uuid NOT NULL`, `production_code text NOT NULL`, `reference text NOT NULL DEFAULT ''`, `machine_code text NOT NULL DEFAULT ''`, `opened_at_utc timestamptz NOT NULL`
- **FK:** `actor_id → internal_users.actor_id`; `job_on_id → job_on.job_on_id`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N24_jobon_user_current.sql`

---

## 6. Controlo

### `controlo_sheets`
- **AREA:** Controlo
- **PK:** `controlo_sheet_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `job_on_id uuid NOT NULL`, `job_on_revision_id uuid NOT NULL`, `production_code text NOT NULL`, `reference text NOT NULL`, `machine_code text NOT NULL`, `display_id text NOT NULL`, `status text NOT NULL DEFAULT 'rascunho'`, `created_by text`, `created_at_utc`, `submitted_by text`, `submitted_at_utc timestamptz`, `submitted_note text`, `decided_by text`, `decided_at_utc timestamptz`, `decision text`, `decision_note text`, `updated_at_utc`
- **FK:** `job_on_id → job_on.job_on_id`; `job_on_revision_id → job_on_revision.job_on_revision_id`; `created_by/submitted_by/decided_by → internal_users.actor_id`
- **CHECK:** `ck_controlo_sheets_status` (`status IN ('rascunho','submetido','aprovado','rejeitado')`); `ck_controlo_sheets_decision` (decision tuple consistency)
- **INDEXES:** `ix_controlo_sheets_job_on`, `ix_controlo_sheets_revision`, `ix_controlo_sheets_production (production_code, machine_code)`, `ix_controlo_sheets_status`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N23_controlo_folha.sql`

### `controlo_sheet_items`
- **AREA:** Controlo
- **PK:** `controlo_sheet_item_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `controlo_sheet_id uuid NOT NULL`, `family text NOT NULL`, `source_tool_id uuid`, `source_lot_id uuid`, `reference_snapshot text`, `lot_snapshot text`, `technical_name_snapshot text`, `result text`, `observation text`, `mcaliper_link text`
- **FK:** `controlo_sheet_id → controlo_sheets.controlo_sheet_id` (ON DELETE CASCADE); `source_tool_id → tool_references.tool_reference_id` (cross-area); `source_lot_id → tool_lotes.tool_lote_id` (cross-area)
- **CHECK:** `ck_controlo_sheet_items_result` (`result IS NULL OR result IN ('OK','NOK')`)
- **INDEXES:** `ix_controlo_sheet_items_sheet (controlo_sheet_id)`, `ix_controlo_sheet_items_family (controlo_sheet_id, family)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N23_controlo_folha.sql`

### `controlo_sheet_events`
- **AREA:** Controlo
- **PK:** `controlo_sheet_event_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `controlo_sheet_id uuid NOT NULL`, `event_type text NOT NULL`, `actor_id text`, `occurred_at_utc timestamptz NOT NULL`, `before_summary jsonb`, `after_summary jsonb`, `note text`
- **FK:** `controlo_sheet_id → controlo_sheets.controlo_sheet_id` (ON DELETE CASCADE); `actor_id → internal_users.actor_id`
- **CHECK:** `ck_controlo_sheet_events_type` (`event_type IN ('criar','editar','submeter','reeabrir','decidir')`)
- **TRIGGER:** `trg_controlo_sheet_events_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_controlo_sheet_events_sheet (controlo_sheet_id)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N23_controlo_folha.sql`

---

## 7. Ferramentas

### `tool_references`
- **AREA:** Ferramentas
- **PK:** `tool_reference_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `tool_type text NOT NULL`, `ref_code text NOT NULL`, `technical_name text`, `owner_plant text`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `created_by → internal_users.actor_id`
- **UNIQUE:** `uq_tool_references_type_code (tool_type, ref_code)`
- **CHECK:** `ck_tool_references_type` → `tool_type IN ('CM','MF','BQ','PU','CS')`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N04_ferramentas.sql`

### `tool_lotes`
- **AREA:** Ferramentas
- **PK:** `tool_lote_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `tool_reference_id uuid NOT NULL`, `lote text NOT NULL`, `qty integer`, `allowed_lines text[] NOT NULL DEFAULT '{}'`, `drawing_code text`, `drawing_revision text`, `processo text`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `tool_reference_id → tool_references.tool_reference_id`; `created_by → internal_users.actor_id`
- **UNIQUE:** `uq_tool_lotes_reference_lote (tool_reference_id, lote)`
- **CHECK:** `ck_tool_lotes_qty` (`qty IS NULL OR qty >= 0`)
- **INDEXES:** `ix_tool_lotes_reference (tool_reference_id)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N04_ferramentas.sql`

### `physical_pieces`
- **AREA:** Ferramentas
- **PK:** `physical_piece_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `tool_lote_id uuid NOT NULL`, `sequence integer NOT NULL`, `number text NOT NULL`, `status text NOT NULL DEFAULT 'operational'`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `tool_lote_id → tool_lotes.tool_lote_id`; `created_by → internal_users.actor_id`
- **UNIQUE:** `uq_physical_pieces_lote_number (tool_lote_id, number)`
- **CHECK:** `ck_physical_pieces_sequence` (`sequence >= 1`)
- **INDEXES:** `ix_physical_pieces_lote (tool_lote_id)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N04_ferramentas.sql`

### `tool_check_rules`
- **AREA:** Ferramentas
- **PK:** `tool_check_rule_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `tool_lote_id uuid NOT NULL`, `rule_text text NOT NULL`, `frequency text NOT NULL`, `active boolean NOT NULL DEFAULT TRUE`, `copied_from_rule_id uuid`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `tool_lote_id → tool_lotes.tool_lote_id`; `copied_from_rule_id → tool_check_rules.tool_check_rule_id` (self); `created_by → internal_users.actor_id`
- **CHECK:** `ck_tool_check_rules_frequency` (`frequency IN ('uma_vez_no_lote','por_fabrico')`)
- **INDEXES:** `ix_tool_check_rules_lote (tool_lote_id)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N04_ferramentas.sql`

### `tool_check_occurrences`
- **AREA:** Ferramentas
- **PK:** `tool_check_occurrence_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `tool_check_rule_id uuid NOT NULL`, `job_on_id uuid` (no FK), `job_on_component_id uuid` (no FK), `status text NOT NULL DEFAULT 'pendente'`, `completion_source text NOT NULL DEFAULT 'manual_job_on'`, `completed_by text`, `completed_at_utc timestamptz`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `tool_check_rule_id → tool_check_rules.tool_check_rule_id`; `completed_by → internal_users.actor_id`; `created_by → internal_users.actor_id`
- **CHECK:** `ck_tool_check_occurrences_status` (`status IN ('pendente','confirmada','reposta','desativada')`); `ck_tool_check_occurrences_source` (`completion_source = 'manual_job_on'`); `ck_tool_check_occurrences_completed` (`(status IN ('confirmada','reposta')) = (completed_at_utc IS NOT NULL)`)
- **INDEXES:** `ix_tool_check_occurrences_rule`, `ix_tool_check_occurrences_job_on`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N04_ferramentas.sql`

### `tool_usage_records`
- **AREA:** Ferramentas
- **PK:** `tool_usage_record_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `tool_lote_id uuid NOT NULL`, `sap_start numeric(5,2)`, `sap_end numeric(5,2)`, `percent_used numeric(5,2)`, `value_added numeric(12,2)`, `value_cumulative numeric(12,2) NOT NULL`, `notes text`, `actor_id text`, `reading_at_utc timestamptz NOT NULL`
- **FK:** `tool_lote_id → tool_lotes.tool_lote_id`; `actor_id → internal_users.actor_id`
- **CHECK:** `ck_tool_usage_records_sap_start`, `ck_tool_usage_records_sap_end`, `ck_tool_usage_records_percent` (each `IS NULL OR (0..100)`); `ck_tool_usage_records_cumulative` (`value_cumulative >= 0`)
- **TRIGGER:** `trg_tool_usage_records_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_tool_usage_records_lote (tool_lote_id)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N19_tool_usage.sql`

---

## 8. Armazém

### `warehouse_locations`
- **AREA:** Armazém
- **PK:** `warehouse_location_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `code text NOT NULL UNIQUE`, `kind text`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `created_by → internal_users.actor_id`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N09_armazem.sql`

### `warehouse_stock`
- **AREA:** Armazém
- **PK:** `warehouse_stock_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `warehouse_location_id uuid NOT NULL`, `tool_lote_id uuid NOT NULL`, `occupied_since_utc timestamptz NOT NULL`, `occupied_by text`, `released_at_utc timestamptz`, `released_by text`
- **FK:** `warehouse_location_id → warehouse_locations.warehouse_location_id`; `tool_lote_id → tool_lotes.tool_lote_id`; `occupied_by → internal_users.actor_id`; `released_by → internal_users.actor_id`
- **UNIQUE INDEX:** `uq_warehouse_stock_active_occupation` on `(warehouse_location_id, tool_lote_id)` `WHERE released_at_utc IS NULL`
- **INDEXES:** `ix_warehouse_stock_location`, `ix_warehouse_stock_tool_lote`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N09_armazem.sql`

### `warehouse_movements`
- **AREA:** Armazém
- **PK:** `warehouse_movement_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `warehouse_stock_id uuid`, `direction text NOT NULL`, `qty numeric(12,2)`, `destination text`, `repair_exit_id uuid`, `actor_id text`, `occurred_at_utc timestamptz NOT NULL`
- **FK:** `warehouse_stock_id → warehouse_stock.warehouse_stock_id`; `repair_exit_id → repair_exits.repair_exit_id` (cross-area); `actor_id → internal_users.actor_id`
- **CHECK:** `ck_warehouse_movements_direction` (`direction IN ('in','out')`)
- **TRIGGER:** `trg_warehouse_movements_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_warehouse_movements_stock`, `ix_warehouse_movements_occurred`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N09_armazem.sql`

---

## 9. Boquilhas

`bq_lotes` and `tool_lotes` are distinct tables with no FK between them.

### `bq_lotes`
- **AREA:** Boquilhas
- **PK:** `bq_lote_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `reference text NOT NULL`, `batch_code text NOT NULL`, `allowed_lines text[] NOT NULL DEFAULT '{}'`, `lifecycle_state text NOT NULL DEFAULT 'available'`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `created_by → internal_users.actor_id`
- **UNIQUE:** `uq_bq_lotes_reference_batch (reference, batch_code)`
- **CHECK:** `ck_bq_lotes_reference` (`reference ~ '^[A-Z][0-9]{3}$'`); `ck_bq_lotes_lifecycle` (`lifecycle_state IN ('available','archived','scrapped')`)
- **INDEXES:** `ix_bq_lotes_lifecycle`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N03_bq.sql`

### `bq_traces`
- **AREA:** Boquilhas
- **PK:** `bq_trace_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `bq_lote_id uuid NOT NULL`, `status text NOT NULL`, `purpose text NOT NULL`, `start_line text NOT NULL`, `sap_start numeric(5,2)`, `sap_end numeric(5,2)`, `reopen_history jsonb NOT NULL DEFAULT '[]'`, `deleted_movements jsonb NOT NULL DEFAULT '[]'`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `bq_lote_id → bq_lotes.bq_lote_id`; `created_by → internal_users.actor_id`
- **CHECK:** `ck_bq_traces_status` (`status IN ('active','closed')`); `ck_bq_traces_purpose` (`purpose IN ('production','repair')`); `ck_bq_traces_sap_start` / `ck_bq_traces_sap_end` (`IS NULL OR (0..100)`)
- **UNIQUE INDEX:** `uq_bq_traces_active` on `(bq_lote_id)` `WHERE status = 'active'`
- **INDEXES:** `ix_bq_traces_lote`, `ix_bq_traces_status`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N03_bq.sql`

### `bq_movements`
- **AREA:** Boquilhas
- **PK:** `bq_movement_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `bq_trace_id uuid NOT NULL`, `movement_type text NOT NULL`, `qty numeric(12,2)`, `exceptional_received_qty numeric(12,2)`, `line text`, `notes text`, `occurred_at_utc timestamptz NOT NULL`, `actor_id text`, `noted_repairer_id uuid`
- **FK:** `bq_trace_id → bq_traces.bq_trace_id`; `actor_id → internal_users.actor_id`; `noted_repairer_id → repairers.repairer_id` (cross-area → Reparação Externa)
- **CHECK:** `ck_bq_movements_type` (`movement_type IN ('inicio','saida','entrada','irreparavel','linha','contagem','fim')`); `ck_bq_movements_qty` (`qty IS NOT NULL OR movement_type = 'linha'`); `ck_bq_movements_exceptional` (`exceptional_received_qty IS NULL OR >= 0`)
- **TRIGGER:** `trg_bq_movements_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_bq_movements_trace`, `ix_bq_movements_occurred`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · `database\migrations\N03_bq.sql` (`. N18_bq_repairer.sql`)

### `bq_discrepancies`
- **AREA:** Boquilhas
- **PK:** `bq_discrepancy_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `bq_lote_id uuid NOT NULL`, `bq_trace_id uuid`, `expected_qty numeric(12,2) NOT NULL`, `actual_qty numeric(12,2) NOT NULL`, `excess_qty numeric(12,2) NOT NULL`, `status text NOT NULL DEFAULT 'open'`, `resolution_note text`, `resolved_by text`, `resolved_at_utc timestamptz`, `created_at_utc`, `created_by text`
- **FK:** `bq_lote_id → bq_lotes.bq_lote_id`; `bq_trace_id → bq_traces.bq_trace_id`; `resolved_by → internal_users.actor_id`; `created_by → internal_users.actor_id`
- **CHECK:** `ck_bq_discrepancies_status` (`status IN ('open','under_review','resolved')`)
- **INDEXES:** `ix_bq_discrepancies_lote`, `ix_bq_discrepancies_status`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N03_bq.sql`

### `bq_lifecycle_history`
- **AREA:** Boquilhas
- **PK:** `bq_lifecycle_history_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `bq_lote_id uuid NOT NULL`, `event text NOT NULL`, `reason text`, `actor_id text`, `occurred_at_utc timestamptz NOT NULL`
- **FK:** `bq_lote_id → bq_lotes.bq_lote_id`; `actor_id → internal_users.actor_id`
- **CHECK:** `ck_bq_lifecycle_history_event` (`event IN ('archived','scrapped','restored','retired')`)
- **TRIGGER:** `trg_bq_lifecycle_history_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_bq_lifecycle_history_lote`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N03_bq.sql`

### `bq_utilisation_readings`
- **AREA:** Boquilhas
- **PK:** `bq_utilisation_reading_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `bq_trace_id uuid NOT NULL`, `reading_kind text NOT NULL`, `value numeric(5,2) NOT NULL`, `actor_id text`, `occurred_at_utc timestamptz NOT NULL`
- **FK:** `bq_trace_id → bq_traces.bq_trace_id`; `actor_id → internal_users.actor_id`
- **CHECK:** `ck_bq_utilisation_readings_kind` (`reading_kind IN ('initial','final')`); `ck_bq_utilisation_readings_value` (`value >= 0 AND value <= 100`)
- **TRIGGER:** `trg_bq_utilisation_readings_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_bq_utilisation_readings_trace`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N03_bq.sql`

---

## 10. Reparação Interna

### `internal_repair_records`
- **AREA:** Reparação Interna
- **PK:** `internal_repair_record_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `line text NOT NULL`, `job_on_id uuid` (no FK), `tool_type text NOT NULL`, `individual_number text NOT NULL`, `operator_id text`, `occurred_at_utc timestamptz NOT NULL`, `correction_of_id uuid`, `before_snapshot jsonb`, `correction_reason text`, `created_at_utc`, `created_by text`, `job_on_revision_id uuid`, `production_code text`, `reference text`, `lot_id uuid` (no FK)
- **FK:** `correction_of_id → internal_repair_records.internal_repair_record_id` (self); `operator_id → internal_users.actor_id`; `created_by → internal_users.actor_id`; `job_on_revision_id → job_on_revision.job_on_revision_id`
- **CHECK:** `ck_internal_repair_records_type` → `tool_type IN ('CM','MF','BQ')`; `ck_internal_repair_records_correction` (`(correction_of_id IS NULL) = (before_snapshot IS NULL)`)
- **INDEXES:** `ix_internal_repair_records_line`, `ix_internal_repair_records_job_on`, `ix_internal_repair_records_revision`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · `database\migrations\N08_reparacoes.sql` (`. N22_reparacao_interna_context.sql`)

---

## 11. Peso

### `peso_references`
- **AREA:** Peso
- **PK:** `peso_reference_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `mold_number text NOT NULL`, `neckring_number text NOT NULL`, `counter_mold text`, `capacity numeric(18,4)`, `volume_neck numeric(18,4)`, `volume_pu numeric(18,4)`, `calote_tp numeric(18,4)`, `change_log jsonb NOT NULL DEFAULT '[]'`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `created_by → internal_users.actor_id`
- **UNIQUE:** `uq_peso_references_mold_neckring (mold_number, neckring_number)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N06_peso.sql`

### `peso_lotes`
- **AREA:** Peso
- **PK:** `peso_lote_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `peso_reference_id uuid NOT NULL`, `lote text NOT NULL`, `processo text NOT NULL`, `allowed_lines text[] NOT NULL`, `report_subfolder text NOT NULL`, `nominal_weight numeric(18,4)`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `peso_reference_id → peso_references.peso_reference_id`; `created_by → internal_users.actor_id`
- **UNIQUE:** `uq_peso_lotes_reference_lote (peso_reference_id, lote)`
- **CHECK:** `ck_peso_lotes_processo` (`processo IN ('NNPB','PS')`); `ck_peso_lotes_allowed_lines` (`cardinality(allowed_lines) >= 1`)
- **INDEXES:** `ix_peso_lotes_reference`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N06_peso.sql`

### `peso_controlos`
- **AREA:** Peso
- **PK:** `peso_controlo_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `peso_reference_id uuid NOT NULL`, `peso_lote_id uuid NOT NULL`, `record_type text NOT NULL`, `mold_number text NOT NULL`, `neckring_number text NOT NULL`, `production_code text NOT NULL`, `line text NOT NULL`, `lote text NOT NULL`, `control_date date NOT NULL`, `job_on_id uuid NOT NULL`, `job_on_revision_id uuid NOT NULL`, `cm_snapshot jsonb`, `status text NOT NULL DEFAULT 'rascunho'`, `measurements_snapshot jsonb NOT NULL DEFAULT '{}'`, `approval_log jsonb NOT NULL DEFAULT '[]'`, `previous_control jsonb`, `comparison_decisions jsonb`, `approved_by text`, `approved_at_utc timestamptz`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `peso_reference_id → peso_references.peso_reference_id`; `peso_lote_id → peso_lotes.peso_lote_id`; `job_on_id → job_on.job_on_id` (cross-area); `job_on_revision_id → job_on_revision.job_on_revision_id` (cross-area); `approved_by → internal_users.actor_id`; `created_by → internal_users.actor_id`
- **UNIQUE:** `uq_peso_controlos_identity (mold_number, neckring_number, production_code, line, lote, control_date)`
- **CHECK:** `ck_peso_controlos_record_type` (`record_type IN ('novo_controlo','comparacao')`); `ck_peso_controlos_status` (`status IN ('rascunho','pendente','aprovado','nao_aprovado')`); `ck_peso_controlos_approved_consistent` (`(status='aprovado')=(approved_at_utc IS NOT NULL)`)
- **TRIGGER:** `trg_peso_controlos_approved_guard` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_peso_controlos_reference`, `ix_peso_controlos_job_on`, `ix_peso_controlos_job_on_revision`, `ix_peso_controlos_status_date (status, control_date)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N06_peso.sql`

### `peso_leituras`
- **AREA:** Peso
- **PK:** `peso_leitura_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `peso_controlo_id uuid NOT NULL`, `cm_number text NOT NULL`, `readings jsonb NOT NULL DEFAULT '{}'`, `created_at_utc`, `created_by text`
- **FK:** `peso_controlo_id → peso_controlos.peso_controlo_id` (ON DELETE CASCADE); `created_by → internal_users.actor_id`
- **UNIQUE:** `uq_peso_leituras_controlo_cm (peso_controlo_id, cm_number)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N06_peso.sql`

### `peso_comparacao_anterior`
- **AREA:** Peso
- **PK:** `peso_controlo_id uuid` (also FK → `peso_controlos`, ON DELETE CASCADE)
- **IMPORTANT COLUMNS:** `previous_peso_controlo_id uuid`, `previous_snapshot jsonb`, `deltas jsonb`, `resolved_at_utc timestamptz NOT NULL`
- **FK:** `peso_controlo_id → peso_controlos.peso_controlo_id` (ON DELETE CASCADE); `previous_peso_controlo_id → peso_controlos.peso_controlo_id`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N06_peso.sql`

### `peso_day_approvals`
- **AREA:** Peso
- **PK:** `peso_day_approval_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `mold_number text NOT NULL`, `neckring_number text NOT NULL`, `line text NOT NULL`, `approval_date date NOT NULL`, `approved_by text`, `approved_at_utc timestamptz NOT NULL`, `notes text`
- **FK:** `approved_by → internal_users.actor_id`
- **UNIQUE:** `uq_peso_day_approvals_identity (mold_number, neckring_number, line, approval_date)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N06_peso.sql`

### `peso_settings`
- **AREA:** Peso
- **PK:** `setting_key text`
- **IMPORTANT COLUMNS:** `setting_value jsonb NOT NULL`, `updated_at_utc timestamptz NOT NULL`, `updated_by text`
- **FK:** `updated_by → internal_users.actor_id`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N06_peso.sql`

---

## 12. Pegamentos

### `pegamento_controlos`
- **AREA:** Pegamentos
- **PK:** `pegamento_controlo_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `job_on_id uuid NOT NULL`, `job_on_revision_id uuid NOT NULL`, `reference_snapshot jsonb`, `production_code text NOT NULL`, `machine_code text NOT NULL`, `cm_snapshot jsonb`, `bq_snapshot jsonb`, `mf_snapshot jsonb`, `nominal_average numeric(18,4)`, `tolerance numeric(6,3) NOT NULL DEFAULT 0.20`, `status text NOT NULL DEFAULT 'aberto'`, `created_at_utc`, `created_by text`, `updated_at_utc`, `cm_nominal numeric(18,4)`, `bq_nominal numeric(18,4)`, `mf_nominal numeric(18,4)`, `notas text`
- **FK:** `job_on_id → job_on.job_on_id` (cross-area); `job_on_revision_id → job_on_revision.job_on_revision_id` (cross-area); `created_by → internal_users.actor_id`
- **CHECK:** `ck_pegamento_controlos_tolerance` (`tolerance >= 0`); `ck_pegamento_controlos_status` (`status IN ('aberto','fechado')`)
- **INDEXES:** `ix_pegamento_controlos_job_on`, `ix_pegamento_controlos_job_on_revision`, `ix_pegamento_controlos_production (production_code, machine_code)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · `database\migrations\N07_pegamentos.sql` (`. N16_pegamentos_component_nominals.sql`, `N17_pegamentos_notas.sql`)

### `pegamento_medicoes`
- **AREA:** Pegamentos
- **PK:** `pegamento_medicao_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `pegamento_controlo_id uuid NOT NULL`, `component_key text NOT NULL`, `costura numeric(18,4) NOT NULL`, `contra_costura numeric(18,4) NOT NULL`, `measured_at_utc timestamptz NOT NULL`, `actor_id text`, `tool_number integer`
- **FK:** `pegamento_controlo_id → pegamento_controlos.pegamento_controlo_id`; `actor_id → internal_users.actor_id`
- **TRIGGER:** `trg_pegamento_medicoes_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_pegamento_medicoes_controlo`, `ix_pegamento_medicoes_component_tool (pegamento_controlo_id, component_key, tool_number)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · `database\migrations\N07_pegamentos.sql` (`. N15_pegamentos_tool_number.sql`)

### `pegamento_documentos`
- **AREA:** Pegamentos
- **PK:** `pegamento_documento_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `pegamento_controlo_id uuid NOT NULL`, `filename text NOT NULL`, `output_root_snapshot text NOT NULL`, `production_folder_snapshot text NOT NULL`, `generated_at_utc timestamptz NOT NULL`, `generated_by text`
- **FK:** `pegamento_controlo_id → pegamento_controlos.pegamento_controlo_id` (UNIQUE, 1:1); `generated_by → internal_users.actor_id`
- **INDEXES:** `ix_pegamento_documentos_controlo`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N14_pegamentos_documents.sql`

---

## 13. Reparação Externa

### `repairers`
- **AREA:** Reparação Externa
- **PK:** `repairer_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `name text NOT NULL`, `active boolean NOT NULL DEFAULT TRUE`, `created_at_utc`, `updated_at_utc`
- **FK:** none outbound
- **REFERENCED BY:** `repair_exits.repairer_id`, `line_repairer_defaults.repairer_id`, `repairer_repair_types.repairer_id`, `bq_movements.noted_repairer_id`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N08_reparacoes.sql`

### `line_repairer_defaults`
- **AREA:** Reparação Externa
- **PK:** `(line, tool_type)` (composite)
- **IMPORTANT COLUMNS:** `repairer_id uuid NOT NULL`, `updated_at_utc timestamptz NOT NULL`, `updated_by text`
- **FK:** `repairer_id → repairers.repairer_id`; `updated_by → internal_users.actor_id`
- **CHECK:** `ck_line_repairer_defaults_type` (`tool_type IN ('BQ','CM','MF')`)
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N08_reparacoes.sql`

### `repair_exits`
- **AREA:** Reparação Externa
- **PK:** `repair_exit_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `repair_type text NOT NULL`, `repairer_id uuid`, `repairer_snapshot jsonb`, `planned_date date`, `status text NOT NULL DEFAULT 'preparacao'`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `repairer_id → repairers.repairer_id`; `created_by → internal_users.actor_id`
- **CHECK:** `ck_repair_exits_type` (`repair_type IN ('BQ','CM','MF')`); `ck_repair_exits_status` (`status IN ('preparacao','a_retirar','enviado','retorno_parcial','concluido','cancelado')`)
- **INDEXES:** `ix_repair_exits_status`, `ix_repair_exits_planned_date`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N08_reparacoes.sql`

### `repair_exit_items`
- **AREA:** Reparação Externa
- **PK:** `repair_exit_item_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `repair_exit_id uuid NOT NULL`, `bq_lote_id uuid`, `physical_piece_id uuid`, `qty numeric(12,2)`, `individual_number text`, `picked boolean NOT NULL DEFAULT FALSE`, `out_at_utc timestamptz`, `out_operator_id text`, `in_at_utc timestamptz`, `in_operator_id text`, `status text NOT NULL DEFAULT 'pendente'`
- **FK:** `repair_exit_id → repair_exits.repair_exit_id`; `bq_lote_id → bq_lotes.bq_lote_id` (cross-area → Boquilhas); `physical_piece_id → physical_pieces.physical_piece_id` (cross-area → Ferramentas); `out_operator_id → internal_users.actor_id`; `in_operator_id → internal_users.actor_id`
- **CHECK:** `ck_repair_exit_items_qty` (`qty IS NULL OR qty >= 0`); `ck_repair_exit_items_kind` (`(bq_lote_id IS NOT NULL AND physical_piece_id IS NULL AND qty IS NOT NULL) OR (bq_lote_id IS NULL AND physical_piece_id IS NOT NULL AND individual_number IS NOT NULL)`); `ck_repair_exit_items_status` (`status IN ('pendente','em_reparacao','devolvido')`)
- **INDEXES:** `ix_repair_exit_items_exit`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · `database\migrations\N08_reparacoes.sql` (`. N25_remediation.sql`)

### `repair_events`
- **AREA:** Reparação Externa
- **PK:** `repair_event_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `repair_scope text NOT NULL`, `repair_exit_item_id uuid`, `internal_repair_record_id uuid`, `canceled boolean NOT NULL DEFAULT FALSE`, `cancel_reason text`, `notes text`, `actor_id text`, `occurred_at_utc timestamptz NOT NULL`
- **FK:** `repair_exit_item_id → repair_exit_items.repair_exit_item_id`; `internal_repair_record_id → internal_repair_records.internal_repair_record_id` (cross-area → Reparação Interna); `actor_id → internal_users.actor_id`
- **CHECK:** `ck_repair_events_scope` (`repair_scope IN ('interna','externa')`)
- **TRIGGER:** `trg_repair_events_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_repair_events_exit_item`, `ix_repair_events_internal`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N08_reparacoes.sql`

### `repairer_repair_types`
- **AREA:** Reparação Externa
- **PK:** `(repairer_id, repair_type)` (composite)
- **IMPORTANT COLUMNS:** `repairer_id uuid NOT NULL`, `repair_type text NOT NULL`
- **FK:** `repairer_id → repairers.repairer_id`
- **CHECK:** `ck_repairer_repair_types_type` (`repair_type IN ('CM','MF','BQ')`)
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N20_repairer_repair_types.sql`

---

## 14. Tampões

### `tampao_field_defs`
- **AREA:** Tampões
- **PK:** `tampao_field_def_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `field_name text NOT NULL UNIQUE`, `unit text`, `precision_digits integer`, `display_order integer NOT NULL DEFAULT 0`, `active boolean NOT NULL DEFAULT TRUE`, `created_at_utc`, `updated_at_utc`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N10_tampoes.sql`

### `tampao_field_values`
- **AREA:** Tampões
- **PK:** `tampao_field_value_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `tampao_field_def_id uuid NOT NULL`, `value_numeric numeric(18,4) NOT NULL`, `value_label text NOT NULL`, `display_order integer NOT NULL DEFAULT 0`, `active boolean NOT NULL DEFAULT TRUE`, `created_at_utc`, `updated_at_utc`
- **FK:** `tampao_field_def_id → tampao_field_defs.tampao_field_def_id`
- **UNIQUE:** `uq_tampao_field_values (tampao_field_def_id, value_numeric)`
- **INDEXES:** `ix_tampao_field_values_field (tampao_field_def_id, active, value_numeric)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N10_tampoes.sql`

### `tampao_configurations`
- **AREA:** Tampões
- **PK:** `tampao_configuration_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `values_json jsonb NOT NULL`, `active boolean NOT NULL DEFAULT TRUE`, `created_at_utc`, `created_by text`
- **FK:** `created_by → internal_users.actor_id`
- **UNIQUE:** `uq_tampao_configurations_values (values_json)`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N10_tampoes.sql`

### `tampao_saldos`
- **AREA:** Tampões
- **PK:** `tampao_saldo_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `tampao_configuration_id uuid NOT NULL UNIQUE`, `enchidos integer NOT NULL DEFAULT 0`, `por_encher integer NOT NULL DEFAULT 0`, `updated_at_utc`
- **FK:** `tampao_configuration_id → tampao_configurations.tampao_configuration_id` (UNIQUE, 1:1)
- **CHECK:** `ck_tampao_saldos_enchidos` (`enchidos >= 0`); `ck_tampao_saldos_por_encher` (`por_encher >= 0`)
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N10_tampoes.sql`

### `tampao_movements`
- **AREA:** Tampões
- **PK:** `tampao_movement_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `movement_type text NOT NULL`, `origin_configuration_id uuid`, `destination_configuration_id uuid`, `qty integer NOT NULL`, `balances_before jsonb`, `balances_after jsonb`, `actor_id text`, `occurred_at_utc timestamptz NOT NULL`
- **FK:** `origin_configuration_id → tampao_configurations.tampao_configuration_id`; `destination_configuration_id → tampao_configurations.tampao_configuration_id`; `actor_id → internal_users.actor_id`
- **CHECK:** `ck_tampao_movements_type` (`movement_type IN ('adicionar','remover','alterar_estado','alterar_configuracao')`); `ck_tampao_movements_qty` (`qty >= 1`)
- **TRIGGER:** `trg_tampao_movements_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_tampao_movements_origin`, `ix_tampao_movements_occurred`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N10_tampoes.sql`

### `tampao_planos`
- **AREA:** Tampões
- **PK:** `tampao_plano_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `tampao_configuration_id uuid NOT NULL`, `planned_qty integer NOT NULL`, `planned_for_date date`, `job_on_id uuid` (no FK), `production_code text`, `notes text`, `canceled boolean NOT NULL DEFAULT FALSE`, `created_at_utc`, `created_by text`, `updated_at_utc`
- **FK:** `tampao_configuration_id → tampao_configurations.tampao_configuration_id`; `created_by → internal_users.actor_id`
- **CHECK:** `ck_tampao_planos_qty` (`planned_qty >= 1`)
- **INDEXES:** `ix_tampao_planos_configuration`, `ix_tampao_planos_date`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N10_tampoes.sql`

### `tampao_configuration_machines`
- **AREA:** Tampões
- **PK:** `(tampao_configuration_id, machine)` (composite)
- **IMPORTANT COLUMNS:** `tampao_configuration_id uuid NOT NULL`, `machine text NOT NULL`
- **FK:** `tampao_configuration_id → tampao_configurations.tampao_configuration_id`
- **CHECK:** `ck_tampao_configuration_machines_machine` (`machine IN ('B1','B2','B3','C1','C2','C3')`)
- **INDEXES:** `ix_tampao_configuration_machines_machine`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N21_tampoes_machines.sql`

### `tampao_configuration_notes`
- **AREA:** Tampões
- **PK:** `tampao_configuration_note_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `tampao_configuration_id uuid NOT NULL`, `note text NOT NULL`, `actor_id text`, `occurred_at_utc timestamptz NOT NULL`
- **FK:** `tampao_configuration_id → tampao_configurations.tampao_configuration_id`; `actor_id → internal_users.actor_id`
- **TRIGGER:** `trg_tampao_configuration_notes_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_tampao_configuration_notes_config`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N21_tampoes_machines.sql`

### `tampao_configuration_machine_event`
- **AREA:** Tampões
- **PK:** `tampao_configuration_machine_event_id uuid DEFAULT gen_random_uuid()`
- **IMPORTANT COLUMNS:** `tampao_configuration_id uuid NOT NULL`, `machine text NOT NULL`, `action text NOT NULL`, `actor_id text`, `occurred_at_utc timestamptz NOT NULL`
- **FK:** `tampao_configuration_id → tampao_configurations.tampao_configuration_id`; `actor_id → internal_users.actor_id`
- **CHECK:** `ck_tampao_configuration_machine_event_action` (`action IN ('added','removed')`); `ck_tampao_configuration_machine_event_machine` (`machine IN ('B1','B2','B3','C1','C2','C3')`)
- **TRIGGER:** `trg_tampao_configuration_machine_event_append_only` (BEFORE UPDATE OR DELETE)
- **INDEXES:** `ix_tampao_configuration_machine_event_config`
- **RLS / POLICY:** `ba_dmo_app_access`
- **SOURCE:** `database\consolidated_clean_install.sql` · introduced in `database\migrations\N21_tampoes_machines.sql`

---

## 15. Modules With No Dedicated Database Tables

Navigation only: modules with no table named for them in the current schema, and the database tables associated with them (no functional explanation).

| Canonical module | Dedicated tables? | Associated database tables |
|---|---|---|
| História | No `historia_*` table | `audit_events`, `job_on_audit_event`, `controlo_sheet_events`, `repair_events`, plus append-only movement tables |
| Admin | No `admin_*` table | `access_templates`, `internal_users`, `module_catalog_mirror` |
| Users / Access | No own table | `internal_users`, `access_templates`, `module_catalog_mirror` |
| Design Laboratório | No table found | — |
| Login | No `login_*` table | `internal_users.auth_user_id` column exists; no persisted session table |

---

## Primary Key Index

Every current table has one PRIMARY KEY. Guid PKs use `gen_random_uuid()` (48 tables); text/composite PKs are listed explicitly.

| Table | PK Column(s) | Type |
|---|---|---|
| `schema_migrations` | `version` | text |
| `access_templates` | `template_id` | text |
| `internal_users` | `actor_id` | text |
| `audit_events` | `audit_event_id` | uuid |
| `module_catalog_mirror` | `module_id` | text |
| `bq_lotes` | `bq_lote_id` | uuid |
| `bq_traces` | `bq_trace_id` | uuid |
| `bq_movements` | `bq_movement_id` | uuid |
| `bq_discrepancies` | `bq_discrepancy_id` | uuid |
| `bq_lifecycle_history` | `bq_lifecycle_history_id` | uuid |
| `bq_utilisation_readings` | `bq_utilisation_reading_id` | uuid |
| `tool_references` | `tool_reference_id` | uuid |
| `tool_lotes` | `tool_lote_id` | uuid |
| `physical_pieces` | `physical_piece_id` | uuid |
| `tool_check_rules` | `tool_check_rule_id` | uuid |
| `tool_check_occurrences` | `tool_check_occurrence_id` | uuid |
| `tool_usage_records` | `tool_usage_record_id` | uuid |
| `job_on` | `job_on_id` | uuid |
| `job_on_revision` | `job_on_revision_id` | uuid |
| `job_on_component` | `job_on_component_id` | uuid |
| `job_on_component_field` | `job_on_component_field_id` | uuid |
| `job_on_component_row` | `job_on_component_row_id` | uuid |
| `job_on_verification_occurrence` | `job_on_verification_occurrence_id` | uuid |
| `job_on_audit_event` | `job_on_audit_event_id` | uuid |
| `job_on_field_option` | `job_on_field_option_id` | uuid |
| `jobon_user_current` | `actor_id` | text (FK) |
| `peso_references` | `peso_reference_id` | uuid |
| `peso_lotes` | `peso_lote_id` | uuid |
| `peso_controlos` | `peso_controlo_id` | uuid |
| `peso_leituras` | `peso_leitura_id` | uuid |
| `peso_comparacao_anterior` | `peso_controlo_id` | uuid (FK) |
| `peso_day_approvals` | `peso_day_approval_id` | uuid |
| `peso_settings` | `setting_key` | text |
| `pegamento_controlos` | `pegamento_controlo_id` | uuid |
| `pegamento_medicoes` | `pegamento_medicao_id` | uuid |
| `pegamento_documentos` | `pegamento_documento_id` | uuid |
| `repairers` | `repairer_id` | uuid |
| `line_repairer_defaults` | `(line, tool_type)` | (text, text) |
| `repair_exits` | `repair_exit_id` | uuid |
| `repair_exit_items` | `repair_exit_item_id` | uuid |
| `repair_events` | `repair_event_id` | uuid |
| `internal_repair_records` | `internal_repair_record_id` | uuid |
| `repairer_repair_types` | `(repairer_id, repair_type)` | (uuid, text) |
| `warehouse_locations` | `warehouse_location_id` | uuid |
| `warehouse_stock` | `warehouse_stock_id` | uuid |
| `warehouse_movements` | `warehouse_movement_id` | uuid |
| `tampao_field_defs` | `tampao_field_def_id` | uuid |
| `tampao_field_values` | `tampao_field_value_id` | uuid |
| `tampao_configurations` | `tampao_configuration_id` | uuid |
| `tampao_saldos` | `tampao_saldo_id` | uuid |
| `tampao_movements` | `tampao_movement_id` | uuid |
| `tampao_planos` | `tampao_plano_id` | uuid |
| `tampao_configuration_machines` | `(tampao_configuration_id, machine)` | (uuid, text) |
| `tampao_configuration_notes` | `tampao_configuration_note_id` | uuid |
| `tampao_configuration_machine_event` | `tampao_configuration_machine_event_id` | uuid |
| `app_settings` | `setting_key` | text |
| `controlo_sheets` | `controlo_sheet_id` | uuid |
| `controlo_sheet_items` | `controlo_sheet_item_id` | uuid |
| `controlo_sheet_events` | `controlo_sheet_event_id` | uuid |

**Primary keys mapped: 59.**

---

## Foreign Key Index

All 118 current foreign keys. Delete action defaults to `NO ACTION` unless noted.

| Source Table.Column | Target Table.Column | Delete Action |
|---|---|---|
| `internal_users.template_id` | `access_templates.template_id` | NO ACTION |
| `bq_lotes.created_by` | `internal_users.actor_id` | NO ACTION |
| `bq_traces.bq_lote_id` | `bq_lotes.bq_lote_id` | NO ACTION |
| `bq_traces.created_by` | `internal_users.actor_id` | NO ACTION |
| `bq_movements.bq_trace_id` | `bq_traces.bq_trace_id` | NO ACTION |
| `bq_movements.actor_id` | `internal_users.actor_id` | NO ACTION |
| `bq_movements.noted_repairer_id` | `repairers.repairer_id` | NO ACTION |
| `bq_discrepancies.bq_lote_id` | `bq_lotes.bq_lote_id` | NO ACTION |
| `bq_discrepancies.bq_trace_id` | `bq_traces.bq_trace_id` | NO ACTION |
| `bq_discrepancies.resolved_by` | `internal_users.actor_id` | NO ACTION |
| `bq_discrepancies.created_by` | `internal_users.actor_id` | NO ACTION |
| `bq_lifecycle_history.bq_lote_id` | `bq_lotes.bq_lote_id` | NO ACTION |
| `bq_lifecycle_history.actor_id` | `internal_users.actor_id` | NO ACTION |
| `bq_utilisation_readings.bq_trace_id` | `bq_traces.bq_trace_id` | NO ACTION |
| `bq_utilisation_readings.actor_id` | `internal_users.actor_id` | NO ACTION |
| `tool_references.created_by` | `internal_users.actor_id` | NO ACTION |
| `tool_lotes.tool_reference_id` | `tool_references.tool_reference_id` | NO ACTION |
| `tool_lotes.created_by` | `internal_users.actor_id` | NO ACTION |
| `physical_pieces.tool_lote_id` | `tool_lotes.tool_lote_id` | NO ACTION |
| `physical_pieces.created_by` | `internal_users.actor_id` | NO ACTION |
| `tool_check_rules.tool_lote_id` | `tool_lotes.tool_lote_id` | NO ACTION |
| `tool_check_rules.copied_from_rule_id` | `tool_check_rules.tool_check_rule_id` | NO ACTION |
| `tool_check_rules.created_by` | `internal_users.actor_id` | NO ACTION |
| `tool_check_occurrences.tool_check_rule_id` | `tool_check_rules.tool_check_rule_id` | NO ACTION |
| `tool_check_occurrences.completed_by` | `internal_users.actor_id` | NO ACTION |
| `tool_check_occurrences.created_by` | `internal_users.actor_id` | NO ACTION |
| `job_on.copied_from_job_on_id` | `job_on.job_on_id` | NO ACTION |
| `job_on.current_revision_id` | `job_on_revision.job_on_revision_id` | NO ACTION |
| `job_on.canceled_by` | `internal_users.actor_id` | NO ACTION |
| `job_on.created_by` | `internal_users.actor_id` | NO ACTION |
| `job_on_revision.job_on_id` | `job_on.job_on_id` | NO ACTION |
| `job_on_revision.saved_by` | `internal_users.actor_id` | NO ACTION |
| `job_on_component.job_on_revision_id` | `job_on_revision.job_on_revision_id` | NO ACTION |
| `job_on_component.source_tool_id` | `tool_references.tool_reference_id` | NO ACTION |
| `job_on_component.source_lot_id` | `tool_lotes.tool_lote_id` | NO ACTION |
| `job_on_component_field.job_on_component_id` | `job_on_component.job_on_component_id` | NO ACTION |
| `job_on_component_row.job_on_component_id` | `job_on_component.job_on_component_id` | NO ACTION |
| `job_on_verification_occurrence.job_on_component_id` | `job_on_component.job_on_component_id` | NO ACTION |
| `job_on_verification_occurrence.source_rule_id` | `tool_check_rules.tool_check_rule_id` | NO ACTION |
| `job_on_verification_occurrence.completed_by` | `internal_users.actor_id` | NO ACTION |
| `job_on_audit_event.job_on_id` | `job_on.job_on_id` | NO ACTION |
| `job_on_audit_event.job_on_revision_id` | `job_on_revision.job_on_revision_id` | NO ACTION |
| `job_on_audit_event.actor_id` | `internal_users.actor_id` | NO ACTION |
| `peso_references.created_by` | `internal_users.actor_id` | NO ACTION |
| `peso_lotes.peso_reference_id` | `peso_references.peso_reference_id` | NO ACTION |
| `peso_lotes.created_by` | `internal_users.actor_id` | NO ACTION |
| `peso_controlos.peso_reference_id` | `peso_references.peso_reference_id` | NO ACTION |
| `peso_controlos.peso_lote_id` | `peso_lotes.peso_lote_id` | NO ACTION |
| `peso_controlos.job_on_id` | `job_on.job_on_id` | NO ACTION |
| `peso_controlos.job_on_revision_id` | `job_on_revision.job_on_revision_id` | NO ACTION |
| `peso_controlos.approved_by` | `internal_users.actor_id` | NO ACTION |
| `peso_controlos.created_by` | `internal_users.actor_id` | NO ACTION |
| `peso_leituras.peso_controlo_id` | `peso_controlos.peso_controlo_id` | CASCADE |
| `peso_leituras.created_by` | `internal_users.actor_id` | NO ACTION |
| `peso_comparacao_anterior.peso_controlo_id` | `peso_controlos.peso_controlo_id` | CASCADE |
| `peso_comparacao_anterior.previous_peso_controlo_id` | `peso_controlos.peso_controlo_id` | NO ACTION |
| `peso_day_approvals.approved_by` | `internal_users.actor_id` | NO ACTION |
| `peso_settings.updated_by` | `internal_users.actor_id` | NO ACTION |
| `pegamento_controlos.job_on_id` | `job_on.job_on_id` | NO ACTION |
| `pegamento_controlos.job_on_revision_id` | `job_on_revision.job_on_revision_id` | NO ACTION |
| `pegamento_controlos.created_by` | `internal_users.actor_id` | NO ACTION |
| `pegamento_medicoes.pegamento_controlo_id` | `pegamento_controlos.pegamento_controlo_id` | NO ACTION |
| `pegamento_medicoes.actor_id` | `internal_users.actor_id` | NO ACTION |
| `pegamento_documentos.pegamento_controlo_id` | `pegamento_controlos.pegamento_controlo_id` | NO ACTION |
| `pegamento_documentos.generated_by` | `internal_users.actor_id` | NO ACTION |
| `line_repairer_defaults.repairer_id` | `repairers.repairer_id` | NO ACTION |
| `line_repairer_defaults.updated_by` | `internal_users.actor_id` | NO ACTION |
| `repair_exits.repairer_id` | `repairers.repairer_id` | NO ACTION |
| `repair_exits.created_by` | `internal_users.actor_id` | NO ACTION |
| `repair_exit_items.repair_exit_id` | `repair_exits.repair_exit_id` | NO ACTION |
| `repair_exit_items.bq_lote_id` | `bq_lotes.bq_lote_id` | NO ACTION |
| `repair_exit_items.physical_piece_id` | `physical_pieces.physical_piece_id` | NO ACTION |
| `repair_exit_items.out_operator_id` | `internal_users.actor_id` | NO ACTION |
| `repair_exit_items.in_operator_id` | `internal_users.actor_id` | NO ACTION |
| `internal_repair_records.correction_of_id` | `internal_repair_records.internal_repair_record_id` | NO ACTION |
| `internal_repair_records.operator_id` | `internal_users.actor_id` | NO ACTION |
| `internal_repair_records.created_by` | `internal_users.actor_id` | NO ACTION |
| `internal_repair_records.job_on_revision_id` | `job_on_revision.job_on_revision_id` | NO ACTION |
| `repair_events.repair_exit_item_id` | `repair_exit_items.repair_exit_item_id` | NO ACTION |
| `repair_events.internal_repair_record_id` | `internal_repair_records.internal_repair_record_id` | NO ACTION |
| `repair_events.actor_id` | `internal_users.actor_id` | NO ACTION |
| `repairer_repair_types.repairer_id` | `repairers.repairer_id` | NO ACTION |
| `warehouse_locations.created_by` | `internal_users.actor_id` | NO ACTION |
| `warehouse_stock.warehouse_location_id` | `warehouse_locations.warehouse_location_id` | NO ACTION |
| `warehouse_stock.tool_lote_id` | `tool_lotes.tool_lote_id` | NO ACTION |
| `warehouse_stock.occupied_by` | `internal_users.actor_id` | NO ACTION |
| `warehouse_stock.released_by` | `internal_users.actor_id` | NO ACTION |
| `warehouse_movements.warehouse_stock_id` | `warehouse_stock.warehouse_stock_id` | NO ACTION |
| `warehouse_movements.repair_exit_id` | `repair_exits.repair_exit_id` | NO ACTION |
| `warehouse_movements.actor_id` | `internal_users.actor_id` | NO ACTION |
| `tampao_field_values.tampao_field_def_id` | `tampao_field_defs.tampao_field_def_id` | NO ACTION |
| `tampao_configurations.created_by` | `internal_users.actor_id` | NO ACTION |
| `tampao_saldos.tampao_configuration_id` | `tampao_configurations.tampao_configuration_id` | NO ACTION |
| `tampao_movements.origin_configuration_id` | `tampao_configurations.tampao_configuration_id` | NO ACTION |
| `tampao_movements.destination_configuration_id` | `tampao_configurations.tampao_configuration_id` | NO ACTION |
| `tampao_movements.actor_id` | `internal_users.actor_id` | NO ACTION |
| `tampao_planos.tampao_configuration_id` | `tampao_configurations.tampao_configuration_id` | NO ACTION |
| `tampao_planos.created_by` | `internal_users.actor_id` | NO ACTION |
| `tampao_configuration_machines.tampao_configuration_id` | `tampao_configurations.tampao_configuration_id` | NO ACTION |
| `tampao_configuration_notes.tampao_configuration_id` | `tampao_configurations.tampao_configuration_id` | NO ACTION |
| `tampao_configuration_notes.actor_id` | `internal_users.actor_id` | NO ACTION |
| `tampao_configuration_machine_event.tampao_configuration_id` | `tampao_configurations.tampao_configuration_id` | NO ACTION |
| `tampao_configuration_machine_event.actor_id` | `internal_users.actor_id` | NO ACTION |
| `tool_usage_records.tool_lote_id` | `tool_lotes.tool_lote_id` | NO ACTION |
| `tool_usage_records.actor_id` | `internal_users.actor_id` | NO ACTION |
| `app_settings.updated_by` | `internal_users.actor_id` | NO ACTION |
| `controlo_sheets.job_on_id` | `job_on.job_on_id` | NO ACTION |
| `controlo_sheets.job_on_revision_id` | `job_on_revision.job_on_revision_id` | NO ACTION |
| `controlo_sheets.created_by` | `internal_users.actor_id` | NO ACTION |
| `controlo_sheets.submitted_by` | `internal_users.actor_id` | NO ACTION |
| `controlo_sheets.decided_by` | `internal_users.actor_id` | NO ACTION |
| `controlo_sheet_items.controlo_sheet_id` | `controlo_sheets.controlo_sheet_id` | CASCADE |
| `controlo_sheet_items.source_tool_id` | `tool_references.tool_reference_id` | NO ACTION |
| `controlo_sheet_items.source_lot_id` | `tool_lotes.tool_lote_id` | NO ACTION |
| `controlo_sheet_events.controlo_sheet_id` | `controlo_sheets.controlo_sheet_id` | CASCADE |
| `controlo_sheet_events.actor_id` | `internal_users.actor_id` | NO ACTION |
| `jobon_user_current.actor_id` | `internal_users.actor_id` | NO ACTION |
| `jobon_user_current.job_on_id` | `job_on.job_on_id` | NO ACTION |

**Foreign keys mapped: 118.**

---

## Identifier-like Columns Without FK Constraints

Database facts: identifier-shaped UUID/text columns with no FK constraint in the current schema.

- `internal_repair_records.job_on_id`
- `internal_repair_records.lot_id`
- `job_on.article_reference_id`
- `tool_check_occurrences.job_on_id`
- `tool_check_occurrences.job_on_component_id`
- `audit_events.job_on_id`
- `audit_events.revision_id`
- `tampao_planos.job_on_id`

---

## Unique Constraints

All uniqueness rules, split between UNIQUE constraints (18) and partial UNIQUE indexes (3; counted separately). Total unique structures: 21.

### Named UNIQUE constraints (15)

| Table | Constraint | Columns / Expression |
|---|---|---|
| `bq_lotes` | `uq_bq_lotes_reference_batch` | `(reference, batch_code)` |
| `internal_users` | `uq_internal_users_auth_user` | `(auth_user_id)` |
| `job_on_component_field` | `uq_job_on_component_field` | `(job_on_component_id, field_key)` |
| `job_on_field_option` | `uq_job_on_field_option` | `(family, field_key, option_value)` |
| `job_on_revision` | `uq_job_on_revision_number` | `(job_on_id, revision_number)` |
| `peso_controlos` | `uq_peso_controlos_identity` | `(mold_number, neckring_number, production_code, line, lote, control_date)` |
| `peso_day_approvals` | `uq_peso_day_approvals_identity` | `(mold_number, neckring_number, line, approval_date)` |
| `peso_leituras` | `uq_peso_leituras_controlo_cm` | `(peso_controlo_id, cm_number)` |
| `peso_lotes` | `uq_peso_lotes_reference_lote` | `(peso_reference_id, lote)` |
| `peso_references` | `uq_peso_references_mold_neckring` | `(mold_number, neckring_number)` |
| `physical_pieces` | `uq_physical_pieces_lote_number` | `(tool_lote_id, number)` |
| `tampao_configurations` | `uq_tampao_configurations_values` | `(values_json)` |
| `tampao_field_values` | `uq_tampao_field_values` | `(tampao_field_def_id, value_numeric)` |
| `tool_lotes` | `uq_tool_lotes_reference_lote` | `(tool_reference_id, lote)` |
| `tool_references` | `uq_tool_references_type_code` | `(tool_type, ref_code)` |

### Inline UNIQUE (3)

| Table | Columns |
|---|---|
| `warehouse_locations` | `code` |
| `tampao_saldos` | `tampao_configuration_id` |
| `pegamento_documentos` | `pegamento_controlo_id` |

### Partial UNIQUE indexes (3)

| Table | Index | Columns | Predicate | Mechanical effect |
|---|---|---|---|---|
| `warehouse_stock` | `uq_warehouse_stock_active_occupation` | `(warehouse_location_id, tool_lote_id)` | `WHERE released_at_utc IS NULL` | at most one active occupation per location+lot |
| `job_on` | `uq_job_on_identity` | `(production_code, machine_code)` | `WHERE canceled_at_utc IS NULL` | at most one non-canceled row per production+machine |
| `bq_traces` | `uq_bq_traces_active` | `(bq_lote_id)` | `WHERE status = 'active'` | at most one active row per `bq_lote_id` |

---

## Check Constraints

All 64 CHECK constraints.

| Table | Constraint | Rule |
|---|---|---|
| `audit_events` | `ck_audit_events_year_positive` | `year > 0` |
| `audit_events` | `ck_audit_events_result` | `result IN ('succeeded','failed','denied','corrected')` |
| `bq_lotes` | `ck_bq_lotes_reference` | `reference ~ '^[A-Z][0-9]{3}$'` |
| `bq_lotes` | `ck_bq_lotes_lifecycle` | `lifecycle_state IN ('available','archived','scrapped')` |
| `bq_traces` | `ck_bq_traces_status` | `status IN ('active','closed')` |
| `bq_traces` | `ck_bq_traces_purpose` | `purpose IN ('production','repair')` |
| `bq_traces` | `ck_bq_traces_sap_start` | `sap_start IS NULL OR (sap_start >= 0 AND sap_start <= 100)` |
| `bq_traces` | `ck_bq_traces_sap_end` | `sap_end IS NULL OR (sap_end >= 0 AND sap_end <= 100)` |
| `bq_movements` | `ck_bq_movements_type` | `movement_type IN ('inicio','saida','entrada','irreparavel','linha','contagem','fim')` |
| `bq_movements` | `ck_bq_movements_qty` | `qty IS NOT NULL OR movement_type = 'linha'` |
| `bq_movements` | `ck_bq_movements_exceptional` | `exceptional_received_qty IS NULL OR exceptional_received_qty >= 0` |
| `bq_discrepancies` | `ck_bq_discrepancies_status` | `status IN ('open','under_review','resolved')` |
| `bq_lifecycle_history` | `ck_bq_lifecycle_history_event` | `event IN ('archived','scrapped','restored','retired')` |
| `bq_utilisation_readings` | `ck_bq_utilisation_readings_kind` | `reading_kind IN ('initial','final')` |
| `bq_utilisation_readings` | `ck_bq_utilisation_readings_value` | `value >= 0 AND value <= 100` |
| `tool_references` | `ck_tool_references_type` | `tool_type IN ('CM','MF','BQ','PU','CS')` |
| `tool_lotes` | `ck_tool_lotes_qty` | `qty IS NULL OR qty >= 0` |
| `physical_pieces` | `ck_physical_pieces_sequence` | `sequence >= 1` |
| `tool_check_rules` | `ck_tool_check_rules_frequency` | `frequency IN ('uma_vez_no_lote','por_fabrico')` |
| `tool_check_occurrences` | `ck_tool_check_occurrences_status` | `status IN ('pendente','confirmada','reposta','desativada')` |
| `tool_check_occurrences` | `ck_tool_check_occurrences_source` | `completion_source = 'manual_job_on'` |
| `tool_check_occurrences` | `ck_tool_check_occurrences_completed` | `(status IN ('confirmada','reposta')) = (completed_at_utc IS NOT NULL)` |
| `tool_usage_records` | `ck_tool_usage_records_sap_start` | `sap_start IS NULL OR (0..100)` |
| `tool_usage_records` | `ck_tool_usage_records_sap_end` | `sap_end IS NULL OR (0..100)` |
| `tool_usage_records` | `ck_tool_usage_records_percent` | `percent_used IS NULL OR (0..100)` |
| `tool_usage_records` | `ck_tool_usage_records_cumulative` | `value_cumulative >= 0` |
| `job_on` | `ck_job_on_status` | `status IN ('rascunho','planeado','em_fabrico','fechado','cancelado')` |
| `job_on` | `ck_job_on_lifecycle_consistent` | `(status='fechado')=(closed_at_utc IS NOT NULL) AND (status='cancelado')=(canceled_at_utc IS NOT NULL)` |
| `job_on_revision` | `ck_job_on_revision_number` | `revision_number >= 1` |
| `job_on_component` | `ck_job_on_component_family` | `family IN ('MP_CM','MF','BQ','PU','CAL','AN','ARR','PI','CS','TP','FO')` |
| `job_on_component_field` | `ck_job_on_component_field_type` | `value_type IN ('text','integer','decimal','boolean','date','select')` |
| `job_on_verification_occurrence` | `ck_job_on_verification_status` | `status IN ('pendente','confirmada','reposta','desativada')` |
| `job_on_verification_occurrence` | `ck_job_on_verification_source` | `completion_source = 'manual_job_on'` |
| `job_on_verification_occurrence` | `ck_job_on_verification_completed` | `(status IN ('confirmada','reposta')) = (completed_at_utc IS NOT NULL)` |
| `peso_lotes` | `ck_peso_lotes_processo` | `processo IN ('NNPB','PS')` |
| `peso_lotes` | `ck_peso_lotes_allowed_lines` | `cardinality(allowed_lines) >= 1` |
| `peso_controlos` | `ck_peso_controlos_record_type` | `record_type IN ('novo_controlo','comparacao')` |
| `peso_controlos` | `ck_peso_controlos_status` | `status IN ('rascunho','pendente','aprovado','nao_aprovado')` |
| `peso_controlos` | `ck_peso_controlos_approved_consistent` | `(status='aprovado')=(approved_at_utc IS NOT NULL)` |
| `pegamento_controlos` | `ck_pegamento_controlos_tolerance` | `tolerance >= 0` |
| `pegamento_controlos` | `ck_pegamento_controlos_status` | `status IN ('aberto','fechado')` |
| `line_repairer_defaults` | `ck_line_repairer_defaults_type` | `tool_type IN ('BQ','CM','MF')` |
| `repair_exits` | `ck_repair_exits_type` | `repair_type IN ('BQ','CM','MF')` |
| `repair_exits` | `ck_repair_exits_status` | `status IN ('preparacao','a_retirar','enviado','retorno_parcial','concluido','cancelado')` |
| `repair_exit_items` | `ck_repair_exit_items_qty` | `qty IS NULL OR qty >= 0` |
| `repair_exit_items` | `ck_repair_exit_items_kind` | `(bq_lote_id IS NOT NULL AND physical_piece_id IS NULL AND qty IS NOT NULL) OR (bq_lote_id IS NULL AND physical_piece_id IS NOT NULL AND individual_number IS NOT NULL)` |
| `repair_exit_items` | `ck_repair_exit_items_status` | `status IN ('pendente','em_reparacao','devolvido')` |
| `internal_repair_records` | `ck_internal_repair_records_type` | `tool_type IN ('CM','MF','BQ')` |
| `internal_repair_records` | `ck_internal_repair_records_correction` | `(correction_of_id IS NULL)=(before_snapshot IS NULL)` |
| `repair_events` | `ck_repair_events_scope` | `repair_scope IN ('interna','externa')` |
| `repairer_repair_types` | `ck_repairer_repair_types_type` | `repair_type IN ('CM','MF','BQ')` |
| `warehouse_movements` | `ck_warehouse_movements_direction` | `direction IN ('in','out')` |
| `tampao_saldos` | `ck_tampao_saldos_enchidos` | `enchidos >= 0` |
| `tampao_saldos` | `ck_tampao_saldos_por_encher` | `por_encher >= 0` |
| `tampao_movements` | `ck_tampao_movements_type` | `movement_type IN ('adicionar','remover','alterar_estado','alterar_configuracao')` |
| `tampao_movements` | `ck_tampao_movements_qty` | `qty >= 1` |
| `tampao_planos` | `ck_tampao_planos_qty` | `planned_qty >= 1` |
| `tampao_configuration_machines` | `ck_tampao_configuration_machines_machine` | `machine IN ('B1','B2','B3','C1','C2','C3')` |
| `tampao_configuration_machine_event` | `ck_tampao_configuration_machine_event_action` | `action IN ('added','removed')` |
| `tampao_configuration_machine_event` | `ck_tampao_configuration_machine_event_machine` | `machine IN ('B1','B2','B3','C1','C2','C3')` |
| `controlo_sheets` | `ck_controlo_sheets_status` | `status IN ('rascunho','submetido','aprovado','rejeitado')` |
| `controlo_sheets` | `ck_controlo_sheets_decision` | `(decided_by IS NULL AND decided_at_utc IS NULL AND decision IS NULL) OR (decided_by IS NOT NULL AND decided_at_utc IS NOT NULL AND decision IN ('aprovado','rejeitado'))` |
| `controlo_sheet_items` | `ck_controlo_sheet_items_result` | `result IS NULL OR result IN ('OK','NOK')` |
| `controlo_sheet_events` | `ck_controlo_sheet_events_type` | `event_type IN ('criar','editar','submeter','reeabrir','decidir')` |

**CHECK constraints mapped: 64.**

---

## Indexes

All current indexes: 75 non-unique + 3 unique (unique indexes listed in Unique Constraints) = 78.

| Index | Table | Columns | Unique | Predicate |
|---|---|---|---|---|
| `ix_access_templates_active` | access_templates | `(active)` | No | — |
| `ix_internal_users_auth_user_id` | internal_users | `(auth_user_id)` | No | — |
| `ix_internal_users_active` | internal_users | `(active)` | No | — |
| `ix_internal_users_template_id` | internal_users | `(template_id)` | No | — |
| `ix_audit_events_year` | audit_events | `(year)` | No | — |
| `ix_audit_events_module_action` | audit_events | `(module_id, action_code)` | No | — |
| `ix_audit_events_actor` | audit_events | `(actor_user_id, year)` | No | — |
| `ix_audit_events_entity` | audit_events | `(entity_type, entity_id)` | No | — |
| `ix_audit_events_occurred_at` | audit_events | `(occurred_at_utc)` | No | — |
| `ix_audit_events_job_on_id` | audit_events | `(job_on_id)` | No | — |
| `ix_audit_events_module_time` | audit_events | `(module_id, occurred_at_utc)` | No | — |
| `ix_module_catalog_mirror_order` | module_catalog_mirror | `(display_order)` | No | — |
| `ix_bq_lotes_lifecycle` | bq_lotes | `(lifecycle_state)` | No | — |
| `ix_bq_traces_lote` | bq_traces | `(bq_lote_id)` | No | — |
| `ix_bq_traces_status` | bq_traces | `(status)` | No | — |
| `ix_bq_movements_trace` | bq_movements | `(bq_trace_id)` | No | — |
| `ix_bq_movements_occurred` | bq_movements | `(occurred_at_utc)` | No | — |
| `ix_bq_discrepancies_lote` | bq_discrepancies | `(bq_lote_id)` | No | — |
| `ix_bq_discrepancies_status` | bq_discrepancies | `(status)` | No | — |
| `ix_bq_lifecycle_history_lote` | bq_lifecycle_history | `(bq_lote_id)` | No | — |
| `ix_bq_utilisation_readings_trace` | bq_utilisation_readings | `(bq_trace_id)` | No | — |
| `ix_tool_lotes_reference` | tool_lotes | `(tool_reference_id)` | No | — |
| `ix_physical_pieces_lote` | physical_pieces | `(tool_lote_id)` | No | — |
| `ix_tool_check_rules_lote` | tool_check_rules | `(tool_lote_id)` | No | — |
| `ix_tool_check_occurrences_rule` | tool_check_occurrences | `(tool_check_rule_id)` | No | — |
| `ix_tool_check_occurrences_job_on` | tool_check_occurrences | `(job_on_id)` | No | — |
| `ix_tool_usage_records_lote` | tool_usage_records | `(tool_lote_id)` | No | — |
| `ix_job_on_production_code` | job_on | `(production_code)` | No | — |
| `ix_job_on_status` | job_on | `(status)` | No | — |
| `ix_job_on_machine_planned` | job_on | `(machine_code, planned_start_at)` | No | — |
| `ix_job_on_revision_job_on` | job_on_revision | `(job_on_id)` | No | — |
| `ix_job_on_component_revision` | job_on_component | `(job_on_revision_id)` | No | — |
| `ix_job_on_component_field_component` | job_on_component_field | `(job_on_component_id)` | No | — |
| `ix_job_on_component_row_component` | job_on_component_row | `(job_on_component_id)` | No | — |
| `ix_job_on_verification_component` | job_on_verification_occurrence | `(job_on_component_id)` | No | — |
| `ix_job_on_audit_event_job_on` | job_on_audit_event | `(job_on_id)` | No | — |
| `ix_job_on_field_option_lookup` | job_on_field_option | `(family, field_key, active)` | No | — |
| `ix_peso_lotes_reference` | peso_lotes | `(peso_reference_id)` | No | — |
| `ix_peso_controlos_reference` | peso_controlos | `(peso_reference_id)` | No | — |
| `ix_peso_controlos_job_on` | peso_controlos | `(job_on_id)` | No | — |
| `ix_peso_controlos_job_on_revision` | peso_controlos | `(job_on_revision_id)` | No | — |
| `ix_peso_controlos_status_date` | peso_controlos | `(status, control_date)` | No | — |
| `ix_pegamento_controlos_job_on` | pegamento_controlos | `(job_on_id)` | No | — |
| `ix_pegamento_controlos_job_on_revision` | pegamento_controlos | `(job_on_revision_id)` | No | — |
| `ix_pegamento_controlos_production` | pegamento_controlos | `(production_code, machine_code)` | No | — |
| `ix_pegamento_medicoes_controlo` | pegamento_medicoes | `(pegamento_controlo_id)` | No | — |
| `ix_pegamento_medicoes_component_tool` | pegamento_medicoes | `(pegamento_controlo_id, component_key, tool_number)` | No | — |
| `ix_pegamento_documentos_controlo` | pegamento_documentos | `(pegamento_controlo_id)` | No | — |
| `ix_repair_exits_status` | repair_exits | `(status)` | No | — |
| `ix_repair_exits_planned_date` | repair_exits | `(planned_date)` | No | — |
| `ix_repair_exit_items_exit` | repair_exit_items | `(repair_exit_id)` | No | — |
| `ix_internal_repair_records_line` | internal_repair_records | `(line)` | No | — |
| `ix_internal_repair_records_job_on` | internal_repair_records | `(job_on_id)` | No | — |
| `ix_internal_repair_records_revision` | internal_repair_records | `(job_on_revision_id)` | No | — |
| `ix_repair_events_exit_item` | repair_events | `(repair_exit_item_id)` | No | — |
| `ix_repair_events_internal` | repair_events | `(internal_repair_record_id)` | No | — |
| `ix_warehouse_stock_location` | warehouse_stock | `(warehouse_location_id)` | No | — |
| `ix_warehouse_stock_tool_lote` | warehouse_stock | `(tool_lote_id)` | No | — |
| `ix_warehouse_movements_stock` | warehouse_movements | `(warehouse_stock_id)` | No | — |
| `ix_warehouse_movements_occurred` | warehouse_movements | `(occurred_at_utc)` | No | — |
| `ix_tampao_field_values_field` | tampao_field_values | `(tampao_field_def_id, active, value_numeric)` | No | — |
| `ix_tampao_movements_origin` | tampao_movements | `(origin_configuration_id)` | No | — |
| `ix_tampao_movements_occurred` | tampao_movements | `(occurred_at_utc)` | No | — |
| `ix_tampao_planos_configuration` | tampao_planos | `(tampao_configuration_id)` | No | — |
| `ix_tampao_planos_date` | tampao_planos | `(planned_for_date)` | No | — |
| `ix_tampao_configuration_machines_machine` | tampao_configuration_machines | `(machine)` | No | — |
| `ix_tampao_configuration_notes_config` | tampao_configuration_notes | `(tampao_configuration_id, occurred_at_utc)` | No | — |
| `ix_tampao_configuration_machine_event_config` | tampao_configuration_machine_event | `(tampao_configuration_id, occurred_at_utc)` | No | — |
| `ix_controlo_sheets_job_on` | controlo_sheets | `(job_on_id)` | No | — |
| `ix_controlo_sheets_revision` | controlo_sheets | `(job_on_revision_id)` | No | — |
| `ix_controlo_sheets_production` | controlo_sheets | `(production_code, machine_code)` | No | — |
| `ix_controlo_sheets_status` | controlo_sheets | `(status)` | No | — |
| `ix_controlo_sheet_items_sheet` | controlo_sheet_items | `(controlo_sheet_id)` | No | — |
| `ix_controlo_sheet_items_family` | controlo_sheet_items | `(controlo_sheet_id, family)` | No | — |
| `ix_controlo_sheet_events_sheet` | controlo_sheet_events | `(controlo_sheet_id)` | No | — |

Unique indexes (3): `uq_warehouse_stock_active_occupation`, `uq_job_on_identity`, `uq_bq_traces_active` (listed in Unique Constraints).

**Indexes mapped: 78** (75 non-unique + 3 unique).

---

## Functions and Triggers

### Functions (2)

| Function | SQL behavior | Source |
|---|---|---|
| `ba_dmo_guard_append_only()` | Trigger function; raises an exception on UPDATE or DELETE of an append-only table | `database\consolidated_clean_install.sql` · `database\migrations\N01_identity.sql` |
| `ba_dmo_guard_peso_approved()` | Trigger function; raises an exception on DELETE of an approved line, and on UPDATE that changes the identity columns of an approved line | `database\consolidated_clean_install.sql` · `database\migrations\N25_remediation.sql` |

### Triggers (18)

| Trigger | Table | Event | Timing | Function called | SQL effect |
|---|---|---|---|---|---|
| `trg_audit_events_append_only` | audit_events | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_bq_movements_append_only` | bq_movements | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_bq_lifecycle_history_append_only` | bq_lifecycle_history | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_bq_utilisation_readings_append_only` | bq_utilisation_readings | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_job_on_audit_event_append_only` | job_on_audit_event | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_pegamento_medicoes_append_only` | pegamento_medicoes | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_repair_events_append_only` | repair_events | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_warehouse_movements_append_only` | warehouse_movements | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_tampao_movements_append_only` | tampao_movements | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_tampao_configuration_notes_append_only` | tampao_configuration_notes | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_tampao_configuration_machine_event_append_only` | tampao_configuration_machine_event | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_tool_usage_records_append_only` | tool_usage_records | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_controlo_sheet_events_append_only` | controlo_sheet_events | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_peso_controlos_approved_guard` | peso_controlos | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_peso_approved` | blocks DELETE; blocks identity-column UPDATE on approved |
| `trg_job_on_revision_append_only` | job_on_revision | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_job_on_component_append_only` | job_on_component | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_job_on_component_field_append_only` | job_on_component_field | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |
| `trg_job_on_component_row_append_only` | job_on_component_row | UPDATE, DELETE | BEFORE, row | `ba_dmo_guard_append_only` | blocks UPDATE / DELETE |

---

## RLS / Policies

- **RLS-enabled tables: 59** — every current table (`schema_migrations` and the 58 application tables).
- **Policies: 58** — one `ba_dmo_app_access` policy per application table. `schema_migrations` has RLS enabled but no policy.
- **Policy shape** (identical for each table): `CREATE POLICY ba_dmo_app_access ON <table> FOR ALL TO ba_dmo_app USING (true) WITH CHECK (true)`.
- **Roles:**
  - `ba_dmo_app` NOLOGIN — granted `SELECT, INSERT, UPDATE, DELETE` on all tables + `USAGE, SELECT` on all sequences (guarded for Supabase-hosted).
  - `ba_dmo_migrate` NOLOGIN — migration-runner role.
  - `anon` / `authenticated` (Supabase) — `REVOKE ALL` on all tables and sequences (guarded when roles exist).
- **Source:** `database\consolidated_clean_install.sql` (security contract section) · `database\migrations\N12_rls.sql` and `N25_remediation.sql`.

---

## Database Relationships

Direct FK chains (referencing relationships as defined by the schema).

```
job_on
  → job_on_revision          (job_on_revision.job_on_id; circular via job_on.current_revision_id)
    → job_on_component       (job_on_component.job_on_revision_id)
      → job_on_component_field       (…job_on_component_id)
      → job_on_component_row         (…job_on_component_id)
      → job_on_verification_occurrence (…job_on_component_id)
  → job_on_audit_event       (job_on_audit_event.job_on_id)

tool_references
  → tool_lotes
    → physical_pieces
    → tool_check_rules
      → tool_check_occurrences
    → tool_usage_records

bq_lotes
  → bq_traces
    → bq_movements
    → bq_utilisation_readings
    → bq_discrepancies
  → bq_discrepancies (bq_lote_id)
  → bq_lifecycle_history

repair_exits
  → repair_exit_items
    → physical_pieces | bq_lotes
    → repair_events
  repair_events.internal_repair_record_id → internal_repair_records

peso_references
  → peso_lotes
    → peso_controlos
      → peso_leituras
      → peso_comparacao_anterior (self previous_peso_controlo_id)

pegamento_controlos
  → pegamento_medicoes
  → pegamento_documentos (1:1)

controlo_sheets
  → controlo_sheet_items
  → controlo_sheet_events

tampao_configurations
  → tampao_saldos (1:1)
  → tampao_movements (origin | destination)
  → tampao_planos
  → tampao_configuration_machines
  → tampao_configuration_notes
  → tampao_configuration_machine_event

warehouse_locations
  → warehouse_stock
    → warehouse_movements

repairers
  → referenced by repair_exits, line_repairer_defaults, repairer_repair_types, bq_movements.noted_repairer_id
```

---

## Cross-Area Database Relationships

Actual FK edges between database areas.

| Source Table.Column | Target Table.Column | Areas |
|---|---|---|
| `job_on_component.source_tool_id` | `tool_references.tool_reference_id` | Job On → Ferramentas |
| `job_on_component.source_lot_id` | `tool_lotes.tool_lote_id` | Job On → Ferramentas |
| `job_on_verification_occurrence.source_rule_id` | `tool_check_rules.tool_check_rule_id` | Job On → Ferramentas |
| `peso_controlos.job_on_id` | `job_on.job_on_id` | Peso → Job On |
| `peso_controlos.job_on_revision_id` | `job_on_revision.job_on_revision_id` | Peso → Job On |
| `pegamento_controlos.job_on_id` | `job_on.job_on_id` | Pegamentos → Job On |
| `pegamento_controlos.job_on_revision_id` | `job_on_revision.job_on_revision_id` | Pegamentos → Job On |
| `controlo_sheets.job_on_id` | `job_on.job_on_id` | Controlo → Job On |
| `controlo_sheets.job_on_revision_id` | `job_on_revision.job_on_revision_id` | Controlo → Job On |
| `internal_repair_records.job_on_revision_id` | `job_on_revision.job_on_revision_id` | Reparação Interna → Job On |
| `repair_exit_items.physical_piece_id` | `physical_pieces.physical_piece_id` | Reparação Externa → Ferramentas |
| `repair_exit_items.bq_lote_id` | `bq_lotes.bq_lote_id` | Reparação Externa → Boquilhas |
| `bq_movements.noted_repairer_id` | `repairers.repairer_id` | Boquilhas → Reparação Externa |
| `repair_events.internal_repair_record_id` | `internal_repair_records.internal_repair_record_id` | Reparação Externa → Reparação Interna |
| `warehouse_stock.tool_lote_id` | `tool_lotes.tool_lote_id` | Armazém → Ferramentas |
| `warehouse_movements.repair_exit_id` | `repair_exits.repair_exit_id` | Armazém → Reparação Externa |
| `controlo_sheet_items.source_tool_id` | `tool_references.tool_reference_id` | Controlo → Ferramentas |
| `controlo_sheet_items.source_lot_id` | `tool_lotes.tool_lote_id` | Controlo → Ferramentas |

**Cross-area database relationships: 18.**

---

## Database Objects / Special-Purpose Tables

Database-only special-purpose tables, with structure and source location. (These have no dedicated Domain type, but that is not mapped here.)

- ### `schema_migrations`
  - **PK:** `version text`
  - **IMPORTANT COLUMNS:** `filename text NOT NULL`, `sha256 text NOT NULL`, `applied_at timestamptz NOT NULL DEFAULT now()`, `execution_time_ms integer`
  - **RLS:** enabled (no policy)
  - **SOURCE:** established by the migration runner; defined in `database\consolidated_clean_install.sql`

- ### `audit_events`
  - Described in [Section 4 — Shared / Identity / Access](#4-shared--identity--access).

- ### `app_settings`
  - Described in [Section 4 — Shared / Identity / Access](#4-shared--identity--access).

- ### `peso_settings`
  - Described in [Section 11 — Peso](#11-peso).

- ### `peso_day_approvals`
  - Described in [Section 11 — Peso](#11-peso).

- ### `repair_events`
  - Described in [Section 13 — Reparação Externa](#13-reparação-externa).

- ### `jobon_user_current`
  - Described in [Section 5 — Job On](#5-job-on).

- ### `job_on_audit_event`
  - Described in [Section 5 — Job On](#5-job-on).

---

## Sources Verified

**Primary database source authority**
- `database\consolidated_clean_install.sql` — the current full-schema SQL definition (tables, columns, constraints, indexes, functions, triggers, RLS/policies, grants).

**Per-object source-location references (introducing migration files)**
- `database\migrations\N01_identity.sql`
- `database\migrations\N02_catalog.sql`
- `database\migrations\N03_bq.sql`
- `database\migrations\N04_ferramentas.sql`
- `database\migrations\N05_jobon.sql`
- `database\migrations\N06_peso.sql`
- `database\migrations\N07_pegamentos.sql`
- `database\migrations\N08_reparacoes.sql`
- `database\migrations\N09_armazem.sql`
- `database\migrations\N10_tampoes.sql`
- `database\migrations\N11_partilhado.sql`
- `database\migrations\N12_rls.sql`
- `database\migrations\N13_jobon_production_folder.sql`
- `database\migrations\N14_pegamentos_documents.sql`
- `database\migrations\N15_pegamentos_tool_number.sql`
- `database\migrations\N16_pegamentos_component_nominals.sql`
- `database\migrations\N17_pegamentos_notas.sql`
- `database\migrations\N18_bq_repairer.sql`
- `database\migrations\N19_tool_usage.sql`
- `database\migrations\N20_repairer_repair_types.sql`
- `database\migrations\N21_tampoes_machines.sql`
- `database\migrations\N22_reparacao_interna_context.sql`
- `database\migrations\N23_controlo_folha.sql`
- `database\migrations\N24_jobon_user_current.sql`
- `database\migrations\N25_remediation.sql`
- `database\migrations\N26_user_modules_override.sql`

**Registry / navigation contract (not database evidence)**
- `maps\00_INDEX.md` — canonical module registry and mapping contract.

*End of 02_DATABASE.md — pure database technical inventory + location. No database, migration, source, test, Domain or AI-CONTEXT file was modified.*