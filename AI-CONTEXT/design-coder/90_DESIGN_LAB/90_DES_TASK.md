## DES-017 — Complete DesignLaboratorio and visual regression harness
**STATUS:** READY  
**DEPENDENCIES:** DES-001, DES-002  
**AUTHORITATIVE DESIGN FILES:** implementation contract and design system  
**CURRENT SOURCE FILES:** DesignLaboratorio Razor and shared CSS/JS  
**FUNCTIONAL RULES TO PRESERVE:** No domain behavior or authorization decisions.  
**EXACT PROBLEM:** Missing complete responsive/sticky/nav/sidebar/keyboard/failure demonstrations; language and embedded-overlay presentation are inconsistent.  
**IMPLEMENTATION SCOPE:** Normalize labels and add explicit examples for every universal component/state and required breakpoint.  
**MUST PRESERVE:** Token-only/shared-component consumption.  
**MUST NOT:** Add module-specific CSS or fake persisted success.  
**EXPECTED FILES TO CHANGE:** DesignLaboratorio Razor and shared visual tests only.  
**VERIFICATION:** Automated screenshot baselines at 1440×900, 980×900, 720×900 and 375×812 plus keyboard/accessibility scan.  
**VISUAL ACCEPTANCE CRITERIA:** All components/states can be assessed without entering a business module.  
**FUNCTIONAL REGRESSION CRITERIA:** Laboratory remains non-operational.  
**BLOCKERS:** None.

