# Reparação Externa

## IMPLEMENTATION TASK

DES-015. See `90_DES_TASK.md`.

## READ IN THIS ORDER

1. `01_VISUAL_AUTHORITY_moldes.html`
2. `02_SUPPORTING_LIFECYCLE_reparacao-externa-v1.html`
3. `03_BRIEF_REPARACAO_EXTERNA.md`
4. `99_DO_NOT_IMPLEMENT_reparacao-v2.html`
5. `90_DES_TASK.md`
6. `91_ACCEPTANCE.md`

Before these local files, read `../00_READ_FIRST.md`, `../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `../03_GLOBAL_DESIGN_SYSTEM.md`, `../04_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `../05_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**01_VISUAL_AUTHORITY_moldes.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`../02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `02_SUPPORTING_LIFECYCLE_reparacao-externa-v1.html`
- `03_BRIEF_REPARACAO_EXTERNA.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\ReparacaoExterna\*.cshtml; module partial/JS/CSS; services/UoW

## TARGET PAGE ANATOMY

Honest deferred BQ state; separate CM/MF builders; exits/list detail; explicit pickup/return; history; repairer/line settings.

## CRITICAL LOCAL FUNCTIONAL RULES

BQ workflow is deferred. Pickup/return changes repair and physical state atomically through the Warehouse-owned port.

## MUST PRESERVE

Distinct identities; repairer snapshots; Warehouse port ownership; one-UoW cross-state operations; duplicate-open-item hard rule.

## MUST NOT

Invented BQ behavior; direct Warehouse-table writes; inferred physical effects; CancelarLista.

## DO NOT USE

- reparacao-v2.html — SUPERSEDED transitional combined navigation; copied only as 99_DO_NOT_IMPLEMENT
- reparacao-externa-v1.html — SUPPORTING lifecycle reference, not primary visual authority

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `91_ACCEPTANCE.md`.

