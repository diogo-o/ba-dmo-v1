# BA DMO — CODEX IMPLEMENTATION HANDOFF

## Purpose

This is the operational handoff for Codex.

It consolidates the reconciled functional deltas already identified from the current Manual, current Maps, and old Design material, while leaving all technical implementation decisions to Codex after inspection of the live repository.

This file does **not** replace the Manual, Maps, or authoritative Design assets.

Its purpose is to prevent Codex from repeating the old documentation reconciliation and to make the remaining work immediately visible.

---

# 1. Authority Model

Use the sources in this order and for these purposes:

1. **Manual** = final functional authority.
   - Defines what the application must do.
   - Defines valid module boundaries, user/profile behavior, workflows, ownership, closed decisions, and Owner questions.

2. **Live repository** = definitive evidence of what is implemented now.
   - If Maps and live code differ, live code wins for current implementation state.

3. **Maps** = technical navigation and implementation-state evidence.
   - Use them to locate pages, services, repositories, database structures, capabilities, tests, and known implementation gaps.
   - Maps do not override the Manual functionally.

4. **This handoff** = reconciled remaining functional work.
   - Tells Codex what is still missing, what must change, what must disappear, what needs verification, and what must remain deferred.
   - It defines **WHAT must be true**, not **HOW** to implement it.

5. **Authoritative Design HTML/CSS/JS** = visual and interaction target.
   - The real application should match these references as closely as possible where still functionally valid.
   - They are not merely inspiration.

Old Design markdown/history must not override the current Manual.

---

# 2. Codex Operating Rule

For every item in this handoff:

1. Read the relevant Manual file.
2. Read the relevant Map.
3. Inspect the live implementation.
4. Inspect the relevant authoritative HTML/CSS/JS when the surface is visual or interactive.
5. Resolve every `VERIFY FIRST` item from live evidence.
6. Classify the current state as one of:
   - `ALREADY CORRECT`
   - `REAL FUNCTIONAL GAP`
   - `VISUAL PARITY GAP`
   - `CURRENT FUNCTIONAL DIVERGENCE`
   - `OBSOLETE IMPLEMENTATION`
   - `DEFERRED / OWNER DECISION`
7. Choose the smallest safe implementation that satisfies the Manual.

Do **not** treat technical observations in this handoff as mandatory implementation instructions.

Examples:

- A DB constraint permitting an obsolete value does not automatically mean a migration is required.
- Dormant legacy tables or Domain types do not automatically need removal.
- A current single-template persistence model does not make single-template access the final functional rule.

Codex decides the technical convergence after live inspection.

---

# 3. Technical Freedom

Prefer the smallest safe change.

Prefer:

- PATCH
- EXTEND
- CONNECT
- CORRECT
- STYLE
- REUSE

before:

- REBUILD
- REARCHITECT
- DUPLICATE

Use only the layers required by the verified gap:

- visual parity only → Razor/CSS/JS/shared components;
- missing UI interaction → Web/Razor/JS;
- missing application behavior → Application + Web;
- missing domain rule → Domain + Application;
- missing persistence only when genuinely required → Infrastructure/DB.

Do not create schema, persistence, Domain, or architecture changes solely because historical technical remnants exist.

---

# 4. Global Functional Boundaries

## Top-level modules

The current top-level modules are:

1. Job On
2. Controlo
3. Ferramentas
4. Armazém
5. Boquilhas
6. Reparação Interna
7. Reparação Externa
8. Tampões
9. Admin

`História` is **not** an assignable top-level module.

## Controlo internal areas

Controlo contains:

- Peso
- Pegamentos
- Resumo / Folha de Controlo
- Histórico

Peso and Pegamentos are not separate canonical top-level modules.

## BQ ownership

- **Ferramentas** = BQ master / identity / classification.
- **Armazém** = normal physical location and movements.
- **Boquilhas** = BQ external-repair movement flow.
- BQ is never repaired in Reparação Interna.

## Users / Access

Final functional model:

- a user may have one or more Access Templates;
- templates determine accessible modules;
- profile determines behavior inside accessible modules;
- profile does not automatically grant modules;
- direct URL access cannot bypass authorization.

Current single-template persistence is implementation state only, not final functional truth.

---

# 5. Remaining Functional Work — Master Matrix

| Area | Still To Implement | Change / Correct | Remove / Do Not Implement | Verify First | Deferred / Owner |
|---|---|---|---|---|---|
| **Global / Users-Access** | One-or-more templates per user | — | Single-template as final rule | Persistence/convergence approach; live `historia` catalog state; override resolution | Free-text profile technical reconciliation |
| **História** | — | Remove as assignable module | História as normal template module | Live `historia` capability/module resolution | — |
| **Job On** | `Ver Controlo`, `Ver Peso`, `Ver Pegamentos`, `Ver Resumo`, `Ver reparações` **if still missing** | Reference-scoped article image functional rule | Per-revision image ownership as functional rule | Presence of linked views; article-image provider/persistence; open Job On clarifications | Manual §17 Owner questions; `% usage` snapshot; calendar source; lot eligibility |
| **Controlo** | PU/CS in Resumo **if still missing** | — | Second selector/calendar; obsolete Peso comparison model | Resumo five-piece coverage; MCaliper; free-mode consultation | — |
| **Ferramentas** | No confirmed functional gap | — | — | Open Manual questions only | Manual §16 six open questions |
| **Armazém** | BQ support; `Corrigir localização`; `+ Criar novo` | — | `Substituir` from active functionality | Live BQ presence; live `Substituir`; Q1–Q4; Saídas Programadas | Q1–Q4 |
| **Boquilhas** | No confirmed core gap | — | Generic BQ lifecycle / obsolete master ownership interpretations | PDF/print/export requirement | — |
| **Reparação Interna** | `Ver reparações` through Job On **if still missing** | BQ must not be recordable; `Editar contexto` must not exist | BQ recordability; manual context editing | DB CHECK divergence; live UI/code presence | — |
| **Reparação Externa** | No confirmed core gap | No BQ batches in active application | BQ batch type; combined/generic Reparação navigation | Print/PDF; dormant compatibility hooks | `CancelarLista`; close-with-open-items rules |
| **Tampões** | No confirmed core gap | — | Planeamento; active Job On/Production relation | Dormant planning structures; live Planeamento exposure | TP ↔ Job On conflict |
| **Admin** | Multi-template association UI | X12 Email defect; remove História from assignable modules | — | Override semantics; technical multi-template approach | — |
| **Login** | — | — | — | — | No delta |

---

# 6. Module Detail

## 6.1 Global / Users / Access

### Manual authority

- `01_GLOBAL_MODULE_USER_ROLE.md`
- `02_MODULES_OPERATIONAL.md`
- `03_USERS_ACCESS_OPERATIONAL.md`
- `90_ADMIN_FUNCTIONAL.md`

### Maps

- `16_USERS_ACCESS.md`
- `15_ADMIN.md`
- `19_APPLICATION.md`
- `20_WEB.md`

### Still to implement

- Functional support for **one or more Access Templates per user**.

### Current technical state

- Current implementation persists one template per user.
- A per-user override structure also exists.

### Verify first

Codex must inspect the live resolver, catalogs, persistence, and override semantics before choosing the technical convergence.

### História correction

História must not remain a top-level assignable module.

Preserve only the valid concepts:

- module-specific Histórico areas;
- Admin Audit;
- grant-scoped transversal read surface where functionally valid.

### Deferred

- TP/Tampões ↔ Job On conflict remains unresolved.
- Do not silently resolve it.

---

## 6.2 Job On

### Manual authority

- `10_JOB_ON_FUNCTIONAL.md`

### Map

- `06_JOB_ON.md`

### Visual / output references

Use the authoritative `20_JOB_ON_*` HTML/design references and Job On print authority from old-design.

### Still to implement — verify live first

Production-level read access for Responsável:

- `Ver Controlo`
- `Ver Peso`
- `Ver Pegamentos`
- `Ver Resumo`
- `Ver reparações`

If any already exist in live code, classify them as `ALREADY CORRECT` rather than reimplementing them.

### Article image — final functional rule

- The image belongs to the **Article/Reference context**.
- Job On consumes that image.
- It is not functionally owned by an individual production revision.
- Only the required Job On print sheet displays it.

### Verify first

Determine whether the existing provider/persistence already satisfies the reference-scoped behavior before changing implementation.

Also verify:

- `% usage` snapshot moment;
- calendar past-day movement source;
- selector lot eligibility states.

### Preserve

- Job On is the central production context.
- Downstream modules inherit context rather than redefining tooling.
- Revisions/snapshots remain immutable.

### Deferred / Owner

Keep the Manual §17 open questions unresolved:

- family siglas meaning;
- lifecycle statuses;
- Novo em branco template fields;
- Duplicar anterior ordering;
- verification completion/cancellation/reset rules;
- stock vs quantity meaning;
- Tipo vs Processo relationship.

---

## 6.3 Controlo

### Manual authority

- `20_CONTROLO_FUNCTIONAL.md`

### Map

- `07_CONTROLO.md`

### Visual references

- `21_CONTROLO_*`
- `22_PESO_OPERADOR_*`
- `23_PESO_RESPONSAVEL_*`
- `24_PEGAMENTOS_*`

### Verify first

Confirm whether Resumo / Folha de Controlo surfaces all five required pieces:

- CM
- BQ
- MF
- PU
- CS

PU/CS must come from the exact Job On revision context, not Armazém.

If PU/CS are absent, this becomes a real functional gap.

Also verify:

- MCaliper persistence/link behavior;
- free-mode consultation.

### Preserve

- one top-level Controlo module;
- Peso/Pegamentos/Resumo/Histórico internal only;
- exact `job_on_id + job_on_revision_id` pinning;
- draft → submitted → approved/rejected + reopen;
- server-side Peso calculations;
- comparison is per-CM glass weight, not water/capacity/global average.

### Remove / do not reintroduce

- second selector / second calendar concepts;
- obsolete Peso water/capacity/global-average comparison rules;
- legacy browser-local document behavior.

If live verification shows full alignment, no additional functional work is required here beyond visual parity.

---

## 6.4 Ferramentas

### Manual authority

- `30_FERRAMENTAS_FUNCTIONAL.md`

### Map

- `08_FERRAMENTAS.md`

### Current status

No confirmed functional implementation gap in the reconciled material for the core CM/MF/BQ/PU/CS master, lots, verifications, and utilisation.

### Preserve

- Ferramentas owns BQ master/identity.
- `MP` is a legacy alias of CM, not a separate family.
- SAP utilisation is manual and never automatically calculated.

### Deferred / open Manual questions

Preserve the six Manual §16 open questions without guessing:

- lot numbering rule;
- reason requirement for technical-state changes;
- repair → technical-state flow;
- verification duplication of inactive rules;
- actual additional master fields;
- Entrada `Estado` synchronization.

Main implementation work here may therefore be visual parity unless live repository inspection reveals a genuine functional divergence.

---

## 6.5 Armazém

### Manual authority

- `40_ARMAZEM_FUNCTIONAL.md`

### Map

- `09_ARMAZEM.md`

### Visual reference

Use the authoritative `32_ARMAZEM_*` Design assets.

### Still to implement

Subject to live confirmation:

- BQ support in the normal warehouse model;
- `Corrigir localização`;
- `+ Criar novo`.

### Remove from active functionality

- `Substituir`.

Current Maps indicate `Substituir` exists in the implementation. Codex must verify the live state and choose the smallest safe removal/correction.

### Preserve

- Armazém owns physical location/movements, not technical master/state.
- Destino is not technical state.
- one-position / one-item occupation rule;
- append-only movement history.

### Verify / deferred

Manual open questions remain open:

- Q1 role split;
- Q2 `Programadas` final target;
- Q3 Destino required vs optional;
- Q4 Entrada `Estado` classification.

Also verify current Saídas Programadas implementation before changing it.

---

## 6.6 Boquilhas

### Manual authority

- `50_BOQUILHAS_FUNCTIONAL.md`

### Map

- `10_BOQUILHAS.md`

### Visual reference

Use the authoritative `31_BOQUILHAS_*` Design assets.

### Current status

Core movement/trace/saldo behavior is considered implemented by the reconciled evidence.

### Preserve

- Ferramentas owns BQ master.
- Boquilhas owns only the BQ external-repair movement flow.
- BQ behaves like CM/MF in normal master/warehouse context.
- BQ never belongs to Reparação Interna.
- excess return such as 20 → 25 is accepted and must not block.

### Remove / do not reintroduce

- Boquilhas as owner of BQ master;
- generic BQ lifecycle such as module-owned archive/scrap behavior.

### Verify first

Confirm whether PDF/print/export of movements is an actual current requirement before implementing output work.

---

## 6.7 Reparação Interna

### Manual authority

- `60_REPARACAO_INTERNA_FUNCTIONAL.md`

### Map

- `11_REPARACAO_INTERNA.md`

### Visual reference

Use the authoritative `34_REPARACAO_INTERNA_*` Design assets only where consistent with the current Manual.

### Final functional rule

Reparação Interna supports **CM and MF only**.

BQ:

- may appear only as production/reference context;
- is never selectable;
- is never repairable;
- is never processed as an RI repair type.

### Current technical divergence

The current DB CHECK may still permit BQ even where Domain/Application reject it.

This is a `VERIFY FIRST` technical divergence, not an instruction to change the schema automatically.

### Change / correct

- BQ must not be recordable in RI.
- `Editar contexto` must not exist as active functional behavior if still present.

### Still to implement

- Job On production-level read visibility: `Ver reparações`, if still missing.

### Preserve

- repeated repair numbers create independent occurrences;
- no operational hard blocks;
- append-only corrections/history;
- repairer = authenticated user;
- 06:00/09:00 context rule;
- full reference such as `5447T173` remains visible.

### Verify first

- live presence of `Editar contexto`;
- live BQ selectors/options;
- whether DB hardening/cleanup is actually required.

---

## 6.8 Reparação Externa

### Manual authority

- `70_REPARACAO_EXTERNA_FUNCTIONAL.md`

### Map

- `12_REPARACAO_EXTERNA.md`

### Visual reference

Use valid `35_REPARACAO_EXTERNA_*` authority files.

The obsolete combined `reparacao-v2.html` must not define the target.

### Final functional rule

- No BQ batches in active Reparação Externa.
- BQ external repair belongs to Boquilhas.
- CM and MF remain separate and are never mixed.

### Current technical state

BQ values/fields may still exist as dormant compatibility hooks.

Do not remove them automatically. Verify whether cleanup is necessary.

### Remove / do not implement

- BQ batch type;
- old combined/generic Reparação navigation and six-area composition.

### Preserve

- Responsável-only module;
- Operador uses Armazém for physical individual movements;
- batch always editable;
- Armazém owns physical state via the integration port;
- CM/MF separate.

### Verify first

- Print/PDF requirement for programmed list;
- dormant BQ compatibility hooks.

### Deferred

- `CancelarLista` / `Cancelado`;
- close-with-open-items rules.

---

## 6.9 Tampões

### Manual authority

- `80_TAMPOES_FUNCTIONAL.md`

### Map

- `13_TAMPOES.md`

### Visual reference

Use valid `33_TAMPOES_*` Design assets, excluding obsolete functional areas.

### Remove from active functionality

- Planeamento;
- active Job On/Production planning relation;
- active Reference relationship.

### Current technical remnants

Planning Domain/DB structures may still exist.

Do not prescribe their removal or deactivation solely from this handoff.

### Preserve

- autonomous module;
- configuration = Máquina(s) + Diâmetro + Calote;
- aggregated quantities;
- append-only movements;
- no individual TP numbers;
- Operador/Controlador is the operational user.

### Verify first

- whether Planeamento surfaces are still exposed in live UI;
- whether dormant planning structures require any cleanup.

### Owner decision

The TP/Tampões ↔ Job On conflict remains unresolved.

Do not implement either side of that conflict until explicitly decided.

---

## 6.10 Admin

### Manual authority

- `90_ADMIN_FUNCTIONAL.md`

### Map

- `15_ADMIN.md`

### Visual reference

Use authoritative `13_ADMIN_*` Design assets.

### Still to implement

- one-or-more Access Template association UI, after the access model is technically converged.

### Change / correct

- X12: never display auth UUID as Email in the Users list.
- Remove História from assignable modules.

### Preserve

- fail-closed authorization gates;
- profile and templates are separate concepts;
- self-lockout guard;
- append-only audit;
- Templates remain a current Admin area.

### Verify first

- per-user override semantics;
- technical approach for multi-template association;
- live catalog/template behavior for História.

---

## 6.11 História

### Manual authority

- `90_ADMIN_FUNCTIONAL.md`
- `99_DESIGN_LABORATORIO.md`
- `01_GLOBAL_MODULE_USER_ROLE.md`

### Map

- `14_HISTORIA.md`

### Visual reference

Use `36_HISTORIA_*` only as a visual/read-surface reference.

### Change / correct

História must not be a top-level assignable module.

### Preserve

- module-specific Histórico tabs/areas;
- Admin Audit;
- grant-scoped transversal read of audit events where the implementation retains this valid read surface.

### Remove from active functionality

- História as a normal Access Template module.

### Verify first

Inspect how the live `historia` module/capability currently resolves against the Manual's non-module rule.

---

## 6.12 Login

No confirmed functional delta.

Use:

- current Manual;
- current Maps;
- authoritative Login visual reference.

Primary expected work, if any, is visual parity rather than new functionality.

---

# 7. Contradictions Already Reconciled

Do not reopen these as if both alternatives were still active.

| Area | Obsolete interpretation | Current functional truth |
|---|---|---|
| História | Normal assignable top-level module | Not assignable; transversal read + Admin Audit + module Histórico |
| Users/Access | Single template as final rule | One-or-more templates per user |
| Reparação Interna | BQ selectable/repairable | CM/MF only; BQ context-only |
| Reparação Interna | `Editar contexto` as normal behavior | Not part of current functional truth |
| Tampões | Planeamento / Job On / Production relation | Autonomous; Planeamento obsolete |
| Armazém | `Substituir` as normal action | No active Substituir target |
| Boquilhas | Boquilhas owns BQ master | Ferramentas owns BQ master |
| Reparação Externa | Generic combined repair / BQ batches | Separate module; BQ excluded |
| Job On image | Per-revision image ownership | Article/Reference-scoped image |
| Peso | Water/capacity/global-average comparison | Per-CM glass-weight comparison |
| Controlo | Peso/Pegamentos as separate modules / second selector/calendar | One module, internal areas, one bound context |

The only genuine unresolved cross-module conflict explicitly preserved here is TP/Tampões ↔ Job On.

---

# 8. Visual Implementation Contract

A significant portion of the approved Design may still be missing or only partially reproduced in the real application.

For every surface touched:

1. Locate the authoritative HTML.
2. Inspect its CSS.
3. Inspect its JS/interactions.
4. Compare with the current Razor/application implementation.
5. Reproduce the approved Design as closely as possible where functionally valid.

Preserve applicable:

- layout;
- dimensions;
- spacing;
- typography;
- cards;
- headers;
- tabs;
- side panels;
- tables;
- forms;
- dialogs;
- controls;
- states;
- responsive behavior;
- interaction patterns.

Do not redesign or modernize approved layouts simply because another implementation would be easier.

When a confirmed new functional requirement has no exact old HTML control, integrate it using the closest existing approved visual and interaction pattern.

---

# 9. First Codex Pass — Verification Only

Before modifying code:

1. Establish Git baseline:
   - repository root;
   - branch;
   - HEAD;
   - working tree state.

2. Read:
   - Manual index;
   - Maps index;
   - this handoff.

3. Validate the Master Matrix against the **live repository**.

4. For every listed item, report:
   - `ALREADY CORRECT`
   - `REAL FUNCTIONAL GAP`
   - `VISUAL PARITY GAP`
   - `CURRENT FUNCTIONAL DIVERGENCE`
   - `OBSOLETE IMPLEMENTATION`
   - `VERIFY_FIRST_RESOLVED`
   - `DEFERRED / OWNER DECISION`

5. Identify any Qwen-era/Map-era gap that has already been implemented since the documentation was produced.

6. Resolve technical `VERIFY FIRST` items where live evidence is sufficient.

7. Produce the smallest safe execution order.

**Do not modify code during this first verification pass.**

---

# 10. Implementation Acceptance Rule

A module is not complete merely because its backend workflow exists.

Completion requires, where applicable:

- functional behavior aligned with the Manual;
- obsolete behavior removed from the active application;
- verified integrations working;
- authorization behaving correctly;
- authoritative visual/interaction parity applied;
- targeted tests passing;
- regression checks passing;
- unresolved Owner items left untouched.

Codex should use this handoff to avoid re-reconciling old documentation, then rely on live repository evidence to choose the best technical implementation.
