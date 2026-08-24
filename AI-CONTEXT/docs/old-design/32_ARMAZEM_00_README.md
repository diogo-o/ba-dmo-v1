# Armazém

## IMPLEMENTATION TASK

DES-012. See `32_ARMAZEM_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `32_ARMAZEM_01_VISUAL_AUTHORITY_armazem.html`
2. `32_ARMAZEM_02_BRIEF_ARMAZEM.md`
3. `32_ARMAZEM_03_OWNER_DECISION_SAP_ALERT.md`
4. `32_ARMAZEM_90_DES_TASK.md`
5. `32_ARMAZEM_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**32_ARMAZEM_01_VISUAL_AUTHORITY_armazem.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md`

## SUPPORTING AUTHORITY

- `32_ARMAZEM_02_BRIEF_ARMAZEM.md`
- `32_ARMAZEM_03_OWNER_DECISION_SAP_ALERT.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Armazem\*.cshtml; wwwroot\styles\modules\armazem-layout.css; wwwroot\scripts\armazem.js; services

## TARGET PAGE ANATOMY

Inline CM/MF entry/exit; search; utilisation-aware operational alerts; consultation/correction; calendar history.

## CRITICAL LOCAL FUNCTIONAL RULES

Stored manual SAP utilisation may be consumed to display the appropriate alert when a tool enters storage. Armazém does not calculate utilisation.

**Supports Job On planning:** Armazém tells Job On **where the required tooling physically is** (CM/MF/BQ where supported): position/location, presence, in production, away for repair, returned/available, and noteworthy state before a planned production. Job On consumes this for **production planning**. Selecting/associating a tool in Job On does **not** create an Armazém movement or reservation; physical movements remain Armazém operations.

**Existing BQ/Lot record — operational maintenance surface (OWNER-CONFIRMED Q4):** when a BQ/Lot **already exists**, its record and tool characteristics are **viewed/maintained from ARMAZÉM**; maintenance of the **characteristics functionally confirmed as editable** is performed by the **RESPONSÁVEL** profile. Q4 does **not** make every master field editable by itself. This does **not** transfer ownership: the BQ stays a tool whose master/domain belongs to Ferramentas, and the BQ external-repair movement flow stays in Boquilhas. The BQ/Lot created in Boquilhas when missing is the **same logical record** later seen/maintained here — no duplicate master.

**`% utilização` (OWNER-CONFIRMED Q2+Q4):** value is **always manual** — never calculated, incremented, derived or auto-updated by the system; production → Armazém transition shows **only a reminder/alarm to update `% utilização`** (never mutates the value). Since the existing BQ/Lot record is maintained here by Responsável, the **manual update may be performed here** where the characteristic is exposed as editable. Armazém does **not** own `% utilização` (Ferramentas remains the master domain) and does not calculate/update it automatically (see `32_ARMAZEM_03_OWNER_DECISION_SAP_ALERT.md`, refined by Q2+Q4).

## MUST PRESERVE

Warehouse ownership; read-only tool identity; 4-digit positions; explicit persistence; 1:1 occupation; prior movements; existing BQ/Lot record view/maintenance surface (Armazém, Responsável, confirmed-editable characteristics only); manual `% utilização` with Production→Armazém reminder only; no automatic calculation/update of utilisation.

## MUST NOT

Silent normalization/replacement; direct tool-domain edits; BQ or programmed external-flow activation; utilisation calculation; automatic update of `% utilização`; generic master editing outside the confirmed editable characteristics; treating the operational maintenance surface as ownership of the BQ master or of the BQ repair flow.

## DO NOT USE

- normal Substituir action — forbidden
- programmed external-repair and BQ mockup behavior — out of current U-14 scope

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. Functional behavior wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `32_ARMAZEM_91_ACCEPTANCE.md`.

