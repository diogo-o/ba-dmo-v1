# Persistence Cross-Reference Audit

> READ-ONLY AUDIT — no source, migration, database object, or test was modified; no SQL was applied; no database writes were performed. This document is the only artifact produced by the audit task.
>
> Audit date: 2026 — session against repository baseline **main @ 847830824262bc42aadfc9a34d9c4d9bdc058baf**.
>
> Evidence policy: LIVE DATABASE + CURRENT MIGRATION CHAIN + CURRENT SOURCE CODE decide the factual state. The maps in `AI-CONTEXT/docs/Maps/` were used only as navigation aids; every claim below that touches persistence was verified against the migration files, the consolidated baseline, the migration runner, or the C# source. Where a live-database fact could not be verified from this session it is explicitly marked `UNVERIFIABLE_FROM_THIS_SESSION` and an owner verification is requested.

---

## 1. Executive Summary

The BA-DMO persistence architecture is a single, well-disciplined forward-only migration family (N01–N31) executed whole-script by a custom Npgsql runner, plus a consolidated clean-install baseline. The architecture is fundamentally sound: append-only tables are guarded by triggers and are not mutated by any Dapper code; RLS/policy/grants coverage extends to every application table in the migration chain; the migration bookkeeping mechanism (SHA-256 whole-script, record-after-success, fail-closed on mismatch) is cleanly designed; and the 61-application-table + `schema_migrations` count reconciles exactly with the previously observed live inventory of 62 base tables.

The audit nevertheless confirmed real drift and risk clusters that need owner decisions:

1. **Consolidated baseline drift (Phase 2) — confirmed and broader than the map's note.** `database/consolidated_clean_install.sql` does not contain the N31 objects at all (no `access_template_profiles`, no `ba_dmo_ensure_access_template_profile`, no `trg_access_templates_ensure_profile`, no `ux_internal_user_access_templates_actor`), its header still claims “N01 … N24”, and — new finding — it fails to emit the RLS/policy/grants stanza for `article_reference_images` (N29), so a database built from the consolidated file would have that table RLS-less and un-granted. N27/N28/N29 data-reconciliation DML is also omitted (no-op on empty databases, but the baseline is therefore **not** equivalence-proven).
2. **Job On lifecycle constraint conflict (Phase 5/8) — critical.** `ck_job_on_lifecycle_consistent` (N25) requires `status='fechado' ⟺ closed_at_utc NOT NULL` and the same for `cancelado`, but the only lifecycle write path, `DapperJobOnRepository.UpdateLifecycleStateAsync`, writes `status` alone and `closed_at_utc`/`canceled_at_utc` are never persisted anywhere. Any `fechado`/`cancelado` transition that reaches the database raises 23514. The partial-unique `uq_job_on_identity` (`WHERE canceled_at_utc IS NULL`) can therefore never release identity. These paths have no Web route/UI today (the entire Job On write surface — create/duplicate/save-revision/transition — is unexposed), so the defect is latent, but it is a genuine DB-contradicts-application condition the N25 remediation introduced.
3. **Multi-template plumbing contradicts the N31 single-assignment schema (Phase 5/8) — high.** `DapperAdminRepository.ReplaceUserAccessTemplatesAsync` inserts **all** submitted template ids into `internal_user_access_templates`, while N31 added unique index `ux_internal_user_access_templates_actor (actor_id)`. `AdminUserService.ChangeTemplatesAsync`/`CreateUserAsync` still accept multiple template ids → a two-template assignment raises an unhandled 23505, and `IdentityResolutionService` fails closed with `ACCESS_TEMPLATE_AMBIGUOUS` for >1 active template. The multi-template model is a legacy remainder of N27 that N31 superseded.
4. **Data-authority fragmentation around the functional profile (Phase 6/16).** `access_template_profiles` (N31, template-owned profile) is written only from the **Web layer** (`Pages/Admin/TemplateProfileStore.cs` issues raw SQL: upsert + `UPDATE internal_users SET profile_title …`), while `AdminUserService.UpdateUserAsync` writes `internal_users.profile_title` directly from the user form, and the N31 trigger/backfill write the same two columns. Three writers of one mirrored fact; no single owner.
5. **Raw non-JSON strings are bound into `jsonb` columns in several write paths (Phase 5).** Audit `before_snapshot`/`after_snapshot` payloads built from `Guid.ToString()`/`enum.ToString()` (`DapperJobOnRepository.DuplicateAtomicallyAsync`, `JobOnService.TransitionAsync`) are not valid JSON and will fail jsonb assignment (22P02) if those paths ever run; sibling repositories cast explicitly (`CAST(@BeforeSnapshot AS jsonb)`, `::jsonb`), showing the convention is known.
6. **Confirmed orphan/dead persistence surfaces (Phase 7/11/12).** `IJobOnRepository.InsertImageMutationAsync` (+ seven granular revision methods), `IAdminRepository.ChangeUserTemplateAsync`/`SetUserModulesOverrideAsync`, `IFerramentasRepository.CopyCheckRuleAsync`, `IPesoRepository.GetApprovedControlsForJobOnAsync`/`GetPreviousApprovedAsync` (plus the entire `peso_comparacao_anterior` table, which no code reads or writes), `TampaoService.PlanearAsync` + `tampao_planos` CRUD (no API/UI), `ArmazemService.SubstituirAsync` + `SubstituirRequest` (no API/UI), `IBoquilhasRepository.VoidMovementAsync`/`ListVoidedMovementIdsAsync` (BQ void contract), `BqCloseSnapshot`, `NavigationArea`, `ModuleKind.FunctionalArea`, `ControloSheetModuleCatalog.ComponentFamilies`, `PesoModuleCatalog.ReportSubfolderMinLength`, `InternalRepairRules`, `internal_users.modules_override` (dormant but still projected by two SQL reads).
7. **Concurrency/atomicity gaps (Phase 10).** `SetRepairerRepairTypesAsync` (DELETE + N×INSERT, no transaction), `CreateExitAsync` (exit+items+audit, multi-command, no transaction), audit `audit_events` inserts issued after commit or on separate connections in several modules, and a missing `FOR UPDATE` on the Armazém return-reoccupation path (`ConfirmReturnAsync`), which the partial-unique `uq_warehouse_stock_active_occupation` does not protect (per-location, per-lot).
8. **Test coverage stops at the fake gateway (Phase 17).** No test executes any N01–N31 file or the real `NpgsqlMigrationScriptGateway` against PostgreSQL; the 61-table schema contract and `consolidated_clean_install.sql` equivalence are unasserted; the only live-PG suite is the env-guarded `RemediationGuardTests` (N25-era; class doc says N01–N25). Dapper write-path SQL is exercised only for the `DapperAdminRepository` projection.
9. **Provenance/bookkeeping is single-mechanism in this repository, but the live database's history cannot be verified from this session.** The engine is one: `schema_migrations` + runner. The prior live observation (“schema_migrations contained only N26 while N01–N25 objects exist; Supabase CLI history carried N27+”) would imply a second provenance system; it cannot be confirmed or refuted without live-DB access (no connection string in this environment) — flagged `UNVERIFIABLE_FROM_THIS_SESSION`, owner verification required.

Top owner decisions required (21 total, enumerated in §18): consolidated baseline refresh (N31 + `article_reference_images` security stanza + header), Job On lifecycle constraint reconciliation, single-template enforcement in administration flows, functional-profile write authority, jsonb binding convention, transaction shape of repair-write sequences, BQ void/planos/substituir/orphan surface disposition, index addition policy for `bq_movements.noted_repairer_id`, and live-DB provenance reconciliation.

---

## 2. Audit Baseline

- Repository: `D:\BA-DMO`; branch `main`; HEAD `847830824262bc42aadfc9a34d9c4d9bdc058baf` (working tree contains modified Maps under `AI-CONTEXT/docs/Maps/` — the refreshed map set; no other uncommitted changes to source/migrations).
- Target framework: `net10.0` (`Directory.Build.props:8`) — note: not .NET 8.
- Live database: **NOT REACHABLE from this session.** No `BA_DMO_DB_CONNECTION_STRING`, no `DATABASE_URL`, no local listener on 5432, and no DB dump/snapshot exists in the repository. The only live-PG evidence available in-repo is the env-guarded integration suite (`BA_DMO_TEST_DATABASE`) and prior map inventory text. Live-state claims are therefore either migration-derived or marked unverifiable.
- Canonical maps (navigation aid; not authority): `AI-CONTEXT/docs/Maps/00_INDEX.md … 20_WEB.md`; primary: `02_DATABASE.md`, `03_MIGRATIONS.md`, `04_DAPPER_INFRASTRUCTURE.md`, `05_TESTS.md`, `16_USERS_ACCESS.md`.

### 2.1 Evidence read in full (primary)

- `database/migrations/N01_identity.sql … N31_template_profiles_single_assignment.sql` — all 31 files (2,619 lines).
- `database/consolidated_clean_install.sql` — all 1,666 lines.
- Migration subsystem: `MigrationRunner.cs`, `NpgsqlMigrationScriptGateway.cs`, `MigrationDiscovery.cs`, `MigrationChecksum.cs`, `MigrationFile.cs`, `MigrationExceptions.cs`, `IMigrationScriptGateway.cs`; `src/BA.Dmo.Web/Cli/MigrateCommand.cs` (grep level), `Program.cs` (full).
- Persistence foundation: `Db.cs`, `DapperUnitOfWork.cs`, `DbConnectionFactory.cs` (grep), `DatabaseConnectionSettings.cs`, `PersistenceMappings.cs`, `DateTimeOffsetHandler.cs` (referenced), `IDbUnitOfWork.cs`, `ConcurrencyGuard.cs`, `PersistenceAuthorship.cs`, `SchemaMigrationRequiredException.cs`.
- Repositories fully or substantially read: `DapperJobOnRepository.cs` (1,318 lines), `DapperPesoRepository.cs`, `DapperPegamentoRepository.cs`, `DapperBoquilhasRepository.cs`, `DapperArmazemRepository.cs`, `DapperArmazemRepairMovementRepository.cs`, `DapperRepairRepository.cs`, `DapperReparacaoInternaRepository.cs`, `DapperTampaoRepository.cs`, `DapperAdminRepository.cs` (710 lines), `DapperInternalUserRepository.cs`, `DapperArticleReferenceImageRepository.cs`, `DapperJobOnUserContextRepository.cs`, three UoW factories; remaining lookups/repositories audited by delegated evidence (see §7.3).
- Application: `IAdminRepository.cs`, `AdminUserService.cs`, `AdminTemplateService.cs`, `IdentityResolutionService.cs`, `AccessTemplateGrantsParser.cs`, `JobOnService.cs`, `ArmazemService.cs`, `TampaoService.cs` (grep), `Pages/Admin/TemplateProfileStore.cs`, plus contract/domain grep coverage.
- Tests: `AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/{Migrations,Persistence,Integrity,Access,Cli}/*`, `IntegrationTestEnvironment.cs`, `RemediationGuardTests.cs` (392 lines), and unit-test sweep (grep-level).

### 2.2 Migration-derived final schema (count reconciliation)

| Category | Count | Derivation |
|---|---|---|
| Application tables (N01–N31) | **61** | N01:3 + N02:1 + N03:6 + N04:5 + N05:8 + N06:7 + N07:2 + N08:6 + N09:3 + N10:6 + N11:1 + N14:1 + N19:1 + N20:1 + N21:3 + N23:3 + N24:1 + N27:1 + N29:1 + N31:1 = 61 |
| Migration bookkeeping table | 1 | `schema_migrations` (runner-created; reproduced in consolidated baseline) |
| **Total live tables (if fully migrated)** | **62** | identical to the previously observed live inventory “62 base tables incl. schema_migrations” → **count reconciled, no unexplained discrepancy** |
| Functions | 3 | `ba_dmo_guard_append_only` (N01), `ba_dmo_guard_peso_approved` (N25), `ba_dmo_ensure_access_template_profile` (N31) |
| Triggers | 19 | N01:1, N03:3, N05:1, N07:1, N08:1, N09:1, N10:1, N19:1, N21:2, N23:1, N25:5, N31:1 |
| RLS-enabled tables (migration-derived) | 62 | N12:49 + N25:10 + N27:1 + N29:1 + N31:1 |
| `ba_dmo_app` policies (migration-derived) | 61 | N12:48 + N25:10 + N27:1 + N29:1 + N31:1 (all except `schema_migrations`) |

---

## 3. Migration Chain Findings

Chain: N01 role/identity/audit → N02 catalog → N03 BQ → N04 Ferramentas → N05 Job On → N06 Peso → N07 Pegamentos → N08 Repair → N09 Armazém → N10 Tampões → N11 settings → N12 RLS → N13/N14/N15/N16/N17 additive columns → N18 BQ repairer → N19 usage → N20 repairer types → N21 machines → N22 internal-repair context → N23 Controlo folha → N24 user-current → N25 remediation → N26 modules override → N27 access convergence → N28 CM/MF-only → N29 reference images → N30 index → N31 single-assignment profiles.

Obeyed conventions: idempotent (`IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`, `DROP POLICY IF EXISTS`, constraint guards via `pg_constraint`); forward-only; no destructive changes of legacy rows; late FKs added in-file after both sides exist (`fk_job_on_current_revision` N05, `fk_repair_events_internal_record` N08, `fk_internal_repair_records_revision` N22); functional-profile CHECK recreated under a guarded DROP (N27); append-only guard triggers on all fact tables.

### 3.1 Findings

**PA-MC-01 — MEDIUM — MIGRATION_DRIFT (documentation/transient DDL) — N22 vs N28 `ck_internal_repair_records_type`.**
- EVIDENCE: `database/migrations/N22_reparacao_interna_context.sql:23-34` (widen CHECK to `('CM','MF','BQ')`) and `N28_reparacao_interna_cm_mf_only.sql:26-35` (drop + `ADD … NOT VALID` + `VALIDATE` re-narrowing to `('CM','MF')`, fail-closed guard at 14-24).
- CURRENT STATE: on any fresh build, N22's widening is immediately reversed by N28; N22's header comment (“BQ is a THIRD recordable type”) is stale.
- EXPECTED/COMPETING STATE: N28 documents this as a deliberate convergence (not duplication) — accepted; the N22 comment is the residual drift.
- IMPACT: none on final schema; only source-of-truth confusion for future maintainers.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO (informational).

**PA-MC-02 — HIGH — TRANSACTION_RISK / UNSAFE MIGRATION ORDERING — explicit `BEGIN;…COMMIT;` inside the runner-owned per-script transaction (N28, N29, N30).**
- EVIDENCE: `N28_reparacao_interna_cm_mf_only.sql:12,37`, `N29_jobon_reference_images.sql:11,157`, `N30_jobon_reference_image_updated_by_index.sql:4,9`; `NpgsqlMigrationScriptGateway.ExecuteScriptAsync` (`NpgsqlMigrationScriptGateway.cs:71-90`) already wraps each script in `BeginTransactionAsync` → commit/rollback. The runner (`MigrationRunner.RunAsync`) sends the whole file as one command inside that transaction.
- CURRENT STATE: a `BEGIN` inside an open transaction is ignored (server warning); the script's `COMMIT` commits the **gateway's** transaction; the gateway's own follow-up `CommitAsync` can then fail with 25P01 “no transaction in progress” (or the inner COMMIT leaves subsequent statements outside any transaction if any were added later). Whether the three files currently succeed end-to-end cannot be proven without executing them against real PostgreSQL (no such test exists — see TEST-02/-16).
- EXPECTED/COMPETING STATE: either scripts contain no transaction-control statements (rely on the runner), or the gateway tolerates client-side COMMIT; an integration test of N28/N29/N30 against a disposable PG.
- IMPACT: the three latest DDL scripts may fail at deploy time depending on Npgsql/server interaction; any *future* script placing statements after an inner COMMIT would silently lose atomicity.
- CONFIDENCE: MEDIUM (behavior depends on Npgsql/simple-query semantics; unexecuted in tests).
- OWNER DECISION REQUIRED: YES.

**PA-MC-03 — LOW — MIGRATION_DRIFT — N25's remediation guard sets `internal_users.auth_user_id NOT NULL` and the N27 data conversions all assume rows exist; a fresh build is safe, an upgrade path is guarded.**
- EVIDENCE: `N25_remediation.sql:35-53` (fail-closed NULL guard before `SET NOT NULL`); `N27_access_convergence.sql:19-111` (profile inference, legacy-override templates, junction backfill, modules rewrite, `modules_override = NULL`).
- CURRENT STATE: all guarded/idempotent; N27 executes a data-rewrite on `access_templates.modules` (module-only normalization, `peso`/`pegamentos` → `controlo`) that is destructive-by-design for legacy rows (documented owner decision).
- EXPECTED/COMPETING STATE: as designed.
- IMPACT: none if applied in order by the runner.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO.

**PA-MC-04 — LOW — BOOKKEEPING DRIFT (in-file provenance) — N27 header lacks the `Authority:` trail.**
- EVIDENCE: `N27_access_convergence.sql:1-6` (no authority reference vs. the pattern in every other file; map 03_MIGRATIONS.md:779 flags this); N27's `internal_user_access_templates_app_access` policy name also diverges from the `ba_dmo_app_access` convention (see §16).
- CURRENT STATE: decision trail for profile inference / legacy-override / module rewrite is not documented in-file.
- IMPACT: provenance gap only.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO (doc-level).

**PA-MC-05 — LOW — MIGRATION_DRIFT (stale assumptions) — N31 depends on N27 junction state; consolidated baseline paths and the runner history do not guarantee N27's DML ran.**
- EVIDENCE: `N31_template_profiles_single_assignment.sql:75-97` (DELETE collapsing hybrid assignments, re-INSERT on `template_id`, unique index).
- CURRENT STATE: safe in the migration chain; unsafe only if N31 is applied to a database whose junction was populated by the consolidated baseline (which omits N27's DML — see §4).
- IMPACT: junction rows inconsistent with `internal_users.template_id` until next admin save.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES (ties to consolidated baseline refresh).

**PA-MC-06 — INFO — OK — no duplicate table/constraint/index creation, no contradictory constraint sets in final state.**
- EVIDENCE: full-chain read; every later ALTER is a documented additive/convergent change; `uq_*`, `ck_*`, `ix_*` names unique; per-migration counts in §2.2.
- IMPACT: none.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO.

---

## 4. Consolidated Baseline Drift

`database/consolidated_clean_install.sql` (1,666 lines) was compared independently against the N01–N31 final state. The map note (“does not contain N31 objects; header claims N01–N24”) is **confirmed**, and the divergence list is broader.

| # | Divergence | Evidence (file) | Classification |
|---|---|---|---|
| 1 | **N31 objects entirely absent**: no `access_template_profiles`, no `ba_dmo_ensure_access_template_profile()`, no `trg_access_templates_ensure_profile`, no `ux_internal_user_access_templates_actor`, no N31 profile backfill/sync DML, no N31 policy. | grep of `consolidated_clean_install.sql` → zero matches for `access_template_profiles`/`ba_dmo_ensure_access_template_profile`; N31 file has all of them | **CONSOLIDATED BASELINE DRIFT** (schema-level) |
| 2 | **`article_reference_images` (N29) is created with table+constraints+index but without RLS enable, policy, or `ba_dmo_app` grants.** The consolidated RLS section (N12 §1/§4 + N25 §2) covers only the 48+10 tables; `article_reference_images` is in neither list (`consolidated_clean_install.sql:452-470` vs RLS arrays at `:1229-1308`, `:1551-1562`). | verified by grep: only 3 `ENABLE ROW LEVEL SECURITY` blocks (N12, N25, N27) | **CONSOLIDATED BASELINE DRIFT — SECURITY** (live-DB from this file would have an RLS-disabled, ungranted table) |
| 3 | Header still claims “the full forward-only migration family N01 … N24 in order” and references a test `ShippedFreshBuildFamily_IsComplete_N01ThroughN24` (now `…_N01ThroughN31`); trailing comment says “includes N25-N27” while the body already contains N28 (static CHECK) and N29/N30 objects. | `:4-29`, `:1666` | **CONSOLIDATED BASELINE DRIFT** (header/bookkeeping) |
| 4 | N27's data-reconciliation DML omitted (profile inference UPDATE, `legacy-override-*` template materialization, junction backfill INSERTs, `access_templates.modules` rewrite, `modules_override = NULL`). | `consolidated_clean_install.sql:1618-1664` (junction schema + profile CHECK only) vs `N27_access_convergence.sql:19-111` | INTENTIONAL on empty installs (no rows to convert); **UNKNOWN** whether intended for parity — the file claims to “reproduce the exact final effective schema”, which the omitted DML does not affect on an empty DB |
| 5 | N29 fail-closed guards + legacy image promotion DML omitted (table/constraints only) and N30 index present. | `:450-470` vs `N29:31-137` | INTENTIONAL on empty installs (no legacy rows); parity UNKNOWN |
| 6 | N28 fail-closed guard + `NOT VALID/VALIDATE` omitted; only the static narrowed CHECK `('CM','MF')` is present. | `:836-856` vs `N28:14-35` | INTENTIONAL on empty installs |
| 7 | Guarded role/default-privilege/no-op adaptations for Supabase Hosted (role-existence guards around `ALTER DEFAULT PRIVILEGES`, role creation, grants, policy creation). | `:37-63`, `:1273-1319` | INTENTIONAL (documented Supabase compatibility) |
| 8 | Section numbering out of order (19/20/23/24 before 12) and mixed comment trails; `schema_migrations` reproduced at `:79-85`. | cosmetic | INTENTIONAL/INFO |

**PA-CB-01 — HIGH — CONSOLIDATED BASELINE DRIFT — N31 absent (verified, matches map).**
- EVIDENCE: item 1 above.
- CURRENT STATE: a clean install from the consolidated file yields a database without `access_template_profiles`/profile trigger/unique single-assignment index; runtime N31-dependent code (`TemplateProfileStore.ListAsync/GetAsync/UpsertAsync`) would hit 42P01 undefined-table errors (not silently masked — `TemplateProfileStore` only falls back on `DatabaseConnectionException`, `Pages/Admin/TemplateProfileStore.cs:60-64,90-96`), so the failure is loud, not silent.
- EXPECTED: consolidated file updated to N31 final state.
- IMPACT: fresh installs diverge from migration-driven installs; N31-dependent Admin template edit breaks.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-CB-02 — HIGH — CONSOLIDATED BASELINE DRIFT — `article_reference_images` missing RLS/policy/grants.**
- EVIDENCE: item 2 above.
- CURRENT STATE: RLS disabled, no `ba_dmo_app_access` policy, no explicit grants in the consolidated baseline for this table (migration N29 has all three).
- EXPECTED: consolidated baseline mirrors N29's security stanza.
- IMPACT: on a consolidated-built database the table is readable/writable via Supabase Data API by default privileges — a real (if environment-dependent) security divergence from the migration-built schema.
- CONFIDENCE: HIGH (grep-verified absence).
- OWNER DECISION REQUIRED: YES.

**PA-CB-03 — MEDIUM — CONSOLIDATED BASELINE DRIFT — stale header/equivalence claims.**
- EVIDENCE: item 3; also 03_MIGRATIONS.md:1210-1221.
- IMPACT: operators may believe the file is N01–N24-only (or N01–N27-only) and miss the N29/N31 gaps.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES (refresh header/claims with the refresh).

**PA-CB-04 — LOW — CONSOLIDATED BASELINE DRIFT (parity UNKNOWN) — omitted reconciliation DML (N27/N28/N29).**
- EVIDENCE: items 4–6.
- IMPACT: zero on an empty database; owners should decide whether the file should additionally carry the fail-closed guards for parity/self-checking on migrated-but-partial databases.
- CONFIDENCE: HIGH (behavior), UNKNOWN (intent).
- OWNER DECISION REQUIRED: YES.

---

## 5. Live Schema Cross-Reference

### 5.1 Availability

No live PostgreSQL/Supabase endpoint was reachable from this session (no `BA_DMO_DB_CONNECTION_STRING`/`DATABASE_URL`, no local 5432 listener, no dump artifact). Live-row classification below is therefore **migration-derived final state + repo evidence**, with live-DB verification deferred to the owner. The previously reported live inventory (62 base tables incl. `schema_migrations`) reconciles **exactly** with the migration-derived count (61 application tables + `schema_migrations` = 62) — the “discrepancy” reported by the map refresh is a counting-frame difference (application tables only vs. incl. bookkeeping), **not a real live/migration mismatch**: `MATCH` (subject to live verification).

### 5.2 Per-table reconciliation summary (61 application tables)

| Classification | Count | Notes |
|---|---|---|
| MATCH (migration-derived, no evidence of drift) | 61 | every table's final structure = union of N01–N31 DDL; Dapper/application column usage verified per module (§7). Live verification pending |
| SCHEMA_DRIFT (migration chain vs consolidated baseline) | ≠ 0 | `access_template_profiles` (absent from consolidated), `article_reference_images` (RLS stanza absent from consolidated) — see §4 |
| ORPHAN_OBJECT (migration vs code) | 1 | `peso_comparacao_anterior` (N06-created; **no code reads or writes it** — DAP-PESO-01) |
| MISSING_OBJECT | 0 | all migration objects are represented in the runner chain; consolidated is the only gap carrier |
| NEEDS_REVIEW | 4 | `access_template_profiles` (writer fragmentation, §16/§6), `internal_users.modules_override` + `job_on_revision.image_asset_id` (dormant columns, §11), `tampao_planos` (fully-implemented, no surface, §11) |

Table-by-table column/type/PK/FK/unique/check/index/trigger verification was performed against the Dapper SQL during the Phase-5 audit; the complete per-method matrix (145+ methods) is summarized in §7.3 and the OK-verified rows are listed in §7.4.

**PA-LIVE-01 — MEDIUM — NEEDS_REVIEW — live-DB inventory cannot be machine-verified from this session; count reconciled but contents unverified.**
- EVIDENCE: environment lacks DB config; §2.2 count reconciliation; maps 02/03 document 61/62 frames.
- CURRENT STATE: expected 62 tables; actual live contents unknown.
- IMPACT: undetected live drift (missing N27+ objects, stale `schema_migrations`) would only surface at runtime; the app fail-closes on several (e.g. `SchemaMigrationRequiredException` for N26; `TemplateProfileStore` failure for N31).
- CONFIDENCE: HIGH (counts), UNVERIFIABLE (live contents).
- OWNER DECISION REQUIRED: YES (run the migrate CLI / inventory query against the live DB).

**PA-LIVE-02 — LOW — OK — legacy/dormant columns that stay readable.**
- EVIDENCE: `job_on_revision.image_asset_id` (dormant since N29, not dropped — `N29:6-8`), `internal_users.modules_override` (dormant since N27 but still projected — see PA-11-01).
- IMPACT: none today; they are intentionally preserved for auditability/compatibility.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO.

---

## 6. Migration Provenance / Bookkeeping

Mechanism (repo): `MigrationDiscovery` (regex `^(N\d{2})_[A-Za-z0-9_]+\.sql$`, ordinal order, duplicate-version rejection) → `MigrationRunner` (SHA-256 over raw file bytes; skip-if-same-checksum; **fail** on checksum mismatch; record-only-after-success; failure stops the run) → `NpgsqlMigrationScriptGateway` (whole-script single command inside one transaction via `NpgsqlCommand`; no parser/splitter — `MigrationArchitectureGuardTests` enforce this) → tracking in `schema_migrations (version PK, filename, sha256, applied_at, execution_time_ms)`. CLI verb `migrate` only (`MigrateCommand`); env connection `BA_DMO_DB_CONNECTION_STRING` → `DATABASE_URL` fallback; optional `BA_DMO_MIGRATIONS_DIR`.

Findings:

**PA-BK-01 — LOW — BOOKKEEPING DRIFT (unverifiable live) — `schema_migrations` contents vs. applied objects.**
- EVIDENCE: prior live observation (task brief): `schema_migrations` contained only N26 while N01–N25 objects existed; a separate Supabase CLI history carried N27+. No in-repo artifact can confirm or refute this; the runner design expects one record per applied file.
- CURRENT STATE: single mechanism by design; a live DB migrated through Supabase CLI (`supabase_migrations` history) plus the DMO runner would have two provenance systems.
- EXPECTED: one authoritative tracking table; reconcile live before next deploy.
- IMPACT: checksum-mismatch failures or silent re-runs if the histories disagree.
- CONFIDENCE: UNVERIFIABLE_FROM_THIS_SESSION.
- OWNER DECISION REQUIRED: YES (inspect the live DB).

**PA-BK-02 — INFO — OK — bookkeeping contract is sound.**
- EVIDENCE: `MigrationRunner.cs:39-96`, `MigrationChecksum.cs`, `MigrationExceptions.cs`, `schema_migrations` DDL in `NpgsqlMigrationScriptGateway.cs:13-22`.
- IMPACT/confidence: no action; HIGH.
- OWNER DECISION REQUIRED: NO.

**PA-BK-03 — LOW — BOOKKEEPING DRIFT — `MigrationFile`/docs still reference old family bounds.**
- EVIDENCE: `MigrationFile.cs:5` doc-comment mentions “N01_identity.sql … N12_rls.sql”; `MigrationDiscoveryTests` now asserts N01–N31; map 03 line 1162 confirms; `RemediationGuardTests` class doc says N01–N25 (`RemediationGuardTests.cs:14`).
- IMPACT: doc-level only.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO.

---

## 7. Dapper ↔ Schema Cross-Reference

### 7.1 Method-level audit (Phase 5)

Every Dapper component was enumerated from `04_DAPPER_INFRASTRUCTURE.md` and verified against source. All 30 components below were audited (complete per-method SQL → schema mapping for the four core repositories in §7.3; the remaining 26 files at finding level in §7.4), plus the 3 UoW factories and the `Db`/`DapperUnitOfWork` foundation.

Components audited: `DapperAdminRepository`, `DapperAppSettingsReader`, `DapperArmazemRepository`, `DapperArmazemRepairMovementRepository`, `DapperArticleReferenceImageRepository`, `DapperBoquilhasRepository`, `DapperControloProductionContextLookup`, `DapperControloSheetRepository`, `DapperFerramentasIdentityLookup`, `DapperFerramentasPieceLookup`, `DapperFerramentasRepository`, `DapperFerramentasRuleLookup`, `DapperHistoriaRepository`, `DapperInternalUserRepository`, `DapperJobOnActiveContextLookup`, `DapperJobOnProductionContextLookup`, `DapperJobOnProductionFolderResolver`, `DapperJobOnRepository`, `DapperJobOnUserContextRepository`, `DapperModuleCatalogMirrorRepository`, `DapperPegamentoRepository`, `DapperPesoRepository`, `DapperRepairRepository`, `DapperReparacaoInternaRepository`, `DapperTampaoRepository`, `DapperRepairUnitOfWorkFactory`, `DapperTampoesUnitOfWorkFactory`, `DapperBoquilhasUnitOfWorkFactory`, `DapperUnitOfWork`, plus Web-layer direct-SQL `TemplateProfileStore` (§8).

### 7.2 Findings — cross-cutting

**PA-DAP-01 — MEDIUM — CONSTRAINT_CONFLICT (type-binding convention) — raw C# strings bound to `jsonb` columns without explicit cast, inconsistently with sibling code.**
- EVIDENCE: `DapperPesoRepository.cs:233` and same pattern `DapperJobOnRepository.cs:224`, `DapperPegamentoRepository.cs:77`, `DapperBoquilhasRepository.cs:216` (no `::jsonb`); contrasted with explicit casts `DapperAdminRepository.cs:579` (`@BeforeSummary::jsonb`), `DapperInternalUserRepository.cs:43/51` (`@AdminGrantPattern::jsonb`), `DapperArticleReferenceImageRepository.cs:168` (`CAST(@BeforeSnapshot AS jsonb)`).
- CURRENT STATE: Npgsql's server-side parameter-type inference generally types untyped parameters from context, so INSERT **assignment** contexts work when content is valid JSON; the failure surface is (a) **non-JSON content** (PA-DAP-02) and (b) comparison contexts without assignment casts (e.g. `tampao_configurations WHERE values_json = @ValuesJson`, `DapperTampaoRepository.cs:145`).
- EXPECTED: one convention — either explicit jsonb casts everywhere or NpgsqlDbType=Jsonb binding; audit the two comparison sites.
- IMPACT: 22P02/42804-class runtime failures in write paths; currently latent because most callers pass serialized JSON.
- CONFIDENCE: MEDIUM (mechanism depends on Npgsql inference; not exercised by any test).
- OWNER DECISION REQUIRED: YES.

**PA-DAP-02 — HIGH — CONSTRAINT_CONFLICT — non-JSON strings written into jsonb audit columns.**
- EVIDENCE: `DapperJobOnRepository.cs:667-669` (`after_snapshot = sourceJobOnId.ToString()`, a bare GUID) and `JobOnService.cs:255-258` (`afterSnapshot = jobOn.LifecycleState.ToString()`, an enum name like `Fechado`); both feed `job_on_audit_event.before_snapshot/after_snapshot` (jsonb, `N05:198-202`).
- CURRENT STATE: `'Fechado'`/GUID text is not valid JSON; jsonb assignment parses and fails (22P02). The affected paths (`DuplicateAtomicallyAsync`, `TransitionAsync`) have no Web surface today (see PA-DAP-08), so the defect is latent but real.
- EXPECTED: serialize snapshots as JSON (`JsonSerializer`) or pass NULL; add cast.
- IMPACT: duplication and lifecycle transition would fail at the audit insert (duplication inside the same UoW would roll back cleanly; transition already wrote status on a separate connection → partial state).
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-DAP-03 — HIGH — CONSTRAINT_GAP (application/domain level) — `pegamento_medicoes.contra_costura NOT NULL` (N07:63) vs nullable domain value.**
- EVIDENCE: `DapperPegamentoRepository.cs:295` binds `ContraCostura ?? DBNull.Value`; `PegamentoControlo.cs:300` nullable; `PegamentoMeasurementCalculator.cs:20` supports one-sided measurements.
- CURRENT STATE: any one-sided measurement insert raises 23502 NOT NULL violation.
- EXPECTED: either DB nullable (with a domain-level rule) or domain requires both values.
- IMPACT: one-sided measurements (a stated domain capability) always fail.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-DAP-04 — MEDIUM — CONSTRAINT_GAP — `bq_traces.start_line NOT NULL` (N03:44) vs nullable/optional binding.**
- EVIDENCE: `DapperBoquilhasRepository.cs:214` (`StartLine ?? DBNull.Value`); `BqTrace.cs:80` nullable.
- CURRENT STATE: trace creation without a start line violates NOT NULL.
- IMPACT: trace-create 23502 on valid domain states.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO (align domain or column; low frequency).

**PA-JOBON-01 — CRITICAL — CONSTRAINT_CONFLICT — Job On lifecycle: `fechado`/`cancelado` transitions are impossible against `ck_job_on_lifecycle_consistent` because close/cancel timestamps are never persisted.**
- EVIDENCE: `DapperJobOnRepository.UpdateLifecycleStateAsync` writes `status` only (`DapperJobOnRepository.cs:183-196`); `closed_at_utc`/`canceled_at_utc` have no writer anywhere in `src/` (grep-verified; N05 defines the columns `:28-29`); N25's `ck_job_on_lifecycle_consistent` (`N25:70-82`) requires `(status='fechado') = (closed_at_utc IS NOT NULL)` and the same for `cancelado`; the only caller is `JobOnService.TransitionAsync` (`JobOnService.cs:255-258`).
- CURRENT STATE: any transition to `fechado`/`cancelado` (via `JobOnDomain.TransitionTo`, `JobOn.cs:148-168`) reaches `UPDATE … SET status` and raises 23514. The domain's dedicated `Close()`/`Cancel()` timestamping methods (`JobOn.cs:171-192`) are never invoked by the service.
- EXPECTED: persist the timestamps in the same command (and same transaction) as the status, or drop the CHECK.
- IMPACT: the terminal lifecycle states are unreachable at the database level; N25's own remediation contract is contradicted by the write path.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-JOBON-02 — HIGH — CONSTRAINT_CONFLICT — `uq_job_on_identity`'s cancellation exemption can never be satisfied, and duplicate non-canceled identities surface as raw 23505.**
- EVIDENCE: `N25:60-62` partial unique `(production_code, machine_code) WHERE canceled_at_utc IS NULL`; combined with PA-JOBON-01, `canceled_at_utc` never becomes non-NULL → canceled jobs permanently occupy their identity; plain `INSERT INTO job_on` (`DapperJobOnRepository.cs:29-38,851-860`) has no ON CONFLICT handling.
- EXPECTED: a reachable cancel path that records `canceled_at_utc` (unblocking re-issue) and/or explicit conflict handling for identity reuse.
- IMPACT: identity re-issue after cancellation is impossible; duplicate creation yields an unhandled unique-violation 500 instead of a domain error.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-DAP-05 — MEDIUM — DUPLICATE_AUTHORITY — `peso_comparacao_anterior` is a dead table; the previous-approved resolution lives in a live query.**
- EVIDENCE: `peso_comparacao_anterior` created in `N06:134-140`; docs describe it (`DapperPesoRepository.cs:14-16`); no INSERT/UPDATE/DELETE/SELECT on it exists anywhere (`DapperPesoRepository.GetPreviousApprovedAsync.cs:417-446` queries `peso_controlos` directly).
- CURRENT STATE: two declared sources for “previous approved control” — one materialized table (never written) and one computed query (used).
- EXPECTED: single authority; retire the unused table or materialize it.
- IMPACT: divergence risk if someone starts writing the table; dead DDL.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-DAP-06 — MEDIUM — CONSTRAINT_GAP / TRANSACTION_RISK — `peso_leituras` DELETE+re-INSERT on update; no immutability guard on readings of approved controls.**
- EVIDENCE: `DapperPesoRepository.cs:383-398` (DELETE ALL leituras + re-INSERT inside a UoW); N25 guards `peso_controlos` only (`ba_dmo_guard_peso_approved`, `N25:137-165`; `peso_leituras` has no append-only trigger).
- CURRENT STATE: an approved control's readings can be silently rewritten via `UpdateControlAsync` (only the controlos identity columns are guarded by the trigger).
- EXPECTED: either the trigger guards `peso_leituras` under approved parents, or the app prevents editing leituras of approved controls.
- IMPACT: history rewriting of approved readings (contrary to the immutability contract).
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-DAP-07 — MEDIUM — TRANSACTION_RISK — repair-write sequences are not atomic.**
- EVIDENCE: `DapperRepairRepository.SetRepairerRepairTypesAsync` (DELETE + N×INSERT without transaction, `DapperRepairRepository.cs:354-370`); `ReparacaoExternaService.CreateExitAsync` (exit + items + audit multi-command, `ReparacaoExternaService.cs:81-91`); audit inserts after commit / separate connections (`ReparacaoExternaService.cs:195-196,255-256`; `DapperArmazemRepository.cs:433-455`).
- CURRENT STATE: partial failure leaves half-written state (e.g. exit without items; repairer without types; movement without audit).
- EXPECTED: wrap in `DapperUnitOfWork.RunAsync` or the existing `IRepairUnitOfWorkFactory` scope.
- IMPACT: inconsistent repair/armazém/audit state under failures.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-DAP-08 — MEDIUM — READ/WRITE ASYMMETRY — the entire Job On **write** surface has no Web route/UI; only reads, image association, and current-context are wired.**
- EVIDENCE: `Program.cs` registers Job On endpoints only for image attach/replace/remove (`:291-338`), current context (`:343-364`), document (`:368-379`); `jobon.js` save is “presentational close” (`jobon.js:131-145`); `Pages/JobOn/Index.cshtml.cs:259-261` only calls `SetCurrentOpenAsync`. No endpoint/handler calls `CreateJobOnAsync`, `DuplicateJobOnAsync`, `SaveRevisionAsync`, `TransitionAsync`, `ConfirmVerificationAsync`.
- CURRENT STATE: `DapperJobOnRepository`'s ~1,000 lines of write SQL (SaveRevisionGraphAsync, DuplicateAtomicallyAsync, granular inserts, lifecycle) are exercised only by unit tests/fakes; the DB constraints N25 added for these flows (`ck_job_on_lifecycle_consistent`, `uq_job_on_identity`) are never exercised at runtime.
- EXPECTED: either wire the write endpoints (and fix PA-DAP-02/PA-JOBON-01) or explicitly scope Job On as read-now/write-later.
- IMPACT: latent breakages in the most complex schema area; divergence between the “designed” app and the shipped app.
- CONFIDENCE: HIGH (grep-verified).
- OWNER DECISION REQUIRED: YES.

**PA-DAP-09 — HIGH — CONSTRAINT_CONFLICT — multi-template assignment vs N31 single-assignment unique index (confirms DAP-ADM-01).**
- EVIDENCE: `DapperAdminRepository.cs:277-288` (`DELETE` all junction rows then `INSERT … FROM unnest(@TemplateIds)` for **all** ids) vs `N31:87-88` unique index `ux_internal_user_access_templates_actor (actor_id)`; callers `AdminUserService.cs:233-241,336-352` accept multiple ids; runtime fails closed in `IdentityResolutionService.cs:109-113`.
- CURRENT STATE: a two-template save → 23505 unhandled → 500; orphaning the “multi-template” capability that N31 removed.
- EXPECTED: enforce single-template at service/request level or drop the multi-id path entirely.
- IMPACT: admin user edit with >1 template breaks; developer confusion.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-DAP-10 — MEDIUM — TRANSACTION_RISK — Armazém return re-occupation has no lock; 1:1 position occupancy can race.**
- EVIDENCE: `DapperArmazemRepairMovementRepository.ConfirmReturnAsync` occupancy check (`:76-88`) lacks `FOR UPDATE` (while `DapperArmazemRepository.RegisterEntradaAsync` locks at `:185-213`); `uq_warehouse_stock_active_occupation` is per `(location, tool_lote)` (`N09:37-39`).
- CURRENT STATE: two concurrent returns of different tool lots to the same empty position both pass the check → two active rows on one position.
- EXPECTED: `SELECT … FOR UPDATE` on the position or a partial unique index keyed on `(warehouse_location_id)` for active rows.
- IMPACT: violation of the 1:1 occupation invariant under concurrency.
- CONFIDENCE: MEDIUM-HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-DAP-11 — MEDIUM — SCHEMA_DRIFT (fact fidelity) — `alterar_configuracao` movement serializes a truncated `balances_after`.**
- EVIDENCE: `TampaoService.cs:445` (`SerializeBalances(new TampaoSaldo{ Enchidos = newOrigin })`) → `por_encher` forced to 0 and the destination balance absent in the append-only `tampao_movements.balances_after`.
- CURRENT STATE: persisted movement fact does not reflect true before/after balances for `alterar_configuracao`.
- EXPECTED: serialize both origin and destination balances.
- IMPACT: history reads of Tampões movements are misleading.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-DAP-12 — LOW — DUPLICATE_AUTHORITY/DOC-IMPL DRIFT — `CreateConfigurationAsync` documented as “configuration + saldo” but only inserts the configuration.**
- EVIDENCE: `ITampaoRepository.cs:30-31` vs `DapperTampaoRepository.cs:199-210`; saldo created later by `TampaoService.cs:435-436` (`SetSaldoAsync`); `uq_tampao_configurations_values` (`N10:56`) can race on concurrent new destinations.
- CURRENT STATE: contract/impl mismatch; saldo row creation depends on caller choreography.
- IMPACT: maintenance risk; theoretical two-configuration/saldo split.
- CONFIDENCE: MEDIUM-HIGH.
- OWNER DECISION REQUIRED: NO (align contract or impl).

**PA-DAP-13 — MEDIUM — CONSTRAINT_GAP (registered via DAP-BQ-01) — BQ void plumbing exists with no producer/consumer; `deleted_movements` unread by balance math.**
- EVIDENCE: `IBoquilhasRepository.VoidMovementAsync/ListVoidedMovementIdsAsync` (`IBoquilhasRepository.cs:61`), `DapperBoquilhasRepository.cs:325,332` (write/read `bq_traces.deleted_movements`); movement listing (`:256-259`) is not filtered by `deleted_movements`; `bq_movements.movement_type` CHECK includes `'fim'` with no producer (`N03:78-79`).
- CURRENT STATE: dormant contract + dormant column; if wired, balances could include voided movements.
- IMPACT: latent correctness risk in the BQ balance flow.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-DAP-14 — LOW — SCHEMA_DRIFT (convention deviation) — `SELECT c.*` wildcards against business tables.**
- EVIDENCE: `DapperPesoRepository.cs:264,290,331` (`SELECT c.*, …`) vs the stated convention “no SELECT * against business tables; explicit column lists” (`Db.cs:22-26`, map 04).
- CURRENT STATE: `c.*` hydrates all `peso_controlos` columns dynamically; aliased columns (`m_mold`, etc.) create implicit coupling.
- IMPACT: adding a column to `peso_controlos` silently changes hydration; fragile.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO (convention).

### 7.3 Transaction and table matrix — core repositories (DAP evidence, verified)

**DapperJobOnRepository** — own-connection single-command for most methods; `DapperUnitOfWork.RunAsync` for `SaveRevisionGraphAsync`, `DuplicateAtomicallyAsync`, `InsertImageMutationAsync`; granular insert loops (`InsertComponentsAsync`, `InsertFieldsAsync`, `InsertRowsAsync`, `InsertVerificationsAsync`) are per-row single commands (non-atomic when used outside the graph methods — and they are currently uncalled). Reads: `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`; writes: `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`, `job_on_audit_event`.

**DapperPesoRepository** — `RunAsync` transactions for `CreateControlAsync` (controlo+leituras), `UpdateControlAsync` (UPDATE + leituras DELETE+INSERT), `DeleteControlAsync`; single-command everything else. Reads/writes: `peso_references`, `peso_lotes`, `peso_controlos`, `peso_leituras`, `peso_day_approvals`, `peso_settings`, `audit_events`. `peso_comparacao_anterior` never touched. `GetControlsAsync`/`GetControlByIdAsync` do per-row leituras N+1 reads (performance note, §14).

**DapperPegamentoRepository** — all single-command (no UoW); writes `pegamento_controlos`, `pegamento_medicoes`, `pegamento_documentos` (upsert), `audit_events` (service-side). Reads: same + N16/N17 columns (cm/bq/mf_nominal, notas) and N15 `tool_number` — all N-aligned.

**DapperBoquilhasRepository** — participation model: methods taking `IDbUnitOfWork` (lot+trace+movement+discrepancy+lifecycle+audit) run inside `BoquilhasService`'s `IBoquilhasUnitOfWorkFactory` scope; read methods open own connections. Writes: `bq_lotes`, `bq_traces`, `bq_movements`, `bq_lifecycle_history`, `bq_utilisation_readings`, `bq_discrepancies`, `repairers`, `line_repairer_defaults`, `audit_events`. `deleted_movements`/`reopen_history` jsonb updated via `||` JSONB concatenation (valid). `noted_repairer_id` (N18) read/written; filtered-by-repairer listing has no supporting index (PA-IDX-01).

Other files — transaction patterns and tables written (verified matrix, all 20 data classes + 3 UoW factories; paths relative to `src/BA.Dmo.Infrastructure/Access/`):

| File | Transaction pattern | Tables written |
|---|---|---|
| `DapperRepairRepository.cs` | single-table writes: implicit single-command; pickup/return coordinated writes: **caller-provided `IDbUnitOfWork`**; `SetRepairerRepairTypesAsync`: multi-command **no transaction** (PA-DAP-07) | `repair_exits`, `repair_exit_items`, `repairers`, `line_repairer_defaults`, `repairer_repair_types`, `repair_events`, `audit_events` |
| `DapperReparacaoInternaRepository.cs` | writes: **caller-provided `IDbUnitOfWork`**; reads: implicit single-command | `internal_repair_records`, `repair_events`, `audit_events` |
| `DapperArmazemRepository.cs` | multi-table writes: **`DapperUnitOfWork.RunAsync`** (incl. FOR UPDATE on entrada, `:185-213`); reads: implicit | `warehouse_locations`, `warehouse_stock`, `warehouse_movements`, `audit_events` |
| `DapperArmazemRepairMovementRepository.cs` | all writes: **caller-provided `IDbUnitOfWork`** (`:32,:68,:116`) | `warehouse_locations`, `warehouse_stock`, `warehouse_movements` |
| `DapperTampaoRepository.cs` | multi-row transforms: **caller-provided `IDbUnitOfWork`** (saldos FOR UPDATE, `:212-233`); single-row: implicit | `tampao_field_defs`, `tampao_field_values`, `tampao_configurations`, `tampao_saldos`, `tampao_movements`, `tampao_planos`, `tampao_configuration_machines`, `tampao_configuration_machine_event`, `tampao_configuration_notes`, `audit_events` |
| `DapperFerramentasRepository.cs` | mostly implicit; `CreateReferenceWithFirstLoteAsync`: **UoW-run** | `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_usage_records`, `audit_events` |
| `DapperFerramentasIdentityLookup.cs` / `DapperFerramentasPieceLookup.cs` / `DapperFerramentasRuleLookup.cs` | implicit (read-only) | — |
| `DapperControloSheetRepository.cs` | writes: **caller-provided `IDbUnitOfWork`**; reads: implicit | `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events` |
| `DapperControloProductionContextLookup.cs` / `DapperJobOnActiveContextLookup.cs` / `DapperJobOnProductionContextLookup.cs` / `DapperJobOnProductionFolderResolver.cs` | implicit (read-only) | — |
| `DapperJobOnUserContextRepository.cs` | implicit single-statement upsert (atomic) | `jobon_user_current` |
| `DapperArticleReferenceImageRepository.cs` | Set/Remove: **UoW-run**; Get: implicit | `article_reference_images`, `job_on_audit_event` |
| `DapperHistoriaRepository.cs` | implicit (read-only) | — |
| `DapperModuleCatalogMirrorRepository.cs` | `UpsertAllAsync`: **UoW-run**; Get: implicit | `module_catalog_mirror` |
| `DapperAppSettingsReader.cs` | implicit (read-only) | — |
| `DapperAdminRepository.cs` | most: implicit; `ReplaceUserAccessTemplatesAsync`/`SetUserActiveAsync`/`UpdateTemplateAsync`/`GuardedUserWriteAsync`: **UoW-run** | `internal_users`, `internal_user_access_templates`, `access_templates`, `audit_events` |
| `DapperRepairUnitOfWorkFactory.cs` / `DapperTampoesUnitOfWorkFactory.cs` / `DapperBoquilhasUnitOfWorkFactory.cs` | factory only — open `DapperUnitOfWork` (`DapperUnitOfWork.BeginAsync`), no SQL | — |

### 7.4 OK-verified rows (no drift)

DAP-AUDIT-01 (no UPDATE/DELETE on any append-only table anywhere in Infrastructure — case-insensitive grep across all 23 files), DAP-RI-01 (N22 context columns written; CM/MF-only), DAP-HIS-01 (História reads `audit_events` only; all columns exist), DAP-REP-04 (`repair_exit_items.status` writes ⊆ `('pendente','em_reparacao','devolvido')`), DAP-ARM-03 (warehouse release semantics + partial-unique index respected), DAP-TAM-03 (saldos two columns; atomic transforms with FOR UPDATE), DAP-CON-01 (controlo status/decision values align with CHECKs), DAP-JON-01 (`jobon_user_current` upsert on actor PK), DAP-IMG-01 (`article_reference_images` upsert/delete + audit atomic), DAP-LOOK-01 (lookups/resolvers read real columns), DAP-ADM-02 (admin/mirror/settings SQL aligned), DAP-PESO-04 (peso constraints met via service invariants + trigger backstop).

---

## 8. Domain Sources of Truth

Per-area authority (from the migration chain + repository/consumer wiring; write authority = repository class that persists; service-level dual writers are flagged):

| Domain area | Source of truth | Write authority | Read authority | History authority | Configuration authority |
|---|---|---|---|---|---|
| Identity / Users / Access | `internal_users` + `internal_user_access_templates` + `access_templates` (N01/N27/N31) | `DapperAdminRepository`, `DapperInternalUserRepository` (+ **Web-layer `TemplateProfileStore`** for profiles) | `DapperInternalUserRepository.FindByAuthUserIdAsync` | `audit_events` (module `admin`) | `access_templates.modules` + `access_template_profiles` |
| Job On | `job_on` + `job_on_revision` graph (N05) | `DapperJobOnRepository` (write surface unwired, PA-DAP-08) | same repo + 3 context lookups | `job_on_audit_event` (module-local) — **not** `audit_events` | `job_on_field_option` (+ `module_catalog_mirror` for UI) |
| Controlo (Folha) | `controlo_sheets`/items/events (N23) | `DapperControloSheetRepository` | same | `controlo_sheet_events` | N/A (snapshots from revision) |
| Peso | `peso_references`/`peso_lotes`/`peso_controlos` (N06) | `DapperPesoRepository` | same | `audit_events` + `approval_log` jsonb; previous-approved = **live query** (table unused, PA-DAP-05) | `peso_settings` |
| Pegamentos | `pegamento_controlos`/`medicoes`/`documentos` (N07/N14–N17) | `DapperPegamentoRepository` | same + `DapperJobOnProductionContextLookup` | `pegamento_medicoes` (append-only) | tolerance on control row (configurable data) |
| Ferramentas | `tool_references`/`tool_lotes`/`physical_pieces` (N04) | `DapperFerramentasRepository` | same + identity/piece/rule lookups | `tool_usage_records` (N19) + check-rule occurrences | (per-lot rules) |
| Boquilhas | `bq_lotes`/`bq_traces` (N03) | `DapperBoquilhasRepository` (UoW-factory) | same | `bq_movements`/`bq_lifecycle_history`/`bq_utilisation_readings` (append-only) | `line_repairer_defaults` + canonical `repairers` |
| Armazém | `warehouse_locations`/`warehouse_stock` (N09) | `DapperArmazemRepository` + `DapperArmazemRepairMovementRepository` (repair port) | same | `warehouse_movements` (append-only) | N/A |
| Reparação Externa | `repair_exits`/`repair_exit_items` (N08) | `DapperRepairRepository` (UoW-factory for pickup/return) | same | `repair_events` | `repairers`/`repairer_repair_types` (N20)/`line_repairer_defaults` |
| Reparação Interna | `internal_repair_records` (N08+N22) | `DapperReparacaoInternaRepository` (UoW) | same | corrections-as-new-rows (GLM-DATA-07) | context from Job On (snapshot) |
| Tampões | `tampao_configurations`/`tampao_saldos` (N10) | `DapperTampaoRepository` (UoW-factory) | same | `tampao_movements`/`tampao_configuration_notes`/`-machine_event` (append-only) | `tampao_field_defs`/`tampao_field_values` |
| Audit / História | `audit_events` (N01, canonical global) | every module's `InsertAuditEventAsync` (many post-commit, PA-DAP-07) | `DapperHistoriaRepository` | `audit_events` (append-only) | N/A |

**PA-DS-01 — MEDIUM — POTENTIAL_DUPLICATE_AUTHORITY — Job On has two audit authorities: module-local `job_on_audit_event` and global `audit_events`; Job On mutations never land in the global table.**
- EVIDENCE: Job On flows write `job_on_audit_event` only (e.g. `DapperJobOnRepository.cs:485-509`, `:887-911`); `DapperHistoriaRepository` reads `audit_events` only (DAP-HIS-01); no Job On flow writes `audit_events`.
- CURRENT STATE: História omits Job On activity; Job On history is invisible in the transversal history view.
- EXPECTED: single canonical history authority or a documented dual-emit contract (N25 D2 mentions “dual emit (code-side)” only for a different concern — INT-06).
- IMPACT: history completeness/authority ambiguity.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-DS-02 — MEDIUM — POTENTIAL_DUPLICATE_AUTHORITY — functional profile is mirrored in `access_template_profiles` (N31, template-owned) and `internal_users.profile_title`, with three writers.**
- EVIDENCE: writers — (1) N31 trigger + backfill (`N31:24-70,92-97`), (2) Web `TemplateProfileStore.UpsertAsync` (`Pages/Admin/TemplateProfileStore.cs:98-136` also syncs `profile_title`), (3) `AdminUserService.UpdateUserAsync` (`AdminUserService.cs:286-292` writes `profile_title` directly from the user form); readers — `IdentityResolutionService` (`:115-135`) uses `profile_title` and junction templates, not `access_template_profiles`.
- CURRENT STATE: a user-level profile edit can be overwritten by the template-level sync and vice versa; `access_template_profiles` is not consulted by runtime resolution at all.
- EXPECTED: single owner (template) with `profile_title` as a derived mirror, or resolution reads the profile table.
- IMPACT: divergent profiles between view and resolution.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

---

## 9. Duplicate Authority Candidates

| Candidate | Competing structures | Classification | Evidence |
|---|---|---|---|
| Previous-approved Peso comparison | `peso_comparacao_anterior` (table) vs live query over `peso_controlos` | POTENTIAL_DUPLICATE_AUTHORITY (table dead) | PA-DAP-05, N06:134-140 |
| Article image | `job_on_revision.image_asset_id` (dormant) vs `article_reference_images` (active) | LEGACY_CANDIDATE (resolved by N29) | PA-11-05, N29 |
| Per-user grants | `internal_users.modules_override` (dormant, N26) vs template grants (N27/N31 model) | LEGACY_CANDIDATE | PA-11-01/02 |
| Audit trail | `audit_events` vs `job_on_audit_event` | POTENTIAL_DUPLICATE_AUTHORITY (Job On side unlinked) | PA-DS-01 |
| Functional profile | `access_template_profiles` vs `internal_users.profile_title` | POTENTIAL_DUPLICATE_AUTHORITY (three writers) | PA-DS-02 |
| Component-family catalog | `ControloSheetModuleCatalog.ComponentFamilies` vs `job_on_component.family` CHECK/5-family SQL list | POTENTIAL_DUPLICATE_AUTHORITY (unused, diverges) | PA-11-12 |
| Job On identity uniqueness | `uq_job_on_identity` (N25) vs app check-then-insert | CONSTRAINT_CONFLICT (constraint unreachable-state) | PA-JOBON-01/02 |
| BQ repairer vocabulary | `repairers`/`repairer_repair_types`/`line_repairer_defaults` shared by Reparação Externa + Boquilhas | INTENTIONAL_NORMALIZATION (shared canonical registry) | N08/N18/N20 |
| Job On context for Pegamentos/Controlo/RI | revision-anchored snapshots vs live lookup | INTENTIONAL_NORMALIZATION (immutable anchor pattern) | N06/N07/N22/N23 comments |

---

## 10. Legitimate Normalized Structures

These were evaluated semantically, not visually, and are NOT duplication:

- `tampao_configurations` + `tampao_configuration_machines` + `tampao_configuration_machine_event` + `tampao_configuration_notes` + `tampao_saldos` + `tampao_movements` + `tampao_planos` — one configuration, N:M machines (never duplicated per machine; N21 owner decision), append-only notes/events, two balances, append-only movements, planning without reservation.
- `job_on` → `job_on_revision` → `job_on_component` → (`job_on_component_field`/`job_on_component_row`) + `job_on_verification_occurrence` + `job_on_audit_event` + `job_on_field_option` — aggregate + immutable snapshots + materialized checks + data-driven options.
- `access_templates` + `internal_user_access_templates` + `access_template_profiles` (N27/N31) — reusable template, junction (single-assignment since N31), profile-owned template.
- `tool_references` → `tool_lotes` → `physical_pieces` + `tool_check_rules` → `tool_check_occurrences` (and the N05 materialization `job_on_verification_occurrence`) — orthogonal identity/lot/piece/config-fact layers.
- `repairers` + `repairer_repair_types` + `line_repairer_defaults` — canonical registry + capability join + convenience default (explicitly not capability, N20).
- `warehouse_locations`/`warehouse_stock` (release-kept fact rows + partial unique active index)/`warehouse_movements` — position + occupancy 1:1 + append-only movement facts.
- `peso_references`/`peso_lotes`/`peso_controlos`/`peso_leituras`/`peso_day_approvals`/`peso_settings` — master/lot/control/reading/appoval/config layers with TD-26 “no duplicated attributes”.
- `bq_lotes`/`bq_traces`/`bq_movements`/`bq_discrepancies`/`bq_lifecycle_history`/`bq_utilisation_readings` — lot/trace/fact/exception/history/reading layers (lifecycle_state + trace-status duality is intentional: lot lifecycle vs production trace).
- `controlo_sheets`/`controlo_sheet_items`/`controlo_sheet_events` — current sheet + component snapshot + append-only event history (later Job On revisions must not reinterpret a submitted sheet).
- `audit_events` as the single global audit table (denormalized facts; no FK coupling) vs module-local `job_on_audit_event`/`controlo_sheet_events`/`bq_lifecycle_history`/`tampao_configuration_*` — module-local append-only event logs are context-specific event streams, not replacements of the global audit — EXCEPT the Job On case (§9/PA-DS-01) which is a genuine authority gap.

---

## 11. Legacy Candidates

Phase-7 candidates verified **independently** (callers via grep, DB dependency, test coverage, route/UI dependency). Classification only — no removal decision is made here.

| # | Candidate | Classification | DB dependency | Callers | Test coverage | Route/UI dependency | Evidence |
|---|---|---|---|---|---|---|---|
| 11.1 | `internal_users.modules_override` (N26) | LEGACY_CANDIDATE (dormant but still read) | column; NULLed for all rows by N27 (`N27:109-111`); N26-missing schema fails closed via 42703 → `SchemaMigrationRequiredException` (`SchemaMigrationRequiredException.cs:5`) | reads: `DapperAdminRepository.cs:38`, `DapperInternalUserRepository.cs:27`; writer `SetUserModulesOverrideAsync` has no callers; `AccessResolver`/`IdentityResolutionService` never consume it | `IdentityResolutionServiceTests.cs:192` (dormant), `DapperAdminRepositoryProjectionTests.cs:35-81`, `AdminUserServiceTests.cs:273-304` (schema-missing fail-closed) | Admin pages carry `ModulesOverrideJson` only as a model property; no override UI | PA-11-01 |
| 11.2 | `SetUserModulesOverrideAsync` (IAdminRepository/DapperAdminRepository) | ORPHAN_CANDIDATE | writes dormant column (guarded UPDATE, `DapperAdminRepository.cs:335-366`) | none in `src/` (fakes only: `FakeAdminRepository.cs:144`, `AdminWebAuthorizationTests.cs:355`, `AdminFormAntiforgeryTests.cs:441`, `AdminUserListResetTests.cs:313`, `HistoriaWebAuthorizationTests.cs:265`) | fake-level only | none | `IAdminRepository.cs:87-92` |
| 11.3 | `IJobOnRepository.InsertImageMutationAsync` | ORPHAN_CANDIDATE | writes `job_on_revision.image_asset_id` (dormant since N29; active flow = `article_reference_images`) in a UoW (`DapperJobOnRepository.cs:516-569`); stale comment `JobOnService.cs:325` | none; flows use `IArticleReferenceImageRepository` (`JobOnService.cs:362,419`) | fakes **throw** if called (`FakeJobOnRepository.cs:133`, `JobOnImageWebApiTests.cs:234`); N29 text assertions | none | `IJobOnRepository.cs:61-68` |
| 11.4 | `tampao_planos` / `TampaoService.PlanearAsync` | ORPHAN_CANDIDATE (complete implementation, zero surface) | `tampao_planos` (N10) + `DapperTampaoRepository` CRUD (`CreatePlanoAsync:289`, `GetPlanoByIdAsync:313`, `CancelPlanoAsync:328` UoW, `ListPlanosAsync:334`); audit `tampoes.planear`/`plano.cancelar` | `TampaoService.cs:465,496,515` → `ITampaoRepository.cs:59-62` | `TampaoServiceTests.cs:202-221`, `TampaoTestSupport.cs:204`, `TampaoWebApiTests.cs:216` — covered but unwired | none — no `/api/tampoes/plan*` route (`Program.cs:1281-1397`); leftover `#planosTable` CSS (`tampoes-layout.css:121`) | PA-11-04 |
| 11.5 | `ArmazemService.SubstituirAsync` + `SubstituirRequest` | ORPHAN_CANDIDATE | `warehouse_stock` release+insert, `warehouse_movements` out/in via `ReplaceOccupationAsync` (`ArmazemService.cs:128-180`) | service only; **no route** | `ArmazemServiceTests.cs:307-333` (service-level only) | none — no `POST /api/armazem/substituir` (`Program.cs:848-890`; `armazem.js` none) | PA-11-05, `ArmazemRequests.cs:23` |
| 11.6 | `CopyCheckRuleAsync` (IFerramentasRepository/DapperFerramentasRepository) | ORPHAN_CANDIDATE + POTENTIAL_DUPLICATE_AUTHORITY | `tool_check_rules.copied_from_rule_id` (N04) insert (`DapperFerramentasRepository.cs:383-414`) | none; lot duplication re-creates rules manually via `AddCheckRuleAsync` in `FerramentasService.CreateLoteFromBaseAsync` (`FerramentasService.cs:134-142`, `CopiedFromRuleId` in `ToolCheckRule.cs:81`) | fakes only, never invoked (`FakeFerramentasRepository.cs:127`, `FerramentasWebApiTests.cs:222`) | none | `IFerramentasRepository.cs:38` |
| 11.7 | `ChangeUserTemplateAsync` (IAdminRepository/DapperAdminRepository) | LEGACY_CANDIDATE (single-template shim) | delegates to `ReplaceUserAccessTemplatesAsync` (`DapperAdminRepository.cs:235-242`; updates `internal_users.template_id` + junction + lockout guard) | none (fakes in 5 test files; `AdminUserService` uses the plural path `AdminUserService.cs:350`) | fakes only (`AdminWebAuthorizationTests.cs:333`, `HistoriaWebAuthorizationTests.cs:259`, …) | none | `IAdminRepository.cs:49-54` |
| 11.8 | `NavigationArea` | LEGACY_CANDIDATE | none | zero construction sites in `src/` | `NavigationServiceTests.cs:49` (type-surface only); shell emits `NavigationTab` | none | `NavigationService.cs:22-28` |
| 11.9 | `ModuleKind.FunctionalArea` | LEGACY_CANDIDATE (unused enum value) | none | no usage; only `ModuleKind.Module` read (`AccessResolver.cs:166`) | `ModuleCatalogTests.cs:90` (enum-surface only) | none | `ModuleKind.cs:17` |
| 11.10 | `BqCloseSnapshot` | ORPHAN_CANDIDATE | none (no table; close persists only `status='closed'`) | none; `BoquilhasService.CloseTraceAsync:375-419` builds no snapshot | none | none | `BqLote.cs:35-60` |
| 11.11 | BQ void contract (`VoidMovementAsync`/`ListVoidedMovementIdsAsync`, `deleted_movements`, `movement_type 'fim'`) | ORPHAN_CANDIDATE | `bq_traces.deleted_movements` jsonb (`N03:48`); `ck_bq_movements_type` incl. `'fim'` (`N03:79`); Dapper impl `DapperBoquilhasRepository.cs:325,332` | no service/route/UI callers; port methods uncalled | `BqTestSupport.cs:192,195`; `FakeBoquilhasWebRepository.cs:130,131` | none (`boquilhas.js`/`Index.cshtml`: no anula/void UI) | PA-DAP-13; spec `50_BOQUILHAS_FUNCTIONAL.md:303-314` |
| 11.12 | `ControloSheetModuleCatalog.ComponentFamilies` | POTENTIAL_DUPLICATE_AUTHORITY | none directly; mirrors `job_on_component.family` (N05) | zero usages; authoritative 5-family list lives in the projection SQL (`DapperControloProductionContextLookup.cs:96`) | `ControloProjectionGuardTests` enforces the **5** families | none | `ControloSheetModuleCatalog.cs:23-24` |
| 11.13 | `PesoModuleCatalog.ReportSubfolderMinLength` | ORPHAN_CANDIDATE | none — no DB CHECK on `peso_lotes.report_subfolder` (`N06:45` plain `text NOT NULL`) | zero usages | none | none | `PesoModuleCatalog.cs:23` |
| 11.14 | `InternalRepairRules` | INTENTIONAL_NORMALIZATION + MIGRATION_DRIFT note | no DB objects of its own; DB-side equivalent = `internal_repair_records` CHECK `tool_type IN ('CM','MF')` (`N28:26-35`; N22:32-34 widened then re-narrowed) | no callers of `EvalCollectibleWhen`/`NumberInContextLot`; behavior inlined in `ReparacaoInternaService.cs:150-160,:291+` | `ReparacaoInternaWebApiTests` (CM/MF surface); no direct rule tests | `/reparacao-interna` page + API (registrar/corrigir/historico) — the **flow is live**; only the rules *class* is dead | `InternalRepairRules.cs:27,:35`; `InternalRepairToolType.cs:13-39` |

Dormant-column summary: `internal_users.modules_override` and `job_on_revision.image_asset_id` are physically present, intentionally preserved, but their only writers are orphan methods — recommending removal of the columns is **not** this audit's decision; an owner decision is requested (§18).

---

## 12. Orphan Candidates

- Tables: `peso_comparacao_anterior` (PA-DAP-05).
- Contract methods (no callers): `InsertImageMutationAsync` + `InsertRevisionAsync`/`GetRevisionsAsync`/`InsertComponentsAsync`/`InsertFieldsAsync`/`InsertRowsAsync`/`InsertVerificationsAsync`/`UpdateCurrentRevisionAsync` (IJobOnRepository), `ChangeUserTemplateAsync`/`SetUserModulesOverrideAsync` (IAdminRepository), `CopyCheckRuleAsync` (IFerramentasRepository), `GetActiveStocksAsync` (IArmazemRepository), `GetApprovedControlsForJobOnAsync`/`GetPreviousApprovedAsync` (IPesoRepository), `GetChainRootAsync` (IReparacaoInternaRepository — note `GetChainAsync` is used by history), `VoidMovementAsync`/`ListVoidedMovementIdsAsync` (IBoquilhasRepository).
- Service methods with no Web surface: `TampaoService.PlanearAsync` (+ planos CRUD), `ArmazemService.SubstituirAsync`, `JobOnService` write family (`CreateJobOnAsync`, `DuplicateJobOnAsync`, `SaveRevisionAsync`, `TransitionAsync`, `ConfirmVerificationAsync`), `AdminUserService.ChangeTemplateAsync` (public service method unused by Web — Web uses `SaveUserAsync`).
- Domain artifacts: `BqCloseSnapshot`, `InternalRepairRules`, `NavigationArea`, `ModuleKind.FunctionalArea`, `ControloSheetModuleCatalog.ComponentFamilies`, `PesoModuleCatalog.ReportSubfolderMinLength`.

All interface-level implementations are DI-registered (`Program.cs:140-275`) — no orphan *interfaces* exist; the orphans are method/artifact-level. Discovery evidence: grep across `src/` for each identifier; test-side fakes excluded.

---

## 13. Referential Integrity Findings

**PA-RI-01 — MEDIUM — MISSING UNIQUE/identity-release semantics — `uq_job_on_identity`'s exemption predicate is unreachable.**
- EVIDENCE: `N25:60-62` partial unique on `(production_code, machine_code) WHERE canceled_at_utc IS NULL`; no code path ever sets `canceled_at_utc` (PA-JOBON-01) — so canceled state cannot release identity, and duplicate non-canceled inserts raise unhandled 23505 (`DapperJobOnRepository.cs:29,851`).
- IMPACT: identity re-issue after cancellation is impossible; duplicate creation fails raw.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES (ties to PA-JOBON-01).

**PA-RI-02 — MEDIUM — ORPHAN RISK / MISSING FK — logical links without FKs are used inconsistently.**
- EVIDENCE: `repair_events.internal_repair_record_id` has an FK (added late, N08:142-151); `internal_repair_records.job_on_id` and `lot_id` are plain uuids (intentional, N22:42-50); `tool_check_occurrences.job_on_id/job_on_component_id` plain uuids (intentional, N04:104-110); `tampao_planos.job_on_id` plain uuid (N10:117); `job_on.article_reference_id` plain uuid (N05:21).
- CURRENT STATE: intentional contract-level coupling documented per table; no drift, but there is no DB backstop for orphans (e.g. `tampao_planos.job_on_id` pointing at a deleted/canceled job).
- IMPACT: accepted by design; flag for owner awareness only.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO.

**PA-RI-03 — INFO — OK — actor/user references are consistent.**
- EVIDENCE: all `*_by`/`actor_*` columns reference `internal_users(actor_id)` via nullable FK except: `access_templates.created_by` (plain text, N01:62 — no FK by design), `audit_events.actor_user_id` (plain text snapshot, N01:102), `internal_user_access_templates.assigned_by` (plain text, N27:12), `job_on.canceled_by`/`created_by` (FKs), `repairer` snapshot patterns (`repair_exits.repairer_snapshot` jsonb).
- IMPACT: none; mixed snapshot-vs-FK usage is the documented pattern.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO.

**PA-RI-04 — MEDIUM — CASCADING BEHAVIOR gaps.**
- EVIDENCE: `peso_leituras` and `peso_comparacao_anterior` cascade on controlo delete (N06:120,135) while `ba_dmo_guard_peso_approved` only blocks approved-row deletes; `controlo_sheet_items`/`controlo_sheet_events` cascade on sheet delete (N23:67,88) with no delete guard on sheets; `access_template_profiles` cascades on template delete (N31:14) but templates are deactivated, never deleted (UD-10).
- CURRENT STATE: cascade paths exist but are inconsistently guarded (peso approved → cannot delete via trigger; controlo sheet delete permitted; template profile cascades on a delete that shouldn't happen).
- IMPACT: risk of bulk-deleting history through cascades if misused; low today.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO (awareness; policy alignment later).

**PA-RI-05 — INFO — OK — the four revision-family anchors used by Peso/Pegamentos/Controlo/RI (immutable `job_on_revision_id`) are protected by the N25 append-only triggers (N25:187-209), and all four consumers pin correctly (N06:87, N07:32, N22:47, N23:32).**
- CONFIDENCE: HIGH. OWNER DECISION REQUIRED: NO.

---

## 14. Index / Query Findings

| Query pattern | Supporting schema | Classification |
|---|---|---|
| História module+time range: `audit_events` `(module_id, occurred_at_utc) ORDER BY occurred_at_utc DESC` (`DapperAdminRepository.QueryAuditAsync` `:661-691`, `DapperHistoriaRepository`) | `ix_audit_events_module_action (module_id, action_code)` + `ix_audit_events_occurred_at` + **`ix_audit_events_module_time (module_id, occurred_at_utc)` (N25 PERF-01)** | INDEX PRESENT |
| Audit actor/year filter (`actor_user_id, year`) | `ix_audit_events_actor` (N01) | INDEX PRESENT |
| Job On calendar per machine+time range: `WHERE machine_code = @ AND status IN … AND planned_start_at …` (`GetActiveAsync` `:109-144`) | `ix_job_on_machine_planned (machine_code, planned_start_at)` (N05) | INDEX PRESENT |
| Job On by production code (unique lookup; partial-unique identity) | `ix_job_on_production_code` (N05) + `uq_job_on_identity` (N25) | INDEX PRESENT (semantics of the partial unique are broken — PA-RI-01) |
| Revision graph load per job: `job_on_revision WHERE job_on_id ORDER BY revision_number` | `ix_job_on_revision_job_on` (N05); `uq_job_on_revision_number` backs the per-job ordering | INDEX PROBABLY ADEQUATE |
| `GetHistoricalProductionsAsync` correlated subqueries (`DapperJobOnRepository.cs:913-952`: `ORDER BY revision_number DESC LIMIT 1`, `MAX(revision_number)`) | same indexes | INDEX PROBABLY ADEQUATE (triple scan per row — NEEDS PERFORMANCE TEST at scale) |
| Peso previous-approved: `(mold, neckring, status='aprovado', production/date) ORDER BY control_date DESC LIMIT 1` (`:417-427`) | `uq_peso_controlos_identity` members + `ix_peso_controlos_status_date (status, control_date)` (N06) | INDEX PROBABLY ADEQUATE |
| Peso per-control leituras read (N+1 loop in `GetControlsAsync`) | `ix_peso_leituras_controlo_cm`? — `uq_peso_leituras_controlo_cm (peso_controlo_id, cm_number)` doubles as index | INDEX PRESENT; **NEEDS PERFORMANCE TEST** (N+1 round-trips) |
| Pegamentos search by production/machine/status | `ix_pegamento_controlos_production (production_code, machine_code)` (N07) | INDEX PRESENT (status filter unindexed — low cardinality) |
| BQ movements filtered by `noted_repairer_id` (N18 column) | **no index on `noted_repairer_id`** | **INDEX GAP — EVIDENCE** (PA-IDX-01 below) |
| BQ movements by trace/lote/date | `ix_bq_movements_trace`, `ix_bq_movements_occurred` (N03) | INDEX PRESENT |
| Warehouse history by stock/tool lot; list by occurred_at DESC LIMIT | `ix_warehouse_movements_stock`, `ix_warehouse_movements_occurred` (N09) | INDEX PRESENT |
| Repairer filter on exits; status/planned-date | `ix_repair_exits_status`, `ix_repair_exits_planned_date` (N08) | INDEX PRESENT |
| Controlo sheets by job/revision/production/status | `ix_controlo_sheets_*` (N23) | INDEX PRESENT |
| Tampões movements by origin config / date | `ix_tampao_movements_origin`, `ix_tampao_movements_occurred` (N10) | INDEX PRESENT |
| Active-stock resolution `(location OR tool_lote) WHERE released_at_utc IS NULL` | `uq_warehouse_stock_active_occupation` partial + `ix_warehouse_stock_location`, `ix_warehouse_stock_tool_lote` (N09) | INDEX PRESENT |
| Article-reference image by `reference_code` / `updated_by` | PK + `ix_article_reference_images_updated_by` (N30) | INDEX PRESENT |
| ILIKE `%term%` searches (admin users `:78-82`, ferramentas identity, peso references) | none usable for leading-wildcard | NEEDS PERFORMANCE TEST (accepted pattern at current scale) |

**PA-IDX-01 — LOW — INDEX GAP — EVIDENCE — `bq_movements.noted_repairer_id` filter has no supporting index.**
- EVIDENCE: `DapperBoquilhasRepository.cs:279` (`AND (@RepairerId IS NULL OR m.noted_repairer_id = @RepairerId)`); N18 adds the column without an index (`N18_bq_repairer.sql:15-16`).
- CURRENT STATE: repairer-filtered BQ history performs a sequential scan over the trace's movements.
- IMPACT: performance degradation as BQ history grows (high-frequency module).
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO (index addition is a remediation candidate, §19).

No speculative indexes are otherwise recommended.

---

## 15. Transaction / Concurrency Findings

Mechanisms: `DapperUnitOfWork`/`RunAsync` (one connection + one transaction; disposal rolls back), three UoW factories (Repair, Tampões, Boquilhas), caller-provided `IDbUnitOfWork` participants, implicit single-command, and — in several flows — **no visible transaction** (multi-command on a self-opened connection).

Multi-step flows with partial-success risk:

| Flow | Steps | Transaction | Risk |
|---|---|---|---|
| Admin user create (junction+user) | `DapperAdminRepository.CreateInternalUserAsync` CTE-insert (`:165-180`) | implicit single command | OK |
| Admin user template replace | UPDATE user + DELETE junction + INSERT junction + lockout count (`DapperAdminRepository.cs:257-295`) | `RunAsync` | OK; multi-template dead path (PA-DAP-09) |
| Bootstrap admin | template+user+junction+audit (`DapperInternalUserRepository.cs:173-210`) | `RunAsync` | OK |
| Job On save graph / duplicate / image mutation | multi-table | `RunAsync` | OK internally; write surface unwired (PA-DAP-08); audit payloads invalid JSON (PA-DAP-02) |
| Job On lifecycle transition | status UPDATE (conn A) + audit INSERT (conn B) (`JobOnService.cs:255-258`) | **two separate connections** | partial state on audit failure; plus constraint conflict (PA-JOBON-01) |
| Peso control update (delete+reinsert leituras) | `RunAsync` | OK atomic; approved-readings rewrite risk (PA-DAP-06) |
| Peso approved immutability | trigger `ba_dmo_guard_peso_approved` (N25) | DB-enforced | OK (identity only; non-identity updatable) |
| Ferramentas rule copy | `AddCheckRuleAsync` via `CreateLoteFromBaseAsync` (per-rule inserts) | single-command each | partial duplication on mid-loop failure (orphan `CopyCheckRuleAsync` was the intended atomic path) |
| Armazém entrada | check + lock + insert (`RegisterEntradaAsync` `:174-233`) | UoW + FOR UPDATE | OK (locked) |
| Armazém return re-occupation (repair) | check + insert (`ConfirmReturnAsync` `:67-114`) | UoW (repair scope) | **no FOR UPDATE** → 1:1 race (PA-DAP-10) |
| Reparação externa create exit | exit + items + audit (`ReparacaoExternaService.cs:81-91`) | **multi-command, no transaction** | partial exit (PA-DAP-07) |
| Repairer types replace | DELETE + N×INSERT (`DapperRepairRepository.cs:354-370`) | **no transaction** | partial capability sets (PA-DAP-07) |
| Boquilhas lot+trace+movement bundles | UoW-factory scope | OK |
| Tampões transforms | saldos FOR UPDATE + movement + audit (UoW) | OK (DAP-TAM-03); `balances_after` fidelity issue (PA-DAP-11) |
| Controlo sheet submit/decide | UoW where applicable (B-audited; status/decision CHECK-aligned DAP-CON-01) | OK |
| audit_events emissions | per-module `InsertAuditEventAsync` | mostly **post-commit / separate connection** (PA-DAP-07/DAP-REP-03) | audit gaps under failure |

**PA-TX-01 — MEDIUM — TRANSACTION_RISK — audit writes are not co-transactional with the business writes that produce them.**
- EVIDENCE: DAP-REP-03 evidence set plus `DapperArmazemRepository.cs:433-455`; pattern repeated across modules (each module opens its own connection for `audit_events`).
- IMPACT: an audit record can be lost or duplicated when the business write's transaction commits but the audit insert's connection fails (or vice versa).
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-TX-02 — INFO — OK — `DapperUnitOfWork` usage is disciplined where the schema demands atomicity (jobon graph, peso control, tampões, boquilhas, bootstrap, image association); no ambient TransactionScope; disposal rollback covered by `DapperUnitOfWorkTests` (11 tests, fake-backed).**
- CONFIDENCE: HIGH. OWNER DECISION REQUIRED: NO.

---

## 16. RLS / Security Findings

Migration coverage — every application table has RLS enabled + `ba_dmo_app` policy + anon/authenticated denial in the migration chain: N12 (48 tables), N25 (10 post-N12 tables), N27 (`internal_user_access_templates` inline), N29 (`article_reference_images` inline, role-guarded), N31 (`access_template_profiles` inline). `schema_migrations`: RLS on, no policy (migrate CLI only, documented). Grants: explicit `GRANT … TO ba_dmo_app` for DML + sequences (N12 §3, N25 §2, N27/N29/N31 inline), plus N01 `ALTER DEFAULT PRIVILEGES`.

**PA-RLS-01 — LOW — POLICY NAMING DRIFT (verified) — two conventions coexist; semantics are identical.**
- EVIDENCE: `ba_dmo_app_access` on all N12/N25 tables and N29's `article_reference_images`; `internal_user_access_templates_app_access` (N27:137-144) and `access_template_profiles_app_access` (N31:115-120). All are `FOR ALL TO ba_dmo_app USING(true) WITH CHECK(true)`.
- CURRENT STATE: naming inconsistency only — no semantic drift (the `{table}_app_access` policies have identical behavior). Map 03_MIGRATIONS.md:1103-1107 confirms.
- IMPACT: tooling/grepping for policies is unreliable; cosmetic.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: NO (or YES if a single convention is mandated — see §18).

**PA-RLS-02 — HIGH — CONSOLIDATED-BASELINE SECURITY GAP — `article_reference_images` loses RLS/policy/grants in the consolidated baseline.**
- EVIDENCE: PA-CB-02.
- IMPACT: consolidated-built databases expose the table without RLS and without grants (Supabase default-privilege exposure).
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-RLS-03 — INFO — OK — newly added tables (N27/N29/N31) carry their RLS/policy/grants inline; N25 covered the 10 pre-existing late tables; no table is left without a policy in the migration chain.**
- EVIDENCE: §2.2 counts (62 RLS-enabled, 61 policies).
- CONFIDENCE: HIGH. OWNER DECISION REQUIRED: NO.

---

## 17. Test Coverage Relevant to Persistence

Source of truth: `AI-CONTEXT/docs/tests/BA.Dmo.IntegrationTests/{Migrations,Persistence,Integrity,Access,Cli}` + `BA.Dmo.UnitTests` sweep; map 05_TESTS.md cross-checked (accurate; the one map/brief fact correction is the target framework **net10.0**, `Directory.Build.props:8`).

Well covered:
- Runner orchestration: `MigrationRunnerTests` (7: whole-script byte-for-byte, record-after-success, same-checksum skip, checksum-mismatch fail, failure-not-recorded-and-stops-run, canonical order, semicolons-in-strings never split) against `FakeMigrationGateway`; `MigrationDiscoveryTests` (11 incl. `ShippedFreshBuildFamily_IsComplete_N01ThroughN31` and static closure guards for N28/N29/N30/N31); `MigrationChecksumTests` (3); `MigrationArchitectureGuardTests` (2).
- `RemediationGuardTests` (10) — the **only** live-PostgreSQL suite (env var `BA_DMO_TEST_DATABASE`, runtime `[SKIP]` when unset): SQLSTATE probes 23505/23502/23514 for `auth_user_id` NOT NULL/UNIQUE, `uq_job_on_identity`, `uq_bq_traces_active`, peso approved-immutability, lifecycle/status CHECKs, append-only triggers on the revision family, RLS/policy/grants for the 10 late tables, `ix_audit_events_module_time`. Class doc still says “N01–N25” (stale — family is N01–N31).
- UoW lifecycle (11), connection-factory failure translation (10), mapping idempotency, architecture guards, N27/N31 access model at the **application** level (`IdentityResolutionServiceTests`: ambiguity fail-closed, dormant override, invalid profile; `AdminUserServiceTests` incl. schema-missing fail-closed; `AccessResolverTests` profile rules), and DB-constraint mirror tests at the domain level (repair status machine, peso workflow, jobon lifecycle, tampões balances, warehouse rules).
- `DapperAdminRepositoryProjectionTests` — the only test executing real Dapper + SQL through ADO.NET doubles (asserts the N27 junction projection `AS TemplateIds`, `AS ModulesOverrideJson`, `NULL::text AS AuthEmail`).

Structural gaps (full detail in TEST-01..16):

**PA-TST-01 — HIGH — GAP — the real `NpgsqlMigrationScriptGateway` (transaction per script, `schema_migrations` DDL, record INSERT) is never executed by any test; no migration file N01–N31 is ever run against PostgreSQL (TEST-01/TEST-02/TEST-05/TEST-16).**
- EVIDENCE: zero greps of `NpgsqlMigrationScriptGateway`/`EnsureTrackingTableSql` in tests; `MigrateCliTests` cover only failure paths; `FakeDbConnection.CreateCommand()` throws.
- IMPACT: broken migration DDL, the N28/N29/N30 explicit-BEGIN/COMMIT interaction (PA-MC-02), and gateway transaction semantics ship green.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-TST-02 — HIGH — GAP — no test asserts the final 61-table schema (catalog, constraints, RLS/policy/grants inventory); `consolidated_clean_install.sql` equivalence is untested (TEST-03/TEST-04).**
- EVIDENCE: only N25-scoped catalog probes exist in `RemediationGuardTests`; zero references to the consolidated file in tests (independently stale — §4).
- IMPACT: accidental drift (dropped constraint, missing RLS on a new table, consolidated divergence) is undetected.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-TST-03 — HIGH — GAP — N31 is only statically guarded; the profile trigger, backfill DML and single-assignment unique index are unproven at DB level (TEST-06/TEST-07/TEST-13).**
- EVIDENCE: `MigrationDiscoveryTests.N31_…` reads file text; `RemediationGuardTests` N01–N25 mandate.
- IMPACT: headline N27/N31 convergence contract regresses silently.
- CONFIDENCE: HIGH.
- OWNER DECISION REQUIRED: YES.

**PA-TST-04 — MEDIUM — GAP — almost all Dapper write-path SQL is unexercised; only the `DapperAdminRepository` projection has ADO.NET-double coverage; `DapperArticleReferenceImageRepository` has zero test references (TEST-08/TEST-09).**
- CONFIDENCE: HIGH. OWNER DECISION REQUIRED: YES.

**PA-TST-05 — MEDIUM — PARTIAL — `RemediationGuardTests` are runtime-skips without the env var; DB-level probes missing for tampão balances, warehouse partial-unique, N28 BQ rejection (TEST-10/-11/-12/-14/-15).**
- CONFIDENCE: HIGH. OWNER DECISION REQUIRED: YES (CI must guarantee `BA_DMO_TEST_DATABASE`).

Map 05_TESTS.md accuracy: verified accurate on counts/structure/gap records; corrections: net10.0 (not .NET 8), and the `RemediationGuardTests` N01–N25 doc-scope staleness is confirmed.

---

## 18. Owner Decisions Required

| # | Decision | Findings |
|---|---|---|
| 1 | Refresh `consolidated_clean_install.sql` to the N31 final state (add `access_template_profiles`, profile trigger/function, `ux_internal_user_access_templates_actor`, N31 DML) and refresh its header/equivalence claims | PA-CB-01/-03/-04 |
| 2 | Add the N29 security stanza (RLS/policy/grants) to the consolidated baseline | PA-CB-02, PA-RLS-02 |
| 3 | Reconcile Job On lifecycle: either persist `closed_at_utc`/`canceled_at_utc` when transitioning to `fechado`/`cancelado` (and audit valid JSON) or relax `ck_job_on_lifecycle_consistent`/`uq_job_on_identity` | PA-JOBON-01/-02, PA-DAP-02, PA-RI-01 |
| 4 | Decide whether Job On write flows (create/duplicate/save/transition) are in scope for the shipped Web app; if yes, wire endpoints and fix the jsonb/constraint defects; if no, mark the write repository surface explicitly dormant | PA-DAP-08, PA-JOBON-01/-02, PA-DAP-02 |
| 5 | Enforce single-template assignment in Admin flows (align `ReplaceUserAccessTemplatesAsync`/`ChangeTemplatesAsync`/`CreateUserAsync` with `ux_internal_user_access_templates_actor`) | PA-DAP-09 |
| 6 | Designate the single writer of the functional profile (`access_template_profiles` vs `internal_users.profile_title`; Web-layer `TemplateProfileStore` vs Application `AdminUserService`) | PA-DS-02 |
| 7 | Decide whether `peso_comparacao_anterior` is materialized (wire writes) or retired | PA-DAP-05 |
| 8 | Decide the jsonb binding convention (explicit `::jsonb`/`CAST` everywhere; fix comparison sites) | PA-DAP-01/-02 |
| 9 | Make repair-write sequences (repairer types, create exit, audit emission) atomic; decide audit co-transactionality policy | PA-DAP-07, PA-TX-01 |
| 10 | Add `FOR UPDATE` (or a per-location partial unique index) to the Armazém return re-occupation path | PA-DAP-10 |
| 11 | Fix `alterar_configuracao` `balances_after` serialization (origin+destination) | PA-DAP-11 |
| 12 | Decide disposition of orphan surfaces: `tampao_planos`/`PlanearAsync`, `SubstituirAsync`/`SubstituirRequest`, BQ void contract, `CopyCheckRuleAsync`, `InsertImageMutationAsync`, granular revision methods, `SetUserModulesOverrideAsync`, `ChangeUserTemplateAsync`, `GetActiveStocksAsync`, `GetApprovedControlsForJobOnAsync`/`GetPreviousApprovedAsync`, `GetChainRootAsync`, `BqCloseSnapshot`, `NavigationArea`, `ModuleKind.FunctionalArea`, `ControloSheetModuleCatalog.ComponentFamilies`, `PesoModuleCatalog.ReportSubfolderMinLength`, `InternalRepairRules`, dormant columns `modules_override`/`image_asset_id` | §11/§12 |
| 13 | Reconcile live-DB migration provenance (`schema_migrations` vs any Supabase CLI history) and refresh live inventory | PA-LIVE-01, PA-BK-01 |
| 14 | Decide N28/N29/N30 explicit `BEGIN;…COMMIT;` handling inside the runner transaction (remove or tolerate) and add a real-PG migration execution test | PA-MC-02, PA-TST-01 |
| 15 | Decide `pegamento_medicoes.contra_costura` nullability vs domain one-sided measurements | PA-DAP-03 |
| 16 | Guard `peso_leituras` of approved controls (trigger or app-level) | PA-DAP-06 |
| 17 | Decide BQ `noted_repairer_id` index (remediation candidate) | PA-IDX-01 |
| 18 | Decide `bq_traces.start_line` strictness vs optional binding | PA-DAP-04 |
| 19 | Decide single RLS policy naming convention | PA-RLS-01 |
| 20 | Guarantee `BA_DMO_TEST_DATABASE` in CI; extend live-PG coverage to N26–N31 and the 61-table contract | PA-TST-02/-03/-05 |
| 21 | Resolve Job On dual-audit authority (`job_on_audit_event` vs `audit_events`) — dual emit or dedicated contract | PA-DS-01 |

---

## 19. Remediation Candidates

Candidate list only — **no remediation plan, no implementation waves, no changes** are defined here (per task constraints).

1. Update `consolidated_clean_install.sql` to the N31 final state + N29 security stanza + corrected header/claims (DPDEL 1/2 above).
2. Add `ck`/`uq` reconciliation for Job On lifecycle: persist close/cancel timestamps + valid-JSON audit payloads in the same unit of work.
3. Single-template enforcement in Admin service/repository (junction write path).
4. Designate functional-profile write authority; move `TemplateProfileStore` SQL into the Application repository boundary.
5. Unify jsonb binding (explicit casts) across repositories; fix the audit snapshot serialization.
6. Make repair setup writes and audit emissions transactional.
7. Armazém return-occupation locking (FOR UPDATE or index) — decision-dependent.
8. `balances_after` origin+destination serialization for Tampões `alterar_configuracao`.
9. Orphan-surface disposition (wire, retire, or explicitly mark dormant) — §18 items 12.
10. `bq_movements(noted_repairer_id)` index — decision-dependent.
11. Real-PostgreSQL migration execution test (fresh-build N01–N31 + 61-table schema contract + consolidated equivalence).
12. Live-DB provenance and inventory reconciliation.

---

## Audit Validation Checklist

- ✅ Every N01–N31 migration inspected (all 31 files read in full).
- ✅ Consolidated baseline inspected (all 1,666 lines) and diffed against N01–N31 final state.
- ✅ Every live table reconciled at count level (61 application + `schema_migrations` = 62, matching the prior live inventory); live row-level verification pending (unverifiable from this session).
- ✅ Every Dapper repository/lookup/UoW reconciled (30 components + 3 UoW factories + foundation; per-method matrices in §7).
- ✅ Every Phase-7 legacy candidate verified (callers/DB/test/route).
- ✅ Migration bookkeeping reconciled (runner/discovery/checksum/gateway/schema_migrations + live-History caveat).
- ✅ No DB writes performed; no source/migration/test/database object modified; only `reports/persistence_cross_reference_audit.md` created.