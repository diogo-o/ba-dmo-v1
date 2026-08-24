## DES-014 — Correct and recompose Reparação Interna
**STATUS:** READY  
**DEPENDENCIES:** DES-001, DES-005  
**AUTHORITATIVE DESIGN FILES:** final RI HTML/brief and owner decision  
**CURRENT SOURCE FILES:** RI Razor/JS/CSS and service/context lookup  
**FUNCTIONAL RULES TO PRESERVE:** CM/MF only; complete reference; repeated numbers; no hard blocks; append-only correction with recalibrated line context.  
**EXACT PROBLEM:** Current UI still offers BQ repair and truncation risk; tab/quick-entry/consultation composition differs.  
**IMPLEMENTATION SCOPE:** Remove BQ type; render B1–C3 production cards with full reference; implement line→CM/MF→number→OK; recent records, Consulta, correction chain and free no-production context.  
**MUST PRESERVE:** `5447T173` display, with `T173` context only; exact Job On context when available; own-record visibility rules.  
**MUST NOT:** Select/process BQ, truncate to `5447`, deduplicate occurrence numbers, block no-production registration, or alter Job On.  
**EXPECTED FILES TO CHANGE:** RI Razor/JS/CSS and display models if needed to expose complete reference.  
**VERIFICATION:** Explicit CM/MF-only and full-reference regressions; correction-context tests; screenshots at every viewport for production/no-production/recent/consultation/correction.  
**VISUAL ACCEPTANCE CRITERIA:** Only CM and MF selectors exist; `5447T173` remains visible in context; keyboard rapid entry works.  
**FUNCTIONAL REGRESSION CRITERIA:** BQ cannot be submitted and original corrections remain.  
**BLOCKERS:** None.

