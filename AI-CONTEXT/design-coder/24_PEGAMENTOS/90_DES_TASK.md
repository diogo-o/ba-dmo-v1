## DES-009 — Recompose Pegamentos
**STATUS:** READY  
**DEPENDENCIES:** DES-005, DES-006  
**AUTHORITATIVE DESIGN FILES:** Pegamentos HTML/handoff/snapshot  
**CURRENT SOURCE FILES:** Pegamentos Index/Detail Razor, JS/CSS and services/PDF  
**FUNCTIONAL RULES TO PRESERVE:** Exact revision; inherited CM/BQ/MF; ±0.20 boundary; server calculations; one immutable final PDF.  
**EXACT PROBLEM:** Manual revision-ID entry, redundant open action, wrong displayed tolerance and local-document language contradict authority.  
**IMPLEMENTATION SCOPE:** Bind creation to active context, canonical history open behavior, inherited-tool summary, measurement entry, workflow/history and unified-workspace document states.  
**MUST PRESERVE:** Structured snapshot, closed-document immutability, antiforgery.  
**MUST NOT:** Let users choose inherited tools, type internal IDs, calculate in JS or silently replace final PDF.  
**EXPECTED FILES TO CHANGE:** Pegamentos Razor/JS/CSS and minimal context read DTOs.  
**VERIFICATION:** Tolerance-boundary and document-once tests; screenshots at all viewports for context complete/incomplete, measurement, history, loading/error.  
**VISUAL ACCEPTANCE CRITERIA:** Production context is human-readable and inherited; no redundant `Abrir folha` button.  
**FUNCTIONAL REGRESSION CRITERIA:** Exact revision and frozen snapshot remain authoritative.  
**BLOCKERS:** None.

