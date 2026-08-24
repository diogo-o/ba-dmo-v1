# Tampões — operational acceptance (Owner-confirmed model)

## VISUAL

- Capture the authority and implementation at the identical viewport and state.
- Produce an overlay/diff and correct every unexplained discrepancy.
- Verify hierarchy, section order, tabs, main table, cards, controls, responsive composition and visible states.

## VIEWPORTS

- 1440×900
- 980×900
- 720×900
- 375×812

## STATES

- Populated
- Selected
- Empty
- Loading
- Error
- Dialog/edit where applicable
- Mobile first screen

## FUNCTIONAL

**VERIFICATION:** atomic movement + configuration-edit regressions and all viewport screenshots per tab and state.
**FUNCTIONAL REGRESSION CRITERIA:** every quantity change remains a new movement and balances stay server-derived; a configuration edit updates the row without silently rewriting history.

Additionally prove the critical local rules and the corrections below in `33_TAMPOES_00_README.md` / `33_TAMPOES_02_BRIEF_TAMPOES.md` and run the existing relevant regression tests.

### Owner-confirmed model — PASS criteria

- Main table displays **Máquina/Máquinas + Diâmetro + Calote** per configuration.
- Quantity is visible per configuration (with optional category balances when used).
- **One click** selects the row and exposes quick quantity actions.
- Quantity can be **added / removed** (inline).
- An **optional quantity category** can be chosen.
- **Double click** opens the configuration editor for that row.
- Máquina/Máquinas, Diâmetro and Calote can be **edited**.
- Saving updates the row in the main table.
- A **new configuration** can be created and appears in the main table.
- Máquina may be a **single or multiple machines** (represented as Máquina ou Máquinas).
- Configuration fields are editable and the configurable-characteristic system stays flexible.

## NEGATIVE — MUST NOT appear or occur

- No **Reference**.
- No **Production**.
- No **Job On** (no field, no link, no read-only integration, no data flow either direction).
- No **Planeamento** (no tab, no plan cards, no active plans, no "Planear" actions, no reservation).
- No mandatory state lifecycle (Enchidos/Por encher and Maquinados/Por maquinar are optional classifications, never forced).
- No individual tampão numbers.
- The "Linhas" pagination field is **not** modeled/used as Máquina.
- No absolute client balance rewrites, no movement deletion, no silent overwrite of historical facts.
- None of the local **MUST NOT** or **DO NOT USE** items appears or occurs.
- No demo IDs/data/logic, client business calculation, invented workflow, temporary layout or schema-driven redesign is introduced.
- No page-level horizontal scrolling occurs at any required viewport.
