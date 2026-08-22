## DES-003 — Align Login
**STATUS:** READY  
**DEPENDENCIES:** DES-001  
**AUTHORITATIVE DESIGN FILES:** `login.html`, Login/Admin handoff  
**CURRENT SOURCE FILES:** Login Razor/PageModel and shared auth styling/script  
**FUNCTIONAL RULES TO PRESERVE:** Antiforgery, generic errors, no role choice, server routing.  
**EXACT PROBLEM:** Test-environment notice is forbidden; remaining geometry/states need canonical responsive treatment.  
**IMPLEMENTATION SCOPE:** Remove notice; tune split layout, form spacing, loading, error and password reveal states.  
**MUST PRESERVE:** Credentials are never repopulated; submit locking.  
**MUST NOT:** Display test credentials or reveal email existence.  
**EXPECTED FILES TO CHANGE:** Login Razor and shared login CSS/JS.  
**VERIFICATION:** Auth flow tests plus four viewport screenshots including invalid and submitting states.  
**VISUAL ACCEPTANCE CRITERIA:** Desktop split and mobile stack match reference without excess vertical void.  
**FUNCTIONAL REGRESSION CRITERIA:** Operational/admin redirects remain capability-correct.  
**BLOCKERS:** None.

