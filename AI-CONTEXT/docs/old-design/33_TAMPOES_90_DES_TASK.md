## DES-013 — Recompose Tampões (Owner-confirmed model)
**STATUS:** READY  
**DEPENDENCIES:** DES-001, DES-002  
**AUTHORITATIVE DESIGN FILES:** Tampões HTML/brief (Owner-confirmed)
**CURRENT SOURCE FILES:** Tampões Razor/JS/CSS and services
**FUNCTIONAL RULES TO PRESERVE:** Simple autonomous module; main configuration-quantity table (Máquina(s) + Diâmetro + Calote); one-click quantity actions; double-click configuration edit; append-only movements; derived balances; atomic quantity changes; optional quantity classifications; auditable history.
**EXACT PROBLEM:** The previous canonical topology carried a Planeamento area and an optional read-only Job On link, neither of which belongs to the Owner-confirmed functional model; the interaction must move to a single main table with direct configuration maintenance.
**IMPLEMENTATION SCOPE:** Canonical Registo/main table / Histórico / Opções(Configuração); main configuration-quantity table with Máquina(s) + Diâmetro + Calote; one-click selection + quick inline Add/Remove with optional category; double-click opens configuration edit (Diâmetro / Calote / Máquina(s), plus other configured fields); create new configuration; recent movements and history filters; options/config values management; responsive behavior.
**MUST PRESERVE:** Current functional data even when its presentation moves; editable configuration fields; optional quantity classifications; auditable movement and configuration-edit history.
**MUST NOT:** Associate to Reference/Production; integrate/mutate Job On; plan or reserve through Tampões; perform absolute client balance rewrites; delete movement history; model Máquina via the "Linhas" pagination field.
**EXPECTED FILES TO CHANGE:** Tampões Razor/JS/CSS and only proven read DTO additions.
**VERIFICATION:** Atomic movement regressions, configuration-edit regressions and all viewport screenshots per tab and state.
**VISUAL ACCEPTANCE CRITERIA:** Main-table composition, one-click/two-click interactions, proportional editors and table/card reflow match reference.
**FUNCTIONAL REGRESSION CRITERIA:** Every change remains a new movement and balances stay server-derived; configuration edits update the table without silent historical rewrites.
**BLOCKERS:** None.
