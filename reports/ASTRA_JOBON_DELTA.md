# ASTRA — Job On Delta

Verification date: 2026-09-08  
Repository: `diogo-o/ba-dmo-v1`, branch `main`

## 1. Current state

- Implementation baseline inspected: `0e5ee0a943f1ad2f957ebae1fd503077026f749d`
- Baseline message: `Fix Job On new tool component revision binding`.
- Astra handoff/delta commit: `e61875e345d15bdff8d5cfdb20f7873644472171`.
- The implementation baseline above is not the current repository tip after the delta file was committed.
- The remote repository contains the Job On implementation and context files.
- No local checkout exists in the current workspace, therefore local uncommitted changes cannot be inspected or classified. Astra MUST inspect the actual working tree before modifying Job On. Recent lifecycle work may exist uncommitted locally. Do not treat this delta as proof that remote GitHub contains all current Job On work, and do not infer that the implementation baseline is the user's complete working tree.

Recent Job On commits at/behind HEAD include:

- real duplicate flow;
- alter-date revision flow;
- tool association picker;
- verification confirmation;
- new component revision binding.

## 2. Existing context status

- Manual and Maps Job On material: **still authoritative**. Use for functional/domain rules.
- `AI-CONTEXT/docs/BA_DMO_CODEX_HANDOFF.md`: **partially stale**. Its “verify first / if still missing” Job On items must be reclassified against the current implementation below; its ownership, immutable-revision, snapshot, and “design cannot remove content” rules remain valid.
- `AI-CONTEXT/docs/design-bundle/00_OLD_DESIGN_ALL.md` and its index: **still accurate as visual/reference material**, not as permission to remove functional fields.
- Existing repository reports: **partially stale where they describe Job On work as pending**. The current source now contains create, duplicate, alter-date, revision-save, tool association, and verification code. No separate current Job On report was found in `reports/`; this file is the current delta.

## 3. Confirmed current functionality

Directly verified in current source:

- Create: `POST /api/jobon`; service creates header plus initial immutable revision atomically.
- Duplicate: `POST /api/jobon/{id}/duplicate`; copies the source setup/component graph into a new Job On and regenerates pending verifications.
- Alter dates: `POST /api/jobon/{id}/date`; same Job On, new immutable revision.
- Save revision: `POST /api/jobon/{id}/revision`; complete submitted component graph is persisted as a new revision.
- Component association: tool-options lookup and explicit CM/MF/BQ association; save-time validation checks registered tool/lot identity.
- Verification confirmation: `POST /api/jobon/{id}/verifications/{occurrenceId}/confirm`; confirmation is server-persisted and the page reloads persisted status.
- Lifecycle: domain/service/repository support `rascunho → planeado → em fabrico → fechado` and cancellation handling.
- Hydration: repository loads Job On revisions plus component, field, row, and verification children.
- History/revisions: revision count/current revision and immutable revision graph are present in the domain and page model.
- Printing: the UI calls `POST /api/jobon/{id}/document`; a PDF renderer/service and four-page print authority exist in the repository.

## 4. Confirmed current gaps/regressions

- The current sheet exposes many summary values as inputs/selects, but the save script only explicitly wires:
  - dates through the dedicated alter-date flow;
  - general notes;
  - component cards, fields, rows, and verification graph.
- The save script explicitly treats production/machine/dates as header/context and does not submit them in the revision payload. This is correct for the separate date flow, but means Astra must verify every visible editable summary control before allowing edit mode to imply persistence.
- The current page has visible summary controls for reference, production, sections, drop count, type, stop, weight, and process. Their persistence/materialization must be tested against the authoritative model; do not assume that being rendered as an input means they save.
- The current UI materializes the canonical card set (CM, BQ, AN, MF, PU, ARR, CAL, PI, CS, TP, FO), but the old four-page print authority contains richer per-tool rows, quantities, lot data, observations, and team/document content. The current sheet/renderer must be checked for complete materialization of that content. Design simplification must not remove it.
- The current print button is labelled “Imprimir 4 folhas”, but source verification here confirms the request path and renderer existence, not that all four current pages contain all required live values. Treat print completeness as an outstanding acceptance gap.
- Initial verification occurrence generation for a newly associated tool/lot remains **unresolved**. Existing verification confirmation is implemented, and duplication can regenerate pending occurrences from existing source occurrences. That does not prove that the new CM/MF/BQ association path loads active Ferramentas verification rules and creates the initial Job On verification occurrences. Astra must verify this save/association path end-to-end; do not implement it from this delta.
- No direct browser/runtime interaction was possible in this verification session; reachability and round-trip behavior still require targeted execution tests.

### Central UI regression

The current Job On visual sheet must **not** be treated as the functional specification. During the design port, a previously more complete Job On presentation/content was simplified and significant information appears to have been lost from the visible UI.

Astra must compare the last functionally complete Job On presentation against the current implementation, identify disappeared fields/content/controls, and restore missing functional content while preserving the current backend, revision, tool-association, and verification functionality. Functional content must never be removed merely to match the current visual design.

Do not perform that comparison as part of this delta.

## 5. Supabase PROD reality

BA-DMO-PROD is still the main environment being completed. It is not yet in operational use. DEV must not become the target merely because it exists. Astra should work against the established PROD reality unless the owner explicitly changes this. Do not alter PROD now.

BA-DMO-PROD is active/healthy (project ref `bddfhbyrmchktqotpzgb`). Read-only inspection confirmed these tables exist:

- `job_on`
- `job_on_revision`
- `job_on_component`
- `job_on_component_field`
- `job_on_component_row`
- `job_on_field_option`
- `job_on_verification_occurrence`
- `job_on_audit_event`
- `jobon_user_current`

The component/revision/verification schema is present. Current row counts are zero for all inspected Job On tables, including `job_on`, revisions, components, verifications, and audit events. No production Job On data currently exists to validate with.

## 6. Astra starting point

Astra must begin in this order:

A. Inspect the actual working tree and all uncommitted Job On work.
B. Read this delta plus the existing authoritative Manual, Maps, and AI-CONTEXT material.
C. Verify only the stale or unresolved points identified here.
D. Finish Job On end-to-end, including functional content, round-trips, and print output.
E. Do not redesign until functional completeness is proven.

A field is not implemented merely because it renders as an input/select. For every editable field, prove the complete chain:

`UI → request payload → service request → repository persistence → reload/hydration → print output where applicable`

If any link is missing, classify the field as incomplete.

Start at `src/BA.Dmo.Web/Pages/JobOn/Index.cshtml`, `Index.cshtml.cs`, `wwwroot/scripts/jobon.js`, `Application/Modules/JobOn/JobOnService.cs`, and `Infrastructure/Access/DapperJobOnRepository.cs`.

First run targeted round-trip checks on the current UI. Map every visible/editable field to its request payload, service request, repository insert, reload, and four print pages. Restore missing content/materialization only where confirmed; preserve the existing service/revision/schema behavior and do not redesign the sheet.
