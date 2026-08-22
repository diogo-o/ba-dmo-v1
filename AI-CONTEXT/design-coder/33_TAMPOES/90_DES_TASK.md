## DES-013 — Recompose Tampões
**STATUS:** READY  
**DEPENDENCIES:** DES-001, DES-002  
**AUTHORITATIVE DESIGN FILES:** Tampões HTML/brief  
**CURRENT SOURCE FILES:** Tampões Razor/JS/CSS and services  
**FUNCTIONAL RULES TO PRESERVE:** Append-only movements, derived balances, atomic quantity changes, optional read-only Job On, planning ≠ reservation.  
**EXACT PROBLEM:** Tab topology and registration/detail hierarchy diverge; selected actions, calendar and responsive states are incomplete.  
**IMPLEMENTATION SCOPE:** Canonical Registo/Consulta/Planeamento/Histórico/Opções; fold line-machine details into designed surfaces; inline quantity/state/config transformation; recent movements and filters.  
**MUST PRESERVE:** Current functional data and line/machine information even when its presentation moves.  
**MUST NOT:** Reserve through planning, mutate Job On, perform absolute client balance rewrites, or delete movement history.  
**EXPECTED FILES TO CHANGE:** Tampões Razor/JS/CSS and only proven read DTO additions.  
**VERIFICATION:** Atomic movement/planning regressions and all viewport screenshots per tab and state.  
**VISUAL ACCEPTANCE CRITERIA:** Five-tab composition, proportional editors and table/card reflow match reference.  
**FUNCTIONAL REGRESSION CRITERIA:** Every change remains a new movement and balances stay server-derived.  
**BLOCKERS:** None.

