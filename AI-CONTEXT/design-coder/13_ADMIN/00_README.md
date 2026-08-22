# Admin

## IMPLEMENTATION TASK

DES-004. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_admin.html`
2. `02_HANDOFF_LOGIN_ADMIN.md`
3. `03_HANDOFF_GLOBAL_AUDIT.md`
4. `90_DES_TASK.md`
5. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_admin.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `02_HANDOFF_LOGIN_ADMIN.md`
- `03_HANDOFF_GLOBAL_AUDIT.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Admin\**\*.cshtml; wwwroot\styles\modules\admin-layout.css

## TARGET PAGE ANATOMY

Dedicated admin shell with Users, Templates, Applications and Audit; filters, compact tables, external actions, confirmations and focused detail.

## CRITICAL LOCAL FUNCTIONAL RULES

Authorization gates fail closed; reset never reveals a password; audit remains append-only.

## MUST PRESERVE

admin.gerir; templates/overrides; profile-title separation; idempotent creation; append-only audit.

## MUST NOT

Auth UUID labelled Email; current passwords; title equated with permission.

## DO NOT USE

- historical Admin route/card variants — superseded by the dedicated workspace

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

