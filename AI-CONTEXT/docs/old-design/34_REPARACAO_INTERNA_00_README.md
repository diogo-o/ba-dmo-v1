# Reparação Interna

## IMPLEMENTATION TASK

DES-014. See `34_REPARACAO_INTERNA_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `34_REPARACAO_INTERNA_01_VISUAL_AUTHORITY_reparacao-interna.html`
2. `34_REPARACAO_INTERNA_02_BRIEF_REPARACAO_INTERNA.md`
3. `34_REPARACAO_INTERNA_03_OWNER_DECISION_CM_MF_ONLY.md`
4. `34_REPARACAO_INTERNA_90_DES_TASK.md`
5. `34_REPARACAO_INTERNA_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**34_REPARACAO_INTERNA_01_VISUAL_AUTHORITY_reparacao-interna.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `34_REPARACAO_INTERNA_02_BRIEF_REPARACAO_INTERNA.md`
- `34_REPARACAO_INTERNA_03_OWNER_DECISION_CM_MF_ONLY.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\ReparacaoInterna\*.cshtml; wwwroot\styles\modules\reparacao-interna-layout.css; wwwroot\scripts\reparacao-interna.js; services/context lookup

## TARGET PAGE ANATOMY

B1–C3 production cards with full reference; line→CM/MF→number→OK rapid entry; recent records; Consulta; append-only correction chain; no-production context.

## CRITICAL LOCAL FUNCTIONAL RULES

CM/MF only. BQ is never selectable or repairable. Always show 5447T173 in full; T173 is context only.

**Boundary with Controlo and Job On:** Reparação Interna is **independent from Controlo** — there is **no `Controlo → Reparação Interna` dependency**. Both consume the Job On production context directly. RI associates its records to the exact Job On / production context and **does not reconstruct or independently decide which production tooling was used** — Job On provides the production context/association. RI owns its repair records; Job On **integrates/links** the repairs associated with the production for consultation but does **not** own them. RI repairs **CM and MF only**; **BQ is never repairable, selectable, or processed here** — BQ is not an RI repair input and may remain visible only in the overall Reference/production identification context (BQ repair belongs functionally to the Boquilhas / external-repair lifecycle).

## MUST PRESERVE

CM/MF only; full 5447T173 reference; repeated numbers; no hard blocks; recalibrated correction context.

## MUST NOT

BQ selection/processing; truncation to 5447; deduplication; no-production block; Job On mutation.

## DO NOT USE

- any historical RI source allowing BQ — superseded by owner decision

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `34_REPARACAO_INTERNA_91_ACCEPTANCE.md`.

