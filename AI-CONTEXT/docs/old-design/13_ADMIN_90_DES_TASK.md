## DES-004 — Recompose Admin
**STATUS:** READY  
**DEPENDENCIES:** DES-001, DES-002  
**AUTHORITATIVE DESIGN FILES:** `admin.html`, Login/Admin and audit handoffs  
**CURRENT SOURCE FILES:** Admin Razor/PageModels, admin CSS, shared interactions  
**FUNCTIONAL RULES TO PRESERVE:** `admin.gerir`, templates/overrides, profile-title separation, idempotent user creation, append-only audit.  
**EXACT PROBLEM:** Fragmented route pages do not form the canonical dedicated admin workspace and list/detail interactions are inconsistent.  
**IMPLEMENTATION SCOPE:** Recompose Users, Templates, Applications and Audit using common nav, filters, tables, external actions, confirmations and detail panels.  
**MUST PRESERVE:** Current services/routes and server validation.  
**MUST NOT:** Show auth UUID as Email; expose current passwords; equate title with permission.  
**EXPECTED FILES TO CHANGE:** Admin Razor/CSS/interaction wiring; only minimal PageModel display DTOs if proven.  
**VERIFICATION:** Authorization/CRUD regression tests and four-viewport screenshots for each area and state.  
**VISUAL ACCEPTANCE CRITERIA:** Dedicated admin shell and compact canonical tables match reference.  
**FUNCTIONAL REGRESSION CRITERIA:** Identity operations remain confirmed, audited and fail closed.  
**BLOCKERS:** None.

