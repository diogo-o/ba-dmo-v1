# POST-CODEX REMEDIATION — FUNCTIONAL SAFETY GATE

> **Type:** READ-ONLY CROSS-REFERENCE — no source, migration, test, schema object,
> or database was modified. This is the **functional safety gate** that must pass
> before any remediation of the findings in
> `reports/post_codex_database_contract_audit.md` is implemented.
>
> **Inputs:**
> - Authoritative audit: `reports/post_codex_database_contract_audit.md` (HEAD `8d916cb`).
> - N34 audit (separate design input, **NOT gated here and NOT to be implemented**):
>   `reports/schema_rationalization_N34_legacy_mirror_removal_audit.md`.
> - Canonical functional authority (the Manual): `AI-CONTEXT/docs/Manual/*` (14 files).
>
> **Scope:** every **P0** and **P1** finding plus every **behavior-changing P2**
> finding of the post-Codex audit is cross-referenced against the canonical
> functional Manual/SOT. Each finding is classified as exactly one of:
> `SAFE_TECHNICAL_FIX`, `FUNCTIONAL_ALIGNMENT`, `OWNER_DECISION_REQUIRED`,
> `NOT_A_VALID_FINDING`. Two supplementary entries (PC-11 MED, PC-10 P3) are
> included because their remediation changes user-visible behavior.
>
> **Imperatives honored:** nothing was modified; no code/migration/DB/test changes;
> no remediation migrations were designed for implementation; the Manual was not
> fitted to the current code; no missing business rule was invented. Where the
> Manual/SOT does not define the required rule clearly, the finding is marked
> `OWNER_DECISION_REQUIRED` and no inference is substituted.

---

## 1. Functional Authority Map (as used by this gate)

1. **The canonical manual/SOT is `AI-CONTEXT/docs/Manual/*`** — the
   "normalized functional model set … owner-reviewed; the pre-manual functional
   reference" (`Manual/00_INDEX.md:3–14`). `MANUAL_VS_OLD_DESIGN_PASS1.md:12`
   states it explicitly: *"The Manual is the functional authority."* Old-design
   files are **not** business-rule authorities. This gate may cite them only as
   corroboration, never as authority.
2. **The Manual is not decision-complete.** It records genuine open owner
   questions — Job On §17 (7 business + 3 design), Ferramentas §16 (6) — and a
   quarantined cross-module conflict (Job On §6.1 TP/Tampão vs
   Tampões §16) that may **not** be silently resolved
   (`10_JOB_ON_FUNCTIONAL.md:266`; `80_TAMPOES_FUNCTIONAL.md:226`).
3. **Module/area hierarchy (owner-confirmed):** exactly nine assignable
   top-level modules — Job On, Controlo, Ferramentas, Armazém, Boquilhas,
   Reparação Interna, Reparação Externa, Tampões, Admin
   (`01_GLOBAL_MODULE_USER_ROLE.md:226–236`; `02_MODULES_OPERATIONAL.md:364–368`).
   **Peso and Pegamentos are internal areas of the single CONTROLO module**
   (`01_GLOBAL:155–158,261–289`; `02_MODULES:137–151`); Comparação is a
   workflow inside Peso. História is **not** a module — it is a transversal
   read surface of audit events
   (`01_GLOBAL:251,380,820,1010`; `02_MODULES:366–368`).
   A technical representation of Peso/Pegamentos as separate catalog entries is
   expressly permissible and does not alter the functional classification
   (`01_GLOBAL:283`). **Consequence for this gate:** Pegamentos findings are
   interpreted under the Controlo functional model; the application's own
   `HistoriaModuleCatalog.OriginModuleIds` (`src/BA.Dmo.Application/Modules/Historia/HistoriaModuleCatalog.cs:24–35`)
   is technical evidence, consistent with `01_GLOBAL:283`.
4. **Audit/History model:** the global single append-only audit diary
   (`audit_events`) receives events from the modules
   (`90_ADMIN_FUNCTIONAL.md:326,414`; `99_DESIGN_LABORATORIO.md:54`);
   História shows events of the modules granted to the user, with admin events
   gated by audit capability (`01_GLOBAL:820–822`).
5. **Gate rule:** where the Manual does not define the required rule clearly,
   classification = `OWNER_DECISION_REQUIRED` — no inference.

---

## 2. Classification Summary

| # | Finding | Severity | Classification | User-visible change | Owner decision |
|---|---|---|---|---|---|
| F1 | PC-01 Pegamentos create 23502 | CRITICAL (P0) | SAFE_TECHNICAL_FIX | YES (bug-fix restore) | NO |
| F2 | PC-02 one-sided measurements (`contra_costura`) | CRITICAL (P0) | OWNER_DECISION_REQUIRED | YES (either branch) | YES |
| F3 | PC-03 audit jsonb convention (5 emitters) | HIGH (P0) | FUNCTIONAL_ALIGNMENT | YES | NO |
| F4 | PC-08 RE return status `concluido` | HIGH (P0) | FUNCTIONAL_ALIGNMENT | YES | NO |
| F5 | PC-04 Pegamentos no audit_events | HIGH (P1) | FUNCTIONAL_ALIGNMENT | YES | NO* |
| F6 | PC-05 Job On no audit_events (D-5) | HIGH (P1) | FUNCTIONAL_ALIGNMENT | YES | NO* |
| F7 | PC-07 app_settings zero writers | HIGH (P1) | OWNER_DECISION_REQUIRED | YES | YES |
| F8 | PG-04 Pegamentos workflows not transactional | HIGH (P1) | SAFE_TECHNICAL_FIX | NO (race closing) | NO |
| F9 | PC-09 Peso approved-reading mutability (D-10) | HIGH (P2) | FUNCTIONAL_ALIGNMENT | YES | NO |
| F10 | PC-06 `job_on.production_folder` | HIGH (P2) | FUNCTIONAL_ALIGNMENT | YES | NO |
| F11 | PC-13 Tampões `alterar_configuracao` facts | MED-HIGH (P2) | FUNCTIONAL_ALIGNMENT | YES | NO |
| F12 | PC-14 BQ discrepancy facts | MED-HIGH (P2) | FUNCTIONAL_ALIGNMENT | YES | NO |
| F13 | ON-02/JA-03/TP-06/BQ-15 unique violations | MEDIUM (P2) | SAFE_TECHNICAL_FIX | YES (error UX) | NO |
| F14 | FA-03 Ferramentas duplication atomicity | MEDIUM (P2) | SAFE_TECHNICAL_FIX | NO (success path) | NO |
| F15 | FA-05 `physical_pieces.status` | MEDIUM (P2) | OWNER_DECISION_REQUIRED | might be YES | YES |
| F16 | PA-01 occurrence tables | MEDIUM (P2) | OWNER_DECISION_REQUIRED | NO | YES (disposal) |
| F17 | Dormant surfaces D-7/D-8/D-9/D-11 + Substituir + BQ void | MEDIUM (P2) | OWNER_DECISION_REQUIRED | NO | YES (DDL only) |
| F18 | ADM-14 deploy-order hardening | MEDIUM (P2) | SAFE_TECHNICAL_FIX | NO | NO |
| F19 | PC-11 Admin audit NULL summaries | MEDIUM (suppl.) | NOT_A_VALID_FINDING | NO | NO |
| F20 | PC-10 consolidated baseline drift | MED-HIGH (P3, suppl.) | SAFE_TECHNICAL_FIX | YES (fresh installs) | NO (sequencing unless N34) |

\* Eligibility is settled by the Manual; the *exact event catalogue* (event codes) is
a technical/implementation matter, not a functional decision.
**Special-attention coverage:** Pegamentos creation/one-sided → F1/F2 ·
audit JSONB handling → F3 · audit coverage Pegamentos/Job On → F5/F6 ·
Job On production_folder → F10 · app_settings → F7 ·
Reparação Externa lifecycle/concluido → F4 · Peso approved-reading mutability → F9.

---

## 3. Findings

---

### F1 — PC-01: Pegamentos create path violates NOT NULL `updated_at_utc`

**Finding ID:** PC-01 (audit §6, P0 #1; DT-02).
**Technical severity:** CRITICAL.

**Technical evidence:**
- `DapperPegamentoRepository.cs:91` — `UpdatedAtUtc = (object?)control.UpdatedAtUtc ?? DBNull.Value`.
- Domain factory never sets it: `PegamentoControlo.Create` (`PegamentoControlo.cs:100–119`)
  initializes `CreatedAtUtc`/`CreatedBy` but not `UpdatedAtUtc` (:66, nullable).
- N07:44 — `updated_at_utc timestamptz NOT NULL DEFAULT now()`; an explicit NULL
  bypasses the DEFAULT → SQLSTATE 23502.
- Flow: `PegamentoService.CreateControlAsync` (`PegamentoService.cs:39–70`) → `Repository.CreateAsync`.
- Live behavior not executed here: `LIVE VERIFICATION REQUIRED` on the deployed DDL (audit §22.2).

**Functional source:** Manual `20_CONTROLO_FUNCTIONAL.md` §7 (Pegamentos, internal
area of Controlo), §12 (structural validation), §13 (Controlo owns Pegamentos records).
Manual `02_MODULES_OPERATIONAL.md:137–151` (Pegamentos = internal area).

**Manual/SOT rule:** Pegamentos is a working internal area where the operator
registers dimensional measurements and controls are created from the exact
Job On revision context (20_CONTROLO §3, §4, §7). No Manual rule forbids control
creation; the Manual's structural-validation principle allows rejecting only
objectively invalid operations (20_CONTROLO:538–549). The `updated_at_utc`
column is **technical audit metadata**, not a functional rule — the Manual is
silent on it.

**Current behavior:** every `POST /api/pegamentos` control creation binds an
explicit NULL into a NOT NULL column → 23502 → Pegamentos control creation fails
on a migration-compliant DB.

**Proposed remediation (audit §20 #1):** bind `UpdatedAtUtc` falling back to
`CreatedAtUtc` exactly as `UpdateAsync` already does
(`DapperPegamentoRepository.cs:266`), or set it in `PegamentoControlo.Create`.

**Classification:** **SAFE_TECHNICAL_FIX** — a mechanical defect in a technical
timestamp column; no functional rule is reinterpreted and nothing is invented;
the fix restores the Manual-supported creation flow.

**User-visible behavior change:** **YES** — control creation stops failing and the
Pegamentos (Controlo) area becomes usable; **no change to any intended functional
rule** (the intended behavior was always "creation works").

**Owner decision required:** **NO**.

**Notes:** verify against deployed DDL before rollout (audit §22.2 probe);
no test currently exercises this path against PG.

---

### F2 — PC-02: one-sided pegamento measurement (`contra_costura`) — D-12 not implemented

**Finding ID:** PC-02 (audit §6, P0 #2; DT-01).
**Technical severity:** CRITICAL.

**Technical evidence:**
- Domain: `PegamentoControlo.AddMeasurement(…, decimal? contraCostura …)`
  (`PegamentoControlo.cs:180–239`); `PegamentoMedicao.ContraCostura decimal?` (:300);
  calculator supports single-sided (nullable results) (`PegamentoMeasurementCalculator.cs:12–25`).
- Repository binds `ContraCostura ?? DBNull.Value` (`DapperPegamentoRepository.cs:295`).
- N07:63 — `contra_costura numeric(18,4) NOT NULL`.
- Any measurement without contra costura → 23502.

**Functional source:** Manual `20_CONTROLO_FUNCTIONAL.md` §7 (Pegamentos).
No Pegamentos/Peso-specific file exists in the Manual set (they are Controlo areas,
`02_MODULES:137–151`); the old-design Pegamentos data contract
(`old-design/24_PEGAMENTOS_03_DATA_CONTRACT_SNAPSHOT.json`) contains **no**
contra-costura nullable declaration and is not functional authority.

**Manual/SOT rule:** "Costura = 0° · Contra costura = 90°. Os dois eixos são
perpendiculares. As medições são registadas por linha/componente…"
(20_CONTROLO:301–304). Formulas: "Ovalização = Costura − Contra costura" (:308),
"Média = (Costura + Contra costura) / 2" (:314). Tolerance corridor ±0.20 with
boundary-as-alert (:332–344). **The Manual defines a two-axis dimensional model
and defines no one-sided-measurement case** — a measurement without contra
costura is neither declared valid nor declared invalid by the Manual.

**Current behavior:** a two-axis measurement persists correctly; an attempt to
record a one-sided measurement is accepted by the domain but rejected by the DB
with a raw 23502.

**Proposed remediation (audit §20 #2):** implement owner decision **D-12**
(relax the column to NULL + a domain rule) **or**, alternatively, align with the
Manual's two-axis model by requiring contra costura at the domain level (reject
the null with an actionable structural-validation error) and keeping the column
NOT NULL.

**Classification:** **OWNER_DECISION_REQUIRED** — the Manual implies a two-axis
model but does not clearly decide whether a one-sided measurement is a valid
business record; neither branch may be chosen by inference.

**User-visible behavior change:** **YES** under either branch (branch A: one-sided
measurements become persistable; branch B: users get a structured validation
error instead of a DB 500 when contra costura is omitted).

**Owner decision required:** **YES** — confirm the business rule (one-sided
measurements valid?) and the corresponding branch; D-12 is the recorded owner
decision vehicle.

**Notes:** do not silently pick a branch; the audit labels this
"D-12 NOT implemented"; a probe (audit §22.2) should confirm the deployed DDL.

---

### F3 — PC-03: cross-module audit JSONB binding convention break (5 of 9 global emitters)

**Finding ID:** PC-03 (audit §6, P0 #3).
**Technical severity:** HIGH.

**Technical evidence:**
- Uncast, no `AuditJson.Normalize` bind sites (before/after_summary → jsonb):
  `DapperBoquilhasRepository.cs:585`; `DapperTampaoRepository.cs:458`;
  `DapperPesoRepository.cs:521–523`; `DapperFerramentasRepository.cs:525`;
  `DapperReparacaoInternaRepository.cs:198`; `DapperControloSheetRepository.cs:199`
  (module-local events jsonb). ≥17 free-text payload call sites
  (Boquilhas/Tampões/Peso/Ferramentas/RI services).
- Contrast — cast + Normalize (the refactor convention, pinned by
  `AuditJsonBindingTests`): `DapperAdminRepository.cs:651`; `DapperArmazemRepository.cs:441,450–451`;
  `DapperRepairRepository.cs:426,432–433`; `DapperJobOnRepository.cs:529,536–537,612,619–620`.
- N01:114–115 — `before_summary/after_summary jsonb NULL`.
- `AuditJson.Normalize` exists (`Access/AuditJson.cs:7–23`) but is not applied at
  these sites. Runtime SQLSTATE behavior is `LIVE VERIFICATION REQUIRED`.

**Functional source:** Manual `50_BOQUILHAS_FUNCTIONAL.md:316`, 
`60_REPARACAO_INTERNA_FUNCTIONAL.md:428–438`, `90_ADMIN_FUNCTIONAL.md:326,330`; all
module files require operations to be recorded with attribution and to succeed.

**Manual/SOT rule:** "Auditoria: cada operação fica registada com quem/quando; a
escrita e o registo de auditoria ocorrem na **mesma operação atómica**"
(50:316); "Cada ação preserva: ator canónico; nome legível em snapshot; data/hora;
módulo; ação; entidade; resultado. As ações integram o diário global de auditoria,
append-only" (60:428–438). The Manual requires the operation to **succeed** while
being **audited in the same atomic operation** — it does **not** define a JSON
wire format (payload shape is technical; the Manual restricts only payload
*content*: no secrets/arbitrary blobs, 90:330).

**Current behavior:** co-transactional flows (Tampões machines/notes, BQ
close/reopen) bind raw text into jsonb → 22P02/42804 → the business write rolls
back as a generic `…_SAVE_FAILED`; post-commit flows (Peso nao_aprovar/reabrir/
documento.gerar, Ferramentas criar-lote/duplicar/regras) commit the business write
and then 500 on the audit insert.

**Proposed remediation (audit §20 #3):** apply `AuditJson.Normalize` + `::jsonb`
casts across the 5 uncast emitters; convert the ≥17 free-text payload sites to
serialized JSON; extend `AuditJsonBindingTests` to Boquilhas/Tampões/Peso/
Ferramentas/RI/Controlo.

**Classification:** **FUNCTIONAL_ALIGNMENT** — the fix restores the
Manual-defined behavior (operations succeed and are audited atomically, 50:316);
the payload-format aspect is the technical jsonb convention, not a business rule.

**User-visible behavior change:** **YES** — Tampões/BQ operations that currently
fail begin to succeed; Peso/Ferramentas stop returning a 500 after the business
write committed. No functional rule changes.

**Owner decision required:** **NO**.

**Notes:** `LIVE VERIFICATION REQUIRED` for the exact SQLSTATE on deployed
PostgreSQL (audit §22.3); deterministic for non-JSON text bound without a cast.

---

### F4 — PC-08: Reparação Externa return status machine — `concluido`/`retorno_parcial` unreachable on the finishing return

**Finding ID:** PC-08 (audit §6, P0 #4; RE-01).
**Technical severity:** HIGH.

**Technical evidence:**
- `ReparacaoExternaService.ConfirmReturnAsync:335–341` recomputes the exit status
  from `GetExitItemsAsync`, which opens a **fresh connection**
  (`DapperRepairRepository.cs:94–107`) *before* `uow.CommitAsync` — under READ
  COMMITTED the just-confirmed item's `in_at_utc` is invisible.
- `RepairExitStatusMachine.ConfirmReturn` (`RepairExitStatusMachine.cs:50–66`)
  can then never see `itemsAfter.All(i => i.InAtUtc.HasValue)` on the finishing
  return → `Concluido` unreachable; only a *subsequent* return of a different item
  can produce `RetornoParcial`.

**Functional source:** Manual `70_REPARACAO_EXTERNA_FUNCTIONAL.md` §5.6, §6.8–6.10, §7.

**Manual/SOT rule:** "retorno parcial → 'Retorno parcial'; **todos os itens de
volta → 'Concluído', ciclo fechado**" (70:458–459); "O ciclo só fecha quando todos
os itens regressarem" (70:540); "Quando todos os itens regressam, o ciclo fecha.
Estado: 'Concluído'. '**O retorno fecha o ciclo item a item**'. O batch concluído
passa para o Histórico. **Os factos históricos persistidos não são reescritos.**
" (70:552–556); transitions "são executadas pela máquina de estados" on persisted
confirmations (70:605–615); SOT C/D: "Qualquer confirmação que altere
simultaneamente o estado do ciclo de reparação e o estado físico do Armazém corre
num **único unit of work**" (70:558–565).
The Manual does not verbatim state "the last return confirmation flips the exit to
Concluído in that same DB operation", but "o retorno fecha o ciclo item a item"
(70:554) plus the state-machine mapping (70:608–614) makes that the documented
execution consequence — a supported inference, not an invented rule.

**Current behavior:** the exit list never reaches `concluido` through the normal
flow (the status write either stays `Enviado` or is skipped); business rows
(item return + warehouse movement + repair event) still commit — the status is
the loss.

**Proposed remediation (audit §20 #4):** recompute the status from the just-written
state inside the same UoW (in-transaction read of the items, or pass the confirmed
item into the status machine).

**Classification:** **FUNCTIONAL_ALIGNMENT** — current behavior contradicts the
Manual's explicit all-items-back → Concluído rule; the remediation restores it.

**User-visible behavior change:** **YES** — an exit whose final item returns shows
`Concluído` and moves to Histórico immediately, instead of remaining `Enviado`.

**Owner decision required:** **NO**.

**Notes:** probe against a real DB (audit §22.4); keep the one-UoW rule (SOT C/D)
when fixing — do not "fix" by moving reads to another connection.

---

### F5 — PC-04: Pegamentos (Controlo area) emits NO `audit_events` at all

**Finding ID:** PC-04 (audit §6, P1 #6; HS-02).
**Technical severity:** HIGH.

**Technical evidence:**
- Zero audit SQL in `src/BA.Dmo.Application/Modules/Pegamentos` and
  `DapperPegamentoRepository` (grep).
- `HistoriaModuleCatalog.cs:24–35` declares `pegamentos` (and `jobon`) among the
  **origin module ids** surfaced by História — the application's own declared
  design; per `01_GLOBAL:283` such a technical catalog entry is permissible and
  does not change the functional classification (Controlo area).

**Functional source:** Manual `90_ADMIN_FUNCTIONAL.md:326,414`;
`99_DESIGN_LABORATORIO.md:54`; `01_GLOBAL_MODULE_USER_ROLE.md:380,820,283`.

**Manual/SOT rule:** "the Audit area reads the **global, single, append-only**
action history (`audit_events`). **Every authenticated user's relevant business
actions become events** (user, module, action, entity, UTC timestamp, result)"
(90:326); "…all modules write to the **same single append-only table**…"
(90:414); "Toda a auditoria cross-module é registada de forma imutável
(append-only) na tabela global `audit_events`" (99:54); "História mostra apenas
eventos dos módulos concedidos ao utilizador" (01:820). Pegamentos (a Controlo
area) is classified among the modules whose events História surfaces; the Manual
does not enumerate the exact event codes — the catalogue is technical.

**Current behavior:** the História module filter for Pegamentos/Controlo area is
empty; pegamento create/measure/update/close/confirm-document are globally
unaudited; Admin Audit cannot query them.

**Proposed remediation (audit §20 #6):** add `audit_events` emission for the
Pegamentos operations in the same UoW as the business write (per the Manual's
atomic write+audit posture), with event codes consistent with sibling Controlo
area events (e.g., Peso's `peso.*`). The audit's alternative "explicitly document
the module as non-observable" contradicts 90:414 and is **not** Manual-aligned.

**Classification:** **FUNCTIONAL_ALIGNMENT** — the Manual's cross-module audit
rule requires module business actions to appear in the global diary; an empty
Pegamentos history contradicts it.

**User-visible behavior change:** **YES** — Pegamentos actions appear in
História/Admin Audit for users granted Controlo and holding `audit.view`
(01:820–822).

**Owner decision required:** **NO** for eligibility (Manual is categorical);
the exact event catalogue is a technical implementation decision.

**Notes:** this is the *global projection*; the Controlo internal Histórico
(append-only module history) is a distinct, complementary surface
(20_CONTROLO:469–493) and must not be conflated with the global audit.

---

### F6 — PC-05: Job On never emits `audit_events` (D-5 dual-emit not implemented)

**Finding ID:** PC-05 (audit §6, P1 #5; D-5/JA-06/HS-01).
**Technical severity:** HIGH.

**Technical evidence:**
- All Job On audit writes target `job_on_audit_event` only
  (`DapperJobOnRepository.cs:528,611`); zero `audit_events` references in JobOn
  files; `DapperHistoriaRepository` projects only `audit_events` (:30–47,88–111).
- N25:11 — "D2/INT-06 Option C — dual emit (code-side; no DDL here)"; HEAD does
  not implement it.
- `HistoriaModuleCatalog.cs:24–35` declares `jobon` an origin module.

**Functional source:** Manual `10_JOB_ON_FUNCTIONAL.md:168`;
`90_ADMIN_FUNCTIONAL.md:326,414`; `99_DESIGN_LABORATORIO.md:54`; `01_GLOBAL:380,820`.

**Manual/SOT rule:** Job On owns "**its own audit/history of the production
context**" (10:168) — which supports **keeping** the domain stream
`job_on_audit_event`. Independently, the transversal rules are categorical:
"Every authenticated user's relevant business actions become events" (90:326);
"…all modules write to the **same single append-only table**" (90:414);
"Toda a auditoria cross-module é registada de forma imutável (append-only) na
tabela global `audit_events`" (99:54). Job On is an assignable top-level module
(01:226–228) whose business actions (create, save revision, lifecycle transition,
image mutation) are relevant business actions.

**Current behavior:** the transversal História/Admin Audit hide all Job On
creation/transition/revision/image facts; only the domain stream has them.

**Proposed remediation (audit §20 #5):** implement **D-5 dual-emit** — write
`audit_events` projections for Job On **inside the same UoW** as the domain write
(parity guard test), while retaining `job_on_audit_event` as the domain stream.

**Classification:** **FUNCTIONAL_ALIGNMENT** — the Manual's global audit rules
require Job On actions in the global diary; 10:168 keeps the domain stream
intact, so dual-emit is the coherent reading. (The Manual does not enumerate the
Job On event codes — technical.)

**User-visible behavior change:** **YES** — Job On facts appear in História/Admin
Audit for users granted Job On and holding `audit.view`.

**Owner decision required:** **NO** for eligibility (preponderance of Manual text
is categorical). Documented caveat: a hypothetical owner decision to keep Job On
global-audit-exempt (internal-only) would contradict 90:414/99:54 and would need
to be an explicit owner decision — it is not the reading this gate adopts.

**Notes:** D-5 is the recorded owner decision vehicle (audit artifacts; N25:11);
do **not** remove `job_on_audit_event` when adding the projection.

---

### F7 — PC-07: `app_settings` has zero writers — `main_documents_output_root` unsettable

**Finding ID:** PC-07 (audit §6, P1 #7; HS-06).
**Technical severity:** HIGH.

**Technical evidence:**
- grep INSERT/UPDATE/DELETE `app_settings` over `src/` → 0; N11 seeds nothing.
- Reader only: `DapperAppSettingsReader.cs:18,28–72` (key `main_documents_output_root`,
  JSON string value); consumers `FileSystemJobOnImageProvider.cs` and
  `PegamentoService.cs:243–247`.

**Functional source:** Manual `20_CONTROLO_FUNCTIONAL.md` §11 (Diretórios);
Manual `90_ADMIN_FUNCTIONAL.md` is **silent** on application settings surfaces
(grep: no pasta/output/raiz/documentos/path/app_settings/definições).

**Manual/SOT rule:** "Root/└── Reference/└── Production/…" (20:513–520);
"**apenas a raiz é configurada manualmente pelo utilizador**; as pastas inferiores
são criadas ou reutilizadas automaticamente; a criação/reutilização deve ser
idempotente; o Job On acede à mesma relação exata de documento de
produção/revisão; **não existe uma árvore de documentos duplicada propriedade do
Job On**" (20:526–532). The Manual defines the functional requirement — the root
is **user-configured manually**, lower folders are automatic/idempotent — but
does **not** define the configuration surface (UI vs seed vs file).

**Current behavior:** the root can never be populated through code; Pegamento
document confirm hard-fails with `PEGAMENTO_OUTPUT_ROOT_MISSING`
(`PegamentoService.cs:244–247`); the Job On image provider silently yields no
images when the root is unset; only a manual DBA insert can fix it.

**Proposed remediation (audit §20 #7):** provide an owner/writer surface for
`app_settings.main_documents_output_root` **or** document the supported manual
seed (audit flags GLM-ARCH-05: "each setting written only by its owner").

**Classification:** **OWNER_DECISION_REQUIRED** — the Manual states the
functional requirement (root manually configured by the user) but not the
surface. Whether the root is configured in Admin UI, via a documented/manual
seed, or via provisioning is an owner decision; the audit already flags the
decision (GLM-ARCH-05).

**User-visible behavior change:** **YES** once the surface/seed exists and is
used — Pegamentos document confirm becomes usable and Job On images appear;
aligned with the Manual's directory rule.

**Owner decision required:** **YES** (surface decision).

**Notes:** couples with F10 (PC-06): with the Manual's deterministic
Root/Reference/Production structure, the root is the only variable requiring
manual configuration; lower folders must not require per-production manual setup.

---

### F8 — PG-04: Pegamentos workflows are not transactional

**Finding ID:** PG-04 (audit §12/§19.4, P1 #8).
**Technical severity:** HIGH.

**Technical evidence:**
- No UoW anywhere in `PegamentoService`: `CreateControlAsync`
  (context resolve + insert on independent connections), `AddMeasurementAsync`
  (read control on connection A, insert measurement on connection B), close/update
  (`GetByIdAsync` then `UpdateAsync`), `ConfirmDocumentSavedAsync` (4 independent
  reads + upsert).
- TOCTOU: measurement insert after a concurrent close; double document confirm.
- The closed-control rule is already enforced in the domain
  (`PegamentoControlo.cs:187–190` `PEGAMENTO_CONTROL_CLOSED`; `PegamentoService.cs:259–266`
  `PEGAMENTO_FINAL_DOCUMENT_FROZEN`).

**Functional source:** Manual `20_CONTROLO_FUNCTIONAL.md` §7, §10, §12;
50_BOQUILHAS:316 (established atomic write+audit posture). Transactionality per se
is a technical persistence contract (GLM-DATA-05; `maps/`), not a functional rule.

**Manual/SOT rule:** closed controls are frozen — the final document of a closed
control must not be silently replaced (20_CONTROLO §10/§11; corroborated by the
old-design SOT §12 Pegamentos: "final document persisted exactly once (ON
CONFLICT); closed control cannot silently replace its final document"); history is
append-only (20:485). The Manual assumes operations are atomic at the persistence
layer (the module posture expressed at 50:316).

**Current behavior:** fragmented, non-atomic multi-connection flows; race windows
(measurement on just-closed control, double document confirm, partial writes on
crash).

**Proposed remediation (audit §20 #8):** make controlo+medicoes and
confirm-document single UoWs; keep/back the closed-control block with
repository-level (in-tx) checks.

**Classification:** **SAFE_TECHNICAL_FIX** — no Manual rule change; the
functional constraints are already implemented; the remediation closes technical
races and crash windows.

**User-visible behavior change:** **NO** on normal paths (only race/failure paths
become clean).

**Owner decision required:** **NO**.

---

### F9 — PC-09: Peso approved-control readings still rewritable (D-10 not implemented)

**Finding ID:** PC-09 (audit §6, P2 #9; D-10).
**Technical severity:** HIGH.

**Technical evidence:**
- `DapperPesoRepository.UpdateControlAsync:383–399` — unconditional `DELETE FROM
  peso_leituras` + re-INSERT, inside a header UPDATE that also rewrites
  `status/approved_by/approved_at_utc/measurements_snapshot`.
- `peso_leituras` has no append-only trigger (N06:118–126 only UNIQUE); N25's
  `ba_dmo_guard_peso_approved` (N25:137–165) guards `peso_controlos` identity+
  delete only.
- `PesoService.SaveControlAsync` (`PesoService.cs:389–410`) calls
  `PesoValidator.ValidateControlEditable(status, request.ChangeReason)` — with a
  ChangeReason an `aprovado` control passes (only a null reason is blocked, per
  `PesoControlWorkflowTests.ValidateEditable_ApprovedWithoutReason_IsBlocked`).

**Functional source:** Manual `20_CONTROLO_FUNCTIONAL.md` §6 (Peso), §9
(decisões/reabertura), §10 (Histórico); corroborating GLM layer recorded in
`AI-CONTEXT/docs/Maps/07_CONTROLO.md:175–176` and `PesoControlWorkflowTests`
(GLM-PESO-06.6/06.7/06.8).

**Manual/SOT rule:** "A Comparação não altera o controlo base previamente
aprovado… não reinterpreta ou apaga o controlo anterior" (20:263); "Correções não
apagam o passado. Quando algo é corrigido, o histórico preserva a sequência
funcional. A correção cria continuidade histórica, **não substituição
silenciosa**" (20:481); "Onde estabelecido, eventos e histórico são append-only"
(20:485); an approved/historical record keeps the context it had (20:477); the
decided-sheet flow provides explicit reabertura that preserves events, never
silently deleting them (20:441–452). The GLM-encoded Peso contract corroborates
the mechanics: `Reopen(aprovado/nao_aprovado → rascunho, revision+1, mandatory
reason)` is the correction path for decided controls; `IsDeletable` only
rascunho/nao_aprovado; comparison never mutates the approved base.

**Current behavior:** approved control readings (and partial header) can be
rewritten in place via `SaveControlAsync` with a ChangeReason; the DELETE+re-INSERT
destroys the original fact chain at the DB layer, breaching the immutability
contract.

**Proposed remediation (audit §20 #9):** implement D-10 — protect `peso_leituras`
(append-only trigger + service assertion requiring the audited reopen path) while
keeping the existing `peso_controlos` guard.

**Classification:** **FUNCTIONAL_ALIGNMENT** — remediation enforces the Manual's
no-silent-rewrite/history principles; the approved base is only changeable via the
audited reopen path.

**User-visible behavior change:** **YES** — direct in-place edits of approved
readings are rejected; users must reopen with a reason (visible in errors and in a
new revision), matching the Manual's history rules.

**Owner decision required:** **NO** for the principle (Manual's append-only /
no-silent-substitution rules define it). Caveat for awareness: the sentence "essa
aprovação não transforma automaticamente qualquer medição em decisão final
permanente" (20:152) concerns technical-result-vs-human-decision, **not**
mutability of approved facts; it is not a license to edit approved readings in
place. If the owner nevertheless wants in-place edits, that is a deliberate
departure requiring explicit confirmation.

**Notes:** D-10 is the recorded owner decision (prior artifacts); the trigger is
additive DDL consistent with GLM-DATA-12.

---

### F10 — PC-06: `job_on.production_folder` has no application writer; calendar SELECTs omit the column

**Finding ID:** PC-06 (audit §6, P2 #10; JA-04/JA-05).
**Technical severity:** HIGH.

**Technical evidence:**
- `DapperJobOnRepository.GetActiveAsync` (:111–127) and
  `GetByProductionCodeAsync` (:148–164) **omit** `production_folder` from their
  SELECTs, while `JobOn.FromRow` reads `row.production_folder`
  (`JobOn.cs:51`); `GetByIdAsync` includes it (:80).
- `DapperJobOnProductionFolderResolver.cs:25–43` reads only `job_on.production_folder`.
- grep: zero INSERT/UPDATE of `production_folder` in `src/`.
- `PegamentoService.ConfirmDocumentSavedAsync:250–254` hard-fails
  `PEGAMENTO_PRODUCTION_FOLDER_MISSING` when the folder is unset.

**Functional source:** Manual `20_CONTROLO_FUNCTIONAL.md` §11 (Diretórios);
`10_JOB_ON_FUNCTIONAL.md` §14 (documentos/impressão).

**Manual/SOT rule:** lower folders are "criadas ou reutilizadas **automaticamente**"
and creation/reuse "deve ser **idempotente**"; the structure is the deterministic
`Root/Reference/Production`; the Job On accesses "a mesma relação exata de
documento de produção/revisão" and there is "**não existe uma árvore de documentos
duplicada propriedade do Job On**" (20:513–532). **The Manual defines no
per-Job-On `production_folder` business field** — the folder follows from the
production context (Reference/Production) under the user-configured root
(10_JOB_ON §2, §14).

**Current behavior:** the folder must be set out-of-band (manual SQL/admin);
otherwise Pegamento document confirm hard-fails and the calendar / RI / Controlo
by-production hydration paths silently lack the value (DapperRow dynamic
missing-column semantics `NEEDS VERIFICATION` — audit §22.5).

**Proposed remediation (audit §20 #10):** add a writer for `production_folder` in
the Job On save flow and include the column in `GetActiveAsync` /
`GetByProductionCodeAsync`. The SOT-aligned end state additionally requires the
lower folders to be auto-resolved/auto-created (idempotent) so that no out-of-band
seeding is needed and the hard-fail disappears.

**Classification:** **FUNCTIONAL_ALIGNMENT** — remediation moves folder handling
toward the Manual's automatic, idempotent, deterministic directory rule. Caveat:
merely persisting the field without auto-resolution leaves existing/en-route
productions partially aligned (they would still hard-fail until a folder exists).

**User-visible behavior change:** **YES** — document-confirm and image paths stop
depending on out-of-band seeding; calendar/RI/Controlo projections stop
silently-nulling the folder.

**Owner decision required:** **NO** for the behavior; whether `production_folder`
remains a persisted convenience column or is fully derived is a technical choice
(derived is the Manual-least-surprise option).

**Notes:** run the audit's LIVE-5 probe for Dapper `dynamic` missing-column
semantics before finalizing the SELECT fix.

---

### F11 — PC-13: Tampões `alterar_configuracao` balance/audit facts are truncated/false

**Finding ID:** PC-13 (audit §6, P2 #11; TP-01/TP-02).
**Technical severity:** MEDIUM–HIGH.

**Technical evidence:**
- `TampaoService.cs:445` — `BalancesAfter = SerializeBalances(new TampaoSaldo {
  Enchidos = newOriginEnchidos })` (por_encher forced to 0; destination absent)
  while the DB saldo write preserves por_encher on both configurations
  (:435–436).
- Audit `after_summary` receives `destBefore` as the after-state
  (`InsertMovementAndAuditAsync(… originBefore, destBefore …)`, :449–451).

**Functional source:** Manual `80_TAMPOES_FUNCTIONAL.md` §8–§10.

**Manual/SOT rule:** "Não confundir edição de configuração com transformação de
quantidade" (80:112); when quantity is intentionally moved between configurations
it is a quantity movement with "origem e destino preservados; histórico
append-only" (80:116–119); "Preservar histórico auditável" (80:123); movement
history preserves data/hora, configuração, movimento/ação, categoria/saldo,
quantidade, "Antes / Depois", operador (80:125–132); configuration-edit history
preserves "o que mudou; valor anterior / novo valor; quem mudou; quando"
(80:134–138); "Sem sobrescrita silenciosa de factos históricos" (80:140). The
Manual does **not** prescribe the JSON schema of before/after payloads — its
requirement is that recorded facts be **truthful** (Antes/Depois, origem/destino,
no silent overwrite).

**Current behavior:** the movement fact's "after" record is a truncated/false
state (por_encher=0, destination absent) even though the persisted balances
preserve por_encher; the audit `after_summary` records the destination's BEFORE
state as if it were the AFTER state.

**Proposed remediation (audit §20 #11):** record truthful `balances_before`/
`balances_after` (true destination-after state, both origins/destinations as
applicable, including por_encher) and correct the audit after_summary to the true
post-change state.

**Classification:** **FUNCTIONAL_ALIGNMENT** — the Manual requires truthful,
non-overwritten history facts with origin/destination preservation; a false
after-summary contradicts it. Exact payload schema remains technical.

**User-visible behavior change:** **YES** — História and the Tampões Histórico
area show correct after-states and attribution.

**Owner decision required:** **NO** for truthfulness. Note: whether balance
snapshots belong in the **config-edit** audit payload at all is an implementation
choice (the Manual audits config edits as what-changed/prev/new/who/when, 80:134–138);
where quantities are actually moved, the Manual requires origin+destination
preservation (80:116–119).

**Notes:** keep movements append-only; do not force a rigid balance-class lifecycle
(80:95–102).

---

### F12 — PC-14: BQ discrepancy `expected_qty` wrong; resolution attribution never written; `under_review` unproducible

**Finding ID:** PC-14 (audit §6, P2 #12; BQ-03/BQ-04/BQ-05).
**Technical severity:** MEDIUM–HIGH.

**Technical evidence:**
- `BoquilhasService.cs:233`; `BqRules.cs:125–132`; `DapperBoquilhasRepository.cs:400–409`
  binds `resolved_by`/`resolved_at_utc` as NULL; disjoint codec.

**Functional source:** Manual `50_BOQUILHAS_FUNCTIONAL.md` §9, §11, §19.

**Manual/SOT rule:**
- *Expected:* "Entrada/retorno de reparação: quantidade que voltou; **reconciliação
  com o que estava 'em reparação'**" (50:152); "devolve de 'em reparação' para
  'disponível' (**até ao esperado**); o excesso vai para 'entrada excecional'"
  (50:173); the excess is a "**registo separado**" never auto-summed (50:194, 155,
  364); canonical example "voltou 25, esperado 20" (50:284). The Manual's
  "esperado" is **the outstanding "em reparação" quantity the repairer should
  have returned** — never an accumulated exceptional/received amount.
- *Resolution attribution:* "Discrepâncias: entradas excecionais e respetivas
  resoluções ficam registadas (**quem/quando/nota**)" (50:306); resolution note
  only required at the moment of resolution (50:155).
- *Under-review:* the Manual defines **no** "em análise"/under-review state —
  only "discrepância aberta" and resolution (grep-verified absence).

**Current behavior:** `expected_qty` stores prior accumulated ExceptionalReceived
instead of the matched return; resolution leaves `resolved_by`/`resolved_at_utc`
NULL (attribution lost); an `under_review` state is unproducible.

**Proposed remediation (audit §20 #12):** store the correct expected/matched
return value; write `resolved_by`/`resolved_at_utc` on resolution; **do not**
implement an under-review state.

**Classification:** **FUNCTIONAL_ALIGNMENT** for the expected_qty and resolution-
attribution parts (Manual-defined). The `under_review` sub-aspect is
**NOT_A_VALID_FINDING** — no such state exists in the Manual; the observation is
consistent with the Manual. Aligning to the Manual means **not** inventing that
state.

**User-visible behavior change:** **YES** — discrepancy expected values become
correct; resolutions carry who/when/note into História/history.

**Owner decision required:** **NO** for the two aligned parts; an under-review
state would require an owner decision (Manual silent → must not be inferred).

**Notes:** keep the 20→25 non-blocking semantics (never block the return; never
auto-sum the excess); the note is mandatory only at resolution.

---

### F13 — ON-02/JA-03/TP-06/BQ-15 (ADM-06): duplicate/unique violations surface as raw 23505 or generic failures

**Finding ID:** ON-02/JA-03/TP-06/BQ-15 (+ PC-12 duplicate path); audit P2 #13.
**Technical severity:** MEDIUM.

**Technical evidence:**
- `job_on` create/duplicate inserts without ON CONFLICT against the partial
  `uq_job_on_identity` (N25:60–62) → raw 23505 (JA-03; PC-12).
- `tampao_configurations` pre-check + plain INSERT against
  `uq_tampao_configurations_values` → concurrent duplicate → generic
  `TAMPAO_SAVE_FAILED` (TP-06).
- `bq_lotes` pre-check + plain INSERT against `uq_bq_lotes_reference_batch` →
  generic `BQ_SAVE_FAILED` (BQ-15).
- internal_users create: ON CONFLICT (actor_id) absorbs some duplicates but a
  same-auth-user duplicate raises 23505 unhandled (ADM-06).

**Functional source:** Manual `20_CONTROLO_FUNCTIONAL.md` §12 (structural
validation); no Manual rule defines duplicate-identity error UX.

**Manual/SOT rule:** structural/data validation may reject an objectively invalid
operation with a clear requirement ("campo obrigatório estruturalmente em falta…
requisito explícito de fluxo não satisfeito", 20:540–549). The Manual does not
specify error codes — mapping raw constraint violations to actionable domain
errors is technical robustness.

**Current behavior:** 500/raw 23505, or generic `…_SAVE_FAILED`, on concurrent
duplicate submissions.

**Proposed remediation (audit §20 #13):** map unique/duplicate violations to
domain errors (as sibling flows already do), e.g. `JOB_ON_IDENTITY_DUPLICATE`,
`TAMPAO_CONFIGURATION_DUPLICATE`, `BQ_LOTE_DUPLICATE`.

**Classification:** **SAFE_TECHNICAL_FIX** — no functional rule change; only
error semantics improve.

**User-visible behavior change:** **YES** — actionable domain error instead of
500/generic failure on duplicate submissions.

**Owner decision required:** **NO**.

---

### F14 — FA-03: Ferramentas lot duplication is not atomic despite the doc claim

**Finding ID:** FA-03 (audit §6, P2 #14; FA-08).
**Technical severity:** MEDIUM.

**Technical evidence:**
- `FerramentasService.CreateLoteFromBaseAsync:111–150` — per-rule
  own-connection calls; audit write post-commit.
- Stale doc claim: `DapperFerramentasRepository.cs:10–15`.

**Functional source:** Manual `30_FERRAMENTAS_FUNCTIONAL.md` §4.4, §6.
The Manual is **silent** on failure atomicity of duplication.

**Manual/SOT rule:** "New lot via 'Novo lote a partir deste' **copies configuration
only (never occurrences/checks/history)** and keeps master identity read-only"
(30:218); duplication copies verification-rule configuration, never
occurrences/history (30:316; 10_JOB_ON §7). The Manual states *what* is copied,
not that copying is all-or-nothing; atomicity is a technical robustness contract.

**Current behavior:** partial rule copies on failure; stale documentation claim.

**Proposed remediation (audit §20 #14):** run lot + copied-rules + audit in one
UoW; remove the stale doc claim.

**Classification:** **SAFE_TECHNICAL_FIX** — no Manual rule change;
failure-path robustness.

**User-visible behavior change:** **NO** on the success path; **YES** (clean
rollback) on partial-failure paths.

**Owner decision required:** **NO**.

---

### F15 — FA-05: `physical_pieces.status` double-meaning (condition codec in an unconstrained column)

**Finding ID:** FA-05 (audit §6, P2 #15; DT-06).
**Technical severity:** MEDIUM.

**Technical evidence:**
- `DapperFerramentasRepository.cs:246,274` — piece INSERT/UPDATE writes a
  condition codec into `physical_pieces.status`; `:605–606` — `MapPiece` hard-codes
  `Status = "operational"` and reads `Condition = ToolConditionCodec.FromStorage(row.status)`.
- N04:72 — `physical_pieces.status` has no CHECK.

**Functional source:** Manual `30_FERRAMENTAS_FUNCTIONAL.md` §5 (Estado Técnico).
Ferramentas has 6 open owner questions (§16) that must not be implicitly resolved.

**Manual/SOT rule:** "The known technical states are: **Novo, Reparado, Por
reparar, Sucatado**" (30:240, 246, 659); "Keep **technical state** and
**operational / physical state** separate. **Do NOT collapse technical condition
and physical whereabouts into one enum/model**" (30:244); operational states
(Em armazém; Em produção; Em reparação / enviado para reparação; other derived
destinations) are open and movement-owned (30:248). The Manual defines the tool
record's **technical** state vocabulary but does **not** define a piece-level
record or its state column at all.

**Current behavior:** an un-constrained text column stores a condition codec with
a double meaning; arbitrary values are silently read as "New"; a hard-coded
"operational" is layered on top.

**Proposed remediation (audit §20 #15):** add a CHECK constraint or split the
column (condition vs physical/operational status), consistently with the Manual's
no-collapse rule.

**Classification:** **OWNER_DECISION_REQUIRED** — the Manual constrains the tool
record's technical states but does not define the piece-level state model; the
choice of CHECK-on-4-states, split-column, or free-text is a schema/domain
decision for the owner, and Ferramentas §16 forbids inventing one.

**User-visible behavior change:** **might be YES** — a CHECK would reject
previously accepted writes; a split changes read semantics.

**Owner decision required:** **YES**.

**Notes:** the row at 30:240 is "known technical states (labels confirmed by
Armazém and Job On current design)" — the piece column is not covered by it.

---

### F16 — PA-01: occurrence twins — `tool_check_occurrences` (N04) vs `job_on_verification_occurrence` (N05)

**Finding ID:** PA-01 (audit §6/§17.4, P2 #16; FA-01; DUP-03).
**Technical severity:** MEDIUM.

**Technical evidence:**
- N04 `tool_check_occurrences`: zero writers in `src/`; only orphan reader
  `DapperFerramentasRepository.GetOccurrencesByRuleAsync:427–441` (+ DTO/interface);
  N04 CHECKs dead.
- Job On writes the N05 sibling (`DapperJobOnRepository.cs:411–468`; N05:170–187).

**Functional source:** Manual `30_FERRAMENTAS_FUNCTIONAL.md` §6.2 +
`10_JOB_ON_FUNCTIONAL.md` §7.

**Manual/SOT rule:** "Rules are configured in the lot card **Verificações** tab
(owned by Ferramentas); **Job On only presents/confirms generated occurrences**"
(30:312); "Frequencies V1: `uma_vez_no_lote` and `por_fabrico`" (30:321, 10:302);
"Job On materializes/presents the occurrences relevant to production" (10:275–280);
"verification occurrences confirmed from Job On" (30:660). The Manual names a
**single functional authority for materialized occurrences: the Job On production
context** (the N05 sibling in the implementation). A Ferramentas-owned occurrence
table is not Manual-defined (30:551: "The exact persistence model is not business
truth here").

**Current behavior:** two tables exist; the N04 table is schema-only; live code
already writes the N05 surface.

**Proposed remediation (audit §20 #16 / DUP-03):** decide consolidation — keep the
Job-On-level occurrence surface as the single authority; retire/remove
`tool_check_occurrences`.

**Classification:** **OWNER_DECISION_REQUIRED** as posed (the *physical
consolidation/removal* is owner-gated under GLM-DATA-12 and the audit's own
"MAYBE (owner/product)" verdict). The functional *authority* outcome, however, is
unambiguous in the Manual (Job-On-level materialization) — so the code-side
decision (never write N04) is already aligned.

**User-visible behavior change:** **NO** from consolidation (no live path writes
the N04 table); schema-surface only.

**Owner decision required:** **YES** (table disposal).

---

### F17 — Dormant surfaces: D-7/D-8/D-9/D-11, Substituir/ReplaceOccupation, BQ void contracts, dead runtime methods

**Finding ID:** Audit §17/§18 surface (P2 #17).
**Technical severity:** MEDIUM.

**Technical evidence:** audit §17 (orphan tables/columns: `peso_comparacao_anterior`,
`job_on_field_option`, `tampao_planos`, `tool_check_occurrences`, `bq_traces.sap_end`,
`bq_discrepancies.resolved_by/resolved_at_utc` pre-fix, `modules_override`,
`job_on_revision.image_asset_id`, `bq_movements.movement_type='fim'`,
`repair_events` write-only) and §18 (orphan methods incl. `SubstituirAsync`/
`ReplaceOccupationAsync`, BQ void family, `GetOccurrencesByRuleAsync`, etc.).
Existing owner decisions govern disposition: D-7 keep-dormant (`job_on_field_option`),
D-8 keep-dormant (`tampao_planos`), D-9 REMOVE_LATER (`peso_comparacao_anterior`),
D-11 REMOVE_LATER (`modules_override`, `image_asset_id`).

**Functional source:** Manual — silent on these surfaces (they are not functional
features; no behavior is defined for them); existing owner decisions D-7/D-8/D-9/D-11
recorded in prior artifacts.

**Manual/SOT rule:** no Manual rule requires these tables/columns; several are
explicitly "keep-dormant" by owner decision (D-7/D-8), others "REMOVE_LATER"
(D-9/D-11). None change user-visible behavior.

**Current behavior:** dormant/present; zero runtime impact; N34 (physical removal of
the access mirrors) is a separate design that this gate **must not** pre-empt.

**Proposed remediation (audit §20 #17):** execute the existing D-* dispositions;
remove dead code methods; keep D-7/D-8 items dormant per decision.

**Classification:** **OWNER_DECISION_REQUIRED** for physical DDL removals
(GLM-DATA-12: no table/column drop without the owner decision and row-count/parity
guards; sequencing interacts with the N34 destructive phase). Dead-code removal
alone is **SAFE_TECHNICAL_FIX**.

**User-visible behavior change:** **NO** (dormant surfaces).

**Owner decision required:** **YES** for DDL disposition/sequencing; **NO** for
dead-code removal.

**Notes:** D-7/D-8 are **keep-dormant**, NOT removals — do not dispose them.
N34 (mirror removal) is explicitly out of scope: **DO NOT IMPLEMENT N34.**

---

### F18 — ADM-14: deploy-order hardening (N33 before first user write)

**Finding ID:** ADM-14 (audit §19.1, P2 #18).
**Technical severity:** MEDIUM.

**Technical evidence:** N33 revokes all `ba_dmo_app` privileges on the access
mirrors and applies column-level grants on `internal_users`; a consolidated-built
or partially-migrated DB diverges (CB-04).

**Functional source:** Manual `03_USERS_ACCESS_OPERATIONAL.md:107–110`;
`90_ADMIN_FUNCTIONAL.md` (templates as the functional vehicle; fail-closed
posture).

**Manual/SOT rule:** "Os templates de acesso geridos no Admin são a forma
funcional através da qual os módulos são associados ao utilizador; os mecanismos
técnicos por baixo dessa atribuição são matéria de mapeamento técnico"
(03:107–110). Deploy order is operational/technical — the Manual does not define it.

**Current behavior:** if user writes occur before N33 (or on a baseline without
N33), the mirrors remain writable by `ba_dmo_app`.

**Proposed remediation (audit §20 #18):** guarantee `migrate` (incl. N33) precedes
the first user write; document in the deploy runbook.

**Classification:** **SAFE_TECHNICAL_FIX** (operational hardening; no functional
rule change).

**User-visible behavior change:** **NO**.

**Owner decision required:** **NO**.

---

### F19 — PC-11 (supplementary): Admin audit rows always carry NULL before/after summaries

**Finding ID:** PC-11 / ADM-08 (audit §6; MED — not in the P0/P1/P2 backlog).
**Technical severity:** LOW–MEDIUM.

**Technical evidence:** `DapperAdminRepository.cs:666–667` binds NULLs;
`AdminModels.cs:123–124` exposes Before/AfterSummary; no Admin caller passes them;
no Normalize.

**Functional source:** Manual `90_ADMIN_FUNCTIONAL.md:326,330`.

**Manual/SOT rule:** audit events are fact columns "(user, module, action, entity,
UTC timestamp, result)" (90:326); payload content is restricted — "events never
include passwords, tokens, cookies, credentials, full emails, PDFs, images, or
arbitrary payloads" (90:330). **Before/after JSON summaries are NOT required for
admin audit events** by the Manual.

**Current behavior:** always NULL — compliant with the Manual; latent 22P02 risk
only if a future non-JSON string is bound without a cast.

**Proposed remediation (audit ADM-08):** optionally bind via `AuditJson.Normalize`
+ `::jsonb` for future-proofing.

**Classification:** **NOT_A_VALID_FINDING** as a functional defect (the Manual
does not require summaries; NULL is compliant). The defensive hardening is a
voluntary **SAFE_TECHNICAL_FIX**.

**User-visible behavior change:** **NO**.

**Owner decision required:** **NO**.

---

### F20 — PC-10 (supplementary): consolidated clean-install baseline drift (CB-01/CB-02/CB-03/CB-04/CB-05)

**Finding ID:** PC-10 / CB-01..CB-05 (audit §4, P3 #19 — behavior-relevant, hence gated).
**Technical severity:** MEDIUM–HIGH.

**Technical evidence:** `database/consolidated_clean_install.sql` — 60/61 app
tables (no `access_template_profiles` + no N31 trigger/UX); `article_reference_images`
created without RLS/policy/grants (vs N29:139–155); pre-N33 mirror posture
(`profile_title NOT NULL` + CHECK; junction CREATE + RLS + full DML grants to
`ba_dmo_app`); stale header; missing N27/N28/N29 reconciliation DML.

**Functional source:** Manual `01_GLOBAL_MODULE_USER_ROLE.md` (D-1/D-2 authority
chain; 03_USERS_ACCESS:107–110); RLS/grants posture is technical (GLM-DATA-12 and
the migration chain are the schema authority).

**Manual/SOT rule:** the access authority is template → profile (D-1/D-2; Manual
90/03); the Manual defines no baseline SQL content. The forward migration chain
N01–N33 is the schema authority; parity between chain-migrated and
baseline-installed databases is a technical requirement (GLM-DATA-12).

**Current behavior:** a consolidated-built DB: Admin template edit breaks (42P01 —
N31 objects missing); `article_reference_images` is RLS-less; the junction and
`profile_title` remain writable by `ba_dmo_app` (pre-N33 security posture) — i.e.,
a fresh install diverges materially from a chain-migrated DB, including access
posture.

**Proposed remediation (audit §20 #19):** refresh the baseline to reproduce the
N31 objects + N29 RLS stanza + N33 posture + corrected header (D-16), sequenced
after the post-N34 final state per the audit's ordering rule.

**Classification:** **SAFE_TECHNICAL_FIX** (parity restoration; no functional rule
change) with an owner **sequencing** consideration (baseline refresh should follow
the N34 destructive phase, which is blocked).

**User-visible behavior change:** **YES** on consolidated-built DBs (Admin template
edit works; RLS correct; mirror writes blocked); NO on chain-migrated DBs.

**Owner decision required:** **NO** for content parity (chain is the authority);
**YES** for sequencing relative to N34.

**Notes:** strictly outside the P0/P1/behavior-P2 scope; gated because the
remediation is behavior-relevant on fresh installs.

---

## 4. FINAL SECTION — Implementation Queues

### A. SAFE TO IMPLEMENT NOW
*(Technical fixes — no functional-rule change, no Manual reinterpretation.
Code-first, additive; verify against `BA_DMO_TEST_DATABASE` per audit §21.)*

1. **PC-01** — Pegamentos create: `UpdatedAtUtc` fallback to `CreatedAtUtc`
   (mirrors `UpdateAsync` at `DapperPegamentoRepository.cs:266`). Precondition:
   deployed-DDL probe (audit §22.2).
2. **PG-04** — Pegamentos controlo+medicoes and confirm-document as single UoWs;
   in-transaction closed-control check (domain rule already exists). No change to
   functional rules.
3. **ON-02/JA-03/TP-06/BQ-15/ADM-06** — map unique/duplicate violations to
   domain errors (`job_on` identity, `tampao_configurations`, `bq_lotes`,
   `internal_users` auth-user, `physical_pieces`); raw 23505/500 → actionable
   error. User-visible change is error-UX only.
4. **FA-03** — make Ferramentas lot duplication (lot + copied rules + audit) one
   UoW; remove the stale atomicity doc claim.
5. **ADM-14** — deploy runbook: guarantee `migrate` (incl. N33) precedes the
   first user write.
6. **PC-10/CB-01..05** — refresh `consolidated_clean_install.sql` to chain parity
   (N31 objects, N29 RLS stanza, N33 posture, corrected header) — **sequenced
   after the N34 phase** (see queue C); if parity is needed before N34, a
   *non-destructive* baseline refresh to the current HEAD state is acceptable.
7. **F17 (code part)** — remove dead runtime methods (orphan ports, Substituir/
   ReplaceOccupation wiring, BQ void family, `GetOccurrencesByRuleAsync`, stale
   catalogs per audit §18) **without** any DDL.
8. **PC-11 (optional)** — defensively apply `AuditJson.Normalize` + `::jsonb` on
   the Admin audit insert (voluntary hardening; finding classified NOT_A_VALID_FINDING).
9. **BQ-16 (optional, P3)** — add the `bq_movements.noted_repairer_id` index.

### B. IMPLEMENT TO ALIGN WITH MANUAL
*(FUNCTIONAL_ALIGNMENT — each changes user-visible behavior toward the Manual's
defined rules. Sequence code-first, then additive DDL; extend guard/parity tests.)*

1. **PC-08** — Reparação Externa `ConfirmReturnAsync`: recompute the exit status
   inside the UoW (in-tx item read or pass the confirmed item) so the finishing
   return reaches `Concluído` per 70:452–459/552–556 and the SOT C/D single-UoW
   rule (70:558–565).
2. **PC-03** — reconcile the audit jsonb convention: `AuditJson.Normalize` +
   `::jsonb` on Boquilhas/Tampões/Peso/Ferramentas/RI (+Controlo sheet events);
   convert the ≥17 free-text payload sites to serialized JSON; extend
   `AuditJsonBindingTests`. Manual anchor 50:316 (write+audit in the same atomic
   operation must succeed).
3. **PC-05 (D-5)** — Job On dual-emit: `audit_events` projections inside the same
   UoW; keep `job_on_audit_event` as the domain stream; parity guard test. Manual
   anchors 10:168 + 90:326/414 + 99:54.
4. **PC-04** — Pegamentos audit emission (Controlo-area events) in the same UoW;
   História/Admin Audit become observable. Manual anchors 90:326/414, 99:54,
   01:820.
5. **PC-09 (D-10)** — protect approved Peso readings: append-only trigger on
   `peso_leituras` (additive migration) + service assertion requiring the audited
   reopen path (revision+1, reason). Manual anchors 20:263,481,485.
6. **PC-06** — `job_on.production_folder`: include the column in
   `GetActiveAsync`/`GetByProductionCodeAsync`; add a folder writer **and** the
   SOT-aligned auto-resolution/auto-creation of the Root/Reference/Production
   lower folders (idempotent) so `PEGAMENTO_PRODUCTION_FOLDER_MISSING` disappears.
   Manual anchor 20:513–532.
7. **PC-13** — Tampões `alterar_configuracao`: truthful `balances_before`/
   `balances_after` (true destination-after incl. por_encher; origins/destinations
   preserved) and correct audit after_summary. Manual anchors 80:116–119,125–140.
8. **PC-14** — BQ discrepancy: `expected_qty` = the matched/outstanding return
   expectation (the "até ao esperado" reconciliation, 50:152,173); write
   `resolved_by`/`resolved_at_utc` + note at resolution (50:155,306); **do not**
   implement an under-review state. Manual anchor 50:155,173,194,306.

### C. BLOCKED — OWNER DECISION REQUIRED
*(No implementation until the named owner decisions are taken; nothing may be
inferred.)*

1. **PC-02 (D-12)** — one-sided Pegamentos measurements: owner decides whether a
   measurement without contra costura is a valid business record; branch A (make
   column nullable + domain rule) vs branch B (require contra costura at domain
   level; keep NOT NULL). Manual is two-axis (20:301–316) and does not decide.
2. **PC-07** — `app_settings.main_documents_output_root`: owner chooses the
   configuration surface (Admin settings UI vs documented/supported manual seed);
   Manual defines the requirement (root manually configured; subfolders
   automatic, 20:526–528) but not the surface.
3. **FA-05** — `physical_pieces.status`: owner decides the piece-level state model
   (CHECK on the 4 technical states, split column, or free-text) consistently with
   30:244 (no collapse of technical condition and physical whereabouts).
4. **PA-01** — occurrence-table consolidation: owner decision to physically retire
   `tool_check_occurrences` (the functional authority — Job-On-level
   materialization, 30:312 / 10:275–280 — is already honored in code; only the
   schema disposal is gated).
5. **F17 (DDL part)** — dormant-surface disposition **and sequencing**:
   D-9/D-11 REMOVE_LATER disposals and any table/column drop require the owner
   decision + row-count/parity guards (GLM-DATA-12); **N34 (mirror removal) must
   not be implemented**; D-7/D-8 are keep-dormant, not removals.
6. **PC-10 (sequencing)** — the final `consolidated_clean_install.sql` refresh
   targets the post-N34 state; a non-destructive parity refresh may proceed
   (queue A), but the destructive-phase baseline refresh is blocked on N34
   sequencing.

*Queue ordering rule (from audit §21): never mix schema changes with
dormant-surface removals in one migration; never drop a table/column without the
owner decision and guards; N34 stays separate and blocked.*

---

## 5. Annex — Excluded from this gate (technical-only, no user-visible behavior)

The following audit items are **technical-only** (no behavior-changing P0/P1/P2
functional content) and were therefore not gated, but are recorded here for the
remediation backlog:

- **MC-02** — N28/N29/N30 inner BEGIN/COMMIT handling + real-PG migration
  execution test (migration mechanics).
- **ON-04/FA-04** — warehouse 1:1-per-position DB enforcement vs code-level
  FOR UPDATE; `ReplaceOccupationAsync` FOR UPDATE parity (tied to F17 disposal).
- **PESO-05/DT-08** — Guid.Empty FK sentinel pre-validation.
- **DT-07** — cast-less snapshot binds in `repair_exits.repairer_snapshot` /
  RI `before_snapshot` (currently JSON-valid at call sites; convention-fragile —
  same family as PC-03).
- **BQ-16/HS-10/perf** — BQ index (queue A optional), História EXPLAIN, index
  housekeeping.
- **ADM-18/ADM-11** — bootstrap edge, vestigial 42703 gate (code hygiene).
- **RLS-06** — `RepairAtomicityTests` teardown `DELETE FROM audit_events` vs the
  append-only trigger (test-environment concern; LIVE VERIFICATION REQUIRED).
- **N34** — legacy mirror physical removal: separate design
  (`reports/schema_rationalization_N34_legacy_mirror_removal_audit.md`),
  **not gated here, not to be implemented.**

---

## Validation checklist

- ✅ 12 of 14 canonical Manual files read in full (6 directly by this gate:
  00_INDEX, 01_GLOBAL, 02_MODULES, 03_USERS, 10_JOB_ON, 20_CONTROLO; 6 by
  delegated full-file readers, reconciled verbatim: 30_FERRAMENTAS, 50_BOQUILHAS,
  60_RI, 70_RE, 80_TAMPOES, 90_ADMIN). The remaining 2 (40_ARMAZEM,
  99_DESIGN_LABORATORIO) were read in their audit-relevant sections only
  (grep-verified) and are not load-bearing for any finding classification here.
- ✅ All P0 (4) and P1 (5 incl. PG-04) and behavior-changing P2 (8) findings
  cross-referenced; supplementary PC-11/PC-10 included.
- ✅ Code-level evidence re-verified for PC-01, PC-02, PC-03 (bind sites),
  PC-04/PC-05 (origin catalog + audit targets), PC-06, PC-07, PC-08, PC-09,
  PC-13, PC-14 (Manual rules), FA-05.
- ✅ Every classification justified against Manual text; `OWNER_DECISION_REQUIRED`
  used wherever the Manual does not define the rule (PC-02, PC-07, FA-05, PA-01,
  F17 DDL).
- ✅ No source, migration, test, schema object, or database was modified; the
  only artifact produced is this report.