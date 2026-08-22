## DES-015 — Recompose Reparação Externa
**STATUS:** READY  
**DEPENDENCIES:** DES-001, DES-010, DES-012  
**AUTHORITATIVE DESIGN FILES:** external-repair mockups/brief, constrained by functional SOT A–G  
**CURRENT SOURCE FILES:** External Repair Razor/partial/JS/CSS and services/UoW  
**FUNCTIONAL RULES TO PRESERVE:** BQ deferred; warehouse ownership; atomic pickup/return; explicit physical confirmation; duplicate-open-item rule; cancellation deferred.  
**EXACT PROBLEM:** Sparse/fake-prone BQ tab, manual refresh, incomplete CM/MF builder/lifecycle and inconsistent selection/detail layouts.  
**IMPLEMENTATION SCOPE:** Honest BQ deferred empty state; separate CM/MF builders; exits, list detail, confirmation/return, history and repairer/line settings.  
**MUST PRESERVE:** Distinct identities, repairer snapshots and one-UoW cross-state operations.  
**MUST NOT:** Invent BQ repair behavior, write warehouse tables directly, infer physical effects or expose CancelarLista.  
**EXPECTED FILES TO CHANGE:** External Repair Razor/partial/JS/CSS and minimal display queries.  
**VERIFICATION:** Duplicate/atomic-flow regressions, JS load test, and screenshots for all tabs/states/viewports.  
**VISUAL ACCEPTANCE CRITERIA:** CM/MF stay visually parallel but distinct; BQ clearly states unavailable without fake controls.  
**FUNCTIONAL REGRESSION CRITERIA:** Warehouse effects occur only through its port in the same UoW.  
**BLOCKERS:** None.

