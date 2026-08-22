# Ferramentas

## IMPLEMENTATION TASK

DES-010. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_ferramentas.html`
2. `02_BRIEF_REGISTRATION.md`
3. `03_OWNER_DECISION_SAP_UTILISATION.md`
4. `90_DES_TASK.md`
5. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_ferramentas.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `02_BRIEF_REGISTRATION.md`
- `03_OWNER_DECISION_SAP_UTILISATION.md`
- The `Verificações` tab behavior is defined in `../20_JOB_ON/05_BRIEF_VERIFICATIONS.md` (the in-package verification contract). Do not search outside `AI-CONTEXT/design-coder` for it.

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Ferramentas\*.cshtml; wwwroot\styles\modules\ferramentas-layout.css; wwwroot\scripts\ferramentas.js; services

## TARGET PAGE ANATOMY

Compact reference list and focused tabs: Referência, Lotes, Verificações, Utilização and Histórico; creation and controlled duplication.

## CRITICAL LOCAL FUNCTIONAL RULES

Activate Utilização in this test version. User manually enters the percentage read from SAP. The application never calculates it; future SAP automation is out of scope.

## MUST PRESERVE

Separate CM/MF identities; stable IDs; append-only verification and SAP utilisation; master-vs-lote ownership.

## MUST NOT

CM/MF merge; inferred drawing codes; copied checks/history; warehouse identity; calculated utilisation.

## DO NOT USE

- older split large CM/MF page compositions — superseded by focused list/detail workspace

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

