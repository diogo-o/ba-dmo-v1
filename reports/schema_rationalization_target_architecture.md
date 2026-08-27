# SCHEMA-RAT-01 — Schema Rationalization: Target Persistence Architecture

> **Type:** READ-ONLY DESIGN / AUDIT — no source, migration, schema, or database object was modified; no DDL/DML executed; no Supabase writes; no commit. The only artifact produced by this task is this report.
>
> **Baseline:** `D:\BA-DMO`, branch `main`, HEAD `683765f6ea6ee7fbecd456d96b3c116ecaa08236` — "Fix Job On lifecycle persistence".
>
> **Evidence policy:** migrations (`database/migrations/N01…N31`), consolidated baseline (`database/consolidated_clean_install.sql`), current C# source (`src/BA.Dmo.Application`, `src/BA.Dmo.Domain`, `src/BA.Dmo.Infrastructure`, `src/BA.Dmo.Web`), the repository maps under `AI-CONTEXT/docs/Maps/` (navigation aid), the two prior persistence reports (`reports/persistence_cross_reference_audit.md`, `reports/persistence_high_impact_validation.md`), and tests under `AI-CONTEXT/docs/tests/` where they reveal persistence authority. Where a statement could not be verified against the current tree it is explicitly marked `UNVERIFIED` / `NEEDS OWNER CONFIRMATION`.
>
> **Live database:** **UNAVAILABLE from this session** — no `BA_DMO_DB_CONNECTION_STRING`/`DATABASE_URL`/`BA_DMO_TEST_DATABASE` in the environment, no local PostgreSQL listening on 127.0.0.1:5432, and no dump artifact in the repository. All schema facts below are migration‑/source-derived; live validation is explicitly deferred (see §14).

---

## 1. Executive Summary

### 1.1 Current persistence shape

BA DMO persists through a single, disciplined, forward-only PostgreSQL migration family **N01–N31** (31 files, whole-script execution via a custom Npgsql runner tracked in `schema_migrations`), producing **61 application tables** plus the runner bookkeeping table:

| Layer | Count | Contents |
|---|---|---|
| Access/Identity/Admin | 6 | `access_templates`, `internal_users`, `internal_user_access_templates`, `access_template_profiles`, `module_catalog_mirror`, plus shared `audit_events` |
| Job On | 10 | `job_on`, revision family (`job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`), `job_on_verification_occurrence`, `job_on_audit_event`, `job_on_field_option`, `jobon_user_current`, `article_reference_images` |
| Controlo area | 13 | Folha de Controlo (`controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events`) + Peso (`peso_references`, `peso_lotes`, `peso_controlos`, `peso_leituras`, `peso_comparacao_anterior`, `peso_day_approvals`, `peso_settings`) + Pegamentos (`pegamento_controlos`, `pegamento_medicoes`, `pegamento_documentos`) |
| Ferramentas | 6 | `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences`, `tool_usage_records` |
| Repair (Interna + Externa) | 7 | `repairers`, `repairer_repair_types`, `line_repairer_defaults`, `repair_exits`, `repair_exit_items`, `repair_events`, `internal_repair_records` |
| Armazém | 3 | `warehouse_locations`, `warehouse_stock`, `warehouse_movements` |
| Tampões | 9 | `tampao_field_defs`, `tampao_field_values`, `tampao_configurations`, `tampao_saldos`, `tampao_movements`, `tampao_planos`, `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event` |
| Boquilhas | 6 | `bq_lotes`, `bq_traces`, `bq_movements`, `bq_discrepancies`, `bq_lifecycle_history`, `bq_utilisation_readings` |
| Shared | 1 | `app_settings` |

The architecture is broadly sound: append-only fact tables are trigger-guarded (`ba_dmo_guard_append_only`), RLS/policy/grants cover every application table in the migration chain, all modules persist through Dapper repositories inside `BA.Dmo.Infrastructure`, and multi-statement atomicity is delivered by `DapperUnitOfWork` (plus per-module UoW factories for Repair/Tampões/Boquilhas).

**HEAD correction vs. prior audits:** the previous persistence reports (`persistence_cross_reference_audit.md` PA-JOBON-01/-02, `persistence_high_impact_validation.md` VAL-01) were recorded at HEAD `8478308`. At the current HEAD `683765f` ("Fix Job On lifecycle persistence") the Job On lifecycle defect is **fixed in the repository layer**: `DapperJobOnRepository.TransitionLifecycleAsync` now persists `status` + `closed_at_utc`/`canceled_at_utc`/`canceled_by`/`cancel_reason` in one `DapperUnitOfWork` together with the audit event (`src/BA.Dmo.Infrastructure/Access/DapperJobOnRepository.cs:183-212`), `JobOnService.TransitionAsync` invokes the domain `Close()`/`Cancel()` methods (`src/BA.Dmo.Application/Modules/JobOn/JobOnService.cs:234-271`), and the jsonb audit-payload defect class is addressed by `AuditJson.Normalize` + explicit `::jsonb` casts across repositories (`AuditJson.cs`, `DapperJobOnRepository.cs:529/536-537`, `DapperArmazemRepository.cs:450-451`, `DapperRepairRepository.cs:432-433`). Findings below that were fixed at HEAD are labelled accordingly; the rest remain open.

### 1.2 Major duplicated-authority problems

1. **User↔template assignment is stored twice:** `internal_users.template_id` **and** `internal_user_access_templates` (junction with `ux_internal_user_access_templates_actor`). Since N31 the junction is a 1:1 mirror of the `template_id` column; two write sites must stay in sync (N31 DML, `DapperAdminRepository.CreateInternalUserAsync`/`ReplaceUserAccessTemplatesAsync`), and a residual multi-template path still exists in `AdminUserService.CreateUserAsync`/`ChangeTemplatesAsync` (`templateIds.Length > 1` → unhandled 23505 from `ux_internal_user_access_templates_actor`) even though every Web form now sends exactly one template (`Pages/Admin/Users/Create|Edit.cshtml(.cs)`).
2. **Functional profile is stored twice with three writers:** `access_template_profiles.functional_profile` (N31, template-owned authority) **vs.** `internal_users.profile_title` (mirror). Writers: (a) N31 trigger/backfill + profile sync (migration-time), (b) Web-layer `TemplateProfileStore.UpsertAsync` raw SQL (also syncs `profile_title` on a separate connection), (c) `AdminUserService.UpdateUserAsync`/`CreateUserAsync` (user-level write of `profile_title` via `DapperAdminRepository.UpdateUserAsync`). Runtime resolution (`IdentityResolutionService.ResolveAsync`) reads **only** `profile_title` and never `access_template_profiles` — so today the "template-owned profile" is not the operative source of truth at login.
3. **Dead persisted read-model:** `peso_comparacao_anterior` (N06) is never read or written by any code; the "previous approved control" resolution is a live query (`DapperPesoRepository.GetPreviousApprovedAsync`, `DapperPesoRepository.cs:417-446`).
4. **Two dormant legacy columns:** `internal_users.modules_override` (N26; NULLed by N27, no runtime reader, still projected by two SQL reads, still writable via an uncalled port method) and `job_on_revision.image_asset_id` (N05; superseded by N29 `article_reference_images`, kept dormant).
5. **Dual audit authority (gap, not conflict):** Job On mutations write **only** `job_on_audit_event` and never `audit_events`, so transversal História (which reads `audit_events` exclusively) is blind to Job On activity. This is a missing dual-emit contract rather than a live duplicated writer.
6. **Whole-table mirrors of non-DB authority:** `module_catalog_mirror` mirrors the in-code `CanonicalModuleCatalog` (deliberate Admin display read-model, never grants access); the N27-era `legacy-override-<md5>` compatibility rows inside `access_templates` remain as data.

### 1.3 Areas already correctly normalized (do NOT touch)

- **Job On revision graph** — `job_on` → `job_on_revision` → `job_on_component` → (`job_on_component_field`/`job_on_component_row`) + `job_on_verification_occurrence` + `job_on_audit_event`; the immutable-revision snapshot pattern is the attribution anchor for Peso/Pegamentos/Controlo/Reparação Interna (TD-18/R006), append-only since N25, and must be preserved wholesale.
- **Tampões** — configuration / values / machine-association (N:M) / notes+events (append-only) / balances / movements / planning split is legitimate normalization (N21 owner decision; never collapse).
- **Ferramentas** — reference / lote / physical-piece / check-rule / check-occurrence / usage-history layering is orthogonal identity+config+fact; BQ tool-type identity is deliberately separate from the Boquilhas operational domain.
- **Armazém** — position / occupation-fact-with-release-history / movement facts is the correct current+history split; the partial unique index expresses "one active occupation per (position, lot)".
- **Repair** — `repairers` registry + `repairer_repair_types` capability join + `line_repairer_defaults` convenience default (explicitly NOT capability); `repair_events` as a shared append-only fact stream discriminated by scope; internal records CM/MF-only (N28); Boquilhas never becomes an RI repair entity.
- **Boquilhas** — lote/trace/movement/discrepancy/lifecycle/utilisation layers; lifecycle-state + trace-status duality is intentional.
- **Controlo/Peso/Pegamentos** — master/lote/control/reading/approval/config layers; per-control snapshots and revision anchors are intentional (snapshot ≠ live); `controlo_sheets`/items/events are a distinct owner-decision workflow inside Controlo.

### 1.4 Overall target direction

Keep the normalized graph; eliminate **duplicated business authority** (not table count):

1. One fact = one writer = one table; mirrors become explicit read-models or are removed.
2. Users/templates/profiles converge to: Applications (in-code catalog) → Template (title + ONE profile + modules) → User (ONE template). `access_template_profiles` becomes the profile authority; `internal_users.profile_title` becomes a derived mirror and is eventually removed; `internal_users.template_id` becomes the single user→template authority (with the junction either demoted to history or removed — owner options in §11).
3. Dead/dormant persistence (`peso_comparacao_anterior`, `modules_override`, `image_asset_id`, unwired `tampao_planos`/`job_on_field_option`) is dispositioned by owner decision, not automatically deleted.
4. Domain event streams stay domain-owned; `audit_events` stays the global compliance/História layer; the Job On dual-audit contract is defined explicitly.
5. The consolidated clean-install baseline is brought to the N31 final state (documented drift only in this phase).
6. All convergence is executed as **N32+ additive/forward-only migrations**; historical N01–N31 remain immutable.

---

## 2. Authority Principles

These are the rules used for every classification in this report.

| # | Principle | Rule | Application examples |
|---|---|---|---|
| A1 | **Current-state authority** | One table owns the *current operational state* of each business fact; other stores of the same fact are mirrors, read-models, or snapshots. | `job_on` (production context), `warehouse_stock` active rows (occupancy), `tampao_saldos` (balances), `internal_users` (identity), `peso_controlos` (control state). |
| A2 | **Configuration authority** | Configuration data owned by one table/one code surface; never duplicated into operational rows. | `access_templates.modules`, `access_template_profiles.functional_profile`, `tampao_field_defs/values`, `tool_check_rules`, `peso_settings`, `app_settings`, `job_on_field_option` (if wired). |
| A3 | **Event/history authority** | Facts are append-only and owned by their domain; corrections are new rows. History tables are authoritative *fact stores*, not duplicates of current state. | `bq_movements`, `warehouse_movements`, `tampao_movements`, `pegamento_medicoes`, `repair_events`, `tool_usage_records`, `bq_lifecycle_history`, `tampao_configuration_notes`, `tampao_configuration_machine_event`, `controlo_sheet_events`, revision-family tables. |
| A4 | **Snapshot ≈ not duplication** | Immutable snapshots pinned at write time (revision anchors, business-attribute copies like `mold_number`/`production_code` on a control) are *fact fidelity* — they freeze history against later edits (snapshot ≠ live, GLM-DATA-04.5). | `peso_controlos` identity columns, `pegamento_controlos.*_snapshot`, `job_on_component.*_snapshot`, `controlo_sheet_items`, `internal_repair_records` N22 context columns, `repair_exits.repairer_snapshot`. |
| A5 | **Derived/read-model data** | Data recomputed from another authority is derived; storing it is allowed only as an explicit read-model with a defined synchronizer, and it is never the write target. | `module_catalog_mirror` (mirror of in-code catalog), `jobon_user_current` (user-scoped explicit-open context), `audit_events` denormalized `job_on_id`/snapshots (audit facts). |
| A6 | **Mirrors to eliminate** | A stored value that duplicates another store of the same fact, has no independent writer consensus, and is not required for reconstruction is a mirror → eliminate or re-derive. | `internal_users.profile_title` (mirror of `access_template_profiles`), `peso_comparacao_anterior` (dead mirror of a live query), legacy `capabilities[]` inside template `modules` jsonb (no longer authorization input). |
| A7 | **Legacy data** | Columns/rows kept for compatibility/auditability are legacy; they are readable, documented, and eventually removed under owner decision — never silently dropped. | `internal_users.modules_override`, `job_on_revision.image_asset_id`, `legacy-override-*` templates. |
| A8 | **Orphans** | A persistence structure with no readers, no writers, and no surface is orphan-candidate — verify before classifying, never delete on suspicion. | `peso_comparacao_anterior` (verified dead), `job_on_field_option` (DB-only, no code consumer), `tampao_planos` (fully implemented, no surface). |
| A9 | **Junction/relationship tables are not duplication** | M:N and association tables are normalization, not redundancy — unless they replicate a fact already stored column-wise with no extra semantics. | `repairer_repair_types`, `tampao_configuration_machines`, `internal_user_access_templates` (junction kept **only** if it adds semantics; see §5). |
| A10 | **Configuration + events are not duplication** | Config tables and their append-only event/change streams are separate concerns (current vs. history). | `tool_check_rules` + `tool_check_occurrences`, `tampao_configurations` + `tampao_movements`, `tampao_configuration_machines` + `tampao_configuration_machine_event`. |
| A11 | **Current + history are not duplication** | A current-state row and its event/history rows are complementary by design. | `bq_lotes.lifecycle_state` + `bq_lifecycle_history`, `tampao_saldos` + `tampao_movements.balances_before/after`, `warehouse_stock` (active/released) + `warehouse_movements`. |
| A12 | **One write path per fact** | For every business fact, exactly one Application service → repository method should be the writer; other writers are debt. | Violations today: `profile_title` (3 writers), user→template (2 stores), audit (many post-commit writers — see §7). |
| A13 | **Catalog authority is code** | The module catalog is a compiled `CanonicalModuleCatalog`; the database never grants access. | `module_catalog_mirror` display only; `access_templates.modules` validated in Application; no capability check in SQL/RLS. |
| A14 | **Do not merge distinct domains** | External vs. internal repair, Ferramentas vs. Boquilhas, Peso vs. Pegamentos, Controlo vs. Peso: separate module ownership is preserved even when tables share prefixes or concern adjacent concepts. | `repair_events` (shared fact stream, both scopes) is kept; `internal_repair_records` stays CM/MF; no Boquilhas rows become RI records. |

---

## 3. Full Table Classification

**61 application tables classified.** Classification vocabulary: `KEEP` · `KEEP_NORMALIZED` · `MERGE` · `MIRROR_ELIMINATE` · `LEGACY_REMOVE_LATER` · `ORPHAN_CANDIDATE` · `DERIVED_NOT_AUTHORITY` · `NEEDS_OWNER_DECISION`.

Legend for Action: **KEEP** = retain as-is; **SYNC-GUARD** = add a consistency invariant between the table and its mirror; **RETIRE** = remove in a later convergence phase (N32+ phase F) with written plan; **WIRE** = give it a real consumer; **DECIDE** = owner decision gates the action.

| # | Table | Domain | Current Purpose | Classification | Target Authority | Action | Confidence |
|---|---|---|---|---|---|---|---|
| 1 | `access_templates` | Access/Identity | Reusable template: title/function (`name`), module grants (`modules` jsonb), active flag | KEEP | Template title + module selection (canonical, validated against in-code catalog) | KEEP; drop legacy `capabilities[]` semantics (A6); strip/ignore at write time | HIGH |
| 2 | `internal_users` | Access/Identity | Internal user identity: auth link, display name, active; `template_id` compatibility/authority pointer; `profile_title` mirror; dormant `modules_override` | KEEP | Identity + user's single template (`template_id`) + display/active | KEEP; `template_id` becomes THE user→template authority (see §5); `profile_title`/`modules_override` columns → RETIRE later | HIGH |
| 3 | `audit_events` | Global audit | Single global append-only compliance/history table (História + Admin Auditoria) | KEEP | Global cross-module audit/História authority | KEEP; close the Job On dual-emit gap (§7) | HIGH |
| 4 | `module_catalog_mirror` | Access/Admin | Display/ordering mirror of the in-code module catalog (Admin "Aplicações"); never grants access | DERIVED_NOT_AUTHORITY | In-code `CanonicalModuleCatalog` (compiled) | DECIDE: keep as explicit read-model with synchronizer, or eliminate and read the catalog directly (§8) | HIGH |
| 5 | `internal_user_access_templates` | Access/Identity | User↔template junction; single-assignment since N31 (`ux_internal_user_access_templates_actor`) | KEEP_NORMALIZED | Single user→template assignment — but the fact is **duplicated** with `internal_users.template_id` | KEEP under single-assignment; converge to one authority (§5): either becomes append-only assignment history or is removed after reader switch | HIGH |
| 6 | `access_template_profiles` | Access/Identity | N31 template-owned functional profile (1:1 with template; one of three) | KEEP | **Functional profile authority** | KEEP; make `internal_users.profile_title` a derived mirror, then RETIRE mirror | HIGH |
| 7 | `job_on` | Job On | Production sheet aggregate root; planning/status/current revision pointer | KEEP | Current Job On state | KEEP | HIGH |
| 8 | `job_on_revision` | Job On | Immutable revision snapshots; attribution anchor for Peso/Pegamentos/Controlo/RI; append-only (N25) | KEEP | Revision/immutable-snapshot authority | KEEP; `image_asset_id` column dormant → RETIRE later (A7) | HIGH |
| 9 | `job_on_component` | Job On | Per-family component rows of a revision (tool links + snapshots); append-only | KEEP | Revision component facts | KEEP | HIGH |
| 10 | `job_on_component_field` | Job On | Typed field values per component; append-only | KEEP | Component field facts | KEEP | HIGH |
| 11 | `job_on_component_row` | Job On | Repeatable CAL rows per component; append-only | KEEP | CAL row facts | KEEP | HIGH |
| 12 | `job_on_verification_occurrence` | Job On | Verification checks materialized per component (rules from Ferramentas) | KEEP | Verification facts/state | KEEP | HIGH |
| 13 | `job_on_audit_event` | Job On | Module-level Job On event stream (create/duplicate/save/image/transition facts with before/after) | KEEP | Job On domain event stream (module-scoped) | KEEP; define the dual-audit contract with `audit_events` (§7) | HIGH |
| 14 | `job_on_field_option` | Job On | Data-driven dropdown catalog per family/field (Definições) | NEEDS_OWNER_DECISION | Config (if wired) | DECIDE: wire the Definições surface (repository + API) or retire; zero code consumers today (§8) | HIGH (no consumers) |
| 15 | `article_reference_images` | Job On | Master Article/Reference image association (N29); keyed by normalized reference | KEEP | Reference image authority (file names only; binaries on filesystem) | KEEP; RLS/policy/grants must be present in the consolidated baseline (drift, §14) | HIGH |
| 16 | `jobon_user_current` | Job On | User-scoped "current Job On" the user explicitly opened (R011) | KEEP | User-scoped explicit-open context | KEEP | HIGH |
| 17 | `controlo_sheets` | Controlo | Folha de Controlo header (production/revision anchor, status, decision) | KEEP | Sheet current state | KEEP | HIGH |
| 18 | `controlo_sheet_items` | Controlo | Per-component snapshot + OK/NOK result of a sheet | KEEP | Sheet item facts (snapshot fidelity, A4) | KEEP | HIGH |
| 19 | `controlo_sheet_events` | Controlo | Append-only sheet workflow events (criar/editar/submeter/reeabrir/decidir) | KEEP | Sheet event stream | KEEP | HIGH |
| 20 | `peso_references` | Peso (Controlo) | Master mold/neckring identity + calculation volumes | KEEP | Peso master reference | KEEP | HIGH |
| 21 | `peso_lotes` | Peso | Peso lots per reference (processo, allowed_lines, nominal weight) | KEEP | Peso lot authority | KEEP | HIGH |
| 22 | `peso_controlos` | Peso | Control records (identity + snapshots + approval state; approved rows immutable) | KEEP | Peso control facts/state | KEEP | HIGH |
| 23 | `peso_leituras` | Peso | Per-CM readings of a control | KEEP | Reading facts | KEEP; add immutability guard for approved parents (§9B) | HIGH |
| 24 | `peso_comparacao_anterior` | Peso | Persisted "previous approved control" read path (N06) — never read/written | MIRROR_ELIMINATE | Live query `DapperPesoRepository.GetPreviousApprovedAsync` | RETIRE (empty table; confirmed zero writers/readers) | HIGH |
| 25 | `peso_day_approvals` | Peso | Day-approval facts per (mold, neckring, line, date) | KEEP | Day-approval facts | KEEP | HIGH |
| 26 | `peso_settings` | Peso | Peso constants/recipients/output-folder config | KEEP | Peso settings config | KEEP | HIGH |
| 27 | `pegamento_controlos` | Pegamentos (Controlo) | Measurement controls pinned to Job On revision; per-component nominals, tolerance, notas | KEEP | Pegamento control facts/state | KEEP | HIGH |
| 28 | `pegamento_medicoes` | Pegamentos | Append-only raw measurements (costura/contra_costura, tool_number) | KEEP | Measurement fact stream | KEEP | HIGH |
| 29 | `pegamento_documentos` | Pegamentos | 1:1 final PDF metadata per control | KEEP | Document-production facts | KEEP | HIGH |
| 30 | `tool_references` | Ferramentas | Master tool identity (type + ref_code; CM/MF/BQ/PU/CS) | KEEP | Tool reference authority | KEEP | HIGH |
| 31 | `tool_lotes` | Ferramentas | Lots per reference (qty, lines, drawing, processo) | KEEP | Tool lot authority (incl. `processo` per TD-17) | KEEP | HIGH |
| 32 | `physical_pieces` | Ferramentas | Individual numbered pieces of CM/MF lots | KEEP | Physical piece identity | KEEP | HIGH |
| 33 | `tool_check_rules` | Ferramentas | Configurable check rules per lot (with copy-origin) | KEEP | Check-rule config | KEEP | HIGH |
| 34 | `tool_check_occurrences` | Ferramentas | Materialized check occurrences (logical job_on links, no FK by design) | KEEP | Check occurrence facts | KEEP | HIGH |
| 35 | `tool_usage_records` | Ferramentas | Append-only utilisation readings (SAP manual %) per lote | KEEP | Utilisation fact stream | KEEP | HIGH |
| 36 | `repairers` | Reparação Externa/Interna | Canonical repairer registry (TD-15) | KEEP | Repairer registry authority | KEEP | HIGH |
| 37 | `line_repairer_defaults` | Reparação Externa | Convenience default repairer per (line, tool_type) — NOT capability | KEEP | Defaults config | KEEP | HIGH |
| 38 | `repair_exits` | Reparação Externa | Planned exit lists (status machine) | KEEP | External repair cycle state | KEEP | HIGH |
| 39 | `repair_exit_items` | Reparação Externa | Exit items (BQ by qty / CM-MF by piece number; pick/out/in facts) | KEEP | Item facts/state | KEEP | HIGH |
| 40 | `repair_events` | Reparação Externa/Interna | Append-only repair fact stream, `repair_scope` discriminator (interna/externa) | KEEP | Shared repair fact stream (deliberate; entities stay separate) | KEEP | HIGH |
| 41 | `internal_repair_records` | Reparação Interna | Quick internal repair records, CM/MF only (N28), corrections as new rows | KEEP | RI record facts (CM/MF) | KEEP | HIGH |
| 42 | `repairer_repair_types` | Reparação Externa | M:N repairer capability (CM/MF/BQ) | KEEP | Capability join (relationship) | KEEP | HIGH |
| 43 | `warehouse_locations` | Armazém | Physical positions (code unique) | KEEP | Location authority | KEEP | HIGH |
| 44 | `warehouse_stock` | Armazém | Occupation facts with release history; active = `released_at_utc IS NULL` | KEEP | Current occupancy authority (active rows) | KEEP; integrity hardening for 1:1 per-position (§9B) | HIGH |
| 45 | `warehouse_movements` | Armazém | Append-only in/out movement facts (destination, exit link) | KEEP | Movement fact stream | KEEP | HIGH |
| 46 | `tampao_field_defs` | Tampões | Configurable comparable fields (Diâmetro, Profundidade/Calote) | KEEP | Field config | KEEP | HIGH |
| 47 | `tampao_field_values` | Tampões | Normalized available values per field | KEEP | Value config | KEEP | HIGH |
| 48 | `tampao_configurations` | Tampões | Value-combination destinations, reused by id (UNIQUE values_json) | KEEP | Configuration authority | KEEP | HIGH |
| 49 | `tampao_saldos` | Tampões | Exactly two balances per configuration (Enchidos / Por encher) | KEEP | Current balance authority (moves only via movements) | KEEP | HIGH |
| 50 | `tampao_movements` | Tampões | Append-only movements with before/after balances | KEEP | Movement fact stream | KEEP | HIGH |
| 51 | `tampao_planos` | Tampões | Planned needs (planear ≠ reservar); full service/repo implementation, **zero Web/API surface** | NEEDS_OWNER_DECISION | Planning config (if shipped) | DECIDE: wire Planeamento UI+API (future-owned) or retire the surface + table (§8) | HIGH |
| 52 | `tampao_configuration_machines` | Tampões | Normalized M:N configuration→machine (B1–C3) | KEEP | Machine association (relationship) | KEEP | HIGH |
| 53 | `tampao_configuration_notes` | Tampões | Append-only comments per configuration | KEEP | Notes fact stream | KEEP | HIGH |
| 54 | `tampao_configuration_machine_event` | Tampões | Append-only added/removed machine-association events | KEEP | Association event stream | KEEP | HIGH |
| 55 | `app_settings` | Shared | Key/value shared settings (e.g. `main_documents_output_root`) | KEEP | Shared settings config | KEEP | HIGH |
| 56 | `bq_lotes` | Boquilhas | Master lot identity (reference+batch unique; lifecycle state) | KEEP | Boquilhas lot authority | KEEP | HIGH |
| 57 | `bq_traces` | Boquilhas | Production/repair traces per lot (one active per lot via partial unique) | KEEP | Trace state (active/closed) + reopen/void facts | KEEP | HIGH |
| 58 | `bq_movements` | Boquilhas | Append-only movement facts (incl. N18 `noted_repairer_id`) | KEEP | Movement fact stream | KEEP | HIGH |
| 59 | `bq_discrepancies` | Boquilhas | Return-excess discrepancy records (open/under_review/resolved) | KEEP | Discrepancy facts | KEEP | HIGH |
| 60 | `bq_lifecycle_history` | Boquilhas | Append-only lot lifecycle events (archived/scrapped/restored/retired) | KEEP | Lifecycle fact stream (complements `lifecycle_state`) | KEEP | HIGH |
| 61 | `bq_utilisation_readings` | Boquilhas | Append-only manual utilisation readings per trace | KEEP | Utilisation fact stream | KEEP | HIGH |

**Classification totals:** KEEP **56** · KEEP_NORMALIZED **1** · MERGE **0** · MIRROR_ELIMINATE **1** · LEGACY_REMOVE_LATER **0** · ORPHAN_CANDIDATE **0** · DERIVED_NOT_AUTHORITY **1** · NEEDS_OWNER_DECISION **2**.

Notes on the zeros (transparency, not padding):

- **MERGE = 0.** No table today is a merge candidate. The nearest-looking pairs are legitimate split structures: `repair_exits`/`repair_exit_items`/`internal_repair_records` (external vs internal are distinct workflows; shared `repair_events` is a fact stream, not a merge), `tool_references`/`bq_lotes` (Ferramentas vs Boquilhas domains stay separate by owner decision), and `job_on_audit_event` vs `audit_events` (distinct event-stream concepts — a *contract* decision, not a table merge; see §7).
- **LEGACY_REMOVE_LATER = 0 and ORPHAN_CANDIDATE = 0 at table level.** The orphan/legacy layer lives in **columns and unwired surfaces**, not whole tables: `internal_users.modules_override`, `job_on_revision.image_asset_id`, legacy `capabilities[]` in `access_templates.modules`, unwired `job_on_field_option`/`tampao_planos` (NEEDS_OWNER_DECISION above). No table is a confirmed dead-weight table except `peso_comparacao_anterior`, which is classified MIRROR_ELIMINATE (a mirror of a live query) rather than ORPHAN because its *role* is duplicated authority.

---

## 4. Column-Level Duplicate Authority

Important columns that duplicate (or mirror) another source of truth, with writers/readers and migration risk. "Current Writers" = code that writes the value today; "Current Readers" = code that reads it at runtime.

| Table.Column | Competing Authority | Current Writers | Current Readers | Target | Migration Risk |
|---|---|---|---|---|---|
| `internal_users.profile_title` | `access_template_profiles.functional_profile` (N31 template-owned) | (1) Web `TemplateProfileStore.UpsertAsync` (also syncs users); (2) `AdminUserService.UpdateUserAsync`/`CreateUserAsync` → `DapperAdminRepository.UpdateUserAsync`; (3) N31 trigger/backfill (migration-time); (4) `DapperInternalUserRepository.CreateBootstrapAdminAsync` | `IdentityResolutionService.ResolveAsync` (parses `profile_title` → functional profile — **never reads the profile table**) | **Mirror to remove:** profile table = authority; reader switches to `access_template_profiles`; user-level write path removed from service contract; column retired after parity | MEDIUM — resolution change affects login; needs writer consolidation first (§§5, 9A, 13) |
| `internal_users.template_id` | `internal_user_access_templates.template_id` (junction, unique per actor) | `DapperAdminRepository.CreateInternalUserAsync`/`ReplaceUserAccessTemplatesAsync` (both stores in one UoW); `DapperInternalUserRepository.CreateBootstrapAdminAsync`; N31 DML | `DapperInternalUserRepository.FindByAuthUserIdAsync` (junction primary + `template_id` fallback); `DapperAdminRepository.UserColumns` (both); self-lockout joins (junction); identity fallback | **One authority** (§5): recommend `template_id` as the persisted fact; junction becomes append-only assignment history (preferred) or is removed; a DB consistency guard (trigger/deferred constraint) enforces equality while both exist | MEDIUM — any change touches login resolution + self-lockout count; scripted reconciliation first |
| `internal_users.modules_override` | template modules (N27/N31 model) | `DapperAdminRepository.SetUserModulesOverrideAsync` — **no callers** (dormant; N27 already NULLed rows) | `DapperAdminRepository.UserColumns` (`modules_override::text`), `DapperInternalUserRepository.FindByAuthUserIdSql` (projected) | **Dormant legacy → remove later** (§8) after removing projections and the fail-closed 42703 schema-gate | LOW — column is NULL for all rows (N27); removal is additive-safe after code cleanup |
| `job_on_revision.image_asset_id` | `article_reference_images.image_asset_id` (N29 master) | `DapperJobOnRepository.InsertImageMutationAsync` — **no production callers** since N29 | revision inserts still carry the column; N29 migration read it once | **Dormant legacy → remove later** (§8); active image path is `article_reference_images` | LOW — dormant; zero live writers; tests pin the no-revision-created contract |
| `peso_controlos.{mold_number, neckring_number, production_code, line, lote}` | `peso_references` / `peso_lotes` / `job_on` live values | `DapperPesoRepository.CreateControlAsync` (copied at write time) | the UNIQUE identity (`uq_peso_controlos_identity`), guard trigger (N25), listing queries | **Keep as immutable identity snapshot** (A4) — NOT duplication; enforced immutable after approval (N25 trigger) | NONE (intentional; document only) |
| `pegamento_controlos.{reference_snapshot, production_code, machine_code, cm/bq/mf_snapshot, cm/bq/mf_nominal}` | live Job On revision context | `DapperPegamentoRepository.CreateAsync` (copied from pinned revision) | Pegamento reads/PDF; context lookups | **Keep as revision-pinned snapshots** (A4/A14) | NONE (intentional) |
| `job_on_component.{reference_snapshot, lot_snapshot, technical_name_snapshot, stock_snapshot, usage_snapshot}` + `source_tool_id`/`source_lot_id` FKs | live `tool_references`/`tool_lotes` | `DapperJobOnRepository.SaveRevisionGraphAsync` (append-only) | Job On reads; Peso/Pegamentos/Controlo/RI context lookups | **Keep as snapshot+link pair** (A4) | NONE (intentional) |
| `internal_repair_records.{production_code, reference, lot_id}` (N22) | live Job On revision context | `DapperReparacaoInternaRepository.InsertAsync` (copied at save) | RI history/context | **Keep as historical context snapshot** (A4); `lot_id` logical link stays FK-less by design | NONE (intentional) |
| `controlo_sheets.{production_code, reference, machine_code}` + `controlo_sheet_items.*_snapshot` | live job/revision context | `DapperControloSheetRepository.InsertAsync` (copied from pinned revision) | Sheet views/PDF | **Keep as pinned snapshots** (A4) | NONE (intentional) |
| `repair_exits.repairer_id` + `repairer_snapshot` | live `repairers` | `DapperRepairRepository.CreateExitAsync` | exit listing | **Keep as FK+snapshot** (A4); snapshots survive repairer deactivation | NONE (intentional) |
| `tampao_movements.{balances_before, balances_after}` | current `tampao_saldos` | `DapperTampaoRepository.InsertMovementAsync` (within UoW) | movement history | **Keep as event snapshots; current balances separate** (A11). Known fidelity bug: `alterar_configuracao` writes a truncated `balances_after` (see §9B) | NONE (intentional column; fix serialization) |
| `access_templates.modules[…]capabilities[]` (legacy arrays) | profile-derived capabilities (`AccessResolver.ProjectProfileCapabilities`) | N27 rewrote arrays to `[]`; `AdminTemplateService` now rejects non-empty capabilities | none at runtime (capability sets are derived; stored arrays are not authorization input) | **Legacy storage semantics** — stop persisting capabilities (already enforced); optionally strip the key shape in a later migration | LOW — data is inert; tests assert capability arrays are not consulted |
| `audit_events.{job_on_id, revision_id, before_summary, after_summary, actor_name_snapshot}` | `job_on_audit_event` (Job On) — **no intersection today** | Job On writes `job_on_audit_event` only; global table written by all other modules... | `DapperHistoriaRepository` (global only), Admin Auditoria | **Denormalized audit facts by design in `audit_events`; Job On elements need a dual-emit contract** — currently a *gap*, not a conflict (§7) | N/A (missing linkage) |
| `bq_traces.{sap_start, sap_end}` vs `tool_usage_records.{sap_start, sap_end, percent_used}` | — different domains (BQ trace utilisation vs. CM/MF lote utilisation) | different repositories | different consumers | **Not duplication** — different scopes (A14); do not merge | NONE |
| `module_catalog_mirror.{display_name, display_order, active}` | in-code `CanonicalModuleCatalog` (compiled) | `DapperModuleCatalogMirrorRepository.UpsertAllAsync` via `AdminMirrorService` synchronizer | Admin Aplicações page | **Derived read-model** (A5/A13) — mirror of code; never grants access; disposition = owner decision (§8) | LOW |
| `jobon_user_current.{production_code, reference, machine_code}` | live `job_on` row | `DapperJobOnUserContextRepository.SetCurrentAsync` (upsert) | current-open context consumers | **User-scoped display snapshot of an explicit open action** (A4/A5) — not a planning duplicate | NONE |

Duplicate-authority candidates (by A1–A13): **8 core candidates** — `profile_title` mirror, `template_id`↔junction pair, `modules_override` (dormant mirror), `image_asset_id` (dormant mirror), `peso_comparacao_anterior` (dead mirror), `module_catalog_mirror` (code mirror), legacy `capabilities[]` (stored-but-inert), Job On audit gap (`job_on_audit_event` ⇄ `audit_events`). Of these, three are live competing writers (`profile_title`, user→template pair), four are dormant/inert, and one is a missing-contract gap.

---

## 5. Users / Templates / Profiles Target Model

### 5.1 CURRENT model (N27 + N31, reconciled at HEAD)

```
Applications (in-code CanonicalModuleCatalog: 12 entries, 10 assignable)
        │  (1..* modules per template; 'admin' only for Admin profile; controlo→peso/pegamentos expansion;
             history derived, weight/peso/pegamentos non-assignable; capabilities DERIVED from profile)
        ▼
access_templates (template_id PK · name [title/function] · modules jsonb · active)
        │ 1..*  ←── internal_user_access_templates (actor_id, template_id) PK · UNIQUE(actor_id)  [1 row/user]
        │ 1:1   ←── access_template_profiles (template_id PK · functional_profile)               [1 profile/template]
        ▼
internal_users (actor_id PK · auth_user_id · template_id FK · display_name · profile_title [mirror] · active · modules_override [dormant])
        ▲
        └── written by: DapperAdminRepository (users+templates+junction), DapperInternalUserRepository (bootstrap),
                        Web TemplateProfileStore (access_template_profiles + profile_title sync, raw SQL)
```

Current authorities per the five questions:

| Fact | Current authority | Comments |
|---|---|---|
| 1. User's template | **two stores**: `internal_users.template_id` (NOT NULL FK, "compatibility/authority pointer" per N31) + junction row (UNIQUE per actor) | kept in sync in one UoW by `ReplaceUserAccessTemplatesAsync`; multi-template service path remains but is unreachable from Web UI; N31 DML treats `template_id` as the collapsing key |
| 2. Template's functional profile | `access_template_profiles.functional_profile` (N31) **nominally**, but runtime reads `internal_users.profile_title` | 3 writers (trigger/backfill, TemplateProfileStore, AdminUserService) |
| 3. Template's modules | `access_templates.modules` (module-only entries; validated by `GrantNormalizer`/`AdminTemplateService`) | capabilities inside the array are legacy/inert |
| 4. User's visible title/function | `access_templates.name` (surfaced at login as the profile/title string via `ResolvedIdentity.ProfileTitle` = active template NAME) | `profile_title` is the functional profile, not the display title |
| 5. Module catalog | **in-code** `CanonicalModuleCatalog` (compiled, validated at composition); `module_catalog_mirror` is display-only | the DB never grants; RLS is technical-only |

### 5.2 TARGET model (recommended)

```
Aplicações  = in-code CanonicalModuleCatalog            (canonical module catalog — authority #5)
        │   (module set selectable per template; capabilities derived from profile)
        ▼
access_templates   (template_id PK · name [title/function — authority #4] · modules jsonb [authority #3] · active)
        │ 1:1
        ▼
access_template_profiles (functional_profile [authority #2] · updated_at_utc)
        ▲                                                    ▲
        │ 1:1                                                 │ (read at resolution)
internal_users.template_id (FK NOT NULL — authority #1) ─────┘  (or the resolution join reads
        │                                                       access_templates → access_template_profiles)
        ▼
effective access/navigation = AccessResolver (in-code, derived; nothing stored)

Optional (if assignment auditing is required):
internal_user_template_history (actor_id · template_id · assigned_at_utc · assigned_by) — append-only history
```

**Authoritative columns (final):**

- `access_templates.template_id / name / modules / active` — template definition.
- `access_template_profiles.functional_profile` — the one functional profile per template.
- `internal_users.actor_id / auth_user_id / template_id / display_name / active` — identity + single effective template.
- (optional) assignment-history table if template-change audit is a product requirement.

**Redundant columns/tables to eliminate later (Phase F):**

- `internal_users.profile_title` — derived mirror of `access_template_profiles.functional_profile`; removed after resolution switches readers.
- `internal_users.modules_override` — dormant N26 column; removed after code projections and schema-gate cleanup.
- `internal_user_access_templates` as a **1:1 synchronised store** — the duplicate store of fact #1; either (i) it becomes an append-only assignment-history table (multiple rows per actor over time — eliminates the mirror while preserving audit), or (ii) it is removed entirely and the `template_id` column carries the fact (with `assigned_at/assigned_by` folded into `internal_users` if needed).
- `access_templates.modules` legacy `capabilities[]` shape — stop persisting (already enforced at the service boundary); cleanup in a schema-convergence pass.
- `module_catalog_mirror` — disposition per owner decision (keep as explicit read-model vs. read catalog directly).

### 5.3 Viable alternatives (comparison)

| Option | Shape | Pros | Cons | Verdict |
|---|---|---|---|---|
| **A (recommended): `template_id` column is the single authority; junction demoted to append-only history (or removed)** | One stored fact per user; junction no longer a 1:1 mirror | Removes duplicate write path; N31 lineage ("template_id = authority pointer") honoured; smallest reader churn (`IdentityResolutionService` keeps `template_id`-based resolution); history option preserves assignment audit | Requires reader switch + write consolidation + a DB consistency guard while both exist; new history table is new DDL | **RECOMMENDED** — recommended because it eliminates the duplicated fact with the least behavioural change to login resolution and Admin self-lockout, and it preserves audit if needed |
| B: junction remains the only authority; `template_id` column removed | `internal_users` without `template_id` | Single store in the formal relation | Highest churn: `template_id` is NOT NULL FK referenced by N31 DML, admin writes, identity fallback; the "compatibility/authority pointer" lineage would be reversed; larger migration + reader rewrite | Rejected |
| C: keep both, add DB-level sync guard | current shape + trigger/deferred constraint guaranteeing `template_id = junction.template_id` | Lowest immediate change | Perpetuates dual storage; guard is another sync mechanism; the duplicate fact remains | Acceptable *interim* (Phase A guard) but not the final shape |
| D: fold assignment metadata into `internal_users` and delete junction | `internal_users` + `assigned_at_utc`/`assigned_by` columns | Fewest tables; no junction | Loses per-assignment history unless a separate history table is added anyway; column additions to a hot identity table | Acceptable if history is not required (equivalent end-state to A-without-history) |

**Recommendation:** Option **A** (preferred) or **D** (if the owner declines assignment history). Both converge to *one authoritative write path* (A12): only `DapperAdminRepository` (Application boundary) writes the user→template fact; `TemplateProfileStore`'s direct SQL moves into the Application repository boundary; `IdentityResolutionService` reads the template-owned profile.

**Target cardinalities:** Applications → Templates 1..*; Template → Profile 1:1 (mandatory); Template → User 1..*; User → Template **1:1** (exactly one effective template; changing it REPLACES access — no accumulation, no hybrid); Template → Modules 1..*; Module catalog → modules 1:1 (code).

---

## 6. Domain-by-Domain Target Persistence Model

### 6.1 Job On

- **Current authorities:** `job_on` (aggregate/current), `job_on_revision` + component/field/row (immutable snapshots, append-only since N25), `job_on_verification_occurrence` (materialized checks), `job_on_audit_event` (module event stream), `jobon_user_current` (per-user explicit open), `article_reference_images` (reference-owned image), `job_on_field_option` (dormant config).
- **Target authorities:** unchanged — the revision graph is the immutable attribution anchor consumed by Peso/Pegamentos/Controlo/RI (`job_on_revision_id` FKs). The lifecycle fix at HEAD (`TransitionLifecycleAsync` persists `closed_at_utc`/`canceled_at_utc`/`canceled_by`/`cancel_reason` in the same UoW as the status + audit) removed the previous DB-vs-app contradiction; keep that write path and add tests (§9B). Image authority = `article_reference_images`; `job_on_revision.image_asset_id` = dormant legacy (remove later).
- **Legitimate normalized tables:** all 10 (listed in §3 rows 7–16). Do NOT collapse the revision/component/history structure.
- **Duplicate/legacy structures:** `image_asset_id` (dormant column), `job_on_field_option` (no code consumer — WIRE or RETIRE by owner decision), `insertImageMutationAsync`-style per-revision image persistence (dead port path since N29 — its interface methods are orphan candidates to prune).
- **Unresolved decisions:** (1) dual audit with `audit_events` (§7); (2) `job_on_field_option` surface; (3) whether the Job On write surface (create/duplicate/save/transition/confirm endpoints) is in Web scope — today only images/context/document are exposed; repository-level transition/lifecycle is wired and consistent, but no HTTP route reaches create/edit/transition.

### 6.2 Controlo / Peso / Pegamentos

- **Current authorities:** Peso: `peso_references`/`peso_lotes` (master), `peso_controlos` (control facts; approved = immutable via N25 trigger), `peso_leituras` (readings), `peso_day_approvals` (day approvals), `peso_settings`; Pegamentos: `pegamento_controlos`/`pegamento_medicoes`/`pegamento_documentos`; Folha: `controlo_sheets`/`controlo_sheet_items`/`controlo_sheet_events`. All pinned to the immutable revision (DS-04/DS-05).
- **Target authorities:** unchanged. `peso_comparacao_anterior` is **not** the previous-approved authority — the live query `DapperPesoRepository.GetPreviousApprovedAsync` is (TD-13/TD-30 documented in docs only; the table is dead). Eliminate the table (MIRROR_ELIMINATE) or, if the owner wants a persisted read path, wire it with a defined writer and invalidation — but do **not** keep it as an unpopulated twin.
- **Legitimate normalized tables:** everything else; `peso_day_approvals` is a distinct day-approval fact (not a duplicate of control approvals). Snapshots + nominals on `pegamento_controlos` are intentional fidelity.
- **Duplicate/legacy structures:** `peso_comparacao_anterior` (MIRROR_ELIMINATE); `GetApprovedControlsForJobOnAsync`/`GetPreviousApprovedAsync` interface methods are orphan-candidates on the repository surface.
- **Unresolved decisions:** nil for schema; see §11 for the dead-table disposition and for `peso_leituras` immutability under approved parents (§9B).

### 6.3 Ferramentas

- **Current authorities:** `tool_references` (master identity), `tool_lotes` (lot + `processo` TD-17), `physical_pieces` (numbered pieces), `tool_check_rules` (config), `tool_check_occurrences` (materialized facts; logical job_on links by design), `tool_usage_records` (append-only utilisation).
- **Target authorities:** unchanged. `tool_usage_records` is the utilisation history (R003); `bq_utilisation_readings` stays in Boquilhas (different domain scope — no merge, A14).
- **Legitimate normalized tables:** all six (§3 rows 30–35).
- **Duplicate/legacy structures:** `CopyCheckRuleAsync` (orphan port — `copied_from_rule_id` is populated by a direct `AddCheckRuleAsync` in lot duplication instead); none at table level.
- **Unresolved decisions:** nil of schema consequence.

### 6.4 Armazém

- **Current authorities:** current physical occupancy = `warehouse_stock` rows with `released_at_utc IS NULL` (active occupation facts); movement history = `warehouse_movements` (append-only); positions = `warehouse_locations`.
- **Target authorities:** unchanged. `warehouse_stock` is a fact table that keeps releases (same position/lot may be re-occupied; partial unique index `uq_warehouse_stock_active_occupation (location, tool_lote) WHERE released_at_utc IS NULL` enforces one active per pair). "fora" is calculated, never stored.
- **Legitimate normalized tables:** all three; there is **no** duplicated current-state representation.
- **Duplicate/legacy structures:** none. Integrity concerns (separate design concern from ARMAZEM-01 locking): the partial unique index does not express "at most one active occupation per position"; the repair return path (`DapperArmazemRepairMovementRepository.ConfirmReturnAsync`) performs its occupancy check without `FOR UPDATE` (TOCTOU) while `RegisterEntradaAsync` does lock — §9B.
- **Unresolved decisions:** whether 1:1-per-position is a hard rule (if yes → per-position partial unique index or lock; if no → document). Locking vs schema constraint are treated as separate design choices, per task instruction.

### 6.5 Boquilhas

- **Current authorities:** `bq_lotes` (master lot identity), `bq_traces` (trace state; one active per lot via `uq_bq_traces_active`), `bq_movements` (append-only facts), `bq_discrepancies` (excess records), `bq_lifecycle_history` (lifecycle facts), `bq_utilisation_readings` (readings); canonical repairer vocabulary read from `repairers`/`repairer_repair_types`/`line_repairer_defaults` (BQ).
- **Target authorities:** unchanged. `lifecycle_state` (current) + `bq_lifecycle_history` (events) is complementary, not duplicated (A11).
- **Legitimate normalized tables:** all six + shared repairer tables.
- **Duplicate/legacy structures:** the BQ void contract (`VoidMovementAsync`/`ListVoidedMovementIdsAsync`, `bq_traces.deleted_movements`, `movement_type 'fim'`) is dormant with no producer/consumer — `deleted_movements` is not consulted by balance math. Owner decision: wire or retire. `bq_movements.noted_repairer_id` (N18) has no supporting index (§9B).
- **Unresolved decisions:** BQ void contract disposition.

### 6.6 Reparação Interna

- **Current authorities:** `internal_repair_records` (CM/MF only, N28; corrections are new rows with `correction_of_id`/`before_snapshot`); N22 context columns (`job_on_revision_id` FK + production/reference/lot snapshots) pin history to the immutable revision; `repair_events` (scope `interna`) records the fact stream.
- **Target authorities:** unchanged — **Reparação Interna = CM/MF only**; Boquilhas never becomes an RI repair entity; no merge with external repair even though both write `repair_events` (shared fact stream with scope discriminator is intentional).
- **Legitimate normalized tables:** `internal_repair_records`, `repair_events` (interna scope).
- **Duplicate/legacy structures:** none at table level; `InternalRepairRules` (dead rule class) and `GetChainRootAsync` (uncalled port) are code-level orphans.
- **Unresolved decisions:** nil of schema consequence.

### 6.7 Reparação Externa

- **Current authorities:** `repairers` (canonical registry, TD-15), `repairer_repair_types` (capability M:N — NOT the same as `line_repairer_defaults`, a convenience default), `repair_exits`/`repair_exit_items` (planned exit cycle; BQ by qty, CM/MF by piece number), `repair_events` (scope `externa`), plus `warehouse_movements` provenance via `DapperArmazemRepairMovementRepository`.
- **Target authorities:** unchanged. `line_repairer_defaults` stays "pure convenience default" — capability lives in `repairer_repair_types` (N20 explicit).
- **Legitimate normalized tables:** all seven (§3 rows 36–42).
- **Duplicate/legacy structures:** none at table level. Integrity: `SetRepairerRepairTypesAsync` (DELETE + N×INSERT) and `CreateExitAsync` (exit+items+audit) are non-atomic multi-command writes (§9B); audit payloads were normalized at HEAD (`AuditJson.Normalize`).
- **Unresolved decisions:** transaction-shape policy for repair setup flows (§11).

### 6.8 Tampões

- **Current authorities:** `tampao_configurations` (destination config, reused by id), `tampao_saldos` (two balances, movement-only changes), `tampao_movements` (append-only facts with balances before/after), `tampao_field_defs`/`tampao_field_values` (config), `tampao_configuration_machines` (M:N machines), `tampao_configuration_notes`/`tampao_configuration_machine_event` (append-only logs), `tampao_planos` (planning; **unwired**).
- **Target authorities:** unchanged — this split is legitimate normalization and must NOT be collapsed (task instruction). `tampao_planos` is fully implemented (domain entity, service `PlanearAsync/CancelarPlanoAsync/ListPlanosAsync`, repository CRUD, audit codes `tampoes.planear`/`tampoes.plano.cancelar`) with **zero Web/API surface** (no `/api/tampoes/plan*` routes; `TampaoWebApiTests.Planeamento_IsAbsentFromRenderedSurface_AndEndpoints` asserts 404s; stale `#planosTable` CSS remains).
- **Legitimate normalized tables:** all nine.
- **Duplicate/legacy structures:** `tampao_planos` = dormant/future-owned planning surface → NEEDS_OWNER_DECISION (wire the Planeamento tab + endpoints, or retire the surface and table). Known fact-fidelity defect: `alterar_configuracao` writes a truncated `balances_after` (§9B).
- **Unresolved decisions:** `tampao_planos` disposition; `balances_after` serialization contract.

### 6.9 Admin / Access

- **Current authorities:** `access_templates` (template defs), `access_template_profiles` (profile — nominal), `internal_users` (identity + `template_id` + `profile_title` mirror + dormant override), `internal_user_access_templates` (junction), `module_catalog_mirror` (display mirror), `audit_events` (Admin audit/query), in-code catalogs (module/page/capability authority).
- **Target authorities (from §5):** template-owned profile = `access_template_profiles` (read at resolution); user→template = `internal_users.template_id` (single fact); modules = `access_templates.modules`; catalog = code. `module_catalog_mirror` = explicit read-model or removed. `TemplateProfileStore`'s raw SQL is moved behind the Application repository boundary (single write path, A12).
- **Legitimate normalized tables:** the 6 access tables (with the §5 convergence).
- **Duplicate/legacy structures:** `profile_title` mirror (3 writers), junction 1:1 mirror, `modules_override`, legacy `capabilities[]`, N27 `legacy-override-*` compatibility rows (data).
- **Unresolved decisions:** all of §5 plus RLS policy-naming convention (§9B).

### 6.10 Audit / History

See §7.

---

## 7. Audit / Event Strategy

### 7.1 The two layers

| Layer | Table(s) | Role | Append-only |
|---|---|---|---|
| Global compliance/history | `audit_events` | Canonical cross-module audit: every domain mutation *that should appear in História/Admin Auditoria*. Denormalized facts (actor snapshot, module, action, entity, result, reason, before/after summaries); no FK coupling by design | yes (`trg_audit_events_append_only`) |
| Domain event streams | `bq_movements`, `bq_lifecycle_history`, `bq_utilisation_readings`, `pegamento_medicoes`, `repair_events`, `warehouse_movements`, `tampao_movements`, `tampao_configuration_notes`, `tampao_configuration_machine_event`, `controlo_sheet_events`, `tool_usage_records`, `job_on_audit_event`, and the immutable revision family | Domain-owned fact streams required for reconstruction/querying of that domain (balances, traces, sheets, movements) | yes (each guarded) |

### 7.2 Per-domain fact → store map

| Domain fact class | Goes to | Verdict (1=domain history required, 2=audit/compliance only, 3=duplicated with audit_events) |
|---|---|---|
| Boquilhas movements/lifecycle/readings | `bq_movements`/`bq_lifecycle_history`/`bq_utilisation_readings` | **1** — reconstruction/querying (balance math, trace history) — plus `audit_events` module `boquilhas` (2) for compliance |
| Pegamentos measurements | `pegamento_medicoes` (raw facts; averages calculated in code) | **1** — keep; control-level compliance in `audit_events` |
| Peso controls/approvals | `peso_controlos` (state + `approval_log` jsonb) + `peso_day_approvals` | **1** — domain facts; `audit_events` (module `peso`) for compliance |
| Controlo sheet workflow | `controlo_sheet_events` | **1** — sheet audit trail required by owner contract; not duplicated in `audit_events` |
| Folha items/results | `controlo_sheet_items` | **1** — current+snapshot; no event table needed beyond items |
| Repair facts | `repair_events` (both scopes) | **1** — fact stream for repair counting/history; `audit_events` module `reparacao_*` (2) |
| Ferramentas utilisation | `tool_usage_records` | **1** — history for cumulative %; `audit_events` module `ferramentas` (2) |
| Tampões movements/machine changes/comments | `tampao_movements`/`tampao_configuration_machine_event`/`tampao_configuration_notes` | **1** — reconstruction (balances derive from movements); `audit_events` module `tampoes` (2) |
| Armazém movements | `warehouse_movements` | **1** — history + "fora" derivation; `audit_events` module `armazem` (2) |
| Job On lifecycle/revision/image facts | `job_on_audit_event` + immutable revision family | **1** — reconstruction (revision attribution anchor); **global `audit_events` currently NOT written by Job On — gap** (see 7.3) |
| All modules — compliance/História | `audit_events` | **2** — global layer; do NOT centralize domain reconstruction into it |

### 7.3 Explicit statements

1. **`audit_events` and the domain event tables are different legitimate concepts and must NOT be merged.** Domain tables are the authoritative fact stores for reconstruction and domain querying (the thousands of movement/measurement/reading rows have specific shapes and indexes); `audit_events` is the slim, denormalized, cross-module compliance/history view. Centralizing domain history into `audit_events` would multiply row volume, destroy domain query shapes, and add no authority.
2. **No domain event table is "duplicated with audit_events"** — they carry disjoint facts by design. The one authority question is on the **Job On side**: `job_on_audit_event` is populated for every Job On mutation (create/duplicate/save/image/lifecycle), but **no Job On flow writes `audit_events`** (verified: `src/BA.Dmo.Application/Modules/JobOn/` and `DapperJobOnRepository` write only `job_on_audit_event`; `DapperHistoriaRepository` reads only `audit_events`). Consequence: transversal História is blind to Job On activity — an authority **gap**, not a conflict. Recommendation (decision D-8): **dual-emit** — Job On keeps `job_on_audit_event` as its reconstruction/attribution stream AND emits a compact `audit_events` row (module `jobon`) for the same mutation, in the same `DapperUnitOfWork` (matching the co-transactionality principle the other modules already follow at HEAD). Keep both tables.
3. **Write-side contract:** audit events must be co-transactional with their business write (the convention now applied at HEAD via `AuditJson.Normalize` + in-UoW inserts in image/lifecycle paths). Several modules still emit `audit_events` on a separate connection after commit (`DapperArmazemRepository`, `DapperRepairRepository` service paths, `DapperPesoRepository`, etc.) — §9B (B5).

---

## 8. Current → Target Dependency Map

Every MERGE / MIRROR_ELIMINATE / LEGACY_REMOVE_LATER / DERIVED / decision-gated attrition item, with all known dependencies. (There are **no MERGE items**; the entries below are the attrition/mirror/derived items and the two NEEDS_OWNER_DECISION tables.)

### 8.1 `peso_comparacao_anterior` — MIRROR_ELIMINATE

| Aspect | Detail |
|---|---|
| FK dependencies | `peso_controlo_id` PK→`peso_controlos` ON DELETE CASCADE; `previous_peso_controlo_id`→`peso_controlos` (nullable) |
| Dapper repositories | `DapperPesoRepository` — **zero** SQL on this table (verified; only a doc comment at `DapperPesoRepository.cs:14`); authority = `GetPreviousApprovedAsync` live query (`:417-446`) |
| Application services | none (previous-approved resolution is the live query; no service materializes this table) |
| Web / raw SQL consumers | none |
| Tests | none reference the table; `PesoService`/unit tests cover the live query behavior |
| Migrations | created N06; RLS/policy in N12 |
| Data migration risk | **NONE** — the table is empty by construction (zero writers ever). `DROP TABLE` is a pure schema cleanup; keep as a normal Phase-F migration, guarded by a row-count zero check for safety |

### 8.2 `module_catalog_mirror` — DERIVED_NOT_AUTHORITY (decision-gated disposition)

| Aspect | Detail |
|---|---|
| FK dependencies | none (standalone) |
| Dapper repositories | `DapperModuleCatalogMirrorRepository` (GetAllAsync / UpsertAllAsync — delete-stale + per-row upsert in one UoW) |
| Application services | `AdminMirrorService` (Aplicações page read/write), `ModuleCatalogMirrorSynchronizer` (build/validate/merge from in-code `ModuleCatalog`) |
| Web / raw SQL consumers | `Pages/Admin/Applications/Index.cshtml(.cs)`; no raw SQL (repository only) |
| Tests | `ModuleCatalogMirrorSynchronizerTests`, `AdminAuditAndMirrorTests`, `AdminWebAuthorizationTests` (fake mirror) |
| Migrations | N02 (table + index), N12 (RLS/policy) |
| Data migration risk | LOW — content is re-syncable from code at any time; eliminating the table only affects the Admin Aplicações display path (either keep the read-model or switch the page to `CanonicalModuleCatalog`). Owner decision D-6 |

### 8.3 `internal_users.modules_override` — LEGACY column (remove later)

| Aspect | Detail |
|---|---|
| FK dependencies | none (column) |
| Dapper repositories | `DapperAdminRepository.SetUserModulesOverrideAsync` (dormant, no callers; `UserColumns` projects `modules_override::text`), `DapperInternalUserRepository.FindByAuthUserIdSql` (projects) |
| Application services | none consume it; `IdentityResolutionService` ignores it (unit test `ModulesOverride_IsDormant_AndDoesNotReplaceTemplateModules`) |
| Web / raw SQL consumers | none (Admin models carry `ModulesOverrideJson` only as a projection) |
| Tests | `IdentityResolutionServiceTests.cs:192`, `DapperAdminRepositoryProjectionTests`, `AdminUserServiceTests` (schema-missing fail-closed via SQLSTATE 42703 → `SchemaMigrationRequiredException`) |
| Migrations | N26 (add), N27 (NULLed) |
| Data migration risk | LOW (all rows NULL); removal must be preceded by deleting the projections, the dormant port method, and the 42703 schema-gate catch (otherwise N26-missing detection is lost — that gate needs a new or replaced mechanism) |

### 8.4 `job_on_revision.image_asset_id` — LEGACY column (remove later)

| Aspect | Detail |
|---|---|
| FK dependencies | none (column) |
| Dapper repositories | `DapperJobOnRepository` (revision inserts still write it; `InsertImageMutationAsync` dormant path — no production callers since N29) |
| Application services | `JobOnService` image use cases route via `IArticleReferenceImageRepository`; `IJobOnRepository.InsertImageMutationAsync` orphan port |
| Web / raw SQL consumers | none (image API uses `article_reference_images`) |
| Tests | `JobOnImageWebApiTests` (fake throws on `InsertImageMutationAsync` — pins the no-revision-created contract), `JobOnServiceTests` (legacy `image_asset_id` NOT persisted) |
| Migrations | N05 (add), N29 (read-once + keep dormant) |
| Data migration risk | LOW — after N29 promotion all current images are in `article_reference_images`; historical per-revision values remain only inside old revisions. Removal decision D-11 |

### 8.5 `tampao_planos` — NEEDS_OWNER_DECISION (dormant/future-owned)

| Aspect | Detail |
|---|---|
| FK dependencies | `tampao_configuration_id`→`tampao_configurations`; `created_by`→`internal_users`; `job_on_id`/`production_code` logical links (no FK) |
| Dapper repositories | `DapperTampaoRepository` — `CreatePlanoAsync`, `GetPlanoByIdAsync`, `CancelPlanoAsync` (UoW), `ListPlanosAsync` (all live implementations) |
| Application services | `TampaoService.PlanearAsync`/`CancelarPlanoAsync`/`ListPlanosAsync`; request records `PlanearRequest`/`CancelarPlanoRequest`/`PlanoFilter`; audit codes `tampoes.planear`/`tampoes.plano.cancelar` |
| Web / raw SQL consumers | **none** — no `/api/tampoes/planos|planear|cancelar` routes in `Program.cs` (Tampões block lines 1281–1403); no Planeamento tab in `Index.cshtml`; `tampoes.js` never calls it; stale `#planosTable` CSS only |
| Tests | `TampaoServiceTests` (planning does not reserve; cancel preserves balances), `TampaoWebApiTests.Planeamento_IsAbsentFromRenderedSurface_AndEndpoints` (asserts 404s + absence) |
| Migrations | N10 (table), N12 (RLS/policy) |
| Data migration risk | NONE today (no surface); if **wired**, zero data migration (fresh feature data); if **retired**, table is likely empty — guard with row-count check |

### 8.6 `job_on_field_option` — NEEDS_OWNER_DECISION (DB-only dropdown catalog)

| Aspect | Detail |
|---|---|
| FK dependencies | none (standalone) |
| Dapper repositories | **none** — zero matches for `job_on_field_option` anywhere in `src/` (verified) |
| Application services | none — the Definições surface in `jobon.js` (catalog option CRUD UI elements) has no persistence path |
| Web / raw SQL consumers | none |
| Tests | none |
| Migrations | N05 (table + unique + index), N12 (RLS/policy) |
| Data migration risk | NONE (no rows written); WIRE (repository + API + Definições handler) or RETIRE — decision D-7 |

### 8.7 `internal_users.profile_title` mirror + junction 1:1 mirror — convergence items (§5)

| Aspect | Detail |
|---|---|
| FK dependencies | `internal_users.template_id`→`access_templates` (NOT NULL); junction FKs; `access_template_profiles.template_id`→`access_templates` ON DELETE CASCADE |
| Dapper repositories | `DapperAdminRepository` (users/templates/junction: `CreateInternalUserAsync`, `ReplaceUserAccessTemplatesAsync`, `UpdateUserAsync`, `CountActiveAdminsOnAsync`), `DapperInternalUserRepository` (`FindByAuthUserIdAsync`, `AdminExistsAsync`, `CreateBootstrapAdminAsync`) |
| Application services | `AdminUserService` (create/update/change/save — still multi-template capable at the service edge), `IdentityResolutionService` (resolution reads `profile_title` + junction with `template_id` fallback), `AccessResolver` (profile-derived capabilities) |
| Web / raw SQL consumers | `Pages/Admin/TemplateProfileStore.cs` (raw SQL upsert `access_template_profiles` + `UPDATE internal_users SET profile_title` — separate connection from `AdminTemplateService`); Admin Users/Templates pages |
| Tests | `IdentityResolutionServiceTests` (single-template, ambiguity fail-closed, dormant override, invalid profile), `AdminUserServiceTests`, `AdminWebAuthorizationTests` (profile-derived capabilities), `DapperAdminRepositoryProjectionTests`, `AdminTemplateServiceTests` |
| Migrations | N01, N25 (auth unique), N26, N27, N31 |
| Data migration risk | MEDIUM — reader/writer switch of login path; reconcile `profile_title` from `access_template_profiles` before switching; enforce exactly-one template at the service edge before relying on the unique index; see §13 order |

---

## 9. Schema Integrity Improvements

Design-only recommendations (no SQL produced). Two clearly separated tracks:

### A. Duplicate-authority cleanup (track A — remove mirrors/legacy/duplicate paths)

- **A1. Profile authority.** Make `access_template_profiles` the sole profile writer target; collapse the three writers into one Application-boundary method (repository upsert + `internal_users.profile_title` sync **in the same UoW** — or drop the sync entirely and switch readers); make `IdentityResolutionService` resolve the profile from the template (`access_templates → access_template_profiles`); remove the user-level `profile_title` field from the `AdminUserService` write contract (UI already treats it as template-supplied); retire the column in Phase F.
- **A2. User→template authority.** Declare `internal_users.template_id` the single stored fact; optionally convert `internal_user_access_templates` to an append-only assignment-history table (one row per assignment change; drop `ux_internal_user_access_templates_actor`, add a plain index) or remove it; while both exist, add a **DB consistency guard** (trigger or deferred constraint) asserting `internal_users.template_id = (SELECT template_id FROM internal_user_access_templates WHERE actor_id = ...)`, and enforce exactly-one template at the `AdminUserService` edge (reject `templateIds.Count != 1` with a typed error instead of relying on the unique index's 23505).
- **A3. Dormant columns.** `internal_users.modules_override` and `job_on_revision.image_asset_id`: remove projections + dormant port methods first, then drop columns in Phase F (keep the N26 schema-gate semantics — replace the 42703 catch with an explicit preflight check or a fresh migration-required signal).
- **A4. `peso_comparacao_anterior`.** Drop the table (Phase F) after a row-count-zero guard; the live query remains the authority.
- **A5. `module_catalog_mirror`.** Either keep as an explicitly documented read-model (with the synchronizer as its only writer) or eliminate it and have Admin Aplicações read the compiled catalog — owner decision D-6.
- **A6. Legacy capability arrays in `access_templates.modules`.** Stop persisting `capabilities[]` (already enforced by `AdminTemplateService`); normalize the stored JSON shape in a convergence migration; the runtime never consults them (already true).

### B. Integrity hardening (track B — robustness, independent of authority cleanup)

- **B1. Job On lifecycle test-proofing (HEAD fix is code-complete; prove it).** Add DB-level probes (extend the `BA_DMO_TEST_DATABASE` pattern): transitioning to `fechado`/`cancelado` with timestamps succeeds, without them raises 23514; repository-level assertion that `TransitionLifecycleAsync` sets status+timestamps+reason in one statement; service-level co-commit of status+audit.
- **B2. Armazém 1:1-per-position invariant.** If "one active occupation per position" is the rule: either lock the position row (`warehouse_locations ... FOR UPDATE`) at the start of the repair-return occupancy check (mirroring `RegisterEntradaAsync`), or add a partial unique index on `(warehouse_location_id) WHERE released_at_utc IS NULL`. This is a concurrency invariant, deliberately separated from the ARMAZEM-01 locking discussion.
- **B3. `peso_leituras` immutability under approved parents.** `peso_controlos` is guarded by `ba_dmo_guard_peso_approved`, but leituras of an approved control are silently rewritable (`UpdateControlAsync` DELETE+INSERT). Guard at DB level (trigger on `peso_leituras` when parent approved) or strictly enforce it in the service before the DELETE+INSERT.
- **B4. Repair write atomicity.** `SetRepairerRepairTypesAsync` (DELETE + N×INSERT) and `ReparacaoExternaService.CreateExitAsync` (exit+items+audit) must run in one UoW; audit inserts should be co-transactional (see B5).
- **B5. Audit co-transactionality policy.** Define one policy: business write + its `audit_events`/`job_on_audit_event` row commit or roll back together; migrate the post-commit/separate-connection emitters (`DapperArmazemRepository`, `DapperRepairRepository` service paths, `DapperPesoRepository`, `DapperFerramentasRepository`, etc.) onto their existing UoW/`InsertAuditEventAsync`-in-transaction pattern.
- **B6. Missing indexes.** `bq_movements(noted_repairer_id)` (N18 column, filtered listing in `DapperBoquilhasRepository`); review N+1 per-control leituras reads and `GetHistoricalProductionsAsync` correlated subqueries at scale.
- **B7. Constraint semantics review (design-level).** `pegamento_medicoes.contra_costura NOT NULL` vs. the domain's one-sided measurement capability (align column or domain rule); `bq_traces.start_line NOT NULL` vs. optional binding; `tampao_movements.balances_after` fidelity for `alterar_configuracao` (serialize origin+destination balances); consider a single RLS policy naming convention (`ba_dmo_app_access` everywhere); decide whether ripple cascades (`controlo_sheet_items/events` ON DELETE CASCADE, `access_template_profiles` ON DELETE CASCADE) match the product's actual delete policy (templates are deactivated, never deleted).
- **B8. `job_on` identity handling.** With the cancel path now reachable (timestamps persisted), `uq_job_on_identity`'s exemption works; translate the raw 23505 on duplicate non-canceled `(production_code, machine_code)` into a typed domain error and decide the re-issue semantics explicitly.

---

## 10. Migration Convergence Strategy (N32+, design only)

Historical N01–N31 are immutable; all convergence goes through additive/forward-only N32+ migrations with the same whole-script runner conventions. Phases (each with affected objects, rollback, validation gates, destructiveness):

### Phase A — Add target structures / constraints (non-destructive)
- Create any new target structures: (optional) `internal_user_template_history` (append-only assignment history) if the owner chooses history; `access_template_profiles` already exists; add the **consistency guard** (trigger/deferred constraint) between `internal_users.template_id` and the junction while both exist; add the per-position occupancy invariant (B2) if adopted; add `bq_movements(noted_repairer_id)` index (B6); add the missing CHECK reconciliation for `pegamento_medicoes.contra_costura`/`bq_traces.start_line` (B7, decision-gated).
- Affected: `internal_users`, `internal_user_access_templates`, `warehouse_stock`, `bq_movements`, `pegamento_medicoes`, `bq_traces`.
- Rollback: reversible (additive) — drop the new objects; no data touched.
- Validation gates: fresh-build CI runs N01–N31 + N32+ on a disposable PG; guard tests assert new constraints exist.

### Phase B — Backfill and reconcile data (non-destructive; guarded)
- Recompute `access_template_profiles` from canonical sources if any drift exists; reconcile `internal_users.profile_title` from the profile table (one-pass, idempotent, `ON CONFLICT DO NOTHING`); verify single-assignment per user (junction row count = 1 per actor); inventory `module_catalog_mirror` vs catalog; inventory rows in `peso_comparacao_anterior`/`tampao_planos`/`job_on_field_option` for the disposition decisions.
- Affected: `access_template_profiles`, `internal_users`, junction, the three decision-gated tables.
- Rollback: the reconcile writes are idempotent re-runnable; keep pre-images in report-only mode first.
- Validation gates: pre/post row-count and divergence reports; fail-closed guards raise instead of fabricating values (mirroring N27/N29/N31 conventions).

### Phase C — Switch writers (application code, then data)
- Single write path for the profile (Application-boundary repository method; remove `TemplateProfileStore` direct SQL and the user-level `profile_title` write from `AdminUserService`); single write path for user→template (reject multi-template at the service edge; `ReplaceUserAccessTemplatesAsync` becomes single-template or history-append); stop emitting legacy `capabilities[]` (already enforced).
- Affected: `DapperAdminRepository`, `DapperInternalUserRepository`, `AdminUserService`, `IdentityResolutionService`, `TemplateProfileStore` (removed/moved), Admin pages.
- Rollback: application-version rollback is safe because schema is additive; the DB guard (Phase A) makes dual-storage divergence impossible.
- Validation gates: full unit/integration suite (real-PG for the DB-dependent paths); login resolution tests assert profile comes from the template table.

### Phase D — Switch readers
- `IdentityResolutionService`/`DapperInternalUserRepository.FindByAuthUserIdAsync` resolve the functional profile from `access_template_profiles` (via template) and the single template from `internal_users.template_id`; remove `profile_title` from the identity row projection; Junction reads collapse to template_id (+ history if adopted).
- Affected: `DapperInternalUserRepository`, `IdentityResolutionService`, `AccessResolver` (invariant: profile only), Admin user listing projections.
- Rollback: feature-flag or versioned resolution for one cycle.
- Validation gates: equivalence tests (old resolver vs new resolver on the same fixture data); História/Admin unaffected.

### Phase E — Verify parity
- Long-running parity checks (report-only): `profile_title == access_template_profiles.functional_profile` for every user; junction == `template_id`; `peso_comparacao_anterior` row count stays 0; mirror sync matches catalog; audit rows exist for every business write after the dual-emit fix.
- Validation gates: scheduled report job (or a guard test) fails closed on divergence; no writes during verification except the reconciled ones.

### Phase F — Remove legacy authorities (destructive)
- Drop `peso_comparacao_anterior`; drop `internal_users.modules_override` / `job_on_revision.image_asset_id`; remove `internal_users.profile_title`; either remove the junction (Option D) or convert it to the history table (Option A — this is where the unique index goes away and the append-only history index appears); (if decided) retire `tampao_planos`/`job_on_field_option` tables; remove legacy `capabilities[]` normalization; (if decided) eliminate `module_catalog_mirror`.
- Affected: the tables/columns listed; Dapper repositories without projections on them.
- Rollback: NOT reversible for dropped tables/columns — therefore gated behind: (1) owner decisions, (2) Phase E parity green, (3) row-count zero guards, (4) one-release soak. Destructive classification: **F is the only destructive phase; A–E are non-destructive.**
- Validation gates: migration guard raises if any unexpected rows exist; post-drop schema tests; consolidated baseline re-verified against the new final state.

### Phase G — Rebuild consolidated install baseline
- Refresh `database/consolidated_clean_install.sql` to the **N31+ final state**: add `access_template_profiles` + `ba_dmo_ensure_access_template_profile` + `trg_access_templates_ensure_profile` + `ux_internal_user_access_templates_actor` + N31 backfill/collapse semantics (or their Phase-A/B equivalents); add the **`article_reference_images` RLS/policy/grants stanza** (currently missing — see §14); refresh the header/provenance claims (currently "N01 … N24" / "includes N25-N27") to the exact final migration horizon; re-run the consolidated-equivalence verification (the referenced `reports/consolidated_schema_equivalence.md` does not exist in-repo — reproduce it). Non-destructive (file regeneration, not a migration).
- Affected: one file + its equivalence test.
- Rollback: git revert of the file; guard tests assert equivalence either way.
- Validation gates: clean-install run produces the same 61(+Δ) tables as the migration chain; RLS/policy/grants inventory test covers every application table; `ShippedFreshBuildFamily_IsComplete_N01ThroughN3x` updated consistently.

---

## 11. Owner Decisions Required

Compact numbered list — exact question, alternatives, recommendation, consequence. (Decisions that are strictly product/scope are marked ★.)

1. **Functional-profile authority.★** Who is the single writer/reader of the functional profile — `access_template_profiles` (N31, template-owned) or `internal_users.profile_title`?
   - Alternatives: (a) profile table is authority, `profile_title` becomes derived then removed; (b) keep `profile_title` as authority, retire the profile table/trigger; (c) status quo with three writers.
   - Recommendation: **(a)** — matches the N31 template-owned model and product authority.
   - Consequence: (a) touches login resolution + Admin writer consolidation (medium, scripted); (b) reverts N31 semantics; (c) divergence risk persists indefinitely.
2. **User→template single store.★** Which structure owns "user's one template": `internal_users.template_id` or `internal_user_access_templates`?
   - Alternatives: (a) `template_id` the single fact; junction becomes append-only assignment history; (b) `template_id` single fact; junction removed (metadata folded into `internal_users`); (c) junction single fact; `template_id` removed; (d) keep both with a DB sync guard.
   - Recommendation: **(a)** (history retained) or **(b)** if assignment audit is not required — both honour N31's "template_id = authority pointer".
   - Consequence: (a)/(b) converge to one write path (A12) with a reader switch; (c) is the highest-churn; (d) keeps the duplicate fact with an added sync mechanism.
3. **Enforce single-template at the Application edge.** Reject `templateIds.Count != 1` in `CreateUserAsync`/`ChangeTemplatesAsync`/`SaveUserAsync` (typed error), or keep the multi-template service path (which today produces an unhandled 23505 if a non-Web caller uses it)?
   - Recommendation: enforce exactly-one (the DB unique index stays as backstop).
   - Consequence: closes VAL-02 debt; no Web-visible change (forms already send one).
4. **Job On write surface scope.★** Is the Job On write family (create/duplicate/save-revision/transition/confirm-verification) in the shipped Web app (wire endpoints now), or explicitly deferred (keep repository-level, add no routes, mark dormant interfaces)?
   - Recommendation: keep repository-level for now (consistent at HEAD), but **prove it with real-PG lifecycle tests** and decide the endpoint scope with the product.
   - Consequence: wiring now exposes the most complex schema area early; deferring keeps latent risk but matches the current shipped surface.
5. **Job On dual audit.★** Should Job On emit compact `audit_events` rows (module `jobon`) for every mutation, alongside `job_on_audit_event`?
   - Recommendation: **yes (dual-emit, same UoW)** — closes the História gap without merging the two stores.
   - Consequence: yes → História shows Job On activity; no → the gap persists (document it as a known História limitation).
6. **`module_catalog_mirror` disposition.★** Keep as the documented Admin display read-model, or eliminate and read the compiled catalog directly?
   - Recommendation: keep as a read-model for now (Admin Applications already works and the mirror carries display order); revisit when Admin display needs it or does not.
   - Consequence: keep → a derived table with a synchronizer (A5); remove → one table + one repository + one service less, page reads code.
7. **`job_on_field_option` disposition.★** Wire the Job On Definições dropdown catalog (repository + API + page handler) or retire the table/domain record?
   - Recommendation: decision needed — the catalog pattern is sound and the UI exists visually, but there is zero persistence path; treat as **future-owned** unless the Definições surface is in the current roadmap.
   - Consequence: wire → new repository + endpoints; retire → drop table in Phase F; neither → dormant table (acceptable, documented).
8. **`tampao_planos` disposition.★** Wire the Planeamento tab + `/api/tampoes/plan*` endpoints (feature) or retire the planning surface and table?
   - Recommendation: **wire later** (dormant/future-owned is acceptable interim); the service/repo code is complete and tested, so the marginal cost of wiring is an endpoint block + JS.
   - Consequence: wire → live feature; retire → remove service methods + table in Phase F; keep dormant → no user-visible effect.
9. **`peso_comparacao_anterior` disposition.** Drop (recommended — empty, dead mirror) or materialize (write the previous-approved path)?
   - Recommendation: **drop** in Phase F; the live query is the authority.
   - Consequence: drop → cleaner schema; materialize → needs a writer + invalidation contract.
10. **Peso readings immutability.** Guard `peso_leituras` of approved controls (trigger or service rule)? Recommendation: **yes** (DB trigger mirroring `ba_dmo_guard_peso_approved`, or enforce in `UpdateControlAsync`). Consequence: strengthens the immutability contract; small write-path change.
11. **Dormant columns.** Remove `internal_users.modules_override` + `job_on_revision.image_asset_id` (recommended, Phase F), or keep as permanent auditability remnants? Consequence: removal needs code-projection cleanup first; keeping is harmless but documented.
12. **`pegamento_medicoes.contra_costura` nullability.** Column NOT NULL vs. domain one-sided measurement capability: align the column (make nullable + domain rule) or the domain (require both sides)?
    - Recommendation: align per the domain capability (nullable column + a domain-level completeness rule) — the domain explicitly supports one-sided measurements.
    - Consequence: unblocks the stated capability; schema change is a Phase-A CHECK/nullability migration.
13. **Audit co-transactionality policy.** Adopt "business write + audit row commit together" for every module (including Job On dual-emit) and migrate the post-commit emitters? Recommendation: yes (§9B B5). Consequence: audit-loss-on-partial-failure eliminated; touches several repositories' transaction shapes.
14. **Armazém 1:1-per-position invariant.** Is "at most one active occupation per position" a hard rule (then add per-position partial unique index or FOR UPDATE on the repair-return path — B2), or a soft convention? Recommendation: hard rule (matches GLM occupancy semantics; the repair path already opens a UoW — add the lock); schema-constraint and locking are separate decisions per ARMAZEM-01.
15. **RLS policy naming.** One convention (`ba_dmo_app_access`) everywhere vs. current mixed naming? Recommendation: unify in Phase A (rename `internal_user_access_templates_app_access`/`access_template_profiles_app_access` or document the divergence as accepted). Consequence: cosmetic; grep/tooling reliability.
16. **Consolidated baseline refresh.★** Approved to bring `consolidated_clean_install.sql` to the N31 final state (N31 objects + `article_reference_images` security stanza + corrected header + reproduced equivalence report) as part of the convergence Phase G? Recommendation: yes — without it, clean installs diverge from migration-built databases (currently: N31 absent → `TemplateProfileStore` fails loudly on 42P01; `article_reference_images` would be RLS-less). Consequence: refresh is non-destructive; delay keeps the documented drift.

---

## 12. Proposed Final Schema Shape

Not optimized for minimum table count. Final target per domain (61 − retired):

**Authoritative tables retained (by domain):**

| Domain | Authoritative tables |
|---|---|
| Access/Identity | `access_templates`, `access_template_profiles`, `internal_users` (+ optionally `internal_user_template_history`); `audit_events` (global); `module_catalog_mirror` *decision-gated* (keep-as-read-model default) |
| Job On | `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`, `job_on_audit_event`, `jobon_user_current`, `article_reference_images` (+ `job_on_field_option` *decision-gated*) |
| Controlo | `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events` |
| Peso | `peso_references`, `peso_lotes`, `peso_controlos`, `peso_leituras`, `peso_day_approvals`, `peso_settings` |
| Pegamentos | `pegamento_controlos`, `pegamento_medicoes`, `pegamento_documentos` |
| Ferramentas | `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences`, `tool_usage_records` |
| Reparação | `repairers`, `repairer_repair_types`, `line_repairer_defaults`, `repair_exits`, `repair_exit_items`, `repair_events`, `internal_repair_records` |
| Armazém | `warehouse_locations`, `warehouse_stock`, `warehouse_movements` |
| Tampões | `tampao_field_defs`, `tampao_field_values`, `tampao_configurations`, `tampao_saldos`, `tampao_movements`, `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event` (+ `tampao_planos` *decision-gated*) |
| Boquilhas | `bq_lotes`, `bq_traces`, `bq_movements`, `bq_discrepancies`, `bq_lifecycle_history`, `bq_utilisation_readings` |
| Shared | `app_settings` |

**Structures intended for eventual removal (Phase F, decision-gated):** `peso_comparacao_anterior` (drop); `internal_users.profile_title` (mirror); `internal_users.modules_override` (dormant); `job_on_revision.image_asset_id` (dormant); `internal_user_access_templates` as a 1:1 store (become history or removed — §5); optionally `module_catalog_mirror`, `tampao_planos`, `job_on_field_option` per decisions D-6/D-7/D-8.

**Major authority relationships (final):**

```
CanonicalModuleCatalog (code) ──(mirror)──> module_catalog_mirror [read-model]
CanonicalModuleCatalog (code) ──(grants)──> access_templates.modules
access_templates (1) ──(1:1)──> access_template_profiles.functional_profile   [profile authority]
access_templates (1) ──(1:1)──> internal_users.template_id                    [user's single template]
internal_users (1) ──(1..*)──> every operational table via actor FKs
job_on (1) ──(1..*)──> job_on_revision ──(1..*)──> job_on_component ──(1..*)──> fields/rows
job_on_revision (immutable) <──(FK)── peso_controlos / pegamento_controlos / controlo_sheets / internal_repair_records  [attribution anchors]
Every domain event stream (append-only) + audit_events (global compliance, dual-emit for Job On)
```

---

## 13. SAFE Implementation Order

No code, no SQL, no migrations — the exact order a future agent should execute the convergence.

1. **Re-baseline the evidence:** re-run a fresh-build N01–N31 on a disposable PostgreSQL and capture the true object inventory (the maps and the two prior reports were verified at earlier HEADs); reproduce the consolidated-equivalence check.
2. **Lock the product decisions (all of §11 that are product/scope).** Nothing below proceeds without D-1 (profile authority), D-2 (user→template authority), D-4 (Job On write scope), D-5 (dual audit), D-7/D-8 (field options / planos), D-12 (contra_costura), D-14 (Armazém invariant), D-16 (baseline refresh).
3. **Phase A (non-destructive DDL):** add the consistency guard between `template_id` and the junction; add the per-position occupancy invariant and the missing index; align `contra_costura`/`start_line` nullability per decisions; unify RLS policy naming. Prove with a real-PG migration-run test (currently missing — `MigrationRunnerTests` use a fake gateway; no file is ever executed against PostgreSQL).
4. **Phase B (report-only reconcile):** inventory divergence (profiles, single-assignment, mirror, dead tables) in report form; write fail-closed guards; do not fix data until writers are consolidated.
5. **Phase C (writer consolidation, application code first):** single profile writer (move `TemplateProfileStore` behind the Application repository, remove the user-level `profile_title` write, keep the sync inside one UoW); single user→template writer (reject multi-template at the service edge); stop capability persistence; Job On dual-emit in the same UoW.
6. **Phase D (reader switch):** `IdentityResolutionService` reads the template-owned profile; resolution reads `internal_users.template_id`; remove `profile_title` from the identity projection; update Admin user-list projection and self-lockout queries to the final authority.
7. **Phase E (parity soak):** equivalence guard-tests and report-only checks for one release cycle (profile equality, single assignment, audit completeness, mirror sync).
8. **Phase F (destructive, gated):** drop `peso_comparacao_anterior`, dormant columns, `profile_title`, and the 1:1 junction (or convert to history); retire `tampao_planos`/`job_on_field_option`/`module_catalog_mirror` per decisions; each drop has a row-count-zero guard and ships after Phase E green.
9. **Phase G (baseline):** refresh `consolidated_clean_install.sql` to the final state (N31 objects + `article_reference_images` security stanza + header) and add the consolidated-equivalence test that is currently missing.
10. **Close-out:** update the maps (02/03/16/15/19/20) and the two prior persistence reports to the new HEAD; remove stale doc references (`MigrationFile.cs` N01–N12 comment, `RemediationGuardTests` N01–N25 doc scope, the audit's stale findings that are now fixed).

---

## 14. Consolidated Install / Migration Path Audit (documented divergence — no edits)

`database/consolidated_clean_install.sql` (1,666 lines) — compared independently against the N01–N31 final state (migration ground truth):

| # | Divergence | Evidence | Required in the FINAL baseline |
|---|---|---|---|
| 1 | **N31 objects entirely absent** — no `access_template_profiles`, no `ba_dmo_ensure_access_template_profile()`, no `trg_access_templates_ensure_profile`, no `ux_internal_user_access_templates_actor`, no N31 backfill/collapse/sync DML | grep of the file: zero matches for `access_template_profiles`/`ba_dmo_ensure_access_template_profile`; N31 file contains all of them | Final baseline MUST include the N31 final state (table + trigger/function + single-assignment unique index + profile backfill/`profile_title` sync semantics or their Phase-A/B equivalents) |
| 2 | **`article_reference_images` (N29) created without RLS/policy/grants** — the table + constraints + N30 index exist (lines 452-470) but the table is in neither `rls_tables` nor `policy_tables` arrays, and there is no `ba_dmo_app` GRANT for it; the consolidated file otherwise mirrors N29's security stanza only in the migration | verified: only three RLS-enable blocks exist (N12/N25/N27 sections); `article_reference_images` absent from all lists; migration N29 has RLS+policy+GRANT inline | Final baseline MUST include RLS enable + `ba_dmo_app_access` policy + GRANT for `article_reference_images` (a consolidated-built DB today has an RLS-less, un-granted table — a real Supabase default-privilege exposure) |
| 3 | **Header/provenance stale** — claims "migration family N01 … N24", references the old test name `ShippedFreshBuildFamily_IsComplete_N01ThroughN24` (now `…_N01ThroughN31`), and the trailing comment says "includes N25-N27" while the body already contains N28 (static CHECK) and N29/N30 objects | file lines 4-29 and 1666; map 03_MIGRATIONS.md documents the same | Final baseline header MUST state the exact migration horizon it reproduces (N01–N31+Phase-A/B) and the equivalence test name must agree |
| 4 | **N27/N28/N29 data-reconciliation DML omitted** (profile inference, `legacy-override-*` materialization, junction backfill, `modules` rewrite, `modules_override=NULL`, N28 fail-closed guard, N29 promotion guards) | baseline lines 1618-1664 vs N27:19-111, N28:14-35, N29:31-137 | Harmless on an empty install (no rows), but the file claims "reproduce the final effective schema"; decide whether guards are reproduced for parity/self-checking |
| 5 | **Supabase-hosted adaptations** (guarded role creation, default-privilege suppression, guarded grants/policies) | lines 37-63, 1273-1319 | Intentional (documented compatibility) — keep |
| 6 | Section numbering out of order (19/20/23/24 before 12) + mixed comment trails; `schema_migrations` reproduced | cosmetic | Clean up during the Phase G refresh |

**Final consolidated baseline SHOULD contain:** N01–N31 final state + any Phase-A/B converged objects; `article_reference_images` RLS/policy/grants; the N31 access-model objects; an accurate header/provenance; and a reproduced equivalence report (the referenced `reports/consolidated_schema_equivalence.md` is **not present** in the repository). **No edits were made in this phase.**

Live database: **validation UNAVAILABLE** — no connection envelope in this session, no local listener, no dump artifact. Migration-derived counts (61 application tables + `schema_migrations` = 62) reconcile exactly with the previously observed live inventory, but live contents/provenance (`schema_migrations` vs any Supabase CLI history) remain **UNVERIFIED** and require the owner to run the migrate CLI / inventory query before Phase A.

---

## 15. Closing Verification (task requirements)

- **Output file created:** `reports/schema_rationalization_target_architecture.md` (this file). No other file was modified.
- **Number of tables classified:** **61** (all application tables; `schema_migrations` is the runner bookkeeping table, not an application table).
- **Count per classification:** KEEP **56** · KEEP_NORMALIZED **1** (`internal_user_access_templates`) · MERGE **0** · MIRROR_ELIMINATE **1** (`peso_comparacao_anterior`) · LEGACY_REMOVE_LATER **0** · ORPHAN_CANDIDATE **0** · DERIVED_NOT_AUTHORITY **1** (`module_catalog_mirror`) · NEEDS_OWNER_DECISION **2** (`job_on_field_option`, `tampao_planos`).
- **Duplicate-authority candidates:** **8** core candidates, of which 3 are live competing writers (`internal_users.profile_title` vs `access_template_profiles`; `internal_users.template_id` vs junction), 4 are dormant/inert mirrors (`modules_override`, `job_on_revision.image_asset_id`, `peso_comparacao_anterior`, legacy `capabilities[]` + `module_catalog_mirror` as a code mirror), and 1 is a missing-contract gap (Job On ⇄ `audit_events` dual-emit). Full column-level detail in §4.
- **Owner decisions:** **16** (listed in §11).
- **Live database validation status:** **UNAVAILABLE from this session** (no connection string env vars, no local PostgreSQL listener, no dump artifact); migration-derived count reconciliation only.
- **Change confirmation:** **No source code, migration, schema, constraint, or database object was modified; no DDL/DML executed; no Supabase writes; no N32 created; no commit or push performed.** Historical migrations N01–N31 remain immutable; the future implementation strategy is N32+ additive convergence per §10, with the consolidated baseline refreshed last (Phase G).