# Tampões

## IMPLEMENTATION TASK

DES-013. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_tampoes.html`
2. `02_BRIEF_TAMPOES.md`
3. `90_DES_TASK.md`
4. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_tampoes.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `02_BRIEF_TAMPOES.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Tampoes\*.cshtml; wwwroot\styles\modules\tampoes-layout.css; wwwroot\scripts\tampoes.js; services

## TARGET PAGE ANATOMY

Registo, Consulta, Planeamento, Histórico and right-aligned Opções; inline quantity/state/configuration transformations; selected detail and recent movements.

## CRITICAL LOCAL FUNCTIONAL RULES

Planning does not reserve stock. Every quantity change is an append-only, atomic server operation.

## MUST PRESERVE

Append-only movements; server-derived balances; atomic deltas/locks; optional read-only Job On; current line/machine data.

## MUST NOT

Planning reservation; Job On mutation; absolute client balance rewrites; movement deletion.

## DO NOT USE

- tampoes-v38-standalone.html — standalone historical variant; not canonical and not copied

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

