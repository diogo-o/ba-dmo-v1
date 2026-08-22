# Foundation

## IMPLEMENTATION TASK

DES-001. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_CANONICAL_DESIGN_SYSTEM.css`
2. `02_CANONICAL_INTERACTIONS.js`
3. `03_DESIGN_SYSTEM.md`
4. `04_IMPLEMENTATION_CONTRACT.md`
5. `90_DES_TASK.md`
6. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_CANONICAL_DESIGN_SYSTEM.css and 03_DESIGN_SYSTEM.md (the final plan prescribes no module HTML)**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `03_DESIGN_SYSTEM.md`
- `04_IMPLEMENTATION_CONTRACT.md`

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

Follow `91_ACCEPTANCE.md`.

