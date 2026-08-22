# Peso — Operador

## IMPLEMENTATION TASK

DES-007. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_peso-operador.html`
2. `02_VISUAL_AUTHORITY_PRINT_peso.html`
3. `03_HANDOFF_PESO.md`
4. `04_OWNER_DECISION_GLASS_COMPARISON.md`
5. `05_OWNER_DECISION_SHARED_DOCUMENTS.md`
6. `90_DES_TASK.md`
7. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_peso-operador.html; print authority: 02_VISUAL_AUTHORITY_PRINT_peso.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `03_HANDOFF_PESO.md`
- `04_OWNER_DECISION_GLASS_COMPARISON.md`
- `05_OWNER_DECISION_SHARED_DOCUMENTS.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Peso\Index.cshtml; wwwroot\styles\modules\peso-layout.css; wwwroot\scripts\peso.js; Peso services

## TARGET PAGE ANATOMY

Bound production/reference context; readings; per-CM glass-weight results; explicit calculation/submit; prior-production choice; current-to-previous CM pairing; history/documents.

## CRITICAL LOCAL FUNCTIONAL RULES

Calculations remain server-side. Comparison uses approved per-CM final glass-weight rules.

## MUST PRESERVE

Exact revision; server-only formula/density; positional pairing; exact NNPB/PS snapshot; approved snapshot.

## MUST NOT

Client formulas; water/capacity/global-average comparison; CM/lote reselection; fake revision.

## DO NOT USE

- capacity/global-average comparison variants — superseded by owner decision

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

