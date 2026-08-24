# Tampões

> **Autoridade Owner:** este módulo segue a clarificação funcional do Owner. Tampões é um módulo **simples e autónomo** de disponibilidade de quantidade por configuração técnica / máquina, **sem** relação com Job On, Reference ou Production e **sem** planeamento de produção.

## IMPLEMENTATION TASK

DES-013. See `33_TAMPOES_90_DES_TASK.md`.

## READ IN THIS ORDER

1. `33_TAMPOES_01_VISUAL_AUTHORITY_tampoes.html`
2. `33_TAMPOES_02_BRIEF_TAMPOES.md`
3. `33_TAMPOES_90_DES_TASK.md`
4. `33_TAMPOES_91_ACCEPTANCE.md`

Before these local files, read `0_GLOBAL_READ_FIRST.md`, `0_GLOBAL_DESIGN_SYSTEM.md`, `0_GLOBAL_IMPLEMENTATION_CONTRACT.md`, and `0_GLOBAL_CODER_EXECUTION_RULES.md`.

## VISUAL AUTHORITY

**33_TAMPOES_01_VISUAL_AUTHORITY_tampoes.html**

This defines the required composition. Supporting/demo behavior never overrides the functional source of truth.

## FUNCTIONAL AUTHORITY

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md` + this module's brief (Owner-confirmed). Where a contradiction exists, the Owner-confirmed Tampões model in this package supersedes older global/design text (see OWNER MODEL section and STOP CONDITIONS).

## SUPPORTING AUTHORITY

- `33_TAMPOES_02_BRIEF_TAMPOES.md`

## CURRENT APP LOCATION

AI-CONTEXT\app\BA.Dmo.Web\Pages\Tampoes\*.cshtml; wwwroot\styles\modules\tampoes-layout.css; wwwroot\scripts\tampoes.js; services

## OWNER MODEL

Tampões is a SIMPLE, AUTONOMOUS top-level module whose purpose is to help the operator know how many TP / tampões are available for each technical configuration / machine.

- It is essentially a **simple table of configurations and quantities**.
- Main configuration per row: **Máquina / Máquinas + Diâmetro + Calote** + current quantity / optional saldos.
- A configuration may apply to **one or multiple Máquinas** — Máquina is part of the functional configuration (not an incidental display field).
- **No Reference, no Production, no Job On** — no functional flow Tampões ↔ Job On, no production planning, no reservation, no individual tampão numbers, no rigid lifecycle.
- **One click = select row + quick quantity actions** (add / remove / optional category).
- **Double click = edit that configuration** (Diâmetro, Calote, Máquina(s), other configured characteristics).
- Operator can create a new configuration / table row.
- **All configuration fields are editable**; the configuration system stays flexible — Diâmetro/Calote are not permanently the only possible characteristics.
- **Enchidos/Por encher and Maquinados/Por maquinar are OPTIONAL quantity classifications**, not a mandatory lifecycle.
- **No Planeamento area** — planning is out of scope.
- Operator (Operador/Controlador) is the operational owner/user; no Admin/Responsável required for normal configuration management.

## TARGET PAGE ANATOMY

Registo/main configuration-quantity table; Histórico; right-aligned Opções/Configuração. The main table supports consultation, one-click quantity actions, double-click configuration edit and new-configuration creation. (No Planeamento, no Consulta as a separate required duplicate, no Job On/Production/Reference.)

## CRITICAL LOCAL FUNCTIONAL RULES

- Main table shows Máquina/Máquinas + Diâmetro + Calote and current quantity per configuration.
- One click selects and exposes quick quantity actions; double click edits the configuration.
- Quantity add/remove is an inline operation; optional quantity category may be chosen.
- Every quantity change is an append-only, atomic server operation (balances derived from facts).
- **No negative balances; server-confirmed persistence before success; auditable corrections.**
- **No cross-module data flow:** no Job On, no Reference, no Production, no reservation.
- Configuration edits are direct maintenance — never simulated through production planning.

## MUST PRESERVE

Append-only movements; server-derived balances; atomic deltas/locks; configuration-metric editability (Máquina(s)/Diâmetro/Calote and future configurable fields); optional quantity classifications; auditable history (moves and configuration edits); responsive behavior.

## MUST NOT

Job On integration/mutation; Production/Reference association; planning/reservation; absolute client balance rewrites; movement deletion; silent overwrite of historical facts; modeling Máquina as source "Linhas" pagination field.

## DO NOT USE

- `tampoes-v38-standalone.html` — standalone historical variant; not canonical and not copied.

## STOP CONDITIONS

Stop and report if required data is unavailable, a schema change appears necessary, HTML and functional authority conflict, or a business rule would have to be invented. The Owner-confirmed Tampões model wins conflicts, but the conflict must be reported before implementation proceeds.

## ACCEPTANCE

Follow `33_TAMPOES_91_ACCEPTANCE.md`.
