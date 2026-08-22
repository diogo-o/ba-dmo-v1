# Controlo

## IMPLEMENTATION TASK

DES-006. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_controlo.html`
2. `02_HANDOFF_RESUMO_MCALIPER.md`
3. `03_HANDOFF_HISTORY.md`
4. `04_OWNER_DECISION_SHARED_DOCUMENTS.md`
5. `90_DES_TASK.md`
6. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_controlo.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `02_HANDOFF_RESUMO_MCALIPER.md`
- `03_HANDOFF_HISTORY.md`
- `04_OWNER_DECISION_SHARED_DOCUMENTS.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Controlo\*.cshtml; wwwroot\styles\modules\controlo-layout.css; wwwroot\scripts\controlo.js; Controlo services/lookups

## TARGET PAGE ANATOMY

One active-production card binding Resumo, Peso, Comparação, Pegamentos and Histórico; clear free-mode read-only consultation.

## CRITICAL LOCAL FUNCTIONAL RULES

Consume the user's exact current-open Job On. Every tab uses the same exact production/revision. Free mode remains available.

## MUST PRESERVE

Exact job_on_id + job_on_revision_id; snapshot components; append-only workflow/history; useful free mode.

## MUST NOT

Second production selector; second calendar; silent production selection; click-to-release context semantics.

## DO NOT USE

- module redirect-shell behavior — superseded by the bound workspace

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

