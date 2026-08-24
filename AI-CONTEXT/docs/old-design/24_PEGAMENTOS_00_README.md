# Pegamentos

## IMPLEMENTATION TASK

DES-009. See `24_PEGAMENTOS_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `24_PEGAMENTOS_01_VISUAL_AUTHORITY_pegamentos.html`
2. `24_PEGAMENTOS_02_HANDOFF_PEGAMENTOS.md`
3. `24_PEGAMENTOS_03_DATA_CONTRACT_SNAPSHOT.json`
4. `24_PEGAMENTOS_90_DES_TASK.md`
5. `24_PEGAMENTOS_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**24_PEGAMENTOS_01_VISUAL_AUTHORITY_pegamentos.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `24_PEGAMENTOS_02_HANDOFF_PEGAMENTOS.md`
- `24_PEGAMENTOS_03_DATA_CONTRACT_SNAPSHOT.json`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Pegamentos\*.cshtml; wwwroot\styles\modules\pegamentos-layout.css; wwwroot\scripts\pegamentos.js; services/PDF

## TARGET PAGE ANATOMY

Active Job On context; inherited tool summary; measurement entry/results; structured history; immutable document state.

## CRITICAL LOCAL FUNCTIONAL RULES

Exact inherited Job On revision. No manual re-selection of inherited CM/BQ/MF.

**Production context (no independent reselection):** Pegamentos operates inside the **Controlo production workspace** using the **same exact Job On / revision**. It **inherits the exact CM/MF/BQ** from Job On and does **not** independently reselect those tools where forbidden; the inherited tooling/revision is authoritative.

## MUST PRESERVE

Exact inherited revision and CM/BQ/MF; ±0.20 boundary; server calculations; one persisted final PDF.

## MUST NOT

Manual revision IDs; inherited-tool reselection; client calculation; silent PDF replacement; redundant open button.

## DO NOT USE

- local-browser-only document behavior — demo-only and conflicts with server snapshot authority

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `24_PEGAMENTOS_91_ACCEPTANCE.md`.

