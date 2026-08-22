## DES-005 — Recompose Job On planning and operational sheet
**STATUS:** READY  
**DEPENDENCIES:** DES-001, DES-002  
**AUTHORITATIVE DESIGN FILES:** Job On HTML/print and all Job On briefs  
**CURRENT SOURCE FILES:** JobOn Razor/PageModel, JS/CSS, JobOn application/read services and PDF renderer  
**FUNCTIONAL RULES TO PRESERVE:** Immutable exact revisions, atomic full aggregate, per-user current-open context, deterministic machine colours.  
**EXACT PROBLEM:** Planning and sheet require canonical geometry, consultation/edit hierarchy, current-vs-snapshot separation and print parity.  
**IMPLEMENTATION SCOPE:** Compact calendar/list; creation/duplication flows; fixed context; full family-card sheet; verification states; history/settings; four-page print.  
**MUST PRESERVE:** Full component snapshot, historical tools, master-domain ownership, exact context consumed by Peso/Pegamentos/Controlo; reference/image ownership per `08_OWNER_DECISION_ARTICLE_IMAGE.md`.  
**MUST NOT:** Create/edit master tools, reinterpret historical revisions, expose IDs, or redesign persistence/schema.  
**EXPECTED FILES TO CHANGE:** JobOn Razor/JS/CSS and minimal display/read DTOs; PDF presentation only.  
**VERIFICATION:** Existing functional tests, aggregate hydration/duplication regression, print render, all four viewport screenshots in consultation/edit/empty/error states.  
**VISUAL ACCEPTANCE CRITERIA:** Dates/machine/CM-MF-BQ dominate; all secondary families remain accessible; no page horizontal scroll.  
**FUNCTIONAL REGRESSION CRITERIA:** Opening sets the exact current Job On; revisions and history stay immutable.  
**BLOCKERS:** None outstanding. Q-002 is resolved: the article image belongs to the master article/reference and is not owned per Job On revision; see `08_OWNER_DECISION_ARTICLE_IMAGE.md`. The technical schema representation of that master-reference image remains separate, later work.

