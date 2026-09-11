# BA-DMO — Recovery Register Addendum — 2026-09-11

> This addendum records owner-confirmed recovery items discovered after the initial `BA_DMO_RECOVERY_REGISTER.md` was created. These items are authoritative current owner decisions and must be merged into the master recovery register before implementation batches are closed.

## R-058 — Top-level module naming is wrong
**Status:** `OPEN / REGRESSION / UX`

The primary module navigation currently labels the production-planning module as `Job On`.

Correct owner rule:
- top-level module = `Planeamento`;
- `Job On` = the individual production sheet opened from Planeamento;
- do not use `Job On` as the top-level module/action label.

## R-059 — Controlo incorrectly appears as a Planeamento sub-tab
**Status:** `OPEN / REGRESSION / UX`

`Controlo` is a separate top-level module and must not appear as an internal tab/view of Planeamento.

Planeamento may link to Controlo contextually where authorized, but that does not make Controlo part of Planeamento navigation.

## R-060 — Job On machine/line appears hardcoded or non-editable
**Status:** `OPEN / REGRESSION / FUNCTIONAL`

The current Job On presentation suggests the machine/line value is hardcoded or cannot be altered.

Correct owner rule:
- machine/line is user-entered/editable Job On data for Responsável while editing;
- the initial/current value may come from existing production context;
- machine/line associations may help filtering/search;
- they must not make the field immutable or become inferred industrial enforcement.

Verify the real edit/save path and remove any hardcoded/non-editable behavior.

## R-061 — Production selector is oversized
**Status:** `OPEN / UX`

The Job On production dropdown occupies substantially more width than its content requires.

Production number is six digits plus a short state/context label such as `atual`.

Required direction:
- make the selector compact and content-appropriate;
- preserve enough width for the expected production label without truncation;
- do not waste header space needed by other Job On information;
- this is part of the wider Job On content-driven sizing pass.

## R-062 — BQ Job On fields `Stock` / `Necessárias` are unauthorized and unused
**Status:** `OPEN / REGRESSION / REMOVE`

The Job On BQ block currently exposes fields/concepts such as `Stock` and/or `Necessárias` that are not used in the real workflow and were not requested by the owner.

Correct owner rule:
- remove these fields from the Job On BQ UI unless an authoritative Manual/current owner rule proves a real use;
- do not invent stock/required-quantity logic for BQ in Job On;
- Boquilhas has its own real tracking flow and discrepancy behavior;
- Job On should show only the BQ information actually required for production context.

Before removal, trace whether these labels are presentation-only or backed by stale DTO/service/schema/test assumptions. Remove stale UI/logic safely rather than merely hiding labels if unused code was introduced around them.

## R-063 — CM/MF repairer should prefill from latest repair record
**Status:** `MISSING / AUTHORITY`

For CM and MF shown in Job On, the `Reparador` field should reuse the latest known repair context for that exact tool/lot instead of requiring the user to repeatedly type information the system already has.

Correct owner rule:
- resolve the latest repair record associated with the exact CM/MF tool context;
- prefill `Reparador` from that latest repair record when one exists;
- if there is no prior repair record, leave the field empty;
- this is contextual prefill, not an industrial validity rule;
- Responsável may alter the value while editing Job On;
- do not hardcode or invent a default repairer;
- historical repairer information must never block a legitimate current selection.

Use the canonical persisted tool identity/relationship rather than matching only on a loose reference string. Preserve the distinction between CM/MF external repair history and Reparação Interna records as defined by their respective flows.

## R-064 — Job On `...` dropdown renders behind the header
**Status:** `OPEN / UX / REGRESSION`

The Job On secondary `...` menu opens, but its dropdown/content is rendered behind the global header and becomes partially or fully invisible.

Required direction:
- overlay menus must render above the global shell/header;
- opening a menu must not move or resize the page;
- do not fix this with arbitrary page-specific offsets;
- establish a consistent overlay/z-index layer in the shared design system for menus, popovers and dialogs;
- ensure the menu is not clipped by parent `overflow` rules;
- preserve keyboard/focus behavior and click-outside dismissal;
- verify the same overlay behavior anywhere the shared `...` menu pattern is reused.

This is a shell/overlay-layer defect, not a Job On-specific visual patch.

## R-065 — PU Job On fields `% uso` / `Quantidade em máquina` are unauthorized and invented
**Status:** `OPEN / REGRESSION / REMOVE`

The Job On PU block currently exposes fields/concepts such as `% uso` and `Quantidade em máquina` that are not part of the owner-authorized workflow and were not requested.

Correct owner rule:
- remove `% uso` from the Job On PU block unless an authoritative current Manual/owner rule proves a real operational use;
- remove `Quantidade em máquina` from the Job On PU block unless an authoritative current Manual/owner rule proves a real operational use;
- do not invent utilization percentages, machine quantities or derived operational logic for PU;
- Job On should show only the PU information actually required for the production sheet/context;
- do not preserve invented fields merely because they already exist in UI/tests/DTOs.

Before removal, trace whether these are presentation-only fields or whether stale DTO/service/schema/test assumptions were introduced around them. Remove unsupported logic safely rather than hiding labels while leaving dead behavior underneath.

---

## Priority

Treat `R-058`–`R-065` as part of the current recovery backlog. `R-058`, `R-059`, `R-060`, `R-062`, `R-064` and `R-065` are correctness/regression items; `R-061` is a UX sizing defect; `R-063` is an owner-authorized contextual data reuse requirement.
