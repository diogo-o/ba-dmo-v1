# Job On — operational acceptance

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

**VERIFICATION:** Existing functional tests, aggregate hydration/duplication regression, print render, all four viewport screenshots in consultation/edit/empty/error states.  
**FUNCTIONAL REGRESSION CRITERIA:** Opening sets the exact current Job On; revisions and history stay immutable.  

**OPERATIONAL-MODEL ACCEPTANCE (owner-approved):**
- Job On is presented as the central production/planning context and the operational consultation hub for the production (integrating, not owning, associated Controlo and Reparação Interna records).
- Only the **Responsável** profile can modify the Job On; the **Operador** is read-only except for manually confirming verification checks.
- Controlo and RI keep owning their own records; Job On only links their association to the production.
- Each production is planned per a specific **Machine/Line**; the saved revision carries Reference + Production + Machine/Line + the exact tooling chosen (CM/MF/BQ, each with Reference + Lot + Machine where that is part of the option).
- Tooling options are distinguishable by **Type + Reference + Lot + Machine/Line**; the same Reference + Lot may exist on different Machines, and a different Machine does not imply a different lot.
- The application supports the Responsável's tooling decision and **never infers/auto-selects** the correct tool from Machine/Line or Reference; the Responsável makes the final choice and the Job On persists exactly what was selected (never silently replaced).
- **Downstream no-redefinition:** Controlo, Peso, Pegamentos and Reparação Interna do not independently redefine the tooling configuration of a Job On production; they consume the required inherited subset. Controlo inherits the CM/MF/BQ summary; Peso functionally uses the inherited CM + lot only. A module may still select/identify another valid lot as the subject of its own domain workflow (e.g. Controlo of a newly received lot) without altering the Job On production plan; selecting the subject of a control is distinct from selecting the production tooling.

Additionally prove the critical local rule in `20_JOB_ON_00_README.md` and run the existing relevant regression tests.

## NEGATIVE

- None of the local **MUST NOT** or **DO NOT USE** items appears or occurs.
- No demo IDs/data/logic, client business calculation, invented workflow, temporary layout or schema-driven redesign is introduced.
- No page-level horizontal scrolling occurs at any required viewport.

