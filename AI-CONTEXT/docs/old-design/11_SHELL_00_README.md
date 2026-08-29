# Shell

## IMPLEMENTATION TASK

DES-002. See `11_SHELL_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `11_SHELL_01_HANDOFF_LOGIN_ADMIN.md`
2. `11_SHELL_02_HANDOFF_GLOBAL_AUDIT.md`
3. `11_SHELL_90_DES_TASK.md`
4. `11_SHELL_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**Global design system plus canonical operational headers; the final plan prescribes no single Shell HTML**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `11_SHELL_01_HANDOFF_LOGIN_ADMIN.md`
- `11_SHELL_02_HANDOFF_GLOBAL_AUDIT.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Shared\_Layout.cshtml, _Header.cshtml, _Navigation.cshtml, _AdminNav.cshtml; shared layout CSS

## TARGET PAGE ANATOMY

Logo and page identity, authenticated identity/account control, primary navigation, optional secondary navigation, sticky stacking and responsive overflow.

## CRITICAL LOCAL FUNCTIONAL RULES

Navigation is derived server-side from capabilities. Admin-pure shell remains isolated.

### GLOBAL SHELL FREEZE

Module visual-authority designs do not override the BA DMO global header,
primary navigation, or user/profile shell unless explicitly requested by the owner.

A module owns only its own secondary navigation and its workspace/content. A module's
canonical HTML/CSS may not change the global application header, the primary module
navigation, or the global user/profile presentation — neither by moving, restyling, or
re-rendering those elements, nor by loading module CSS that overrides their shared
classes (e.g. `.app-header`, `.dmo-app-header*`, `.dmo-primary-nav`, `.app-nav`).
Pages that opt out of the normal shell must be explicitly requested by the owner.

## MUST PRESERVE

Capability-derived navigation; operational Job On landing; pure-admin isolation; fail-closed gates.

## MUST NOT

Client-visible navigation as authorization; profile title as permission; hidden or overlapping navigation.

## DO NOT USE

- admin.html — Admin module composition, not the global Shell primary authority
- login.html — Login page composition, not the global Shell primary authority

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `11_SHELL_91_ACCEPTANCE.md`.

