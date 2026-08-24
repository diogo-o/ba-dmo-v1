# Foundation

## IMPLEMENTATION TASK

DES-001. See `10_FOUNDATION_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_CANONICAL_DESIGN_SYSTEM.css`
2. `02_CANONICAL_INTERACTIONS.js`
3. `10_FOUNDATION_03_DESIGN_SYSTEM.md`
4. `10_FOUNDATION_04_IMPLEMENTATION_CONTRACT.md`
5. `10_FOUNDATION_90_DES_TASK.md`
6. `10_FOUNDATION_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_CANONICAL_DESIGN_SYSTEM.css and 10_FOUNDATION_03_DESIGN_SYSTEM.md (the final plan prescribes no module HTML)**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `10_FOUNDATION_03_DESIGN_SYSTEM.md`
- `10_FOUNDATION_04_IMPLEMENTATION_CONTRACT.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\wwwroot\styles\dmo-*.css; shared scripts

## TARGET PAGE ANATOMY

Tokens, shared buttons/fields/cards, lists/tables, calendar, dialogs, states, focus/keyboard behavior, sticky layers and responsive primitives.

## CRITICAL LOCAL FUNCTIONAL RULES

Components contain no domain logic. Server authorization remains authoritative.

## MUST PRESERVE

Existing class aliases during migration; canonical event contracts; component neutrality.

## MUST NOT

Module business logic in CSS/JS; colour-only meaning; page-wide horizontal scrolling.

## DO NOT USE

- integrated-mockup.css — demo integration asset, not cited by DES-001
- integrated-mockup.js — demo behavior, not cited by DES-001

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `10_FOUNDATION_91_ACCEPTANCE.md`.

