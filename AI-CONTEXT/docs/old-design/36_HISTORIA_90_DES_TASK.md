## DES-016 — Recompose História
**STATUS:** READY  
**DEPENDENCIES:** DES-001, DES-002  
**AUTHORITATIVE DESIGN FILES:** `historia.html`, control-history handoff  
**CURRENT SOURCE FILES:** Historia Razor/PageModel/service/repository  
**FUNCTIONAL RULES TO PRESERVE:** Read-only audit source; visible-module intersection; admin events only with audit capability.  
**EXACT PROBLEM:** Stacked technical cards do not match focused entity list + timeline and expose raw implementation detail.  
**IMPLEMENTATION SCOPE:** Compact filters, canonical entity selection, selected context, readable timeline, factual before/after correction detail, pagination and states.  
**MUST PRESERVE:** Raw event immutability and authorized filtering.  
**MUST NOT:** Add writes, rankings, interpretations, or leak unauthorized module events.  
**EXPECTED FILES TO CHANGE:** Historia Razor/CSS and minimal presentation mapping.  
**VERIFICATION:** Authorization/read-only tests and four viewport screenshots for selection, empty, loading/error and correction detail.  
**VISUAL ACCEPTANCE CRITERIA:** Split list/detail at desktop and stacked selected-detail at mobile match reference.  
**FUNCTIONAL REGRESSION CRITERIA:** Results remain derived only from authorized `audit_events`.  
**BLOCKERS:** None.

