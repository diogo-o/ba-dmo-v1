## DES-018 — Cross-module visual and functional acceptance pass
**STATUS:** READY AFTER DES-003–DES-017  
**DEPENDENCIES:** DES-003 through DES-017  
**AUTHORITATIVE DESIGN FILES:** all canonical HTML/handoffs listed in §2  
**CURRENT SOURCE FILES:** all changed presentation files and existing functional tests  
**FUNCTIONAL RULES TO PRESERVE:** All invariants in the functional SOT.  
**EXACT PROBLEM:** Module-local acceptance cannot prove shell consistency, cross-module context or responsive integrity.  
**IMPLEMENTATION SCOPE:** Capture and compare screenshots for every major state at 1440×900, 980×900, 720×900 and 375×812; run functional regression; verify cross-links and current-context transitions.  
**MUST PRESERVE:** Authorization, routing, module ownership, server calculations, exact revision context, append-only history/corrections, antiforgery, server actor, immutable Job On history, complete identifiers, CM/MF-only RI.  
**MUST NOT:** Accept build/tests as visual proof, redesign schema, or waive mismatches based on previous completion claims.  
**EXPECTED FILES TO CHANGE:** Visual baselines/tests and focused presentation fixes only.  
**VERIFICATION:** Screenshot diff review plus full relevant test suite and manual keyboard walkthrough.  
**VISUAL ACCEPTANCE CRITERIA:** Geometry, headers, navigation, side panels, titles, cards, tabs, forms, lists, tables, dialogs, selection, empty/loading/error states and responsive behavior match authority at all four sizes.  
**FUNCTIONAL REGRESSION CRITERIA:** Explicitly prove: `5447T173` remains complete; BQ is context-only in RI; Controlo uses current-open Job On without a second selector; Boquilhas/Ferramentas domains remain separate; no schema assumption drives UI.  
**BLOCKERS:** None outstanding. Q-001 and Q-002 are both resolved; see `30_FERRAMENTAS_03_OWNER_DECISION_SAP_UTILISATION.md` and `20_JOB_ON_08_OWNER_DECISION_ARTICLE_IMAGE.md`.

