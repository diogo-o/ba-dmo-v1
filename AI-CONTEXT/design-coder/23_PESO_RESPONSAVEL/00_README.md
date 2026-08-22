# Peso — Responsável

## IMPLEMENTATION TASK

DES-008. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_peso-responsavel.html`
2. `02_HANDOFF_PESO.md`
3. `03_OWNER_DECISION_GLASS_COMPARISON.md`
4. `90_DES_TASK.md`
5. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_peso-responsavel.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `02_HANDOFF_PESO.md`
- `03_OWNER_DECISION_GLASS_COMPARISON.md`

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

Follow `91_ACCEPTANCE.md`.

