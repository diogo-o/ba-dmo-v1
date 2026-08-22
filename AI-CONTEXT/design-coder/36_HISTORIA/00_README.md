# História

## IMPLEMENTATION TASK

DES-016. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_historia.html`
2. `02_HANDOFF_HISTORY.md`
3. `90_DES_TASK.md`
4. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_historia.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `02_HANDOFF_HISTORY.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Historia\*.cshtml; Historia PageModel/service/repository

## TARGET PAGE ANATOMY

Compact filters and entity list; selected context and readable read-only timeline; before/after correction detail; pagination and states.

## CRITICAL LOCAL FUNCTIONAL RULES

História reads audit_events only and never writes. Visibility is filtered by authorized module intersection.

## MUST PRESERVE

Raw event immutability; module-intersection visibility; admin audit capability gate.

## MUST NOT

Writes; rankings; interpretations; technical-ID exposure; unauthorized events.

## DO NOT USE

- stacked technical-card composition — superseded by focused list/timeline

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

