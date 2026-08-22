# Pegamentos

## IMPLEMENTATION TASK

DES-009. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_pegamentos.html`
2. `02_HANDOFF_PEGAMENTOS.md`
3. `03_DATA_CONTRACT_SNAPSHOT.json`
4. `90_DES_TASK.md`
5. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_pegamentos.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `02_HANDOFF_PEGAMENTOS.md`
- `03_DATA_CONTRACT_SNAPSHOT.json`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Pegamentos\*.cshtml; wwwroot\styles\modules\pegamentos-layout.css; wwwroot\scripts\pegamentos.js; services/PDF

## TARGET PAGE ANATOMY

Active Job On context; inherited tool summary; measurement entry/results; structured history; immutable document state.

## CRITICAL LOCAL FUNCTIONAL RULES

Exact inherited Job On revision. No manual re-selection of inherited CM/BQ/MF.

## MUST PRESERVE

Exact inherited revision and CM/BQ/MF; ±0.20 boundary; server calculations; one persisted final PDF.

## MUST NOT

Manual revision IDs; inherited-tool reselection; client calculation; silent PDF replacement; redundant open button.

## DO NOT USE

- local-browser-only document behavior — demo-only and conflicts with server snapshot authority

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

