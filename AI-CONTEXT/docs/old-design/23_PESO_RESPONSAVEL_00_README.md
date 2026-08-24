# Peso — Responsável

## IMPLEMENTATION TASK

DES-008. See `23_PESO_RESPONSAVEL_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `23_PESO_RESPONSAVEL_01_VISUAL_AUTHORITY_peso-responsavel.html`
2. `23_PESO_RESPONSAVEL_02_HANDOFF_PESO.md`
3. `23_PESO_RESPONSAVEL_03_OWNER_DECISION_GLASS_COMPARISON.md`
4. `23_PESO_RESPONSAVEL_90_DES_TASK.md`
5. `23_PESO_RESPONSAVEL_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**23_PESO_RESPONSAVEL_01_VISUAL_AUTHORITY_peso-responsavel.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `23_PESO_RESPONSAVEL_02_HANDOFF_PESO.md`
- `23_PESO_RESPONSAVEL_03_OWNER_DECISION_GLASS_COMPARISON.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Peso\Responsavel*.cshtml; shared Peso JS/CSS

## TARGET PAGE ANATOMY

Monday-first calendar; daily selectable list; focused compact detail; approve/reject; per-CM decisions; explicit send-to-production confirmation.

## CRITICAL LOCAL FUNCTIONAL RULES

Calculations remain server-side. Decisions operate on approved per-CM final glass-weight pairs.

## MUST PRESERVE

Same server results; individual decisions; justification when any CM is set aside; all-CM completeness.

## MUST NOT

Overall-average decisions; second comparison page; mutation of approved original.

## DO NOT USE

- capacity/global-average responsible detail — superseded by owner decision

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `23_PESO_RESPONSAVEL_91_ACCEPTANCE.md`.

