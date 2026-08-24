# Admin

## IMPLEMENTATION TASK

DES-004. See `13_ADMIN_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `13_ADMIN_01_VISUAL_AUTHORITY_admin.html`
2. `13_ADMIN_02_HANDOFF_LOGIN_ADMIN.md`
3. `13_ADMIN_03_HANDOFF_GLOBAL_AUDIT.md`
4. `13_ADMIN_90_DES_TASK.md`
5. `13_ADMIN_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**13_ADMIN_01_VISUAL_AUTHORITY_admin.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `13_ADMIN_02_HANDOFF_LOGIN_ADMIN.md`
- `13_ADMIN_03_HANDOFF_GLOBAL_AUDIT.md`

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

Follow `13_ADMIN_91_ACCEPTANCE.md`.

