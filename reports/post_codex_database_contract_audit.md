# POST-CODEX FULL DATABASE CONTRACT AUDIT

> **Type:** READ-ONLY AUDIT — no source, migration, test, schema object, or
> database was modified; no SQL executed; no DDL/DML performed; no commit or
> push. The only artifact produced by this task is this report.
>
> **Head verified:** `8d916cb` ("Quiesce legacy access mirrors" — N33), branch
> `main`, working tree clean (only pre-existing untracked
> `reports/schema_rationalization_N34_legacy_mirror_removal_audit.md`, which was
> **not** modified).
>
> **Relationship to N34:** the N34 legacy-mirror-removal audit
> (`reports/schema_rationalization_N34_legacy_mirror_removal_audit.md`) is
> **unchanged and remains a separate design input**. Its conclusions are valid
> ONLY for `internal_user_access_templates` and `internal_users.profile_title`
> and are NOT generalized here. This report covers the complete persistence
> contract of the application.
>
> **Evidence policy:** four authority layers were cross-referenced — (1) the
> forward-only migration chain N01–N33, (2) the migration-derived expected
> schema, (3) the current Dapper/raw SQL persistence code in `src/`, and (4)
> the C# persistence/result models. Live-database state could NOT be verified
> from this session (no `BA_DMO_DB_CONNECTION_STRING`, no `DATABASE_URL`, no
> `BA_DMO_TEST_DATABASE`, no local PostgreSQL listener). Every claim that
> depends on executing against real PostgreSQL is explicitly marked
> `LIVE VERIFICATION REQUIRED`; migration-derived state is never silently
> substituted for live state. Where the owner-supplied live fact is known from
> prior artifacts (project `bddfhbyrmchktqotpzgb` read-only audits), it is
> cited as such and dated.
>
> **Confidence labels used:** `CONFIRMED` (direct code+migration evidence),
> `HIGH CONFIDENCE` (strong multi-evidence static reasoning), `NEEDS
> VERIFICATION` (runtime/DB-server-dependent), `LIVE VERIFICATION REQUIRED`
> (cannot be established without real-PG execution or live-DB access). No
> suspicion is reported as fact; every static claim carries `file:line` /
> `migration:line` evidence.

---

## 1. Executive Summary

A large Codex-driven structural refactor landed between the previous
persistence audit baseline (`8478308`, `reports/persistence_cross_reference_
audit.md`) and the current HEAD `8d916cb`. The refactor commits
(`81ce5a2` → `8d916cb`) resolved **five previously-confirmed high-impact
defect classes**, verified first-hand in this audit:

| # | Prior finding | Status at HEAD `8d916cb` | Evidence |
|---|---|---|---|
| 1 | Job On lifecycle: `fechado`/`cancelado` unreachable (`ck_job_on_lifecycle_consistent` 23514) | **FIXED** — `TransitionLifecycleAsync` writes status + `closed_at_utc`/`canceled_at_utc`/`canceled_by`/`cancel_reason` in one UPDATE inside one UoW with the audit fact; `JobOnService.TransitionAsync` invokes domain `Close()`/`Cancel()` | `DapperJobOnRepository.cs:183-212`; `JobOnService.cs:234-271`; verified |
| 2 | Non-JSON strings bound into jsonb audit columns (JobOn mechanic path) | **FIXED for the audited repo paths** — `AuditJson.Normalize` + `::jsonb` casts on JobOn/Armazém/Repair/ArticleImage/Admin audit inserts | `AuditJson.cs`; `DapperJobOnRepository.cs:529,536-537`; `DapperArmazemRepository.cs:441,450-451`; `DapperRepairRepository.cs:426,432-433`; verified |
| 3 | Repair multi-step non-atomicity (create exit, repairer types) | **FIXED for create-exit + repairer-types** — `CreateExitAsync` runs exit+items+audit in one UoW; `SetRepairerRepairTypesAsync` is one UoW | `ReparacaoExternaService.cs:90-101`; `DapperRepairRepository.cs:369-385`; verified |
| 4 | Armazém return re-occupation TOCTOU race | **FIXED** — `ConfirmReturnAsync` locks the location row `FOR UPDATE` before the occupant check | `DapperArmazemRepairMovementRepository.cs:79-98`; verified |
| 5 | Access authority fragmentation / mirror writers | **RESOLVED (D-1/D-2)** — identity resolves through `internal_users.template_id → access_templates → access_template_profiles.functional_profile`; zero `src/` references to either legacy mirror; junction write paths removed; `TemplateProfileStore.cs` deleted | grep `internal_user_access_templates`/`profile_title` over `src/` → **0 hits**; `DapperInternalUserRepository.cs:26-43`; `DapperAdminRepository.cs:46-52,236-282`; verified |

The audit nevertheless confirms material **residual and new** contract
problems that the refactor did not cover and that were not present in the
prior reports at the same severity:

**Confirmed broken contracts (highest priority):**

1. `pegamento_controlos` creation is **CRITICAL** — `DapperPegamentoRepository.
   CreateAsync` binds `UpdatedAtUtc = (object?)control.UpdatedAtUtc ??
   DBNull.Value` while the domain factory never sets `UpdatedAtUtc`
   (`PegamentoControlo.cs:100-119`), targeting `updated_at_utc` which is
   `NOT NULL` (N07:44). Every `POST /api/pegamentos` control creation would
   raise SQLSTATE **23502**. (PG-01 — CONFIRMED statically; LIVE
   VERIFICATION REQUIRED for the deployed DDL.)
2. **Audit jsonb binding remains inconsistent across 5 of 9 global emitters**
   (Boquilhas, Tampões, Peso, Ferramentas, Reparação Interna): raw/non-JSON
   strings are bound into `before_summary`/`after_summary` (jsonb, N01:114-115)
   with **no `::jsonb` cast and no `AuditJson.Normalize`**, diverging from the
   convention the refactor established and which `AuditJsonBindingTests`
   enforces for JobOn/Repair/Armazém. ≥17 call sites pass free text
   (`TampaoService.cs:172,212,491,510`; `PesoService.cs:273,480,498,667,828`;
   `FerramentasService.cs:63,145,168,191,212,245,378`;
   `BoquilhasService.cs:408-409,446-447,490-491,538-539`;
   `DapperReparacaoInternaRepository.cs:196-212`). On strict PostgreSQL this
   raises 22P02/42804; co-transactional flows roll back (Tampões/BQ flows map
   it to a generic `…_SAVE_FAILED`), post-commit flows (Peso/Ferramentas)
   commit the business write and then 500. **NEEDS VERIFICATION / LIVE
   VERIFICATION REQUIRED** (pg behavior; deterministic for non-JSON text).
3. **Pegamentos writes NO `audit_events` rows at all** (PG-03, HS-02,
   CONFIRMED) — the module is a História origin module
   (`HistoriaModuleCatalog.cs:29`) yet has zero global-audit emitters; Peso
   module origin (`HistoriaModuleCatalog.cs:26`) is likewise empty of Job On
   activity because **Job On never emits `audit_events`** (D-5 dual-emit NOT
   implemented; JA-06/HS-01, CONFIRMED). História is therefore blind to Job On
   and Pegamentos.
4. `job_on.production_folder` (N13) has **no application writer anywhere**
   (JA-05, CONFIRMED) — `PegamentoService.ConfirmDocumentSavedAsync` hard-fails
   with `PEGAMENTO_PRODUCTION_FOLDER_MISSING` for any production whose folder
   is not set out-of-band; `GetActiveAsync`/`GetByProductionCodeAsync` omit the
   column from their SELECTs while `FromRow` reads it (JA-04, HIGH
   CONFIDENCE).
5. `app_settings` has **zero writers** (HS-06, CONFIRMED) — the only read key
   `main_documents_output_root` (`DapperAppSettingsReader.cs:18,32`) can never
   be populated by code, hard-failing Pegamentos document confirm and silently
   degrading the Job On image provider.
6. **Reparação Externa return status machine** — `ConfirmReturnAsync` re-reads
   the item list on a **separate connection** (READ COMMITTED) before commit,
   so the just-confirmed return is invisible and `concluido`/`retorno_parcial`
   are effectively unreachable on the finishing return (RE-01, CONFIRMED).
7. **Consolidated clean-install baseline drift persists and extends** —
   `access_template_profiles` (N31) is absent (60 vs 61 app tables),
   `article_reference_images` (N29) lacks its RLS/policy/grants stanza, the
   header still claims N01…N24, and — new since the prior audit — the baseline
   does **not** reproduce the N33 quiescence posture (it still emits
   `profile_title NOT NULL` + CHECK and full junction privileges to
   `ba_dmo_app`), so a fresh install diverges from a chain-migrated DB
   (CONFIRMED, this report §4).

**Orphan/legacy surface** (verified, §17/§18): dead tables
`peso_comparacao_anterior`, `job_on_field_option`, unreachable
`tool_check_occurrences` (zero writers), dormant `tampao_planos` (D-8),
write-only `repair_events`; dormant columns `modules_override`,
`job_on_revision.image_asset_id`, `nominal_average`, `bq_traces.sap_end`,
`bq_discrepancies.resolved_by/resolved_at_utc` (never written); a large set of
runtime-dead repository/service methods (traced in §18).

**Clean surfaces (explicitly PASS):** migration chain integrity N01→N33
(shipped-family test asserts completeness), append-only trigger discipline,
RLS/policy/grants parity across all 61 app tables in the chain, ON CONFLICT
targets (every arbiter exists as a PK/unique), Tampões transform atomicity
(UoW + FOR UPDATE), Boquilhas bundle atomicity, Controlo sheet
status/decision CHECK alignment, Armazém locked transitions, repair pickup
business-write atomicity, and the D-1/D-2 access authority chain.

Overall contract health: the schema layer is disciplined and internally
consistent; the **runtime write layer carries one critical bug (PG-01), one
cross-cutting audit-payload convention break, two complete audit-coverage
gaps (JobOn/Pegamentos), one dead configuration authority (app_settings), and
a repair status-machine defect**. The remediation backlog is prioritized in
§20 and sequenced in §21.

---

## 2. Audit Scope and Evidence

### 2.1 Scope

Complete post-Codex database contract audit, all four layers:

1. **Migrations:** `database/migrations/N01_identity.sql … N33_legacy_access_
   mirror_quiescence.sql` (33 files, ~2,800 lines) read in full; expected
   final schema constructed from the chain.
2. **Current/live schema:** **NO live endpoint was reachable in this session**
   (no connection env vars; no local PG). Current-schema state is
   migration-derived plus the owner-supplied live facts recorded in prior
   artifacts (N34 audit; SCHEMA-RAT-03A postdeploy report — project
   `bddfhbyrmchktqotpzgb`; dated 2026-08-28). Every table with unverified live
   contents is marked `LIVE VERIFICATION REQUIRED`.
3. **Persistence code:** every Dapper repository/lookup/UoW factory in
   `src/BA.Dmo.Infrastructure/` (23 repositories/lookups + 3 UoW factories +
   migration subsystem), every service in `src/BA.Dmo.Application/Modules/`
   with SQL-relevant paths, domain aggregates where they constrain writes, and
   `src/BA.Dmo.Web/Program.cs` (endpoint + DI inventory).
4. **Models/projections:** Application DTOs/records and repository mapping
   code cross-checked against SELECT column aliases.

### 2.2 Primary evidence read in full (this session)

- Migrations N01–N33 (all 33 files).
- `database/consolidated_clean_install.sql` (1,666 lines; 61 CREATE TABLE =
  60 app tables + `schema_migrations`; **missing `access_template_profiles`
  → 60/61**).
- Persistence foundation: `Db.cs`, `DapperUnitOfWork.cs`, `DbConnectionFactory.cs`,
  `PersistenceMappings.cs`, `DateTimeOffsetHandler.cs`, `AuditJson.cs`,
  `MigrationRunner.cs`, `MigrationDiscovery.cs`, `MigrationChecksum.cs` (grep),
  `NpgsqlMigrationScriptGateway.cs`, `MigrationFile.cs`.
- Infrastructure repositories (full): `DapperInternalUserRepository`,
  `DapperAdminRepository`, `DapperJobOnRepository`, `DapperArticleReferenceImageRepository`,
  `DapperJobOnUserContextRepository`, `DapperBoquilhasRepository`,
  `DapperRepairRepository`, `DapperReparacaoInternaRepository`,
  `DapperArmazemRepository`, `DapperArmazemRepairMovementRepository`,
  `DapperFerramentasRepository`, `DapperPesoRepository`,
  `DapperPegamentoRepository`, `DapperTampaoRepository`,
  `DapperControloSheetRepository`, `DapperHistoriaRepository`,
  `DapperAppSettingsReader`, `DapperModuleCatalogMirrorRepository`,
  lookups/resolvers.
- Application services (full): `AdminUserService`, `AdminTemplateService`,
  `AdminAuditService`, `AdminMirrorService`, `IdentityResolutionService`,
  `BootstrapAdminService`, `JobOnService`, `PesoService`, `ControloSheetService`,
  `PegamentoService`, `BoquilhasService`, `FerramentasService`, `ArmazemService`,
  `ReparacaoExternaService`, `ReparacaoInternaService`, `TampaoService`,
  `HistoriaService`.
- Web: `Program.cs` (1,631 lines; endpoint + DI inventory), Admin Razor page
  handlers, `Pages/Auth/*`, `Pages/JobOn/*`.
- Tests (as evidence of enforced contracts): `MigrationDiscoveryTests`
  (family N01–N33 asserted), `AccessAuthorityGuardTests`,
  `AccessMirrorQuiescenceGuardTests`, `AuditJsonBindingTests`,
  `RemediationGuardTests` (N01–N33 scope, N33 probes), new PG-gated suites
  (`ArmazemReturnPostgresTests`, `JobOnLifecyclePostgresTests`,
  `RepairAtomicityTests`).
- Prior audit artifacts: `persistence_cross_reference_audit.md`,
  `persistence_high_impact_validation.md`, `schema_rationalization_target_
  architecture.md`, `schema_rationalization_owner_decisions.md`,
  `schema_rationalization_03A_postdeploy_parity_check.md`,
  `schema_rationalization_03B_plan.md`,
  `schema_rationalization_n32_application_path.md`,
  `schema_rationalization_N34_legacy_mirror_removal_audit.md` (unchanged).

### 2.3 Delegated module audits

Per-module deep audits were executed in parallel and are reconciled into this
report (§19). Each module finding was corroborated at code level by this
session's direct reads where it changed the verdict (JobOn lifecycle, audit
cast sites, Armazém locking, repair atomicity, admin projection, identity
query, Tampões `alterar_configuracao`, BQ audit insert, Pegamentos create
path, RI audit insert, Controlo sheet event insert, História
queries/app_settings).

### 2.4 Known environment limits

- No live DB → `LIVE VERIFICATION REQUIRED` markers throughout (§22).
- NuGet vulnerability-data fetch offline (NU1900 warnings) — informational.

---

## 3. Migration Evolution Summary

| N | Theme | Objects created/changed (tables unless noted) |
|---|---|---|
| N01 | Identity/roles/audit | roles `ba_dmo_app`/`ba_dmo_migrate`; default privileges; `ba_dmo_guard_append_only()`; `access_templates`, `internal_users` (incl. `profile_title` NULL), `audit_events` + 7 indexes + append-only trigger; 3 indexes on users |
| N02 | Catalog mirror | `module_catalog_mirror` |
| N03 | Boquilhas | `bq_lotes`, `bq_traces`, `bq_movements` (+append trigger), `bq_discrepancies`, `bq_lifecycle_history` (+trigger), `bq_utilisation_readings` (+trigger); CHECKs incl. movement_type 7 values |
| N04 | Ferramentas | `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences` |
| N05 | Job On family | `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`, `job_on_audit_event` (+trigger), `job_on_field_option`; circular FK `fk_job_on_current_revision` |
| N06 | Peso | `peso_references`, `peso_lotes`, `peso_controlos`, `peso_leituras` (CASCADE), `peso_comparacao_anterior` (CASCADE), `peso_day_approvals`, `peso_settings` |
| N07 | Pegamentos | `pegamento_controlos`, `pegamento_medicoes` (+append trigger) |
| N08 | Repairs | `repairers`, `line_repairer_defaults`, `repair_exits`, `repair_exit_items`, `repair_events` (+trigger), `internal_repair_records`; late FK `fk_repair_events_internal_record` |
| N09 | Armazém | `warehouse_locations`, `warehouse_stock` (+partial unique active occupation), `warehouse_movements` (+trigger) |
| N10 | Tampões | `tampao_field_defs`, `tampao_field_values`, `tampao_configurations`, `tampao_saldos`, `tampao_movements` (+trigger), `tampao_planos` |
| N11 | Shared | `app_settings` |
| N12 | RLS | RLS enabled on 49 tables (48 app + `schema_migrations`); anon/authenticated REVOKE; `ba_dmo_app` grants; per-table `ba_dmo_app_access` policy |
| N13 | JobOn | `job_on.production_folder text NULL` |
| N14 | Pegamentos | `pegamento_documentos` (1:1 UNIQUE) |
| N15 | Pegamentos | `pegamento_medicoes.tool_number integer NULL` + index |
| N16 | Pegamentos | `pegamento_controlos.cm_nominal/bq_nominal/mf_nominal` |
| N17 | Pegamentos | `pegamento_controlos.notas text NULL` |
| N18 | Boquilhas | `bq_movements.noted_repairer_id uuid NULL REFERENCES repairers` (no index) |
| N19 | Ferramentas | `tool_usage_records` (+append trigger) |
| N20 | Repair | `repairer_repair_types` (PK repairer_id,repair_type) |
| N21 | Tampões | `tampao_configuration_machines`, `tampao_configuration_notes` (+trigger), `tampao_configuration_machine_event` (+trigger) |
| N22 | RI | `internal_repair_records`: widen tool_type to include BQ (later narrowed by N28); add `job_on_revision_id` (+FK), `production_code`, `reference`, `lot_id` |
| N23 | Controlo | `controlo_sheets`, `controlo_sheet_items` (CASCADE), `controlo_sheet_events` (+trigger, CASCADE) |
| N24 | JobOn | `jobon_user_current` (PK actor_id) |
| N25 | Remediation | `internal_users.auth_user_id NOT NULL` + `uq_internal_users_auth_user` (fail-closed guard); `uq_job_on_identity` partial unique; `ck_job_on_lifecycle_consistent`; `uq_bq_traces_active`; `ck_pegamento_controlos_status`; `ck_repair_exit_items_status`; `ck_peso_controlos_approved_consistent`; `ba_dmo_guard_peso_approved` on `peso_controlos`; `ck_job_on_verification_completed`; append-only triggers on the 4 revision-family tables; RLS/policy/grants for 10 post-N12 tables; `ix_audit_events_module_time` |
| N26 | Access | `internal_users.modules_override jsonb` (dormant) |
| N27 | Access | `internal_user_access_templates` junction; profile inference + `profile_title SET NOT NULL`+CHECK; modules normalization; `modules_override = NULL`; junction RLS/policy/grants |
| N28 | RI | `ck_internal_repair_records_type` re-narrowed to `('CM','MF')` NOT VALID+VALIDATE (N22 BQ widening reversed; fail-closed guard). Transaction-control debt: explicit BEGIN/COMMIT |
| N29 | JobOn | `article_reference_images`; legacy image promotion from `job_on_revision.image_asset_id` (fail-closed guards); RLS/policy/grants inline. BEGIN/COMMIT |
| N30 | JobOn | `ix_article_reference_images_updated_by`. BEGIN/COMMIT |
| N31 | Access | `access_template_profiles` (+CHECK + N31 trigger `ba_dmo_ensure_access_template_profile` + backfill); junction collapse to single assignment + `ux_internal_user_access_templates_actor`; `profile_title` sync; RLS/policy/grants |
| N32 | Access | D-1/D-2 fail-closed reconciliation (junction guards, deterministic profile backfill); non-destructive bounds restated |
| N33 | Access | `profile_title DROP NOT NULL`; junction `REVOKE ALL` from `ba_dmo_app`; column-level SELECT/INSERT/UPDATE grants on `internal_users` excluding `profile_title` |

**Expected final schema (migration-derived):** 61 application tables (count
verified per-family above) + `schema_migrations` = 62 total; 3 functions
(`ba_dmo_guard_append_only`, `ba_dmo_guard_peso_approved`,
`ba_dmo_ensure_access_template_profile`); 21 triggers; every app table RLS
enabled + `ba_dmo_app` policy + anon/authenticated denial in the chain.

**Chain-integrity findings:**

- **MC-01 (CONFIRMED, positive):** the family is complete and deterministic.
  `MigrationDiscoveryTests.ShippedFreshBuildFamily_IsComplete_N01ThroughN33`
  asserts the exact 33-file list (line 90-103); discovery is ordinal
  (`MigrationDiscovery.cs:26-54`); runner is whole-script + SHA-256 +
  record-after-success (`MigrationRunner.cs:51-93`).
- **MC-02 (HIGH CONFIDENCE — carrying from prior audit, remains):** explicit
  `BEGIN;…COMMIT;` inside runner-owned per-script transactions in N28
  (:12,37), N29 (:11,157), N30 (:4,9). The gateway wraps each script in its
  own transaction (`NpgsqlMigrationScriptGateway.cs:78-83`), so the inner
  `BEGIN` is a no-op and the script `COMMIT` commits the gateway transaction;
  the gateway's trailing `CommitAsync` is then a warning-level no-op (25P01).
  Functionally tolerated today, but any future statement placed after an inner
  `COMMIT` silently breaks atomicity; the mechanism is unproven by a real-PG
  test. `LIVE VERIFICATION REQUIRED` for N28–N30 execution.
- **MC-03 (HIGH CONFIDENCE):** `N22` widening of the RI tool_type CHECK to
  BQ is immediately reversed by `N28` (narrowed CM/MF) — final state correct;
  the N22 header comment ("BQ is a THIRD recordable type") is stale
  documentation drift.

---

## 4. Current Schema Authority Map

Live schema is not reachable from this session. The owner-supplied live facts
on record (project `bddfhbyrmchktqotpzgb`, read-only audits dated
2026-08-28):

- Live provenance is **Supabase-CLI managed**
  (`supabase_migrations.schema_migrations`, last row
  `20260827150130`/`n31_template_profiles_single_assignment`); N32 was NOT
  registered as of that date; clean parity observed (0 multi-assignments, 0
  junction conflicts, 0 missing profiles, 0 divergences) — per
  `schema_rationalization_03A_postdeploy_parity_check.md` §UPDATE.
- The N34 audit's live facts (§2, most recent): `internal_user_access_templates`
  and `internal_users.profile_title` are fully quiesced — `ba_dmo_app` has NO
  privileges on either; RLS enabled on the junction; only the CHECK
  `ck_internal_users_functional_profile` depends on `profile_title`; zero
  external `pg_depend` for the junction. **This implies N33 (at least) was
  applied live after the 03A observation.**
- Everything else about the live DB (row counts, per-table contents, applied
  index/trigger state) is **not verifiable from this session** →
  `LIVE VERIFICATION REQUIRED`.

**Authority map (runtime-authoritative objects — migration-derived):**

```
internal_users.template_id  (direct FK — D-2 authority)
   └─> access_templates.modules  (module-access authority; capabilities [] inert)
   └─> access_template_profiles.functional_profile  (functional profile — D-1 authority)
internal_users.auth_user_id  (identity link; NOT NULL + UNIQUE since N25)
audit_events  (global transversal audit — single authority)
module_catalog_mirror  (display read-model; in-code CanonicalModuleCatalog is the authority)
job_on / job_on_revision graph  (production context + immutable attribution anchor)
peso_controlos / pegamento_controlos / controlo_sheets / internal_repair_records
   └─> job_on_revision_id (immutable anchor — append-only since N25)
warehouse_stock active rows  (occupancy current-state; movements = facts)
tampao_saldos + tampao_movements  (balance current-state + append-only fact history)
bq_*  (lot/trace/movement/discrepancy/lifecycle/reading layers)
repairers / repairer_repair_types / line_repairer_defaults  (canonical TD-15)
app_settings / peso_settings  (settings; app_settings currently write-less — HS-06)
```

**Consolidated baseline vs migration chain (CONFIRMED, this session):**

| # | Drift | Evidence |
|---|---|---|
| CB-01 | **N31 objects absent**: no `access_template_profiles`, no `ba_dmo_ensure_access_template_profile`, no `trg_access_templates_ensure_profile`, no `ux_internal_user_access_templates_actor`, no N31 backfill/sync DML/policy → consolidated clean install yields **60 app tables** (61 with `schema_migrations`) vs 61/62 chain | grep zero matches; `consolidated_clean_install.sql` CREATE TABLE list |
| CB-02 | **`article_reference_images` created without RLS/policy/grants stanza** (N29 migration has them inline N29:139-155) → a consolidated-built DB has the table RLS-less and un-granted | `consolidated_clean_install.sql:452-470` vs RLS arrays at :1229-1256/:1553-1575; only occurrence is the CREATE |
| CB-03 | **Header stale**: claims "N01 … N24", references missing `/reports/consolidated_schema_equivalence.md`, trailing comment says "includes N25-N27" while body contains N28-N30 objects | `:4-29, :1666` |
| CB-04 | **Pre-N33 security posture for the access mirrors**: baseline still emits `profile_title NOT NULL` + `ck_internal_users_functional_profile` (:1632-1638) and junction CREATE + RLS + full DML grants to `ba_dmo_app` (:1621-1664) — does NOT reproduce N33's DROP NOT NULL / REVOKE / column-level grants | `:1618-1664` vs `N33:63-108` — NEW since prior audit |
| CB-05 | N27/N28/N29 reconciliation DML omitted (no-op on empty DB; parity UNKNOWN on partial databases) | `:1618-1664` vs `N27:19-111`, `N28:14-35`, `N29:31-137` |

---

## 5. Database Contract Health Matrix

The 61 application tables are grouped by domain with the aggregated contract
verdicts; full reader/writer evidence is in §19 per module and in the
per-module sub-reports. Verdicts are per-axis: **S-m** Migration↔Schema,
**S-D** Schema↔Dapper, **M-D** Migration↔Dapper.

| Group | Tables | S-m | S-D | M-D | Overall risk |
|---|---|---|---|---|---|
| Identity/Access/Admin | internal_users, access_templates, access_template_profiles, internal_user_access_templates (mirror, dead), module_catalog_mirror | PASS | PASS | PASS* | LOW (deploy-order caveat ADM-14) |
| Job On | job_on, job_on_revision, job_on_component, job_on_component_field, job_on_component_row, job_on_verification_occurrence, job_on_audit_event, job_on_field_option, article_reference_images, jobon_user_current | PASS | PARTIAL (JA-04 select gap; dormant write surface) | PASS | MEDIUM |
| Peso | peso_references, peso_lotes, peso_controlos, peso_leituras, peso_comparacao_anterior, peso_day_approvals, peso_settings | PASS | PARTIAL (jsonb binds, approved-leituras rewrite, Guid.Empty FK) | PASS | HIGH |
| Pegamentos | pegamento_controlos, pegamento_medicoes, pegamento_documentos | PASS | **FAIL (PG-01 create, PG-02 one-sided)** | PASS | CRITICAL |
| Controlo Folha | controlo_sheets, controlo_sheet_items, controlo_sheet_events | PASS | PARTIAL (jsonb event binds) | PASS | MEDIUM |
| Boquilhas | bq_lotes, bq_traces, bq_movements, bq_discrepancies, bq_lifecycle_history, bq_utilisation_readings | PASS | PARTIAL (audit binds, sap_end, discrepancy calc) | PASS | HIGH |
| Ferramentas | tool_references, tool_lotes, physical_pieces, tool_check_rules, tool_check_occurrences, tool_usage_records | PASS | PARTIAL (occurrences no writer; status double-meaning; audit binds) | PARTIAL | MEDIUM-HIGH |
| Armazém | warehouse_locations, warehouse_stock, warehouse_movements | PASS | PASS (1:1-per-position code-enforced only) | PASS | LOW-MEDIUM |
| Reparação Externa | repairers, repairer_repair_types, line_repairer_defaults, repair_exits, repair_exit_items, repair_events | PASS | PARTIAL (RE-01 status; audit post-commit) | PASS | HIGH |
| Reparação Interna | internal_repair_records | PASS | PARTIAL (uncast audit binds; GetChainRoot orphan) | PASS | MEDIUM |
| Tampões | tampao_field_defs, tampao_field_values, tampao_configurations, tampao_saldos, tampao_movements, tampao_planos, tampao_configuration_machines, tampao_configuration_notes, tampao_configuration_machine_event | PASS | PARTIAL (balances_after fidelity; audit binds) | PASS | MEDIUM |
| Audit/Shared | audit_events, app_settings | PASS | PARTIAL (audit payload convention; app_settings writers=0) | PASS | HIGH (audit completeness) |

\* N33-before-write deploy-order requirement (ADM-14) — see §6.

---

## 6. Confirmed Broken Contracts

Highest-priority findings; each is either a hard failure on a live path or a
cross-module contract break.

**PC-01-CRIT — Pegamentos create path violates NOT NULL (CONFIRMED, LIVE VERIFICATION REQUIRED on deployed DDL).**
- Evidence: `DapperPegamentoRepository.cs:91` `UpdatedAtUtc = (object?)control.UpdatedAtUtc ?? DBNull.Value`; `PegamentoControlo.cs:66` (`DateTimeOffset? UpdatedAtUtc`, unset by `Create()` at :100-119); N07:44 `updated_at_utc timestamptz NOT NULL DEFAULT now()` (explicit NULL bypasses DEFAULT → 23502); flow `PegamentoService.CreateControlAsync:39-70` → `Repository.CreateAsync`.
- Impact: `POST /api/pegamentos` (Program.cs:610-617) fails on control creation on a migration-compliant DB.
- Test: none exercises this path with PG (PegamentoWebApiTests use fakes).

**PC-02-CRIT — One-sided pegamento measurement impossible (D-12 NOT implemented) (CONFIRMED).**
- Evidence: domain `AddMeasurement(…, decimal? contraCostura …)` (`PegamentoControlo.cs:184`), `PegamentoMedicao.ContraCostura decimal?` (:300), calculator supports single-sided (`PegamentoMeasurementCalculator.cs:12-25`); repository binds `ContraCostura ?? DBNull.Value` (`DapperPegamentoRepository.cs:295`); column `contra_costura numeric(18,4) NOT NULL` (N07:63). No DB relaxation, alternative CHECK, or 0-fill.
- Impact: any measurement without contra costura → 23502.

**PC-03-CRIT-HIGH — Cross-module audit jsonb binding convention break (5 of 9 global emitters uncast; ≥17 non-JSON payload sites) (CONFIRMED statically; runtime LIVE VERIFICATION REQUIRED).**
- Evidence (uncast, no Normalize): `DapperBoquilhasRepository.cs:583-592`; `DapperTampaoRepository.cs:456-467`; `DapperPesoRepository.cs:519-537`; `DapperFerramentasRepository.cs:523-541`; `DapperReparacaoInternaRepository.cs:196-213`; `DapperControloSheetRepository.cs:194-213` (module-local events jsonb). Free-text payloads: `BoquilhasService.cs:408-409,446-447,490-491,538-539`; `TampaoService.cs:172,212,491,510`; `PesoService.cs:273,480,498,667,828`; `FerramentasService.cs:63,145,168,191,212,245,378`; `ReparacaoInternaService.cs:202,368` (serialized JSON — safe). Contrast (cast + Normalize, enforced by `AuditJsonBindingTests.cs:27-29,59-61,80-81`): `DapperAdminRepository.cs:651,666-667` (cast, always NULL), `DapperArmazemRepository.cs:441,450-451`, `DapperRepairRepository.cs:426,432-433`, `DapperJobOnRepository.cs:529,536-537`, `DapperArticleReferenceImageRepository.cs:163-170` (JSON built).
- Impact: co-tx BQ/Tampões flows throw 22P02 → generic `…_SAVE_FAILED`; post-commit Peso/Ferramentas flows persist the business write then 500; exact SQLSTATE depends on Npgsql parameter typing → `LIVE VERIFICATION REQUIRED`.

**PC-04-HIGH — PEGAMENTOS has no audit_events emitters at all (CONFIRMED).**
- Evidence: grep `audit|Audit` in `Modules/Pegamentos/*` → single doc comment (`PegamentoService.cs:121`); `DapperPegamentoRepository` has no audit SQL; sibling modules emit in-flow.
- Impact: História module filter `pegamentos` is empty; create/measure/update/close/confirm are globally unaudited.

**PC-05-HIGH — Job On never emits audit_events (D-5 dual-emit NOT implemented) (CONFIRMED).**
- Evidence: only `job_on_audit_event` writers (`DapperJobOnRepository.cs:527-529,610-612`; `DapperArticleReferenceImageRepository.cs:163`); zero `audit_events` references in JobOn files; `DapperHistoriaRepository` projects only `audit_events` (`:30-47,88-111`); N25:11 says "D2/INT-06 Option C — dual emit (code-side; no DDL here)" — **HEAD does not implement it**.
- Impact: transversal História hides all Job On creation/transition/revision/image facts.

**PC-06-HIGH — `job_on.production_folder` has NO application writer (CONFIRMED); calendar SELECTs omit the column (HIGH CONFIDENCE).**
- Evidence: readers only (`DapperJobOnRepository.cs:80`; `DapperJobOnProductionFolderResolver.cs:25-43`); zero INSERT/UPDATE of it; `GetActiveAsync` SELECT (:111-127) and `GetByProductionCodeAsync` (:148-164) do **not** project `production_folder` while `FromRow` reads `row.production_folder` (`JobOn.cs:51`) → silent null risk on calendar/RI/Controlo-by-production paths (DapperRow missing-column semantics NEEDS VERIFICATION).
- Impact: `PegamentoService.ConfirmDocumentSavedAsync:250-254` hard-fails `PEGAMENTO_PRODUCTION_FOLDER_MISSING` for any production whose folder was not set out-of-band (SQL/admin).

**PC-07-HIGH — `app_settings` has zero writers; `main_documents_output_root` unsettable (CONFIRMED).**
- Evidence: grep INSERT/UPDATE/DELETE `app_settings` → 0; N11:12 seeds nothing; readers `DapperAppSettingsReader.cs:18,28-72` consumed by `FileSystemJobOnImageProvider.cs:45` and `PegamentoService.cs:243`.
- Impact: `PEGAMENTO_OUTPUT_ROOT_MISSING` (PegamentoService.cs:244-247) and JobOn images always absent via filesystem provider; only manual DBA insert can fix. Owner decision needed (GLM-ARCH-05 "each setting written only by its owner").

**PC-08-HIGH — Reparação Externa return status machine: `concluido`/`retorno_parcial` unreachable on the finishing return (RE-01, CONFIRMED).**
- Evidence: `ReparacaoExternaService.ConfirmReturnAsync:335-341` recomputes list status from `GetExitItemsAsync` which opens a **fresh connection** (`DapperRepairRepository.cs:94-107`) before commit → under READ COMMITTED the just-confirmed item's `in_at_utc` is invisible; `RepairExitStatusMachine.ConfirmReturn` (`RepairExitStatusMachine.cs:50-63`) then never sees `itemsAfter.All(InAtUtc.HasValue)` on the finishing return.
- Impact: exit lists never reach `concluido` via the normal flow; only a *subsequent* return of a different item can produce `retorno_parcial`. Business row (item return + movement) still commits — status is the loss.

**PC-09-HIGH — Peso approved-control readings are still rewritable (D-10 NOT implemented) (CONFIRMED).**
- Evidence: `DapperPesoRepository.UpdateControlAsync:383-399` DELETE+re-INSERTs `peso_leituras` unconditionally; `peso_leituras` has no append-only trigger (N06:118-126); N25's `ba_dmo_guard_peso_approved` (`N25:137-165`) guards only `peso_controlos` identity+delete; `SaveControlAsync` with ChangeReason can pass `ValidateControlEditable` for `aprovado` (PesoReference.cs:93-105).
- Impact: approved control readings and even partial header can be rewritten; immutability contract (GLM-PESO-06.7) breached at the DB layer.

**PC-10-HIGH — Consolidated baseline drift (CB-01/CB-02/CB-04) (CONFIRMED, §4).** N31 objects missing → N31-dependent Admin template edit breaks on consolidated-built DBs (42P01); `article_reference_images` RLS-less; mirrors at pre-N33 posture.

**PC-11-MED — Admin audit rows always carry NULL before/after summaries (ADM-08, HIGH CONFIDENCE).** `DapperAdminRepository.cs:666-667` binds nulls despite `AuditEntry.Before/AfterSummary` (AdminModels.cs:123-124); no Admin-scope caller passes them; no Normalize → a future non-null non-JSON string would 22P02.

**PC-12-MED — Job On write surface is entirely dormant; duplicate non-canceled identity → unhandled 23505 (JA-08/JA-03, CONFIRMED).** No create/duplicate/save/transition/confirm endpoints (Program.cs:291-380 only image/current/document); `DuplicateAtomicallyAsync` inserts without ON CONFLICT against `uq_job_on_identity` (N25:60-62) → raw 23505 500.

**PC-13-MED — Tampões `alterar_configuracao` `balances_after` fact is truncated/false; audit after_summary is the destination BEFORE state (TP-01/TP-02, CONFIRMED).**
- Evidence: `TampaoService.cs:445` `BalancesAfter = SerializeBalances(new TampaoSaldo { Enchidos = newOriginEnchidos })` (por_encher forced 0, destination absent) vs DB write preserving por_encher (:435); audit receives `destBefore` as after (:449-451).
- Impact: movement fact cannot reproduce balances; História misattributes the "after" state.

**PC-14-MED — Boquilhas `bq_discrepancies.expected_qty` stores prior accumulated ExceptionalReceived, not the matched return (BQ-03, HIGH CONFIDENCE); `resolved_by`/`resolved_at_utc` never written (BQ-04, CONFIRMED); `under_review` unproducible (BQ-05).**
- Evidence: `BoquilhasService.cs:233`; `BqRules.cs:125-132`; `DapperBoquilhasRepository.cs:400-409` binds both NULL; disjoint codec.
- Impact: wrong discrepancy expected values; resolution unattributable in-table.

**PC-15-MED — Ferramentas lot duplication is NOT atomic despite the repository doc claim (FA-03, CONFIRMED); piece `status` column double-meaning (FA-05, CONFIRMED).**
- Evidence: `FerramentasService.CreateLoteFromBaseAsync:111-150` (per-rule own-connection calls); doc `DapperFerramentasRepository.cs:10-15`; `RegisterPieceAsync:260` writes condition codec into `physical_pieces.status` (N04:72 no CHECK); `MapPiece:605` hard-codes "operational".
- Impact: partial rule copies on failure; condition facts untyped at DB.

**PC-16-MED — BQ open-trace unique race surfaces 23505 as generic error (BQ-07, CONFIRMED mechanism); `bq_traces.start_line` binding nullable vs NOT NULL (BQ-14, latent); `noted_repairer_id` has no index (BQ-16, HIGH).** (§10/§14.)

---

## 7. Partial Refactor / Dual-Authority Findings

Classified per the task taxonomy; confirmed high-priority cases first.

**PA-01 — PARTIAL_MIGRATION (structurally confirmed): `tool_check_occurrences` (N04) has NO writer; the live materialization is `job_on_verification_occurrence` (N05).**
- Evidence: only reader `DapperFerramentasRepository.GetOccurrencesByRuleAsync:427-441` (+ interface `IFerramentasRepository.cs:43`, DTO `FerramentasRequests.cs:112-119`); zero INSERT/UPDATE in src; Job On writes the N05 sibling (`DapperJobOnRepository.cs:411-443,445-468`; N05:170-187 with real FK N05:173). N04 table is schema-only, its CHECKs dead. Two competing occurrence models.
- Classify: new table written / old table never read-or-written → **PARTIAL_MIGRATION (N04 legacy surface, N05 authority)**.

**PA-02 — DUAL_AUTHORITY (dead mirror): previous-approved Peso comparison.**
- Evidence: `peso_comparacao_anterior` (N06:134-140) has **zero SQL anywhere in src/** (grep) while the live query `DapperPesoRepository.GetPreviousApprovedAsync:417-446` (interface `IPesoRepository.cs`) resolves the fact. D-9 REMOVE_LATER owner decision exists (owner_decisions D-9) but table is still present.
- Classify: table (dead) vs live query (authority) → **DUAL_AUTHORITY (legacy)**.

**PA-03 — DUAL_AUTHORITY (dormant twin): Armazém `Substituir` vs `Corrigir localização`.**
- Evidence: `ArmazemService.SubstituirAsync:128-180` + `ReplaceOccupationAsync` (`DapperArmazemRepository.cs:259-295`, **no FOR UPDATE** — FA-04) with **no route**; the live UI path is `corrigir-localizacao` (`Program.cs:882-888`, `armazem.js:578`) which uses FOR UPDATE (:315-332). Two release+occupy implementations on the same tables; one locked, one dormant and unlocked.
- Classify: **DUAL_AUTHORITY remnant** (dormant Substituir vs live Corrigir). Risk rises if Substituir is ever revived.

**PA-04 — PARTIAL_MIGRATION (surface-level, not data): entire Job On write family is repository/service-complete but unwired.**
- Evidence: endpoints only for image/current/document (`Program.cs:291-380`); no route for create/duplicate/save/transition/confirm; service methods `CreateAsync/DuplicateAsync/SaveRevisionAsync/TransitionAsync/ConfirmVerificationAsync` have zero runtime callers; 8 repository methods caller-less (`InsertRevisionAsync`, `GetRevisionsAsync`, `InsertComponentsAsync`, `InsertFieldsAsync`, `InsertRowsAsync`, `InsertVerificationsAsync`, `UpdateCurrentRevisionAsync`, `InsertImageMutationAsync`).
- Classify: dormant write surface (D-4 Option B deferral); the DB constraints added by N25 for these flows are runtime-unexercised.

**PA-05 — RESOLVED (verification, not a finding): both legacy access mirrors.**
- Zero src references (grep); N32 fail-closed guards; N33 kill switches; `AccessAuthorityGuardTests` (7 facts) and `AccessMirrorQuiescenceGuardTests` pin the state. Not a partial refactor — a completed one.

**PA-06 — DUAL_AUTHORITY (gap, not conflict): Job On history (`job_on_audit_event`) vs global audit (`audit_events`).** D-5 dual-emit not implemented — see PC-05. The two tables are different concepts (domain stream vs compliance projection); the defect is a missing projection, not a conflict.

**PA-07 — PARTIAL_MIGRATION risk: `bq_traces.sap_start/sap_end` vs `bq_utilisation_readings`.** `sap_start` is mirrored onto the trace at create (`BoquilhasService.cs:101`); `sap_end` is **never written** (BQ-08, CONFIRMED) — the "final" utilisation lives only in `bq_utilisation_readings`. A partial mirror with the promised trailing fact missing.

---

## 8. Dapper / SQL Contract Findings

Column-existence/type/nullability verification results per module (full tables
in §19/sub-reports; representative confirmed items):

- **On-conflict targets all valid** (DAP-OK): `access_templates` PK
  (`DapperInternalUserRepository.cs:64`), `access_template_profiles` PK
  (`DapperAdminRepository.cs:536-538,605-607`), `module_catalog_mirror` PK
  (`DapperModuleCatalogMirrorRepository.cs:26-30`), `internal_users.actor_id`
  PK (`DapperAdminRepository.cs:184`), `article_reference_images.reference_code`
  PK (`DapperArticleReferenceImageRepository.cs:69`), `jobon_user_current.actor_id`
  PK (`DapperJobOnUserContextRepository.cs:40`), `line_repairer_defaults` PK
  (`DapperBoquilhasRepository.cs:568-573`, `DapperRepairRepository.cs:338`),
  `pegamento_documentos.pegamento_controlo_id` UNIQUE
  (`DapperPegamentoRepository.cs:334-339`), `tampao_saldos` UNIQUE
  (`DapperTampaoRepository.cs:230`), `peso_day_approvals` UNIQUE
  (`DapperPesoRepository.cs:457`), `peso_settings` PK (:493),
  `warehouse_locations.code` UNIQUE (`DapperArmazemRepository.cs:44-47` /
  `DapperArmazemRepairMovementRepository.cs:158`).
- **SELECT-column existence:** all verified present; notable defects are the
  *missing* columns: `production_folder` omitted from `GetActiveAsync` /
  `GetByProductionCodeAsync` (JA-04, HIGH) and `pmt/nominal_average`
  dormancy (PG-10 — column exists, no code reads it).
- **`SELECT c.*` wildcards:** `DapperPesoRepository.cs:264,290,331`
  (`SELECT c.*, …`) violate the "explicit column lists" convention
  (`Db.cs:22-26`) — carries from prior audit (PA-DAP-14), still present.
- **Insert column lists:** no INSERT anywhere references the quiesced mirrors
  (ADM-04); `pegamento_controlos` create is the only NOT-NULL-insert defect
  (PC-01).
- **`dynamic` mapping risk:** several repos map `dynamic` rows
  (e.g. `DapperBoquilhasRepository.MapLote`, `DapperRepairRepository.MapExit`,
  `DapperJobOnRepository`), which is fragile to column renames — no current
  rename detected; flag for future migrations.

---

## 9. Result Mapping / Type / Nullability Findings

- **DT-01 (HIGH CONFIDENCE): `pegamento_medicoes.contra_costura` NOT NULL vs
  nullable domain (PC-02).** Type/nullability mismatch on a live write path.
- **DT-02 (CONFIRMED): `pegamento_controlos.updated_at_utc` explicit-NULL
  insert (PC-01).** Nullability contract violation.
- **DT-03 (HIGH CONFIDENCE): missing `production_folder` in Dapper dynamic
  row (JA-04).** DapperRow missing-member behavior for `dynamic` needs
  live confirmation; severity either silent null (RI/Controlo calendar paths)
  or binder exception.
- **DT-04 (CONFIRMED): timestamp handling is UTC-consistent.** All
  `timestamptz` bound as UTC `DateTimeOffset`/`DateTime`; `DateTimeOffsetHandler`
  bridges; business dates use `date`.
- **DT-05 (CONFIRMED): status codecs align with CHECK constraints.** Verified:
  pegamento `aberto/fechado` (N25:96-104) ↔ `ToDbStatus`
  (`DapperPegamentoRepository.cs:39-44`); repair exit/items codecs
  (`RepairExitStatusCodec`, `ck_repair_exit_items_status` N25:110-118);
  peso statuses; controlo sheet status/decision; tampoes movement types;
  bq movement types; audit result.
- **DT-06 (HIGH): `physical_pieces.status` double-meaning** — condition codec
  written into an un-constrained column (`FA-05`); arbitrary values silently
  read as "New".
- **DT-07 (HIGH CONFIDENCE): `repair_exits.repairer_snapshot` / RI
  `before_snapshot` jsonb binds are cast-less** (`DapperRepairRepository.cs:62,69`;
  `DapperReparacaoInternaRepository.cs:36,56-57`) — same class as PC-03;
  content is JSON-valid at current call sites (calling `Serialize`), so
  runtime is likely OK but convention-fragile. `LIVE VERIFICATION REQUIRED`.
- **DT-08 (CONFIRMED): integer/GUID mapping consistent** (Guid PKs,
  Guid.Empty sentinel risk in Peso reference/lot FK — PESO-05
  (`PesoService.cs:359-361` can insert `Guid.Empty` → 23503 on real FK;
  HIGH CONFIDENCE).
- **DT-09 (CONFIRMED): `peso_controlos` `c.*` hydration includes dormant
  columns** — nominal_average etc. mapped but unused.

---

## 10. FK / Constraint Findings

- **FK-01 (CONFIRMED, positive): immutable revision anchors are protected.**
  The four consumers (peso_controlos N06:87, pegamento_controlos N07:32,
  controlo_sheets N23:32, internal_repair_records N22:47) pin
  `job_on_revision_id`; all four revision-family tables are append-only since
  N25 (triggers N25:187-209) — reconstruction/attribution safe.
- **FK-02 (CONFIRMED, by design): logical uuid links without FKs** —
  `job_on.article_reference_id` (N05:21), `tool_check_occurrences.job_on_id/
  job_on_component_id` (N04:115-116), `tampao_planos.job_on_id/production_code`
  (N10:117-118), `internal_repair_records.job_on_id/lot_id` (N22:42-50) —
  documented contract-level coupling; no DB orphan backstop. OK per design;
  flag: `job_on.article_reference_id` has no producer visible in src either.
- **FK-03 (HIGH CONFIDENCE): Guid.Empty sentinel risk** — Peso unresolved
  reference/lot routes bind `Guid.Empty` into real FKs (`PesoService.cs:359-361`),
  a latent 23503 500 with no pre-validation. NEEDS VERIFICATION for
  reachability.
- **FK-04 (CONFIRMED): cascade behavior inconsistent guard** —
  `peso_leituras`/`peso_comparacao_anterior` CASCADE on controlo delete
  (N06:120,135) is guarded only by the approved-trigger (approved rows cannot
  be deleted); `controlo_sheet_items/events` CASCADE on sheet delete (N23:67,88)
  with **no delete guard** on sheets (`DapperControloSheetRepository` has no
  DELETE path — good today, but no DB backstop); `access_template_profiles`
  CASCADE on template delete (N31:14) — templates are deactivated never
  deleted (AdminTemplateService.cs:16).
- **FK-05 (HIGH CONFIDENCE): no second-junction/concurrency backstop needed
  post-N31** — `ux_internal_user_access_templates_actor` exhausted; N33
  revoked all privileges; nothing can write the junction.
- **FK-06 (CONFIRMED): `bq_movements.noted_repairer_id` FK to repairers added
  N18 without index** — filter in `DapperBoquilhasRepository.cs:279` has no
  supporting index (see §14).
- **FK-07 (CONFIRMED): `job_on.copied_from_job_on_id` self-FK and
  `tool_check_rules.copied_from_rule_id` self-FK exist and are written
  (`DuplicateFrom`, `CreateLoteFromBaseAsync`) — no orphan risk detected.**

---

## 11. ON CONFLICT / Uniqueness Findings

- **ON-01 (CONFIRMED, positive): every ON CONFLICT arbiter exists** — §8 list.
- **ON-02 (CONFIRMED risk): `SELECT → if absent → INSERT` uniqueness without
  conflict handling** (concurrency may duplicate or 500):
  - `internal_users` create: ON CONFLICT (actor_id) DO NOTHING absorbs
    duplicates but the N25 `uq_internal_users_auth_user` is a *different*
    arbiter — a duplicate create with same auth_user_id but different actor_id
    raises 23505 unhandled (ADM-06, HIGH).
  - `job_on` create/duplicate: no ON CONFLICT on `uq_job_on_identity`
    (partial) → 23505 raw (JA-03).
  - `tampao_configurations` create: pre-check + plain INSERT against
    `uq_tampao_configurations_values` → concurrent same-destination → 23505 →
    generic `TAMPAO_SAVE_FAILED` (TP-06).
  - `bq_lotes` create: pre-check + plain INSERT against
    `uq_bq_lotes_reference_batch` → concurrent dup → generic `BQ_SAVE_FAILED`
    (BQ-15).
  - `warehouse_locations`: correct pattern (ON CONFLICT DO NOTHING + reselect).
- **ON-03 (HIGH CONFIDENCE): `uq_bq_traces_active` interplay with reopen** —
  reopen updates the SAME row to active (BQ-07); DB invariant holds, but the
  pre-check runs outside the UoW (`BoquilhasService.cs:429-443`) so a
  concurrent reopen surfaces 23505 as generic failure. Live UX
  NEEDS VERIFICATION.
- **ON-04 (CONFIRMED): `uq_warehouse_stock_active_occupation` is per
  (location, tool_lote); 1:1-per-position is NOT DB-enforced** (D-14 not
  implemented); safety is code-level FOR UPDATE (present on all live paths,
  **absent on dormant ReplaceOccupationAsync** — FA-04).

---

## 12. Transaction Findings

Mechanisms: `DapperUnitOfWork` (one connection + one transaction), 3 UoW
factories (Repair, Tampões, Boquilhas), caller-provided `IDbUnitOfWork`,
implicit single-command, and — for a residual set — **separate-connection
multi-command**.

**Atomic (verified — same connection + same transaction):**
- Job On save-graph, duplicate, image mutations, lifecycle transition
  (`DapperJobOnRepository.cs:636-704, 195-211`), article-image set/remove
  (`DapperArticleReferenceImageRepository.cs:60-147`).
- Bootstrap admin (template+user+audit; `DapperInternalUserRepository.cs:180-210`).
- Admin guarded user writes (UoW; `DapperAdminRepository.cs:245-275, 358-372`),
  template create/update (profile upsert same UoW; :510-549, :569-623).
- Peso control create/update/delete (controlo + leituras; `DapperPesoRepository`
  UoW usage).
- Controlo sheet create/submit/decide (+events; `ControloSheetService.cs:69-72,277-280`).
- Tampões transforms (saldos FOR UPDATE + movement + audit in one UoW;
  TP-07 CONFIRMED; **except `alterar_configuracao` fact fidelity TP-01**).
- Boquilhas lot+trace+movement+discrepancy+lifecycle bundles (UoW factory).
- Armazém entrada/saida/corrigir (UoW + FOR UPDATE).
- Repair create-exit (+items+audit; `ReparacaoExternaService.cs:90-101`),
  repairer-types replace (`DapperRepairRepository.cs:369-385`), pickup/return
  business writes (item+armazem port+exit status+repair event; :229-285,
  :289-348), RI record+event+audit (UoW).
- `jobon_user_current` upsert (single statement).

**NOT atomic / partial-completion risk (confirmed):**

| Flow | Shape | Evidence |
|---|---|---|
| Ferramentas lot duplication + rule copy + audit | separate connections per step | `FerramentasService.cs:132-145` (FA-03) |
| Ferramentas create reference w/ first lote: audit POST-commit | ref+lote UoW; audit own conn after | `DapperFerramentasRepository.cs:445-471`; `FerramentasService.cs:62-63` |
| Admin user/template/mirror mutations: audit POST-commit | domain write (own conn or UoW), then audit own conn | `AdminUserService.cs:222-232,269-285,320-339,368-386`; `AdminTemplateService.cs:133-139,179-201`; `AdminMirrorService.cs:87-99` (ADM-07) |
| Armazém entrada/saida/corrigir: audit POST-commit | UoW for stock; audit own conn | `DapperArmazemRepository.cs:433-455`; `ArmazemService.cs:76-79,120-123,175-178,310-312` (FA-10) |
| Peso mutations: audit POST-commit; approve = control UoW + audit conn + day-approval upsert (3 tx) | separate connections | `PesoService.cs:457-458,466-482,497-498,526-527` (PC-09) |
| Repair add/remove item, disponibilizar, repairer CRUD: audit POST-commit or own-conn | item+audit separate | `ReparacaoExternaService.cs:128-131,189-192,222-223,282-283,345-346,440,469,483,509` (RE-02) |
| Pegamentos: NO UoW at all; read-then-write across independent connections | TOCTOU (measurements on closed control; double document confirm) | `PegamentoService.cs:187-202,234-280`; repo own-conn pattern (PG-04) |
| Tampões planear: plano persist + audit separate tx | dormant surface | `TampaoService.cs:489-492` + `DapperTampaoRepository.cs:298-311` (TP-04) |
| BQ repairer writes: no UoW, no audit (contradicts service contract) | single-command each | `BoquilhasService.cs:624-691` (BQ-13) |
| `ConfirmReturnAsync` status recompute across connections | new-conn read inside UoW scope | RE-01 (PC-08) |

**TX verdict:** the infrastructure supports atomic multi-write correctly and
the refactor closed the highest-impact gaps (create-exit, repairer types,
JobOn lifecycle). The residual risk is concentrated in **audit emission
after commit** (crash → write without audit) and in **Pegamentos having no
transactional workflow at all**. D-13 (audit co-transactionality policy) is
only partially implemented; JobOn/image/repair-create/bootstrap/BOQ/Tampões/
RI are co-transactional; Admin/Armazém/Ferramentas/Peso/repair-add-item are
not.

---

## 13. RLS / Security Findings

- **RLS-01 (CONFIRMED, positive): chain-side parity complete.** Every app
  table gets RLS enable + `ba_dmo_app` policy + anon/authenticated denial:
  N12 (48+`schema_migrations` no-policy), N25 §2 (10 late tables), N27
  (junction inline), N29 (`article_reference_images` inline), N31
  (`access_template_profiles` inline). 62 RLS-enabled tables, 61 app policies.
- **RLS-02 (CONFIRMED): policy naming drift** — `ba_dmo_app_access` on N12/
  N25/N29 vs `internal_user_access_templates_app_access` (N27:137-144) and
  `access_template_profiles_app_access` (N31:115-120); identical semantics
  (D-15 not implemented). Cosmetic/tooling.
- **RLS-03 (CONFIRMED): consolidated baseline security gaps** — CB-02
  (`article_reference_images` un-RLSed on consolidated builds) and CB-04
  (pre-N33 posture; junction still granted to `ba_dmo_app` in the baseline).
  A consolidated-built DB diverges materially in access posture.
- **RLS-04 (CONFIRMED): N33 privilege surgery is coherent** — column-level
  grants on `internal_users` exclude `profile_title` but include
  `modules_override`; junction fully revoked. Runtime code never needs
  broader grants (verified identity query reads only granted columns).
- **RLS-05 (CONFIRMED): no per-user/module RLS policies** — functional
  authorization is C#-side (GLM-DATA-06.3); RLS is technical-scope only. No
  table introduced post-N12 lacks the technical treatment. `schema_migrations`
  intentionally policy-less.
- **RLS-06 (test-side, HIGH CONFIDENCE): `RepairAtomicityTests.cs:244`
  teardown `DELETE FROM audit_events` conflicts with the N01 append-only
  trigger** on a real PG (`trg_audit_events_append_only` N01:134-138) — the
  only UPDATE/DELETE of `audit_events` anywhere in the tree. LIVE
  VERIFICATION REQUIRED (may only bite when those PG-gated tests run with
  `BA_DMO_TEST_DATABASE`).

---

## 14. Index Findings

Evidence-driven (each gap shows the query that would benefit; no speculative
indexes):

| Query pattern | Supporting index | Verdict |
|---|---|---|
| BQ history filtered by `noted_repairer_id` (`DapperBoquilhasRepository.cs:279`) | **none** (N18 adds column only) | **GAP (BQ-16)** — sequential scan per trace |
| História module+time+order (`DapperHistoriaRepository.cs:186-187,239-249`; Admin `QueryAuditAsync`) | `ix_audit_events_module_time` (N25 PERF-01) + `_occurred_at` + `_module_action` | PRESENT |
| História group-keys `GROUP BY entity_type,entity_id ... MAX(occurred)` (:67-82) and `entity_type\|\|'|'\|\|entity_id = ANY(...)` (:107) | `ix_audit_events_entity` (N01) does not cover the concatenation expression | **CANDIDATE — NEEDS EXPLAIN on populated table (HS-10)** |
| História free-text `ILIKE` over label/entity/actor/action (:192-199) | none usable for leading-wildcard | NEEDS PERFORMANCE TEST (accepted at current scale) |
| JobOn calendar `machine_code + status + planned_start_at` (`GetActiveAsync`) | `ix_job_on_machine_planned` (N05) | PRESENT |
| Peso previous-approved `(status, control_date) ORDER BY` | `ix_peso_controlos_status_date` (N06) | PRESENT |
| Peso per-control leituras (N+1) | `uq_peso_leituras_controlo_cm` doubles as index | PRESENT; NEEDS PERFORMANCE TEST |
| BQ movements by trace / occurred | `ix_bq_movements_trace`, `_occurred` (N03) | PRESENT |
| Warehouse movements by stock / occurred | `ix_warehouse_movements_stock`, `_occurred` (N09) | PRESENT |
| Active occupation lookups | partial `uq_warehouse_stock_active_occupation` + `ix_*` (N09) | PRESENT (1:1-per-position not enforced — §10 ON-04) |
| `ix_pegamento_documentos_controlo` (N14:20-21) | duplicates the UNIQUE column index | REDUNDANT (informational) |

No other speculative index recommendations.

---

## 15. Apparent Duplication Analysis

Pairs/groups flagged as "repeated structures" and resolved:

**DUP-01 — Tampões configuration structures (the schema visualizer's Tampões cluster).**
- Object A: `tampao_configuration_machines` (current machine set); Object B: `tampao_configuration_machine_event` (append-only change stream); Object C: `tampao_configuration_notes`; Object D: `tampao_saldos`; Object E: `tampao_movements`.
- Same fact? **NO** — current-set vs events vs fact-history vs balances are complementary (A11/A10).
- Reason both exist: owner decision N21 (normalized N:M, never per-machine copies); append-only history trait.
- Current authority: machines table (current), movements (facts), saldos (balances).
- Safe to consolidate? **NO.** Evidence: TP-13 (CONFIRMED) — N21 normalized design; per-machine duplication would violate the owner decision.

**DUP-02 — Peso comparison twins.**
- Object A: `peso_comparacao_anterior` (table); Object B: live previous-approved query (`GetPreviousApprovedAsync`).
- Same fact? **YES (stored vs computed)**.
- Reason both exist: N06 declared a persisted read path; never implemented.
- Current authority: the query (PA-02).
- Safe to consolidate? **YES (remove table; D-9 REMOVE_LATER)** — evidence: zero SQL on the table.

**DUP-03 — occurrence twins.**
- Object A: `tool_check_occurrences` (N04); Object B: `job_on_verification_occurrence` (N05).
- Same fact? **PARTIAL** — same business concept (materialized verification checks), separate tables, different linkage (plain uuid vs component FK).
- Reason both exist: N04 defined Ferramentas-owned occurrences; the Job On materialization uses the N05 sibling; N04 never wired.
- Current authority: N05 table (actual writer).
- Safe to consolidate? **MAYBE (owner/product)** — one table should survive; needs owner decision (PA-01).

**DUP-04 — audit authorities.**
- Object A: `audit_events` (global); Object B: `job_on_audit_event`/`controlo_sheet_events`/`bq_lifecycle_history`/`tampao_configuration_*`/`repair_events`.
- Same fact? **NO** — global compliance projection vs domain event streams (different concepts, HS-01 conclusion).
- Safe to consolidate? **NO** — do NOT merge; the gap is the missing `audit_events` projection for JobOn/Pegamentos (PC-04/PC-05).

**DUP-05 — profile mirrors.**
- `access_template_profiles.functional_profile` vs `internal_users.profile_title`. Same fact? **YES (mirror, D-1)**. Resolved by N32/N33: authority = template profile; mirror quiesced; physical removal is N34's scope (unchanged).

**DUP-06 — user→template assignment.**
- `internal_users.template_id` vs `internal_user_access_templates`. Same fact? **YES (1:1 mirror since N31)**. Resolved by N32 (direct FK authority) + N33 (junction dead); removal = N34.

**DUP-07 — article image.**
- `article_reference_images` vs `job_on_revision.image_asset_id`. Same fact? **YES (mirror)**. Resolved by N29 (authority = reference table); column dormant (JA-15). Safe to consolidate: **YES later (D-11)**.

---

## 16. Legitimate Repetition

Evaluated semantically — NOT duplication:

- **Job On revision graph** (`job_on_revision`/`component`/`field`/`row` +
  verifications + audit_event + field_option): aggregate + immutable
  snapshots + materialized checks + data-driven options; append-only (D-5
  anchored). Preserve wholesale.
- **Tampões** (config / values / machines N:M / notes / events / saldos /
  movements / planos): N21 owner decision; legitimate current+history+
  configuration layering.
- **Ferramentas** (reference / lote / piece / check-rule / check-occurrence /
  usage): orthogonal identity+config+fact layers; BQ tool-type identity
  deliberately separate from Boquilhas domain (N04 BOUNDARY NOTE).
- **Armazém** (location / stock-with-release-history / movements): correct
  current+history split; partial unique expresses the intended constraint
  (weaker than 1:1-per-position — separate finding ON-04).
- **Repair** (repairers registry / capability join / line defaults): shared
  canonical registry (TD-15); `line_repairer_defaults` explicitly NOT
  capability (N20).
- **Boquilhas** (lote/trace/movement/discrepancy/lifecycle/utilisation):
  lifecycle-state vs trace-status duality is intentional (N03 comments).
- **Snapshot columns** (`*_snapshot` on controlo/pegamento/component/sheet/
  repair items; `repairer_snapshot`; `balances_before/after`): fact fidelity,
  never treated as live authority (A4).
- **Peso master/lot/control/reading/approval/config** and **Controlo
  sheets/items/events**: standardized layered patterns; `controlo_sheet_events`
  is the workflow audit trail by owner decision (N23).

---

## 17. Orphan Schema Candidates

Verified (no runtime owner in `src/`; DI/test evidence cited; classification
only — no removal decision):

| # | Object | Classification | Evidence |
|---|---|---|---|
| 17.1 | `peso_comparacao_anterior` (N06) | LEGACY_CANDIDATE (dead mirror; D-9 REMOVE_LATER) | zero SQL in src; only doc comments; live query is authority (PA-02) |
| 17.2 | `job_on_field_option` (N05) | FUTURE_FEATURE (D-7 keep-dormant) | zero code consumers (JA-14, HS-01: only domain record `JobOnVerifications.cs:43`) |
| 17.3 | `tampao_planos` (N10) + `TampaoService.PlanearAsync` family | FUTURE_FEATURE (D-8 keep-dormant; fully implemented, no surface) | no `/api/tampoes/plan*` routes; tests assert 404 (TampaoWebApiTests:78-83); `job_on_id`/`production_code` never written (TP-10) |
| 17.4 | `tool_check_occurrences` (N04) | LEGACY_CANDIDATE (schema-only; zero writers) | FA-01; only reader DapperFerramentasRepository:427-441; N04 CHECKs dead |
| 17.5 | `repair_events` (N08) | HISTORICAL/AUDIT (write-only today) | writers: DapperRepairRepository/RI; zero readers in src (RE-orphan) |
| 17.6 | `bq_traces.sap_end` (N03) | LEGACY/DEAD COLUMN | no writer (BQ-08) |
| 17.7 | `bq_discrepancies.resolved_by/resolved_at_utc` (N03) | LEGACY/DEAD COLUMNS | never written; resolve path NULLs them (BQ-04) |
| 17.8 | `internal_users.modules_override` (N26) | LEGACY (dormant; D-11 REMOVE_LATER) | projected but no runtime consumer; writer uncalled (ADM-16) |
| 17.9 | `job_on_revision.image_asset_id` (N05) | LEGACY (dormant; D-11 REMOVE_LATER) | live writers force null (JA-15) |
| 17.10 | `pegamento_controlos.nominal_average` (N07) | LEGACY (dormant) | zero src reads/writes (PG-10); authority = N16 nominals |
| 17.11 | `bq_movements.movement_type='fim'` + `BqCloseSnapshot` + `FinalCount` | LEGACY (BQ-10/BQ-17) | no producer for 'fim'; BqCloseSnapshot & FinalCount zero usages |
| 17.12 | `internal_user_access_templates` + `internal_users.profile_title` | LEGACY (quiesced; N34 scope) | zero src refs; physically present by design (N33) |

---

## 18. Legacy Code Candidates

Verified runtime-dead (interface + implementation exist; **no src callers**
outside tests/fakes; DI registration is wholesale — `Program.cs:140,172,174,…`
registers the repository type, so the methods are reachable but never called):

| # | Candidate | Location | src callers | Verdict |
|---|---|---|---|---|
| 18.1 | `SetUserModulesOverrideAsync` | `IAdminRepository.cs:92-97`; `DapperAdminRepository.cs:315-346` | none (fakes only) | ORPHAN (writes dormant column) |
| 18.2 | `CountActiveAdminsAsync` (public) | `IAdminRepository.cs:105-107`; `DapperAdminRepository.cs:381-394` | none (internal `CountActiveAdminsOnAsync` used) | ORPHAN |
| 18.3 | `ModuleCatalogMirrorSynchronizer.BuildSyncRows` | `ModuleCatalogMirrorSynchronizer.cs:33-41` | none (MergeForDisplay used) | ORPHAN |
| 18.4 | `CopyCheckRuleAsync` | `IFerramentasRepository.cs:38`; `DapperFerramentasRepository.cs:383-393` | none (dup flow inlines) | ORPHAN (FA-03/FA-08) |
| 18.5 | `GetOccurrencesByRuleAsync` + `FerramentasOccurrenceItem` | `IFerramentasRepository.cs:43`; `DapperFerramentasRepository.cs:427-441`; `FerramentasRequests.cs:112` | none | ORPHAN (with table 17.4) |
| 18.6 | `GetActiveStocksAsync`, `GetStockByToolIdAsync` | `IArmazemRepository.cs:21,23`; `DapperArmazemRepository.cs:127-140,157-170` | none | ORPHAN |
| 18.7 | `SubstituirAsync` + `SubstituirRequest` + `ReplaceOccupationAsync` | `ArmazemService.cs:128-180`; `ArmazemRequests.cs:23-28`; `IArmazemRepository.cs:31-36`; `DapperArmazemRepository.cs:259-295` | none (no route; live path = corrigir-localização) | ORPHAN + DUAL (PA-03) |
| 18.8 | JobOn dormant writes: `InsertImageMutationAsync`, `InsertRevisionAsync`, `GetRevisionsAsync`, `InsertComponentsAsync`, `InsertFieldsAsync`, `InsertRowsAsync`, `InsertVerificationsAsync`, `UpdateCurrentRevisionAsync` | `DapperJobOnRepository.cs:214-468,486-499,548-593,1002-1045`; `IJobOnRepository.cs` | none | DORMANT (JA-08) |
| 18.9 | `GetApprovedControlsForJobOnAsync`, `GetPreviousApprovedAsync` | `IPesoRepository.cs`; `DapperPesoRepository.cs:417-446` | none (previous-resolution via CreateComparisonRequest) | ORPHAN (PA-02) |
| 18.10 | `GetChainRootAsync` | `IReparacaoInternaRepository.cs`; `DapperReparacaoInternaRepository.cs` | none | ORPHAN |
| 18.11 | `VoidMovementAsync`/`ListVoidedMovementIdsAsync` | `IBoquilhasRepository.cs:71-74`; `DapperBoquilhasRepository.cs:325-342` | none (fakes only) | ORPHAN (BQ-09) |
| 18.12 | `GetMeasurementsAsync` (public port) | `IPegamentoRepository.cs:23`; `DapperPegamentoRepository.cs:304-319` | self-called by GetByIdAsync only | DORMANT surface (PG-12) |
| 18.13 | `GetLineRepairerDefaultAsync`, `ListMovementsByLoteAsync`, `CountLotesAsync`, `GetOpenDiscrepancyForTraceAsync` | `IBoquilhasRepository.cs`; `DapperBoquilhasRepository.cs:529-545,266-267,89-107,369-383` | none | ORPHAN (BQ-21) |
| 18.14 | `InternalRepairRules.EvalCollectibleWhen`/`NumberInContextLot` | `InternalRepairRules.cs:27,35` | none (rules inlined in service) | ORPHAN (code-level) |
| 18.15 | Domain artifacts: `BqCloseSnapshot`, `NavigationArea`, `ModuleKind.FunctionalArea`, `ControloSheetModuleCatalog.ComponentFamilies` (stale 3 vs runtime 5), `PesoModuleCatalog.ReportSubfolderMinLength`, `TampaoMovement.IsSingleBalance`, `TampaoBalanceKindCodec`, `PesoCmDecisionCodec` | Domain/Application catalogs | zero usages | ORPHAN/surface |
| 18.16 | `JobOnVerificationGenerator.Generate`, `JobOnRevision.CloneWithChanges/CreateImageRemovalRevision`, `JobOn.SaveRevision` | Domain JobOn | zero runtime callers | DORMANT |
| 18.17 | Repair: `RegisterReparacaoAsync` (singular), `RepairExitRules.HasUnknownLocation`, `RepairExit.ValidateNotAlreadyInOpenExit`, `RepairExit.IsOpen`, `Repairer.SupportedTypes` | ReparaçãoExterna domain | zero callers | ORPHAN surface |
| 18.18 | `SchemaMigrationRequiredException` 42703 gate (N26) | `DapperAdminRepository.cs:99-108,127-132`; `AdminUserService.cs:72-81,130-138` | live but vestigial (never fires on migrated DB; mislabels other undefined-column errors) | VESTIGIAL (ADM-11) |

Note: none of these are "dead because tests don't call them" — runtime
reachability was traced through Program.cs endpoints/DI (wholesale
registrations) and service call graphs.

---

## 19. Module-by-Module Results

### 19.1 Identity / Auth / Access / Admin

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | PASS |
| Dapper ↔ Models | PASS |
| Source of truth | CLEAR (D-1/D-2 authority chain; mirrors quiesced) |
| Security parity | PASS (N33 surgery coherent; column grants correct) |
| Overall risk | LOW (deploy-order caveat: N33 must precede first user write — ADM-14) |

Confirmed: zero mirror refs (ADM-01), single-template everywhere
(ADM-03), `TemplateProfileStore` deleted (ADM-15), bootstrap atomic
(ADM-12), ON CONFLICT targets valid (ADM-09). Findings: ADM-06 (create
race), ADM-07 (audit post-commit), ADM-08 (NULL summaries), ADM-11
(vestigial gate), ADM-16 (orphans), ADM-18 (bootstrap edge). No admin/auth
minimal API surface (Razor pages only).

### 19.2 Job On

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | PARTIAL (JA-04 select gap; dormant write surface; JA-17 DuplicateFrom identity semantics) |
| Dapper ↔ Models | PASS |
| Source of truth | REVIEW (production context ✓; História projection missing; production_folder no writer) |
| Security parity | PASS |
| Overall risk | MEDIUM (latent write-surface defects; unhandled 23505) |

Lifecycle fix CONFIRMED (JA-01). Article image authority clean (JA-12).
`jobon_user_current` correct (JA-13). Audit payloads cast+normalized
(JA-07). Dormant write family (JA-08/JA-09 — create+audit not atomic).

### 19.3 Peso

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | PARTIAL (jsonb binds uncast; `SELECT c.*`; Guid.Empty FK) |
| Dapper ↔ Models | PARTIAL (Revision never persisted — PESO-04) |
| Source of truth | REVIEW (approve = 3 tx; day_approvals write-only; comparacao_anterior dead) |
| Security parity | PASS |
| Overall risk | HIGH |

PC-01 peso (leituras rewrite of approved — D-10 open), PESO-04 Revision,
PESO-05 Guid.Empty, PESO-06 cm_snapshot/approval_log never populated,
PESO-09 non-atomic approve/audit/day-approval, PESO-10 write-only
day_approvals, PESO-11 in-place reference "revision" vs doc contract.

### 19.4 Pegamentos

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | FAIL (PC-01 create 23502; PC-02 one-sided) |
| Dapper ↔ Models | PARTIAL |
| Source of truth | REVIEW (no global audit; production_folder snapshot from live — PG-05) |
| Security parity | PASS |
| Overall risk | CRITICAL |

### 19.5 Controlo (Folha de Controlo)

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | PARTIAL (audit jsonb binds; no audit_events by design) |
| Dapper ↔ Models | PASS |
| Source of truth | CLEAR (revision-anchored; status/decision CHECK-aligned) |
| Security parity | PASS |
| Overall risk | MEDIUM |

Events append-only honored; atomic UoW; `ComponentFamilies` stale catalog
(orphan). Controlo is deliberately module-local in history (not a História
origin module).

### 19.6 Boquilhas

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | PARTIAL (audit binds; sap_end no writer; discrepancy calc) |
| Dapper ↔ Models | PARTIAL (void contract unimplemented docs) |
| Source of truth | REVIEW (balances derived-replay correct TODAY because void unreachable — BQ-09) |
| Security parity | PASS |
| Overall risk | HIGH |

### 19.7 Ferramentas

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | PARTIAL (occurrences no writer; status double-meaning; audit binds) |
| Dapper ↔ Models | PARTIAL |
| Source of truth | REVIEW (qty vs pieces never reconciled — FA-12; utilisation value_added unvalidated — FA-06) |
| Security parity | PASS |
| Overall risk | MEDIUM-HIGH |

### 19.8 Armazém

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | PASS (all live transitions locked; TOCTOU repair-return closed by 838afe8 — verified; Substituir dormant unlocked) |
| Dapper ↔ Models | PASS |
| Source of truth | CLEAR (occupancy current + append-only movements; "fora" derived) |
| Security parity | PASS |
| Overall risk | LOW-MEDIUM |

### 19.9 Reparação Externa

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | PARTIAL (RE-01 status machine; audit post-commit residual) |
| Dapper ↔ Models | PASS |
| Source of truth | REVIEW (exit status vs items facts; repairer snapshots) |
| Security parity | PASS |
| Overall risk | HIGH |

### 19.10 Reparação Interna

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | PARTIAL (uncast audit binds; orphan port GetChainRoot) |
| Dapper ↔ Models | PASS |
| Source of truth | CLEAR (CM/MF-only N28 everywhere; repeated numbers unconstrained; corrections new rows) |
| Security parity | PASS |
| Overall risk | MEDIUM |

### 19.11 Tampões

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | PARTIAL (TP-01/TP-02 balances fidelity; TP-05 audit binds) |
| Dapper ↔ Models | PASS |
| Source of truth | CLEAR (balances stored-with-history; config identity canonical) |
| Security parity | PASS |
| Overall risk | MEDIUM |

### 19.12 Audit / História / Shared settings

| Contract | Result |
|---|---|
| Migration ↔ Schema | PASS |
| Schema ↔ Dapper | PARTIAL (payload convention; JobOn/Pegamentos missing emitters) |
| Dapper ↔ Models | PASS |
| Source of truth | REVIEW (audit_events authority; JobOn gap; app_settings no writers) |
| Security parity | PASS (chain; baseline gaps CB-02/CB-04) |
| Overall risk | HIGH (audit completeness + settings dead) |

---

## 20. Prioritized Remediation Backlog

> Backlog only — implementation is out of scope for this audit. Each item
> cites its finding(s) and owner decision (existing D-* where one exists).

**P0 — blocks reaching correct behavior:**
1. **Fix Pegamentos create path** (`UpdatedAtUtc` fallback to `CreatedAtUtc`
   like `UpdateAsync`, or domain sets it) — PC-01 (CRITICAL).
2. **Resolve `contra_costura` nullability** (implement D-12: make column
   nullable + domain rule) — PC-02.
3. **Reconcile audit jsonb convention** across the 5 uncast emitters
   (apply `AuditJson.Normalize` + `::jsonb` casts; convert the ≥17
   free-text payload sites to JSON) — PC-03; extend `AuditJsonBindingTests`
   to cover Boquilhas/Tampões/Peso/Ferramentas/RI/Controlo.
4. **Fix `ConfirmReturnAsync` status recomputation** to read the just-written
   state within the UoW (in-tx read or pass the confirmed item) — PC-08/RE-01.

**P1 — close audit/history completeness:**
5. **Implement D-5 dual-emit** for Job On (`audit_events` projection in the
   same UoW; parity guard test) — PC-05/JA-06/HS-01.
6. **Add audit emission for Pegamentos** (or explicitly document module as
   non-observable) — PC-04/HS-02.
7. **Provide a writer/owner surface for `app_settings`**
   (`main_documents_output_root`) or document the manual seed — PC-07/HS-06.
8. **Make Pegamentos workflows transactional** (controlo+medicoes,
   confirm-document; and block measurements on closed controls) — PG-04.

**P2 — correctness/robustness (open owner decisions):**
9. **Guard approved Peso readings** (D-10: trigger on `peso_leituras` +
   service assertion) — PC-09.
10. **Persist `job_on.production_folder`** (writer in the Job On save flow;
    add column to `GetActiveAsync`/`GetByProductionCodeAsync` SELECTs) —
    PC-06/JA-04/JA-05.
11. **Fix Tampões `alterar_configuracao` balance/audit facts** — PC-13/TP-01/TP-02.
12. **Fix BQ discrepancy `expected_qty` + write `resolved_by/resolved_at_utc`** —
    PC-14/BQ-03/BQ-04.
13. **Map duplicate/unique violations to domain errors** (`job_on` identity,
    `tampao_configurations`, `bq_lotes`, `physical_pieces`) — ON-02/JA-03/TP-06/BQ-15.
14. **Make Ferramentas lot duplication atomic** and remove the stale doc claim
    — FA-03.
15. **Clarify `physical_pieces.status`** (add CHECK or split column) — FA-05.
16. **Decide occurrence-table consolidation** (`tool_check_occurrences` vs
    `job_on_verification_occurrence`) — PA-01/FA-01.
17. **Dispose of the dormant surfaces** per existing decisions: D-8
    (`tampao_planos`), D-9 (`peso_comparacao_anterior`), D-7
    (`job_on_field_option`), D-11 (`modules_override`, `image_asset_id`),
    Substituir/ReplaceOccupation, BQ void contract — §17/§18.
18. **Deploy-order hardening**: guarantee `migrate` (incl. N33) precedes the
    first user write (ADM-14); document in the deploy runbook.

**P3 — data/DDL hygiene:**
19. **Refresh `consolidated_clean_install.sql`** (D-16): add N31 objects +
    N29 RLS stanza + N33 posture + corrected header — CB-01..CB-05.
20. **Add `bq_movements.noted_repairer_id` index** (BQ-16).
21. **Consider per-location partial unique for warehouse 1:1** (D-14) and
    remove/keep `ReplaceOccupationAsync` accordingly — ON-04/FA-04/PA-03.
22. **Remove redundant `ix_pegamento_documentos_controlo`** (informational).
23. **Decide N28/N29/N30 BEGIN/COMMIT handling** (remove or tolerate) + add a
    real-PG migration execution test — MC-02 (carried).

---

## 21. Recommended Safe Remediation Order

Sequenced to minimize risk; each step is additive/code-first where possible
and follows the established migration discipline (forward-only migrations,
own transaction, fail-closed guards, parity checks):

1. **Code fixes, no schema (P0):** Pegamentos create (PC-01); repair-return
   status read (RE-01); audit payload JSON serialization + casts (PC-03);
   extend `AuditJsonBindingTests` to all emitters. Verify against
   `BA_DMO_TEST_DATABASE`.
2. **Code + DDL additive (P0/P1):** D-12 `contra_costura` nullable (new
   migration N34+); D-10 `peso_leituras` protection trigger; `job_on`
   production_folder writer + SELECT fixes; Tampões balances fix; BQ
   discrepancy fixes; app_settings owner surface or seed documentation.
3. **Audit completeness (P1):** D-5 dual-emit for Job On; Pegamentos audit
   emitters; parity guard tests.
4. **Convergence of dormant surfaces (P2, owner-gated):** execute the
   approved D-7/D-8/D-9/D-11 dispositions; enforce unique-violation mapping
   (ON-02); then — only after 03B/N33 parity — the **N34 destructive phase**
   (separate design, unchanged report).
5. **Baseline refresh (P3):** D-16 consolidated clean-install regeneration to
   the post-N34 final state + equivalence protocol.
6. **Index/perf (P3):** BQ `noted_repairer_id` index; EXPLAIN the História
   group query; optional warehouse 1:1 index after data check.

Ordering rule: **never mix schema changes with dormant-surface removals in
one migration; never drop a table/column without the row-count/parity guards
and the owner decision** (project convention, GLM-DATA-12; N27-N33 style).

---

## 22. Live Verification Still Required

No live database was reachable in this session. The following must be
verified against the real environment (`BA_DMO_DB_CONNECTION_STRING`,
`BA_DMO_TEST_DATABASE`, or the owner's read-only verifier on project
`bddfhbyrmchktqotpzgb`), listed in priority order:

1. **Deployed DDL drift** — confirm the live DB matches the N01–N33 expected
   schema: `access_template_profiles` present, `profile_title`
   nullable + no `ba_dmo_app` privileges, junction privilege-less, all 61
   tables RLS-enabled with the technical policy, and the state of
   `supabase_migrations.schema_migrations` vs the Npgsql runner record.
2. **PG-01/PG-02 probes** — create a pegamento control and a one-sided
   measurement against a migrated DB: expect 23502 on `updated_at_utc` and on
   `contra_costura` respectively; if they succeed, the deployed DDL differs
   from N07/N25 and must be re-audited.
3. **Audit jsonb probes** — exercise Tampões `/maquinas` + `/observacao`,
   Boquilhas close/reopen with a reason, Peso nao_aprovar/reabrir/
   documento.gerar, Ferramentas criar-lote/duplicar/regras, RI registrar —
   confirm 22P02/42804 or silent coercion, and confirm the Peso/Ferramentas
   post-commit 500 behavior; also run `AuditJsonBindingTests` +
   `RepairAtomicityTests` against `BA_DMO_TEST_DATABASE` (incl. the
   `DELETE FROM audit_events` teardown conflict RLS-06/HS-03).
4. **RE-01 probe** — return the final item of an exit via the API; confirm
   `repair_exits.status` stays `enviado`/`retorno_parcial` and never reaches
   `concluido` on that call.
5. **JA-04 probe** — confirm Dapper `dynamic` missing-column semantics for
   `production_folder` on the calendar/RI/Controlo paths (silent null vs
   exception); confirm whether any operational process writes
   `job_on.production_folder` at all (LIVE-5).
6. **HS-10** — `EXPLAIN` the História group/events/flat queries on populated
   `audit_events` to validate PERF-01 coverage.
7. **Application of N28/N29/N30 and N32/N33** — live execution of the
   transaction-carrying migration files (MC-02) and confirm the N32/N33
   fail-closed guards no-op on the chain-migrated DB (ADM-14 deploy order).
8. **Data-shape assertions** (read-only): `audit_events` row count per
   module_id incl. `jobon`/`pegamentos` (expect 0 — PC-04/PC-05),
   `app_settings` rows (expect 0 or manual seed — PC-07), column null-rates
   for `sap_end`, `resolved_by`, tool_usage(value_added), and any live
   divergence of `consolidated_clean_install.sql`-installed DBs.

---

## Audit validation checklist

- ✅ All 33 migrations read in full; effective schema constructed and counted
  (61 app tables + `schema_migrations` = 62).
- ✅ Consolidated baseline read in full and diffed against the chain final
  state (CB-01…CB-05 confirmed).
- ✅ Every Dapper repository/lookup/UoW factory reconciled (per-module
  contract blocks in §19 and in the module sub-reports).
- ✅ Every audit emitter enumerated (9 global + 2 module-local) and classified
  by payload validity / cast / transaction shape.
- ✅ Every ON CONFLICT arbiter verified against the chain (all valid).
- ✅ Source-of-truth classification completed for the named business facts
  (machine, reference, status, quantities, balances, configuration, access,
  profile, lifecycle, location, repair state, Job On).
- ✅ Legacy/orphan candidates verified by grep + DI/runtime trace (§17/§18).
- ✅ No source, migration, test, schema object, or database was modified;
  the only artifact produced is this report; the N34 audit report is
  unchanged.