# Controlo

## IMPLEMENTATION TASK

DES-006. See `21_CONTROLO_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `21_CONTROLO_01_VISUAL_AUTHORITY_controlo.html`
2. `21_CONTROLO_02_HANDOFF_RESUMO_MCALIPER.md`
3. `21_CONTROLO_03_HANDOFF_HISTORY.md`
4. `21_CONTROLO_04_OWNER_DECISION_SHARED_DOCUMENTS.md`
5. `21_CONTROLO_90_DES_TASK.md`
6. `21_CONTROLO_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**21_CONTROLO_01_VISUAL_AUTHORITY_controlo.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `21_CONTROLO_02_HANDOFF_RESUMO_MCALIPER.md`
- `21_CONTROLO_03_HANDOFF_HISTORY.md`
- `21_CONTROLO_04_OWNER_DECISION_SHARED_DOCUMENTS.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Controlo\*.cshtml; wwwroot\styles\modules\controlo-layout.css; wwwroot\scripts\controlo.js; Controlo services/lookups

## TARGET PAGE ANATOMY

One active-production card binding Resumo, Peso, Comparação, Pegamentos and Histórico; clear free-mode read-only consultation.

## CRITICAL LOCAL FUNCTIONAL RULES

Consume the user's exact current-open Job On. Every tab uses the same exact production/revision. Free mode remains available.

**Boundary with Job On (do not duplicate):** Controlo is an **independent functional module** that associates its records with a Job On/production context. Job On owns the **planned production tooling configuration**; Controlo does **not** redefine it. Two legitimate entry cases are defined:
- **Case A — control of the Job On/production tooling:** when Controlo is entered in the context of an existing Job On/production, the tools/lots already selected in Job On are automatically available as the production context. Controlo receives/keeps/displays that tooling summary (`CM + Lote`, `MF + Lote`, `BQ + Lote`) with enough identity/context to trace those selections; where **Machine/Line** is part of the selected tooling context, Controlo preserves it. Controlo does **not** ask the user to reconstruct the production context manually.
- **Case B — control of another valid lot:** Controlo may also need to select/identify **another valid tooling lot** that is not currently the lot planned in that Job On (e.g. a newly arrived lot that must be controlled/inspected before it is selected for a production). That lot may be chosen as the **subject of a Controlo record** even though it is not yet the Job On production lot.

**Critical distinction:** selecting the **subject of a control** is different from selecting the **production tooling**. Controlo answers "which tool/lot am I controlling?"; Job On answers "which tool/lot is associated with this production?". A Controlo record identifying/controlling another valid lot does **not** add that lot to a Job On, does **not** replace a Job On tool, does **not** make it the production lot, and does **not** alter any Job On revision. It does **not** create a second production-tooling configuration or general tracking model — Job On remains the single central production/planning context. Changing which tooling is planned for production remains a Job On action performed by the Responsável. Controlo owns its control records/results and workflow/history; Job On integrates/links those results with the production context for consultation but does **not** own them.

## MUST PRESERVE

Exact job_on_id + job_on_revision_id; snapshot components; append-only workflow/history; useful free mode.

## MUST NOT

Second production selector; second calendar; silent production selection; click-to-release context semantics.

## DO NOT USE

- module redirect-shell behavior — superseded by the bound workspace

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `21_CONTROLO_91_ACCEPTANCE.md`.

