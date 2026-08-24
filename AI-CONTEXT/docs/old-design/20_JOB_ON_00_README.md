# Job On

## IMPLEMENTATION TASK

DES-005. See `20_JOB_ON_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `20_JOB_ON_01_VISUAL_AUTHORITY_job-on.html`
2. `20_JOB_ON_02_VISUAL_AUTHORITY_PRINT_job-on-4-pages.html`
3. `20_JOB_ON_03_BRIEF_JOB_ON.md`
4. `20_JOB_ON_04_DATA_CONTRACT_JOB_ON.md`
5. `20_JOB_ON_05_BRIEF_VERIFICATIONS.md`
6. `20_JOB_ON_06_HANDOFF_PRINT.md`
7. `20_JOB_ON_07_OWNER_DECISION_SHARED_DOCUMENTS.md`
8. `20_JOB_ON_08_OWNER_DECISION_ARTICLE_IMAGE.md`
9. `20_JOB_ON_90_DES_TASK.md`
10. `20_JOB_ON_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**20_JOB_ON_01_VISUAL_AUTHORITY_job-on.html; print authority: 20_JOB_ON_02_VISUAL_AUTHORITY_PRINT_job-on-4-pages.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `20_JOB_ON_03_BRIEF_JOB_ON.md`
- `20_JOB_ON_04_DATA_CONTRACT_JOB_ON.md`
- `20_JOB_ON_05_BRIEF_VERIFICATIONS.md`
- `20_JOB_ON_06_HANDOFF_PRINT.md`
- `20_JOB_ON_07_OWNER_DECISION_SHARED_DOCUMENTS.md`
- `20_JOB_ON_08_OWNER_DECISION_ARTICLE_IMAGE.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\JobOn\*.cshtml; wwwroot\styles\modules\jobon-layout.css; wwwroot\scripts\jobon.js; JobOn services/PDF renderer

## TARGET PAGE ANATOMY

Compact calendar and production list; creation/duplication; fixed production context; consultation/edit sheet; complete family cards; verifications; history/settings; four-page print.

## CRITICAL LOCAL FUNCTIONAL RULES

Article image belongs to master article/reference, is selected from the company-server image directory, and only the required Job On print sheet displays it.

Each production is planned per a specific **Machine/Line**; a saved Job On revision represents Reference + Production + Machine/Line + the exact tooling chosen. Tooling option identity preserves **Type + Reference + Lot + Machine/Line** (the same Reference + Lot may exist on different Machines; a different Machine does not imply a different lot). The **Responsável makes the final tooling choice** — the application does not infer/auto-select the correct tool. Job On persists exactly what was selected; **downstream modules do not independently redefine the tooling configuration of a Job On production** (Controlo inherits CM/MF/BQ; Peso functionally uses the inherited CM + lot only). A module may still select/identify another valid lot as the subject of its own domain workflow (e.g. Controlo registering/controlling a newly received lot); that does not alter the Job On production plan.

## MUST PRESERVE

Immutable exact revisions; atomic full aggregate; historical tools; master-domain ownership; exact context for Peso/Pegamentos/Controlo; reference-owned article image.

## MUST NOT

Master-tool edits; historical reinterpretation; internal IDs; schema redesign; per-revision image model; image on every print sheet.

## DO NOT USE

- job-on-v48-folha-producao.html — referenced by an older package README/HANDOFF text but absent; do not substitute another file

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `20_JOB_ON_91_ACCEPTANCE.md`.

