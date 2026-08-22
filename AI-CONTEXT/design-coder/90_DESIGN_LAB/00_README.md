# DesignLaboratorio

## IMPLEMENTATION TASK

DES-017. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_SUPPORTING_COMPONENT_REVIEW.html`
2. `02_DESIGN_SYSTEM.md`
3. `03_IMPLEMENTATION_CONTRACT.md`
4. `90_DES_TASK.md`
5. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**02_DESIGN_SYSTEM.md and 03_IMPLEMENTATION_CONTRACT.md; 01_SUPPORTING_COMPONENT_REVIEW.html is supporting only**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `01_SUPPORTING_COMPONENT_REVIEW.html`
- `02_DESIGN_SYSTEM.md`
- `03_IMPLEMENTATION_CONTRACT.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml; shared CSS/JS

## TARGET PAGE ANATOMY

Non-domain demonstrations of every universal component/state, sticky/two-nav layouts, sidebars/drawers, responsive reflow and keyboard/failure states.

## CRITICAL LOCAL FUNCTIONAL RULES

The laboratory is a visual regression surface only and grants no capabilities.

## MUST PRESERVE

Token-only shared components; non-operational character.

## MUST NOT

Domain rules; authorization decisions; module-specific CSS; fake persisted success.

## DO NOT USE

- design-review.html as production module authority — it is copied only as supporting component review

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

