# Job On

## IMPLEMENTATION TASK

DES-005. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_job-on.html`
2. `02_VISUAL_AUTHORITY_PRINT_job-on-4-pages.html`
3. `03_BRIEF_JOB_ON.md`
4. `04_DATA_CONTRACT_JOB_ON.md`
5. `05_BRIEF_VERIFICATIONS.md`
6. `06_HANDOFF_PRINT.md`
7. `07_OWNER_DECISION_SHARED_DOCUMENTS.md`
8. `08_OWNER_DECISION_ARTICLE_IMAGE.md`
9. `90_DES_TASK.md`
10. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_job-on.html; print authority: 02_VISUAL_AUTHORITY_PRINT_job-on-4-pages.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `03_BRIEF_JOB_ON.md`
- `04_DATA_CONTRACT_JOB_ON.md`
- `05_BRIEF_VERIFICATIONS.md`
- `06_HANDOFF_PRINT.md`
- `07_OWNER_DECISION_SHARED_DOCUMENTS.md`
- `08_OWNER_DECISION_ARTICLE_IMAGE.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\JobOn\*.cshtml; wwwroot\styles\modules\jobon-layout.css; wwwroot\scripts\jobon.js; JobOn services/PDF renderer

## TARGET PAGE ANATOMY

Compact calendar and production list; creation/duplication; fixed production context; consultation/edit sheet; complete family cards; verifications; history/settings; four-page print.

## CRITICAL LOCAL FUNCTIONAL RULES

Article image belongs to master article/reference, is selected from the company-server image directory, and only the required Job On print sheet displays it.

## MUST PRESERVE

Immutable exact revisions; atomic full aggregate; historical tools; master-domain ownership; exact context for Peso/Pegamentos/Controlo; reference-owned article image.

## MUST NOT

Master-tool edits; historical reinterpretation; internal IDs; schema redesign; per-revision image model; image on every print sheet.

## DO NOT USE

- job-on-v48-folha-producao.html — referenced by an older package README/HANDOFF text but absent; do not substitute another file

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

