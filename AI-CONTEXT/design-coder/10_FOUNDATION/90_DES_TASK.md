## DES-001 — Close the shared visual foundation
**STATUS:** READY  
**DEPENDENCIES:** None  
**AUTHORITATIVE DESIGN FILES:** design system CSS/docs and implementation contract  
**CURRENT SOURCE FILES:** `wwwroot\styles\dmo-tokens.css`, `dmo-foundation.css`, `dmo-components.css`, `dmo-layout.css`, `dmo-utilities.css`; shared scripts  
**FUNCTIONAL RULES TO PRESERVE:** Components contain no domain logic; server authorization remains authoritative.  
**EXACT PROBLEM:** Shared primitives exist but are not consistently consumed and line-colour tokens remain placeholders.  
**IMPLEMENTATION SCOPE:** Normalize token consumption, focus/keyboard states, list/table/calendar/dialog contracts, sticky layers, responsive gutters, local overflow and reduced motion.  
**MUST PRESERVE:** Existing class aliases during migration; canonical event contracts.  
**MUST NOT:** Put module business rules in CSS/JS; use colour alone; introduce page-wide horizontal scrolling.  
**EXPECTED FILES TO CHANGE:** Shared CSS/scripts and focused component tests.  
**VERIFICATION:** Component tests, accessibility checks, keyboard walkthrough, screenshots at all four required viewports.  
**VISUAL ACCEPTANCE CRITERIA:** Buttons are filled then invert on hover/focus; fields, cards, lists, tables, calendar, dialogs, empty/loading/error states and responsive overflow match the reference.  
**FUNCTIONAL REGRESSION CRITERIA:** Shared events keep stable IDs and do not submit commands.  
**BLOCKERS:** None.

