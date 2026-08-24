## DES-008 — Recompose Peso Responsible
**STATUS:** READY  
**DEPENDENCIES:** DES-007  
**AUTHORITATIVE DESIGN FILES:** responsible HTML and Peso handoff  
**CURRENT SOURCE FILES:** Responsavel Razor and Peso JS/CSS  
**FUNCTIONAL RULES TO PRESERVE:** Same server results; individual comparison decisions; justification rule; no mutation of original approval.  
**EXACT PROBLEM:** Daily calendar/list/detail and decision layout are incomplete and contain obsolete capacity/global-average context.  
**IMPLEMENTATION SCOPE:** Canonical calendar, daily selectable list, compact result detail, approve/reject, per-CM decision table, send-to-production confirmation.  
**MUST PRESERVE:** All-CM decision completeness; server-derived operator/responsible identity.  
**MUST NOT:** Create a second comparison page or allow overall-average decisions.  
**EXPECTED FILES TO CHANGE:** Responsavel Razor, Peso JS/CSS.  
**VERIFICATION:** Approval/decision tests and all four viewport screenshots with pending/approved/rejected/partial-decision states.  
**VISUAL ACCEPTANCE CRITERIA:** Calendar left/list and focused detail right match reference; responsible table hides only allowed operational detail.  
**FUNCTIONAL REGRESSION CRITERIA:** Approved original remains immutable and send is explicit.  
**BLOCKERS:** None.

