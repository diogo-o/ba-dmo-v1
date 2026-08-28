# CONTROLO SCHEMA ALIGNMENT — PRE-BASELINE STRUCTURAL AUDIT

> **Type:** AUDIT + DESIGN ONLY — **GATED**. No source, migration, test, schema
> object, function, or database was modified. No DDL/DML executed. The ONLY
> artifact produced by this task is this report.
>
> **Scope:** one targeted structural validation of the CONTROLO module against
> the canonical functional hierarchy before the N34+ rationalization sequence
> is implemented. The audit cross-references the Controlo Manual with migrations
> N01–N33, the current effective schema, `database/consolidated_clean_install.sql`,
> all Dapper/raw-SQL persistence paths, application services, domain
> models/projections, tests, and the existing N34–N42 rationalization findings.
>
> **Baseline verified:** repo `diogo-o/ba-dmo-v1`, branch `main`, HEAD
> `8d916cb` ("Quiesce legacy access mirrors" — N33) + Queue A hardening working
> tree (N01…N33 immutable; `consolidated_clean_install.sql` parity-hardened).
> All claims are static-evidence based (files read in full this session);
> live-database checks remain `LIVE VERIFICATION REQUIRED` per the established
> evidence policy.
>
> **Authority stack (unchanged hierarchy):**
> 1. `AI-CONTEXT/docs/Manual/*` — functional authority (Manual files read in
>    full: `00_INDEX`, `02_MODULES_OPERATIONAL` (Controlo sections),
>    `10_JOB_ON_FUNCTIONAL` (full), `20_CONTROLO_FUNCTIONAL` (full)).
> 2. Current source code / Dapper / tests — implementation authority.
> 3. N01–N33 — immutable migration history.
> 4. `database/consolidated_clean_install.sql` — target clean-install baseline
>    (parity-hardened by Queue A).
> 5. Existing audit reports (`post_codex_database_rationalization_plan.md`,
>    `post_codex_database_contract_audit.md`, `post_codex_remediation_functional_gate.md`,
>    `post_codex_queue_A_baseline_hardening_report.md`,
>    `schema_rationalization_target_architecture.md`,
>    `schema_rationalization_owner_decisions.md`, `persistence_cross_reference_audit.md`).

---

## 1. Executive Summary

The Controlo relational model **correctly represents the already-defined
functional model**. The Manual defines CONTROLO as one top-level module whose
internal areas (Peso, Pegamentos, Resumo/Folha de Controlo, Histórico) are
NOT independent modules and whose Comparação is a **workflow/record-type inside
Peso** (`00_INDEX` §4; `02_MODULES_OPERATIONAL` §5–§6). The schema mirrors that:
every Controlo persistence surface (Peso, Pegamentos, Folha de Controlo) is
anchored to the **same Job On context** (`job_on_id` + immutable
`job_on_revision_id`, both NOT NULL FKs), the access catalog treats `controlo`
as **the single grant** with `peso`/`pegamentos` derived from it
(`AccessResolver.cs:147-153`, `CanonicalModuleCatalog.cs:60`), and the
`record_type` CHECK on `peso_controlos` (`novo_controlo`/`comparacao`)
physically encodes the Comparação-inside-Peso hierarchy.

The highest-priority check — **Comparação ↔ Peso baseline** — resolves cleanly:

- The comparison baseline is an **explicitly user-selected, server-validated
  approved Novo Controlo** of a previous production
  (`PesoService.CreateComparisonAsync`, `peso.js` selector); it is **never
  auto-resolved** by the schema or a legacy table.
- The baseline identity and values are persisted **at comparison creation as an
  intentional immutable snapshot** (`peso_controlos.previous_control` JSON:
  both the current and previous Job On identities + per-CM glass weights,
  differences and percentages). `UpdateControlAsync` never rewrites it
  (verified in the Dapper UPDATE column list). This matches the Manual's
  "não altera o controlo base previamente aprovado… preserva rastreabilidade
  histórica" (20:251-263) and "associação é explícita/validada" (20:208).
- The legacy `peso_comparacao_anterior` table (N06:134-140) is a **dead,
  never-wired mirror**: zero SQL in `src/` (re-grepped this session; only doc
  comments), no Manual rule ever mandated an automatic previous-approved
  materialization, and its planned removal (**N37**) is re-confirmed.

Three planned rationalization migrations touch Controlo. Re-evaluation result
(§15): **N37 UNCHANGED**, **N39 UNCHANGED**, **N40 MODIFY_DESIGN** — the N40
approved-readings trigger, implemented naively, would break the approve/reopen
transition flows because `UpdateControlAsync` rewrites readings (DELETE +
re-INSERT) *after* flipping the parent status inside the same transaction. N40
must ship with a code pairing (only the draft-edit path rewrites readings).
This is a design refinement of a **planned, already-numbered migration**; no
renumbering and **no new migration number are required**.

**Answer to the final question (Section 18):** **YES** — the current Controlo
schema can safely become part of the new post-Codex stable database baseline
as-is, subject only to the already-planned N34–N42 rationalization, with two
carry-ons that require **no new structural change**: (a) N40's design must
include its code pairing (readings-rewrite flow) and (b) one **optional**
additive CHECK (`record_type='comparacao' ⇒ previous_control IS NOT NULL`) can
ride an already-planned migration wave to make the baseline-snapshot
requirement DB-enforced; neither is required for baseline safety because the
comparison writer is a single, validated code path.

---

## 2. Canonical Controlo Functional Model

Manual authority (`00_INDEX` §4, `02_MODULES_OPERATIONAL` §4–§6, §13–§14;
`20_CONTROLO_FUNCTIONAL` §1):

- **CONTROLO is one top-level module.** "o CONTROLO é um módulo lógico único…
  Não é um conjunto de módulos independentes" (20:23).
- **Internal areas:** Peso, Pegamentos, Resumo / Folha de Controlo, Histórico
  do Controlo (20:27-31). "Peso is not a top-level module; Pegamentos is not a
  top-level module; Resumo is not a top-level module" (02 §5).
- **Comparação is a workflow/record-type inside Peso**: "Comparação é um
  workflow / tipo de registo dentro do Peso. Não é: um módulo separado, uma
  área de topo do Controlo, uma unidade de acesso atribuível" (02 §6); tree
  `CONTROLO → Peso (área interna) → Controlo inicial | Comparação` (02 §6).
- **Job On is the consumed context, never duplicated or replaced**:
  "O contexto de produção/revisão usado pelo CONTROLO provém do Job On. Esta
  relação é de consumo, não de duplicação ou substituição" (20:72); "Job On
  mantém a autoridade do planeamento" (20:99); "O CONTROLO não tem um seletor
  independente de Job On. O contexto vem da relação funcional com o Job On"
  (20:87).
- **Peso lifecycle:** "Controlo inicial, antes de produção; Comparação, durante
  produção. Ambos usam o mesmo modelo de cálculo, mas têm finalidades
  funcionais diferentes" (20:172-177). "A Comparação… é complementar ao
  controlo inicial e não substitui o controlo anteriormente aprovado"
  (20:251); "A Comparação não altera o controlo base previamente aprovado"
  (20:263).
- **Comparison pairing is explicit**: "Em Comparação, essa associação é
  explícita/validada. Não é inferida por emparelhamento posicional, nem
  simplesmente pelo número do CM" (20:208).
- **Resumo / Folha de Controlo**: consolidated evaluation per piece
  (OK/NOK + observation + MCaliper) covering exactly CM/BQ/MF/PU/CS (20:369-397);
  workflow Rascunho → Submetida → Aprovada/Rejeitada with reopen (20:411-467).
- **History/snapshot discipline**: "Revisões posteriores não reinterpretam o
  histórico anterior" (20:477); "o registo estruturado/snapshot funcional" is
  the official source, PDF is derived (20:487-491).

**Terminology note (audit-relevant):** grepping the full Manual set shows the
Manual names **no technical identifiers** (`production_code`, `job_on_id`,
`job_on_revision`, `jobon_user_current` appear zero times). Schema names are
implementation naming validated against the *concepts*: stable Job On identity
(`10_JOB_ON` §1/§3), revision immutability (§2.4/§15 CURRENT DESIGN: every save
creates a new revision; corrections create new rows), active production context
(`02` §8/§12). Critically, **the Manual does not specify any mechanism for
selecting the "produção anterior aprovada"** — it only requires the association
to be explicit/validated (§C in the Manual-evidence report). This is why the
dead `peso_comparacao_anterior` table has no functional authority to lose.

---

## 3. Current Persistence Inventory

Complete inventory of every object related to Peso, Comparação, Pegamentos,
Resumo do Controlo and their Job On / production relationship. Line references
are to `database/consolidated_clean_install.sql` (final N33 state; migrations
in parentheses). RLS/policy/grants are per N12 (blanket, consolidated
:1256-1346) and N25 §2 (late tables, consolidated :1575-1627); all Controlo
tables are RLS-enabled with the technical policy `ba_dmo_app_access` and
explicit `ba_dmo_app` DML grants.

### 3.1 Peso family (module owner: Peso — an internal area of Controlo)

| Object | Consolidated | Columns / constraints | Notes |
|---|---|---|---|
| `peso_references` | :616-630 | PK; `mold_number`,`neckring_number` NOT NULL; `uq_peso_references_mold_neckring`; `counter_mold`,`capacity`,`volume_neck`,`volume_pu`,`calote_tp`; `change_log` jsonb `[]` | Master identity (mold+neckring). Volumes feed the deterministic C# glass-weight calc (reference, not duplicated per control — see §9). |
| `peso_lotes` | :632-646 | FK→`peso_references`; `uq_peso_lotes_reference_lote`; `ck_peso_lotes_processo` (NNPB/PS); `ck_peso_lotes_allowed_lines` (≥1); `ix_peso_lotes_reference` | Processo lives in the lot (TD-17); report_subfolder relative. |
| `peso_controlos` | :650-680 | PK; FK→`peso_references`, FK→`peso_lotes`, FK→`job_on`, FK→`job_on_revision`, FK→`internal_users` (approved_by, created_by); `ck_peso_controlos_record_type` (`novo_controlo`/`comparacao`); `ck_peso_controlos_status` (`rascunho`/`pendente`/`aprovado`/`nao_aprovado`); `ck_peso_controlos_approved_consistent` (N25, :1477-1483); `uq_peso_controlos_identity` (mold, neckring, production, line, lote, control_date); snapshots/JSON: `cm_snapshot`, `measurements_snapshot`, `approval_log`, `previous_control`, `comparison_decisions`; indexes :682-685 incl. `ix_peso_controlos_status_date`; trigger `trg_peso_controlos_approved_guard` (N25, function :1489-1517) | **The single table for both Controlo inicial AND Comparação** — the physical encoding of Comparação-inside-Peso. `previous_control` = comparison baseline snapshot (see §5). `cm_snapshot` never populated (PESO-06, §12). |
| `peso_leituras` | :687-695 | PK; FK→`peso_controlos` ON DELETE CASCADE; `uq_peso_leituras_controlo_cm` (controlo, cm_number); `readings` jsonb (PesoEmAgua/PesoVidro) | **No trigger, no guard** (D-10/PC-09 — §11/§15). Doc comment claims append-only (PesoLeitura.cs:5-7) but implementation deletes+re-inserts (§6). |
| `peso_comparacao_anterior` | :697-703 | PK→`peso_controlos` CASCADE; FK `previous_peso_controlo_id`→`peso_controlos`; `previous_snapshot`,`deltas` jsonb; `resolved_at_utc` | **Dead mirror — zero SQL in src** (N37/D-9). See §5/§12. |
| `peso_day_approvals` | :705-716 | PK; `uq_peso_day_approvals_identity` (mold, neckring, line, approval_date); notes | Write-only by design (approval surfaces via approval_log/audit). |
| `peso_settings` | :718-723 | PK setting_key; setting_value jsonb | Peso constants (constant_nnpb/ps) + email recipients. |

### 3.2 Pegamentos family (module owner: Pegamentos — internal area of Controlo)

| Object | Consolidated | Columns / constraints | Notes |
|---|---|---|---|
| `pegamento_controlos` | :728-749 | PK; **FK→`job_on` + FK→`job_on_revision` (both NOT NULL)**; `ck_pegamento_controlos_tolerance` (≥0); `ck_pegamento_controlos_status` (N25, :1451-1453, aberto/fechado); snapshots (reference/cm/bq/mf), `production_code`,`machine_code`; N16 nominals `cm_nominal`,`bq_nominal`,`mf_nominal`; N17 `notas`; **dormant `nominal_average`** (N07); indexes :751-753 | Context inherited from the pinned revision; nominals frozen at creation. |
| `pegamento_medicoes` | :755-764 | PK; FK→`pegamento_controlos`; `component_key`; `costura` NOT NULL; **`contra_costura` NOT NULL (D-12/PC-02)**; N15 `tool_number` NULL; append-only trigger `trg_pegamento_medicoes_append_only`; indexes :766-771 | Append-only facts; ovalização/média are C#-derived, never stored. |
| `pegamento_documentos` | :780-788 | PK; **UNIQUE `pegamento_controlo_id` (1:1)**; filename; output_root/production_folder snapshots | Document metadata (PDF is derived; structured record is the source — Manual 20:487-491). Redundant index `ix_pegamento_documentos_controlo` (N35 removal candidate). |

### 3.3 Resumo / Folha de Controlo family (module: Controlo)

| Object | Consolidated | Columns / constraints | Notes |
|---|---|---|---|
| `controlo_sheets` | :1167-1191 | PK; **FK→`job_on` + FK→`job_on_revision` (NOT NULL)**; `ck_controlo_sheets_status` (rascunho/submetido/aprovado/rejeitado); `ck_controlo_sheets_decision` (coherent decision block); `display_id`; snapshots `production_code`,`reference`,`machine_code`; submission/decision actor columns; indexes :1193-1196 | The Folha's authoritative current state (see §8). |
| `controlo_sheet_items` | :1198-1211 | PK; FK→`controlo_sheets` CASCADE; FK→`tool_references`/`tool_lotes` (source ids); `family` (**no CHECK**); reference/lot/technical_name snapshots; `result` (`ck_controlo_sheet_items_result` OK/NOK); `observation`; `mcaliper_link` | Per-piece control evaluation surface. Families code-filtered to MP_CM/MF/BQ/PU/CS (see §8). |
| `controlo_sheet_events` | :1216-1227 | PK; FK→`controlo_sheets` CASCADE; `ck_controlo_sheet_events_type` (criar/editar/submeter/reeabrir/decidir); append-only trigger :1231-1235 | Append-only workflow history. |

### 3.4 Job On family (context owner — consumed, not owned, by Controlo)

| Object | Consolidated | Notes |
|---|---|---|
| `job_on` | :404-425 | Production context; `uq_job_on_identity` partial unique (production_code, machine_code) WHERE canceled_at_utc IS NULL (N25); lifecycle `ck_job_on_lifecycle_consistent` (N25); `production_folder` (N13, no app writer — C6/PC-06, non-blocking). |
| `job_on_revision` | :431-452 | Immutable revision snapshots (append-only trigger N25; `image_asset_id` dormant — N38); **attribution anchor for Peso/Pegamentos/Controlo.** |
| `job_on_component` (+field/row) | :506-557 | Per-revision component rows (MP_CM/MF/BQ/PU/CS/TP/… via ck family N05) with source_tool_id/source_lot_id FKs to Ferramentas + snapshots. Append-only (N25). |
| `job_on_verification_occurrence` | :559-573 | N05 verification materialization (N04 sibling is the PA-01 candidate). |
| `job_on_audit_event` | :578-592 | Module-level append-only audit facts. |
| `job_on_field_option` | :597-610 | Dormant catalog (D-7, KEEP/DEFER). |
| `jobon_user_current` | :1240-1247 | **Per-user** active Job On context (upsert on actor_id PK) — the "Nenhum Job On carregado" context state is per-user/never global (Manual 20:89-95; DapperJobOnUserContextRepository :11-15). |
| `audit_events` | :128-165 | Global append-only audit; Peso emits (`peso.*`), Controlo sheet has its own `controlo_sheet_events`; Pegamentos emits none today (Queue B F6 — code-only gap, not schema). |

### 3.5 Functions / triggers / policies relevant to Controlo

- `ba_dmo_guard_append_only()` (:71-78) — fires on 13 fact tables + 4 revision
  tables, including `pegamento_medicoes`, `controlo_sheet_events`,
  `job_on_audit_event`, and the revision family (`job_on_revision`,
  `job_on_component`, `job_on_component_field`, `job_on_component_row`).
- `ba_dmo_guard_peso_approved()` (:1489-1511) + `trg_peso_controlos_approved_guard`
  (:1513-1517) — approved controle rows cannot be deleted; identity columns
  cannot be updated once approved (N25 §1.7b/INT-08). **Coverage: `peso_controlos`
  only — `peso_leituras` has no sibling guard (the D-10/N40 gap).**
- RLS: N12 covers `peso_*`, `pegamento_controlos`, `pegamento_medicoes`,
  `job_on*`, `audit_events` (:1256-1346); N25 §2 adds `pegamento_documentos`,
  `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events`,
  `jobon_user_current` (:1575-1627). All RLS-enabled, single technical policy,
  anon/authenticated zero access; functional authorization is C#-side.

---

## 4. Peso ↔ Job On / Production Relationship

**Concept:** the production defined through Job On, and how Peso records are
associated with it.

**Canonical authority:** `job_on` is the production context (stable `job_on_id`;
`production_code` + `machine_code` identity per `uq_job_on_identity`); the
**exact context consumed** is the immutable `job_on_revision` (the revision
graph is append-only since N25 INT-10; its `job_on_component` rows carry the
Ferramenta/CM attribution via `source_tool_id`/`source_lot_id`). Manual:
"Job On mantém a autoridade do planeamento de produção" (20:99); "O contexto
vem da relação funcional com o Job On" (20:87); revision = "historical
version/snapshot… older revisions remain exactly as saved" (10_JOB_ON §2.4/§15).

**Relationship:** `peso_controlos.job_on_id` + `peso_controlos.job_on_revision_id`
are **NOT NULL FKs** to `job_on`/`job_on_revision` (N06:86-87; consolidated
:661-662). Every Peso lifecycle path resolves the context from the Job On's
current revision and pins the revision id at creation
(`PesoService.ResolveJobOnContext` :288-323; `PesoService.CreateControlAsync`
:370-371; `CreateComparisonAsync` :641-642; `DapperPesoRepository` insert
:206-216). Manual: "Peso, Pegamentos, Resumo / Folha de Controlo e Histórico
partilham esse mesmo contexto" (20:85); "para Peso, o contexto funcional da
ferramenta é CM + Lote herdado do Job On" (10_JOB_ON §11).

**DB enforcement:** FKs enforced by the database (NOT NULL REFERENCES). **No**
CHECK enforces that the duplicated context columns on `peso_controlos`
(`production_code`, `line`, `lote`, `mold_number`, `neckring_number`) match the
pinned `job_on`/revision at write time — coherence is maintained by the single
code path that captures them at creation.

**Code enforcement:** `PesoService` derives production/machine/reference/CM/process
from the Job On revision; there is **no independent Job On selector inside
Peso** (`peso.js` receives the jobOnId from the selected production row);
Manual negative rules enforced: no parallel planning, no CM↔MF inference, no
tool promotion from control (20:121-128).

**Historical Peso remains tied to the correct production:** yes. The revision
anchor is append-only (N25 triggers); later Job On edits/tool substitutions
create a NEW revision and never rewrite the pinned one (N06 header comment
:65-73; `PegamentoHistoricalRelationshipTests` proves the same pattern for
Pegamentos). Peso rows are therefore attributable to the original Ferramenta
even after Job On edits.

**Duplicated production/reference/machine fields as independent authorities?**
No. `production_code`/`line`/`lote`/`mold_number`/`neckring_number` on
`peso_controlos` are single-writer capture copies (created once from the
resolved context, members of the `uq_peso_controlos_identity`); they have **no
independent write path**. `cm_snapshot` is a never-populated dormant column
(PESO-06) — declared capacity only. Manual explicitly: "O CONTROLO não
reconstrói, não redefine e não substitui o planeamento do Job On" (20:99).

**Conflict/risk:** LOW. Residual risks: (a) Guid.Empty sentinel binds into
real FKs when a reference/lot is unresolvable (PESO-05/U3 — deferred code
pre-validation, `PesoService.cs:359-360`), (b) the capture-copy columns are not
DB-checked against the pinned revision (coherence is code-only; see §13 finding
CF-8-adjacent, not a blocker), (c) `job_on.production_folder` has no app writer
(C6 — Queue B F10, code-only, out of Controlo's ownership).

**Action required:** **NONE** before baseline freeze. PESO-05 stays deferred
(Df-5). No schema change is needed to enforce capture coherence (would be
over-engineering for a single-writer capture; see §10).

---

## 5. Comparação ↔ Peso Baseline Relationship (highest-priority check)

### 5.1 Mechanism (evidence chain)

1. **Which Peso/control record is the baseline?** An **approved Novo Controlo**
   (`record_type='novo_controlo'`, `status='aprovado'`) of a *previous*
   production, **explicitly selected by the operator** from the list of approved
   novo controlos. Evidence: UI selector `comparisonPreviousControl` fed by
   `GET /api/peso/controls?status=aprovado&type=novo_controlo`
   (`peso.js:170-190`; `Index.cshtml:141-142`); service request field
   `PreviousApprovedControlId` and validation
   (`PesoService.cs:548-553`: must be an approved Novo Controlo;
   `:554-557`: must be a different production — current vs approved JobOn id
   and revision both compared; `:558-560`: must share the same reference;
   `PesoServiceTests.cs:270-284` asserts `PESO_COMPARISON_NO_APPROVED_BASE`).
2. **How is it selected?** Explicit operator choice + server validation.
   **Never automatic.** The legacy `peso_comparacao_anterior` table was designed
   as an auto-resolution read path ("most recent approved control of the same
   mold+neckring, CROSS-LINE" — N06:128-140) but was **never wired**: zero SQL
   anywhere in `src/` (re-grepped this session; matches only in doc comments
   `IPesoRepository.cs:9`, `DapperPesoRepository.cs:14`, `PesoControl.cs:220`;
   plus the dead domain record `PesoControloAnterior` `PesoControl.cs:223-227`).
   Manual authority: the association "é explícita/validada. Não é inferida por
   emparelhamento posicional, nem simplesmente pelo número do CM" (20:208); the
   Manual **never specifies an automatic selection mechanism** (§2 terminology
   note).
3. **Is its identity persisted?** **YES** — the full identity of both sides is
   persisted at creation in `peso_controlos.previous_control` JSON
   (`PesoComparisonSnapshot`, `PesoLeitura.cs:56-75`): `CurrentControlId`,
   `CurrentJobOnId`, `CurrentJobOnRevisionId`, `CurrentProductionCode/Line/Lote`
   and `PreviousControlId`, `PreviousJobOnId`, `PreviousJobOnRevisionId`,
   `PreviousProductionCode/Line/Lote` (written at `PesoService.cs:610-651` into
   `previous_control`; "Both Job On identities are pinned so reference text is
   never used as identity" — `PesoLeitura.cs:52-54`).
4. **Does Comparação reference the original control explicitly?** Yes, **in the
   snapshot** (`PreviousControlId`), deliberately **not** as a real FK column:
   snapshot semantics (Manual 20:263 "preserva rastreabilidade histórica"; a FK
   would create cascade/deletion coupling, and approved baselines can never be
   deleted anyway — N25 guard). DB-level: **no CHECK** ties
   `record_type='comparacao'` to `previous_control IS NOT NULL` — code-enforced
   only (§13 finding CF-8b).
5. **Does it copy baseline values?** Yes, twice: (a) the per-CM math
   (`PreviousGlassWeight`, `Difference`, `DifferencePercent` per paired CM —
   `PesoService.cs:594-608`), and (b) the plain identity columns of the
   comparison row (`mold_number/neckring_number/production_code/line/lote` come
   from the **approved** control; `control_date` and the Job On pins come from
   the **current** control — `PesoService.cs:634-642`).

### 5.2 Classification of every repeated value (required taxonomy)

| Repeated value (where) | Classification | Justification |
|---|---|---|
| `peso_controlos.previous_control` JSON (comparação rows) | **IMMUTABLE_SNAPSHOT** | Written once at comparison creation; `UpdateControlAsync`'s UPDATE list (`DapperPesoRepository.cs:332-340`) never touches `previous_control`; it is the authoritative traceability record of the comparison (Manual 20:261-263). |
| Per-CM `PreviousGlassWeight` / `Difference` / `DifferencePercent` (in the JSON) | **IMMUTABLE_SNAPSHOT (fact fidelity)** | Server-calculated at creation from the approved control's readings; frozen; "O registo de Comparação preserva rastreabilidade histórica" (20:261). |
| Comparison row plain `production_code`, `line`, `lote`, `mold_number`, `neckring_number` (copied from the approved control) | **LEGITIMATE_DUPLICATION** | Single-writer capture at creation for record identity (`uq_peso_controlos_identity`) and search/display; no independent writer; divergence from the baseline after a later baseline edit is impossible by construction (never rewritten) and semantically intended. **Design nuance:** these columns describe the *baseline* production while `job_on_id`/`job_on_revision_id` plus `previous_control.Current*` describe the *current* production — see §11 finding 4 (owner awareness, not a defect). |
| `peso_controlos.previous_control.CurrentControlId/JobOnId/...` | **AUTHORITATIVE** (current-side identity capture) | Persisted once from the current control at comparison creation. |
| Comparison `peso_leituras` rows (weight of glass of the current CMs) | **AUTHORITATIVE** (of the comparison's own readings) | The comparison record's own measurement facts; `cm_number` keys the pairing. |
| `peso_comparacao_anterior` table (previous_peso_controlo_id, previous_snapshot, deltas) | **LEGACY — dead mirror / CONFLICTING-SOURCE-OF-TRUTH-BY-DESIGN (never wired)** | Zero SQL in src; no Manual authority; D-9 = REMOVE_LATER; planned N37. It is the ONLY structure that could have competed with `previous_control`, and it never did (no writer ever). |
| `peso_day_approvals` (day approval facts) | **LEGITIMATE_DUPLICATION (regulatory fact)** | Distinct concept: day-level approval fact vs control-level approval; write-only by design; not a baseline authority. |
| `approval_log` / `measurements_snapshot` on `peso_controlos` | **DERIVED / snapshot** | Captured presentation + decision trail; `measurements_snapshot` rebuilt by `BuildMeasurementsSnapshot` at write; `approval_log` persisted `[]` (PESO-10 write-only — declared capacity for future approval trail). |

### 5.3 Direct answers

- **Can the copied values diverge from the authoritative Peso?** The snapshot
  is frozen; if the approved baseline is later reopened and re-approved with
  different values, the comparison keeps the values captured at creation —
  **that is the intended immutable-snapshot/historical contract** (Manual
  20:263 "não reinterpreta ou apaga o controlo anterior"; 20:477 "Revisões
  posteriores não reinterpretam o histórico anterior"). Not a conflict.
- **Can two different Peso records accidentally be treated as the baseline?**
  No. Selection is explicit and validated (approved novo_controlo, different
  production, same reference); the snapshot pins `PreviousControlId`. The
  candidate list endpoint does not server-side filter by reference (UX
  roughness — the server rejects mismatched references with
  `PESO_COMPARISON_REFERENCE_MISMATCH`), but this cannot produce an accidental
  baseline.
- **Does `previous_control` represent this correctly?** Yes — complete
  two-sided identity + values snapshot; documented as the immutable comparison
  anchor.
- **Does any legacy structure still compete with it?** Only the never-wired
  `peso_comparacao_anterior` (removal planned in N37) and its stale doc
  comments / dead `PesoControloAnterior` record (code cleanup to ride the N37
  change set).

**DB enforcement:** none beyond `record_type` CHECK and code. The
baseline-snapshot requirement is enforced **only by the application** (single
validated writer). An optional DB-enforceable CHECK
(`(record_type = 'comparacao') = (previous_control IS NOT NULL)`) is possible —
recommended as an **optional additive** item inside a planned migration wave
(§15/§16), not required for safety.

---

## 6. Peso vs Comparação Lifecycle

**Peso lifecycle (BEFORE production):** `rascunho → pendente → aprovado /
nao_aprovado`, reopen to `rascunho` (revision+1 in domain). DB-enforced:
`ck_peso_controlos_status` (N06:103-104), `ck_peso_controlos_approved_consistent`
(N25: :1477-1483), `ba_dmo_guard_peso_approved` (N25: approved rows cannot be
deleted; identity columns cannot be updated).

**Comparação lifecycle (DURING production):** same status machine, distinct
`record_type='comparacao'` (N06:101-102), with the extra per-CM decision step
(`comparison_decisions` JSON; every CM needs an explicit decision + mandatory
justification when one is set aside — `PesoService.cs:710-718`; Manual
20:256-261). The comparison row pins the **current** production's Job On
revision (event anchor) while its snapshot holds both sides (§5).

**Are the two workflows interchangeable?** No at the DB/schema level:
- `record_type` CHECK physically distinguishes them; code additionally forbids
  treating a comparison as a baseline (`PesoService.cs:549-550`: the selected
  baseline must be `record_type='novo_controlo'`) and forbids comparison
  creation outside a draft Novo Controlo (`:543-546`).
- No schema element lets a comparison masquerade as a baseline or vice versa.
- Weakness (code-only, single writer): nothing DB-side asserts
  `comparacao ⇒ previous_control NOT NULL` (finding CF-8b).

**Approved/reopened Peso semantics vs the comparison baseline:**
- An approved novo_controlo becomes a selectable baseline. Reopening it
  (aprovado → rascunho) is allowed (non-identity UPDATE; the N25 guard permits
  the status flip) — it then drops out of the baseline candidates until
  re-approved. Existing comparison snapshots that referenced it are unaffected
  (frozen). Re-approval (revision+1) makes it selectable again. This matches
  Manual 20:455-467 (reopen/edit/resubmit without destroying history) and
  20:263 (comparison never reinterprets the base).
- **D-10/PC-09 gap (confirmed):** `peso_leituras` has NO DB guard and
  `UpdateControlAsync` implements readings as DELETE + re-INSERT
  (`DapperPesoRepository.cs:359-375`) even though the domain comment describes
  readings as append-only facts (PesoLeitura.cs:5-7). Today the service paths
  are the only writers and keep draft discipline, so no live corruption path
  exists; the DB layer, however, does not backstop it. **The planned N40
  trigger closes this** — but see §15: N40's design must account for the fact
  that the approval/reopen/decision sends all rewrite readings in the same
  transaction *after* the parent status change.
- **PESO-04 (revision not persisted):** the domain `Revision` counter is never
  written (no column; `MapControl` doesn't read one). Reopen history is
  preserved via status transitions + `audit_events` + (eventually)
  `approval_log`; the Manual does not require a persisted revision number — it
  requires history preservation (§15 of 10_JOB_ON), which is delivered. Verdict:
  acceptable divergence, `DEFER` (known PESO-04), no schema action.

**Re-evaluation of D-9 (peso_comparacao_anterior) and D-10 (approved-parent
guard) in light of the canonical Controlo relationship (§15 for full impact):**
both findings are **confirmed by this audit** — D-9's table is a dead mirror
with no Manual authority, and D-10's gap is real but must be implemented with
its code pairing.

---

## 7. Pegamentos Persistence Alignment

Post-Queue A state (PC-01 create fix + UoW PG-04) is structurally correct:

- **Control linkage:** `pegamento_controlos.job_on_id` + `job_on_revision_id`
  NOT NULL FKs (N07:31-32) — the same canonical context pins as Peso. The
  context lookup (`DapperPegamentoRepository`/`IJobOnProductionContextLookup`,
  `PegamentoService.CreateControlAsync` :49-59) resolves from the Job On
  revision and refuses incomplete contexts
  (`PEGAMENTO_INCOMPLETE_CONTEXT` — "Corrigir ferramentas no Job On", DS-05).
  Tests prove exact-revision pinning and no reinterpretation by later revisions
  (`PegamentoHistoricalRelationshipTests` :30-132).
- **Authority boundaries:** Job On owns the production tooling; Ferramentas
  owns the master tool records; Pegamentos owns only its measurement facts
  (append-only `pegamento_medicoes`), control header, and document metadata.
  `pegamento_documentos` is a strict 1:1 (UNIQUE) with filename + output-root +
  production-folder snapshots — the PDF is derived, the structured record is
  the source (Manual 20:487-491).
- **Document linkage:** `pegamento_documentos.pegamento_controlo_id` UNIQUE FK;
  production folder resolved from the historical Job On context
  (`PegamentoService.ConfirmDocumentSavedAsync` :276-280) — no duplicated
  document tree owned by Job On (Manual 20:526-532).
- **No unrelated-domain treatment:** nothing in the schema or code treats
  Pegamentos as an independent module. The access catalog derives
  `pegamentos` from the `controlo` grant (`AccessResolver.cs:147-153`);
  `PegamentoModuleCatalog` stays a technical id. The separate tables are
  **correct**: Pegamentos facts (dimensional measurements) and Peso facts
  (capacity/volume/weight) are different facts/workflows and must not share a
  table (§10 of this audit + Manual: "distinto do Peso e dos
  Pegamentos/Ferramentas; sem fusão de schema ou lógica" — N23 header,
  controlo_sheets).
- **No artificial FK needed:** a Peso↔Pegamentos FK would be artificial — the
  Manual defines no such link; both share the Job On revision anchor instead.
  Not forcing one is the correct outcome of §10's rule.

**Findings in scope (already planned):** `contra_costura` NOT NULL vs nullable
domain (D-12/PC-02 → N39, owner OD-2); `nominal_average` dormant (C3/PG-10 →
owner, N38 group or N42); redundant index `ix_pegamento_documentos_controlo`
(N35). The N39 nullability widening interacts with the Manual's two-axis rules
(20:300-316): the tolerance corridor is evaluated on the Média of both axes, so
a one-sided measurement yields an un-evaluable Média
(`PegamentoMeasurementCalculator`: ContraCostura null → ovalização null,
média = single value; `PegamentoControlo.AddMeasurement` expects `decimal?
contraCostura`). This supports D-12 branch A (nullable + domain completeness
rule) — the recorded owner default — and leaves N39 UNCHANGED.

---

## 8. Resumo do Controlo Persistence Model

**Classification: MATERIALIZED_STATE — the Folha de Controlo's own
authoritative current state; NOT DUPLICATED_CURRENT_STATE of Peso/Pegamentos.**

Evidence:
- The Manual's "Resumo / Folha de Controlo" is the consolidated presentation
  of the control sheet plus a **decision workflow**
  (Rascunho → Submetida → Aprovada/Rejeitada, reopen — Manual 20:369-467).
  It is not described as a computed projection of Peso/Pegamentos values.
- `controlo_sheets` persists the workflow state (status, submission, decision)
  and per-piece evaluation (`controlo_sheet_items`: result OK/NOK + observation
  + MCaliper link; Manual 20:385-391). The items carry **identity snapshots
  only** of the pinned revision's components (family, source_tool_id,
  source_lot_id, reference/lot/technical-name snapshots — N23:65-78;
  `ControloFolha.Create` copies from `ControloFolhaProductionContext`), **not**
  Peso or Pegamentos measurement values. There is therefore **no duplicated
  current-state data**: no column on any controlo_sheets table stores a Peso
  reading or a Pegamentos measurement.
- Component coverage is code-filtered to exactly the Manual's five families
  `MP_CM, MF, BQ, PU, CS` (`DapperControloProductionContextLookup.cs:96`);
  `controlo_sheet_items.family` has no CHECK (free text) — the domain/source
  filter is the only guard (optional hardening, §13).
- The sheet pins `job_on_id` + `job_on_revision_id` (N23:31-32) and events are
  append-only (`controlo_sheet_events` + trigger) — "Revisões posteriores não
  reinterpretam o histórico anterior" (Manual 20:477) is DB-enforced.
- The Controlo area is a single assignable unit (`ControloSheetModuleCatalog`
  capabilities `controlo.view/edit/submit/review`; `CanonicalModuleCatalog`
  `ControloAreaId` grants `peso`+`pegamentos`; `ControloFolhaTests.cs:22-40`
  pins the exact revision and five families).

**Conclusion:** no persistence should be removed and none should be added. The
"Resumo" is not a read-model that duplicates another authority; it is the
Folha's own record. Do **not** convert it to a derived view (the Manual's
workflow + "quem edita e quem revê" (20:399-403) requires writable per-piece
control facts).

---

## 9. Source-of-Truth Matrix

For each major Controlo concept — exactly one current authority; history/
snapshot copies are never live authority (prior audit principle A4).

### Concept: production / Job On identity
- **Authority:** `job_on` (PK `job_on_id`; `production_code`+`machine_code`
  partial unique `uq_job_on_identity`) + immutable `job_on_revision` graph.
- **Consumers:** Peso (FKs), Pegamentos (FKs), Folha (FKs), RI, Historia
  projection, JobOn itself.
- **Snapshot/history:** `peso_controlos.production_code/line/lote`,
  `pegamento_controlos.production_code/machine_code`,
  `controlo_sheets.production_code/reference/machine_code`,
  `jobon_user_current` (per-user active context), revision `production_snapshot`.
- **Potential duplicate:** the captured copies above (single-writer at
  creation; no independent writer). None compete.
- **Verdict:** **authoritative; copies = LEGITIMATE_DUPLICATION** (capture;
  no CHECK coherence vs `job_on`, code-maintained). No action.

### Concept: article/reference
- **Authority:** `peso_references` (mold_number+neckring_number unique) for the
  Peso reference identity; Job On's `article_reference_id`/snapshot for the
  article link (logical, dormant producer — U2/OD-14); `article_reference_images`
  for images.
- **Consumers:** Peso calculation (`volume_neck`, `volume_pu`), control rows
  (mold/neckring copies), Folha (`reference`), Pegamentos (`reference_snapshot`).
- **Snapshot/history:** `peso_controlos.mold_number/neckring_number` copies;
  `pegamento_controlos.reference_snapshot`; `controlo_sheets.reference`;
  revision `reference_snapshot`.
- **Potential duplicate:** none competing (all capture copies).
- **Verdict:** **authoritative; capture copies legitimate.** No action.

### Concept: machine
- **Authority:** `job_on.machine_code` (calendar line identity; line = machine
  per N05 header). 
- **Consumers:** Peso (`line` column), Pegamentos/Controlo (`machine_code`),
  day approvals (`line`), document routing (Line B/C groups).
- **Snapshot/history:** capture copies on control/pegamento/folha rows.
- **Potential duplicate:** none competing.
- **Verdict:** **authoritative; copies legitimate.** No action.

### Concept: CM identity
- **Authority:** Ferramentas master (`tool_references`/`tool_lotes`), referenced
  by the pinned `job_on_component` rows (`source_tool_id`/`source_lot_id`).
- **Consumers:** Peso (CM lot via revision), Pegamentos (CM snapshot/nominal),
  Folha items (`source_tool_id/lot_id` + snapshots), Comparação pairing
  (cm_number keys, explicit user pairing).
- **Snapshot/history:** `pegamento_controlos.cm_snapshot`,
  `controlo_sheet_items` snapshots, `peso_controlos.cm_snapshot` (dormant).
- **Potential duplicate:** none competing (snapshots are display context).
- **Verdict:** **Ferramentas authoritative; snapshots legitimate.** No action.

### Concept: Peso baseline (produção anterior aprovada)
- **Authority:** the explicit operator selection + server validation of an
  approved Novo Controlo, frozen into `peso_controlos.previous_control`
  (IMMUTABLE_SNAPSHOT — §5).
- **Consumers:** comparison creation, comparison decisions, Peso PDF (previous
  production line), history views.
- **Snapshot/history:** `previous_control` JSON (both sides); the dead
  `peso_comparacao_anterior` table (never populated).
- **Potential duplicate:** `peso_comparacao_anterior` (dead — **N37**).
- **Verdict:** **snapshot + selection flow authoritative; drop the dead mirror.**

### Concept: approved Peso reading
- **Authority:** `peso_leituras` rows under an approved `peso_controlos` row.
- **Consumers:** approvals, comparison baseline construction, PDF, calculations.
- **Snapshot/history:** `measurements_snapshot` JSON on the control; comparison
  snapshot per-CM weights.
- **Potential duplicate:** none.
- **Verdict:** **authoritative** (but see D-10 gap: rewritable at DB level —
  N40 closes; §6/§15).

### Concept: previous control (Comparação's baseline link)
- **Authority:** `previous_control` JSON snapshot on the comparison row (§5).
- **Consumers:** decision flow, PDF, history.
- **Snapshot/history:** the snapshot itself (immutable).
- **Potential duplicate:** `peso_comparacao_anterior` (dead).
- **Verdict:** **authoritative snapshot; dead mirror → N37.**

### Concept: comparison reading
- **Authority:** the comparison row's `peso_leituras` (current-production
  glass weights) + per-CM pairing in `previous_control.Rows`.
- **Consumers:** decisions (`comparison_decisions`), PDF rows.
- **Snapshot/history:** pairing + weights frozen in snapshot.
- **Potential duplicate:** none.
- **Verdict:** **authoritative.** No action.

### Concept: comparison result / out-of-average state
- **Authority:** `comparison_decisions` JSON on the comparison row (per-CM
  decision, justification when set aside); deltas are within the immutable
  snapshot. Manual 20:259-261: decisions explicit per CM.
- **Consumers:** confirm flow, Responsável decision, audit.
- **Snapshot/history:** decision JSON; audit_events `peso.comparacao.decidir`.
- **Potential duplicate:** none (deltas computed once at creation).
- **Verdict:** **authoritative, decision-only container.** No action.

### Concept: Pegamentos control
- **Authority:** `pegamento_controlos` + append-only `pegamento_medicoes` +
  1:1 `pegamento_documentos` (§7).
- **Consumers:** Pegamentos UI, PDF, history.
- **Snapshot/history:** control header capture; nominals frozen; measurements
  append-only.
- **Potential duplicate:** `nominal_average` (dormant — N38/N42 group) vs N16
  per-component nominals.
- **Verdict:** **authoritative; drop/retire dormant nominal_average (owner).**

### Concept: Controlo summary state (Resumo)
- **Authority:** `controlo_sheets` + items + append-only events (§8).
- **Consumers:** Folha UI, history list, decide flow.
- **Snapshot/history:** revision-anchored item snapshots + events.
- **Potential duplicate:** none (does not store Peso/Pegamentos values).
- **Verdict:** **authoritative materialized Folha state.** No action.

---

## 10. Legitimate Duplication

The following repeated values are **intentional and must NOT be normalized**
(per §10 of the task: separate facts/workflows keep separate tables; only
independently writable structures representing the same business fact are
duplication):

| # | Replicated data | Why legitimate (Manual + evidence) | Class |
|---|---|---|---|
| L-1 | Peso/Pegamentos/Folha rows carry `production_code`/`line`/`machine_code` copies | Capture copies written once from the pinned Job On context at creation; identity/search without joins; single writer (`PesoService`, `PegamentoService`, `ControloSheetService`); revision pin preserves attribution (Manual 20:74-85 shared context; 20:477 history immutability) | LEGITIMATE_DUPLICATION |
| L-2 | Comparison snapshot (`previous_control` JSON: both sides identities + per-CM weights) | Immutable fact fidelity (Manual 20:251-263; 20:477); never rewritten; the ONLY baseline authority | IMMUTABLE_SNAPSHOT |
| L-3 | Revision/component snapshots across Peso/Pegamentos/Folha/RI | Pinned to append-only revisions (N25 INT-10); snapshot ≠ live (GLM-DATA-04); Manual 20:477 | IMMUTABLE_SNAPSHOT / DERIVED |
| L-4 | Peso `measurements_snapshot` | Rebuilt at write by `BuildMeasurementsSnapshot` (DapperPesoRepository.cs:574-617) from the same aggregate; presentation/decision copy; never a second writer | DERIVED |
| L-5 | `pegamento_documentos` filename/output_root/production_folder snapshots | Document metadata captured at confirm time; structured record is the source, PDF derived (Manual 20:487-491) | IMMUTABLE_SNAPSHOT |
| L-6 | Controlo sheet item identity snapshots | Captured from the pinned revision at creation; later revisions must not reinterpret (N23; Manual 20:477) | IMMUTABLE_SNAPSHOT |
| L-7 | `jobon_user_current` production/reference/machine copy | Per-user active-context snapshot, upsert-only, never a global "newest Job On" (DapperJobOnUserContextRepository :11-15; Manual 20:89-95) | LEGITIMATE_DUPLICATION |
| L-8 | `peso_day_approvals` | Distinct day-approval fact, write-only (approval regulatory fact) | LEGITIMATE_DUPLICATION |
| DUP-1 | `peso_comparacao_anterior` vs snapshot+selection | **The only stored-vs-computed pair — and the table is dead (N37)** | LEGACY → REMOVE |

---

## 11. Confirmed Structural Problems

Each finding lists Manual evidence, migration evidence, source/Dapper evidence,
schema evidence, confidence, and remediation impact.

### P-1 — `peso_leituras` has no approved-parent DB guard (D-10/PC-09) — CONFIRMED
- Manual: approved data must not be silently rewritten — "A Comparação não
  altera o controlo base previamente aprovado" (20:263); history preservation
  (20:477-485); "a correção cria continuidade histórica, não substituição
  silenciosa" (20:481).
- Migration: `peso_leituras` created without a guard (N06:118-126); N25 added
  `ba_dmo_guard_peso_approved` on `peso_controlos` only (§1.7b).
- Source/Dapper: `UpdateControlAsync` = header UPDATE **then** `DELETE FROM
  peso_leituras` + re-INSERT with fresh UUIDs (`DapperPesoRepository.cs:359-375`);
  readings doc-comment claims append-only (PesoLeitura.cs:5-7).
- Schema: consolidated :687-695 — no trigger on `peso_leituras`; only
  `uq_peso_leituras_controlo_cm`.
- Confidence: HIGH (both static traces + contract audit PC-09).
- Remediation impact: **planned N40** trigger + service assertion (Queue B
  PC-09). Required pairing nuance — see §15 (approve/reopen flows rewrite
  readings after the parent status flip).

### P-2 — `pegamento_medicoes.contra_costura` NOT NULL vs nullable domain (D-12/PC-02)
- Manual: two perpendicular axes (20:301-316); the tolerance corridor applies
  to Média over both axes; one-sided measurement semantics are not defined by
  the Manual — owner decision D-12 branch A (nullable + domain completeness).
- Migration: N07:63 `NOT NULL`; domain supports `decimal? contraCostura`
  (`PegamentoControlo.AddMeasurement`); calculator one-sided support.
- Schema: consolidated :760.
- Confidence: HIGH.
- Remediation impact: **planned N39** (DROP NOT NULL + domain rule, same
  release). UNCHANGED by this audit.

### P-3 — Baseline-snapshot requirement is code-enforced only
- Manual: comparison preserves traceability (20:261-263); association
  explicit/validated (20:208) — no mechanism mandated.
- Migration: N06 has no CHECK relating `record_type` to `previous_control`.
- Source: single writer enforces it (`PesoService.CreateComparisonAsync`).
- Schema: `previous_control` jsonb NULL, no CHECK (consolidated :667).
- Confidence: HIGH (behavior) — impact of the *gap*: LOW (single writer).
- Remediation impact: **optional** additive CHECK
  `(record_type='comparacao') = (previous_control IS NOT NULL)` inside a planned
  migration wave (§16). NOT required for baseline safety.

### P-4 — Comparison row identity columns describe the baseline production, Job On pins the current production (design nuance)
- Manual: Comparação occurs during production (20:249-263) and is a record of
  the control carried out on the current context while referencing the
  base — no column-level prescription.
- Migration/schema: the comparison row's `production_code`,`line`,`lote`,
  `mold_number`,`neckring_number` are copied from the approved control
  (`PesoService.cs:634-639`) while `job_on_id`/`job_on_revision_id` are the
  current production's (`:641-642`); no coherence CHECK exists.
- Evidence: `PesoService.cs:610-651`; snapshot holds both sides completely, so
  nothing is lost.
- Confidence: HIGH (facts) / the "problem" characterization is a design
  judgment: MEDIUM.
- Impact: **display/search semantics only** (a comparison lists under the
  baseline production code); no data conflict, no competing authority. Owner
  awareness item (OD-N2, §17) — either confirm as intended or switch the
  identity copy to the current production (code-only change, no DDL).

### P-5 — `peso_controlos.cm_snapshot` and `approval_log` dormant/dead declarations (PESO-06/PESO-10)
- Manual: CM context is a required input/display; approval trail belongs to
  history (20:185-194 inputs; 20:166 decisions + history).
- Source: `CreateControlAsync` binds `CmSnapshot = CmSnapshotJson ?? DBNull`
  and the service never sets it (`PesoService.cs:356-387`); `ApprovalLogJson`
  always persisted as `"[]"` (approve/reject/reopen don't append to it).
- Schema: columns exist (consolidated :663, :666).
- Confidence: HIGH.
- Impact: dormant capacity, not a functional defect; PESO-06/PESO-10 prior
  findings. **No action required before baseline** (owner product decision on
  whether to populate `approval_log` via code later — Queue B-lite; keep the
  columns).

### P-6 — `pegamento_controlos.nominal_average` dormant (PG-10/C3)
- Manual: per-component nominals are required (16: "a single nominal_average
  must NOT validate all three components"); authority = N16 per-component
  nominals.
- Source: zero reads/writes (`DapperPegamentoRepository` SELECT/INSERT lists
  use cm/bq/mf_nominal only; `nominal_average` absent).
- Schema: consolidated :738.
- Confidence: HIGH.
- Remediation impact: owner decision (OD-9) — drop with the N38 group or N42.

### P-7 — `controlo_sheet_items.family` has no CHECK (five-family rule is code-filtered)
- Manual: "O Resumo / Folha de Controlo cobre exatamente: CM, BQ, MF, PU, CS.
  Não deve ser expandida… sem autoridade funcional explícita" (20:375-381).
- Source: `DapperControloProductionContextLookup.cs:96` filters
  `family IN ('MP_CM','MF','BQ','PU','CS')`.
- Schema: `family text NOT NULL` no CHECK (N23:68; consolidated :1201).
- Confidence: HIGH (gap), LOW severity (single writer).
- Remediation impact: **optional** additive CHECK on the five family values —
  could ride a planned wave; not required (writer is code-filtered; schema
  family keys are `MP_CM` vs Manual's "CM" — presentation mapping, so a DB
  CHECK must be written against storage keys, an owner-visible decision).

### P-8 — Comparison row creation is not idempotent per current control (unique-collision edge)
- Manual: a comparison may measure one or more CMs (20:255-256); nothing
  mandates multiple comparison records per control.
- Source: `CreateComparisonAsync` has no "comparison already exists for this
  draft" guard; repeated identical creations collide on
  `uq_peso_controlos_identity` (same mold/neckring + baseline production/line/
  lote + same control_date) → latent 23505.
- Schema: `uq_peso_controlos_identity` (N06:99-100).
- Confidence: MEDIUM (reachability depends on UI flow; API is callable).
- Impact: code robustness (idempotency/unique-violation mapping), not schema.
  Defer to the N40-era code change set (Queue B wave) or F17-adjacent code
  cleanup. **No new migration.**

### P-9 — Guid.Empty sentinel FK risk (PESO-05) — carries as deferred
- Evidence: `PesoService.cs:359-360` binds `Guid.Empty` into real FKs when the
  reference/lot is unresolvable → latent 23503.
- Schema: FK columns NOT NULL (`peso_reference_id`, `peso_lote_id`).
- Confidence: HIGH (pattern), LOW (observed trigger — requires a reference
  resolution miss).
- Impact: deferred service-level pre-validation (Df-5). No schema change;
  unchanged by this audit.

---

## 12. Legacy / Compatibility Candidates

| Object | Status | Evidence | Disposition |
|---|---|---|---|
| `peso_comparacao_anterior` (table) | LEGACY (dead mirror) | zero SQL in src; only doc comments (IPesoRepository.cs:9, DapperPesoRepository.cs:14, PesoControl.cs:220); never wired; D-9 = REMOVE_LATER | **N37** drop (data-checked: row-count 0 probe §14.3 of the rationalization plan) |
| `PesoControloAnterior` (domain record) + stale doc comments | LEGACY (code/doc) | `PesoControl.cs:223-227` — zero usages; comments name the dead table | Code cleanup riding the N37 change set (rationalization plan §13.4/§10.4) |
| `peso_controlos.cm_snapshot` | DORMANT (declared) | never populated (P-5) | KEEP (no action; product decision later) |
| `peso_controlos.approval_log` | DORMANT-FUTURE (always `[]`) | P-5/PESO-10 | KEEP (write-only by design; future approval trail) |
| `pegamento_controlos.nominal_average` | DORMANT (superseded) | zero reads/writes (P-6/PG-10) | Owner decision (OD-9): drop with N38 group or N42 |
| `job_on_revision.image_asset_id` | LEGACY (dormant mirror, not Controlo-owned) | N29 superseded; N38 (D-11) | **N38** (unchanged by this audit) |
| `ix_pegamento_documentos_controlo` | REDUNDANT index | duplicates the UNIQUE constraint index (N14:12,20-21) | **N35** drop (unchanged) |
| `job_on_field_option` / `tampao_planos` | DORMANT (out of Controlo scope) | D-7/D-8 | KEEP (DEFER) — unchanged |

No *table* in the Controlo area is a compatibility twin of a live structure
except the dead `peso_comparacao_anterior`; everything else is a dormant
column or a redundant index already dispositioned in N34–N42.

---

## 13. Constraint / FK / Index Findings

| # | Finding | Evidence | Verdict / Action |
|---|---|---|---|
| CF-A | Peso/Pegamentos/Folha **FKs to `job_on` + `job_on_revision` are NOT NULL and DB-enforced** — the Controlo↔JobOn relationship is physical, not application-only | N06:86-87; N07:31-32; N23:31-32; consolidated :661-662/:730-731/:1169-1170 | **PASS** — core requirement of §4/§7 met. |
| CF-B | Revision anchors are **immutable**: append-only triggers on the 4 revision-family tables (N25 INT-10); approved `peso_controlos` rows protected (`ba_dmo_guard_peso_approved` + `ck_peso_controlos_approved_consistent`) | N25 §1.7/§1.7b/§1.9; consolidated :1477-1517/:1539-1558 | **PASS** — history cannot be silently rewritten (20:477-485). |
| CF-C | `record_type` CHECK physically encodes Comparação-inside-Peso | N06:101-102 | **PASS** — hierarchy encoded at schema level. |
| CF-D | **No** CHECK `comparacao ⇒ previous_control NOT NULL` | N06; consolidated :667 | **OPTIONAL additive** (P-3) — not required. |
| CF-E | `peso_leituras` lacks approved-parent guard | N06; consolidated :687-695 | **N40** (planned) with code pairing (§15). |
| CF-F | `ck_peso_controlos_approved_consistent` covers only `status='aprovado' ⇔ approved_at_utc` — approvals are one-to-one; reopen clears both | N25:123-131; `PesoControl.Reopen` | **PASS** (coherent). |
| CF-G | `uq_peso_controlos_identity` — per-control uniqueness (mold, neckring, production, line, lote, date) | N06:99-100 | **PASS**; note P-8 edge (comparison idempotency) — code, not schema. |
| CF-H | CASCADE semantics on `peso_comparacao_anterior`/`peso_leituras` ↔ `peso_controlos` | N06:120,135 | Safe today (approved rows undeletable; rascunho/nao_aprovado deletes cascade readings). N37 removes the dead companion; CF-6 in the rationalization plan: **DO NOT TOUCH**. |
| CF-I | `pegamento_medicoes` has **no CHECK** (component_key, tool_number, positivity) on raw columns — values are codec/writer-controlled | N07; consolidated :755-764 | Acceptable (append-only + single writer); optional hardening not required. |
| CF-J | `controlo_sheet_items.family` no CHECK | N23:68 | Optional hardening (P-7); code-filtered today. |
| CF-K | `peso_controlos` `cm_snapshot`/`approval_log` JSON columns exist but are never/`[]`-populated | P-5 | KEEP (dormant capacity). |
| CF-L | Index coverage: `ix_peso_controlos_status_date` serves the approved-list selection (`GET /api/peso/controls?status=aprovado&type=novo_controlo` → `GetControlsAsync` ORDER BY control_date); per-control leituras N+1 is served by `uq_peso_leituras_controlo_cm` | N06:110,:125; §8.4 of rationalization plan | **PASS** (no new index needed; N+1 accepted at current scale). |
| CF-M | All `ON CONFLICT` arbiters exist (`peso_day_approvals` uq, `peso_settings` PK) | N06; consolidated | **PASS**. |

---

## 14. Migration Evolution Findings

Order of construction and what it tells us (all evidence from the migration
files, final provenance in `consolidated_clean_install.sql`):

1. **Peso was created with Comparação from the start** (N06: `record_type`
   column `novo_controlo/comparacao` at :79, CHECK at :101-102). Comparação was
   **not** added later as a separate module or table — the canonical
   relationship (one Peso area, two workflows) is present in the very first
   Peso migration. Confidence: HIGH.
2. **A compatibility structure was left behind at N06:** `peso_comparacao_anterior`
   (:134-140) was an auto-resolution read-path design that **never had a
   writer**. Historical debt, not a current correctness problem — the live
   comparison path went directly to explicit selection + snapshot. Confidence:
   HIGH.
3. **Pegamentos grew additively and cleanly:** N07 (base) → N14 (documents,
   1:1) → N15 (tool_number) → N16 (per-component nominals, which **superseded
   `nominal_average`** — the superseded column was left NULL-able and never
   dropped) → N17 (notas). No table partially replaced another; the only
   duplicated-column artifact is the dormant `nominal_average`. Confidence:
   HIGH.
4. **The Folha de Controlo was added later (N23)** as its own additive family
   under an explicit owner decision ("OWNER DECISION 'TARGET CONTROLO' +
   'MODULE IDENTITY' (R010)" — N23:4-5; "distinct from Peso and
   Pegamentos/Ferramentas; no schema or logic merge" — N23:7-8). This is
   exactly the §10 pattern: functional grouping without table merging.
   Confidence: HIGH.
5. **Enforcement arrived in waves:** N25 added the approved-control guard,
   status CHECKs (`ck_pegamento_controlos_status`, `ck_peso_controlos_approved_consistent`,
   `ck_job_on_lifecycle_consistent`) and the revision-family append-only
   triggers — the relationships were functionally present earlier but only
   DB-enforced at N25. The one remaining unenforced boundary is
   `peso_leituras` under approved parents (D-10 → N40). Confidence: HIGH.
6. **Consolidated baseline parity:** the consolidated file reproduces the chain
   final state for every Controlo object (verified by full-file read this
   session: tables/constraints/indexes/triggers/functions/RLS all match the
   migration-derived inventory; residual drifts D-A…D-D are the documented
   non-Controlo items from the rationalization plan §2.3). Confidence: HIGH.
7. **Obsolete indexes/constraints:** only `ix_pegamento_documentos_controlo`
   (redundant, N14) — planned N35. No stale trigger/function in the Controlo
   area (all 19 triggers referenced; §9.5 of the rationalization plan).
   Confidence: HIGH.
8. **Stale schema artifacts:** `nominal_average` (P-6), `cm_snapshot`/
   `approval_log` dormancy (P-5), dead `peso_comparacao_anterior` (N37),
   `job_on_revision.image_asset_id` (N38, non-Controlo). All dispositioned.

**Historical debt vs current correctness:** every item above that matters for
current correctness is either already fixed (N25 enforcement, Queue A PC-01)
or already planned (N35/N37/N38/N39/N40/N42); none is a newly discovered
correctness defect.

---

## 15. Impact on N34–N42 Plan

Re-evaluation of the rationalization migrations that this Controlo clarification
touches. Status legend: **UNCHANGED · MODIFY_DESIGN · REORDER · DEFER ·
BLOCKED_OWNER_DECISION**.

| Migration | Current plan | Controlo-review outcome | Status |
|---|---|---|---|
| N34 (access-mirror removal) | unchanged | No Controlo dependency (zero src refs re-verified; access chain is D-1/D-2, not Controlo data) | **UNCHANGED** |
| N35 (index hygiene) | unchanged | `ix_pegamento_documentos_controlo` redundancy re-confirmed (N14:20-21 duplicates the UNIQUE index); BQ-16 index unrelated | **UNCHANGED** |
| N36 (policy rename) | unchanged | No Controlo impact | **UNCHANGED** |
| **N37** (drop `peso_comparacao_anterior`) | drop dead mirror (D-9) | **Concept re-confirmed by the canonical Controlo relationship**: the Manual never mandated an automatic previous-approved materialization (20:208 explicit/validated; §2 terminology note), the table has zero SQL in src and zero writers ever. The live authority is the explicit selection + `previous_control` snapshot (§5). Add to the same change set: refresh stale doc comments (`IPesoRepository.cs:9`, `DapperPesoRepository.cs:14`, `PesoControl.cs:220`) and remove the dead `PesoControloAnterior` record (PesoControl.cs:223-227) — all as already noted in the plan (§13.4/§10.4). No renumbering | **UNCHANGED** |
| N38 (drop `modules_override` + `image_asset_id`; optional `nominal_average`) | unchanged | `nominal_average` (P-6/PG-10) remains an owner-optional item in this group; Controlo evidence unchanged (zero reads/writes) | **UNCHANGED** (OD-9 still open) |
| **N39** (`contra_costura` DROP NOT NULL) | D-12 branch A (nullable + domain rule) | **Re-confirmed with Manual depth**: two-axis contract (20:301-316) defines the corridor on the two-axis Média; one-sided records are representable in the calculator (`ContraCostura null → ovalização null, média = single value`) and in the domain (`decimal? contraCostura`), so NOT NULL is the only structural blocker for the recorded owner choice. Manual does not explicitly bless one-sided measurements — it remains the owner decision (OD-2). No dependency on other N34+ items beyond "same release as the domain rule" | **UNCHANGED** |
| **N40** (`peso_leituras` approved-parent guard) | new trigger + service assertion | **CONCEPT CONFIRMED** (P-1; Manual 20:263/20:477-485; GLM-PESO-06.7). **DESIGN REFINEMENT REQUIRED — the trigger as naively designed breaks the approve/reopen flows**: `UpdateControlAsync` performs header UPDATE (which flips `status` to `aprovado` during approval) **before** `DELETE FROM peso_leituras` + re-INSERT in the same transaction (`DapperPesoRepository.cs:332-375`); a row-level trigger raising when `parent.status='aprovado'` would fire on that DELETE and fail the approval itself. **Required pairing (same change set):** restrict readings DELETE+INSERT to the draft-edit path only — i.e., split `UpdateControlAsync` or route approve/submit/reject/reopen/decide through a header-only update that never rewrites readings (approval/decision carry no new measurement data anyway). The service assertion (Queue B PC-09) becomes the primary gate; the trigger the backstop. Ordering note: reopen (aprovado→rascunho, header first) remains compatible once the rewrite is confined to drafts | **MODIFY_DESIGN** (concept unchanged; migration number unchanged) |
| N41 (warehouse per-position unique) | unchanged | No Controlo impact | **UNCHANGED** |
| N42 (PA-01 / FA-05) | unchanged | No Controlo impact (tool_check_occurrences / physical_pieces) | **UNCHANGED** |
| NEW (optional, rides a planned wave) | — | Additive CHECK `(record_type = 'comparacao') INTO ('novo_controlo','comparacao')` — precisely: `CHECK (record_type <> 'comparacao' OR previous_control IS NOT NULL)` on `peso_controlos` (P-3). **Optional** — DB-enforces the baseline-snapshot requirement; can ride the N37 or N40 change set, or the N40 code pairing release. Does not change any migration number | **UNCHANGED numbering + optional additive** |

**Ordering dependencies re-checked:** N40 after N37 remains correct (the N37
drop does not touch `peso_leituras`; the D-10 guard interacts with the
controlo delete path as the plan states). Nothing in this review evidences a
dependency from N34–N36 onto Controlo objects, so **N34–N36 remain unchanged**
as required. No renumbering is needed anywhere.

**Previously unplanned Controlo rationalization genuinely required before
baseline freeze:** **none** (see §16). The only newly surfaced items are
optional (P-3 CHECK, P-7 family CHECK) or code-level (P-4 owner awareness,
P-8 idempotency, N40 pairing).

---

## 16. Additional Migration Needed Before Baseline — **NO**

**NO additional migration is required before the baseline freeze.**

- The only new DDL *candidates* surfaced by this audit are **additive and
  optional** (P-3 `comparacao ⇒ previous_control` CHECK; P-7
  `controlo_sheet_items.family` CHECK on storage keys), and each can ride an
  already-planned migration wave (N37/N40/N42 group) without creating a new
  migration file or renumbering.
- The N40 design refinement (§15) is a **code pairing of an already-planned
  migration**, not a structural change to the plan's DDL inventory (same
  trigger, same target table, same numbering).
- Everything else the audit confirms is already dispositioned in N34–N42
  (N37, N39, N35 index, N38/N42 nominal_average), or explicitly deferred as
  code-only (PESO-05, PESO-06/PESO-10 posture, P-4/P-8, Queue B).

**The consolidated baseline can be regenerated at the planned final state
(D-16 Phase G) without any Controlo-specific adjustment beyond what the
N34–N42 sequence already produces.**

---

## 17. Owner Decisions Required

Existing decisions re-confirmed (no action beyond execution Go already
required by the plan):

- **OD-2 (D-12 branch)** — `contra_costura` nullability: **A** (nullable +
  domain completeness rule) recommended; N39. Unchanged.
- **OD-3 (D-10 Go)** — approved-readings guard: **A** recommended; N40 —
  now with the explicit readings-rewrite pairing requirement from §15.
- **OD-7 (D-9 execution Go)** — drop `peso_comparacao_anterior`: approved
  default stands; N37. Unchanged and re-confirmed.
- **OD-9 (nominal_average / sap_end)** — drop `nominal_average` with the N38
  group (or N42); Controlo evidence unchanged.

New decision items introduced by this audit:

- **OD-N1 — Comparison identity-column semantics (P-4).** Confirm that the
  comparison row's plain `production_code`/`line`/`lote`/`mold_number`/
  `neckring_number` columns legitimately describe the **baseline** production
  (current production appears only in `job_on_id`/`job_on_revision_id` + the
  snapshot `Current*` fields). **RECOMMENDED: confirm as intended** (record is
  a Peso control of the baseline's identity being compared; display works via
  the snapshot). Alternative (switch copies to the current production) is a
  code-only behavioral change, no DDL — must NOT be taken silently before
  baseline because it alters `uq_peso_controlos_identity` semantics.
- **OD-N2 — Optional additive CHECKs (P-3, P-7).** Approve (recommended) or
  decline folding `comparacao ⇒ previous_control IS NOT NULL` and (optionally)
  the `controlo_sheet_items.family` storage-key CHECK into the planned wave.
  Default: approve P-3 (tiny, matches the single-writer invariant); P-7 needs
  a storage-key decision (`MP_CM` vs user-facing "CM") before writing the
  CHECK.
- **OD-N3 — Dormant Peso columns (P-5/PESO-06/PESO-10).** Confirm
  `cm_snapshot` (never populated) and `approval_log` (`[]`-only) remain as
  declared capacity; recommend KEEP with a code-population decision for
  `approval_log` in the Queue B wave (no DDL either way).

Carried owner items (out of Controlo scope, unchanged): OD-1 (N34 Go),
OD-4/OD-8/OD-10/OD-11/OD-12/OD-13/OD-14/OD-15/OD-16, FA-05, PA-01.

---

## 18. Final Recommendation

**Can the current Controlo schema safely become part of the new post-Codex
stable database baseline as-is, subject only to the already-planned N34–N42
rationalization?**

### **YES.**

Conditions (all within the already-planned sequence — none adds a migration):

1. **N37** proceeds as planned (drop the dead `peso_comparacao_anterior` +
   refresh its stale doc comments and the dead `PesoControloAnterior` record).
   This audit provides the canonical evidence the plan asked for: the Manual
   neither mandates nor names an automatic previous-approved resolution, and
   the live authority is the explicit selection + `previous_control` immutable
   snapshot.
2. **N39** proceeds as designed (D-12 branch A, owner-gated); no change.
3. **N40** proceeds with its **design refinement**: the readings-rewrite path
   (`UpdateControlAsync` DELETE+re-INSERT) must be confined to the draft-edit
   flow in the same change set, so the new approved-parent trigger cannot
   break approve/reopen/decision transitions.
4. **Optional (recommended, rides the planned wave):** add the additive CHECK
   `record_type = 'comparacao' ⇒ previous_control IS NOT NULL`; optionally the
   `controlo_sheet_items.family` storage-key CHECK. Both are hardening, not
   blockers.
5. All other Controlo objects enter the baseline unchanged: the Job On
   relationship is physically FK-enforced and revision-pinned; Comparação is a
   record-type inside Peso with an explicit, snapshot-persisted baseline and
   no competing authority; Pegamentos is correctly independent in its own
   facts and correctly anchored to the same Job On context; the Folha de
   Controlo is a materialized, authoritative record that duplicates no Peso or
   Pegamentos state; and every identified legacy/dormant artifact is already
   dispositioned in N34–N42.

**Residual risks accepted (already deferred in the plan, re-confirmed here):**
PESO-05 Guid.Empty pre-validation (code), PESO-06/PESO-10 dormant columns
(product posture), P-4 comparison identity naming (owner awareness, no DDL),
P-8 comparison-creation idempotency (code robustness in the N40-era wave),
Queue B audit-emission gaps (JobOn/Pegamentos — code-only).

---

## Audit validation checklist

- ✅ Manual read in full for CONTROLO (20), Job On (10), Index (00),
  Modules Operational (02, Controlo sections); zero Manual references found to
  any technical column name (schema names validated against concepts).
- ✅ All 33 migrations read in full or exhaustively grepped; Controlo object
  provenance traced (N06, N07, N13-N17, N23, N25 primary).
- ✅ Consolidated clean-install baseline read in full; every Controlo object
  and the N25 guard/approval/postures verified by line (616-791, 1164-1247,
  1472-1560).
- ✅ Dapper/source readers-writers traced for every Controlo surface
  (`DapperPesoRepository`, `DapperPegamentoRepository`,
  `DapperControloSheetRepository`, `DapperControloProductionContextLookup`,
  `DapperJobOnUserContextRepository`, `DapperJobOnRepository`); direct SQL
  outside these files: none (grep-verified).
- ✅ Services/domain traced (`PesoService`, `PegamentoService`,
  `ControloSheetService`, `PesoControl`, `PesoLeitura`, `PegamentoControlo`,
  `ControloFolha`/Context, catalogs, `AccessResolver`).
- ✅ Tests reviewed (Peso domain/service/comparison guards, ControloFolha/
  service, Pegamentos calculator/document/history/Postgres, RemediationGuard
  DB-level approved guard, MigrationDiscovery family list, projection guards).
- ✅ Source-of-truth matrix completed for the 11 required concepts; every
  repeated value classified (AUTHORITATIVE / IMMUTABLE_SNAPSHOT / DERIVED /
  LEGITIMATE_DUPLICATION / LEGACY / CONFLICTING_SOURCE_OF_TRUTH / UNKNOWN —
  no UNKNOWN remains).
- ✅ N34–N42 re-evaluated (N37/N39 UNCHANGED, N40 MODIFY_DESIGN with code
  pairing; N34–N36/N38/N41/N42 unchanged; no renumbering).
- ✅ Final question answered: **YES**, with the minimum carry-ons listed.
- ✅ NO implementation, NO DDL/DML, NO database mutation; the only artifact
  produced is this report.

— End of report.