# BA-DMO — Recovery / Repair Register

> **Purpose**
>
> Single authoritative working register of the current UI/UX, workflow, permission, architecture and cleanup problems that still need repair.
>
> This file is a **recovery backlog**, not an implementation plan.
>
> Do not treat an item as fixed until it has been verified in the real application and its status is explicitly changed here.

## Authority

When implementation, old design, stale tests, old Maps or historical handoffs conflict with current owner decisions recorded here, the current owner decision wins.

Core product law:

> **Store what the user says. Link it correctly. Filter it well. Do not infer industrial truth.**

The application is primarily responsible for registration, association, persistence, retrieval and filtering. Machine, line, lot, reference, previous production and historical associations are context/search/filter data unless an explicit authoritative rule says otherwise.

## Status legend

- `OPEN` — confirmed problem, not fixed.
- `REGRESSION` — previously defined behavior has regressed.
- `MISSING` — required behavior does not exist.
- `SUPERSEDED` — implementation/module/concept should no longer be authoritative.
- `UX` — UX/layout/presentation problem.
- `CLEANUP` — legacy/waste/security/technical cleanup.
- `UNRESOLVED` — owner decision still required.
- `VERIFY` — suspected or repo-backed issue requiring runtime verification.
- `FIXED` — implemented and verified.

---

# 1. Global Shell / Navigation

## R-001 — Main tabs still shift between modules
**Status:** `OPEN / REGRESSION / UX`

Primary module tabs still move slightly when changing pages/modules. Entering Boquilhas is a confirmed example.

Required direction:
- header, logo, profile area, primary tabs, secondary tabs, global content left edge and page width must feel physically pinned;
- page/module CSS must not redefine shell geometry;
- scrollbar appearance must not move the shell;
- loading/data changes must not collapse mounted regions or cause white flashes.

## R-002 — Admin logout/user menu is displaced
**Status:** `OPEN / UX`

The Admin logout/user menu does not align with the shared header/profile geometry.

This must be fixed at shell ownership level, not with an Admin-specific positional patch.

## R-003 — Sidepanel shells are inconsistent
**Status:** `OPEN / UX`

Where a sidepanel is useful, it must reuse one consistent outer shell:
- same width rules;
- same alignment;
- same spacing;
- same loading behavior;
- same mounted/collapse behavior.

Content may differ by module.

## R-057 — Controls are flush against card edges
**Status:** `OPEN / UX / REGRESSION`

Confirmed across multiple pages/modules: buttons, inputs, text areas, selects and other controls can sit directly against the card/container edge with little or no internal breathing room.

Required direction:
- cards/panels must provide consistent internal padding around interactive controls;
- controls must not visually touch card borders;
- labels, fields and action rows need a stable minimum inset from container edges;
- spacing must be owned by the card/layout component, not repaired with one-off margins on individual buttons/inputs;
- compact/dense UI is still required, but density must not remove basic readable padding;
- the same spacing language should apply across modules so one page does not look cramped while another has excessive empty space.

This is a cross-module design-system/layout issue, not a page-specific patch.

---

# 2. Job On / Planeamento

## R-004 — Operator Job On enters editable state while UI says Consulta
**Status:** `OPEN / REGRESSION`

Confirmed behavior: an Operador can encounter Job On controls in an editable state while the page indicates `Modo consulta`.

Required behavior:
- Operador: consultation only;
- Responsável: opens in consultation and can explicitly enter edit mode;
- displayed mode must always match actual control state;
- backend/capability enforcement must match the UI, not merely hide controls.

## R-005 — Job On role actions are wrong
**Status:** `OPEN / REGRESSION`

Actions intended for Responsável are appearing in Operador context.

For **Responsável**, primary actions remain directly visible:
- `Editar`
- `Duplicar`
- `Imprimir`

For Operador these management actions must not be exposed as if available.

## R-006 — Job On secondary action hierarchy regressed
**Status:** `OPEN / REGRESSION / UX`

The following should live under the `...` menu for Responsável:
- `Controlo`
- `Reparações`
- `Histórico`
- `Verificações`

Do not use redundant labels such as:
- `Ver Controlo`
- `Ver Reparações`
- `Imprimir 4 folhas`

The context already makes the action obvious.

This is restoration of an already-decided UX, not a new redesign.

## R-007 — Job On basic card sizing/typography breaks
**Status:** `OPEN / UX`

Confirmed visual problems:
- labels/titles do not fit inside their own boxes;
- values overflow containers;
- `Peso` is a confirmed example of text/value leaving the card;
- some cards waste width while others do not have enough.

The sheet must remain dense, but no text may overflow or become unreadable.

## R-008 — Data Final semantics were incorrectly reinterpreted
**Status:** `OPEN / REGRESSION`

Correct owner rule:
- Data Inicial is user-entered in Job On;
- Data Final is also user-entered in Job On from the beginning;
- Data Final must **not** be automatically calculated as `Data Inicial + N days`;
- `Por confirmar` is not the normal initial state.

Production may later slip, but that does not remove the planned final date from the original Job On.

## R-009 — Job On/Planeamento rail ownership must remain clear
**Status:** `OPEN / UX`

The Planeamento sidepanel/rail is primarily a quick operational view of current lines.

A permanent rail must not be forced into every Job On page if it steals useful sheet width.

If a page uses a sidepanel, use the shared sidepanel shell.

## R-010 — Planeamento sidepanel must remain mounted during data changes
**Status:** `OPEN / UX`

Selecting a date/production or refreshing data must update content without:
- removing the panel;
- changing its width;
- collapsing its column;
- flashing/rebuilding the whole region.

## R-011 — Job On Definições is incomplete/stub-like
**Status:** `OPEN / VERIFY`

Do not fill this with invented functionality. Reconcile with centralized Admin settings before implementing anything here.

## R-012 — Job On edit-after-submit/state semantics remain unclear
**Status:** `UNRESOLVED`

Current audit found Job On editing may be allowed too broadly across states.

Do not alter state semantics until the exact current owner rule is recorded.

## R-013 — Duplicar anterior ordering remains unresolved
**Status:** `UNRESOLVED`

Do not change ordering/default-selection logic without explicit owner decision.

## R-014 — Calendar movement markers remain unresolved
**Status:** `UNRESOLVED`

Source/meaning of movement markers still needs owner clarification. Do not invent new semantics.

---

# 3. Controlo / Peso / Pegamentos

## R-015 — Controlo says “Selecionar Job On” but provides no selector
**Status:** `OPEN / MISSING`

Controlo recognizes Job On context but currently lacks a usable mechanism to select one.

Required behavior:
- if opened from an active Planeamento/Job On context, use that context;
- user must also be able to explicitly change/select Job On;
- selector should expose enough context to distinguish productions;
- do not infer which production the user “must” use.

## R-016 — Peso historical comparison must not be blocked by machine/lot chronology
**Status:** `OPEN / REGRESSION`

Historical comparison is explicitly chosen by the user.

Do not block a comparison because of:
- current machine;
- previous machine;
- last machine;
- latest production;
- latest lot;
- chronological predecessor.

Examples that must remain valid:
- current B2 compared with historical C1;
- current return to older Lote 2 compared against older Lote 2 even if recent productions used Lote 4.

Filtering/search context may narrow choices, but explicit user selection is authoritative.

## R-017 — Peso approval lifecycle must remain reopenable
**Status:** `VERIFY`

Expected workflow:
1. Operador measures;
2. sends for approval;
3. Responsável approves/rejects;
4. approved record is protected from normal editing;
5. if correction is needed: reopen/remove approved state -> edit -> resubmit -> approve again.

Do not model Approved as permanently terminal if that blocks the real workflow.

---

# 4. Boquilhas

## R-018 — Boquilhas sidepanel is visually inconsistent with Planeamento
**Status:** `OPEN / UX`

Boquilhas may keep a sidepanel because it is useful.

However, it must use the same shared sidepanel outer shell as Planeamento. Only internal content should differ.

## R-019 — Boquilhas external repair flow is duplicated
**Status:** `OPEN / REGRESSION / SUPERSEDED`

Boquilhas already owns its repair/fabrication/tracking flow, while equivalent functionality also appears under the old Reparação Externa area.

Required architecture:
- BQ repair/fabrication/tracking belongs to **Boquilhas**;
- do not keep a duplicate BQ repair source of truth in Reparação Externa.

## R-020 — Boquilhas are individually tracked
**Status:** `VERIFY / AUTHORITY`

BQ is the exception to CM/MF lot-level external movement: Boquilhas have individual tracking.

Do not collapse BQ repair tracking into the CM/MF lot-level model.

## R-021 — Optional Boquilhas association to production/Job On is missing
**Status:** `MISSING / FUTURE IMPLEMENTATION`

Desired direction:
- BQ flow remains independent;
- allow optional contextual association with a production/Job On for later traceability;
- association is not a hard industrial validity rule.

Possible sources when creating/using a BQ lot:
- pull existing lot from Armazém;
- create a new lot;
- use/associate a lot already present in Job On.

The purpose is historical traceability: know later which production used the BQ/lote.

## R-022 — Preserve discrepancy-first behavior
**Status:** `VERIFY / DO NOT REGRESS`

Correct existing behavior:
- if real returned quantity differs from expected, accept the real quantity;
- record the discrepancy;
- do not force mathematical balance.

This is a product-law example and must not regress.

## R-023 — Boquilhas Saldo presentation still needs owner decision
**Status:** `UNRESOLVED / UX`

Audit identified the presentation as divergent, but the desired final presentation is not yet locked.

Do not redesign it autonomously.

---

# 5. Reparação Interna

## R-024 — RI history `lote` echoes reference
**Status:** `OPEN / CONFIRMED BUG`

Confirmed by reconciliation.

Required:
- reference field = actual production/reference context;
- lote field = actual associated CM/MF lot;
- never copy reference into lote;
- if old data cannot resolve a lot, show unknown/null rather than inventing one.

## R-025 — RI annulment is missing
**Status:** `OPEN / MISSING`

Manual defines annulment behavior, but implementation lacks it.

Implementation must preserve:
- history;
- record identity;
- actor attribution;
- production/Job On context.

Do not hard-delete if logical annulment is authoritative.

## R-026 — RI detailed information access must be restricted appropriately
**Status:** `OPEN / REGRESSION`

Detailed repair information is for Responsável use, not a general productivity board visible to everyone.

Responsável consultation should be able to filter by:
- MF;
- CM;
- machine;
- repairer.

RI remains CM/MF-only during production. BQ is not a repaired RI tool.

---

# 6. Reparação Externa / Armazém Architecture

## R-027 — Reparação Externa autonomous module is superseded
**Status:** `OPEN / SUPERSEDED`

Current owner direction:
- autonomous Reparação Externa module should cease to exist;
- BQ repair/fabrication belongs to Boquilhas;
- CM/MF external programmed repair belongs inside Armazém.

Do not merely hide the tab. Trace routes, pages, services, repositories, tests and capabilities before removing the old module.

## R-028 — Old RE `IsPreparing` behavior conflicts with Manual
**Status:** `OPEN / LEGACY`

Confirmed divergence exists around add/remove behavior gated by `IsPreparing`.

Because the autonomous RE module is superseded, avoid spending effort polishing this legacy flow before responsibilities are migrated.

---

# 7. Armazém

## R-029 — Armazém UI/UX is currently unusable/poorly structured
**Status:** `OPEN / UX`

Do not continue adding isolated patches to the current layout. Reorganize around the actual work model.

## R-030 — Armazém target navigation
**Status:** `AUTHORITY / FUTURE IMPLEMENTATION`

Armazém should converge on four main areas:

1. `Consulta`
2. `Registo`
3. `Reparações Programadas`
4. `Histórico`

## R-031 — Entrada and Saída should be movements inside Registo, not tabs
**Status:** `OPEN / UX / ARCHITECTURE`

`Entrada` and `Saída` are two movement types in one workflow.

Inside `Registo`, user chooses Entrada or Saída.

Saída may include real destinations/reasons such as:
- Fabrico/Produção;
- Reparação;
- Engano/Correção;
- other already-authorized movement reasons.

## R-032 — Ferramentas should not remain a standalone module
**Status:** `OPEN / SUPERSEDED`

Ferramentas belongs inside Armazém, primarily through Consulta/cadastro/context.

Tool data such as type, reference, lot and machine/line remains important for lookup and association, but should not justify a separate top-level module.

## R-033 — Armazém Saída -> Reparação loses repairer
**Status:** `OPEN / CONFIRMED BUG`

A normal Armazém exit to Reparação must persist the selected canonical repairer and return it in history/readback.

Do not create a second repairer identity field.

## R-034 — Reparações Programadas move into Armazém
**Status:** `MISSING / FUTURE IMPLEMENTATION`

Access model:
- all users may consult;
- only Responsável may create/edit.

This follows the same consultation-vs-maintenance principle as Job On.

## R-035 — Programmed external CM/MF repair is lot-level
**Status:** `AUTHORITY`

CM/MF external repairs are handled by complete lots, not individual components.

Relevant identity/context includes:
- type;
- reference;
- lot;
- machine/line.

Do not invent piece-by-piece tracking for CM/MF external repair.

## R-036 — Programmed repair waits in standby for lot return
**Status:** `MISSING / FUTURE IMPLEMENTATION`

When a complete lot is sent, its programmed repair remains pending/standby until the lot physically returns.

## R-037 — Armazém entrada should automatically register return of programmed repair
**Status:** `MISSING / FUTURE IMPLEMENTATION`

When the matching lot returns and is registered in Armazém:
- automatically associate the entry with the pending programmed repair;
- mark/check the lot as received;
- record entry date/time;
- record the storage position;
- retain actor/movement trace where applicable.

The real Armazém entry is the physical confirmation of return. Do not require duplicate confirmation in another module.

## R-038 — Internal vs external repair must stay distinct
**Status:** `AUTHORITY`

- CM/MF external programmed repair = Armazém, lot movement.
- CM/MF repaired by company repairers during production = Reparação Interna, own repair records.

Do not merge these workflows into one generic repair model.

---

# 8. Tampões

## R-039 — `Gerir linha` was invented and has no authority
**Status:** `OPEN / REGRESSION / SUPERSEDED`

Tampões currently exposes `Gerir linha`, but no such workflow was requested or defined.

Trace and remove/retire the entire unsupported concept, not only the tab, after verifying dependencies:
- page/route;
- JS;
- service;
- repository;
- DTO/model;
- tests;
- permissions;
- schema artifacts if any.

## R-040 — Tampões should use reusable Templates
**Status:** `MISSING / AUTHORITY`

Desired model:
- user-defined reusable templates;
- arbitrary key/value fields;
- examples may include Diâmetro, Calote, Estado, Condição, Máquina;
- examples are not hardcoded schema requirements;
- quantities/location/notes as needed;
- search by populated field names/values;
- no production planning;
- no industrial inference engine.

## R-041 — Tampões exposes raw JSON to users
**Status:** `OPEN / UX`

Confirmed UI leak: before/after values are displayed as raw JSON such as:

`{"enchidos":68,"por_encher":10}`

Internal JSON persistence may remain if appropriate, but the UI must format this as human-readable information.

## R-042 — Tampões exposes smoke/test-looking identity data
**Status:** `OPEN / CLEANUP / VERIFY`

Examples such as `tampoes-smoke-2` are visible in normal UI.

Verify whether smoke/demo records or technical identifiers are leaking into PROD user-facing history. Remove test pollution where safe and show friendly actor identity when available.

## R-043 — `tampao_planos` appears to be obsolete planning infrastructure
**Status:** `SUPERSEDED / CLEANUP / VERIFY`

Do not delete blindly. Confirm callers/dependencies, then retire if it only serves the superseded planning concept.

---

# 9. História

## R-044 — História classification/access is wrong
**Status:** `OPEN / AUTHORITY`

Owner decision:
- História is a normal functional consultation tab/module;
- Operador has access;
- Responsável has access;
- it is not Admin debug.

This resolves the previous unresolved História classification.

## R-045 — Admin Debug must remain separate from História
**Status:** `AUTHORITY`

Technical debug/diagnostic information belongs in Admin and is Admin-only.

Do not mix user-facing historical consultation with technical diagnostics.

---

# 10. Admin / Module Settings

## R-046 — Module settings/options are inconsistent and scattered
**Status:** `OPEN / UX / ARCHITECTURE`

Current settings/options appear in inconsistent places across modules/pages.

Owner direction:
- operational pages should focus on operation;
- module configuration/settings should be centralized under Admin.

## R-047 — Create centralized Admin module settings area
**Status:** `MISSING / AUTHORITY`

Admin should contain a Configurações/settings area with tabs for the relevant modules.

Example structure:
- Planeamento / Job On
- Controlo
- Armazém
- Boquilhas
- Reparação Interna
- Tampões
- other modules only when they genuinely have configuration

Rule:
- **Operation stays in the module.**
- **Configuration goes to Admin.**

Contextual links may navigate to the central setting, but must not create duplicate settings implementations.

Examples:
- creating a programmed repair = Armazém operation;
- configuring reusable module lists/defaults = Admin;
- creating/editing Tampões templates = Admin > Tampões;
- using a Tampões template = Tampões operational page.

## R-048 — Access template model documentation/UX must not regress
**Status:** `VERIFY / DO NOT REGRESS`

Older docs described one-or-more templates, while the newer effective model uses the current authoritative access-template behavior.

Ensure Admin UI does not resurrect superseded template semantics.

---

# 11. Ferramentas

## R-049 — `allowed_lines`/machine context must remain filtering, not industrial enforcement
**Status:** `VERIFY / DO NOT REGRESS`

Machine/line associations help search/filter/context.

Do not block explicit user choices based on inferred compatibility unless an explicit authoritative structural rule requires it.

## R-050 — SetCondition reason semantics remain unresolved
**Status:** `UNRESOLVED`

Do not invent mandatory reason behavior until owner rule is established.

---

# 12. Waste / Cleanup / Security

## R-051 — Known waste/legacy inventory must be handled after functional recovery
**Status:** `CLEANUP`

Already identified candidates include:
- dead/superseded Boquilhas methods such as `EditLote` / `ApplyLifecycle`;
- dormant `InsertImageMutationAsync`;
- dead `PegamentoToleranceStatus.Warning`;
- duplicate/historical design bundles;
- debug/bootstrap/smoke tooling;
- stale Docs/Maps snapshots;
- superseded Reparação Externa code;
- superseded Tampões planning infrastructure.

Do not delete by appearance alone. Confirm runtime references/dependencies first.

## R-052 — Secret-bearing `ba-dmo.env` exists in local tree
**Status:** `CLEANUP / SECURITY`

Ensure secrets are not committed. Move/ignore/rotate as appropriate using the dedicated security cleanup process.

## R-053 — `ba_dmo_guard_peso_approved` mutable search_path warning
**Status:** `CLEANUP / SECURITY`

Supabase advisor warning exists. This is not the main functional blocker but must remain in the security backlog.

Do not change the function blindly; preserve legitimate Peso reopen/edit/resubmit/reapprove behavior.

## R-054 — PROD schema reconciliation is incomplete
**Status:** `VERIFY`

The prior reconciliation could not fully inspect PROD schema through the management API because the required token was unavailable.

Do not perform destructive schema cleanup based solely on repository assumptions.

## R-055 — Local working tree is unsafe as a clean implementation baseline
**Status:** `CLEANUP / PROCESS`

Known state during reconciliation:
- local branch was behind origin/main;
- dozens of modified/untracked files;
- production rail/context WIP mixed into the tree;
- temporary artifacts present.

Use isolated worktrees/current remote baseline for serious repair work. Never reset/clean unrelated owner WIP.

---

# 13. Process / Authority Failure

## R-056 — Decisions are spread across too many competing sources
**Status:** `OPEN / PROCESS`

Current decisions are distributed across:
- Manual;
- Maps;
- implementation;
- old designs;
- reports/handoffs;
- tests;
- WIP;
- owner conversation decisions.

This has allowed agents to resurrect already-rejected concepts such as:
- separate Reparação Externa;
- Tampões `Gerir linha`;
- old Job On labels/actions;
- inconsistent sidepanels;
- invalid role states.

Required process direction:
- maintain a short current-owner-decisions authority file;
- agents must read it before modifying implementation;
- older implementation/design/tests do not override newer explicit owner decisions.

---

# Recovery Priority Order

This is **not** permission to implement all items at once. Use small verified batches.

## P0 — Make the application trustworthy/useable
1. Job On role/mode enforcement (`R-004`, `R-005`, `R-006`).
2. Global shell shifts / Admin header (`R-001`, `R-002`, `R-003`, `R-057`).
3. Job On overflow/layout basics (`R-007`).
4. Controlo real Job On selection (`R-015`).
5. Raw/technical data leaking into UI (`R-041`, `R-042`).

## P1 — Remove architectural contradictions
1. Reparação Externa supersession (`R-019`, `R-027`, `R-028`).
2. Ferramentas into Armazém (`R-032`).
3. Armazém information architecture (`R-029`–`R-038`).
4. Tampões remove invented Gerir Linha and restore Templates direction (`R-039`, `R-040`, `R-043`).
5. História classification (`R-044`, `R-045`).
6. Centralize module settings in Admin (`R-046`, `R-047`).

## P2 — Confirmed functional corrections
1. Peso comparison freedom (`R-016`).
2. RI lote readback (`R-024`).
3. RI annulment (`R-025`).
4. Armazém repairer persistence (`R-033`).
5. Preserve Job On date semantics (`R-008`).

## P3 — New integrations after recovery
1. Optional Boquilhas <-> production context (`R-021`).
2. Programmed repair return automation (`R-034`–`R-037`).
3. Central Admin module settings implementation (`R-047`).

## P4 — Cleanup / hardening
1. Waste removal after dependency verification (`R-051`).
2. Security hygiene (`R-052`, `R-053`).
3. Schema verification (`R-054`).
4. Working-tree/process cleanup (`R-055`, `R-056`).

---

# Implementation Tracking Table

| ID | Area | Status | Fix Commit | Verified in PROD | Notes |
|---|---|---|---|---|---|
| R-001 | Shell | OPEN | — | No | Tabs shift, Boquilhas confirmed example |
| R-057 | Global UI spacing | OPEN | — | No | Controls touch card/container edges |
| R-004 | Job On | OPEN | — | No | Operador editable while UI says Consulta |
| R-015 | Controlo | MISSING | — | No | No usable Job On selector |
| R-019 | Boquilhas/RE | SUPERSEDED | — | No | Duplicate repair ownership |
| R-024 | RI | OPEN | — | No | Lote echoes reference |
| R-027 | Reparação Externa | SUPERSEDED | — | No | Autonomous module to retire |
| R-029 | Armazém | OPEN | — | No | UI/IA requires rework |
| R-033 | Armazém | OPEN | — | No | Repairer not preserved |
| R-039 | Tampões | SUPERSEDED | — | No | Gerir linha invented |
| R-041 | Tampões | OPEN | — | No | Raw JSON in UI |
| R-044 | História | OPEN | — | No | Must be normal consultation area |
| R-046 | Admin/Settings | OPEN | — | No | Settings scattered across modules |

Update this table as repair batches are completed. Do not remove historical entries from this register; mark them `FIXED` and add the verified commit instead.
