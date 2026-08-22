# Armazém

## IMPLEMENTATION TASK

DES-012. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_armazem.html`
2. `02_BRIEF_ARMAZEM.md`
3. `03_OWNER_DECISION_SAP_ALERT.md`
4. `90_DES_TASK.md`
5. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_armazem.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `02_BRIEF_ARMAZEM.md`
- `03_OWNER_DECISION_SAP_ALERT.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Armazem\*.cshtml; wwwroot\styles\modules\armazem-layout.css; wwwroot\scripts\armazem.js; services

## TARGET PAGE ANATOMY

Inline CM/MF entry/exit; search; utilisation-aware operational alerts; consultation/correction; calendar history.

## CRITICAL LOCAL FUNCTIONAL RULES

Stored manual SAP utilisation may be consumed to display the appropriate alert when a tool enters storage. Armazém does not calculate utilisation.

## MUST PRESERVE

Warehouse ownership; read-only tool identity; 4-digit positions; explicit persistence; 1:1 occupation; prior movements.

## MUST NOT

Silent normalization/replacement; direct tool-domain edits; BQ or programmed external-flow activation; utilisation calculation.

## DO NOT USE

- normal Substituir action — forbidden
- programmed external-repair and BQ mockup behavior — out of current U-14 scope

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

