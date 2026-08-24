# Login

## IMPLEMENTATION TASK

DES-003. See `12_LOGIN_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `12_LOGIN_01_VISUAL_AUTHORITY_login.html`
2. `12_LOGIN_02_HANDOFF_LOGIN_ADMIN.md`
3. `12_LOGIN_90_DES_TASK.md`
4. `12_LOGIN_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**12_LOGIN_01_VISUAL_AUTHORITY_login.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `12_LOGIN_02_HANDOFF_LOGIN_ADMIN.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Auth\Login.cshtml; shared login CSS/JS and PageModel

## TARGET PAGE ANATOMY

Desktop split composition; mobile stack; credentials form; password reveal; loading and generic error states.

## CRITICAL LOCAL FUNCTIONAL RULES

Operational users route to Job On; pure admin routes to Admin.

## MUST PRESERVE

Antiforgery; generic errors; no role choice; server routing; submit lock; credentials never repopulated.

## MUST NOT

Test-environment notice; test credentials; email-existence disclosure.

## DO NOT USE

- design-review.html — design-lab index, not Login authority

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `12_LOGIN_91_ACCEPTANCE.md`.

