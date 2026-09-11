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
- Boquilhas production-context associations while preserving individual BQ tracking;
- repairer configuration and usage;
- Armazém / repair responsibilities;
- removal of duplicated/superseded module responsibilities;
- central module settings direction where already decided;
- restoration of known functional behavior that regressed.

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
