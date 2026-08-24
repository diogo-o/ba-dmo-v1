# DesignLaboratorio

## IMPLEMENTATION TASK

DES-017. See `90_DESIGN_LAB_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `90_DESIGN_LAB_01_SUPPORTING_COMPONENT_REVIEW.html`
2. `90_DESIGN_LAB_02_DESIGN_SYSTEM.md`
3. `90_DESIGN_LAB_03_IMPLEMENTATION_CONTRACT.md`
4. `90_DESIGN_LAB_90_DES_TASK.md`
5. `90_DESIGN_LAB_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**90_DESIGN_LAB_02_DESIGN_SYSTEM.md and 90_DESIGN_LAB_03_IMPLEMENTATION_CONTRACT.md; 90_DESIGN_LAB_01_SUPPORTING_COMPONENT_REVIEW.html is supporting only**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `90_DESIGN_LAB_01_SUPPORTING_COMPONENT_REVIEW.html`
- `90_DESIGN_LAB_02_DESIGN_SYSTEM.md`
- `90_DESIGN_LAB_03_IMPLEMENTATION_CONTRACT.md`

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

Follow `90_DESIGN_LAB_91_ACCEPTANCE.md`.

