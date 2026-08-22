## DES-006 — Recompose Controlo unified workspace
**STATUS:** READY  
**DEPENDENCIES:** DES-002, DES-005  
**AUTHORITATIVE DESIGN FILES:** `controlo.html`, control handoffs, shared-production decision  
**CURRENT SOURCE FILES:** Controlo Razor/JS/CSS and Controlo services/lookups  
**FUNCTIONAL RULES TO PRESERVE:** One exact current Job On for all tabs; no second calendar/reselection; free mode without fake production; append-only sheet workflow.  
**EXACT PROBLEM:** Active context exists but most tabs act as redirect shells and free mode is too empty.  
**IMPLEMENTATION SCOPE:** Build the active-production card and bound Resumo/Peso/Comparação/Pegamentos/Histórico surfaces; provide free-mode read-only queries.  
**MUST PRESERVE:** Exact `job_on_id + job_on_revision_id`, snapshot components, draft→review workflow, reopen history.  
**MUST NOT:** Invent click-to-release context semantics, add a calendar, or silently select a production.  
**EXPECTED FILES TO CHANGE:** Controlo Razor/JS/CSS and minimal additive aggregate read models if current endpoints cannot populate tabs.  
**VERIFICATION:** Context-binding integration tests and screenshots for active, free, loading, empty and error states at four viewports.  
**VISUAL ACCEPTANCE CRITERIA:** Every tab visibly shares one production card; free mode is clearly unbound, never fake.  
**FUNCTIONAL REGRESSION CRITERIA:** Historical records stay pinned if current Job On changes.  
**BLOCKERS:** None.

