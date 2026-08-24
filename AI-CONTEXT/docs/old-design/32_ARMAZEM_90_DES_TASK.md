## DES-012 — Recompose Armazém within current scope
**STATUS:** READY  
**DEPENDENCIES:** DES-001, DES-010  
**AUTHORITATIVE DESIGN FILES:** Armazém HTML/brief, constrained by functional SOT  
**CURRENT SOURCE FILES:** Armazém Razor/JS/CSS and services  
**FUNCTIONAL RULES TO PRESERVE:** Warehouse owns location/movement; read-only tool identity; explicit movement only; 1:1 occupation; no silent normalization.  
**EXACT PROBLEM:** Normal substitute action and programmed-flow labels conflict with design/authority; alerts, consultation/history/correction need full composition.  
**IMPLEMENTATION SCOPE:** Inline CM/MF entry/exit, search, operational alerts, consultation, correction and calendar history; clearly defer out-of-scope programmed external/BQ activation.  
**MUST PRESERVE:** Exact 4-digit positions, server actor, prior movements and explicit confirmations.  
**MUST NOT:** Preserve schema as design authority, directly edit tool-domain state, activate BQ or external programmed exits, or silently replace occupancy.  
**EXPECTED FILES TO CHANGE:** Armazém Razor/JS/CSS and minimal read DTOs for alerts/current context.  
**VERIFICATION:** Movement/occupation regression and all viewport screenshots for entry, exit, alert, consultation, history and failures.  
**VISUAL ACCEPTANCE CRITERIA:** Inline editors and alert cards match reference; no normal `Substituir` action.  
**FUNCTIONAL REGRESSION CRITERIA:** Position change occurs only after successful server persistence.  
**BLOCKERS:** None.

