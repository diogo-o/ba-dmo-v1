# BA-DMO — 4-Phase Implementation Roadmap

> **Purpose**
>
> Keep the project anchored around the four major implementation phases so individual prompts, agents, fixes and reports do not cause the overall direction to drift.
>
> This file is a high-level execution roadmap. It does not replace the detailed authority files for each phase.

## Current phase

**M1 — Functional and structural corrections**

We are currently in **M1**.

The priority in M1 is to correct the product model and the important functional/structural relationships before rebuilding access/security architecture or polishing final design.

---

# M1 — Functional and structural corrections

## Goal

Correct the parts of the current application whose underlying functional model is wrong, incomplete, duplicated or too tightly coupled.

This phase is about **what the application means and how the main operational data relates**.

It is not the final design pass and it is not the final access-system rebuild.

## Main work in M1

Examples of M1 work include:

- Job On as the production context and tool-association hub;
- explicit production ↔ tool relationships;
- tool identity and tool history;
- Controlo ownership split;
- Peso belonging to the CM that was actually weighed;
- Pegamentos belonging to the production / Job On tool combination;
- Resumo do Controlo belonging to the production / Job On when it spans several tools;
- Reparação Interna linked to the exact CM/MF used in production;
- RI preserving the **individual number / individual identity** of each repaired CM/MF piece;
- Boquilhas production-context associations using the same stable tool identity principle, while preserving their quantity-based repair trace;
- repairer configuration and usage;
- Armazém / repair responsibilities;
- removal of duplicated/superseded module responsibilities;
- central module settings direction where already decided;
- restoration of known functional behavior that regressed.

## M1 tool identity / warehouse resolution rule

A major M1 correction is that tools must stop being related across modules by repeatedly matching several visible fields.

The authoritative direction is:

> **The user identifies a tool through familiar industrial attributes; the system resolves and stores one stable invisible `tool_id`.**

The `tool_id` is the persistent internal identity of the tool/ficha. It must not be generated from a mutable concatenation/hash of type + reference + lot + machine. It should be a stable database identity such as a UUID.

Typical visible attributes remain:

- tool type/family;
- reference;
- lot;
- machine(s)/line(s);
- classification/context where applicable.

These values are used to search, filter and confirm the correct tool. They are **not** the cross-module relationship key.

Conceptually:

```text
Tool
- tool_id       <- stable, internal, invisible
- type
- reference
- lot
- machines/lines [0..N]
- other real tool-owned attributes
```

The same `tool_id` should then be referenced by the relevant operational records:

```text
Tool / tool_id
├── Job On / ProductionToolUsage
├── Peso                         (CM)
├── Reparação Interna            (CM/MF + individual number)
├── Armazém movements
├── external repair movements
├── Boquilhas quantity movements
├── História
└── other tool-specific events
```

This avoids fragile relationships based on repeating four fields in every module.

### Where the tool identity is created

The future **Armazém → Ferramentas** operational surface is the natural place where a tool/ficha is created and receives its stable `tool_id`.

Example:

```text
Create tool/ficha
Type: CM
Reference: 5447
Lot: 3
Machines/Lines: B1, C3

Database assigns:
tool_id = <stable UUID>
```

From that point onward, other modules reference this identity rather than recreating the tool from text fields.

Job On should select an existing tool/ficha and persist the resolved `tool_id` in its production/revision association.

### Armazém lookup before movements

For warehouse Entrada, Saída, repair dispatch/return, and general tool lookup, the operator should search using the information they know, for example:

```text
Type + Reference + Lot + Machine/Line context
```

The application filters existing registered tools/fichas. The operator confirms the correct visible result, and the movement then stores that ficha's invisible `tool_id`.

This gives confidence that the correct tool identity is being used without exposing UUIDs to users.

If no matching registered ficha exists, the application must **not invent a tool identity or silently create a guessed association**. Use the existing missing-registration alert/handling behavior rather than building a second alert mechanism.

### Armazém current-state / trace presentation

Once movements are linked to `tool_id`, an Armazém lookup can show the known current logistical state of that exact tool, for example:

- Em armazém + storage position;
- Em produção + production/line context;
- Em reparação + repairer + dispatch date;
- return/entry information;
- last known movement.

If the movement history is insufficient to establish current location/state, show it as unknown/unconfirmed rather than guessing.

Do not create a second unrelated `current_location` truth if current state can be reliably projected from authoritative movements. A cached/projection value is acceptable for performance, but movement history remains the traceable source.

### CM / MF repair granularity

For CM/MF, stable tool identity does **not** replace the individual piece number.

Reparação Interna must preserve both:

```text
- tool_id
- individual_number
```

`tool_id` identifies the real tool/ficha/lot context; `individual_number` identifies the specific CM/MF piece repaired.

If pieces 3, 7 and 12 are repaired, they remain three individual repair events even if they share the same `tool_id`, production and lot.

### Boquilhas identity and quantity trace

Boquilhas uses the same stable `tool_id` principle as CM/MF for its registered reference/lot/tool ficha.

However, Boquilhas repair trace has different granularity:

- **do not create one persistent identity per physical BQ piece;**
- BQ movements are quantity-based;
- the BQ tool/ficha is associated to Job On using the stable `tool_id`;
- reference, lot and machine/line remain visible attributes/context;
- repair movements record quantities and repairer.

Conceptually:

```text
BQ Tool/Ficha
- tool_id
- reference
- lot
- machines/lines

BQ Repair Movement
- tool_id
- repairer_id
- production/jobon context when applicable
- quantity_out
- quantity_in
- dates
- notes/status/audit
```

The trace must preserve how many BQs were sent to and returned from each repairer.

The actual returned quantity is recorded even when it differs from the expected/sent quantity. Do not force an artificial balance or reject a real quantity merely because it is greater or lower than expected. The discrepancy belongs in history.

### Repairer relation

Repair movements should reference the canonical configured repairer identity (`repairer_id` or equivalent), not duplicate the repairer name as independent master data.

For CM/MF, the repairer is selected for each external-repair movement.

For Boquilhas, one canonical repairer may be configured for zero, one or multiple BQ lines/machines. Do not duplicate a repairer record per line.

### Why this matters for implementation/debugging

This identity model is also a diagnostic guide for DeepSeek and later implementation agents.

When a change produces an error, inspect the chain explicitly instead of patching text comparisons:

```text
Tool/Ficha created?
   ↓
Correct stable tool_id resolved?
   ↓
Correct Job On revision / ProductionToolUsage linked?
   ↓
Correct operational record stores tool_id?
   ↓
Read/history resolves visible reference/lot/machine from that identity?
```

Typical failures should be classified by the broken relation:

- tool/ficha does not exist;
- lookup cannot resolve the intended existing tool;
- wrong `tool_id` selected;
- production/revision does not contain that `tool_id`;
- movement/control/repair failed to persist the `tool_id`;
- read path is still using copied text instead of the real tool relation;
- historical record points to the wrong revision/context.

Do **not** fix these failures by reintroducing independent joins on type + reference + lot + machine as the primary identity mechanism. Those fields may help locate/validate the tool, but the persisted relationship remains the stable `tool_id`.

## Main authority for current M1 work

Read and follow, especially:

- `reports/BA_DMO_TOOL_PRODUCTION_CONTEXT_PLAN.md`
- `reports/BA_DMO_RECOVERY_REGISTER.md`
- `reports/BA_DMO_ADMIN_SETTINGS_REPAIRERS.md`

Other Manual/Maps/current LIVE information can be consulted as evidence, but newer explicit owner decisions override stale implementation, old design or historical tests.

## M1 completion condition

M1 is ready to close when the important functional relationships are stable enough that the next access-system rebuild can be based on them without reproducing known wrong architecture.

Do not require every final visual detail to be solved before leaving M1.

---

# M2 — Rebuild the access / authority / user system from LIVE

## Goal

Create a new access and authority architecture instead of continuing to patch the inherited one.

This phase should be implemented as a **reset/rebuild of the access system**, using the corrected LIVE application as the behavioral reference.

The old access implementation is reference material only. It must not automatically become the new design.

## Core M2 direction

M2 must clearly separate:

- authentication / account identity;
- access templates;
- capabilities / permissions;
- frontend modules;
- pages/workflows;
- Operator vs Responsável experience;
- Admin authority;
- navigation generation;
- server-side authorization.

## Operator and Responsável

Do not treat Operador and Responsável as merely two labels over one giant page when their real workflows differ.

M2 must allow three valid patterns depending on the module:

1. **same page, different actions**
   - example: Job On consultation vs management actions;

2. **same module, different workflow/page**
   - example: Peso measurement vs approval/review;

3. **different accessible pages/modules based on permissions**
   - where the template genuinely grants or denies access.

Page/layout/workflow differences should be modeled deliberately per module instead of being accidental CSS/JavaScript state.

## Permission model

Use **capabilities as the real security authority**.

The access template composes which capabilities/pages/modules a user receives.

Avoid relying on hardcoded global role checks such as only `if Operador` / `if Responsável` as the authorization source.

A template should be able to define, for example:

- accessible frontend modules;
- accessible pages/workflows inside each module;
- capabilities/actions;
- navigation order/default page where useful;
- module-specific workflow defaults where useful.

## Accounts and templates

The new system must support:

```text
Account/User
    ↓
Effective Access Template
    ↓
Modules + Pages/Workflows + Capabilities
```

Admin must be able to create/edit templates and associate accounts with the correct template.

Do not introduce per-user permission overrides unless a later explicit owner decision requires them.

## New catalogs/contracts

M2 should establish clean equivalents of:

- MODULE_CATALOG
- PAGE_CATALOG
- CAPABILITY_CATALOG

with clear responsibilities:

- **MODULE** = frontend grouping/navigation area;
- **PAGE / WORKFLOW** = user-facing operational experience;
- **CAPABILITY** = authorization to perform an action.

Backend/domain modules do not have to map 1:1 to frontend navigation modules.

## Implementation method — reset, do not copy the old access stack

M2 should be built in a separate clean worktree/folder/branch or equivalent isolated implementation area.

Create the new access layer deliberately, including new:

- schema/migrations;
- Dapper repositories;
- queries;
- contracts;
- catalogs;
- authorization policies;
- navigation builder;
- workflow/page resolver;
- tests.

Do **not** simply copy the old access schema, Dappers, tests and gates into a new folder.

The current LIVE application is the reference for required behavior, not the inherited access implementation.

## M2 completion condition

M2 is complete when access is predictable and testable for at least:

- Operador;
- Responsável;
- Admin;
- a custom access template;
- direct URL authorization;
- module visibility;
- page/workflow visibility;
- capability/action enforcement;
- server-side protection independent of hidden buttons/CSS.

---

# M3 — Final product design / UX consolidation

## Goal

Once the functional model and access architecture are stable, perform the proper final design pass.

This is where the application becomes visually coherent and deliberately shaped around the real workflows rather than around historical implementation constraints.

## Main M3 work

Examples:

- global shell consistency;
- fixed/pinned header and navigation geometry;
- Operator/Responsável page layouts where their workflows differ;
- shared sidepanel chassis;
- module-specific content layouts;
- card density and spacing;
- typography;
- action hierarchy;
- responsive behavior;
- removal of visual remnants from old implementations;
- consistent states/loading/empty/error presentation;
- making the app compact without becoming cramped.

M3 must follow the functional and access architecture produced by M1/M2. Design must not reinterpret industrial meaning.

## M3 completion condition

The main workflows should be visually coherent, stable, easy to navigate and consistent across modules, with the final intended Operator/Responsável/Admin presentation.

---

# M4 — Final CLEAN reset if accumulated legacy/waste is still excessive

## Goal

After M1–M3, assess whether the repository still contains too much transitional code, duplicated architecture, dead migrations, old tests, abandoned services or compatibility layers.

If the amount of residual waste is still high, perform a final **LIVE → CLEAN rebuild/reset**.

M4 is conditional: it is required only if keeping the repaired repository would leave significant long-term technical debt or confusing competing architecture.

## CLEAN rule

The CLEAN version should be recreated from the now-stable LIVE application and current authority.

Core principle:

> **No positive LIVE evidence, no entry into CLEAN.**

Legacy code may help explain how something currently works, but legacy must not define the CLEAN product merely because it exists.

## What should be rebuilt cleanly if M4 is needed

Depending on the final state, CLEAN may recreate:

- schema/migrations;
- repositories/Dappers;
- queries;
- application contracts;
- tests;
- authorization/access layer;
- pages/layouts;
- document generation paths;
- module boundaries.

Only preserve concepts that are still authoritative after M1–M3.

Do not carry forward old objects just to maintain historical code continuity.

## M4 completion condition

The final repository has one clear architecture, one clear authority model and no major duplicated/superseded implementation paths competing with the current product.

---

# Phase order

```text
M1  Functional + structural corrections
 ↓
M2  New access / authority / user system (reset from LIVE)
 ↓
M3  Final design / UX consolidation
 ↓
M4  Optional final CLEAN reset if too much legacy/waste remains
```

Do not invert the order casually.

In particular:

- do not finalize design before the underlying workflows/access model are stable;
- do not rebuild access around product relationships that are still known to be wrong;
- do not perform the final CLEAN reset before LIVE behavior is sufficiently trustworthy to serve as its reference.

---

# Working rule for prompts and agents

Every implementation prompt should identify which milestone it belongs to.

Agents should not silently expand a task into another milestone.

Examples:

- a Tool/Production relationship task = M1;
- a new template/capability/page authorization architecture = M2;
- visual shell/layout cleanup after workflows are stable = M3;
- greenfield final repo reconstruction from stabilized LIVE = M4.

When in doubt, preserve the current milestone boundary and ask for an explicit owner decision rather than mixing phases.
