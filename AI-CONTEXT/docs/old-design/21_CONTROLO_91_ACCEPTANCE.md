# Controlo — operational acceptance

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

**VERIFICATION:** Context-binding integration tests and screenshots for active, free, loading, empty and error states at four viewports.  
**FUNCTIONAL REGRESSION CRITERIA:** Historical records stay pinned if current Job On changes.  

Additionally prove the critical local rule in `21_CONTROLO_00_README.md` and run the existing relevant regression tests.

## NEGATIVE

- None of the local **MUST NOT** or **DO NOT USE** items appears or occurs.
- No demo IDs/data/logic, client business calculation, invented workflow, temporary layout or schema-driven redesign is introduced.
- No page-level horizontal scrolling occurs at any required viewport.

