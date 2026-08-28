# BA DMO — Migrations Technical Map

PAIRED MAP: [02_DATABASE.md](02_DATABASE.md) owns the final current schema. This map owns schema evolution through the numbered migration family. Cross-links: [00_INDEX.md](00_INDEX.md) (map registry), [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md) (persistence layer), [05_TESTS.md](05_TESTS.md) (migration tests).

## Navigation Index

- [1. Purpose](#1-purpose)
- [2. Migration Source Structure](#2-migration-source-structure)
- [3. Global Migration Inventory](#3-global-migration-inventory)
- [4. N01 — identity](#4-n01--identity)
- [5. N02 — catalog](#5-n02--catalog)
- [6. N03 — bq (Boquilhas)](#6-n03--bq-boquilhas)
- [7. N04 — ferramentas](#7-n04--ferramentas)
- [8. N05 — jobon](#8-n05--jobon)
- [9. N06 — peso](#9-n06--peso)
- [10. N07 — pegamentos](#10-n07--pegamentos)
- [11. N08 — reparacoes](#11-n08--reparacoes)
- [12. N09 — armazem](#12-n09--armazem)
- [13. N10 — tampoes](#13-n10--tampoes)
- [14. N11 — partilhado](#14-n11--partilhado)
- [15. N12 — rls](#15-n12--rls)
- [16. N13 — jobon production folder](#16-n13--jobon-production-folder)
- [17. N14 — pegamentos documents](#17-n14--pegamentos-documents)
- [18. N15 — pegamentos tool number](#18-n15--pegamentos-tool-number)
- [19. N16 — pegamentos component nominals](#19-n16--pegamentos-component-nominals)
- [20. N17 — pegamentos notas](#20-n17--pegamentos-notas)
- [21. N18 — bq repairer](#21-n18--bq-repairer)
- [22. N19 — tool usage](#22-n19--tool-usage)
- [23. N20 — repairer repair types](#23-n20--repairer-repair-types)
- [24. N21 — tampoes machines](#24-n21--tampoes-machines)
- [25. N22 — reparacao interna context](#25-n22--reparacao-interna-context)
- [26. N23 — controlo folha](#26-n23--controlo-folha)
- [27. N24 — jobon user current](#27-n24--jobon-user-current)
- [28. N25 — remediation](#28-n25--remediation)
- [29. N26 — user modules override](#29-n26--user-modules-override)
- [30. N27 — access convergence](#30-n27--access-convergence)
- [31. N28 — reparacao interna cm mf only](#31-n28--reparacao-interna-cm-mf-only)
- [32. N29 — jobon reference images](#32-n29--jobon-reference-images)
- [33. N30 — jobon reference image updated_by index](#33-n30--jobon-reference-image-updated_by-index)
- [34. N31 — template profiles single assignment](#34-n31--template-profiles-single-assignment)
- [Migration Dependencies](#migration-dependencies)
- [Object-to-Migration Index](#object-to-migration-index)
- [Constraint / Index Origin](#constraint--index-origin)
- [Functions / Triggers Origin](#functions--triggers-origin)
- [RLS / Policy Evolution](#rls--policy-evolution)
- [Data Migration Statements](#data-migration-statements)
- [Migration Execution & Bookkeeping](#migration-execution--bookkeeping)
- [Consolidated Clean-Install Baseline](#consolidated-clean-install-baseline)
- [Sources Verified](#sources-verified)

## 1. Purpose

This map inventories the BA DMO numbered migration family exactly as written on disk:

- every migration file and its execution order;
- the SQL objects each migration creates or alters;
- constraints, indexes, functions, triggers, RLS/policies/grants introduced or altered;
- direct migration-to-migration dependencies visible in SQL;
- exact file locations.

It does **not** explain business workflow, module behavior, Design/SOT, application behavior, Dapper/repository behavior, reconciliation, gaps, or future fixes. It does **not** reproduce the final schema (that is `02_DATABASE.md`); chronology described here is migration-evolution only.

**Migration count verified from disk: 36 numbered migration files (N01–N36).**

## 2. Migration Source Structure

Directory: `database\migrations\`

Numbered migration family (verified from disk):

| # | Filename |
|---:|---|
| 1 | `N01_identity.sql` |
| 2 | `N02_catalog.sql` |
| 3 | `N03_bq.sql` |
| 4 | `N04_ferramentas.sql` |
| 5 | `N05_jobon.sql` |
| 6 | `N06_peso.sql` |
| 7 | `N07_pegamentos.sql` |
| 8 | `N08_reparacoes.sql` |
| 9 | `N09_armazem.sql` |
| 10 | `N10_tampoes.sql` |
| 11 | `N11_partilhado.sql` |
| 12 | `N12_rls.sql` |
| 13 | `N13_jobon_production_folder.sql` |
| 14 | `N14_pegamentos_documents.sql` |
| 15 | `N15_pegamentos_tool_number.sql` |
| 16 | `N16_pegamentos_component_nominals.sql` |
| 17 | `N17_pegamentos_notas.sql` |
| 18 | `N18_bq_repairer.sql` |
| 19 | `N19_tool_usage.sql` |
| 20 | `N20_repairer_repair_types.sql` |
| 21 | `N21_tampoes_machines.sql` |
| 22 | `N22_reparacao_interna_context.sql` |
| 23 | `N23_controlo_folha.sql` |
| 24 | `N24_jobon_user_current.sql` |
| 25 | `N25_remediation.sql` |
| 26 | `N26_user_modules_override.sql` |
| 27 | `N27_access_convergence.sql` |
| 28 | `N28_reparacao_interna_cm_mf_only.sql` |
| 29 | `N29_jobon_reference_images.sql` |
| 30 | `N30_jobon_reference_image_updated_by_index.sql` |
| 31 | `N31_template_profiles_single_assignment.sql` |
| 32 | `N32_access_authority_convergence.sql` |
| 33 | `N33_legacy_access_mirror_quiescence.sql` |
| 34 | `N34_legacy_access_mirror_removal.sql` |
| 35 | `N35_index_rationalization.sql` |
| 36 | `N36_ba_dmo_app_access_policy_rename.sql` |

This is the complete contents of `database\migrations\`; there are no other files in the folder. `database\consolidated_clean_install.sql` is **not** part of the numbered family (it is a consolidated clean-install baseline; see [Consolidated Clean-Install Baseline](#consolidated-clean-install-baseline)).

## 3. Global Migration Inventory

| Order | Migration | Technical Scope | Main Objects Created/Altered | Depends On |
|---:|---|---|---|---|
| 1 | N01 | roles, identity, access templates, global audit | roles `ba_dmo_app`/`ba_dmo_migrate`; `ba_dmo_guard_append_only()`; tables `access_templates`, `internal_users`, `audit_events`; trigger `trg_audit_events_append_only` | none |
| 2 | N02 | module catalog mirror | table `module_catalog_mirror` | none (standalone) |
| 3 | N03 | Boquilhas (`bq_*`) | tables `bq_lotes`, `bq_traces`, `bq_movements`, `bq_discrepancies`, `bq_lifecycle_history`, `bq_utilisation_readings`; triggers 1–3 | N01 `internal_users`, `ba_dmo_guard_append_only` |
| 4 | N04 | Ferramentas (tool registry) | tables `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences` | N01 `internal_users` |
| 5 | N05 | Job On family | tables `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`, `job_on_audit_event`, `job_on_field_option`; circular FK `fk_job_on_current_revision`; append-only trigger (audit) | N01 `internal_users`, N04 `tool_references`/`tool_lotes`/`tool_check_rules` |
| 6 | N06 | Peso | tables `peso_references`, `peso_lotes`, `peso_controlos`, `peso_leituras`, `peso_comparacao_anterior`, `peso_day_approvals`, `peso_settings` | N01 `internal_users`, N05 `job_on`/`job_on_revision` |
| 7 | N07 | Pegamentos (baseline) | tables `pegamento_controlos`, `pegamento_medicoes`; append-only trigger (medicoes) | N01 `internal_users`, N05 `job_on`/`job_on_revision` |
| 8 | N08 | Repair (Externa + Interna) | tables `repairers`, `line_repairer_defaults`, `repair_exits`, `repair_exit_items`, `repair_events`, `internal_repair_records`; FK `fk_repair_events_internal_record`; append-only trigger (events) | N01 `internal_users`, N03 `bq_lotes`, N04 `physical_pieces` |
| 9 | N09 | Armazém | tables `warehouse_locations`, `warehouse_stock`, `warehouse_movements`; partial unique `uq_warehouse_stock_active_occupation`; append-only trigger (movements) | N01 `internal_users`, N04 `tool_lotes`, N08 `repair_exits` |
| 10 | N10 | Tampões (baseline) | tables `tampao_field_defs`, `tampao_field_values`, `tampao_configurations`, `tampao_saldos`, `tampao_movements`, `tampao_planos`; append-only trigger (movements) | N01 `internal_users` |
| 11 | N11 | shared settings | table `app_settings` | N01 `internal_users` |
| 12 | N12 | RLS / policies / grants / revokes | RLS enable on 49 tables (`rls_tables`, incl. `schema_migrations`); `ba_dmo_app_access` policy on 48 tables (`policy_tables`, excl. `schema_migrations`); anon/authenticated REVOKE; `ba_dmo_app` grants | all N01–N11 tables + `schema_migrations` |
| 13 | N13 | job_on production folder | ALTER `job_on` ADD `production_folder text NULL` | N05 `job_on` |
| 14 | N14 | Pegamentos documents | table `pegamento_documentos` | N01 `internal_users`, N07 `pegamento_controlos` |
| 15 | N15 | Pegamentos tool number | ALTER `pegamento_medicoes` ADD `tool_number integer NULL`; index | N07 `pegamento_medicoes` |
| 16 | N16 | Pegamentos component nominals | ALTER `pegamento_controlos` ADD `cm_nominal`/`bq_nominal`/`mf_nominal numeric(18,4) NULL` | N07 `pegamento_controlos` |
| 17 | N17 | Pegamentos notas | ALTER `pegamento_controlos` ADD `notas text NULL` | N07 `pegamento_controlos` |
| 18 | N18 | BQ repairer | ALTER `bq_movements` ADD `noted_repairer_id uuid NULL REFERENCES repairers` | N03 `bq_movements`, N08 `repairers` |
| 19 | N19 | tool usage history | table `tool_usage_records`; append-only trigger | N01 `internal_users`, N04 `tool_lotes` |
| 20 | N20 | repairer capabilities | table `repairer_repair_types` | N08 `repairers` |
| 21 | N21 | Tampões multi-machine + notes | tables `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event`; 2 append-only triggers | N01 `internal_users`, N10 `tampao_configurations` |
| 22 | N22 | Reparação Interna context | ALTER `internal_repair_records`: widen `tool_type` CHECK to BQ; ADD `job_on_revision_id`/`production_code`/`reference`/`lot_id`; FK `fk_internal_repair_records_revision`; index | N05 `job_on_revision`, N08 `internal_repair_records` |
| 23 | N23 | Controlo folha | tables `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events`; append-only trigger (events) | N01 `internal_users`, N04 `tool_references`/`tool_lotes`, N05 `job_on`/`job_on_revision` |
| 24 | N24 | jobon user current | table `jobon_user_current` | N01 `internal_users`, N05 `job_on` |
| 25 | N25 | remediation | `internal_users.auth_user_id` NOT NULL + UNIQUE; partial unique `uq_job_on_identity`/`uq_bq_traces_active`; CHECKs on `job_on`/`pegamento_controlos`/`repair_exit_items`/`peso_controlos`/`job_on_verification_occurrence`; `ba_dmo_guard_peso_approved()` + trigger; 4 append-only triggers (revision family); RLS/policy/revoke/grants on 10 post-N12 tables; index `ix_audit_events_module_time` | N01 `internal_users`, N06 `peso_controlos`, N05 job_on family, N03 `bq_traces`, N07 `pegamento_controlos`, N08 `repair_exit_items`, N14/N19/N20/N21/N23/N24 tables |
| 26 | N26 | user modules override | ALTER `internal_users` ADD `modules_override jsonb NULL` | N01 `internal_users` |
| 27 | N27 | access convergence | table `internal_user_access_templates`; DML migration (profile inference, legacy-override template materialization, top-level-only module rewrite); ALTER `internal_users` (`profile_title` NOT NULL + `ck_internal_users_functional_profile`); RLS/policy/grants | N01 `access_templates`/`internal_users` |
| 28 | N28 | Reparação Interna CM/MF-only | re-tighten `ck_internal_repair_records_type` to CM/MF (`NOT VALID` + `VALIDATE`), fail-closed guard; explicit `BEGIN/COMMIT` | N08/N22 `internal_repair_records` |
| 29 | N29 | Job On reference images | table `article_reference_images`; guarded DML migration (legacy `job_on_revision.image_asset_id` → master); RLS/policy/grants | N01 `internal_users`, N05 `job_on`/`job_on_revision` |
| 30 | N30 | reference-image updated_by index | index `ix_article_reference_images_updated_by` | N29 `article_reference_images` |
| 31 | N31 | template profiles / single assignment | table `access_template_profiles`; fn `ba_dmo_ensure_access_template_profile` + trg `trg_access_templates_ensure_profile`; backfill + collapse DML; unique `ux_internal_user_access_templates_actor`; RLS/policy/grants | N01 `access_templates`/`internal_users`, N27 `internal_user_access_templates` |
| 32 | N32 | access-authority convergence (SCHEMA-RAT-03A, D-1/D-2) | fail-closed junction guards (multi-assignment/conflict diagnostics); deterministic `access_template_profiles` backfill; NO destructive DDL | N01 `access_templates`/`internal_users`, N27 junction, N31 `access_template_profiles` |
| 33 | N33 | legacy access mirror quiescence (SCHEMA-RAT-03B) | `internal_users.profile_title DROP NOT NULL`; junction `REVOKE ALL` from `ba_dmo_app`; `internal_users` column-level SELECT/INSERT/UPDATE grants excluding `profile_title` | N01/N27 `internal_users`, N27 junction |
| 34 | N34 | legacy access mirror REMOVAL | `DROP TABLE internal_user_access_templates` (PK/FKs/indexes/inert policy); `DROP CONSTRAINT ck_internal_users_functional_profile`; `DROP COLUMN internal_users.profile_title` — Option A, no CASCADE, `IF EXISTS` | N27/N31/N32/N33 mirror lifecycle complete |
| 35 | N35 | safe index rationalization | ADD `ix_bq_movements_noted_repairer` (BQ-16) on `bq_movements`; DROP redundant `ix_pegamento_documentos_controlo` (duplicates UNIQUE). Optional BQ-10 CHECK trim deliberately EXCLUDED (OD-16) | N03 `bq_movements`, N14 `pegamento_documentos` |
| 36 | N36 | RLS policy-name convention (D-15) | rename `access_template_profiles_app_access` → `ba_dmo_app_access` (identical `FOR ALL TO ba_dmo_app USING(TRUE) WITH CHECK(TRUE)` semantics; no grant change) | N31 `access_template_profiles` |

## 4. N01 — identity

File:
`database\migrations\N01_identity.sql`

**Roles / Grants:**
- `DO` block creates roles `ba_dmo_app` NOLOGIN and `ba_dmo_migrate` NOLOGIN (guarded by `IF NOT EXISTS (pg_roles)`).
- `GRANT USAGE ON SCHEMA public` to both roles.
- `ALTER DEFAULT PRIVILEGES FOR ROLE ba_dmo_migrate IN SCHEMA public` — `SELECT, INSERT, UPDATE, DELETE ON TABLES` to `ba_dmo_app`; `USAGE, SELECT ON SEQUENCES` to `ba_dmo_app`.

**Functions:**
- `CREATE OR REPLACE FUNCTION ba_dmo_guard_append_only()` — trigger function; raises exception on UPDATE/DELETE.

**Creates (tables):**
- `access_templates` (PK `template_id text`; `modules jsonb`; `active`; timestamps).
- `internal_users` (PK `actor_id text`; `auth_user_id uuid NULL`; FK `template_id → access_templates`; timestamps).
- `audit_events` (PK `audit_event_id uuid`; audit fact columns incl. `job_on_id uuid`/`revision_id uuid` plain uuids).

**Constraints:**
- `ck_audit_events_year_positive` CHECK (`year > 0`).
- `ck_audit_events_result` CHECK (`result IN ('succeeded','failed','denied','corrected')`).

**Indexes:**
- `ix_access_templates_active`.
- `ix_internal_users_auth_user_id`, `ix_internal_users_active`, `ix_internal_users_template_id`.
- `ix_audit_events_year`, `ix_audit_events_module_action`, `ix_audit_events_actor`, `ix_audit_events_entity`, `ix_audit_events_occurred_at`, `ix_audit_events_job_on_id`.

**Triggers:**
- `trg_audit_events_append_only` — `BEFORE UPDATE OR DELETE` on `audit_events`, uses `ba_dmo_guard_append_only`.

**Depends on:** none (first migration).

## 5. N02 — catalog

File:
`database\migrations\N02_catalog.sql`

**Creates:**
- table `module_catalog_mirror` (PK `module_id text`; `display_name`, `display_order integer`, `active`, `synced_at_utc`).

**Indexes:**
- `ix_module_catalog_mirror_order` (`display_order`).

**Depends on:** none (standalone). No index/constraint beyond PK and the single index; no seed data.

## 6. N03 — bq (Boquilhas)

File:
`database\migrations\N03_bq.sql`

**Creates (tables):**
- `bq_lotes` (PK `bq_lote_id uuid`; `reference`, `batch_code`, `allowed_lines text[]`, `lifecycle_state`).
- `bq_traces` (PK `bq_trace_id uuid`; FK `bq_lote_id → bq_lotes`; `status`, `purpose`, `start_line`, `sap_start`/`sap_end numeric(5,2)`, snapshots).
- `bq_movements` (PK `bq_movement_id uuid`; FK `bq_trace_id → bq_traces`; `movement_type`, `qty numeric(12,2)`, `exceptional_received_qty`, `line`, `notes`).
- `bq_discrepancies` (PK `bq_discrepancy_id uuid`; FK `bq_lote_id → bq_lotes`; FK `bq_trace_id → bq_traces`; `expected_qty`/`actual_qty`/`excess_qty numeric(12,2)`, `status`).
- `bq_lifecycle_history` (PK; FK `bq_lote_id → bq_lotes`; `event`, `reason`).
- `bq_utilisation_readings` (PK; FK `bq_trace_id → bq_traces`; `reading_kind`, `value numeric(5,2)`).

**Constraints (FKs):** as listed; plus:
- `uq_bq_lotes_reference_batch` UNIQUE (`reference, batch_code`).
- `ck_bq_lotes_reference` CHECK (`reference ~ '^[A-Z][0-9]{3}$'`).
- `ck_bq_lotes_lifecycle` CHECK (`lifecycle_state IN ('available','archived','scrapped')`).
- `ck_bq_traces_status`, `ck_bq_traces_purpose`, `ck_bq_traces_sap_start`, `ck_bq_traces_sap_end`.
- `ck_bq_movements_type`, `ck_bq_movements_qty`, `ck_bq_movements_exceptional`.
- `ck_bq_discrepancies_status`.
- `ck_bq_lifecycle_history_event`.
- `ck_bq_utilisation_readings_kind`, `ck_bq_utilisation_readings_value`.

**Indexes:**
- `ix_bq_lotes_lifecycle`.
- `ix_bq_traces_lote`, `ix_bq_traces_status`.
- `ix_bq_movements_trace`, `ix_bq_movements_occurred`.
- `ix_bq_discrepancies_lote`, `ix_bq_discrepancies_status`.
- `ix_bq_lifecycle_history_lote`.
- `ix_bq_utilisation_readings_trace`.

**Triggers (append-only, `ba_dmo_guard_append_only`):**
- `trg_bq_movements_append_only` on `bq_movements`.
- `trg_bq_lifecycle_history_append_only` on `bq_lifecycle_history`.
- `trg_bq_utilisation_readings_append_only` on `bq_utilisation_readings`.

Note: no partial unique index is created in N03 (`uq_bq_traces_active` is added in N25).

**Depends on:** N01 `internal_users` (FKs on `created_by`/`actor_id`), N01 `ba_dmo_guard_append_only`.

## 7. N04 — ferramentas

File:
`database\migrations\N04_ferramentas.sql`

**Creates (tables):**
- `tool_references` (PK `tool_reference_id uuid`; `tool_type`, `ref_code`, `technical_name`, `owner_plant`).
- `tool_lotes` (PK `tool_lote_id uuid`; FK `tool_reference_id → tool_references`; `lote`, `qty`, `allowed_lines`, `drawing_code`/`drawing_revision`, `processo`).
- `physical_pieces` (PK `physical_piece_id uuid`; FK `tool_lote_id → tool_lotes`; `sequence`, `number`, `status`).
- `tool_check_rules` (PK; FK `tool_lote_id → tool_lotes`; self-FK `copied_from_rule_id → tool_check_rules`; `rule_text`, `frequency`, `active`).
- `tool_check_occurrences` (PK; FK `tool_check_rule_id → tool_check_rules`; logical `job_on_id`/`job_on_component_id uuid` — no physical FK; `status`, `completion_source`, `completed_by`).

**Constraints:**
- `uq_tool_references_type_code` UNIQUE (`tool_type, ref_code`).
- `ck_tool_references_type` CHECK (`tool_type IN ('CM','MF','BQ','PU','CS')`).
- `uq_tool_lotes_reference_lote` UNIQUE (`tool_reference_id, lote`).
- `ck_tool_lotes_qty` CHECK (`qty IS NULL OR qty >= 0`).
- `uq_physical_pieces_lote_number` UNIQUE (`tool_lote_id, number`).
- `ck_physical_pieces_sequence` CHECK (`sequence >= 1`).
- `ck_tool_check_rules_frequency` CHECK (`frequency IN ('uma_vez_no_lote','por_fabrico')`).
- `ck_tool_check_occurrences_status`, `ck_tool_check_occurrences_source`, `ck_tool_check_occurrences_completed`.

**Indexes:**
- `ix_tool_lotes_reference`.
- `ix_physical_pieces_lote`.
- `ix_tool_check_rules_lote`.
- `ix_tool_check_occurrences_rule`, `ix_tool_check_occurrences_job_on`.

**Triggers:** none.

**Depends on:** N01 `internal_users` (FKs on `created_by`/`completed_by`).

## 8. N05 — jobon

File:
`database\migrations\N05_jobon.sql`

**Creates (tables):**
- `job_on` (PK `job_on_id uuid`; `production_code`, `article_reference_id`/`article_reference_snapshot`, `machine_code`, planned dates, `status`, `current_revision_id`).
- `job_on_revision` (PK `job_on_revision_id uuid`; FK `job_on_id → job_on`; snapshot columns; `change_reason`, `saved_by`; `image_asset_id` — legacy logical per-revision image link, later consumed by N29).
- `job_on_component` (PK; FK `job_on_revision_id → job_on_revision`; FKs `source_tool_id → tool_references`, `source_lot_id → tool_lotes`; `family`, snapshots).
- `job_on_component_field` (PK; FK `job_on_component_id → job_on_component`; typed value columns).
- `job_on_component_row` (PK; FK `job_on_component_id → job_on_component`; CAL row columns).
- `job_on_verification_occurrence` (PK; FK `job_on_component_id → job_on_component`; FK `source_rule_id → tool_check_rules`; `status`, `completion_source`).
- `job_on_audit_event` (PK; FKs `job_on_id → job_on`, `job_on_revision_id → job_on_revision`, `actor_id → internal_users`).
- `job_on_field_option` (PK; `family`, `field_key`, `option_value`, `active`).

**Circular FK handling:**
- `fk_job_on_current_revision` — `job_on.current_revision_id → job_on_revision`, added in a guarded `DO` block (both tables created in this file).

**Constraints:**
- `ck_job_on_status` CHECK (`status IN ('rascunho','planeado','em_fabrico','fechado','cancelado')`).
- `uq_job_on_revision_number` UNIQUE (`job_on_id, revision_number`).
- `ck_job_on_revision_number` CHECK (`revision_number >= 1`).
- `ck_job_on_component_family` CHECK (`family IN ('MP_CM','MF','BQ','PU','CAL','AN','ARR','PI','CS','TP','FO')`).
- `uq_job_on_component_field` UNIQUE (`job_on_component_id, field_key`).
- `ck_job_on_component_field_type` CHECK.
- `ck_job_on_verification_status`, `ck_job_on_verification_source`.
- `uq_job_on_field_option` UNIQUE (`family, field_key, option_value`).

**Indexes:**
- `ix_job_on_production_code`, `ix_job_on_status`, `ix_job_on_machine_planned`.
- `ix_job_on_revision_job_on`.
- `ix_job_on_component_revision`.
- `ix_job_on_component_field_component`.
- `ix_job_on_component_row_component`.
- `ix_job_on_verification_component`.
- `ix_job_on_audit_event_job_on`.
- `ix_job_on_field_option_lookup`.

**Triggers (append-only, `ba_dmo_guard_append_only`):**
- `trg_job_on_audit_event_append_only` on `job_on_audit_event`.

**Depends on:** N01 `internal_users` (FKs), N04 `tool_references`/`tool_lotes`/`tool_check_rules` (FKs in component/verification tables). Note: append-only triggers on `job_on_revision`/`job_on_component`/`job_on_component_field`/`job_on_component_row` are **not** in N05 — they are added in N25.

## 9. N06 — peso

File:
`database\migrations\N06_peso.sql`

**Creates (tables):**
- `peso_references` (PK `peso_reference_id uuid`; `mold_number`, `neckring_number`, `counter_mold`, capacity/volume columns `numeric(18,4)`, `calote_tp`, `change_log jsonb`).
- `peso_lotes` (PK; FK `peso_reference_id → peso_references`; `lote`, `processo`, `allowed_lines`, `report_subfolder`, `nominal_weight`).
- `peso_controlos` (PK; FKs `peso_reference_id → peso_references`, `peso_lote_id → peso_lotes`, `job_on_id → job_on`, `job_on_revision_id → job_on_revision`; `status`, snapshots, `approved_by`).
- `peso_leituras` (PK; FK `peso_controlo_id → peso_controlos` ON DELETE CASCADE; `cm_number`, `readings jsonb`).
- `peso_comparacao_anterior` (PK `peso_controlo_id → peso_controlos` ON DELETE CASCADE; FK `previous_peso_controlo_id → peso_controlos`; snapshots).
- `peso_day_approvals` (PK; `mold_number`, `neckring_number`, `line`, `approval_date`; `approved_by`).
- `peso_settings` (PK `setting_key text`; `setting_value jsonb`).

**Constraints:**
- `uq_peso_references_mold_neckring` UNIQUE (`mold_number, neckring_number`).
- `uq_peso_lotes_reference_lote` UNIQUE (`peso_reference_id, lote`).
- `ck_peso_lotes_processo` CHECK (`processo IN ('NNPB','PS')`).
- `ck_peso_lotes_allowed_lines` CHECK (`cardinality(allowed_lines) >= 1`).
- `uq_peso_controlos_identity` UNIQUE (`mold_number, neckring_number, production_code, line, lote, control_date`).
- `ck_peso_controlos_record_type` CHECK.
- `ck_peso_controlos_status` CHECK (`status IN ('rascunho','pendente','aprovado','nao_aprovado')`).
- `uq_peso_leituras_controlo_cm` UNIQUE (`peso_controlo_id, cm_number`).
- `uq_peso_day_approvals_identity` UNIQUE (`mold_number, neckring_number, line, approval_date`).

**Indexes:**
- `ix_peso_lotes_reference`.
- `ix_peso_controlos_reference`, `ix_peso_controlos_job_on`, `ix_peso_controlos_job_on_revision`, `ix_peso_controlos_status_date`.

**Triggers:** none (the approved-state guard is added in N25).

**Depends on:** N01 `internal_users` (FKs), N05 `job_on`/`job_on_revision` (NOT NULL FKs in `peso_controlos`).

## 10. N07 — pegamentos

File:
`database\migrations\N07_pegamentos.sql`

**Creates (tables):**
- `pegamento_controlos` (PK; FKs `job_on_id → job_on`, `job_on_revision_id → job_on_revision`; snapshot columns, `nominal_average`, `tolerance numeric(6,3)`, `status`).
- `pegamento_medicoes` (PK; FK `pegamento_controlo_id → pegamento_controlos`; `component_key`, `costura`, `contra_costura`, `measured_at_utc`, `actor_id`).

**Constraints:**
- `ck_pegamento_controlos_tolerance` CHECK (`tolerance >= 0`).

**Indexes:**
- `ix_pegamento_controlos_job_on`, `ix_pegamento_controlos_job_on_revision`, `ix_pegamento_controlos_production`.
- `ix_pegamento_medicoes_controlo`.

**Triggers (append-only):**
- `trg_pegamento_medicoes_append_only` on `pegamento_medicoes`.

**Depends on:** N01 `internal_users` (FKs), N05 `job_on`/`job_on_revision` (NOT NULL FKs). Note: `tool_number`, `cm_nominal`/`bq_nominal`/`mf_nominal`, and `notas` are added in N15/N16/N17 — not here.

## 11. N08 — reparacoes

File:
`database\migrations\N08_reparacoes.sql`

**Creates (tables):**
- `repairers` (PK `repairer_id uuid`; `name`, `active`).
- `line_repairer_defaults` (composite PK `(line, tool_type)`; FK `repairer_id → repairers`; `updated_by`).
- `repair_exits` (PK; FK `repairer_id → repairers`; `repairer_snapshot`, `planned_date`, `status`).
- `repair_exit_items` (PK; FK `repair_exit_id → repair_exits`; FKs `bq_lote_id → bq_lotes`, `physical_piece_id → physical_pieces`; `qty`, `individual_number`, pick/out/in facts, `status`).
- `repair_events` (PK; FKs `repair_exit_item_id → repair_exit_items`, `actor_id → internal_users`; `internal_repair_record_id` plain uuid).
- `internal_repair_records` (PK; `line`, `job_on_id` plain uuid, `tool_type`, `individual_number`, `operator_id`, `correction_of_id` self-FK, snapshots).

**Forward FK handling:**
- `fk_repair_events_internal_record` — `repair_events.internal_repair_record_id → internal_repair_records`, added in a guarded `DO` block (target table created later in same file).

**Constraints:**
- `ck_line_repairer_defaults_type` CHECK (`tool_type IN ('BQ','CM','MF')`).
- `ck_repair_exits_type` CHECK (`repair_type IN ('BQ','CM','MF')`).
- `ck_repair_exits_status` CHECK.
- `ck_repair_exit_items_qty`, `ck_repair_exit_items_kind`.
- `ck_repair_events_scope` CHECK (`repair_scope IN ('interna','externa')`).
- `ck_internal_repair_records_type` CHECK (`tool_type IN ('CM','MF')`) — later redefined in N22 to add `'BQ'`.
- `ck_internal_repair_records_correction`.

**Indexes:**
- `ix_repair_exits_status`, `ix_repair_exits_planned_date`.
- `ix_repair_exit_items_exit`.
- `ix_repair_events_exit_item`, `ix_repair_events_internal`.
- `ix_internal_repair_records_line`, `ix_internal_repair_records_job_on`.

**Triggers (append-only):**
- `trg_repair_events_append_only` on `repair_events`.

**Depends on:** N01 `internal_users` (FKs), N03 `bq_lotes` (FK in `repair_exit_items`), N04 `physical_pieces` (FK in `repair_exit_items`).

## 12. N09 — armazem

File:
`database\migrations\N09_armazem.sql`

**Creates (tables):**
- `warehouse_locations` (PK `warehouse_location_id uuid`; `code` UNIQUE inline, `kind`).
- `warehouse_stock` (PK; FK `warehouse_location_id → warehouse_locations`; FK `tool_lote_id → tool_lotes`; occupation facts `occupied_since_utc`, `released_at_utc`).
- `warehouse_movements` (PK; FK `warehouse_stock_id → warehouse_stock`; FK `repair_exit_id → repair_exits`; `direction`, `qty`, `destination`).

**Constraints:**
- `warehouse_locations.code` UNIQUE (inline).
- `ck_warehouse_movements_direction` CHECK (`direction IN ('in','out')`).

**Partial unique indexes:**
- `uq_warehouse_stock_active_occupation` UNIQUE (`warehouse_location_id, tool_lote_id`) WHERE `released_at_utc IS NULL`.

**Indexes:**
- `ix_warehouse_stock_location`, `ix_warehouse_stock_tool_lote`.
- `ix_warehouse_movements_stock`, `ix_warehouse_movements_occurred`.

**Triggers (append-only):**
- `trg_warehouse_movements_append_only` on `warehouse_movements`.

**Depends on:** N01 `internal_users` (FKs), N04 `tool_lotes` (FK in stock), N08 `repair_exits` (FK in `warehouse_movements`).

## 13. N10 — tampoes

File:
`database\migrations\N10_tampoes.sql`

**Creates (tables):**
- `tampao_field_defs` (PK `tampao_field_def_id uuid`; `field_name` UNIQUE inline, `unit`, `precision_digits`, `display_order`, `active`).
- `tampao_field_values` (PK; FK `tampao_field_def_id → tampao_field_defs`; `value_numeric`, `value_label`).
- `tampao_configurations` (PK; `values_json`, `active`).
- `tampao_saldos` (PK; FK `tampao_configuration_id → tampao_configurations` UNIQUE inline; `enchidos`, `por_encher`).
- `tampao_movements` (PK; FKs `origin_configuration_id`/`destination_configuration_id → tampao_configurations`; `qty`, balances snapshots).
- `tampao_planos` (PK; FK `tampao_configuration_id → tampao_configurations`; `planned_qty`, `planned_for_date`, `job_on_id` plain uuid, `production_code`, `canceled`).

**Constraints:**
- `uq_tampao_field_values` UNIQUE (`tampao_field_def_id, value_numeric`).
- `uq_tampao_configurations_values` UNIQUE (`values_json`).
- `ck_tampao_saldos_enchidos`, `ck_tampao_saldos_por_encher`.
- `ck_tampao_movements_type`, `ck_tampao_movements_qty`.
- `ck_tampao_planos_qty`.

**Indexes:**
- `ix_tampao_field_values_field`.
- `ix_tampao_movements_origin`, `ix_tampao_movements_occurred`.
- `ix_tampao_planos_configuration`, `ix_tampao_planos_date`.

**Triggers (append-only):**
- `trg_tampao_movements_append_only` on `tampao_movements`.

**Depends on:** N01 `internal_users` (FKs). Note: machine/notes/event tables are added in N21 — not here.

## 14. N11 — partilhado

File:
`database\migrations\N11_partilhado.sql`

**Creates:**
- table `app_settings` (PK `setting_key text`; `setting_value jsonb`; `updated_by` FK to `internal_users`).

No constraints beyond PK, no indexes, no seed data, no triggers.

**Depends on:** N01 `internal_users` (FK).

## 15. N12 — rls

File:
`database\migrations\N12_rls.sql`

**Roles / RLS / Policies / Grants:**
- `DO` block enables RLS (`ALTER TABLE %I ENABLE ROW LEVEL SECURITY`) on **49** tables (`rls_tables`): the 48 application tables from N01–N11 plus `schema_migrations`.
- `DO` block: for roles `anon`, `authenticated` (guarded by `pg_roles` existence) → `REVOKE ALL` on all tables and sequences in `public`.
- `GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO ba_dmo_app`; `GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO ba_dmo_app`.
- `DO` block creates policy `ba_dmo_app_access` (`FOR ALL TO ba_dmo_app USING (true) WITH CHECK (true)`) on **48** application tables (`policy_tables` array; `schema_migrations` intentionally excluded). Each policy is preceded by `DROP POLICY IF EXISTS ba_dmo_app_access` + `CREATE POLICY`.

**Table list arrays used by procedural SQL:**
- `rls_tables` (49 entries) — RLS enable target set.
- `policy_tables` (48 entries) — policy target set.

**Depends on:** all N01–N11 tables (must exist) and `schema_migrations` (migration-runner object). It only covers tables existing at N12 time; tables created after N12 get RLS/policy applied in N25.

## 16. N13 — jobon production folder

File:
`database\migrations\N13_jobon_production_folder.sql`

**Alters:**
- `ALTER TABLE job_on ADD COLUMN IF NOT EXISTS production_folder text NULL`.

**Depends on:** N05 `job_on`.

## 17. N14 — pegamentos documents

File:
`database\migrations\N14_pegamentos_documents.sql`

**Creates:**
- table `pegamento_documentos` (PK `pegamento_documento_id uuid`; FK `pegamento_controlo_id → pegamento_controlos` UNIQUE inline; `filename`, `output_root_snapshot`, `production_folder_snapshot`, `generated_by`).

**Indexes:**
- `ix_pegamento_documentos_controlo`.

**Depends on:** N07 `pegamento_controlos` (FK), N01 `internal_users` (FK).

## 18. N15 — pegamentos tool number

File:
`database\migrations\N15_pegamentos_tool_number.sql`

**Alters:**
- `ALTER TABLE pegamento_medicoes ADD COLUMN IF NOT EXISTS tool_number integer NULL`.

**Indexes:**
- `ix_pegamento_medicoes_component_tool` (`pegamento_controlo_id, component_key, tool_number`).

**Depends on:** N07 `pegamento_medicoes`.

## 19. N16 — pegamentos component nominals

File:
`database\migrations\N16_pegamentos_component_nominals.sql`

**Alters (`pegamento_controlos`):**
- `ADD COLUMN IF NOT EXISTS cm_nominal numeric(18,4) NULL`.
- `ADD COLUMN IF NOT EXISTS bq_nominal numeric(18,4) NULL`.
- `ADD COLUMN IF NOT EXISTS mf_nominal numeric(18,4) NULL`.

**Depends on:** N07 `pegamento_controlos`.

## 20. N17 — pegamentos notas

File:
`database\migrations\N17_pegamentos_notas.sql`

**Alters:**
- `ALTER TABLE pegamento_controlos ADD COLUMN IF NOT EXISTS notas text NULL`.

**Depends on:** N07 `pegamento_controlos`.

## 21. N18 — bq repairer

File:
`database\migrations\N18_bq_repairer.sql`

**Alters:**
- `ALTER TABLE bq_movements ADD COLUMN IF NOT EXISTS noted_repairer_id uuid NULL REFERENCES repairers (repairer_id)`.

**Depends on:** N03 `bq_movements`, N08 `repairers` (FK).

## 22. N19 — tool usage

File:
`database\migrations\N19_tool_usage.sql`

**Creates:**
- table `tool_usage_records` (PK `tool_usage_record_id uuid`; FK `tool_lote_id → tool_lotes`; `sap_start`/`sap_end`/`percent_used numeric(5,2)`, `value_added`/`value_cumulative numeric(12,2)`, `notes`, `actor_id`).

**Constraints:**
- `ck_tool_usage_records_sap_start`, `ck_tool_usage_records_sap_end`, `ck_tool_usage_records_percent` (0–100 checks).
- `ck_tool_usage_records_cumulative` CHECK (`value_cumulative >= 0`).

**Indexes:**
- `ix_tool_usage_records_lote`.

**Triggers (append-only):**
- `trg_tool_usage_records_append_only` on `tool_usage_records`.

**Depends on:** N01 `internal_users` (FK), N04 `tool_lotes` (FK).

## 23. N20 — repairer repair types

File:
`database\migrations\N20_repairer_repair_types.sql`

**Creates:**
- table `repairer_repair_types` (FK `repairer_id → repairers`; `repair_type text`; composite PK `(repairer_id, repair_type)`).

**Constraints:**
- `ck_repairer_repair_types_type` CHECK (`repair_type IN ('CM','MF','BQ')`).

**Depends on:** N08 `repairers` (FK).

## 24. N21 — tampoes machines

File:
`database\migrations\N21_tampoes_machines.sql`

**Creates (tables):**
- `tampao_configuration_machines` (FK `tampao_configuration_id → tampao_configurations`; `machine text`; composite PK `(tampao_configuration_id, machine)`).
- `tampao_configuration_notes` (PK; FK `tampao_configuration_id → tampao_configurations`; `note`, `actor_id`).
- `tampao_configuration_machine_event` (PK; FK `tampao_configuration_id → tampao_configurations`; `machine`, `action`, `actor_id`).

**Constraints:**
- `ck_tampao_configuration_machines_machine` CHECK (`machine IN ('B1','B2','B3','C1','C2','C3')`).
- `ck_tampao_configuration_machine_event_action` CHECK (`action IN ('added','removed')`).
- `ck_tampao_configuration_machine_event_machine` CHECK (same B1–C3 set).

**Indexes:**
- `ix_tampao_configuration_machines_machine`.
- `ix_tampao_configuration_notes_config`.
- `ix_tampao_configuration_machine_event_config`.

**Triggers (append-only):**
- `trg_tampao_configuration_notes_append_only` on `tampao_configuration_notes`.
- `trg_tampao_configuration_machine_event_append_only` on `tampao_configuration_machine_event`.

**Depends on:** N01 `internal_users` (FKs), N10 `tampao_configurations` (FKs).

## 25. N22 — reparacao interna context

File:
`database\migrations\N22_reparacao_interna_context.sql`

**Alters (`internal_repair_records`):**
- `DO` block drops `ck_internal_repair_records_type` if it exists; then `ADD CONSTRAINT ck_internal_repair_records_type` CHECK (`tool_type IN ('CM','MF','BQ')`) — widens the N08 CHECK to include `'BQ'`.
- `ADD COLUMN IF NOT EXISTS` × 4: `job_on_revision_id uuid`, `production_code text`, `reference text`, `lot_id uuid`.
- `DO` block adds FK `fk_internal_repair_records_revision` — `job_on_revision_id → job_on_revision`.

**Indexes:**
- `ix_internal_repair_records_revision` (`job_on_revision_id`).

**Depends on:** N05 `job_on_revision` (FK — needs the guard `pg_constraint` existence check), N08 `internal_repair_records`. Note: `job_on_revision_id` FK references `job_on_revision` which is created in N05.

## 26. N23 — controlo folha

File:
`database\migrations\N23_controlo_folha.sql`

**Creates (tables):**
- `controlo_sheets` (PK `controlo_sheet_id uuid`; FKs `job_on_id → job_on`, `job_on_revision_id → job_on_revision`; `production_code`, `reference`, `machine_code`, `display_id`, `status`, submission/decision columns).
- `controlo_sheet_items` (PK; FK `controlo_sheet_id → controlo_sheets` ON DELETE CASCADE; FKs `source_tool_id → tool_references`, `source_lot_id → tool_lotes`; snapshots, `result`, `observation`, `mcaliper_link`).
- `controlo_sheet_events` (PK; FK `controlo_sheet_id → controlo_sheets` ON DELETE CASCADE; `event_type`, `actor_id`, snapshots, `note`).

**Constraints:**
- `ck_controlo_sheets_status` CHECK (`status IN ('rascunho','submetido','aprovado','rejeitado')`).
- `ck_controlo_sheets_decision` CHECK (decision trio consistency).
- `ck_controlo_sheet_items_result` CHECK (`result IS NULL OR result IN ('OK','NOK')`).
- `ck_controlo_sheet_events_type` CHECK (`event_type IN ('criar','editar','submeter','reeabrir','decidir')`).

**Indexes:**
- `ix_controlo_sheets_job_on`, `ix_controlo_sheets_revision`, `ix_controlo_sheets_production`, `ix_controlo_sheets_status`.
- `ix_controlo_sheet_items_sheet`, `ix_controlo_sheet_items_family`.
- `ix_controlo_sheet_events_sheet`.

**Triggers (append-only):**
- `trg_controlo_sheet_events_append_only` on `controlo_sheet_events`.

Note: no RLS stanza in this file; RLS/policies added in N25.

**Depends on:** N01 `internal_users` (FKs), N04 `tool_references`/`tool_lotes` (FKs in items), N05 `job_on`/`job_on_revision` (FKs).

## 27. N24 — jobon user current

File:
`database\migrations\N24_jobon_user_current.sql`

**Creates:**
- table `jobon_user_current` (PK `actor_id text → internal_users`; FK `job_on_id → job_on`; `production_code`, `reference`, `machine_code`, `opened_at_utc`).

**Depends on:** N01 `internal_users` (PK/FK), N05 `job_on` (FK). Note: RLS/policies added in N25.

## 28. N25 — remediation

File:
`database\migrations\N25_remediation.sql`

This section inventories the SQL operations in file order.

**§1.1 — `internal_users.auth_user_id` NOT NULL + UNIQUE:**
- Guard `DO`: raises exception if any `internal_users.auth_user_id IS NULL` row exists.
- `ALTER TABLE internal_users ALTER COLUMN auth_user_id SET NOT NULL`.
- `DO` (`pg_constraint` guard) adds `uq_internal_users_auth_user` UNIQUE (`auth_user_id`).

**§1.2 — `job_on` non-canceled identity:**
- `CREATE UNIQUE INDEX IF NOT EXISTS uq_job_on_identity` on `job_on (production_code, machine_code)` WHERE `canceled_at_utc IS NULL`.

**§1.3 — `job_on` lifecycle consistency:**
- `DO` (`pg_constraint` guard) adds `ck_job_on_lifecycle_consistent` CHECK: `(status='fechado') = (closed_at_utc IS NOT NULL) AND (status='cancelado') = (canceled_at_utc IS NOT NULL)`.

**§1.4 — active trace per lote:**
- `CREATE UNIQUE INDEX IF NOT EXISTS uq_bq_traces_active` on `bq_traces (bq_lote_id)` WHERE `status = 'active'`.

**§1.5 — `pegamento_controlos` status:**
- `DO` (`pg_constraint` guard) adds `ck_pegamento_controlos_status` CHECK (`status IN ('aberto','fechado')`).

**§1.6 — `repair_exit_items` status:**
- `DO` (`pg_constraint` guard) adds `ck_repair_exit_items_status` CHECK (`status IN ('pendente','em_reparacao','devolvido')`).

**§1.7 — `peso_controlos` approved-state consistency:**
- `DO` (`pg_constraint` guard) adds `ck_peso_controlos_approved_consistent` CHECK (`(status='aprovado') = (approved_at_utc IS NOT NULL)`).

**§1.7b — approved peso controls immutable:**
- `CREATE OR REPLACE FUNCTION ba_dmo_guard_peso_approved()` — trigger function; raises on DELETE of approved row, and on UPDATE changing any identity column (`mold_number`, `neckring_number`, `production_code`, `line`, `lote`, `control_date`) of an approved row.
- `DROP TRIGGER IF EXISTS` + `CREATE TRIGGER trg_peso_controlos_approved_guard` — `BEFORE UPDATE OR DELETE ON peso_controlos`, uses `ba_dmo_guard_peso_approved`.

**§1.8 — `job_on_verification_occurrence` completed-state:**
- `DO` (`pg_constraint` guard) adds `ck_job_on_verification_completed` CHECK (`(status IN ('confirmada','reposta')) = (completed_at_utc IS NOT NULL)`).

**§1.9 — append-only on the four revision-family tables:**
- `trg_job_on_revision_append_only` on `job_on_revision`.
- `trg_job_on_component_append_only` on `job_on_component`.
- `trg_job_on_component_field_append_only` on `job_on_component_field`.
- `trg_job_on_component_row_append_only` on `job_on_component_row`.
- Each: `DROP TRIGGER IF EXISTS` + `CREATE TRIGGER`, `BEFORE UPDATE OR DELETE`, `ba_dmo_guard_append_only`.

**§2 — RLS / policies / revokes / grants for the 10 post-N12 tables:**
- `DO` block (`late_tables` array of 10): for each → `ALTER TABLE ENABLE ROW LEVEL SECURITY`; `DROP POLICY IF EXISTS ba_dmo_app_access`; `CREATE POLICY ba_dmo_app_access ... USING (true) WITH CHECK (true)`.
  - Tables: `pegamento_documentos`, `tool_usage_records`, `repairer_repair_types`, `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event`, `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events`, `jobon_user_current`.
- `DO` block: for roles `anon`/`authenticated` (guarded), `REVOKE ALL ON TABLE` each of the same 10.
- `GRANT SELECT, INSERT, UPDATE, DELETE` on the same 10 tables to `ba_dmo_app`.

**§3 — Performance index:**
- `CREATE INDEX IF NOT EXISTS ix_audit_events_module_time` on `audit_events (module_id, occurred_at_utc)`.

**Depends on:** N01 `internal_users` and `ba_dmo_guard_append_only` (function), N06 `peso_controlos`, N05 job_on family (`job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`), N03 `bq_traces`, N07 `pegamento_controlos`, N08 `repair_exit_items`, and the N14/N19/N20/N21/N23/N24 tables in the `late_tables` set.

## 29. N26 — user modules override

File:
`database\migrations\N26_user_modules_override.sql`

**Alters:**
- `ALTER TABLE internal_users ADD COLUMN IF NOT EXISTS modules_override jsonb NULL`.

No type default (nullable, unindexed), no CHECK, no index, no backfill, no comment.

**Depends on:** N01 `internal_users`.

## 30. N27 — access convergence

File:
`database\migrations\N27_access_convergence.sql`

**PURPOSE:** Final access-model convergence: one-or-more templates per user (junction
table), three closed functional profiles (Admin / Operador / Controlador / Responsável),
and module-only template grants. Legacy `internal_users.template_id` and
`internal_users.modules_override` remain physically present for compatibility/auditability.
This file carries the family's **first reconciliation DML** (see Data Migration Statements).

**Creates (table):**
- `internal_user_access_templates` (PK `(actor_id, template_id)`; FKs
  `actor_id → internal_users`, `template_id → access_templates`; `assigned_at_utc`,
  `assigned_by`).

**Indexes:**
- `ix_internal_user_access_templates_template` (`template_id, actor_id`).

**Alters (`internal_users`):**
- `ALTER COLUMN profile_title SET NOT NULL`.
- `DROP CONSTRAINT IF EXISTS ck_internal_users_functional_profile` + `ADD CONSTRAINT`
  CHECK (`profile_title IN ('Admin','Operador / Controlador','Responsável')`).

**Data migration statements (in file order):**
1. `UPDATE internal_users` — infer `profile_title` from the user's template grants
   (`modules @> '[{"moduleId":"admin"}]'` → Admin; capability values
   `jobon.edit/jobon.configure/peso.aprovar/controlo.review/ferramentas.configure` →
   Responsável; else Operador / Controlador), applied only where `profile_title` is
   NULL or outside the three closed values.
2. `INSERT INTO access_templates ... SELECT 'legacy-override-' || substr(md5(actor_id),1,24) ...`
   — materialize each non-null `modules_override` as a private compatibility template
   (`ON CONFLICT DO NOTHING`).
3. `INSERT INTO internal_user_access_templates ... SELECT actor_id, template_id, created_at_utc, NULL
   FROM internal_users ON CONFLICT DO NOTHING` — first junction assignments from the legacy column.
4. `INSERT INTO internal_user_access_templates ... SELECT actor_id, 'legacy-override-' || ...`
   — override-derived assignments for users with a non-null `modules_override`.
5. `UPDATE access_templates` — rewrite `modules` to top-level module ids only,
   `capabilities` emptied to `[]`, mapping legacy `peso`/`pegamentos` grants → `controlo`;
   unrelated module ids are dropped from the array.
6. `UPDATE internal_users SET modules_override = NULL` — the override is made dormant
   (no DROP; column kept).

**RLS / Policies / Grants:**
- `ALTER TABLE internal_user_access_templates ENABLE ROW LEVEL SECURITY`.
- `REVOKE ALL ON TABLE` from `anon`/`authenticated` (guarded by role existence).
- `GRANT SELECT, INSERT, UPDATE, DELETE ... TO ba_dmo_app`.
- Policy `internal_user_access_templates_app_access` (`FOR ALL TO ba_dmo_app USING (true)
  WITH CHECK (true)`). NOTE: policy name diverges from the N12/N25 convention
  (`ba_dmo_app_access`) — see RLS / Policy Evolution.

**Depends on:** N01 `access_templates`, N01 `internal_users` (junction FKs + the
legacy `template_id`/`modules_override` columns read/written by the DML).

**MIGRATION PROVENANCE NEEDS REVIEW:** N27's header — unlike N01–N26 and N28–N31 —
carries no `Authority:` / owner-decision reference, so the decision trail behind the
profile-inference rules, the `legacy-override-<md5>` template materialization, and the
peso/pegamentos → controlo module rewrite is not documented in-file.

## 31. N28 — reparacao interna cm mf only

File:
`database\migrations\N28_reparacao_interna_cm_mf_only.sql`

**PURPOSE:** DB-level convergence to the domain contract that BQ is never a Reparação
Interna recordable tool type (BQ appears only inside production/reference context).
N22 temporarily widened `ck_internal_repair_records_type` to CM/MF/BQ; N28 reverses
that at the constraint level. N28 never deletes or rewrites a repair record — it
**fails closed** if legacy non-CM/MF rows exist.

**Alters (`internal_repair_records`):**
- Fail-closed guard `DO`: `RAISE EXCEPTION` when any `tool_type NOT IN ('CM','MF')` row exists.
- `DROP CONSTRAINT IF EXISTS ck_internal_repair_records_type`.
- `ADD CONSTRAINT ck_internal_repair_records_type CHECK (tool_type IN ('CM','MF')) NOT VALID`.
- `VALIDATE CONSTRAINT ck_internal_repair_records_type` (checks the existing rows without
  re-scanning the table at add time).

**Transaction shape:** the file opens and commits an explicit `BEGIN; ... COMMIT;`
(like the runner's own per-script transaction; first file in the family with an
explicit transaction wrapper — N29 and N30 follow the same pattern).

**Depends on:** N08 `internal_repair_records` (created), N22 (CHECK widened to BQ —
the exact constraint N28 re-narrows). This is a deliberate convergence of the N22
change, **not** duplication.

## 32. N29 — jobon reference images

File:
`database\migrations\N29_jobon_reference_images.sql`

**PURPOSE:** Converge the Job On article image from per-revision metadata
(N05 `job_on_revision.image_asset_id`) to one current association owned by the
normalized master Article/Reference context. Legacy `image_asset_id` remains dormant
and is **not** dropped. Legacy current associations are migrated only when safe and
unambiguous; otherwise the migration fails closed.

**Creates (table):**
- `article_reference_images` (PK `reference_code text`; `image_asset_id text NOT NULL`;
  `updated_by` FK → `internal_users`; `updated_at_utc`).

**Constraints:**
- `ck_article_reference_images_reference` CHECK (`reference_code` non-empty and
  `reference_code = upper(btrim(reference_code))`).
- `ck_article_reference_images_asset` CHECK (`image_asset_id` non-empty, trimmed,
  no `/`, no `\`, no `..`, and ends `.jpe?g|png|gif|webp|bmp` case-insensitive).

**Data migration statements (guarded):**
1. Guard `DO`: raises if any legacy current revision carries an `image_asset_id`
   without a readable Reference or with a path-like/unsupported asset name.
2. Guard `DO`: raises if the same Reference is promoted from conflicting current
   images (`count(DISTINCT image_asset_id) > 1`).
3. `INSERT INTO article_reference_images ... SELECT DISTINCT ON (reference_code) ...
   ORDER BY reference_code, saved_at_utc DESC ... ON CONFLICT (reference_code) DO NOTHING`
   — promotion of the newest safe legacy association per Reference.

**RLS / Policies / Grants:**
- `ALTER TABLE article_reference_images ENABLE ROW LEVEL SECURITY`.
- `GRANT SELECT, INSERT, UPDATE, DELETE` + `DROP POLICY IF EXISTS ba_dmo_app_access` +
  `CREATE POLICY ba_dmo_app_access ... USING (true) WITH CHECK (true)` — all inside a
  `DO` guarded by `ba_dmo_app` role existence (plain PostgreSQL without the role gets
  RLS enabled but no policy/grant).
- Policy name follows the N12/N25 convention (`ba_dmo_app_access`).

**Depends on:** N01 `internal_users` (FK), N05 `job_on`/`job_on_revision` (reads
`job_on.current_revision_id` → `job_on_revision.image_asset_id` +
`reference_snapshot`).

## 33. N30 — jobon reference image updated_by index

File:
`database\migrations\N30_jobon_reference_image_updated_by_index.sql`

**PURPOSE:** Additive performance convergence (post-N29 advisor check): covering index
for the `article_reference_images.updated_by` FK.

**Indexes:**
- `ix_article_reference_images_updated_by` (`ON article_reference_images (updated_by)`),
  created `IF NOT EXISTS`.

**Transaction shape:** explicit `BEGIN; ... COMMIT;`.

**Depends on:** N29 `article_reference_images`.

## 34. N31 — template profiles single assignment

File:
`database\migrations\N31_template_profiles_single_assignment.sql`

**PURPOSE:** Template-owned functional profile + single effective template per user
(Aplicações → Template (title + profile + modules) → User (one template)). A template
is reusable; assigning it determines both the user's functional profile and the
authorized modules. Historical audit rows are untouched. N31 supersedes the
N27 "one-or-more templates per user" model by collapsing assignments to
`internal_users.template_id` (the compatibility/authority pointer) and enforcing one
row per user.

**Creates (table):**
- `access_template_profiles` (PK `template_id` FK → `access_templates`
  `ON DELETE CASCADE`; `functional_profile text NOT NULL`; `updated_at_utc`).

**Constraints:**
- `ck_access_template_profiles_functional_profile` CHECK
  (`functional_profile IN ('Admin','Operador / Controlador','Responsável')`).

**Functions:**
- `ba_dmo_ensure_access_template_profile()` — `AFTER INSERT` trigger function; inserts a
  deterministic initial profile for a new template (Admin when
  `modules @> '[{"moduleId":"admin"}]'`, else Operador / Controlador),
  `ON CONFLICT (template_id) DO NOTHING`.

**Triggers:**
- `trg_access_templates_ensure_profile` — `AFTER INSERT ON access_templates`, uses
  `ba_dmo_ensure_access_template_profile`. (The family's only `AFTER INSERT` trigger;
  all others are `BEFORE UPDATE OR DELETE` append-only guards.)

**Data migration statements:**
1. `INSERT INTO access_template_profiles ... SELECT ... FROM access_templates
   ON CONFLICT DO NOTHING` — backfill: prefer the agreed user profile
   (`MIN(profile_title)` with `HAVING COUNT(DISTINCT profile_title) = 1`), else the
   deterministic module/name fallback (admin module → Admin; name `LIKE '%respons%'` →
   Responsável; else Operador / Controlador).
2. `DELETE FROM internal_user_access_templates ut USING internal_users u
   WHERE ut.actor_id = u.actor_id AND ut.template_id <> u.template_id` — collapse any
   remaining hybrid (multi-template) assignments.
3. `INSERT INTO internal_user_access_templates (actor_id, template_id, ...)
   SELECT u.actor_id, u.template_id, ... FROM internal_users u ON CONFLICT DO NOTHING`
   — re-materialize the single effective assignment.

**Indexes:**
- `ux_internal_user_access_templates_actor` — `CREATE UNIQUE INDEX IF NOT EXISTS` on
  `internal_user_access_templates (actor_id)` — enforces one template per user.

**Alters (`internal_users`):**
- `UPDATE internal_users SET profile_title = p.functional_profile` — keeps the legacy
  compatibility column in sync with the selected template (data backfill, no DDL).

**RLS / Policies / Grants:**
- `ALTER TABLE access_template_profiles ENABLE ROW LEVEL SECURITY`.
- `REVOKE ALL ON TABLE` from `anon`/`authenticated` (guarded).
- `GRANT SELECT, INSERT, UPDATE, DELETE ... TO ba_dmo_app`.
- Policy `access_template_profiles_app_access` (`FOR ALL TO ba_dmo_app USING (true)
  WITH CHECK (true)`) — again the `{table}_app_access` naming (see RLS / Policy Evolution).

**Depends on:** N01 `access_templates`/`internal_users`, N27 `internal_user_access_templates`
(the junction rows N31 collapses; N31 also reads N27's `template_id`-legacy pointer).

## Migration Dependencies

Only direct technical dependencies visible from SQL.

- **N02** → none.
- **N03** → N01 (`internal_users`, `ba_dmo_guard_append_only`).
- **N04** → N01 (`internal_users`).
- **N05** → N01 (`internal_users`), N04 (`tool_references`, `tool_lotes`, `tool_check_rules` — FKs in component/verification tables).
- **N06** → N01 (`internal_users`), N05 (`job_on`, `job_on_revision` — NOT NULL FKs in `peso_controlos`).
- **N07** → N01 (`internal_users`), N05 (`job_on`, `job_on_revision` — NOT NULL FKs).
- **N08** → N01 (`internal_users`), N03 (`bq_lotes`), N04 (`physical_pieces`).
- **N09** → N01 (`internal_users`), N04 (`tool_lotes`), N08 (`repair_exits`).
- **N10** → N01 (`internal_users`).
- **N11** → N01 (`internal_users`).
- **N12** → all N01–N11 tables + `schema_migrations` (the RLS-target table list must exist at runtime).
- **N13** → N05 (`job_on`).
- **N14** → FK to N07 `pegamento_controlos`, N01 `internal_users`.
- **N15** → N07 (`pegamento_medicoes`).
- **N16** → N07 (`pegamento_controlos`).
- **N17** → N07 (`pegamento_controlos`).
- **N18** → N03 (`bq_movements`), N08 (`repairers` — FK).
- **N19** → N01 (`internal_users`), N04 (`tool_lotes`).
- **N20** → N08 (`repairers`).
- **N21** → N01 (`internal_users`), N10 (`tampao_configurations`).
- **N22** → N05 (`job_on_revision` — FK), N08 (`internal_repair_records`).
- **N23** → N01 (`internal_users`), N04 (`tool_references`, `tool_lotes`), N05 (`job_on`, `job_on_revision`).
- **N24** → N01 (`internal_users`), N05 (`job_on`).
- **N25** → N01 (`internal_users`, `ba_dmo_guard_append_only`), N03 (`bq_traces`), N05 job_on family, N06 (`peso_controlos`), N07 (`pegamento_controlos`), N08 (`repair_exit_items`), plus the 10 post-N12 tables it covers (`pegamento_documentos`, `tool_usage_records`, `repairer_repair_types`, tampão machine/note/event, `controlo_sheets`/items/events, `jobon_user_current`).
- **N26** → N01 (`internal_users`).
- **N27** → N01 (`access_templates`, `internal_users` — junction FKs plus the legacy `template_id`/`modules_override` columns read/written by the DML).
- **N28** → N08/N22 (`internal_repair_records` — re-narrows the CHECK that N22 widened; fail-closed guard on legacy data).
- **N29** → N01 (`internal_users` — `updated_by` FK), N05 (`job_on`/`job_on_revision` — reads `current_revision_id` → `image_asset_id` + `reference_snapshot`).
- **N30** → N29 (`article_reference_images`).
- **N31** → N01 (`access_templates`, `internal_users`), N27 (`internal_user_access_templates` — collapses hybrid assignments, adds the single-template unique index).
- **N32** → N01 (`access_templates`, `internal_users`), N27 (`internal_user_access_templates` — fail-closed guards read the junction), N31 (`access_template_profiles` — completeness backfill).
- **N33** → N01/N27 (`internal_users` — `profile_title` relax + column-level grants), N27 (`internal_user_access_templates` — REVOKE).
- **N34** → N27 (`internal_user_access_templates` — drop), N01/N27 (`internal_users.profile_title` + `ck_internal_users_functional_profile` — drop). Runs last in the mirror lifecycle; every mirror-dependent migration (N27/N31/N32/N33) has already executed.
- **N35** → N03 (`bq_movements` — index), N14 (`pegamento_documentos` — redundant index drop).
- **N36** → N31 (`access_template_profiles` — policy rename; the junction policy pair died in N34).

## Object-to-Migration Index

| Object | Created In | Later Altered By |
|---|---|---|
| `internal_users` | N01 | N25 (auth_user_id NOT NULL + UNIQUE), N26 (modules_override), N27 (profile_title NOT NULL + `ck_internal_users_functional_profile`), N31 (profile_title backfill sync), N33 (profile_title DROP NOT NULL; column-level SELECT/INSERT/UPDATE grants excluding the mirror), **N34 (`profile_title` column + `ck_internal_users_functional_profile` REMOVED**) |
| `access_templates` | N01 | N27 (legacy-override compatibility templates materialized; `modules` rewritten to top-level-only), N31 (profile trigger) |
| `audit_events` | N01 | N25 (index `ix_audit_events_module_time`) |
| `ba_dmo_guard_append_only()` | N01 | — |
| `module_catalog_mirror` | N02 | — |
| `bq_lotes` | N03 | — |
| `bq_traces` | N03 | N25 (partial unique `uq_bq_traces_active`) |
| `bq_movements` | N03 | N18 (noted_repairer_id), **N35 (index `ix_bq_movements_noted_repairer`)** |
| `bq_discrepancies`, `bq_lifecycle_history`, `bq_utilisation_readings` | N03 | — |
| `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences` | N04 | — |
| `job_on` | N05 | N13 (production_folder), N25 (`uq_job_on_identity`, `ck_job_on_lifecycle_consistent`) |
| `job_on_revision` | N05 | N25 (append-only trigger), N29 (legacy `image_asset_id` consumed as source for `article_reference_images`; column kept dormant) |
| `job_on_component` | N05 | N25 (append-only trigger) |
| `job_on_component_field` | N05 | N25 (append-only trigger) |
| `job_on_component_row` | N05 | N25 (append-only trigger) |
| `job_on_verification_occurrence` | N05 | N25 (`ck_job_on_verification_completed`) |
| `job_on_audit_event` | N05 | — |
| `job_on_field_option` | N05 | — |
| `peso_references`, `peso_lotes`, `peso_leituras`, `peso_comparacao_anterior`, `peso_day_approvals`, `peso_settings` | N06 | — |
| `peso_controlos` | N06 | N25 (`ck_peso_controlos_approved_consistent`, `ba_dmo_guard_peso_approved` + trigger) |
| `pegamento_controlos` | N07 | N16 (cm/bq/mf_nominal), N17 (notas), N25 (`ck_pegamento_controlos_status`) |
| `pegamento_medicoes` | N07 | N15 (tool_number + index) |
| `repairers` | N08 | — |
| `line_repairer_defaults` | N08 | — |
| `repair_exits` | N08 | — |
| `repair_exit_items` | N08 | N25 (`ck_repair_exit_items_status`) |
| `repair_events` | N08 | — |
| `internal_repair_records` | N08 | N22 (tool_type BQ + context columns + FK), N28 (tool_type re-narrowed to CM/MF, `NOT VALID` + `VALIDATE`) |
| `warehouse_locations`, `warehouse_stock`, `warehouse_movements` | N09 | — |
| `tampao_field_defs`, `tampao_field_values`, `tampao_configurations`, `tampao_saldos`, `tampao_movements`, `tampao_planos` | N10 | — |
| `app_settings` | N11 | — |
| `pegamento_documentos` | N14 | N25 (RLS/policy/grants), **N35 (redundant standalone index `ix_pegamento_documentos_controlo` REMOVED; UNIQUE constraint index remains)** |
| `tool_usage_records` | N19 | N25 (RLS/policy/grants) |
| `repairer_repair_types` | N20 | N25 (RLS/policy/grants) |
| `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event` | N21 | N25 (RLS/policy/grants) |
| `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events` | N23 | N25 (RLS/policy/grants) |
| `jobon_user_current` | N24 | N25 (RLS/policy/grants) |
| `internal_user_access_templates` | N27 | N31 (collapse to a single row per user; unique `ux_internal_user_access_templates_actor`), N32 (fail-closed guards), N33 (privilege quiescence), **N34 (TABLE REMOVED — mirror removed with its PK/FKs/indexes/policy/default)** |
| `article_reference_images` | N29 | N30 (index `ix_article_reference_images_updated_by`) |
| `access_template_profiles` | N31 | **N36 (policy renamed `access_template_profiles_app_access` → `ba_dmo_app_access`)** |

## Constraint / Index Origin

Later remediation/additive and partial unique items:

| Constraint / Index | Object | Migration |
|---|---|---|
| `uq_internal_users_auth_user` | `internal_users` | N25 |
| `internal_users.auth_user_id NOT NULL` | `internal_users` | N25 |
| `uq_job_on_identity` (partial unique WHERE `canceled_at_utc IS NULL`) | `job_on` | N25 |
| `uq_bq_traces_active` (partial unique WHERE `status='active'`) | `bq_traces` | N25 |
| `uq_warehouse_stock_active_occupation` (partial unique WHERE `released_at_utc IS NULL`) | `warehouse_stock` | N09 |
| `ck_job_on_lifecycle_consistent` | `job_on` | N25 |
| `ck_job_on_verification_completed` | `job_on_verification_occurrence` | N25 |
| `ck_pegamento_controlos_status` | `pegamento_controlos` | N25 |
| `ck_repair_exit_items_status` | `repair_exit_items` | N25 |
| `ck_peso_controlos_approved_consistent` | `peso_controlos` | N25 |
| `ck_internal_repair_records_type` (created CM/MF → N22 widened to BQ → N28 re-narrowed to CM/MF) | `internal_repair_records` | N08 created / N22 redefined / N28 redefined again |
| `ix_pegamento_medicoes_component_tool` | `pegamento_medicoes` | N15 |
| `ix_audit_events_module_time` | `audit_events` | N25 |
| `ck_internal_users_functional_profile` | `internal_users` | N27 (**N34 removed with the column**) |
| `ix_internal_user_access_templates_template` | `internal_user_access_templates` | N27 (**N34 removed with the table**) |
| `ck_article_reference_images_reference` | `article_reference_images` | N29 |
| `ck_article_reference_images_asset` | `article_reference_images` | N29 |
| `ix_article_reference_images_updated_by` | `article_reference_images` | N30 |
| `ck_access_template_profiles_functional_profile` | `access_template_profiles` | N31 |
| `ux_internal_user_access_templates_actor` (unique, one row per user) | `internal_user_access_templates` | N31 (**N34 removed with the table**) |
| `ix_bq_movements_noted_repairer` | `bq_movements` | **N35** |
| `ix_pegamento_documentos_controlo` (redundant — duplicates UNIQUE) | `pegamento_documentos` | N14 (**N35 removed**) |
| `fk_repair_events_internal_record` | `repair_events` | N08 |
| `fk_internal_repair_records_revision` | `internal_repair_records` | N22 |
| `fk_job_on_current_revision` (circular) | `job_on` | N05 |

## Functions / Triggers Origin

**Functions:**
| Function | Migration |
|---|---|
| `ba_dmo_guard_append_only()` | N01 |
| `ba_dmo_guard_peso_approved()` | N25 |
| `ba_dmo_ensure_access_template_profile()` | N31 |

**Triggers (all `BEFORE UPDATE OR DELETE` except `trg_access_templates_ensure_profile`, which is `AFTER INSERT`; all use `ba_dmo_guard_append_only` except `trg_peso_controlos_approved_guard` (`ba_dmo_guard_peso_approved`) and `trg_access_templates_ensure_profile` (`ba_dmo_ensure_access_template_profile`)):**
| Trigger | Table | Migration |
|---|---|---|
| `trg_audit_events_append_only` | `audit_events` | N01 |
| `trg_bq_movements_append_only` | `bq_movements` | N03 |
| `trg_bq_lifecycle_history_append_only` | `bq_lifecycle_history` | N03 |
| `trg_bq_utilisation_readings_append_only` | `bq_utilisation_readings` | N03 |
| `trg_job_on_audit_event_append_only` | `job_on_audit_event` | N05 |
| `trg_pegamento_medicoes_append_only` | `pegamento_medicoes` | N07 |
| `trg_repair_events_append_only` | `repair_events` | N08 |
| `trg_warehouse_movements_append_only` | `warehouse_movements` | N09 |
| `trg_tampao_movements_append_only` | `tampao_movements` | N10 |
| `trg_tool_usage_records_append_only` | `tool_usage_records` | N19 |
| `trg_tampao_configuration_notes_append_only` | `tampao_configuration_notes` | N21 |
| `trg_tampao_configuration_machine_event_append_only` | `tampao_configuration_machine_event` | N21 |
| `trg_controlo_sheet_events_append_only` | `controlo_sheet_events` | N23 |
| `trg_job_on_revision_append_only` | `job_on_revision` | N25 |
| `trg_job_on_component_append_only` | `job_on_component` | N25 |
| `trg_job_on_component_field_append_only` | `job_on_component_field` | N25 |
| `trg_job_on_component_row_append_only` | `job_on_component_row` | N25 |
| `trg_peso_controlos_approved_guard` (`ba_dmo_guard_peso_approved`) | `peso_controlos` | N25 |
| `trg_access_templates_ensure_profile` (AFTER INSERT; `ba_dmo_ensure_access_template_profile`) | `access_templates` | N31 |

## RLS / Policy Evolution

Migration-level evolution only.

**N12** (RLS / policies / grants / revokes, tables existing at N12 SQL):
- RLS enabled on 49 tables (48 application + `schema_migrations`).
- `ba_dmo_app_access` policy (`FOR ALL TO ba_dmo_app USING(true) WITH CHECK(true)`) created on 48 application tables (not `schema_migrations`).
- `REVOKE ALL` on all public tables/sequences from `anon`/`authenticated` (guarded).
- `GRANT` CRUD on all public tables/sequences to `ba_dmo_app`.

**Tables created after N12** (no RLS stanza in their own files):
- N14 `pegamento_documentos`, N19 `tool_usage_records`, N20 `repairer_repair_types`, N21 three tampão tables, N23 three controlo tables, N24 `jobon_user_current`.

**N25** (RLS / policies / revokes / grants for the 10 post-N12 tables):
- RLS enabled + `ba_dmo_app_access` policy created on each of the 10 late tables.
- `REVOKE ALL ON TABLE` each of the 10 from `anon`/`authenticated` (guarded).
- `GRANT SELECT, INSERT, UPDATE, DELETE` on the 10 to `ba_dmo_app`.

**N27** (RLS for `internal_user_access_templates`):
- RLS enabled + policy `internal_user_access_templates_app_access` (`FOR ALL TO ba_dmo_app
  USING (true) WITH CHECK (true)`) + guarded anon/authenticated REVOKE + `ba_dmo_app` DML grant.
- **N34 removes the table — the policy dies with it.**

**N29** (RLS for `article_reference_images`):
- RLS enabled + policy `ba_dmo_app_access` + `ba_dmo_app` DML grant — the whole grant/policy
  block is inside a `DO` guarded by `ba_dmo_app` role existence.

**N31** (RLS for `access_template_profiles`):
- RLS enabled + policy `access_template_profiles_app_access` + guarded anon/authenticated
  REVOKE + `ba_dmo_app` DML grant.
- **N36 (D-15) renames the policy to `ba_dmo_app_access` with identical semantics.**

**N33** (privilege quiescence for the mirrors — non-RLS security surface):
- Junction `REVOKE ALL` from `ba_dmo_app`; `internal_users` column-level SELECT/INSERT/UPDATE
  grants for the 8 canonical columns (excluding the retired `profile_title`; DELETE untouched).

**Policy naming note:** resolved. N12/N25/N29/N36 use the single convention `ba_dmo_app_access`;
the two historical exceptions (`internal_user_access_templates_app_access` N27,
`access_template_profiles_app_access` N31) are gone — the first died with the junction table in
N34, the second was renamed by N36 (D-15). All post-N36 migrations must use `ba_dmo_app_access`.

**Migration-level totals (post-N34/N36):**
- RLS-enabled application tables = **60** (61 N33-era app tables minus the junction removed by N34);
  RLS-enabled tables incl. `schema_migrations` (no policy) = **61**.
- `ba_dmo_app` access policies implied by N12 (48) + N25 (10) + N29 (1, guarded) + N31→N36 (1) =
  **60**, every one named `ba_dmo_app_access`.

## Data Migration Statements

N01–N26 contain **no** data migration/backfill statements (every `INSERT`/`UPDATE`
match in those files is a `BEFORE UPDATE OR DELETE` trigger clause; N01/N02/N10/N11
comments confirm no operational seeds).

**N27, N29, N31 introduce the family's first reconciliation DML** (all guarded/idempotent):

- **N27** — profile-title inference `UPDATE` on `internal_users`; `INSERT ... SELECT
  ON CONFLICT DO NOTHING` materializing `legacy-override-<md5>` compatibility templates
  into `access_templates`; junction `INSERT ... SELECT` rows into
  `internal_user_access_templates`; `UPDATE access_templates` rewriting `modules` to
  top-level-only (peso/pegamentos → controlo); `UPDATE internal_users SET
  modules_override = NULL` (dormancy).
- **N29** — guarded `INSERT INTO article_reference_images ... SELECT DISTINCT ON
  (reference_code) ... ON CONFLICT (reference_code) DO NOTHING` promoting the newest
  safe legacy `job_on_revision.image_asset_id` per Reference; two fail-closed `DO`
  guards (unreadable/unsafe names; conflicting images per Reference).
- **N31** — `INSERT ... SELECT ON CONFLICT DO NOTHING` backfill of
  `access_template_profiles` (agreed-user-profile first, deterministic fallback
  otherwise); `DELETE FROM internal_user_access_templates` collapsing rows whose
  `template_id <> internal_users.template_id`; re-`INSERT` of the single effective
  assignment; `UPDATE internal_users SET profile_title = p.functional_profile` sync.
- **N32** — deterministic `INSERT ... SELECT ON CONFLICT DO NOTHING` completeness
  backfill of `access_template_profiles` (deterministic default only; never copies
  user profiles). The N32 fail-closed junction guards raise diagnostics, they do
  not mutate.
- **N33** — no data migration (privilege quiescence only; `DROP NOT NULL` is DDL).
- **N34** — no data migration — **destructive object removal**: the mirrors'
  fossil `profile_title` values and junction rows are discarded by design (dead
  since N33; pre-drop backup is a rollout step of the N34 verification protocol).
- **N35 / N36** — no data migration (index add/drop and policy rename only).

These are data reconciliations tied to schema changes, not operational seeding.

## Migration Execution & Bookkeeping

The migration runner is a custom Npgsql whole-script runner — no EF Core Migrations,
no DbUp, no SQL parsing/splitting (guard-tested by
`MigrationArchitectureGuardTests`). Source:
`src\BA.Dmo.Infrastructure\Persistence\Migrations\`
(`MigrationRunner.cs`, `MigrationDiscovery.cs`, `MigrationFile.cs`,
`MigrationChecksum.cs`, `NpgsqlMigrationScriptGateway.cs`,
`IMigrationScriptGateway.cs`, `MigrationExceptions.cs`).

**Tracking table — `schema_migrations`** (`NpgsqlMigrationScriptGateway.EnsureTrackingTableSql`):
`version text PK`, `filename text NOT NULL`, `sha256 text NOT NULL`, `applied_at timestamptz
NOT NULL DEFAULT now()`, `execution_time_ms integer NULL`. Created `IF NOT EXISTS` before any
run; the table is owned by the migrate role and gets RLS enabled in N12 but **no** app policy
(migrate CLI only). `schema_migrations` is also reproduced in
`database\consolidated_clean_install.sql`.

**Discovery** (`MigrationDiscovery.Discover(migrationsDirectory)`):
only files matching the regex `^(N\d{2})_[A-Za-z0-9_]+\.sql$` are migrations; canonical
order = ordinal file-name order (N01 … N36); duplicate versions fail explicitly
(`MigrationDiscoveryException`); any non-conforming file in the directory aborts discovery.
The shipped-family completeness is asserted by
`MigrationDiscoveryTests.ShippedFreshBuildFamily_IsComplete_N01ThroughN36`.

**Execution** (`MigrationRunner.RunAsync`):
1. ensures `schema_migrations`; loads applied records keyed by version;
2. computes SHA-256 (`MigrationChecksum.ComputeSha256File`) over the **raw file bytes**;
3. per migration in order:
   - never applied → execute the **whole file**; record only after success;
   - applied with the same checksum → skip (idempotent re-runs);
   - applied with a different checksum → `MigrationChecksumMismatchException`, run stops —
     applied migrations are immutable (forward-only family);
4. a failed script (`MigrationExecutionException`) is never recorded and stops the run —
   no later migration executes.

**Transport** (`NpgsqlMigrationScriptGateway.ExecuteScriptAsync`): each script is sent whole
to PostgreSQL as a single command inside one transaction; no statement splitting, no custom
SQL parser, semicolons are data. This is why migrations like N28/N29/N30 can themselves
open `BEGIN; … COMMIT;` — and why every file must be a single valid script.

**CLI** (`src\BA.Dmo.Web\Cli\MigrateCommand.cs`): invoked as `dotnet BA.Dmo.Web.dll migrate`
(verb resolved by `CliModeResolver.Resolve(args)` → `CliMode.Migrate`; CLI mode never starts
the web server; there is no HTTP migration endpoint, guard-tested by
`MigrationArchitectureGuardTests.WebProgram_HasNoMigrationHookBeyondCliVerb`). Connection:
`BA_DMO_DB_CONNECTION_STRING` (preferred) else `DATABASE_URL`; optional
`BA_DMO_MIGRATIONS_DIR` overrides the migrations directory, otherwise `database/migrations`
is located at or above the application base directory (6 levels). Exit codes: 0 success,
1 migration failure (checksum mismatch / execution / discovery), 2 configuration error
(missing connection string or migrations directory).

**Deploy order (ADM-14)** — `migrate` (the whole N01…N36 family, including N33
legacy-access-mirror quiescence AND N34 legacy-access-mirror removal) MUST complete
BEFORE the first user write of the application build that ships with this family.
N33 is what retires the mirrors as runtime objects (relaxes `internal_users.profile_title`
NOT NULL, revokes `ba_dmo_app` privileges on the junction, and switches
`internal_users` to column-level grants); N34 then physically REMOVES the mirrors
(`internal_user_access_templates` + `profile_title` + its CHECK). A fresh application
version writing user rows before N33/N34 is applied would hit a mirror posture or
columns the runtime no longer supports. On a consolidated-built database the same
guarantee holds: run `migrate` (or install the up-to-date
`database/consolidated_clean_install.sql` baseline, which embeds the post-N34 posture)
before opening the app to users.

**Tests** (`AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Migrations\`): `MigrationRunnerTests`
(whole-script byte-for-byte execution, record-only-after-success, same-checksum skip,
checksum-mismatch fail, failed script not recorded + run stops, canonical order, semicolons
inside strings never split), `MigrationDiscoveryTests`, `MigrationChecksumTests` (SHA-256 FIPS
vector), `MigrationArchitectureGuardTests` (no splitting/parsing machinery, no HTTP surface),
`FakeMigrationGateway` (in-memory `IMigrationScriptGateway` double). See
[05_TESTS.md](05_TESTS.md) §5.8/§9.2 and [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md).

## Consolidated Clean-Install Baseline

File: `database\consolidated_clean_install.sql` (≈1800 lines at the N01–N36 verification).

- One-pass clean-install baseline reproducing the **final effective schema** of the
  migration family; it is NOT a migration and must live OUTSIDE `database/migrations\`
  (every `*.sql` in that folder must match the `N##_<name>.sql` family pattern or
  `MigrationDiscovery` aborts).
- Guarded role/default-privilege statements so it also runs on Supabase Hosted
  (privilege-heavy statements become NOTICEs when the project role lacks entitlement).
- Also reproduces `schema_migrations` and the RLS/policy/grants contract.

**Verification result (N34–N36 session refresh):** the baseline was parity-hardened by
Queue A (PC-10/CB-01..05: N31 objects + N29 RLS stanza + post-N33 mirror posture +
corrected header) and is now extended to the **N01–N36 final state** (this session):
the legacy access mirrors are absent (`internal_user_access_templates` removed; the
`internal_users.profile_title` replica and its CHECK removed — N34 final state), the
redundant `ix_pegamento_documentos_controlo` is gone and `ix_bq_movements_noted_repairer`
is present (N35 final state), and the `access_template_profiles` policy is created under
the convention name `ba_dmo_app_access` (N36 final state). The family test referenced by
this map is `ShippedFreshBuildFamily_IsComplete_N01ThroughN36`; the tail comment reads
"includes N25-N36".

## Sources Verified

Primary evidence — every numbered migration file read in full from:
`database\migrations\` (N01–N36, all 36 files, at HEAD 8d916cb + the Queue A working
tree and the N34–N36 change sets).

Migration execution/bookkeeping — read in full from:
- `src\BA.Dmo.Infrastructure\Persistence\Migrations\MigrationRunner.cs`
- `src\BA.Dmo.Infrastructure\Persistence\Migrations\MigrationDiscovery.cs`
- `src\BA.Dmo.Infrastructure\Persistence\Migrations\MigrationFile.cs`
- `src\BA.Dmo.Infrastructure\Persistence\Migrations\MigrationChecksum.cs`
- `src\BA.Dmo.Infrastructure\Persistence\Migrations\NpgsqlMigrationScriptGateway.cs`
- `src\BA.Dmo.Infrastructure\Persistence\Migrations\IMigrationScriptGateway.cs`
- `src\BA.Dmo.Infrastructure\Persistence\Migrations\MigrationExceptions.cs`
- `src\BA.Dmo.Web\Cli\MigrateCommand.cs`, `CliMode.cs`, `CliModeResolver.cs`

Migration tests:
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Migrations\MigrationRunnerTests.cs`
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Migrations\MigrationDiscoveryTests.cs`
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Migrations\MigrationChecksumTests.cs`
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Migrations\MigrationArchitectureGuardTests.cs`
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Migrations\FakeMigrationGateway.cs`

Consolidated baseline:
- `database\consolidated_clean_install.sql` — N01–N36 final state verified this session
  (mirrors removed; N35 index deltas; N36 policy naming; header/tail corrected).

Contract/registry: `AI-CONTEXT\docs\Maps\00_INDEX.md`.

Notes:
- The map was last verified 2026-08-23 for N01–N26; Queue A and the N34–N36 session bring
  the inventory through N36 (all post-2026-08-23 migrations added) and the
  execution/bookkeeping section.
- Cross-linked maps: [02_DATABASE.md](02_DATABASE.md) (final schema),
  [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md),
  [05_TESTS.md](05_TESTS.md), [00_INDEX.md](00_INDEX.md).
- Not used as migration evidence: `01_DOMAIN.md`, Design/SOT, other Application source.

**Paired-map lag (out of scope, flagged):** [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md),
[15_ADMIN.md](15_ADMIN.md), [16_USERS_ACCESS.md](16_USERS_ACCESS.md), [18_LOGIN.md](18_LOGIN.md),
[19_APPLICATION.md](19_APPLICATION.md), [20_WEB.md](20_WEB.md) still carry pre-03A/03B
narrative (junction joins, `TemplateProfileStore`, mirror writes). They were already stale
at 03A/03B (see reports/schema_rationalization_N34_legacy_mirror_removal_audit.md §3.4)
and remain a separate documentation refresh item; [02_DATABASE.md](02_DATABASE.md)'s
mirror-object entries were refreshed as part of the N34–N36 change set.