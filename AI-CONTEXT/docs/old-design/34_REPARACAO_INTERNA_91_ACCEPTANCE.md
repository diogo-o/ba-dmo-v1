# Reparação Interna — operational acceptance

## VISUAL

- Capture the authority and implementation at the identical viewport and state.
- Produce an overlay/diff and correct every unexplained discrepancy.
- Verify hierarchy, section order, tabs, sidebars, cards, controls, responsive composition and visible states.

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

**VERIFICATION:** Explicit CM/MF-only and full-reference regressions; correction-context tests; screenshots at every viewport for production/no-production/recent/consultation/correction.  
**FUNCTIONAL REGRESSION CRITERIA:** BQ cannot be submitted and original corrections remain.  

Additionally prove the critical local rule in `34_REPARACAO_INTERNA_00_README.md` and run the existing relevant regression tests.

## NEGATIVE

- None of the local **MUST NOT** or **DO NOT USE** items appears or occurs.
- No demo IDs/data/logic, client business calculation, invented workflow, temporary layout or schema-driven redesign is introduced.
- No page-level horizontal scrolling occurs at any required viewport.

