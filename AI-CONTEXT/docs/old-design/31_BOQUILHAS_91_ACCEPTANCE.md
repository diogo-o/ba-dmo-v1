# Boquilhas — operational acceptance

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

**VERIFICATION:** Movement/discrepancy/close regressions; screenshot comparison for every tab/state at all viewports.  
**FUNCTIONAL REGRESSION CRITERIA:** 20→25 remains non-blocking and BQ schema/domain stays independent.  

Additionally prove the critical local rule in `31_BOQUILHAS_00_README.md` and run the existing relevant regression tests.

**OWNER-CONFIRMED RULES (Q1–Q4) — prove in acceptance:**
- **Q1 Perfil:** Operador e Responsável realizam as mesmas ações em Boquilhas quando o módulo está atribuído — sem variantes por perfil, sem aprovação/revisão por perfil.
- **Q2 Utilização:** `% utilização` é sempre manual; o sistema nunca a calcula nem a atualiza automaticamente; a transição Produção → Armazém gera apenas reminder (o valor nunca é mutado pelo sistema).
- **Q3 Data de abertura:** campo DATE editável no Registo (preenchimento manual ou date picker), default hoje, alterável antes de guardar; timestamps técnicos não o substituem.
- **Q4 Registo existente:** criação em falta em Boquilhas com continuação imediata (`CREATE → CONTINUE`); registo BQ/Lote já existente é consultado/mantido a partir do **Armazém** pelo **Responsável** (características confirmadas como editáveis — a Q4 não torna todos os campos editáveis); a BQ/Lote criada em Boquilhas é o **mesmo registo lógico** depois visível no Armazém (sem master duplicado); Boquilhas não é a superfície normal de manutenção; o Armazém não é dono do fluxo de reparação.

## NEGATIVE

- None of the local **MUST NOT** or **DO NOT USE** items appears or occurs.
- No demo IDs/data/logic, client business calculation, invented workflow, temporary layout or schema-driven redesign is introduced.
- No page-level horizontal scrolling occurs at any required viewport.

