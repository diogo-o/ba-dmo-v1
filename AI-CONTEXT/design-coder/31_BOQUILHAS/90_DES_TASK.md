## DES-011 — Recompose Boquilhas
**STATUS:** READY  
**DEPENDENCIES:** DES-001, DES-002  
**AUTHORITATIVE DESIGN FILES:** Boquilhas HTML/handoff  
**CURRENT SOURCE FILES:** Boquilhas Razor/JS and shared/module styles  
**FUNCTIONAL RULES TO PRESERVE:** BQ ownership, 20→25 discrepancy behavior, repairer vocabulary, no live Job On lookup.  
**EXACT PROBLEM:** Incomplete active-lot/history/settings/sidebar compositions and browser prompts.  
**IMPLEMENTATION SCOPE:** Canonical sidebar, inline lot creation, active summary/current state/movements, lot cards, calendar/filter history, correction/delete actions, line-repairer matrix and proper editors.  
**MUST PRESERVE:** Full production-reference display where supplied as context; immutable close snapshot.  
**MUST NOT:** Treat BQ as CM/MF repair, hard-block excess returns, auto-add unmatched quantities or infer Job On relationships from text.  
**EXPECTED FILES TO CHANGE:** Boquilhas Razor/JS/CSS and minimal read DTOs only after proving gaps.  
**VERIFICATION:** Movement/discrepancy/close regressions; screenshot comparison for every tab/state at all viewports.  
**VISUAL ACCEPTANCE CRITERIA:** Sidebar conflict/free/occupied cards, selected history and modal forms match reference.  
**FUNCTIONAL REGRESSION CRITERIA:** 20→25 remains non-blocking and BQ schema/domain stays independent.  
**BLOCKERS:** None.

