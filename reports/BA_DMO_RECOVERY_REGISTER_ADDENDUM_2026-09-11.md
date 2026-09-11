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

---

## Priority

Treat `R-058`–`R-062` as part of the current recovery backlog. `R-058`, `R-059`, `R-060` and `R-062` are correctness/regression items; `R-061` is a UX sizing defect.
