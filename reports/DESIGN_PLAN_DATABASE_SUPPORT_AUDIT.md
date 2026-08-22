# Design Plan — Database Support Audit (READ-ONLY)

**Date:** Current audit pass
**Mode:** Audit only — no database, migrations, schema, application code, tests, or Git were modified.
**Workspace:** `D:\BA-DMO-RECOVERY`
**Primary authority:** `design_plan_FINAL.md` + `AI-CONTEXT\design-coder` package + `FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`
**Current schema evidence:** `database\migrations\N01–N26` (sequential application = current persisted model)

> The current schema is treated as **evidence**, never as business authority. The
> authority order is: (1) functional rules → (2) design plan/package → (3) current
> application → (4) current database.

---

## 0. How to read this audit

This audit answers, for every DES task and module: *does the current database contain the
data, relationships and persistence capability required to implement the designed page
correctly?*

A critical discipline was applied throughout (per the brief §4):
- **QUERY/DTO GAP** is claimed only when the fact already exists in the database but the
  application does not expose it.
- **SCHEMA GAP** is claimed only when the required fact **cannot be represented safely**
  in the current persistence model, and each is proven with (a) the design requirement,
  (b) the functional rule, (c) why existing tables cannot represent it, and (d) why a
  read-model change alone is insufficient.

**High-level conclusion up front:** the final design plan is overwhelmingly a
*presentation recomposition* (`RESTRUCTURE` of Razor/CSS/JS) that **reuses current services,
routes and data contracts** (design_plan_FINAL §3, §5: tasks repeatedly state *"do not
redesign persistence/schema"*, *"minimal display/read DTOs"*, *"only after the gap is
proven"*). The current database (N01–N26) is mature and supports nearly every module now.
There is essentially **one** schema change with a direct design-task dependency — the Job On
**master article/reference audit + reference-scoped image** (Q-002) — plus several
*freeze-only / clean-baseline* schema refinements and a small number of pure read/DTO gaps.

---

## 1. Critical resolution of the two design "owner questions"

`design_plan_FINAL.md` lists two isolated owner questions (Q-001, Q-002). The **current
design-coder package already resolves both** (`00_READ_FIRST.md`; owner-decision files).
This is important because it changes what the database must support:

| Question | `design_plan_FINAL.md` wording | **Resolved in package** | Implication for DB support |
|---|---|---|---|
| Q-001 — Ferramentas SAP Utilização UI | "affects only whether the existing backend is exposed" | `30_FERRAMENTAS\03_OWNER_DECISION_SAP_UTILISATION.md`: **activate the Utilização UI in this test version** | Manual-only, append-only. Backend (`tool_usage_records`, N19) and repos exist. **No schema change** — read/write already supported. |
| Q-002 — Job On article image ownership | "affects only the persistence meaning of the article image" | `20_JOB_ON\08_OWNER_DECISION_ARTICLE_IMAGE.md`: image **belongs to the master article/reference**, chosen from company-server directory, associated with the reference; Job On print consumes it; only the required print sheet displays it; **do not model a per-revision image** | Current schema stores `job_on_revision.image_asset_id` and has no master reference entity → **this is the one real schema-level dependency** (see Job On section). |

---

## 2. Database support per module

For each module: DES TASK → DESIGN DATA REQUIRED → CURRENT TABLES → CURRENT COLUMNS →
CURRENT RELATIONSHIPS → CURRENT WRITE SUPPORT → CURRENT READ SUPPORT → CURRENT
HISTORY/AUDIT SUPPORT → CURRENT SNAPSHOT SUPPORT → MISSING DATA → MISSING RELATIONSHIPS →
MISSING CONSTRAINTS → APP QUERY GAP → SCHEMA GAP → CLASSIFICATION.

---

### 2.1 Shell (DES-002)

- **DES TASK:** Normalize header/navigation anatomy, two-level navigation, capability-driven nav, pure-admin isolation.
- **DESIGN DATA REQUIRED:** No persistent domain data — reads the current user's granted modules for navigation.
- **CURRENT TABLES:** `access_templates`, `internal_users`, `internal_users.modules_override` (N26), `module_catalog_mirror` (N02), `audit_events` (N01, for audit only).
- **CURRENT COLUMNS:** `access_templates.modules` (jsonb grants), `internal_users.template_id`, `.profile_title`, `.active`, `.auth_user_id`, `.modules_override` (jsonb).
- **CURRENT RELATIONSHIPS:** `internal_users.template_id → access_templates.template_id`; `auth_user_id` logical to Supabase Auth (no FK, by design).
- **CURRENT WRITE SUPPORT:** n/a (shell reads).
- **CURRENT READ SUPPORT:** `AccessResolver`, `NavigationService`, `PageCatalog` resolve granted modules server-side.
- **HISTORY/AUDIT:** `audit_events` (module `login`/`session` action records).
- **SNAPSHOT:** n/a.
- **MISSING:** none.
- **CLASSIFICATION:** **SUPPORTED NOW.**

### 2.2 Login (DES-003)

- **DES TASK:** Align the split login composition (remove test notice, tune states), preserve antiforgery + auth flow.
- **DESIGN DATA REQUIRED:** authentication (Supabase), redirect routing to Job On / Admin by capability; profile title display.
- **CURRENT TABLES:** `internal_users`, `access_templates`; Supabase Auth (`auth.users`) via `SupabaseAuthAdapter`/`SupabaseAdminProvisioningAdapter`.
- **CURRENT COLUMNS:** `internal_users.actor_id`, `.auth_user_id` (NOT NULL + UNIQUE via N25), `.template_id`, `.display_name`, `.profile_title`, `.active`.
- **RELATIONSHIPS:** template FK; auth identity mapping.
- **WRITE/READ:** `IdentityResolutionService`, `BootstrapAdminService`, auth adapter.
- **HISTORY/AUDIT:** `audit_events`.
- **MISSING:** none. Password/reset lives in Supabase Auth (external, not a BA DMO table).
- **APP QUERY GAP:** none material; profile-title is display-only and already stored.
- **CLASSIFICATION:** **SUPPORTED NOW.**

### 2.3 Admin (DES-004)

- **DES TASK:** Recompose Users / Templates / Applications / Audit as a dedicated admin workspace; append-only audit; never reveal passwords; profile-title display-only.
- **DESIGN DATA REQUIRED:** users, access templates + capabilities, per-user overrides, audit browsing.
- **CURRENT TABLES:** `internal_users`, `access_templates`, `module_catalog_mirror` (N02), `audit_events` (N01), `internal_users.modules_override` (N26).
- **CURRENT COLUMNS:** as §2.1/§2.2; `module_catalog_mirror.module_id/display_name/display_order/active`.
- **RELATIONSHIPS:** users→template; users→auth (logical); mirror is a UI-ordered catalog (never grants).
- **WRITE SUPPORT:** `AdminUserService`, `AdminTemplateService`, `AdminMirrorService`, `AdminAuditService`, idempotent user creation.
- **READ SUPPORT:** full admin read paths.
- **HISTORY/AUDIT:** `audit_events`.
- **MISSING:** none (no password stored in BA DMO DB — Supabase Auth owns credentials; reset never reveals).
- **APP QUERY GAP:** known cosmetic `X12` item — "Admin Users list shows auth UUID under an Email column"; the *value exists* (auth_user_id) but the label/read DTO should project a human display. **Query/DTO gap, not schema.**
- **CLASSIFICATION:** **SUPPORTED NOW** (one cosmetic read-DTO label gap).

### 2.4 Job On (DES-005) — **core schema-change module**

- **DES TASK:** Recompose the Job On planning/operational sheet, consultation/edit modes, exact immutable revisions, per-user current-open context, four-page print, and the **article image** per Q-002.
- **DESIGN DATA REQUIRED:**
  - master **article/reference identity** + reference-scoped **image association** (Q-002)
  - exact **Job On revision snapshots** (immutable), components/families, typed fields, CAL rows, verification occurrences
  - **Job On current-open per user** (R011)
  - image consumed by print (only required sheet)
  - history/revisions; exact production/machine/reference context
- **CURRENT TABLES:** `job_on`, `job_on_revision`, `job_on_component`, `job_on_component_field`, `job_on_component_row`, `job_on_verification_occurrence`, `job_on_audit_event`, `job_on_field_option` (N05); `job_on.production_folder` (N13); `jobon_user_current` (N24); append-only + lifecycle constraints (N25); `tool_check_rules`/`tool_check_occurrences` (N04).
- **CURRENT COLUMNS:**
  - `job_on`: `job_on_id`, `production_code`, `article_reference_id` (nullable uuid, **no FK**), `article_reference_snapshot` (**jsonb**), `machine_code`, `planned_start_at/end_at`, `status`, `current_revision_id` (FK to revision), `copied_from_job_on_id`, production folder, closed/canceled facts.
  - `job_on_revision`: `revision_number`, `production_snapshot/reference_snapshot/machine_snapshot/dates_snapshot/type_snapshot/stop_snapshot/weight_snapshot/process_snapshot` (**all jsonb**), `sections` (jsonb), `drop_count`, `general_notes`, `image_asset_id` (**text — revision-scoped**), `change_reason`, `saved_by/at`.
  - `job_on_component`: family, `source_tool_id`(FK `tool_references`), `source_lot_id`(FK `tool_lotes`), reference/lot/technical-name snapshots, planned_quantity, stock_snapshot, usage_snapshot, notes, display_order.
  - `job_on_component_field`: `field_key`, `value_type`, typed value columns (text/integer/decimal/boolean/date).
  - `job_on_component_row`: `element_label`, `value_decimal`, `value_text`, `unit`, `machine_quantity`, display_order (no `row_code` column).
  - `job_on_verification_occurrence`: `source_rule_id`(FK), `rule_text_snapshot`, status, `completion_source='manual_job_on'`, completed_by/at; N25 completion consistency.
  - `jobon_user_current`: `actor_id` (PK), `job_on_id`(FK), production/reference/machine snapshot, opened_at.
- **CURRENT RELATIONSHIPS:** revision→job_on; component→revision; component→tool_references/tool_lotes; field→component; row→component; occurrence→component + tool_check_rule; `job_on.current_revision_id → job_on_revision`; `jobon_user_current.job_on_id → job_on`. Append-only on all four revision-family tables (N25 INT-10).
- **CURRENT WRITE SUPPORT:** `JobOnService`, `DapperJobOnRepository` (save creates new revision; no destructive UPDATE), duplication, verifications.
- **CURRENT READ SUPPORT:** full aggregate read; current-open per user via `IJobOnUserContextRepository`; active-context lookups for Peso/Pegamentos/Controlo/R.Interna.
- **HISTORY/AUDIT:** `job_on_audit_event` (append-only) + global `audit_events`; N25 append-only on revision row/component/field/row.
- **SNAPSHOT SUPPORT:** **excellent** — immutable revision + full component/field/row graph.
- **MISSING DATA / RELATIONSHIPS / CONSTRAINTS:**
  - **(A) No master article/reference entity.** `job_on.article_reference_id` is a loose nullable uuid with **no FK and no parent table**. There is no `references`/`articles` master table. The design's `04_DATA_CONTRACT_JOB_ON.md` recommends `article_reference_id` FK to a "Referência mestre", and the Job On history filter is by `article_reference_id` (when present) plus `article_reference_snapshot`.
  - **(B) Article image is revision-scoped, contradicting Q-002.** `image_asset_id` lives on `job_on_revision`; the app's `FileSystemJobOnImageProvider` resolves it from `current_revision.image_asset_id` + `job_on.production_folder`. Q-002 requires the image to be **reference-scoped** ("do not model a per-revision image"). Because there is no master-reference row to hang the image associative fact from, a read-model/DTO change alone **cannot** satisfy Q-002.
  - **(C) The design contract wants typed, queryable revision snapshot columns** (`production_code_snapshot`, `machine_code_snapshot`, `reference_snapshot`, `start/end_at_snapshot` …) but N05 stores these as **jsonb**. It *works* (the app reads them via `JsonDocument`), but it is not clean-relational, and the Job On history filter by reference would be more robust with a stable typed reference identity. This is a freeze/clean-baseline concern, not a test-version blocker (the app already reads them).
  - **(D) `job_on_component_row` lacks `row_code`** that the data contract mentions (stable code when it exists). Presentational/optional.
- **APP QUERY GAP:** none for the core sheet (fully hydrated read exists). The design-plan flagged "add only a proven query/DTO for missing live tool context" — this is a live-tool-state decorator (state/location from Ferramentas/Armazém), not a schema gap.
- **SCHEMA GAP:** **(A)+(B)** require a representation change (a master reference entity with a reference-scoped image association, or an equivalent stable reference row). This is the **only schema change that directly blocks a specific DES task** (the Q-002 image surface of DES-005). **(C)/(D)** are clean-baseline refinements.
- **CLASSIFICATION:** **SCHEMA CHANGE REQUIRED** (specifically: master article/reference identity + reference-scoped image). All other Job On surfaces are **SUPPORTED NOW**.

### 2.5 Controlo (DES-006)

- **DES TASK:** Unified production workspace: one active-production card binds Resumo/Peso/Comparação/Pegamentos/Histórico tabs to the same **exact current-open Job On** and revision; free-mode consultation without a fake production.
- **DESIGN DATA REQUIRED:** exact `job_on_id` + `job_on_revision_id`; snapshot of production components; per-component OK/NOK + observation + manual MCaliper link; draft→submitted→approved/rejected with reopen (append-only history); free-mode read-only consultation; transverse history documents (Resumo/Peso/Pegamentos).
- **CURRENT TABLES:** `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events` (N23); `jobon_user_current` (N24); `peso_controlos`/`peso_leituras`/`peso_day_approvals`; `pegamento_controlos`/`pegamento_medicoes`/`pegamento_documentos`; `audit_events`.
- **CURRENT COLUMNS:** `controlo_sheets`: `job_on_id`, `job_on_revision_id` (both FK, NOT NULL), production_code, reference, machine_code, display_id, status (`draft/submitted/aprovado/rejeitado`), created/submitted/decided facts, decision.
  - `controlo_sheet_items`: family, source_tool_id/source_lot_id (FK), ref/lot/tech-name snapshots, `result` (OK/NOK), `observation`, `mcaliper_link` (text, single field).
  - `controlo_sheet_events`: type (criar/editar/submeter/reeabrir/decidir), actor, before/after jsonb, note; **append-only trigger**.
- **CURRENT RELATIONSHIPS:** sheet→job_on + job_on_revision (exact anchor); items→sheet (CASCADE); events→sheet.
- **WRITE SUPPORT:** `ControloSheetService`, `IControloSheetRepository` (transactional insert of sheet+items; coordinated update).
- **READ SUPPORT:** `DapperControloProductionContextLookup`, `GetForProductionAsync`, `ListAsync` (from/to/machine/jobOn/status). Free-mode read endpoints exist.
- **HISTORY/AUDIT:** `controlo_sheet_events` (append-only) + `audit_events`.
- **SNAPSHOT SUPPORT:** sheet items snapshot the pinned revision's components at creation.
- **MISSING DATA:** The **MCaliper link** design (21_CONTROLO\02) wants a per-result *history* of added/updated/removed actions (append-only events per link). Currently `controlo_sheet_items.mcaliper_link` is a **single updateable text field** and only the sheet-level `controlo_sheet_events` is append-only — there is no per-result link fact table / link-change ledger. This is a **minor additive** need (the current `before/after` event could carry it, or a small link-event table).
- **APP QUERY GAP:** free-mode consultation currently renders "empty messages" in the design review; the underlying read endpoints exist — the UI recompose can call them. Peso/Pegamentos tabs are bound by the shared revision (already persisted). Not a schema gap.
- **SCHEMA GAP:** none blocking. Optional additive: per-result MCaliper-link append-only ledger (clean-baseline/minor).
- **CLASSIFICATION:** **SUPPORTED NOW** (minor additive for MCaliper-link history).

### 2.6 Peso — Operador (DES-007)

- **DES TASK:** Recompose the operator sheet & per-CM comparison (glass-weight only, no capacity/water/global average), server-only calculations, exact revision, sequential submit/approval, comparison by explicit `CM atual → CM anterior` pairs.
- **DESIGN DATA REQUIRED:** exact `job_on_revision_id` binding; per-CM readings (raw), server-calculated `Peso do vidro = CM + BQ − PU`; explicit previous-production comparison (per-CM: current weight, previous weight, delta, variation, author, timestamp); approval/decision state; immutable approved snapshot; document/history.
- **CURRENT TABLES:** `peso_references`, `peso_lotes`, `peso_controlos`, `peso_leituras`, `peso_comparacao_anterior`, `peso_day_approvals`, `peso_settings` (N06); approved-immutability trigger + consistency checks (N25).
- **CURRENT COLUMNS:**
  - `peso_controlos`: `job_on_id` + `job_on_revision_id` (**both FK NOT NULL**), `record_type`, `mold_number/neckring_number`, `production_code`, `line`, `lote`, `control_date`, `cm_snapshot` (jsonb), `status`, `measurements_snapshot` (jsonb), `approval_log` (jsonb), `previous_control` (jsonb), `comparison_decisions` (**jsonb**), approved_by/at; UNIQUE identity; state machine; N25 approved-guard.
  - `peso_leituras`: `cm_number`, `readings` (jsonb), UNIQUE(controlo, cm).
  - `peso_comparacao_anterior`: `previous_peso_controlo_id`(FK), `previous_snapshot` (jsonb), `deltas` (jsonb) — cross-line previous-approved read path.
  - `peso_day_approvals`: daily approvals, UNIQUE(mold, neckring, line, date).
  - `peso_settings`: NNPB/PS constants, recipients, folder.
- **RELATIONSHIPS:** revisão-pinned; lote→reference; control→revision; leitura→control; previous→control; day-approval.
- **WRITE/READ:** `PesoService`, `DapperPesoRepository` (server-side `WeightCalculator`, density lookup, previous resolution).
- **HISTORY/AUDIT:** `approval_log`, `change_log`, `audit_events`; approved controls immutable (N25 trigger).
- **SNAPSHOT SUPPORT:** structured snapshot (`measurements_snapshot`, `cm_snapshot`); approved snapshot immutable.
- **MISSING DATA:** The comparison decision (22_PESO\04_OWNER_DECISION_GLASS_COMPARISON) requires per-CM explicit pairing (current CM ↔ previous CM, both weights, delta, variation, author, timestamp) persisted relationally. Current `comparison_decisions` is **jsonb** and `peso_comparacao_anterior` resolves/denormalizes the previous control. It **can** represent the required fact, and the app reads/writes it, but it is not a clean relational per-CM table. This is a **clean-baseline refinement** (relational table would better serve the design's filtered comparison history), **not a test-version blocker** — the data is fully present and resolvable via the saved engine results.
- **APP QUERY GAP:** the operator comparison sheet needs to read the current control's saved per-CM `Peso do vidro` results and the explicit previous-CM pairings — both already stored; only read DTO/UI surfacing is needed.
- **SCHEMA GAP:** none blocking. Optional clean-baseline: promote `comparison_decisions` to a relational per-CM pair table.
- **CLASSIFICATION:** **SUPPORTED NOW** (comparison pairing via jsonb works; possible clean-baseline relational refinement).

### 2.7 Peso — Responsável (DES-008)

- **DES TASK:** Daily calendar/list/detail + decision composition; per-CM decisions; justification when any CM is put aside; all-CM decision completeness; send-to-production; immutable approved original.
- **DESIGN DATA REQUIRED:** same immutable server results as operator; individual CM decisions; approval history; daily view (date → controls); job_on/revision identity.
- **CURRENT TABLES:** same Peso family; `peso_day_approvals`, `peso_controlos.status/approval_log/approved_by/approved_at`, `comparison_decisions`.
- **WRITE/READ:** `PesoService` responsible flows; approval/decision persistence.
- **HISTORY/AUDIT:** `approval_log`, N25 approved-guard.
- **MISSING:** individual CM decision with mandatory justification when a CM is set aside — representable in `comparison_decisions` jsonb / `approval_log`. Same clean-baseline note as §2.6.
- **CLASSIFICATION:** **SUPPORTED NOW.**

### 2.8 Pegamentos (DES-009)

- **DES TASK:** Bind creation to active context (no manual revision-ID entry), inherited CM/BQ/MF (not reselectable), server-only calculations, ±0.20 boundary, one immutable final PDF.
- **DESIGN DATA REQUIRED:** exact `job_on_revision_id` (immutable); inherited CM/BQ/MF context; measurement facts (append-only); tolerance/result state; **final document metadata persisted exactly once**.
- **CURRENT TABLES:** `pegamento_controlos` (N07 + N16 nominals + N17 notas), `pegamento_medicoes` (N07 + N15 tool_number), `pegamento_documentos` (N14).
- **CURRENT COLUMNS:** `pegamento_controlos.job_on_id` + `job_on_revision_id` (**FK NOT NULL**), `reference_snapshot` (jsonb), production/machine, `cm_snapshot/bq_snapshot/mf_snapshot` (jsonb), `nominal_average`, `cm/bq/mf_nominal` (N16), `tolerance` (default 0.20, ≥0), status (`aberto/fechado`, N25 check), `notas` (N17). `pegamento_medicoes`: component_key, costura, contra_costura, `tool_number` (N15), actor; **append-only trigger**. `pegamento_documentos`: **UNIQUE(pegamento_controlo_id)**, filename, output_root/production_folder snapshots — final document persisted once (`ON CONFLICT` per functional rule).
- **RELATIONSHIPS:** control→revision (exact); medical→control; document→control (1:1 unique); medical append-only.
- **WRITE/READ:** `PegamentoService`, `DapperPegamentoRepository`, `PegamentoPdfRenderer` (server DB), `IJobOnProductionContextLookup` inherits context.
- **HISTORY/AUDIT:** medicoes append-only, `audit_events`, document frozen-once.
- **SNAPSHOT SUPPORT:** structured frozen snapshot; `pegamento_documentos` = immutable final doc metadata.
- **MISSING:** none. The design's tolerance-±0.20 and "persist once" are already the persisted default and DB-unique enforcement.
- **APP QUERY GAP:** the bound-context read for creation (inherited CM/BQ/MF from the pinned revision) is provided by the production-context lookup; minor display DTO for human-readable context.
- **CLASSIFICATION:** **SUPPORTED NOW.**

### 2.9 Ferramentas (DES-010) — read/DTO gap module

- **DES TASK:** Unified reference list + five-tab workspace (Referência/Lotes/Verificações/Utilização/Histórico); create-reference/first-lot + duplicate-lot; current-state/location, usage history, verification history; Utilização active per Q-001.
- **DESIGN DATA REQUIRED:** CM/MF master reference; lote identity; technical data; verification/check history; **manual SAP utilisation records** (append-only); **current state/location**; utilisation history.
- **CURRENT TABLES:** `tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences` (N04); `tool_usage_records` (N19).
- **CURRENT COLUMNS:** ref (tool_type CM/MF/BQ/PU/CS, ref_code, technical_name, owner_plant, UNIQUE type+code); lote (lote, qty, allowed_lines, drawing_code/revision, processo); piece (sequence, number, status, UNIQUE lote+number); check rules (rule_text, frequency, active, copied_from); occurrences (status, manual source); usage (`tool_usage_records`: `sap_start`, `sap_end`, `percent_used` 0–100 **manually entered**, `value_added`, `value_cumulative`, notes, actor; **append-only trigger**).
- **RELATIONSHIPS:** piece→lote; lote→reference; rule→lote; occurrence→rule; usage→lote; warehouse location via `warehouse_stock.tool_lote_id` (Armazém-owned).
- **WRITE/READ:** `FerramentasService`, `DapperFerramentasRepository` (references/lotes/pieces/check-rules/usage/audit), `IFerramentasIdentityLookup` (read-only).
- **HISTORY/AUDIT:** usage append-only; check occurrences; `audit_events`.
- **MISSING DATA:** none — SAP utilisation fully supported (manual fact, append-only, no formula; exactly the Q-001 rule).
- **APP QUERY GAP:** **the Ferramentas read model does not expose the tool's current warehouse location/state.** `IFerramentasRepository`/`IFerramentasIdentityLookup` return reference/lot/type/technical-name but not the active `warehouse_stock` occupation (which exists via `tool_lote_id`). The Ferramentas **Utilização/current-location** surface and the Armazém alert both need this projection. This is a **pure query/DTO gap** (data already in DB), matching the design-plan's own note: *"minimal read DTOs for current location/status if proven absent"* (DES-010).
- **SCHEMA GAP:** none.
- **CLASSIFICATION:** **SUPPORTED WITH READ QUERY/DTO ONLY** (current-location/status projection).

### 2.10 Armazém (DES-012)

- **DES TASK:** Inline CM/MF entry/exit, search, operational alerts (SAP-usage consumed for alert), consultation/history/correction; defer out-of-scope BQ/programmed-external behavior; Armazém sole owner of positions/movements.
- **DESIGN DATA REQUIRED:** physical location; **one active occupation**; movement history; current tool location; **consume stored SAP utilisation for alert display**.
- **CURRENT TABLES:** `warehouse_locations`, `warehouse_stock`, `warehouse_movements` (N09).
- **CURRENT COLUMNS:** location (code UNIQUE, kind); stock (tool_lote_id FK, occupied_since/by, released_at/by; **partial unique index `uq_warehouse_stock_active_occupation(location, tool_lote) WHERE released IS NULL` enforcing one active occupation**); movements (direction in/out, qty, destination, `repair_exit_id` FK, actor; **append-only trigger**).
- **RELATIONSHIPS:** stock→location + tool_lote; movement→stock / repair_exit; one-active-occupation DB constraint.
- **WRITE/READ:** `ArmazemService`, `DapperArmazemRepository`, `FerramentasArmazemToolIdentityResolver`, `IToolIdentityResolver` (Armazém-owned read-only identity), `DapperArmazemRepairMovementRepository`.
- **HISTORY/AUDIT:** movements append-only.
- **MISSING:** none. "fora" is derived (never stored — good). Consuming SAP usage for an alert reads `tool_usage_records` (Ferramentas owner) read-only — data exists.
- **APP QUERY GAP:** the **alert consumption** (paste_latest `percent_used` from `tool_usage_records` when a tool enters storage) is exactly the read projection the Armazém alert card needs; currently only implicitly available via the Ferramentas usage read — the Armazém read surface should expose latest-usage-per-lote. **Query/DTO**, not schema.
- **SCHEMA GAP:** none. (Functional hardening: the app check-then-insert occupation has a TOCTOU risk — the DB partial unique index now closes it at the DB level; the design still may prefer `SELECT … FOR UPDATE`/`ON CONFLICT` for clean messaging.)
- **CLASSIFICATION:** **SUPPORTED NOW** (alert card = read/DTO).

### 2.11 Boquilhas (DES-011)

- **DES TASK:** Recompose with canonical sidebar, inline lot creation, movements, discrepancy, line-repairer matrix, history; preserve BQ independence; 20→25 non-blocking excess.
- **DESIGN DATA REQUIRED:** BQ identity/lotes; movements; discrepancy (20→25); repairer relation; lifecycle/history; external-repair separation.
- **CURRENT TABLES:** `bq_lotes`, `bq_traces`, `bq_movements`, `bq_discrepancies`, `bq_lifecycle_history`, `bq_utilisation_readings` (N03); `bq_movements.noted_repairer_id` (N18); one-active-trace partial unique (N25); Ferramentas `tool_references` BQ identity (separate, by design).
- **CURRENT COLUMNS:** lote (reference regex`^[A-Z][0-9]{3}$`, batch, UNIQUE); trace (status active/closed, purpose production/repair, start_line, sap_start/end, reopen_history/deleted_movements jsonb); movement (type inicio/saida/entrada/irreparavel/linha/contagem/fim, qty, `exceptional_received_qty` 20→25, line, notes, actor; append-only); discrepancy (expected/actual/excess, status open/under_review/resolved, resolution_note); lifecycle history; utilisation readings (initial/final).
- **RELATIONSHIPS:** trace→lote; movement→trace; discrepancy→lote(+trace); lifecycle→lote; N18 movement→repairer; allowed-lines.
- **WRITE/READ:** `BoquilhasService`, `DapperBoquilhasRepository`, `IBoquilhasUnitOfWorkFactory`.
- **HISTORY/AUDIT:** append-only movements/traces/lifecycle/utilisation; reopen_history.
- **MISSING:** none. No live Job On lookup by design (owner D2). BQ external flow stays separate.
- **SCHEMA GAP:** none.
- **CLASSIFICATION:** **SUPPORTED NOW.**

### 2.12 Tampões (DES-013)

- **DES TASK:** canonical 5-tab composition; inline transformation/state/config editors; recent movements; fold line/machine into designed surfaces; append-only, derived balances, atomic updates; optional read-only Job On link; planning ≠ reserving.
- **DESIGN DATA REQUIRED:** configuration; machine association; movements; balances; planning; notes/history.
- **CURRENT TABLES:** `tampao_field_defs`, `tampao_field_values`, `tampao_configurations` (values_json UNIQUE), `tampao_saldos`, `tampao_movements` (N10); `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event` (N21); `tampao_planos` (N10).
- **CURRENT COLUMNS:** config values_json; saldos (enchidos/por_encher ≥0, no third state); movements (type adicionar/remover/alterar_estado/alterar_configuracao, origin/destination config, qty, balances_before/after jsonb, actor; append-only); machines (config/machine PK, B1–C3 CHECK); notes append-only; machine-event append-only; planos (config, qty, date, optional `job_on_id` **plain uuid no FK** = optional read-only Job On link, canceled, notes).
- **RELATIONSHIPS:** saldo→config (1:1 unique); movement→origin/dest config; machine/note/event→config; plano→config (+optional job_on uuid).
- **WRITE/READ:** `TampaoService`, `DapperTampaoRepository`, `ITampoesUnitOfWorkFactory` (atomic transformations).
- **HISTORY/AUDIT:** movements/notes/machine-events append-only.
- **MISSING:** none — machine association (N21) exists; planning is non-reserving (planear≠reservar); balances derived from movements.
- **APP QUERY GAP:** the line/machine detail is present in DB (N21) but currently surfaced under an extra primary tab; the DES-013 recompose folds it into designed surfaces — a presentation read, data exists.
- **SCHEMA GAP:** none. (Functional hardening A4: `SetSaldoAsync` absolute-rewrite lost-update risk — the design requires atomic deltas/row-lock; data model supports it, the app must use delta writes.)
- **CLASSIFICATION:** **SUPPORTED NOW.**

### 2.13 Reparação Interna (DES-014) — **schema-vs-rule conflict**

- **DES TASK:** Remove BQ as a repair type everywhere; CM/MF only; preserve the complete reference (e.g. `5447T173`, `T173` context-only); repeated individual numbers valid; append-only corrections with recalibrated line context.
- **DESIGN DATA REQUIRED:** CM/MF-only representation; complete production reference context (`5447T173`); Job On/revision context; repeated individual numbers; append-only corrections; no BQ repair type.
- **CURRENT TABLES:** `internal_repair_records`, `repair_events` (N08); production-context columns + revision FK (N22).
- **CURRENT COLUMNS:** `internal_repair_records`: line, `job_on_id` (plain uuid logical), `job_on_revision_id` (**FK, nullable**, N22), `tool_type`, `individual_number`, operator, occurred_at, `correction_of_id`(FK self), `before_snapshot`, `correction_reason`, `production_code`/`reference`/`lot_id` (N22, nullable snapshots).
- **CRITICAL ISSUE — constraint reversal (N22):** The **original** N08 constraint was `ck_internal_repair_records_type CHECK (tool_type IN ('CM','MF'))`. **N22 drops and re-creates it as `('CM','MF','BQ')`** and the file header states "BQ is a THIRD recordable type (CM | MF | BQ)". This **directly contradicts the current functional authority** (`FUNCTIONAL_RULES_SOURCE_OF_TRUTH §9`: *"Reparação Interna repairs only CM and MF. BQ is not repairable, selectable, or processed."*) and the design plan (DES-014 / DES-018).
  - The app surface may already gate BQ out in the new design, so this **does not block the DES-014 UI recomposition**, and it **does not lose historical rows** (CM/MF remain valid).
  - However, the persisted CHECK now *permits* a fact the functional authority forbids; the schema is in **contradiction** with the settled rule (CM/MF only), not an unresolved product decision. It must be restored to `('CM','MF')` for the clean/final baseline. This is a **KNOWN SCHEMA DIVERGENCE — TARGET ALREADY DECIDED: CM/MF ONLY. OWNER DECISION REQUIRED: NO.**
- **HISTORY/AUDIT:** corrections are new rows (`correction_of_id`, before/after); `repair_events` append-only (scope interna).
- **MISSING:** full reference context is preserved via N22 `reference`+`production_code` snapshot columns + `job_on_revision_id` anchor — supports `5447T173`. `job_on_revision_id` is nullable (by design: no hard block when context unknown) and **no uniqueness** on `individual_number` (repeated numbers valid) — both correct for the rule.
- **SCHEMA GAP:** the **BQ-in-tool_type CHECK is a schema constraint that contradicts the current rule** → a **KNOWN SCHEMA DIVERGENCE; TARGET ALREADY DECIDED (CM/MF ONLY); OWNER DECISION REQUIRED: NO** (restore `('CM','MF')`), and a **clean-baseline change**. Not a test-version persistence blocker for the app (the app enforces CM/MF-only), but it is a genuine data-model contradiction against current authority that must be corrected.
- **CLASSIFICATION:** **KNOWN SCHEMA DIVERGENCE / CLEAN-BASELINE** (constraint reversal to undo against an already-decided rule). Data surfaces otherwise **SUPPORTED NOW**.

### 2.14 Reparação Externa (DES-015)

- **DES TASK:** Honest BQ-deferred empty surface; separate CM/MF builders; exits, list detail, confirmation/return, history, repairer/line settings; Armazém sole physical owner; duplicate open-item rule; one-UoW pickup/return.
- **DESIGN DATA REQUIRED:** CM/MF list/exit/item identity; repairer; warehouse interaction; return lifecycle; duplicate open-item protection; history.
- **CURRENT TABLES:** `repairers`, `line_repairer_defaults`, `repair_exits`, `repair_exit_items`, `repair_events` (N08); `repairer_repair_types` (N20, R004); `physical_pieces` (N04, via `IFerramentasPieceLookup`).
- **CURRENT COLUMNS:** exits (repair_type BQ/CM/MF, repairer_id + repairer_snapshot jsonb, planned_date, status preparacao→…→concluido|cancelado); items (bq_lote_id XOR physical_piece_id + individual_number CHECK, qty, picked/out/in facts with operators, status pendente/em_reparacao/devolvido via N25); repairers active; line defaults PK(line, tool_type); repair_events (append-only, cancelled guards); repairer_repair_types join.
- **RELATIONSHIPS:** item→exit; item→bq_lote | physical_piece (disjunct CHECK); movement→exit (Armazém repair port); event→item/internal.
- **WRITE/READ:** `ReparacaoExternaService`, `IRepairRepository`, `RepairExitStatusMachine`, `IArmazemRepairMovementPort` (Armazém-owned port), `IFerramentasPieceLookup` (read-only). Pickup/return coordinate in one `IDbUnitOfWork` (owner B/C).
- **HISTORY/AUDIT:** `repair_events` append-only.
- **MISSING:** none. **Duplicate open-item protection** is enforced at the **Application** layer (`ExistsItemInOpenExitAsync`), which the functional rule F requires ("hard Application/domain rule") — the DB does not back it with a uniqueness constraint, by design (a piece can legitimately appear in a *closed* exit). Optionally a partial unique index over open exits could reinforce it, but current rule says app-level. No schema gap.
- **SCHEMA GAP:** none.
- **CLASSIFICATION:** **SUPPORTED NOW.**

### 2.15 História (DES-016)

- **DES TASK:** Transversal read-only audit source; module/entity/actor/result/time filtering; entity list + timeline; visibility filtering; no writes/rankings.
- **DESIGN DATA REQUIRED:** transversal audit source; filtering; enough identifiers/context for entity list + timeline; visibility support.
- **CURRENT TABLES:** `audit_events` (N01).
- **CURRENT COLUMNS:** occurred_at, year, actor_user_id, actor_name_snapshot, module_id, action_code, entity_type, entity_id, entity_label_snapshot, result (succeeded/failed/denied/corrected), reason, correlation_id, `job_on_id`, `revision_id`, before/after jsonb; **append-only trigger**; PERF-01 index (module, time).
- **RELATIONSHIPS:** none cross-domain (denormalized audit facts by design).
- **READ SUPPORT:** `DapperHistoriaRepository` (grouping/paging, filters by query/entity-type/id/module/action/actor/result/from/to, **TD-24 origin-module visibility** + admin-only-with-`audit.view`).
- **HISTORY/AUDIT:** single canonical transverse table — exactly what the design needs.
- **MISSING:** none. The design's "entity list + selected timeline" and "factual before/after correction detail" are fully served by `audit_events` columns.
- **SCHEMA GAP:** none.
- **CLASSIFICATION:** **SUPPORTED NOW.**

### 2.16 DesignLaboratorio (DES-017)

- **DES TASK:** Complete the component/state laboratory; non-domain; no DB surface.
- **DESIGN DATA REQUIRED:** none (static component demonstrations).
- **CURRENT:** `Pages\DesignLaboratorio\Index.cshtml` — a pure static lab with no repository, no grants, no schema.
- **CLASSIFICATION:** **NOT APPLICABLE** (no data support required).

---

## 3. Required-vs-captured matrix summary

| Module | DES | DB SUPPORT | Query/DTO gap | Schema gap | Data migration | Blocks build? |
|---|---|---|---|---|---|---|
| Shell | 002 | SUPPORTED NOW | – | – | – | No |
| Login | 003 | SUPPORTED NOW | – | – | – | No |
| Admin | 004 | SUPPORTED NOW | display-name label (cosmetic) | – | – | No |
| Job On | 005 | **SCHEMA CHANGE REQUIRED** | live-tool decorator (minor) | **master reference + reference image (Q-002)** | yes (additive) | **Q-002 image surface** |
| Controlo | 006 | SUPPORTED NOW | free-mode read surfacing | – (optional MCaliper link ledger) | – | No |
| Peso Operator | 007 | SUPPORTED NOW | comparison read pairing (jsonb→DTO) | – (clean: promote pairing) | – | No |
| Peso Responsible | 008 | SUPPORTED NOW | – | – | – | No |
| Pegamentos | 009 | SUPPORTED NOW | context read DTO (minor) | – | – | No |
| Ferramentas | 010 | **SUPPORTED W/ READ DTO** | **current-location/status projection** | – | – | No |
| Armazém | 012 | SUPPORTED NOW | alert (latest usage) read | – | – | No |
| Boquilhas | 011 | SUPPORTED NOW | – | – | – | No |
| Tampões | 013 | SUPPORTED NOW | – | – | – | No |
| Reparação Interna | 014 | **KNOWN SCHEMA DIVERGENCE / CLEAN-BASELINE** | – | **BQ in tool_type CHECK to revert (rule already decided CM/MF)** | – | No (app gates) |
| Reparação Externa | 015 | SUPPORTED NOW | – | – | – | No |
| História | 016 | SUPPORTED NOW | – | – | – | No |
| DesignLaboratorio | 017 | NOT APPLICABLE | – | – | – | – |

---

## 4. Distinguishing QUERY gaps from SCHEMA gaps

**Confirmed QUERY/DTO gaps (data exists, app not exposing it — no schema change):**
1. **Ferramentas current warehouse location/state** (DES-010): `warehouse_stock` (active occupation) + `tool_usage_records` exist and are joinable via `tool_lote_id`, but the Ferramentas read model does not project them. → read DTO.
2. **Armazém SAP-usage alert** (DES-012): latest `%` from `tool_usage_records` per lot is stored; the alert card needs a read projection. → read DTO.
3. **Controlo free-mode consultation** (DES-006): read endpoints exist; the recompose should populate free-mode from them (currently empty messages). → UI/query reuse.
4. **Admin display label** (X12): `auth_user_id` exists; project a human label instead of the raw UUID under "Email". → read DTO.

**Confirmed SCHEMA gaps (required fact cannot be safely represented today, and read-model alone is insufficient):**
1. **Job On master article/reference + reference-scoped image (Q-002).** Why: the design and the settled functional target require the image to belong to a master article/reference; the current model has no master-reference table and stores `image_asset_id` on `job_on_revision` (per-revision). A read/DTO cannot invent a reference-scoped image because no stable reference row exists to associate it to. Functional support: `04_DATA_CONTRACT_JOB_ON.md` and `08_OWNER_DECISION_ARTICLE_IMAGE.md`. → **REAL SCHEMA GAP — FUNCTIONAL TARGET ALREADY DECIDED** (image is reference-scoped, not per-revision); only the **technical representation** of the master reference entity remains a clean-baseline design choice for later.
2. **(Known schema divergence / clean-baseline) Reparação Interna `tool_type` CHECK reversal.** The current CHECK `('CM','MF','BQ')` permits a fact the functional SOT forbids. This is a constraint/rule **contradiction** that a read-model change cannot fix; the target is **already decided (CM/MF only)** and the DDL revert is a clean-baseline action, not an unresolved product decision. Not a test-version code blocker (the app enforces CM/MF-only).

---

## 5. Critical design requirements — verification results

### 5.1 JOB ON
- master article/reference identity → **PARTIAL / GAP** (no master ref table; `article_reference_id` loose uuid)
- article image association to master reference → **GAP** (revision-scoped today)
- exact Job On revision snapshots → **SUPPORTED** (revision + component/field/row graph, append-only via N25)
- components/families → **SUPPORTED**
- Job On current-open per user → **SUPPORTED** (`jobon_user_current`, N24)
- image use in print → supported once the image is resolvable (current `FileSystemJobOnImageProvider` works, but against the revision, not the reference — contradicting Q-002)
- history/revisions → **SUPPORTED** (`job_on_audit_event` + global audit)
- exact production/machine/reference context → **SUPPORTED** (revision snapshots + `current_revision_id`)

### 5.2 CONTROLO
- exact `job_on_id` → **SUPPORTED** (FK NOT NULL)
- exact `job_on_revision_id` → **SUPPORTED** (FK NOT NULL)
- bound production context → **SUPPORTED**
- control sheets/items/events → **SUPPORTED** (N23)
- free-mode consultation → **SUPPORTED** (read endpoints; UI surfacing needed)
- history → **SUPPORTED** (append-only events + audit)
- MCaliper links → **PARTIAL** (single `mcaliper_link` on items; history/ledger optional additive)

### 5.3 PESO
- exact Job On revision binding → **SUPPORTED** (FK NOT NULL)
- per-CM readings → **SUPPORTED** (`peso_leituras`)
- server-calculated results → **SUPPORTED** (C# engine; results persisted in snapshot)
- previous-production comparison mapping → **SUPPORTED** (`peso_comparacao_anterior` + jsonb `comparison_decisions`; relational promotion optional)
- approval/decision state → **SUPPORTED** (`status`, `approval_log`, `peso_day_approvals`)
- immutable approved snapshot → **SUPPORTED** (N25 approved-guard trigger)
- document/history → **SUPPORTED**

### 5.4 PEGAMENTOS
- exact revision binding → **SUPPORTED** (FK NOT NULL)
- inherited CM/BQ/MF context → **SUPPORTED** (snapshots from pinned revision, not reselectable)
- measurements → **SUPPORTED** (append-only medical facts)
- tolerance/result → **SUPPORTED** (default 0.20; state machine)
- immutable final document metadata → **SUPPORTED** (`pegamento_documentos` UNIQUE — persisted once)
- history → **SUPPORTED**

### 5.5 FERRAMENTAS
- CM/MF master reference → **SUPPORTED**
- lote identity → **SUPPORTED**
- technical data → **SUPPORTED**
- verification/check history → **SUPPORTED**
- manual SAP utilisation records → **SUPPORTED** (`tool_usage_records` append-only, manually entered, never calculated)
- current state/location read support → **QUERY/DTO GAP** (not projected)
- utilisation history → **SUPPORTED**

### 5.6 ARMAZÉM
- physical location → **SUPPORTED**
- one active occupation → **SUPPORTED** (partial unique index)
- movement history → **SUPPORTED** (append-only)
- current tool location → **SUPPORTED**
- consume stored SAP utilisation for alert → **SUPPORTED** (data exists; read DTO to surface)

### 5.7 BOQUILHAS
- BQ identity/lotes → **SUPPORTED**
- movements → **SUPPORTED** (append-only, 20→25 via `exceptional_received_qty` + discrepancy)
- discrepancy → **SUPPORTED** (`bq_discrepancies`, non-blocking)
- repairer relation → **SUPPORTED** (N18 `noted_repairer_id` + canonical repairers)
- lifecycle/history → **SUPPORTED**
- external repair separation → **SUPPORTED** (BQ schema independent; external flow separate)

### 5.8 REPARAÇÃO INTERNA
- CM/MF-only representation → **CONSTRAINT CONTRADICTION** (N22 added BQ to CHECK)
- complete production reference (`5447T173`) → **SUPPORTED** (N22 reference/production snapshot)
- Job On/revision context → **SUPPORTED** (`job_on_id` + `job_on_revision_id` FK)
- repeated individual numbers → **SUPPORTED** (no uniqueness — by design)
- append-only corrections → **SUPPORTED** (`correction_of_id`, before/after)
- no BQ as repair type → **TARGET ALREADY DECIDED (CM/MF only)** (schema contradiction remains; app must gate BQ)

### 5.9 REPARAÇÃO EXTERNA
- CM/MF list/exit/item identity → **SUPPORTED**
- repairer → **SUPPORTED**
- warehouse interaction → **SUPPORTED** (Armazém-owned port; one UoW)
- return lifecycle → **SUPPORTED** (status machine)
- duplicate open-item protection → **SUPPORTED** (Application layer, per rule F)
- history → **SUPPORTED** (append-only events)

### 5.10 TAMPÕES
- configuration → **SUPPORTED**
- machine association → **SUPPORTED** (N21)
- movements → **SUPPORTED** (append-only)
- balances → **SUPPORTED** (derived; two states)
- planning → **SUPPORTED** (non-reserving)
- notes/history → **SUPPORTED** (append-only notes/events)

### 5.11 HISTÓRIA
- transversal audit source → **SUPPORTED**
- module/entity/actor/result/time filtering → **SUPPORTED**
- identifiers/context for list+timeline → **SUPPORTED**
- visibility filtering → **SUPPORTED** (TD-24)

### 5.12 ADMIN/AUTH
- users/templates → **SUPPORTED**
- auth_user mapping → **SUPPORTED** (`auth_user_id` NOT NULL UNIQUE via N25)
- profile title → **SUPPORTED**
- capabilities/templates → **SUPPORTED** (`access_templates.modules` jsonb + `modules_override`)
- audit → **SUPPORTED**
- password/reset workflow → **SUPPORTED** (lives in Supabase Auth, not BA DMO tables; no local password = good)

### 5.13 SAP UTILISATION rule
- manually read from SAP / manually entered / never calculated / append-only / no auto-SAP / Armazém may consume → **all SUPPORTED** by `tool_usage_records`.

### 5.14 JOB ON IMAGE rule
- image belongs to master article/reference, chosen from company-server directory, associated w/ reference, print consumes, only required sheet displays → **NOT fully supported in current model** (image revision-scoped; no master reference) — the schema dependency for Q-002.

### 5.15 REPARAÇÃO INTERNA rule
- CM/MF only; BQ never internally repaired; full reference (`5447T173`) available; BQ code context-only → **reference data supported; the tool_type CHECK must be reconciled to CM/MF-only.**

### 5.16 CONTROLO rule
- consumes current-open Job On, exact revision across tabs, no second selector/calendar, free-mode exists → **SUPPORTED** (`jobon_user_current` + revision-anchored read bindings).

---

## 6. Patch-debt check (evidence for a later schema redesign — do NOT remove yet)

| OBJECT | WHY IT LOOKS PATCHED | CURRENT PURPOSE | STILL REQUIRED BY FINAL DESIGN? |
|---|---|---|---|
| `job_on.production_folder` (N13) | single-column additive added to a base table after original design | shared production-folder identity owned by Job On | **YES** (shared production directory model) |
| `pegamento_documentos` (N14) | new table for final-document metadata on top of N07 | immutable final PDF persisted once | **YES** |
| `pegamento_medicoes.tool_number` (N15) | additive nullable column | per-measurement tool/cavity number | **YES** |
| `pegamento_controlos.cm/bq/mf_nominal` (N16) | three additive nullable columns vs single `nominal_average` | per-component nominals for tolerance | **YES** |
| `pegamento_controlos.notas` (N17) | additive nullable column | notes | **YES** |
| `bq_movements.noted_repairer_id` (N18) | additive nullable FK | per-movement repairer (history preservation) | **YES** |
| `tool_usage_records` (N19) | new table appended | manual SAP utilisation history | **YES** (R003) |
| `repairer_repair_types` (N20) | new many-to-many appended | repairer capability | **YES** (R004) |
| `tampao_configuration_machines/notes/machine_event` (N21) | three tables added on top of N10 | multi-machine + comments/events | **YES** (R008) |
| `internal_repair_records` context cols (N22) | 4 additive nullable columns + **CHECK widened to include BQ** | historical production-context anchor; REVERSED type rule | **PARTIAL** — context columns YES; **BQ-in-CHECK NO** (contradicts rule, revert at freeze) |
| `controlo_sheets/items/events` (N23) | new sheet family appended | Folha de Controlo (owner-decision, R010) | **YES** |
| `jobon_user_current` (N24) | new small per-user table appended | universal landing, current-open Job On (R011) | **YES** |
| `N25_remediation` | batch of constraints/RLS/append-only across many tables | deployment-readiness invariants (immutability, RLS coverage, approved-guard, lifecycle) | **YES** (as invariants) |
| `internal_users.modules_override` (N26) | additive nullable jsonb | per-user grant override | **YES** (owner decision) |

**Clean-baseline consequence (do not remove yet):** the additive families above are mostly *correct and required* — they represent genuine functional additions, not debt. Two items are genuine baseline debt: (1) `job_on_revision` snapshot columns stored as **jsonb** where the design contract wants typed/queryable columns; (2) the **N22 BQ tool_type reversal**. See §7 risks.

---

## 7. Data-model risks

| # | Risk | Severity |
|---|---|---|
| R1 | **Constraint contradicts current rule:** `internal_repair_records.tool_type` CHECK widened to `('CM','MF','BQ')` (N22) vs functional SOT "CM/MF only". A fact the authority forbids is representable. | **CRITICAL** (decision). |
| R2 | **No master article/reference entity; `job_on.article_reference_id` is a loose nullable uuid with no FK.** The Job On history filter and Q-002 image association have no stable reference identity to anchor to. | **HIGH** (schema change). |
| R3 | **Article image stored per-revision (`job_on_revision.image_asset_id`)**, contradicting the resolved Q-002 (reference-scoped). | **HIGH** (schema change). |
| R4 | **Job On revision snapshot columns stored as jsonb** (`production_snapshot`, `reference_snapshot`, `machine_snapshot`, `dates_snapshot`, …) vs the design contract's typed, queryable columns. Works, but not clean-relational; reference filtering less robust. | **MEDIUM** (clean-baseline). |
| R5 | **Duplicate representation of the same concept:** verification occurrences exist in **both** `tool_check_occurrences` (N04) and `job_on_verification_occurrence` (N05). Both materialize check rules with `manual_job_on` source. Historical split; risks divergence. | **MEDIUM** (clean-baseline). |
| R6 | **History split across multiple event tables:** `audit_events`, `job_on_audit_event`, `controlo_sheet_events`, `repair_events`, `bq_movements/reopen_history`. `audit_events` is the canonical transverse source; the others are module-local audit. Acceptable but fragmented. | **MEDIUM** (clean-baseline). |
| R7 | **Comparison pairing persisted as jsonb** (`peso_controlos.comparison_decisions`) where the owner decision wants explicit per-CM relational pairs and filtered comparison history. | **MEDIUM** (clean-baseline; works today). |
| R8 | **MCaliper link is a single updateable field** (`controlo_sheet_items.mcaliper_link`); the design wants per-result append-only link history (added/updated/removed). | **MEDIUM** (minor additive). |
| R9 | **Warehouse occupation check-then-insert not atomic** in the app (functional hardening A3) — mitigated at DB level by the partial unique index, but messaging/UX could still race. | **LOW** (app hardening). |
| R10 | **Tampões `SetSaldoAsync` absolute-rewrite lost-update** (functional hardening A4) — the schema/delta model supports atomic updates; the app should switch to delta/`FOR UPDATE`. | **LOW** (app hardening). |
| R11 | **Duplicate-open-item protection only at Application layer** (per rule F) — no DB partial unique. Acceptable per rule; optional add for defense-in-depth. | **LOW**. |
| R12 | **`job_on_component_row` lacks `row_code`** (design contract mentions a stable code). Presentational. | **LOW**. |
| R13 | **Schema ownership vs functional ownership:** `warehouse_movements.repair_exit_id` and programmed-exit/BQ concepts exist in the schema, but the current Armazém U-14 functional scope excludes programmed external-repair exits and BQ. The capability is present-but-deferred; fine, but must not be activated in the test version. | **LOW** (scope guard). |

---

## 8. Design-implementation blockers (severity split)

### A. DOES NOT BLOCK DESIGN IMPLEMENTATION (data exists; needs a read/DTO/query)
- Ferramentas current-location/status projection (DES-010).
- Armazém SAP-usage alert project (DES-012).
- Controlo free-mode read surfacing (DES-006).
- Admin display-name label (DES-004).
- Peso comparison read pairing (jsonb→queries/DTO) (DES-007).
- Ferramentas live-tool-state decorator for Job On (DES-005 minor).

### B. BLOCKS A SPECIFIC DES TASK
- **DES-005 (Job On) — the article image surface of Q-002.** Requires a master article/reference row with a **reference-scoped** image association. Current model stores the image on the revision and has no reference entity → schema change required to implement the resolved Q-002 as designed. All other Job On work is unblocked.

### C. BLOCKS DATABASE FREEZE ONLY (works for the test version; needs cleanup/reconciliation before the final baseline)
- **R1:** revert `internal_repair_records.tool_type` CHECK back to `('CM','MF')` (target already decided; DDL revert at freeze). Test-version code is unaffected if the app gates BQ.
- **R2/R3:** Job On master reference + reference-scoped image — the representation of a **real schema gap** whose functional target is already decided (clean-baseline design choice for the master-reference entity).
- **R4/R5/R6/R7/R8:** jsonb snapshots, verification-occurrence duplication, fragmented event tables, jsonb comparison pairing, MCaliper-link ledger — all representable now; promote/denormalize as **clean-baseline technical choices** in the later DB redesign if desired.

---

## 9. Output matrix

| MODULE | DES TASK | DB SUPPORT | QUERY/DTO GAP | SCHEMA GAP | DATA MIGRATION NEEDED | DESIGN IMPLEMENTATION BLOCKED | NOTES |
|---|---|---|---|---|---|---|---|
| Shell | 002 | SUPPORTED NOW | – | – | – | No | Capability-driven nav from existing grants |
| Login | 003 | SUPPORTED NOW | – | – | – | No | Auth in Supabase; no local password |
| Admin | 004 | SUPPORTED NOW | display-name label | – | – | No | X12 cosmetic |
| Job On | 005 | PARTIAL (core now; image needs change) | live-tool decorator | **master reference + reference image (Q-002)** | additive | **Only Q-002 image surface** | jsonb snapshots are clean-baseline |
| Controlo | 006 | SUPPORTED NOW | free-mode surfacing | – (optional MCaliper link ledger) | – | No | Unified workspace over existing data |
| Peso Operator | 007 | SUPPORTED NOW | comparison pairing reads | – (clean: promote pairing) | – | No | Server results + snapshot all present |
| Peso Responsável | 008 | SUPPORTED NOW | – | – | – | No | Decisions/approvals persisted |
| Pegamentos | 009 | SUPPORTED NOW | context read DTO (minor) | – | – | No | final doc persisted once |
| Ferramentas | 010 | SUPPORTED W/ READ DTO | **current-location/status** | – | – | No | usage append-only manual (Q-001 active) |
| Armazém | 012 | SUPPORTED NOW | alert (latest usage) read | – | – | No | one-active-occupation DB-enforced |
| Boquilhas | 011 | SUPPORTED NOW | – | – | – | No | BQ independent; 20→25 non-blocking |
| Tampões | 013 | SUPPORTED NOW | – | – | – | No | derived balances; machine association present |
| Reparação Interna | 014 | KNOWN SCHEMA DIVERGENCE | – | **R1: BQ tool_type CHECK revert (rule already decided CM/MF)** | – | No (app gates BQ) | DDL revert at freeze |
| Reparação Externa | 015 | SUPPORTED NOW | – | – | – | No | app-level duplicate rule; Armazém port |
| História | 016 | SUPPORTED NOW | – | – | – | No | audit_events read-only |
| DesignLaboratorio | 017 | NOT APPLICABLE | – | – | – | – | static lab, no DB |

---

## 10. Final verdict

**CAN THE CURRENT DATABASE SUPPORT THE FINAL DESIGN TEST VERSION?**

### ✅ YES WITH LIMITED SCHEMA CHANGES

The current database (N01–N26) supports **13 of 16 modules immediately** (Shell, Login,
Admin, Controlo, Peso Operator, Peso Responsible, Pegamentos, Armazém, Boquilhas,
Tampões, Reparação Externa, História — all `SUPPORTED NOW`), with DesignLaboratorio not
applicable. **One** module requires a genuine schema change for a specific designed
surface (Job On article image + master reference, Q-002). **One** module carries a
**known schema divergence** (Reparação Interna `tool_type` CHECK reversal — target already
decided CM/MF only), which does not block the test-version code because the application
gates BQ; only a DDL revert is needed at the clean baseline.
The only pure read/DTO gaps are small and non-blocking (Ferramentas current-location,
Armazém alert, Controlo free-mode, Admin display label).

### Totals
- **TOTAL MODULES FULLY SUPPORTED (SUPPORTED NOW):** 12 (Shell, Login, Admin, Controlo,
  Peso Operador, Peso Responsável, Pegamentos, Armazém, Boquilhas, Tampões, Reparação
  Externa, História)
- **TOTAL MODULES SUPPORTED WITH READ QUERY/DTO ONLY:** 1 (Ferramentas – current-location/status)
- **TOTAL QUERY/DTO GAPS:** ~4 (Ferramentas current-location; Armazém usage alert;
  Controlo free-mode surfacing; Admin display label) — all non-blocking, data exists.
- **TOTAL SCHEMA GAPS:** **1 primary real** (Job On master reference + reference-scoped
  image, Q-002). Additional *clean-baseline/freeze* refinements: R1 BQ CHECK revert (known
  schema divergence — target already decided), jsonb snapshot promotion (R4), verification
  duplicate (R5), event fragmentation (R6), comparison-pairing promotion (R7),
  MCaliper-link ledger (R8)).
- **TOTAL DESIGN BLOCKERS:** **1** (DES-005 Q-002 article-image surface). No other DES task
  is blocked on persistence.
- **OWNER PRODUCT DECISIONS REQUIRED:** **0.** The Reparação Interna CM/MF-only rule and the
  Job On article-image ownership are already settled; no open owner product decisions remain.
- **CLEAN-BASELINE TECHNICAL DECISIONS:** remain for later DB redesign (master-reference
  table structure, typed vs jsonb Job On snapshots, relational vs jsonb Peso comparison
  pairing, verification-occurrence consolidation, MCaliper link-history representation,
  audit/event consolidation).
- **TOTAL DB-FREEZE-ONLY ITEMS:** ~6 (R1 DDL revert + R4/R5/R6/R7/R8 clean-baseline refinements).

---

## 11. Codex feasibility

**READY FOR CODEX DB AUDIT.**
**NO OPEN OWNER PRODUCT DECISIONS IDENTIFIED BY THIS AUDIT.**
**CLEAN-BASELINE TECHNICAL DESIGN DECISIONS REMAIN.**

A stronger agent with live Supabase access can safely handle the later database rebuild.
No **owner/product** decisions are outstanding; the two rules previously surfaced as owner
questions are already settled:
- **Reparação Interna CM/MF-only rule** — settled; `tool_type` must be CM/MF only, BQ never
  an internal repair type. The N22 CHECK widening is a **schema contradiction** to correct at
  the clean baseline, not a product decision.
- **Job On article image ownership** — settled; the image belongs to the master
  article/reference, not independently to each revision. Only the **technical
  representation** is open.

The following remain **clean-baseline technical design choices** for the later DB redesign
(these are implementation/representation decisions, not owner product decisions, and are
**not** blockers for the current test-version implementation unless a specific DES
dependency is already proven):
1. **Reparação Interna `tool_type` CHECK:** restore the N08 rule (`('CM','MF')`), reversing
   N22's BQ widening — target already decided; DDL revert at the clean baseline.
2. **Job On master article/reference:** choose the exact table structure (typed master-reference
   entity with the Q-002 reference-scoped image association) — business ownership already
   decided; representation open.
3. **Job On revision snapshot columns (jsonb → typed):** decide whether to promote the jsonb
   snapshot columns to typed columns in the clean baseline (affects the app's
   `JsonDocument` reads).
4. **Peso comparison pairing (jsonb → relational per-CM table):** decide whether a
   relational `peso_comparation_pair` table should replace `comparison_decisions` jsonb to
   serve filtered comparison history.
5. **Verification occurrences:** decide which of `tool_check_occurrences` /
   `job_on_verification_occurrence` is canonical (or how the duplication is reconciled).
6. **MCaliper link ledger:** decide whether a per-result append-only link-history table is
   required.
7. **Audit/event consolidation:** decide whether the fragmented module-local event tables are
   folded into the canonical `audit_events` source.

### Context Codex would need
To perform the later database rebuild safely, Codex needs:
- The full functional SOT (`AI-CONTEXT\docs\FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`).
- The design package (`AI-CONTEXT\design-coder\**`) — module briefs, DES tasks, owner-decision files (Q-001/Q-002, CM/MF-only, SAP-usage, glass-comparison, shared documents).
- The current schema + the full migration history (`database\migrations\N01–N26`) as the historical evidence base (not authority).
- Live Supabase credentials + roles (`ba_dmo_app`/`ba_dmo_migrate`), RLS posture, and the migration runner contract (`MigrationRunner`, `NpgsqlMigrationScriptGateway`).
- The app's persistence contracts (`*.Application\Modules\**\I*Repository.cs`, `*.Infrastructure\Access\Dapper*Repository.cs`) so any schema change stays consistent with the app's read/write ports.
- This audit report (for the clean-baseline technical design list above).

No **owner product decision** needs to be recorded before Codex proceeds: the functional
targets are already settled, and the items above are technical representation choices for a
clean-baseline DB redesign — hence **READY FOR CODEX DB AUDIT**, not blocked on owner input.

---

## 12. Appendix — migration/table inventory used as evidence

- **Identity/Auth:** N01 (`roles`, `ba_dmo_guard_append_only`, `access_templates`, `internal_users`, `audit_events`); N25 (auth_user_id NOT NULL + UNIQUE, RLS on late tables); N26 (`modules_override`).
- **Catalog:** N02 (`module_catalog_mirror`).
- **Boquilhas:** N03 (`bq_lotes`, `bq_traces`, `bq_movements`, `bq_discrepancies`, `bq_lifecycle_history`, `bq_utilisation_readings`); N18 (repairer col).
- **Ferramentas:** N04 (`tool_references`, `tool_lotes`, `physical_pieces`, `tool_check_rules`, `tool_check_occurrences`); N19 (`tool_usage_records`).
- **Job On:** N05 (revision family + `jobon_user_current` is N24); N13 (`production_folder`); N25 (`uq_job_on_identity`, lifecycle, append-only, RLS, PERF-01).
- **Peso:** N06 (references, lotes, controlos, leituras, comparação, day approvals, settings); N25 (approved-guard, consistency).
- **Pegamentos:** N07 (controlos, medicoes); N14 (documentos); N15 (tool_number); N16 (nominals); N17 (notas); N25 (status check).
- **Reparações:** N08 (repairers, exits, items, events, internal records); N20 (repairer_repair_types); N22 (RI context + **BQ-reversal**); N25 (item status check).
- **Armazém:** N09 (locations, stock, movements); N25 (one-active-occupation index).
- **Tampões:** N10 (field defs/values, configurations, saldos, movements, planos); N21 (machines, notes, machine events).
- **Controlo:** N23 (sheets, items, events); N25 (RLS) — plus `jobon_user_current` N24.
- **Shared/settings:** N11 (`app_settings`).

---

*End of audit. No database, migration, schema, application code, test, or Git object was
modified to produce this report.*