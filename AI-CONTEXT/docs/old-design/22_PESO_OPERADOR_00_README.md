# Peso — Operador

## IMPLEMENTATION TASK

DES-007. See `22_PESO_OPERADOR_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `22_PESO_OPERADOR_01_VISUAL_AUTHORITY_peso-operador.html`
2. `22_PESO_OPERADOR_02_VISUAL_AUTHORITY_PRINT_peso.html`
3. `22_PESO_OPERADOR_03_HANDOFF_PESO.md`
4. `22_PESO_OPERADOR_04_OWNER_DECISION_GLASS_COMPARISON.md`
5. `22_PESO_OPERADOR_05_OWNER_DECISION_SHARED_DOCUMENTS.md`
6. `22_PESO_OPERADOR_90_DES_TASK.md`
7. `22_PESO_OPERADOR_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**22_PESO_OPERADOR_01_VISUAL_AUTHORITY_peso-operador.html; print authority: 22_PESO_OPERADOR_02_VISUAL_AUTHORITY_PRINT_peso.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `22_PESO_OPERADOR_03_HANDOFF_PESO.md`
- `22_PESO_OPERADOR_04_OWNER_DECISION_GLASS_COMPARISON.md`
- `22_PESO_OPERADOR_05_OWNER_DECISION_SHARED_DOCUMENTS.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Peso\Index.cshtml; wwwroot\styles\modules\peso-layout.css; wwwroot\scripts\peso.js; Peso services

## TARGET PAGE ANATOMY

Bound production/reference context; readings; per-CM glass-weight results; explicit calculation/submit; prior-production choice; current-to-previous CM pairing; history/documents.

## CRITICAL LOCAL FUNCTIONAL RULES

Calculations remain server-side. Comparison uses approved per-CM final glass-weight rules.

**Production context (no independent reconstruction):** Peso operates inside the **Controlo production workspace** (same tab of the unified workspace) using the **same exact Job On / revision** context consumed by Controlo. The production as a whole has **CM + MF + BQ** selected in the Job On, but the **Peso domain functionally uses only the inherited `CM + Lote`** for the weight record. Peso does **not** independently reconstruct production/tool identity, does **not** re-select CM, does **not** select MF/BQ, and does **not** reconstruct the production tooling; CM is inherited, never re-selected. Distinguish **global production tooling (CM + MF + BQ)** from **Peso functional tooling (CM + Lote)**.

## MUST PRESERVE

Exact revision; server-only formula/density; positional pairing; exact NNPB/PS snapshot; approved snapshot.

## MUST NOT

Client formulas; water/capacity/global-average comparison; CM/lote reselection; fake revision.

## DO NOT USE

- capacity/global-average comparison variants — superseded by owner decision

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `22_PESO_OPERADOR_91_ACCEPTANCE.md`.

