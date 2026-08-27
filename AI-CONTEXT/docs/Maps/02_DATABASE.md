# BA DMO — Database Technical Map

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
- [15. RLS / Least-Privilege Overview](#15-rls--least-privilege-overview)
- [16. Table Category Summary](#16-table-category-summary)
- [Sources Verified](#sources-verified)

Related maps: [00_INDEX.md](00_INDEX.md) · [01_DOMAIN.md](01_DOMAIN.md) · [03_MIGRATIONS.md](03_MIGRATIONS.md) · [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md) · [05_TESTS.md](05_TESTS.md) · [19_APPLICATION.md](19_APPLICATION.md) · [20_WEB.md](20_WEB.md) · module maps [06_JOB_ON.md](06_JOB_ON.md) · [07_CONTROLO.md](07_CONTROLO.md) · [08_FERRAMENTAS.md](08_FERRAMENTAS.md) · [09_ARMAZEM.md](09_ARMAZEM.md) · [10_BOQUILHAS.md](10_BOQUILHAS.md) · [11_REPARACAO_INTERNA.md](11_REPARACAO_INTERNA.md) · [12_REPARACAO_EXTERNA.md](12_REPARACAO_EXTERNA.md) · [13_TAMPOES.md](13_TAMPOES.md) · [14_HISTORIA.md](14_HISTORIA.md) · [15_ADMIN.md](15_ADMIN.md) · [16_USERS_ACCESS.md](16_USERS_ACCESS.md)

## 1. Purpose

- `02_DATABASE.md` is the FINAL DATABASE MODEL map: the complete table inventory derived from the current migration chain `database\migrations\N01_identity.sql` … `N31_template_profiles_single_assignment.sql`.
- For every application table it records: MODULE/OWNER, ORIGIN MIGRATION, LATER ALTERATIONS, PRIMARY KEY, IMPORTANT FOREIGN KEYS, UNIQUE CONSTRAINTS, CHECK CONSTRAINTS, IMPORTANT INDEXES, TRIGGERS, RLS/POLICY NOTES, DAPPER CONSUMERS.
- It is technical navigation only. Migration-by-migration object history lives in [03_MIGRATIONS.md](03_MIGRATIONS.md); Dapper query details live in [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md).
- Authority: current migrations (schema ground truth) > current Infrastructure source > previous map text. Where a statement cannot be evidenced the map says `NEEDS REVIEW` — nothing is invented.

## 2. Database Source Structure

- Migration family: `database\migrations\N01_identity.sql` … `N31_template_profiles_single_assignment.sql` (31 numbered files, forward-only, idempotent, executed WHOLE by the Npgsql migration runner tracked in `schema_migrations`).
- `database\consolidated_clean_install.sql` — consolidated clean-install script. **SCHEMA DRIFT — NEEDS AUDIT** (see [03_MIGRATIONS.md](03_MIGRATIONS.md)): its contents do not include the N31 objects (access_template_profiles family); verified by comparing it with N31. It is used for clean installs only; the migration runner remains the authoritative object source.
- Roles: `ba_dmo_app` (runtime, NOLOGIN) and `ba_dmo_migrate` (DDL, NOLOGIN), created with default privileges for future objects (N01).
- Operational table not part of the application model: `schema_migrations` (migrate CLI tracking; RLS enabled, no app policy).
- Total: **61 application tables** (+ `schema_migrations`), created across N01–N31.

## 3. Global Database Inventory

| # | Table | Origin | Module / Owner | Category |
|---|---|---|---|---|
| 1 | `access_templates` | N01 | Access/Identity | Lookup/config |
| 2 | `internal_users` | N01 | Access/Identity | Current-state |
| 3 | `audit_events` | N01 | Global audit | History/event (append-only) |
| 4 | `module_catalog_mirror` | N02 | Access/Admin display | Lookup/config |
| 5 | `bq_lotes` | N03 | Boquilhas | Current-state |
| 6 | `bq_traces` | N03 | Boquilhas | Current-state |
| 7 | `bq_movements` | N03 | Boquilhas | History/event (append-only) |
| 8 | `bq_discrepancies` | N03 | Boquilhas | Current-state |
| 9 | `bq_lifecycle_history` | N03 | Boquilhas | History/event (append-only) |
| 10 | `bq_utilisation_readings` | N03 | Boquilhas | History/event (append-only) |
| 11 | `tool_references` | N04 | Ferramentas | Current-state (master) |
| 12 | `tool_lotes` | N04 | Ferramentas | Current-state |
| 13 | `physical_pieces` | N04 | Ferramentas | Current-state |
| 14 | `tool_check_rules` | N04 | Ferramentas | Current-state/config |
| 15 | `tool_check_occurrences` | N04 | Ferramentas | Current-state |
| 16 | `job_on` | N05 | Job On | Current-state (aggregate) |
| 17 | `job_on_revision` | N05 | Job On | History/immutable snapshot (append-only N25) |
| 18 | `job_on_component` | N05 | Job On | History/immutable snapshot (append-only N25) |
| 19 | `job_on_component_field` | N05 | Job On | History/immutable snapshot (append-only N25) |
| 20 | `job_on_component_row` | N05 | Job On | History/immutable snapshot (append-only N25) |
| 21 | `job_on_verification_occurrence` | N05 | Job On | Current-state |
| 22 | `job_on_audit_event` | N05 | Job On | History/event (append-only) |
| 23 | `job_on_field_option` | N05 | Job On | Lookup/config |
| 24 | `peso_references` | N06 | Peso (Controlo area) | Current-state (master) |
| 25 | `peso_lotes` | N06 | Peso (Controlo area) | Current-state |
| 26 | `peso_controlos` | N06 | Peso (Controlo area) | Current-state |
| 27 | `peso_leituras` | N06 | Peso (Controlo area) | Current-state |
| 28 | `peso_comparacao_anterior` | N06 | Peso (Controlo area) | Current-state (derived) |
| 29 | `peso_day_approvals` | N06 | Peso (Controlo area) | Current-state |
| 30 | `peso_settings` | N06 | Peso (Controlo area) | Lookup/config |
| 31 | `pegamento_controlos` | N07 | Pegamentos (Controlo area) | Current-state |
| 32 | `pegamento_medicoes` | N07 | Pegamentos (Controlo area) | History/event (append-only) |
| 33 | `repairers` | N08 | Reparação Externa/Interna | Lookup/registry |
| 34 | `line_repairer_defaults` | N08 | Reparação Externa/Interna | Lookup/config |
| 35 | `repair_exits` | N08 | Reparação Externa | Current-state |
| 36 | `repair_exit_items` | N08 | Reparação Externa | Current-state |
| 37 | `repair_events` | N08 | Reparação Externa/Interna | History/event (append-only) |
| 38 | `internal_repair_records` | N08 | Reparação Interna | Current-state |
| 39 | `warehouse_locations` | N09 | Armazém | Current-state |
| 40 | `warehouse_stock` | N09 | Armazém | Current-state |
| 41 | `warehouse_movements` | N09 | Armazém | History/event (append-only) |
| 42 | `tampao_field_defs` | N10 | Tampões | Lookup/config |
| 43 | `tampao_field_values` | N10 | Tampões | Lookup/config |
| 44 | `tampao_configurations` | N10 | Tampões | Current-state |
| 45 | `tampao_saldos` | N10 | Tampões | Current-state |
| 46 | `tampao_movements` | N10 | Tampões | History/event (append-only) |
| 47 | `tampao_planos` | N10 | Tampões | Current-state |
| 48 | `app_settings` | N11 | Shared | Lookup/config |
| 49 | `pegamento_documentos` | N14 | Pegamentos (Controlo area) | Current-state (1:1 doc metadata) |
| 50 | `tool_usage_records` | N19 | Ferramentas | History/event (append-only) |
| 51 | `repairer_repair_types` | N20 | Reparação Externa/Interna | Relationship (M:N capability) |
| 52 | `tampao_configuration_machines` | N21 | Tampões | Relationship (M:N) |
| 53 | `tampao_configuration_notes` | N21 | Tampões | History/event (append-only) |
| 54 | `tampao_configuration_machine_event` | N21 | Tampões | History/event (append-only) |
| 55 | `controlo_sheets` | N23 | Controlo | Current-state |
| 56 | `controlo_sheet_items` | N23 | Controlo | Current-state |
| 57 | `controlo_sheet_events` | N23 | Controlo | History/event (append-only) |
| 58 | `jobon_user_current` | N24 | Job On (user context) | Current-state |
| 59 | `internal_user_access_templates` | N27 | Access/Identity | Relationship |
| 60 | `article_reference_images` | N29 | Job On (master article image) | Current-state |
| 61 | `access_template_profiles` | N31 | Access/Identity | Lookup/config |

## 4. Shared / Identity / Access

### `access_templates`

- MODULE/OWNER: Access/Identity (16_USERS_ACCESS.md). ORIGIN: N01. LATER ALTERATIONS: N27 (data UPDATE rewrites `modules` to top-level-only); N31 (AFTER INSERT trigger `trg_access_templates_ensure_profile` → `ba_dmo_ensure_access_template_profile()` creates the template's profile row in `access_template_profiles`).
- PK: `template_id` (text). FKs: none (creation author `created_by` is plain text).
- UNIQUE: — · CHECK: — (grant validation is Application-side: `GrantNormalizer`/`AccessTemplateGrantsParser`).
- INDEXES: `ix_access_templates_active (active)`.
- TRIGGERS: `trg_access_templates_ensure_profile` (AFTER INSERT, N31).
- RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperAdminRepository`, `DapperInternalUserRepository` (src\BA.Dmo.Infrastructure\Access\ and \Identity\).
- CATEGORY: lookup/configuration — reusable access template (modules jsonb `[{moduleId, capabilities[]}]`).

### `internal_users`

- MODULE/OWNER: Access/Identity. ORIGIN: N01. LATER ALTERATIONS: N25 (`auth_user_id` SET NOT NULL + UNIQUE `uq_internal_users_auth_user`; fails closed on NULL legacy rows); N26 (ADD `modules_override` jsonb, nullable per-user override); N27 (data backfill of `profile_title`; `modules_override` made dormant/NULL after materializing `legacy-override-*` compatibility templates; `profile_title` SET NOT NULL + CHECK `ck_internal_users_functional_profile`); N31 (data UPDATE synchronizes `profile_title` from the template's `access_template_profiles.functional_profile`).
- PK: `actor_id` (text). FKs: `template_id → access_templates(template_id)` (NOT NULL).
- UNIQUE: `uq_internal_users_auth_user (auth_user_id)` (N25).
- CHECK: `ck_internal_users_functional_profile` — `profile_title IN ('Admin','Operador / Controlador','Responsável')` (N27).
- IMPORTANT INDEXES: `ix_internal_users_auth_user_id`, `ix_internal_users_active`, `ix_internal_users_template_id`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperAdminRepository`, `DapperInternalUserRepository`.
- CATEGORY: current-state — internal user identity (logical link to Supabase Auth via `auth_user_id`; no FK to `auth.users`).

### `audit_events`

- MODULE/OWNER: global audit (all modules write). ORIGIN: N01. LATER ALTERATIONS: N25 (ADD index `ix_audit_events_module_time (module_id, occurred_at_utc)` — PERF-01 História).
- PK: `audit_event_id` (uuid, DEFAULT `gen_random_uuid()`). FKs: none by design (plain `actor_user_id`/`job_on_id` text/uuid denormalized audit facts).
- UNIQUE: — · CHECK: `ck_audit_events_year_positive (year > 0)`, `ck_audit_events_result (result IN ('succeeded','failed','denied','corrected'))`.
- IMPORTANT INDEXES: `ix_audit_events_year`, `ix_audit_events_module_action (module_id, action_code)`, `ix_audit_events_actor (actor_user_id, year)`, `ix_audit_events_entity (entity_type, entity_id)`, `ix_audit_events_occurred_at`, `ix_audit_events_job_on_id`, `ix_audit_events_module_time` (N25).
- TRIGGERS: `trg_audit_events_append_only` (BEFORE UPDATE OR DELETE → `ba_dmo_guard_append_only()`).
- RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperAdminRepository`, `DapperArmazemRepository`, `DapperBoquilhasRepository`, `DapperFerramentasRepository`, `DapperHistoriaRepository`, `DapperInternalUserRepository`, `DapperPesoRepository`, `DapperRepairRepository`, `DapperReparacaoInternaRepository`, `DapperTampaoRepository` (audit write paths).
- CATEGORY: history/event (global canonical audit, append-only).

### `module_catalog_mirror`

- MODULE/OWNER: Access/Admin display. ORIGIN: N02. LATER ALTERATIONS: none.
- PK: `module_id` (text). FKs: none.
- UNIQUE: — · CHECK: — (rows are written by `AdminMirrorService` via `ModuleCatalogMirrorSynchronizer`, never by free client input).
- INDEXES: `ix_module_catalog_mirror_order (display_order)`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperModuleCatalogMirrorRepository` (read/upsert/delete).
- CATEGORY: lookup/configuration — Admin UI ordering/display mirror; **never grants access** (access resolved from in-code catalog ∩ templates).

### `app_settings`

- MODULE/OWNER: shared. ORIGIN: N11. LATER ALTERATIONS: none.
- PK: `setting_key` (text). FKs: `updated_by → internal_users(actor_id)` (nullable).
- UNIQUE: — · CHECK: —. INDEXES: none.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperAppSettingsReader` (src\BA.Dmo.Infrastructure\Access\); also read by `FileSystemJobOnImageProvider` (image root configuration).
- CATEGORY: lookup/configuration (key/value jsonb; no operational seeds).

### `internal_user_access_templates`

- MODULE/OWNER: Access/Identity. ORIGIN: N27. LATER ALTERATIONS: N31 (data DELETE collapses hybrid assignments to `internal_users.template_id`; INSERT re-materializes exactly one row per user; ADD UNIQUE index `ux_internal_user_access_templates_actor` → ONE effective template per user, single-template model).
- PK: `(actor_id, template_id)`. FKs: `actor_id → internal_users(actor_id)`, `template_id → access_templates(template_id)`.
- UNIQUE: `ux_internal_user_access_templates_actor (actor_id)` (N31, one row per user).
- INDEXES: `ix_internal_user_access_templates_template (template_id, actor_id)`.
- TRIGGERS: none. RLS: policy `internal_user_access_templates_app_access` (N27) + anon/authenticated REVOKE + `ba_dmo_app` grants.
- DAPPER CONSUMERS: `DapperAdminRepository`, `DapperInternalUserRepository`.
- CATEGORY: relationship (user ↔ template junction; single-assignment since N31).

### `access_template_profiles`

- MODULE/OWNER: Access/Identity (16_USERS_ACCESS.md). ORIGIN: N31. LATER ALTERATIONS: none.
- PK: `template_id` (text, FK `access_templates(template_id)` ON DELETE CASCADE).
- CHECK: `ck_access_template_profiles_functional_profile` — `functional_profile IN ('Admin','Operador / Controlador','Responsável')`.
- INDEXES: none. TRIGGERS: none (rows maintained by trigger `trg_access_templates_ensure_profile` on `access_templates` + N31 backfill + Admin template editor).
- RLS: policy `access_template_profiles_app_access` (N31) + anon/authenticated REVOKE + `ba_dmo_app` grants.
- DAPPER CONSUMERS: **none in Infrastructure** — consumed via Web raw SQL in `src\BA.Dmo.Web\Pages\Admin\TemplateProfileStore.cs` (`IDbConnectionFactory` + `Db.*`; see [19_APPLICATION.md](19_APPLICATION.md) §10.4 finding: Web bypasses Application for this table and for the `internal_users.profile_title` sync).
- CATEGORY: lookup/configuration — template-owned functional profile (single template determines user profile + modules).

## 5. Job On

### `job_on`

- MODULE/OWNER: Job On (06_JOB_ON.md). ORIGIN: N05. LATER ALTERATIONS: N13 (ADD `production_folder` text — shared production directory, logical identifier); N25 (UNIQUE partial `uq_job_on_identity (production_code, machine_code) WHERE canceled_at_utc IS NULL`; CHECK `ck_job_on_lifecycle_consistent`).
- PK: `job_on_id` (uuid). FKs: `current_revision_id → job_on_revision` (circular, `fk_job_on_current_revision`, added in N05 DO block); `copied_from_job_on_id → job_on(self)`; `canceled_by, created_by → internal_users`.
- CHECK: `ck_job_on_status` — `status IN ('rascunho','planeado','em_fabrico','fechado','cancelado')`; `ck_job_on_lifecycle_consistent` (N25: `(status='fechado') = (closed_at_utc IS NOT NULL)` AND `(status='cancelado') = (canceled_at_utc IS NOT NULL)`).
- IMPORTANT INDEXES: `ix_job_on_production_code`, `ix_job_on_status`, `ix_job_on_machine_planned (machine_code, planned_start_at)`, `uq_job_on_identity` (partial unique, N25).
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperJobOnRepository`, `DapperJobOnProductionContextLookup`, `DapperJobOnProductionFolderResolver` (src\BA.Dmo.Infrastructure\Access\).
- CATEGORY: current-state — central production context aggregate.

### `job_on_revision`

- MODULE/OWNER: Job On. ORIGIN: N05. LATER ALTERATIONS: N25 (append-only trigger `trg_job_on_revision_append_only`). `image_asset_id` remains present but DORMANT since N29 (legacy per-revision image metadata; current association moved to `article_reference_images`).
- PK: `job_on_revision_id` (uuid). FKs: `job_on_id → job_on`; `saved_by → internal_users`.
- UNIQUE: `uq_job_on_revision_number (job_on_id, revision_number)`. CHECK: `ck_job_on_revision_number (revision_number >= 1)`.
- INDEXES: `ix_job_on_revision_job_on (job_on_id)`.
- TRIGGERS: `trg_job_on_revision_append_only` (N25; `ba_dmo_guard_append_only`).
- RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperJobOnRepository`, `DapperControloProductionContextLookup`, `DapperJobOnActiveContextLookup`, `DapperJobOnProductionContextLookup`.
- CATEGORY: history/immutable snapshot — the attribution anchor for Peso/Pegamentos/Controlo/Reparação Interna (`job_on_revision_id` FKs), append-only since N25.

### `job_on_component`

- MODULE/OWNER: Job On. ORIGIN: N05. LATER ALTERATIONS: N25 (append-only trigger).
- PK: `job_on_component_id` (uuid). FKs: `job_on_revision_id → job_on_revision`; `source_tool_id → tool_references`, `source_lot_id → tool_lotes` (physical Ferramentas links + snapshots).
- CHECK: `ck_job_on_component_family` — `family IN ('MP_CM','MF','BQ','PU','CAL','AN','ARR','PI','CS','TP','FO')`.
- INDEXES: `ix_job_on_component_revision`. TRIGGERS: `trg_job_on_component_append_only` (N25).
- RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperJobOnRepository`, `DapperControloProductionContextLookup`, `DapperJobOnActiveContextLookup`, `DapperJobOnProductionContextLookup`.
- CATEGORY: history/immutable snapshot (append-only since N25).

### `job_on_component_field`

- MODULE/OWNER: Job On. ORIGIN: N05. LATER ALTERATIONS: N25 (append-only trigger).
- PK: `job_on_component_field_id` (uuid). FKs: `job_on_component_id → job_on_component`.
- UNIQUE: `uq_job_on_component_field (job_on_component_id, field_key)`. CHECK: `ck_job_on_component_field_type` — `value_type IN ('text','integer','decimal','boolean','date','select')` (typed columns `value_text/value_integer/value_decimal/value_boolean/value_date`).
- INDEXES: `ix_job_on_component_field_component`. TRIGGERS: `trg_job_on_component_field_append_only` (N25).
- RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperJobOnRepository`, `DapperJobOnProductionContextLookup`.
- CATEGORY: history/immutable snapshot (append-only since N25).

### `job_on_component_row`

- MODULE/OWNER: Job On. ORIGIN: N05. LATER ALTERATIONS: N25 (append-only trigger).
- PK: `job_on_component_row_id` (uuid). FKs: `job_on_component_id → job_on_component`.
- CHECK: none. INDEXES: `ix_job_on_component_row_component`. TRIGGERS: `trg_job_on_component_row_append_only` (N25).
- RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperJobOnRepository`.
- CATEGORY: history/immutable snapshot (append-only since N25).

### `job_on_verification_occurrence`

- MODULE/OWNER: Job On (origin rule from Ferramentas). ORIGIN: N05. LATER ALTERATIONS: N25 (CHECK `ck_job_on_verification_completed`).
- PK: `job_on_verification_occurrence_id` (uuid). FKs: `job_on_component_id → job_on_component`; `source_rule_id → tool_check_rules` (nullable); `completed_by → internal_users`.
- CHECK: `ck_job_on_verification_status` (`pendente/confirmada/reposta/desativada`), `ck_job_on_verification_source` (`completion_source = 'manual_job_on'`), `ck_job_on_verification_completed` (N25: `(status IN ('confirmada','reposta')) = (completed_at_utc IS NOT NULL)`).
- INDEXES: `ix_job_on_verification_component`. TRIGGERS: none.
- RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperJobOnRepository`.
- CATEGORY: current-state.

### `job_on_audit_event`

- MODULE/OWNER: Job On. ORIGIN: N05. LATER ALTERATIONS: none.
- PK: `job_on_audit_event_id` (uuid). FKs: `job_on_id → job_on`; `job_on_revision_id → job_on_revision`; `actor_id → internal_users`.
- INDEXES: `ix_job_on_audit_event_job_on`. TRIGGERS: `trg_job_on_audit_event_append_only` (N05).
- RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperJobOnRepository`, `DapperArticleReferenceImageRepository` (writes before/after events on image mutation).
- CATEGORY: history/event (module-level audit, append-only).

### `job_on_field_option`

- MODULE/OWNER: Job On. ORIGIN: N05. LATER ALTERATIONS: none.
- PK: `job_on_field_option_id` (uuid). FKs: none.
- UNIQUE: `uq_job_on_field_option (family, field_key, option_value)`.
- INDEXES: `ix_job_on_field_option_lookup (family, field_key, active)`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: none found in `src\BA.Dmo.Infrastructure\` (`NEEDS REVIEW` if the Definições surface writes options elsewhere — no Infrastructure consumer exists at HEAD).
- CATEGORY: lookup/configuration (data-driven dropdowns).

### `jobon_user_current`

- MODULE/OWNER: Job On (user-scoped "current Job On" — R011). ORIGIN: N24. LATER ALTERATIONS: none.
- PK: `actor_id` (FK `internal_users(actor_id)`). FKs: `job_on_id → job_on(job_on_id)` (NOT NULL).
- UNIQUE/CHECK: —. INDEXES: none (PK covers the access path).
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N25 §2 late-tables).
- DAPPER CONSUMERS: `DapperJobOnUserContextRepository`.
- CATEGORY: current-state (one row per user; explicit-open context snapshot, not a planning duplicate).

### `article_reference_images`

- MODULE/OWNER: Job On (master Article/Reference image association). ORIGIN: N29. LATER ALTERATIONS: N30 (ADD index `ix_article_reference_images_updated_by (updated_by)` — covering index for the FK).
- PK: `reference_code` (text). FKs: `updated_by → internal_users(actor_id)`.
- CHECK: `ck_article_reference_images_reference` (non-empty, uppercase trimmed) and `ck_article_reference_images_asset` (non-empty trimmed asset id: no `/`, no `\`, no `..`, extension `jpe?g|png|gif|webp|bmp`).
- INDEXES: `ix_article_reference_images_updated_by` (N30).
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` created inside N29 (+anon/authenticated REVOKE + `ba_dmo_app` GRANT).
- DAPPER CONSUMERS: `DapperArticleReferenceImageRepository` (upsert/delete with `job_on_audit_event` writes), `FileSystemJobOnImageProvider` (reads asset/blob config).
- CATEGORY: current-state (master image association; legacy `job_on_revision.image_asset_id` dormant).

## 6. Controlo

### `controlo_sheets`

- MODULE/OWNER: Controlo — Folha de Controlo (07_CONTROLO.md). ORIGIN: N23. LATER ALTERATIONS: none.
- PK: `controlo_sheet_id` (uuid). FKs: `job_on_id → job_on`; `job_on_revision_id → job_on_revision` (immutable anchor); `created_by/submitted_by/decided_by → internal_users`.
- CHECK: `ck_controlo_sheets_status` (`rascunho/submetido/aprovado/rejeitado`); `ck_controlo_sheets_decision` (decision block consistency).
- INDEXES: `ix_controlo_sheets_job_on`, `ix_controlo_sheets_revision`, `ix_controlo_sheets_production (production_code, machine_code)`, `ix_controlo_sheets_status`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N25 §2 late-tables).
- DAPPER CONSUMERS: `DapperControloSheetRepository`.
- CATEGORY: current-state.

### `controlo_sheet_items`

- MODULE/OWNER: Controlo. ORIGIN: N23 (copied from the pinned revision's `job_on_component` rows at creation). LATER ALTERATIONS: none.
- PK: `controlo_sheet_item_id` (uuid). FKs: `controlo_sheet_id → controlo_sheets` ON DELETE CASCADE; `source_tool_id → tool_references`; `source_lot_id → tool_lotes`.
- CHECK: `ck_controlo_sheet_items_result` — `result IS NULL OR result IN ('OK','NOK')`.
- INDEXES: `ix_controlo_sheet_items_sheet`, `ix_controlo_sheet_items_family (controlo_sheet_id, family)`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N25 §2 late-tables).
- DAPPER CONSUMERS: `DapperControloSheetRepository`.
- CATEGORY: current-state (per-component snapshot + result).

### `controlo_sheet_events`

- MODULE/OWNER: Controlo. ORIGIN: N23. LATER ALTERATIONS: none.
- PK: `controlo_sheet_event_id` (uuid). FKs: `controlo_sheet_id → controlo_sheets` ON DELETE CASCADE; `actor_id → internal_users`.
- CHECK: `ck_controlo_sheet_events_type` — `event_type IN ('criar','editar','submeter','reeabrir','decidir')`.
- INDEXES: `ix_controlo_sheet_events_sheet`. TRIGGERS: `trg_controlo_sheet_events_append_only` (N23).
- RLS: policy `ba_dmo_app_access` (N25 §2 late-tables).
- DAPPER CONSUMERS: `DapperControloSheetRepository`.
- CATEGORY: history/event (append-only sheet audit).

## 7. Ferramentas

### `tool_references`

- MODULE/OWNER: Ferramentas (08_FERRAMENTAS.md). ORIGIN: N04. LATER ALTERATIONS: none.
- PK: `tool_reference_id` (uuid). FKs: `created_by → internal_users`.
- UNIQUE: `uq_tool_references_type_code (tool_type, ref_code)`. CHECK: `ck_tool_references_type` — `tool_type IN ('CM','MF','BQ','PU','CS')`.
- INDEXES: none beyond PK. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperFerramentasRepository`, `DapperFerramentasIdentityLookup`, `DapperFerramentasPieceLookup`.
- CATEGORY: current-state (master tool identity; no `processo` — it lives on the lote, TD-17).

### `tool_lotes`

- MODULE/OWNER: Ferramentas. ORIGIN: N04. LATER ALTERATIONS: none.
- PK: `tool_lote_id` (uuid). FKs: `tool_reference_id → tool_references`; `created_by → internal_users`.
- UNIQUE: `uq_tool_lotes_reference_lote (tool_reference_id, lote)`. CHECK: `ck_tool_lotes_qty (qty IS NULL OR qty >= 0)`.
- INDEXES: `ix_tool_lotes_reference`. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperFerramentasRepository`, `DapperFerramentasIdentityLookup`, `DapperFerramentasPieceLookup`.
- CATEGORY: current-state.

### `physical_pieces`

- MODULE/OWNER: Ferramentas. ORIGIN: N04. LATER ALTERATIONS: none.
- PK: `physical_piece_id` (uuid). FKs: `tool_lote_id → tool_lotes`; `created_by → internal_users`.
- UNIQUE: `uq_physical_pieces_lote_number (tool_lote_id, number)`. CHECK: `ck_physical_pieces_sequence (sequence >= 1)`.
- INDEXES: `ix_physical_pieces_lote`. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperFerramentasRepository`, `DapperFerramentasPieceLookup`.
- CATEGORY: current-state (CM/MF numbered pieces; BQ moves by quantity).

### `tool_check_rules`

- MODULE/OWNER: Ferramentas. ORIGIN: N04. LATER ALTERATIONS: none.
- PK: `tool_check_rule_id` (uuid). FKs: `tool_lote_id → tool_lotes`; self-FK `copied_from_rule_id → tool_check_rules` (copies keep origin).
- CHECK: `ck_tool_check_rules_frequency` — `frequency IN ('uma_vez_no_lote','por_fabrico')`.
- INDEXES: `ix_tool_check_rules_lote`. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperFerramentasRepository`, `DapperFerramentasRuleLookup`.
- CATEGORY: current-state/configuration.

### `tool_check_occurrences`

- MODULE/OWNER: Ferramentas (rules materialized in Job On). ORIGIN: N04. LATER ALTERATIONS: none.
- PK: `tool_check_occurrence_id` (uuid). FKs: `tool_check_rule_id → tool_check_rules`; `completed_by/created_by → internal_users`; `job_on_id`/`job_on_component_id` are logical uuid links (NO physical FK — module coupling stays at contract level).
- CHECK: `ck_tool_check_occurrences_status` (`pendente/confirmada/reposta/desativada`), `ck_tool_check_occurrences_source` (`completion_source = 'manual_job_on'`), `ck_tool_check_occurrences_completed` (completed-state consistency).
- INDEXES: `ix_tool_check_occurrences_rule`, `ix_tool_check_occurrences_job_on`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperFerramentasRepository`.
- CATEGORY: current-state (reset history preserved by new rows, never rewritten).

### `tool_usage_records`

- MODULE/OWNER: Ferramentas (utilisation history, R003). ORIGIN: N19. LATER ALTERATIONS: none.
- PK: `tool_usage_record_id` (uuid). FKs: `tool_lote_id → tool_lotes`; `actor_id → internal_users`.
- CHECK: `ck_tool_usage_records_sap_start`, `ck_tool_usage_records_sap_end`, `ck_tool_usage_records_percent` (0–100 or NULL), `ck_tool_usage_records_cumulative (value_cumulative >= 0)`.
- INDEXES: `ix_tool_usage_records_lote`. TRIGGERS: `trg_tool_usage_records_append_only` (N19).
- RLS: policy `ba_dmo_app_access` (N25 §2 late-tables).
- DAPPER CONSUMERS: `DapperFerramentasRepository`.
- CATEGORY: history/event (append-only utilisation readings).

## 8. Armazém

### `warehouse_locations`

- MODULE/OWNER: Armazém (09_ARMAZEM.md). ORIGIN: N09. LATER ALTERATIONS: none.
- PK: `warehouse_location_id` (uuid). FK: `created_by → internal_users`.
- UNIQUE: `code` (inline UNIQUE). CHECK: none.
- INDEXES: none. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperArmazemRepository`, `DapperArmazemRepairMovementRepository`.
- CATEGORY: current-state (positions).

### `warehouse_stock`

- MODULE/OWNER: Armazém. ORIGIN: N09. LATER ALTERATIONS: none.
- PK: `warehouse_stock_id` (uuid). FKs: `warehouse_location_id → warehouse_locations`; `tool_lote_id → tool_lotes`; `occupied_by/released_by → internal_users`.
- UNIQUE (partial): `uq_warehouse_stock_active_occupation (warehouse_location_id, tool_lote_id) WHERE released_at_utc IS NULL` — one active occupation per position/lot; releases keep the fact row.
- INDEXES: `ix_warehouse_stock_location`, `ix_warehouse_stock_tool_lote`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperArmazemRepository`, `DapperArmazemRepairMovementRepository`.
- CATEGORY: current-state (occupation facts with release history).

### `warehouse_movements`

- MODULE/OWNER: Armazém. ORIGIN: N09. LATER ALTERATIONS: none.
- PK: `warehouse_movement_id` (uuid). FKs: `warehouse_stock_id → warehouse_stock` (nullable); `repair_exit_id → repair_exits` (planned exit link); `actor_id → internal_users`.
- CHECK: `ck_warehouse_movements_direction (direction IN ('in','out'))`.
- INDEXES: `ix_warehouse_movements_stock`, `ix_warehouse_movements_occurred`.
- TRIGGERS: `trg_warehouse_movements_append_only` (N09).
- RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperArmazemRepository`, `DapperArmazemRepairMovementRepository`.
- CATEGORY: history/event (append-only).

## 9. Boquilhas

### `bq_lotes`

- MODULE/OWNER: Boquilhas (10_BOQUILHAS.md). ORIGIN: N03. LATER ALTERATIONS: none.
- PK: `bq_lote_id` (uuid). FKs: `created_by → internal_users`.
- UNIQUE: `uq_bq_lotes_reference_batch (reference, batch_code)`. CHECK: `ck_bq_lotes_reference` (`reference ~ '^[A-Z][0-9]{3}$'`), `ck_bq_lotes_lifecycle` (`available/archived/scrapped`).
- INDEXES: `ix_bq_lotes_lifecycle`. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperBoquilhasRepository`.
- CATEGORY: current-state (master lot identity).

### `bq_traces`

- MODULE/OWNER: Boquilhas. ORIGIN: N03. LATER ALTERATIONS: N25 (partial UNIQUE `uq_bq_traces_active (bq_lote_id) WHERE status='active'` — one active trace per lote at DB level).
- PK: `bq_trace_id` (uuid). FKs: `bq_lote_id → bq_lotes`; `created_by → internal_users`.
- CHECK: `ck_bq_traces_status` (`active/closed`), `ck_bq_traces_purpose` (`production/repair`), `ck_bq_traces_sap_start`/`ck_bq_traces_sap_end` (0–100 or NULL).
- INDEXES: `ix_bq_traces_lote`, `ix_bq_traces_status`, `uq_bq_traces_active` (N25).
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperBoquilhasRepository`.
- CATEGORY: current-state.

### `bq_movements`

- MODULE/OWNER: Boquilhas. ORIGIN: N03. LATER ALTERATIONS: N18 (ADD `noted_repairer_id uuid REFERENCES repairers` — per-movement repairer, nullable; NULL = "Sem associação"; history never rewritten by later config changes).
- PK: `bq_movement_id` (uuid). FKs: `bq_trace_id → bq_traces`; `actor_id → internal_users`; `noted_repairer_id → repairers` (N18).
- CHECK: `ck_bq_movements_type` (`inicio/saida/entrada/irreparavel/linha/contagem/fim`), `ck_bq_movements_qty` (qty NULL only for 'linha'), `ck_bq_movements_exceptional` (>= 0 or NULL).
- INDEXES: `ix_bq_movements_trace`, `ix_bq_movements_occurred`.
- TRIGGERS: `trg_bq_movements_append_only` (N03). RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperBoquilhasRepository`.
- CATEGORY: history/event (append-only; NO `allow_unmatched` column — UD-09).

### `bq_discrepancies`

- MODULE/OWNER: Boquilhas. ORIGIN: N03. LATER ALTERATIONS: none.
- PK: `bq_discrepancy_id` (uuid). FKs: `bq_lote_id → bq_lotes`; `bq_trace_id → bq_traces` (nullable); `resolved_by/created_by → internal_users`.
- CHECK: `ck_bq_discrepancies_status` (`open/under_review/resolved`).
- INDEXES: `ix_bq_discrepancies_lote`, `ix_bq_discrepancies_status`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperBoquilhasRepository`.
- CATEGORY: current-state (return-excess records; warning + record, never a block).

### `bq_lifecycle_history`

- MODULE/OWNER: Boquilhas. ORIGIN: N03. LATER ALTERATIONS: none.
- PK: `bq_lifecycle_history_id` (uuid). FKs: `bq_lote_id → bq_lotes`; `actor_id → internal_users`.
- CHECK: `ck_bq_lifecycle_history_event` (`archived/scrapped/restored/retired`).
- INDEXES: `ix_bq_lifecycle_history_lote`. TRIGGERS: `trg_bq_lifecycle_history_append_only` (N03).
- RLS: policy `ba_dmo_app_access` (N12). DAPPER CONSUMERS: `DapperBoquilhasRepository`.
- CATEGORY: history/event (append-only).

### `bq_utilisation_readings`

- MODULE/OWNER: Boquilhas. ORIGIN: N03. LATER ALTERATIONS: none.
- PK: `bq_utilisation_reading_id` (uuid). FKs: `bq_trace_id → bq_traces`; `actor_id → internal_users`.
- CHECK: `ck_bq_utilisation_readings_kind` (`initial/final`), `ck_bq_utilisation_readings_value` (0–100).
- INDEXES: `ix_bq_utilisation_readings_trace`. TRIGGERS: `trg_bq_utilisation_readings_append_only` (N03).
- RLS: policy `ba_dmo_app_access` (N12). DAPPER CONSUMERS: `DapperBoquilhasRepository`.
- CATEGORY: history/event (append-only).

## 10. Reparação Interna

### `internal_repair_records`

- MODULE/OWNER: Reparação Interna (11_REPARACAO_INTERNA.md). ORIGIN: N08. LATER ALTERATIONS: N22 (tool_type CHECK widened to `CM/MF/BQ`; ADD `job_on_revision_id` + FK `fk_internal_repair_records_revision → job_on_revision`; ADD `production_code`, `reference`, `lot_id` logical columns — historical production-context snapshot, no hard block); N28 (**CM/MF-only convergence**: CHECK re-narrowed to `(tool_type IN ('CM','MF'))`, created `NOT VALID` then `VALIDATE`; fails closed with an exception if any non-CM/MF row exists; explicit BEGIN/COMMIT).
- PK: `internal_repair_record_id` (uuid). FKs: `operator_id/created_by → internal_users`; self-FK `correction_of_id → internal_repair_records`; `job_on_revision_id → job_on_revision` (N22); `job_on_id`/`lot_id` are logical uuid links (no FK).
- CHECK: `ck_internal_repair_records_type` (`CM/MF` — N28 state), `ck_internal_repair_records_correction` (`(correction_of_id IS NULL) = (before_snapshot IS NULL)`).
- INDEXES: `ix_internal_repair_records_line`, `ix_internal_repair_records_job_on`, `ix_internal_repair_records_revision` (N22).
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperReparacaoInternaRepository`.
- CATEGORY: current-state (corrections are NEW rows; never rewrites).

## 11. Peso

### `peso_references`

- MODULE/OWNER: Peso (Controlo area — 07_CONTROLO.md). ORIGIN: N06. LATER ALTERATIONS: none.
- PK: `peso_reference_id` (uuid). FKs: `created_by → internal_users`.
- UNIQUE: `uq_peso_references_mold_neckring (mold_number, neckring_number)`.
- INDEXES: none. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperPesoRepository`.
- CATEGORY: current-state (master identity; `change_log` jsonb keeps the justification trail).

### `peso_lotes`

- MODULE/OWNER: Peso. ORIGIN: N06. LATER ALTERATIONS: none.
- PK: `peso_lote_id` (uuid). FKs: `peso_reference_id → peso_references`; `created_by → internal_users`.
- UNIQUE: `uq_peso_lotes_reference_lote (peso_reference_id, lote)`. CHECK: `ck_peso_lotes_processo` (`NNPB/PS`), `ck_peso_lotes_allowed_lines` (cardinality >= 1).
- INDEXES: `ix_peso_lotes_reference`. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperPesoRepository`.
- CATEGORY: current-state.

### `peso_controlos`

- MODULE/OWNER: Peso. ORIGIN: N06. LATER ALTERATIONS: N25 (CHECK `ck_peso_controlos_approved_consistent`; trigger `trg_peso_controlos_approved_guard` via function `ba_dmo_guard_peso_approved` — approved rows: identity columns immutable and DELETE blocked; non-identity columns remain updatable).
- PK: `peso_controlo_id` (uuid). FKs: `peso_reference_id → peso_references`; `peso_lote_id → peso_lotes`; `job_on_id → job_on`; `job_on_revision_id → job_on_revision` (immutable attribution anchor, DS-04); `approved_by/created_by → internal_users`.
- UNIQUE: `uq_peso_controlos_identity (mold_number, neckring_number, production_code, line, lote, control_date)`.
- CHECK: `ck_peso_controlos_record_type` (`novo_controlo/comparacao`), `ck_peso_controlos_status` (`rascunho/pendente/aprovado/nao_aprovado`), `ck_peso_controlos_approved_consistent` (N25).
- IMPORTANT INDEXES: `ix_peso_controlos_reference`, `ix_peso_controlos_job_on`, `ix_peso_controlos_job_on_revision`, `ix_peso_controlos_status_date`.
- TRIGGERS: `trg_peso_controlos_approved_guard` (N25).
- RLS: policy `ba_dmo_app_access` (N12). DAPPER CONSUMERS: `DapperPesoRepository`.
- CATEGORY: current-state (approved controls are immutable facts).

### `peso_leituras`

- MODULE/OWNER: Peso. ORIGIN: N06. LATER ALTERATIONS: none.
- PK: `peso_leitura_id` (uuid). FKs: `peso_controlo_id → peso_controlos` ON DELETE CASCADE; `created_by → internal_users`.
- UNIQUE: `uq_peso_leituras_controlo_cm (peso_controlo_id, cm_number)`.
- INDEXES: none. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperPesoRepository`.
- CATEGORY: current-state (per-CM readings jsonb).

### `peso_comparacao_anterior`

- MODULE/OWNER: Peso. ORIGIN: N06. LATER ALTERATIONS: none.
- PK: `peso_controlo_id` (FK `peso_controlos` ON DELETE CASCADE). FKs: `previous_peso_controlo_id → peso_controlos` (nullable).
- INDEXES: none. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperPesoRepository`.
- CATEGORY: current-state (persisted read path of previous approved control; null deltas = no invented comparison).

### `peso_day_approvals`

- MODULE/OWNER: Peso. ORIGIN: N06. LATER ALTERATIONS: none.
- PK: `peso_day_approval_id` (uuid). FKs: `approved_by → internal_users`.
- UNIQUE: `uq_peso_day_approvals_identity (mold_number, neckring_number, line, approval_date)`.
- INDEXES: none. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperPesoRepository`.
- CATEGORY: current-state.

### `peso_settings`

- MODULE/OWNER: Peso. ORIGIN: N06. LATER ALTERATIONS: none.
- PK: `setting_key` (text). FKs: `updated_by → internal_users`.
- INDEXES: none. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperPesoRepository`.
- CATEGORY: lookup/configuration.

## 12. Pegamentos

### `pegamento_controlos`

- MODULE/OWNER: Pegamentos (Controlo area — 07_CONTROLO.md). ORIGIN: N07. LATER ALTERATIONS: N16 (ADD `cm_nominal`, `bq_nominal`, `mf_nominal` — per-component historical nominals, nullable legacy); N17 (ADD `notas` text); N25 (CHECK `ck_pegamento_controlos_status`).
- PK: `pegamento_controlo_id` (uuid). FKs: `job_on_id → job_on`; `job_on_revision_id → job_on_revision` (immutable attribution anchor, DS-05); `created_by → internal_users`.
- CHECK: `ck_pegamento_controlos_tolerance (tolerance >= 0)`; `ck_pegamento_controlos_status (status IN ('aberto','fechado'))` (N25).
- IMPORTANT INDEXES: `ix_pegamento_controlos_job_on`, `ix_pegamento_controlos_job_on_revision`, `ix_pegamento_controlos_production (production_code, machine_code)`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperPegamentoRepository`.
- CATEGORY: current-state.

### `pegamento_medicoes`

- MODULE/OWNER: Pegamentos. ORIGIN: N07. LATER ALTERATIONS: N15 (ADD `tool_number` integer — nullable, legacy rows not fabricated; Domain/Application/API enforce non-null for new measurements; ADD index).
- PK: `pegamento_medicao_id` (uuid). FKs: `pegamento_controlo_id → pegamento_controlos`; `actor_id → internal_users`.
- CHECK: none. IMPORTANT INDEXES: `ix_pegamento_medicoes_controlo`, `ix_pegamento_medicoes_component_tool (pegamento_controlo_id, component_key, tool_number)` (N15).
- TRIGGERS: `trg_pegamento_medicoes_append_only` (N07). RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperPegamentoRepository`.
- CATEGORY: history/event (append-only measurement facts; costura/contra_costura raw values, ovalização/média computed in code).

### `pegamento_documentos`

- MODULE/OWNER: Pegamentos. ORIGIN: N14. LATER ALTERATIONS: none.
- PK: `pegamento_documento_id` (uuid). FKs: `pegamento_controlo_id → pegamento_controlos` (UNIQUE, 1:1 final PDF metadata); `generated_by → internal_users`.
- INDEXES: `ix_pegamento_documentos_controlo`. TRIGGERS: none.
- RLS: policy `ba_dmo_app_access` (N25 §2 late-tables). DAPPER CONSUMERS: `DapperPegamentoRepository`.
- CATEGORY: current-state (document metadata, no `document_version`).

## 13. Reparação Externa

### `repairers`

- MODULE/OWNER: Reparação Externa (canonical repairer registry, TD-15; also consumed by Boquilhas/Reparação Interna). ORIGIN: N08. LATER ALTERATIONS: none.
- PK: `repairer_id` (uuid). CHECK: none. INDEXES: none. TRIGGERS: none.
- RLS: policy `ba_dmo_app_access` (N12). DAPPER CONSUMERS: `DapperRepairRepository`, `DapperBoquilhasRepository` (reads for BQ movement repairer notes).
- CATEGORY: lookup/registry (deactivated repairers never deleted).

### `line_repairer_defaults`

- MODULE/OWNER: Reparação Externa. ORIGIN: N08. LATER ALTERATIONS: none.
- PK: `(line, tool_type)`. FKs: `repairer_id → repairers`; `updated_by → internal_users`.
- CHECK: `ck_line_repairer_defaults_type (tool_type IN ('BQ','CM','MF'))`.
- INDEXES: none. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperRepairRepository`, `DapperBoquilhasRepository`.
- CATEGORY: lookup/configuration (pure convenience default; does NOT define repairer capability — that is `repairer_repair_types`).

### `repair_exits`

- MODULE/OWNER: Reparação Externa. ORIGIN: N08. LATER ALTERATIONS: none.
- PK: `repair_exit_id` (uuid). FKs: `repairer_id → repairers` (nullable, snapshot used); `created_by → internal_users`.
- CHECK: `ck_repair_exits_type` (`BQ/CM/MF`), `ck_repair_exits_status` (`preparacao/a_retirar/enviado/retorno_parcial/concluido/cancelado`).
- IMPORTANT INDEXES: `ix_repair_exits_status`, `ix_repair_exits_planned_date`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12). DAPPER CONSUMERS: `DapperRepairRepository`.
- CATEGORY: current-state (planned exit lists).

### `repair_exit_items`

- MODULE/OWNER: Reparação Externa. ORIGIN: N08. LATER ALTERATIONS: N25 (CHECK `ck_repair_exit_items_status`).
- PK: `repair_exit_item_id` (uuid). FKs: `repair_exit_id → repair_exits`; `bq_lote_id → bq_lotes` (BQ kind); `physical_piece_id → physical_pieces` (CM/MF kind); `out_operator_id/in_operator_id → internal_users`.
- CHECK: `ck_repair_exit_items_qty (qty IS NULL OR qty >= 0)`; `ck_repair_exit_items_kind` (exactly one of BQ-by-qty or piece+individual-number); `ck_repair_exit_items_status (status IN ('pendente','em_reparacao','devolvido'))` (N25).
- INDEXES: `ix_repair_exit_items_exit`. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperRepairRepository`.
- CATEGORY: current-state (per-item picked/out/in facts).

### `repair_events`

- MODULE/OWNER: Reparação Externa/Interna. ORIGIN: N08. LATER ALTERATIONS: none (FK `fk_repair_events_internal_record` added in the same script to `internal_repair_records`).
- PK: `repair_event_id` (uuid). FKs: `repair_exit_item_id → repair_exit_items` (nullable); `internal_repair_record_id → internal_repair_records` (nullable); `actor_id → internal_users`.
- CHECK: `ck_repair_events_scope (repair_scope IN ('interna','externa'))`.
- INDEXES: `ix_repair_events_exit_item`, `ix_repair_events_internal`.
- TRIGGERS: `trg_repair_events_append_only` (N08). RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperRepairRepository`, `DapperReparacaoInternaRepository`.
- CATEGORY: history/event (append-only; repair_count derived, never stored).

### `repairer_repair_types`

- MODULE/OWNER: Reparação Externa. ORIGIN: N20. LATER ALTERATIONS: none.
- PK: `(repairer_id, repair_type)`. FKs: `repairer_id → repairers`.
- CHECK: `ck_repairer_repair_types_type (repair_type IN ('CM','MF','BQ'))`.
- INDEXES: none. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N25 §2 late-tables).
- DAPPER CONSUMERS: `DapperRepairRepository`, `DapperBoquilhasRepository`.
- CATEGORY: relationship (repairer capability M:N; never duplicate a repairer per type).

## 14. Tampões

### `tampao_field_defs`

- MODULE/OWNER: Tampões (13_TAMPOES.md). ORIGIN: N10. LATER ALTERATIONS: none.
- PK: `tampao_field_def_id` (uuid). UNIQUE: `field_name`.
- INDEXES: none. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperTampaoRepository`.
- CATEGORY: lookup/configuration (comparable fields).

### `tampao_field_values`

- MODULE/OWNER: Tampões. ORIGIN: N10. LATER ALTERATIONS: none.
- PK: `tampao_field_value_id` (uuid). FKs: `tampao_field_def_id → tampao_field_defs`.
- UNIQUE: `uq_tampao_field_values (tampao_field_def_id, value_numeric)`.
- INDEXES: `ix_tampao_field_values_field (tampao_field_def_id, active, value_numeric)`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12). DAPPER CONSUMERS: `DapperTampaoRepository`.
- CATEGORY: lookup/configuration.

### `tampao_configurations`

- MODULE/OWNER: Tampões. ORIGIN: N10. LATER ALTERATIONS: none.
- PK: `tampao_configuration_id` (uuid). FKs: `created_by → internal_users`.
- UNIQUE: `uq_tampao_configurations_values (values_json)` (destinations reused by id, GLM-TP-05.3).
- INDEXES: none. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperTampaoRepository`.
- CATEGORY: current-state.

### `tampao_saldos`

- MODULE/OWNER: Tampões. ORIGIN: N10. LATER ALTERATIONS: none.
- PK: `tampao_saldo_id` (uuid). FKs: `tampao_configuration_id → tampao_configurations` (UNIQUE — one balance row per configuration).
- CHECK: `ck_tampao_saldos_enchidos (enchidos >= 0)`, `ck_tampao_saldos_por_encher (por_encher >= 0)`.
- INDEXES: none. TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperTampaoRepository`.
- CATEGORY: current-state (exactly two balances: Enchidos / Por encher).

### `tampao_movements`

- MODULE/OWNER: Tampões. ORIGIN: N10. LATER ALTERATIONS: none.
- PK: `tampao_movement_id` (uuid). FKs: `origin_configuration_id → tampao_configurations` (nullable); `destination_configuration_id → tampao_configurations` (nullable); `actor_id → internal_users`.
- CHECK: `ck_tampao_movements_type` (`adicionar/remover/alterar_estado/alterar_configuracao`), `ck_tampao_movements_qty (qty >= 1)`.
- IMPORTANT INDEXES: `ix_tampao_movements_origin`, `ix_tampao_movements_occurred`.
- TRIGGERS: `trg_tampao_movements_append_only` (N10). RLS: policy `ba_dmo_app_access` (N12).
- DAPPER CONSUMERS: `DapperTampaoRepository`.
- CATEGORY: history/event (append-only; before/after balances in jsonb).

### `tampao_planos`

- MODULE/OWNER: Tampões. ORIGIN: N10. LATER ALTERATIONS: none.
- PK: `tampao_plano_id` (uuid). FKs: `tampao_configuration_id → tampao_configurations`; `created_by → internal_users`.
- CHECK: `ck_tampao_planos_qty (planned_qty >= 1)`. job_on_id/production_code are logical links (no FK).
- IMPORTANT INDEXES: `ix_tampao_planos_configuration`, `ix_tampao_planos_date`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N12). DAPPER CONSUMERS: `DapperTampaoRepository`.
- CATEGORY: current-state (planear ≠ reservar; cancelling preserves the canceled fact row).

### `tampao_configuration_machines`

- MODULE/OWNER: Tampões. ORIGIN: N21. LATER ALTERATIONS: none.
- PK: `(tampao_configuration_id, machine)`. FKs: `tampao_configuration_id → tampao_configurations`.
- CHECK: `ck_tampao_configuration_machines_machine (machine IN ('B1','B2','B3','C1','C2','C3'))`.
- INDEXES: `ix_tampao_configuration_machines_machine`.
- TRIGGERS: none. RLS: policy `ba_dmo_app_access` (N25 §2 late-tables).
- DAPPER CONSUMERS: `DapperTampaoRepository`.
- CATEGORY: relationship (normalized M:N; no CSV, no per-machine copy).

### `tampao_configuration_notes`

- MODULE/OWNER: Tampões. ORIGIN: N21. LATER ALTERATIONS: none.
- PK: `tampao_configuration_note_id` (uuid). FKs: `tampao_configuration_id → tampao_configurations`; `actor_id → internal_users`.
- INDEXES: `ix_tampao_configuration_notes_config (tampao_configuration_id, occurred_at_utc)`.
- TRIGGERS: `trg_tampao_configuration_notes_append_only` (N21). RLS: policy `ba_dmo_app_access` (N25).
- DAPPER CONSUMERS: `DapperTampaoRepository`.
- CATEGORY: history/event (append-only comments).

### `tampao_configuration_machine_event`

- MODULE/OWNER: Tampões. ORIGIN: N21. LATER ALTERATIONS: none.
- PK: `tampao_configuration_machine_event_id` (uuid). FKs: `tampao_configuration_id → tampao_configurations`; `actor_id → internal_users`.
- CHECK: `ck_tampao_configuration_machine_event_action (action IN ('added','removed'))`, `ck_tampao_configuration_machine_event_machine (B1–C3)`.
- INDEXES: `ix_tampao_configuration_machine_event_config (tampao_configuration_id, occurred_at_utc)`.
- TRIGGERS: `trg_tampao_configuration_machine_event_append_only` (N21). RLS: policy `ba_dmo_app_access` (N25).
- DAPPER CONSUMERS: `DapperTampaoRepository`.
- CATEGORY: history/event (append-only machine-association audit).

## 15. RLS / Least-Privilege Overview

- N12 (rls): RLS ENABLED on **49** tables (48 application tables existing at N12 + `schema_migrations`); single technical policy `ba_dmo_app_access` (`FOR ALL TO ba_dmo_app USING (true) WITH CHECK (true)`) on the 48 application tables; `anon`/`authenticated` have NO table access (guarded REVOKE); `ba_dmo_app` receives SELECT/INSERT/UPDATE/DELETE. `schema_migrations`: RLS on, no policy (migrate CLI only).
- N25 §2 (SEC-02): the **10 late tables** created after N12 (`pegamento_documentos`, `tool_usage_records`, `repairer_repair_types`, `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event`, `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events`, `jobon_user_current`) get RLS + policy `ba_dmo_app_access` + anon/authenticated REVOKE + `ba_dmo_app` GRANT.
- N27: `internal_user_access_templates` gets its own-named policy `internal_user_access_templates_app_access` (same `FOR ALL TO ba_dmo_app` semantics) + REVOKE/GRANT.
- N29: `article_reference_images` gets RLS + policy `ba_dmo_app_access` + REVOKE/GRANT (in-script).
- N31: `access_template_profiles` gets RLS + policy `access_template_profiles_app_access` + REVOKE/GRANT (in-script).
- MODEL NOTE (GLM-DATA-06.3): V1 has NO per-user/per-module RLS policies; capabilities are enforced exclusively in the C# Application layer. RLS is the technical access envelope for `ba_dmo_app` only.

## 16. Table Category Summary

- **Current-state tables (39):** access_templates, internal_users, module_catalog_mirror, bq_lotes, bq_traces, bq_discrepancies, tool_references, tool_lotes, physical_pieces, tool_check_rules, tool_check_occurrences, job_on, job_on_verification_occurrence, job_on_field_option, jobon_user_current, peso_references, peso_lotes, peso_controlos, peso_leituras, peso_comparacao_anterior, peso_day_approvals, peso_settings, pegamento_controlos, pegamento_documentos, repairers, line_repairer_defaults, repair_exits, repair_exit_items, internal_repair_records, warehouse_locations, warehouse_stock, tampao_field_defs, tampao_field_values, tampao_configurations, tampao_saldos, tampao_planos, internal_user_access_templates, access_template_profiles, article_reference_images.
- **History/event tables (append-only, 17):** audit_events, bq_movements, bq_lifecycle_history, bq_utilisation_readings, tool_usage_records, job_on_audit_event, pegamento_medicoes, repair_events, warehouse_movements, tampao_movements, tampao_configuration_notes, tampao_configuration_machine_event, controlo_sheet_events + revision-family snapshots made append-only in N25 (job_on_revision, job_on_component, job_on_component_field, job_on_component_row).
- **Relationship tables (3):** repairer_repair_types, tampao_configuration_machines, internal_user_access_templates.
- **Audit tables (2):** audit_events (global canonical), job_on_audit_event (Job On module audit). (Module-level events also exist append-only in controlo_sheet_events / tampao_configuration_machine_event.)
- **Lookup/configuration tables (10):** access_templates, module_catalog_mirror, app_settings, job_on_field_option, peso_settings, repairers (registry), line_repairer_defaults, tampao_field_defs, tampao_field_values, access_template_profiles.
- Append-only enforcement is by trigger on `ba_dmo_guard_append_only()` (N01 function) — see [03_MIGRATIONS.md](03_MIGRATIONS.md) for the full trigger inventory.

## Sources Verified

- `database\migrations\N01_identity.sql` … `N31_template_profiles_single_assignment.sql` — all 31 files read in full (column-level).
- `database\consolidated_clean_install.sql` — existence + N31 drift noted (SCHEMA DRIFT — NEEDS AUDIT, cross-ref [03_MIGRATIONS.md](03_MIGRATIONS.md)).
- `src\BA.Dmo.Infrastructure\` — Dapper consumer mapping per table (file-level grep of every table name against all Infrastructure .cs files).
- Cross-layer evidence referenced from [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md) (query-level detail), [19_APPLICATION.md](19_APPLICATION.md) (§10.4 TemplateProfileStore raw-SQL bypass finding), [03_MIGRATIONS.md](03_MIGRATIONS.md) (per-migration DDL and provenance notes).
- No source, test, or migration file was modified; only `02_DATABASE.md` was updated.

*End of 02_DATABASE.md — rebuilt from the current migration chain (N01–N31).*