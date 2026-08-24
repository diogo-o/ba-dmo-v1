## DES-007 — Recompose Peso Operator
**STATUS:** READY  
**DEPENDENCIES:** DES-001, DES-005, DES-006  
**AUTHORITATIVE DESIGN FILES:** operator/print HTML, Peso handoff and glass-comparison decision  
**CURRENT SOURCE FILES:** Peso Index Razor/JS/CSS and Peso services  
**FUNCTIONAL RULES TO PRESERVE:** Server-only formulas/density; exact Job On revision; positional pairing; exact saved NNPB/PS value.  
**EXACT PROBLEM:** Legacy page composition and superseded comparison concepts obscure the canonical Job On-bound workflow.  
**IMPLEMENTATION SCOPE:** Recompose reference context, readings, per-CM glass-weight results, calculation/submit states, prior-production selection, comparison pairing and history/document actions.  
**MUST PRESERVE:** At least one reading, explicit submit, approved snapshot, two-decimal presentation.  
**MUST NOT:** Recalculate in JS; compare water/capacity/global averages; reselect CM/lote or fake a revision.  
**EXPECTED FILES TO CHANGE:** Peso Index Razor/JS/CSS and only proven display DTO additions.  
**VERIFICATION:** Formula/service regressions, immutable-context tests, screenshots at all viewports for new control, comparison, history and error/loading states.  
**VISUAL ACCEPTANCE CRITERIA:** Compact fields and per-CM results match operator mockup; comparison shows explicit current→previous CM pairs only.  
**FUNCTIONAL REGRESSION CRITERIA:** Calculation remains C#-only and snapshots retain exact inputs.  
**BLOCKERS:** None.

