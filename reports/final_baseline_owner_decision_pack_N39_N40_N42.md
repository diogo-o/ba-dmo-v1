# Final Baseline Owner Decision Pack — N39 / N40 / N42

> **Repo:** `diogo-o/ba-dmo-v1` · **Branch:** `main`
> **Scope:** DECISION PREPARATION ONLY — no implementation, no DB mutation.
> **Live state:** N25, N34, N35, N36, N37, N38, N41 PASS; LIVE BASELINE PASS.
> N39/N40/N42 are the only remaining owner-gated surfaces (no migration files
> exist past N41; the live baseline is the post-N41 consolidated state).
> **Authorities:** Manual `AI-CONTEXT/docs/Manual/*`; `post_codex_database_rationalization_plan.md`
> (§11 OD-2/OD-3/OD-5/OD-6, §5.1/T4, §13.6/13.7/13.9, §14.8);
> `post_codex_remediation_functional_gate.md` (F2, F9/PC-09, F15, F16, F17);
> `controlo_schema_alignment_prebaseline_audit.md` (§15, §17);
> `pre_N34_PROD_function_trigger_drift_reconciliation.md` (§5/§7);
> `schema_rationalization_owner_decisions.md` (D-10/D-12 registers);
> `schema_rationalization_target_architecture.md` (B7).

---

## N39 — PEGAMENTOS `contra_costura`

**Question:** Should Pegamentos allow a valid measurement/control where only one
measurement side/value is present?

**CURRENT_SCHEMA:**
- `pegamento_medicoes.contra_costura numeric(18,4) NOT NULL` — N07:63;
  identical in the live consolidated baseline (`database/consolidated_clean_install.sql:773`).
- Table is append-only fact stream: PK + FK→`pegamento_controlos`, `component_key`,
  `costura NOT NULL`, `contra_costura NOT NULL`, N15 `tool_number NULL`,
  trigger `trg_pegamento_medicoes_append_only` (`ba_dmo_guard_append_only`).
  No CHECK, no alternative constraint, no 0-fill on the column.

**CURRENT_CODE_BEHAVIOR:**
- Domain accepts one-sided measurements: `PegamentoControlo.AddMeasurement(…, decimal costura, decimal? contraCostura …)`
  (`PegamentoControlo.cs:180–239`) — no rule requiring `contraCostura`.
- Calculator explicitly supports one-sided: `Ovalizacao = costura − contraCostura` → **null** when
  contra-costura missing; `Media = (costura + contraCostura)/2` → **= costura** (single value)
  (`PegamentoMeasurementCalculator.cs:12–25`). Tolerance corridor is checked against Média
  (`PegamentoControlo.cs:226–233`), so a one-sided measurement still gets a tolerance verdict.
- Dapper binds `ContraCostura = (object?)medicao.ContraCostura ?? DBNull.Value`
  (`DapperPegamentoRepository.cs:261`) — i.e. a one-sided insert is always attempted.
- **Result:** a one-sided measurement passes the domain and is rejected by the DB with a raw
  `23502 NOT NULL violation` at the API boundary (Gate F2; audit DT-01; §14 plan).
- Web: `POST /api/pegamentos/{id}/measurements` (`Program.cs:621–632`); service path
  `PegamentoService.AddMeasurementAsync` performs no one-sided validation today.

**MANUAL_EVIDENCE:**
- `20_CONTROLO_FUNCTIONAL.md` §7 (Pegamentos): "Costura = 0° · Contra costura = 90° — os dois
  eixos são perpendiculares; as medições são registadas por linha/componente" (:301–304);
  "Ovalização = Costura − Contra costura" (:308); "Média = (Costura + Contra costura) / 2" (:314);
  "A Média representa o valor médio entre as duas medições… não substitui as medições
  individuais" (:316, :328); tolerance corridor Nominal ± 0.20 with boundary-as-alert (:332–344).
- **The Manual defines a two-axis dimensional model and defines no one-sided case** — a
  measurement without contra-costura is **neither declared valid nor declared invalid**
  (Gate F2; controlo audit §15 re-confirms: "Manual does not explicitly bless one-sided
  measurements — it remains the owner decision (OD-2)").
- The old-design Pegamentos data contract contains no nullable contra-costura declaration and is
  not functional authority (Gate F2).
- Recorded D-12 register default is "make the column nullable + domain rule" (`schema_rationalization_owner_decisions.md` §D-12; `..._plan.md` §11 OD-2 recommended default), but the Gate classifies F2/PC-02 as **OWNER_DECISION_REQUIRED** — the recommendation is not a taken decision.

**OPTIONS**

### A. REQUIRE_BOTH_VALUES
- **User-visible behavior:** one-sided submissions are rejected with an actionable structural/validation error (e.g. `PEGAMENTO_CONTRA_COSTURA_REQUIRED`) instead of a raw `23502`; the measurement form remains two-field. No behavior change for two-sided measurements.
- **DB effect:** none — `contra_costura` stays `NOT NULL` (N07 DDL unchanged).
- **Dapper/service effect:** service/domain gains a completeness validation rejecting `contraCostura == null` in `AddMeasurement` (or the request mapper); Dapper unchanged (already binds non-null).
- **Validation effect:** domain and DB agree (require both axes); the calculator's one-sided branch becomes dead code (or retained defensively); Ovalização/Média always defined.
- **Migration requirement:** **NONE** (schema untouched). Code-only change set.
- **Risk:** LOW operationally, but product-visible: if a real one-sided reading is ever required (e.g. partially rejected/round piece), the operator cannot record it at all — the Manual's silence means this becomes an invented restriction.
- **Recommendation (neutral, no intent inferred):** justified only if the owner reads the Manual's two-axis model (20:301–316) as "both axes are always mandatory". Note this conflicts with the recorded D-12 default (nullable).

### B. ALLOW_ONE_SIDED_MEASUREMENT
- **User-visible behavior:** a measurement with only `costura` persists successfully; Ovalização displays blank/undefined, Média = the single value, and the tolerance corridor is evaluated on that single value (calculator already defines this, `PegamentoMeasurementCalculator.cs:12–25`).
- **DB effect:** `DROP NOT NULL` on `pegamento_medicoes.contra_costura` (widening; existing rows untouched — all current rows are non-null, so no backfill); append-only trigger unaffected.
- **Dapper/service effect:** repository already binds nullable — **no repository change**; service/domain adds the **completeness rule** (measurement must have `costura`; `contra_costura` optional with explicit semantics) so the relaxed column is never ungoverned.
- **Validation effect:** domain rule governs: `costura` required (already implicit), `contra_costura` optional; one-sided no longer dies with `23502`.
- **Migration requirement:** **N39** — `ALTER TABLE pegamento_medicoes ALTER COLUMN contra_costura DROP NOT NULL;` + refreshed consolidated baseline (column DEF NULL) + **same-release** domain rule (avoid transient nullable-with-no-rule state; `..._plan.md` §12 ordering dep #4).
- **Risk:** LOW (widening; rollback = re-apply NOT NULL after a null-absence check). The one-sided case becomes a product behavior the Manual does not explicitly bless — acceptable only as an explicit owner choice.
- **Recommendation (neutral, no intent inferred):** matches the recorded D-12 register default and every audit recommendation (OD-2 recommended default; controlo audit §15 UNCHANGED), and is the only option that makes the existing domain/calculator one-sided capability reachable. **Must be confirmed by the owner — the Manual is silent.**

---

## OWNER DECISION N39 REQUIRED:
A or B

---

## N40 — PESO APPROVED READING PROTECTION

Not an open functional question — canonical rule (Manual 20:263, 20:477, 20:481, 20:485) is that an
approved Peso baseline must not be silently rewritten, and prior audits (Gate F9/PC-09; controlo
audit §15, status **MODIFY_DESIGN**) already refined the design. The exact technical design follows.

### Current write path
- API (`Program.cs`): `POST /api/peso/{id}/save|submit|approve|reject|reopen|delete`,
  `POST /api/peso/{id}/compare/decide`, `POST /api/peso/comparison` → `PesoService`.
- `PesoService` loads the control + readings (`GetControlByIdAsync`), mutates, then calls
  **one** repository method for every non-create flow:
  `UpdateControlAsync` (`DapperPesoRepository.cs:328–378`) =
  1. header `UPDATE peso_controlos` (incl. `status`, `measurements_snapshot`, `approval_log`,
     `approved_by/at_utc`),
  2. `DELETE FROM peso_leituras WHERE peso_controlo_id = @…` (:359),
  3. re-`INSERT` each reading (:362–375) — same `peso_leitura_id` when loaded (destroying rows and
     resetting `created_at_utc` on every save), new id when `Guid.Empty`.
- Create path (`CreateControlAsync`, :202–259): INSERT control (status `rascunho`) then INSERT
  readings — inside one UoW transaction (`DapperUnitOfWork.RunAsync`).
- Controls on `peso_controlos`: `status CHECK ('rascunho','pendente','aprovado','nao_aprovado')`
  (N06:103–104); N25 §1.7b guard `ba_dmo_guard_peso_approved` protects only the **approved parent
  row** (no DELETE; no identity-column change) and explicitly leaves non-identity columns and
  `peso_leituras` updatable (`N25_remediation.sql:137–165`; `pre_N34...drift_reconciliation.md` §5).

### Unsafe write path
- `PesoService.SaveControlAsync` (:389–413) gates edits with
  `PesoValidator.ValidateControlEditable(status, ChangeReason)` (`PesoReference.cs:93–105`), which
  **only requires a non-empty reason** for states outside `rascunho/nao_aprovado`. Therefore a save
  against an **approved** control (with a change reason) is accepted, and `UpdateControlAsync`
  rewrites `peso_leituras` (DELETE+re-INSERT, destroying the fact chain) while leaving
  `status='aprovado'` — a silent rewrite (Gate F9 evidence, :532–535).
- No DB backstop exists on `peso_leituras` (N06:118–126 has only the UNIQUE).

### Desired write boundary
- **Readings (`peso_leituras`) DML is allowed only while the parent control is `rascunho` or
  `nao_aprovado`.**
- Header fields may always be updated under the existing N25 guard (which remains in force).
- Approved control state changes (reopen) happen **header-only**, never touching readings; any
  readings edit on an approved control requires the audited **reopen** first (revision+1, mandatory
  reason — `PesoControl.Reopen` :192–204).
- Service assertion = **primary gate**; trigger = **DB backstop** (Gate F9; controlo audit §15).

### Required Dapper/service change (same change set as the migration)
1. **Split the write path in `DapperPesoRepository`:** introduce
   `UpdateControlHeaderAsync` (header `UPDATE` only — no `DELETE`/`INSERT` on `peso_leituras`) and
   keep `UpdateControlAsync` as the **draft-rewrite** path (header + readings DELETE/INSERT).
2. Route header-only transitions through `UpdateControlHeaderAsync`:
   `SubmitControlAsync`, `ApproveControlAsync`, `RejectControlAsync`, `ReopenControlAsync`,
   `DecideComparisonAsync` (`PesoService.cs:415–430, 434–500, 727`). These flows carry **no new
   measurement data** (approval/decision write only `status`/`approval_log`/`comparison_decisions`),
   so nothing is lost.
3. `SaveControlAsync` keeps the full (draft) rewrite, and gains the **PC-09 service assertion**:
   loaded `Status` must be `rascunho` or `nao_aprovado`; otherwise fail
   (`PESO_CONTROL_READINGS_LOCKED` / "Reabrir primeiro…"). Tighten
   `PesoValidator.ValidateControlEditable` so a non-empty reason alone no longer permits editing
   `pendente`/`aprovado` in place (fixes the Gate-F9 gap where a ChangeReason passes an approved
   save; the edit-then-reopen ordering is contra the Manual 20:441–452).
4. Refresh stale Peso doc comments referencing `peso_comparacao_anterior` as part of the same wave
   (`IPesoRepository.cs:9`, `DapperPesoRepository.cs:14`, `PesoControl.cs:220` — N37-era carry-over,
   controlo audit §15).

### Required migration/guard (N40)
- **Migration file `N40_peso_leituras_approved_guard.sql`** (additive):
  - `CREATE OR REPLACE FUNCTION ba_dmo_guard_peso_leituras_approved()` — `BEFORE INSERT OR UPDATE
    OR DELETE ON peso_leituras FOR EACH ROW`; resolve the parent
    (`SELECT status FROM peso_controlos WHERE peso_controlo_id = COALESCE(NEW.peso_controlo_id,
    OLD.peso_controlo_id)`); `RAISE EXCEPTION` when `status = 'aprovado'` (message mirrors N25
    style). A **new sibling function** keeps the proven N25 function byte-identical live.
  - `DROP TRIGGER IF EXISTS trg_peso_leituras_approved_guard ON peso_leituras; CREATE TRIGGER …`
  - Trigger count goes 19 → 20 (plan §1; §13.7).
  - **Optional additive (owner-gated, audit P-3):** `CHECK (record_type <> 'comparacao' OR
    previous_control IS NOT NULL)` on `peso_controlos` — DB-enforces the comparison snapshot;
    rides the same change set; does not change the migration number (controlo audit §15/§16).
- **Rollback:** drop trigger (+function). **Clean-install:** refreshed baseline includes the trigger
  block. **No data backfill.**

### Transaction ordering (per-flow, all inside the existing single UoW)
1. **Create** (`CreateControlAsync`): INSERT control (`rascunho`) → INSERT readings. Guard never sees
   an approved parent.
2. **Draft edit** (`SaveControlAsync`): header UPDATE → DELETE readings → INSERT readings. Parent
   `rascunho/nao_aprovado` at every statement → guard silent.
3. **Submit** (`rascunho→pendente`): header-only. No readings DML.
4. **Approve** (`pendente→aprovado`): header-only UPDATE flips `status='aprovado'` — **no readings
   DML in the same transaction**, so the readings guard can never fire during approval (this is the
   design refinement that fixes the naive-trigger failure identified in controlo audit §15).
5. **Reject** (`pendente→nao_aprovado`): header-only.
6. **Reopen** (`aprovado/nao_aprovado→rascunho`): header-only UPDATE (parent returns to `rascunho`)
   → any later draft save runs in a **new** transaction against `rascunho` → guard silent. Reopen
   itself never touches readings and never trips the guard (header flip happens before any possible
   readings statement in that transaction).
7. **Decide comparison**: header-only (`comparison_decisions`).
8. **Delete** (`rascunho/nao_aprovado` only — `IsDeletable`; `PesoService.cs:515–518`): the
   `peso_controlos` BEFORE-DELETE guard (N25) already blocks approved parents; the CASCADE
   readings trigger would also raise on an approved parent — either order is fail-closed.

### Reopen behavior
- Unchanged semantics: `Reopen(aprovado/nao_aprovado → rascunho, revision+1, mandatory reason)`
  (`PesoControl.cs:192–204`); approval log preserved; no reading rewrite on reopen itself.
- After reopen, draft editing (including readings) works exactly as today — the guard no longer
  applies because the parent is `rascunho`.
- **Comparação `previous_control` snapshot remains valid:** the snapshot is written at comparison
  creation (`CreateControlAsync` `previous_control` column / `PesoService.CreateComparisonAsync`)
  and never rewritten by approve/decide; N37 already removed the mirror table
  (`N37_peso_previous_comparison_removal.sql`); the N40 guard does not touch `peso_controlos`
  header columns beyond the existing N25 rule. Optional P-3 CHECK further guarantees a comparison
  row always carries its snapshot.

### Tests required
- **Unit (`PesoServiceTests` / fakes):** save on `aprovado`/`pendente` rejected by the new
  assertion; save on `rascunho`/`nao_aprovado` accepted; reopen→save + resubmit passes; submit/
  approve/reject/reopen/decide call the **header-only** repository method (fake-call assertion,
  no readings SQL).
- **Unit (`PesoControlWorkflowTests`):** tighten `ValidateEditable` — edit of `pendente`/`aprovado`
  requires explicit reopen even with a reason (update the existing
  `ValidateEditable_ApprovedWithoutReason_IsBlocked`-family tests).
- **PG-gated integration (extend `RemediationGuardTests`-style or `PesoControlWorkflowTests` PG
  suite):** INSERT/UPDATE/DELETE on `peso_leituras` under an approved parent → denied with the
  guard message; approve flow completes with zero readings statements (same row ids/count, no
  `created_at_utc` churn); reopen flow completes; draft edit completes; comparison creation +
  decide complete; approved control delete still blocked; N37 interaction (delete path) unchanged.
- **Optional (if P-3 approved):** comparison row without `previous_control` rejected at DB level.

### Live prechecks (read-only, before migration)
- Enumerate triggers/functions on `peso_*` (expect current N25 set, **no** readings guard) — plan
  §14 pattern; confirm "N40 untouched" (already CONFIRMED in `N34_N36_PROD_live…report.md:235`).
- `SELECT c.peso_controlo_id, c.status, COUNT(l.peso_leitura_id) FROM peso_controlos c LEFT JOIN
  peso_leituras l ON l.peso_controlo_id = c.peso_controlo_id GROUP BY 1,2;` — baseline inventory of
  approved controls and their readings (diff target for post-deploy verification).
- Re-grep `src/` for `peso_leituras` writers (expect only `DapperPesoRepository` create/update).
- Post-deploy: attempt a readings mutation on an approved control as `ba_dmo_app` → denied; run
  approve and reopen flows → both succeed.

### Verdict
**N40 READY FOR IMPLEMENTATION: YES** — with the mandatory code pairing (header-only transitions +
draft-only rewrite + service assertion) shipped in the **same change set** as the trigger. Concept
and numbering are unchanged from the plan (controlo audit §15, status MODIFY_DESIGN; Gate F9).

**Concise implementation checklist for Codex (single release owning N40):**
1. `database/migrations/N40_peso_leituras_approved_guard.sql` — new sibling guard function + trigger
   on `peso_leituras` (INSERT/UPDATE/DELETE,before,row-wise, parent-checked); optional P-3 CHECK if approved.
2. `DapperPesoRepository` — add `UpdateControlHeaderAsync`; route submit/approve/reject/reopen/decide to it;
   keep draft rewrite in `UpdateControlAsync`.
3. `PesoService` — add save assertion (editable states only); tighten `PesoValidator.ValidateControlEditable`.
4. Same-wave doc-comment refresh (Peso `peso_comparacao_anterior` references).
5. Tests: unit (service assertion + header-only routing) + PG-gated guard/reopen/approve probes.
6. Live prechecks → apply N40 via `migrate` → post-deploy probes (deny approved-readings mutation;
   approve/reopen flows OK) → catalog parity vs refreshed baseline.
7. Regenerate consolidated baseline to include the trigger (+P-3 CHECK if adopted).

---

## N42A — `tool_check_occurrences`

**Current purpose (as created):** rules materialized for Job On display
(`N04_ferramentas.sql:103–132`) — an anticipated materialization surface from the N04 family
("rules materialized in the Job On (created later in this family)"). It was never wired with a
producer.

**Current readers/writers:**
- **Writers: zero in `src/`** (re-grepped this session — no Dapper/service/Program reference).
- **Readers: zero after Queue A** removed the only orphan reader
  `DapperFerramentasRepository.GetOccurrencesByRuleAsync` (Gate F16/F17; plan §5.1/T4).
- The only source artifact is the domain record `ToolCheckOccurrence.cs` (type unused by any
  repository/service).
- Live sibling **`job_on_verification_occurrence`** (N05:170–187) is the actual surface:
  writers `DapperJobOnRepository.InsertVerificationsAsync` (:420–440, :861–872) +
  `JobOnVerificationGenerator`; reader at :1133; N25 added its completed-state CHECK
  (mirroring the N04 CHECK).

**Authoritative / historical / duplicate / dormant:** **DUPLICATE (dormant twin)**. The Manual names
a single functional authority for materialized occurrences — the Job-On production context
(30:312, 10:275–280, 30:660; "exact persistence model is not business truth", 30:551 — Gate F16).
The duplicate adds no read or write value; its 3 CHECKs (`status`, `source`, `completed` —
N04:124–129) and 2 indexes are dead. *(Note for Codex: the plan §13.9 counts "2 CHECKs"; N04
defines 3 — the third (`completed`) has a live mirror on the N05 sibling via N25, so no guard is
lost when the N04 table is dropped.)*

**Baseline requirement (keep/remove/change):** keeping it preserves dead schema and a duplicated
authority surface; removing it changes **no behavior** (no live path reads or writes it) and shrinks
the baseline (plan: −1 table, −2 indexes, −3 CHECKs). Removal is owner-gated under GLM-DATA-12 and
must be guarded by a row-count probe (§14.8 `occurrence_rows` — expect 0; fail-closed otherwise).
The consolidated baseline lists the table in RLS/revoke/grant blocks — a drop requires refreshing
those lists in `consolidated_clean_install.sql` (Path-B equivalence, plan §15).

**Owner decision still needed: YES** (OD-6; Gate F16 "owner decision required: YES (table disposal)").

**Options:**
- **KEEP** — formally close it as a reserved dormant surface. Effect: same as today; no behavior.
  No migration. Lowest churn; leaves the duplicate authority documented only.
- **REMOVE** — drop table (+ its PK/FKs are in-table; the 2 indexes, 3 CHECKs, RLS policy and grants
  vanish with it) in the N42a migration with a row-count guard; refresh the consolidated baseline.
  Matches the recorded default (OD-6 = RETIRE; Gate F16 consolidation recommendation).
- **DEFER** — park the disposal decision until after the live baseline (Phase G refresh happens with
  the table present; a later removal implies a second consolidated refresh — plan §12 ordering dep
  #7's single-pass fallback only holds if N42 is decided before Phase G).

---

## N42B — `physical_pieces.status` CHECK

**Current allowed values:** none — the column is `text NOT NULL DEFAULT 'operational'` with **no
CHECK** (`N04_ferramentas.sql:72`; consolidated :365).

**Current code assumptions:**
- The column stores a **condition codec**, not an operational status: writers
  `RegisterPieceAsync` (INSERT, status = `ToolConditionCodec.ToStorage(New)` = `'new'`) and
  `UpdatePieceAsync` (UPDATE, status = codec of the requested condition) (`DapperFerramentasRepository.cs:245–301`);
  `SetConditionAsync` (`FerramentasService.cs:188–205`).
- Codec vocabulary **stored lowercase English**: `'new' | 'repaired' | 'not_repaired' | 'sucatado'`
  (`PhysicalPiece.cs:18–40`). The readers map `'operational'` → `New` and **unknown → New**
  (silent fallback, :35–39), and `MapPiece` hard-codes the domain `Status="operational"` while
  `Condition = FromStorage(row.status)` (`DapperFerramentasRepository.cs:638–646`) — the column
  carries a **double meaning** (condition codec in a column named/defaulted as operational status).
- Readers: `GetPiecesByLoteAsync`, `DapperFerramentasPieceLookup` (Reparação Externa tool-piece
  resolver), repair-exit item linkage (`physical_piece_id` FK from `repair_exit_items`).

**Manual rule:** "Known technical states: **Novo, Reparado, Por reparar, Sucatado**" (30:240, 30:246,
30:659); "Keep technical state and operational/physical state separate — do NOT collapse technical
condition and physical whereabouts into one enum/model" (30:244); operational states are
movement-owned in Armazém (30:248). Gate F15: **the Manual does not define a piece-level record or
its state column at all**, and Ferramentas §16 forbids inventing one (open Q5/Q6 listed at
30:650–651).

**Is the CHECK required for correctness now?** **NO.** Every live writer already emits one of the
four codec values (`'new'` at register, codec values on condition change); no invalid domain state
is currently produced. The permissiveness is a design smell (unknown text silently reads as `New`),
not a live corruption.

**Is it merely hardening?** **YES** — and even as hardening it is premature:
- The planned CHECK set in the plan is the **Portuguese labels** `('Novo','Reparado','Por reparar','Sucatado')`
  (§14.8 probe × ADD-set), which does **not** match the stored codec vocabulary — the probe itself
  would flag every current codec row as a fork. Adding that CHECK today would fail on live data or
  require a value-normalization/codec-alignment decision first.
- A CHECK on condition values in a `status` column (default `'operational'`) **encodes the double
  meaning into DDL**, which the Manual's no-collapse rule (30:244) warns against — that semantic
  (CHECK vs split column vs free-text; F15/Gate C) is an open owner decision (OD-5), not a
  mechanical hardening step.
- User-visible behavior change from a CHECK is "**might be YES**" (F15) — it would reject
  previously accepted writes.

**Options:**
- **ADD_NOW** — implementable only after (a) the §14.8 distinct-value probe, and (b) a decision on
  the CHECK vocabulary (codec values `'new','repaired','not_repaired','sucatado'` vs Portuguese
  labels + codec change, or a `'operational'`-inclusive set) and the no-collapse semantic. Adds a
  constraint that rejects previously accepted values; implied schema behavior is owner-invented
  until OD-5 is taken.
- **DEFER** — record FA-05 as an open item; add the CHECK (or split) in a later migration once the
  piece-level state model is decided. Zero risk now; nothing depends on it for baseline correctness.
- **DO_NOT_ADD** — keep the unconstrained column and rely on the codec + documented convention;
  accepts silent unknown→`New` reads as a known limitation.

---

## OWNER DECISIONS

**N39:**
- **Decision needed:** YES — Manual two-axis model (20:301–316) does not declare a one-sided
  Pegamentos measurement valid or invalid; the domain/calculator support it, the DB forbids it.
- **Recommended option:** **B — ALLOW_ONE_SIDED_MEASUREMENT** (`contra_costura` → nullable N39 +
  same-release domain completeness rule). Matches the recorded D-12 default and all audit
  recommendations; makes the existing one-sided capability reachable; the only structural change
  required is a widening `DROP NOT NULL`.
- **Why:** zero data impact (no null rows exist), rollback-safe, repository already binds nullable,
  and the calculator/domain semantics (Ovalização null, Média = single value) are already defined.
  If the owner instead reads the Manual as two-axes-always-mandatory, choose A (code-only
  validation, no migration) — the Manual does not decide this for you.

**N40:**
- **Owner decision needed:** YES (execution Go on the D-10/OD-3 guard with the mandated code
  pairing; concept is not an open functional question).
- **Ready for implementation: YES** — with the same-change-set pairing: header-only
  submit/approve/reject/reopen/decide (new `UpdateControlHeaderAsync`), draft-only readings rewrite,
  tightened save assertion (`SaveControlAsync` editable-states only), and the new
  `trg_peso_leituras_approved_guard` backstop; optional P-3 `comparacao ⇒ previous_control` CHECK if approved.

**N42A tool_check_occurrences:**
- **Decision needed:** YES (GLM-DATA-12 disposal gate; OD-6; Gate F16).
- **Recommended option:** **REMOVE** (with row-count-zero guard + consolidated-baseline refresh),
  executed as N42a **before** the Phase G single refresh so the end-state baseline is the copy that
  ships. DEFER is the acceptable fallback if the owner prefers no destructive ops at baseline; KEEP
  has no functional merit (duplicate dormant authority).

**N42B physical_pieces.status:**
- **Decision needed:** YES (FA-05/OD-5: piece-level state model — CHECK vs split vs free-text —
  including the codec vocabulary and the 30:244 no-collapse question).
- **Recommended option:** **DEFER** — the CHECK is hardening, not correctness; the planned CHECK
  set contradicts the stored codec vocabulary (would fail live), and encoding the current
  double-meaning column into DDL contradicts Manual 30:244 without an owner decision. Decide the
  state model first; add the CHECK/split in a later migration.

---

## CODEX NEXT IMPLEMENTATION PACKAGE

Items implementable **once the owner decisions above are supplied** (single release cadence per
plan §12; each item carries its own tests + live probes):

1. **N39** (if **B**): migration `N39_pegamentos_contra_costura_nullable.sql` (DROP NOT NULL) +
   same-release domain completeness rule (costura required; contra_costura optional) +
   API/service validation + unit + PG-gated one-sided-measurement test + post-deploy one-sided
   API probe. (If **A**: code-only validation change, no migration.)
2. **N40** (owner Go): migration `N40_peso_leituras_approved_guard.sql` + Dapper service split
   (`UpdateControlHeaderAsync`, draft-only rewrite) + tightened `SaveControlAsync`/validator
   assertion + optional P-3 CHECK + doc-comment refresh + unit/PG-gated tests + live pre/post probes.
3. **N42a** (if REMOVE): migration `N42_tool_check_occurrences_removal.sql` with row-count guard +
   post-deploy catalog-absence probe + consolidated-baseline refresh removing the N04 table block
   and its RLS/revoke/grant entries.
4. **N42b** (only if ADD_NOW is decided with a vocabulary/semantic resolution): codec-aligned
   CHECK migration (or split-column per OD-5) + distinct-value probe + codec-guard tests.
5. **Consolidated baseline refresh (D-16 Phase G)** capturing the final N39/N40/N42 state **once**
   (single refresh — depends on N42 being decided before Phase G); chain replay N01…Nxx from empty
   + Path-A/Path-B catalog parity.
6. **N42-related cleanup** (rides N42a): remove the unused `ToolCheckOccurrence` domain record if
   the owner approves (code-only).

**Explicitly NOT in this package (no decision taken, no change authorized):** any Ferramentas
master/state-model change beyond N42B disposition; `tampao_planos`/`job_on_field_option` (OD-13
KEEP dormant); Queue B code wave items (PC-03/04/05/06/07/08/13/14) — separate owner sign-off per
plan §11/Df-1.