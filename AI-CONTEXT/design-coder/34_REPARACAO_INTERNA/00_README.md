# Reparação Interna

## IMPLEMENTATION TASK

DES-014. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_reparacao-interna.html`
2. `02_BRIEF_REPARACAO_INTERNA.md`
3. `03_OWNER_DECISION_CM_MF_ONLY.md`
4. `90_DES_TASK.md`
5. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_reparacao-interna.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `02_BRIEF_REPARACAO_INTERNA.md`
- `03_OWNER_DECISION_CM_MF_ONLY.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\ReparacaoInterna\*.cshtml; wwwroot\styles\modules\reparacao-interna-layout.css; wwwroot\scripts\reparacao-interna.js; services/context lookup

## TARGET PAGE ANATOMY

B1–C3 production cards with full reference; line→CM/MF→number→OK rapid entry; recent records; Consulta; append-only correction chain; no-production context.

## CRITICAL LOCAL FUNCTIONAL RULES

CM/MF only. BQ is never selectable or repairable. Always show 5447T173 in full; T173 is context only.

## MUST PRESERVE

CM/MF only; full 5447T173 reference; repeated numbers; no hard blocks; recalibrated correction context.

## MUST NOT

BQ selection/processing; truncation to 5447; deduplication; no-production block; Job On mutation.

## DO NOT USE

- any historical RI source allowing BQ — superseded by owner decision

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

