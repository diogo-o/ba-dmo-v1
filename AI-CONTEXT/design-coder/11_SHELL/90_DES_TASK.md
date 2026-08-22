## DES-002 — Normalize Shell and two-level navigation
**STATUS:** READY  
**DEPENDENCIES:** DES-001  
**AUTHORITATIVE DESIGN FILES:** design system, Login/Admin handoff, all canonical operational headers  
**CURRENT SOURCE FILES:** Shared layout/header/navigation/admin-nav and layout CSS  
**FUNCTIONAL RULES TO PRESERVE:** Capability-derived navigation, operational landing Job On, pure-admin landing/Admin-only shell.  
**EXACT PROBLEM:** Header metadata, nav classes, sticky stacking and responsive behavior vary.  
**IMPLEMENTATION SCOPE:** Standardize header anatomy, primary/secondary bars, active states, account control, desktop/tablet/mobile stacking.  
**MUST PRESERVE:** Server-side grant resolution, fail-closed gates, access-denied feedback.  
**MUST NOT:** Infer authorization from profile title or client visibility.  
**EXPECTED FILES TO CHANGE:** Shared partials and shared/admin layout CSS.  
**VERIFICATION:** Route/navigation tests and screenshot comparisons at all viewports for operational and pure-admin users.  
**VISUAL ACCEPTANCE CRITERIA:** Logo, page identity, user identity and both nav levels remain readable and unobscured at every viewport.  
**FUNCTIONAL REGRESSION CRITERIA:** Unauthorized modules never render; admin isolation remains intact.  
**BLOCKERS:** None.

