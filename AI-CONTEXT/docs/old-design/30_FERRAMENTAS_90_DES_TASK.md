## DES-010 — Recompose Ferramentas
**STATUS:** READY  
**DEPENDENCIES:** DES-001  
**AUTHORITATIVE DESIGN FILES:** Ferramentas HTML and registration/verification briefs  
**CURRENT SOURCE FILES:** Ferramentas Razor/JS/CSS and services  
**FUNCTIONAL RULES TO PRESERVE:** CM/MF separate identities; stable reference/lote IDs; verification and usage append-only; no copied occurrences.  
**EXACT PROBLEM:** Current multi-page/tab structure does not match reference list + focused five-tab workspace and lacks several state/history presentations.  
**IMPLEMENTATION SCOPE:** Unified list/detail composition; Reference, Lotes, Verificações and Histórico; create-reference/first-lot and duplicate-lot flows; activate the Utilização tab per the resolved Q-001 (see `30_FERRAMENTAS_03_OWNER_DECISION_SAP_UTILISATION.md`).  
**MUST PRESERVE:** Master-vs-lote ownership, technical name, allowed lines, drawing/revision, copy rules.  
**MUST NOT:** Merge CM/MF, infer drawing codes, duplicate checks/history, or create warehouse identity.  
**EXPECTED FILES TO CHANGE:** Ferramentas Razor/JS/CSS; minimal read DTOs for current location/status if proven absent.  
**VERIFICATION:** Reference/lote/verification regression tests and all viewport screenshots for list, each tab, create and duplicate.  
**VISUAL ACCEPTANCE CRITERIA:** Compact reference list and stable tabbed detail match canonical HTML.  
**FUNCTIONAL REGRESSION CRITERIA:** Existing IDs and domain ownership remain unchanged.  
**BLOCKERS:** None outstanding. Q-001 is resolved: the Utilização UI is activated, utilisation is manually read from SAP and manually entered, the app never calculates it, and automatic SAP integration is out of scope; see `30_FERRAMENTAS_03_OWNER_DECISION_SAP_UTILISATION.md`.

