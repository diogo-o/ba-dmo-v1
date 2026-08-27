# Manual vs old-design — PASS 1

## 1. Scope and authority used

This is a source-comparison pass only. It compares:

- the current functional reference set under `Manual/`, starting with `Manual/00_INDEX.md`;
- the existing curated design package under `old-design/`, starting with `old-design/00_INDEX.md`.

No application implementation, database, migrations, Git history, or Maps were used. No rendered screenshot acceptance is claimed in this pass.

The Manual is the functional authority for this comparison. The old-design HTML files are evaluated as composition/visual-language candidates, not as business-rule authorities. Demo data, demo JavaScript, old task contracts and package-readiness claims do not override the Manual.

The Manual itself is not fully decision-complete: Job On records 7 owner questions plus 3 design-reconciliation items; Ferramentas records 6 owner questions; Armazém records 4; and Job On §6.1 conflicts explicitly with Tampões §16 over the meaning/relationship of TP/Tampão. Those items must remain unresolved and must not be silently fixed by the design.

## 2. Executive result

The old-design package has a coherent and reusable visual language, but it is not a clean functional match for the current Manual.

The reusable core is strong:

- canonical BA shell, header, two-level navigation, cards/surfaces and compact toolbars;
- one-click selection and double-click open/edit patterns;
- compact tables, external selection actions, pagination and calendars;
- inline expandable forms, contextual summaries, status pills and timelines;
- responsive reflow, internal table scrolling and module-specific side panels;
- dedicated visual compositions for all current modules and the principal Controlo sub-areas.

The main corrections are structural rather than stylistic:

1. There are exactly nine assignable modules: Job On, Controlo, Ferramentas, Armazém, Boquilhas, Reparação Interna, Reparação Externa, Tampões and Admin.
2. Peso, Comparação, Pegamentos, Resumo/Folha and Histórico do Controlo must remain inside the single Controlo module. Comparação is a workflow inside Peso, not a peer module/tab at top level.
3. História is a transversal read-only audit surface, not an assignable module and not a permanent primary-navigation item.
4. Admin access is based on one or more templates associated to each user, kept separate from the functional profile. The current Admin HTML supports only one template and does not expose the required functional-profile selection.
5. The Peso HTML still contains old ownership and comparison models. It creates/edits references and lots, permits context reconstruction and presents global/capacity comparison blocks which the Manual supersedes.
6. Pegamentos contains substantial demo-derived business logic (`ovalMax`, `gapTol`, neighbour-boundary and spacing rules) that the Manual explicitly says must not be promoted to functional authority.
7. The old Armazém package README says BQ and programmed external flow are out of scope, but the current Manual makes BQ normal warehouse scope and Saídas Programadas functionally current. The HTML is closer to the Manual than its own README.
8. Several HTML files use native `confirm`, `prompt` or `alert`, fake client persistence and hard-coded client decisions. These are demo mechanics and must not be carried into the next design authority.

Overall classification:

| Area | Pass 1 result |
|---|---|
| Global shell / Login | ADJUST |
| Job On | RESTRUCTURE |
| Controlo shell / Resumo | RESTRUCTURE |
| Peso — Operador | RESTRUCTURE |
| Peso — Responsável | RESTRUCTURE |
| Pegamentos | RESTRUCTURE |
| Ferramentas | RESTRUCTURE |
| Armazém | ADJUST |
| Boquilhas | ADJUST |
| Reparação Interna | ADJUST |
| Reparação Externa | ADJUST |
| Tampões | KEEP WITH ONE CROSS-MODULE HOLD |
| Admin | RESTRUCTURE |
| História transversal | RESTRUCTURE CLASSIFICATION/NAVIGATION |
| Design Laboratório | RESTRUCTURE AS NON-OPERATIONAL LAB |

`KEEP WITH ONE CROSS-MODULE HOLD` means the Tampões composition already represents its own Manual accurately, but the Job On ↔ TP/Tampão conflict must be resolved before final cross-module design freeze.

## 3. Canonical visual language to retain

The next design pass should continue using the language established by:

- `old-design/0_ASSET_CANONICAL_DESIGN_SYSTEM.css`;
- `old-design/0_ASSET_CANONICAL_INTERACTIONS.js`;
- `old-design/0_GLOBAL_DESIGN_SYSTEM.md`;
- the current canonical module HTML files listed in §5.

New or corrected screens should use the existing patterns rather than introduce a parallel style:

- persistent BA header and capability/template-derived primary navigation;
- module tabs for internal areas only, with settings/options right-aligned where applicable;
- page header followed by compact filters/actions, context summaries and one dominant work surface;
- cards for focused context, tables for repeated facts and a side panel only where continuous operational context is useful;
- one click selects, double click opens, and selected-row actions live outside the table;
- extensive creation/editing inline; focused confirmation in application dialogs;
- status text plus semantic colour, never colour alone;
- loading, empty, no-results, error and forbidden states as distinct reusable components;
- no page-wide horizontal scrolling; responsive reflow before reducing information density;
- no native browser `alert`, `confirm` or `prompt`.

## 4. Global and transversal comparison

### 4.1 Shell, navigation, profiles and access

**Already represented correctly**

- `11_SHELL_00_README.md` correctly describes a shared shell, server-derived navigation, pure-Admin isolation and fail-closed access.
- The module HTML files consistently use the same header, primary navigation, secondary tabs, cards, filters and table language.
- Free-text header titles such as “Metrologia” or “Responsável de qualidade” can remain as display labels provided they are never treated as profiles or permission evidence.

**Missing from the design**

- A single explicit shell composition showing the complete rule: profile determines the experience inside an assigned module; associated templates determine which modules exist in navigation and are directly reachable.
- A neutral Forbidden state for a deep-link to a non-granted module.
- A consistent account/logout control in the authenticated shell.
- A consistent distinction between primary module navigation and internal-area tabs. Several old pages visually blur that boundary.

**Obsolete or misleading**

- Any design catalogue that treats Peso, Pegamentos or História as separately assignable modules.
- Any header text that visually implies “Metrologia”, “Reparador de turno”, “Chefe” or similar is a fourth functional profile.
- Static demo navigation must not be interpreted as an access list.

**Add using the current visual language**

- Keep the existing header/tabs geometry, but generate primary entries from associated-template grants.
- Represent the three profiles explicitly only where profile is edited or explained; keep free-text title as a separate secondary label.
- Use the existing empty/error-card language for Forbidden and no-context states.

**Real authority**

- No single Shell HTML is authoritative. The authority is the global design system plus the canonical headers/navigation patterns repeated across the module HTML files.

### 4.2 Login

**Already represented correctly**

- `12_LOGIN_01_VISUAL_AUTHORITY_login.html` is the real visual authority.
- The desktop split, mobile stack, credential form, password reveal control and focused sign-in action fit the Manual and existing language.

**Missing**

- Generic authentication error, loading/submit lock and a clear server-decided landing outcome.

**Obsolete**

- The “Ambiente de testes” notice.
- Demo routing based on whether the typed email contains `admin`.
- Any implication that the user selects a role at login.

**Add/refine**

- Remove the test notice without replacing it with operational copy.
- Use the existing inline-error and loading-button patterns; leave authentication and landing selection to the server.

## 5. Module-by-module comparison

### 5.1 Job On

**Functional classification**

Top-level assignable module and operational landing. It owns the production/planning context, stable Job On identity, exact revisions/snapshots, production-specific configuration, verification occurrences and production print projection.

**Real visual authorities**

- `20_JOB_ON_01_VISUAL_AUTHORITY_job-on.html` — main visual authority, but requires functional restructuring.
- `20_JOB_ON_02_VISUAL_AUTHORITY_PRINT_job-on-4-pages.html` — print authority, subject to field/image reconciliation.

**Already represented correctly**

- Central calendar/planning surface, day selection and list of productions.
- One-click select and double-click open behaviour.
- Exact Reference + Production + Machine/Line + revision context.
- Consultation/edit modes, new revision save, duplication and history.
- CM/MF/BQ selection context and read-only warehouse/technical context in the picker.
- Production-specific PU, CS, TP, Pinças and Calibres are visually present.
- Verification confirmations, history and four-page print composition are represented.
- Full references such as `5774T173` are preserved.

**Manual requirements missing or not safely resolved**

- The selector must visibly make Machine/Line a filter/context attribute, not part of the tool/lot business identity.
- “Novo em branco”, “Duplicar anterior”, Job On lifecycle statuses, family expansions/required fields, verification cancellation/completion rules, stock-vs-quantity meanings and Tipo-vs-Processo remain owner questions. The HTML currently presents answers for several of them as if settled.
- `% usage` snapshot timing, past-calendar movement source and lot eligibility are design-reconciliation items and must not be frozen from demo values.
- The role variant needs a clear read-only Operador experience and an edit/configure Responsável experience without relying on display title.

**Obsolete or misleading**

- The old package rule that tool identity includes `Type + Reference + Lot + Machine/Line`; the Manual explicitly says Machine/Line is registered context and filtering, not a composite identity.
- The embedded Job On “Controlo” workspace duplicates an independent top-level module. Job On may link to or summarise Controlo results, but must not own a second Controlo implementation.
- “Substituir imagem” in the Job On production editor implies Job On edits the master article/reference image. The Manual assigns image ownership to the master reference.
- The print HTML repeats the article image on pages 1, 2 and 4. The Manual does not settle page allocation; the old owner-decision file says the image appears only on the required Job On print sheet. Do not carry the repetition forward without explicit reconciliation.
- Demo lifecycle pills such as `Pronto`, `Planeado`, `Em fabrico` are not established as the final status set.

**Add/refine using the existing language**

- Retain the calendar + production list + loaded-context card.
- Convert the embedded Controlo tab into a compact cross-module summary with explicit “Abrir Controlo” navigation.
- Keep master-owned fields read-only in the production surface; place production-specific fields in the existing family-card grid.
- Add “Por confirmar” states for unresolved field meanings rather than manufacturing final options.
- Keep the four-page print geometry, but reconcile exact sheet names, fields and the single permitted article-image placement before freeze.

**Supporting/history not required as visual authority**

- `20_JOB_ON_03_BRIEF_JOB_ON.md`, `20_JOB_ON_04_DATA_CONTRACT_JOB_ON.md`, `20_JOB_ON_05_BRIEF_VERIFICATIONS.md`, `20_JOB_ON_06_HANDOFF_PRINT.md` and owner-decision files are supporting requirement/provenance material, not visual authorities.
- The absent `job-on-v48-folha-producao.html` must not be substituted or reconstructed from a similar file.

### 5.2 Controlo — module shell, Resumo and workflow

**Functional classification**

One top-level assignable module. Peso, Pegamentos, Resumo/Folha and Histórico are internal areas. Comparação is a workflow inside Peso.

**Real visual authorities**

- `21_CONTROLO_01_VISUAL_AUTHORITY_controlo.html` — Controlo shell/Resumo authority, with structural changes required.
- The Peso and Pegamentos HTML files below are sub-authorities inside this same module, not separate modules.

**Already represented correctly**

- Exact inherited Job On/revision context.
- Resumo covering exactly CM, MF, BQ, PU and CS.
- Per-piece technical result, comment and MCaliper link.
- Peso and Pegamentos entry points.
- Internal history with structured documents and append-only events.
- Technical result is visually distinct from approval state.

**Missing**

- Explicit no-context state “Nenhum Job On carregado”.
- Case B: selecting another valid lot as the subject of a control without changing production tooling.
- Complete Rascunho → Submetida → Aprovada/Rejeitada → Reaberta actions, with the correct Controlador vs Responsável division.
- Explicit document-send confirmation and Machine/Line-oriented destination context.
- A clear non-blocking warning treatment for NOK/tolerance results.

**Obsolete or misleading**

- Comparação as a peer tab beside Peso. It belongs inside Peso.
- Any “free mode” that becomes a second production selector or creates a second production/tooling configuration. The Manual only confirms a no-context state and Case B control-subject selection.
- Treating the separate Peso/Pegamentos HTML pages as independently assignable modules.

**Add/refine**

- Preserve the current context strip and five-piece Resumo cards.
- Nest Comparação inside the Peso view while retaining its existing focused comparison composition.
- Use the existing inline alert/card pattern for no context and Case B subject selection.
- Add a state/action bar that switches by functional profile without changing the module identity.

### 5.3 Peso — Operador / Controlador

**Real visual authorities**

- `22_PESO_OPERADOR_01_VISUAL_AUTHORITY_peso-operador.html` — visual sub-authority; major pruning required.
- `22_PESO_OPERADOR_02_VISUAL_AUTHORITY_PRINT_peso.html` — Peso print sub-authority.

**Already represented correctly**

- Exact Job On/revision context and inherited CM + Lot are visible.
- Temperature, readings, per-CM Capacity/Volume and glass-weight results are represented.
- Direct current-to-approved-prior CM pairing is present.
- Comparison by per-CM final glass weight, without using the global average as the comparison key, is represented in the newer embedded comparison block.
- History, document actions and responsive layouts use the established language.

**Missing**

- The current Manual’s explicit Tampão/calote informational value and formula must be represented separately from PU and from Job On TP/Tampão.
- The initial-control flow should expose both per-CM values and the informative global glass-weight average, then submit the set for general Responsável decision.
- The screen needs to make clear which inputs feed calculation and which are supporting context.

**Obsolete or misleading**

- `Referências`, reference creation/editing, first-lot creation, machine compatibility and “processo do lote” management inside Peso. These are not Peso-owned functions in the current Manual.
- Any CM/Lot re-selection or “Editar referência” inside Peso; the CM + Lot is inherited from the exact Job On/Controlo context.
- A separate top-level Comparações page or second production context.
- Client-side formula/demo calculation as authority. Calculated results are authoritative only after server validation/persistence.
- Local-browser directory/synchronisation concepts presented as data authority.

**Add/refine**

- Keep the measurement/result cards and embedded per-CM comparison table.
- Remove the reference/master-management views and make the inherited context compact and read-only.
- Add a small informational Tampão/calote result card using the same result-card language, clearly labelled “informativo; não entra no cálculo principal”.

### 5.4 Peso — Responsável

**Real visual authority**

- `23_PESO_RESPONSAVEL_01_VISUAL_AUTHORITY_peso-responsavel.html` — visual sub-authority; decision detail requires correction.

**Already represented correctly**

- Calendar/queue/detail composition.
- General approve/reject surface for initial control.
- Per-CM “Manter / Colocar de parte” decisions for Comparação.
- Justification becomes mandatory when at least one measured CM is set aside.
- The approved prior snapshot remains intact.

**Missing**

- Clear separation between initial-control general decision and comparison per-CM decisions.
- Reopen-to-draft action and preserved decision history where the shared Controlo workflow applies.

**Obsolete or misleading**

- “Comparação global de peso”, average SAP comparisons, capacity-current/capacity-previous and volume-difference blocks as decision criteria. The Manual requires direct approved per-CM final glass-weight pairs for Comparação.
- Capacity/global-average comparison language remains in the HTML even though the module README already marks it superseded.

**Add/refine**

- Retain the queue, identity strip and per-CM decision table.
- Remove the global/capacity comparison sections and use the saved per-CM values already shown in the lower comparison detail.

### 5.5 Pegamentos

**Real visual authority**

- `24_PEGAMENTOS_01_VISUAL_AUTHORITY_pegamentos.html` — visual sub-authority for layout and measurement entry only; its derived rule engine is not authoritative.

**Already represented correctly**

- Exact Job On context and independent CM/BQ/MF measurement sections.
- Costura (0°), Contra costura (90°), signed Ovalização and Média fields.
- Dense measurement tables, visual result states, history and print composition.

**Manual requirements missing or obscured**

- Each component must use its own nominal and the written `Nominal − 0.20` to `Nominal + 0.20` corridor.
- Equality at a limit is an alert.
- Costura, Contra costura and Média are checked independently; Média cannot hide a bad individual reading.
- Alerts are advisory and do not automatically stop production.

**Obsolete or unsupported**

- A separate `ovalMax = 0.20` acceptance rule.
- `gapTol = 0.05` as a business rule.
- Expected inter-component spacing and “CM → BQ / BQ → MF” gap warnings.
- Using neighbouring component nominals as business boundaries.
- The “Mapa de limites — lógica do ficheiro original” as validation authority.
- Destructive local-browser reference deletion/import/reset and native `confirm` dialogs.

**Add/refine**

- Keep the existing measurement-grid and chart visual language, but drive status only from the Manual’s written per-component nominal corridor and independent readings.
- If the map remains, label it explicitly as an informational projection and remove unsupported spacing/ovalization verdicts.
- Replace native dialogs with the canonical confirmation component.

### 5.6 Ferramentas

**Functional classification**

Top-level module and master owner for tool identity/technical data. CM, MF, BQ, PU and CS are tool families; the Manual documents CM/MF in detail and leaves exact fields for all families partly open.

**Real visual authority**

- `30_FERRAMENTAS_01_VISUAL_AUTHORITY_ferramentas.html` — visual authority, but incomplete family coverage.

**Already represented correctly**

- Focused list/detail workspace with Referência, Lotes, Verificações, Utilização and Histórico.
- CM as canonical name and MP as legacy import alias only.
- Separate master/reference and lot views.
- Technical states, manual SAP utilisation and append-only usage history.
- Verification rules live on the lot; Job On materialises occurrences.
- “Novo lote a partir deste” copies configuration, not occurrences/history.

**Missing**

- At least a family-level navigation/filter path for BQ, PU and CS without inventing their still-open field sets.
- Machine/Line as registered lot context, potentially multi-value, clearly separated from identity.
- Explicit role variants: Operador consultation/operational correction versus Responsável master/technical-state editing where established.
- Clear cross-links to warehouse location and repair history without transferring ownership.

**Obsolete or misleading**

- The header says Ferramentas is only CM/MF configuration; that is narrower than the Manual.
- “Compatibilidade B1–C3” on the reference card risks implying inferred compatibility or reference-level identity; the Manual requires registered Machine/Line context and no inference.
- Reset behaviour for verifications is not owner-confirmed in the Manual and must not be frozen from demo text.
- Legacy SAP fields other than the current manual `% usage` are historical implementation evidence, not required design fields.

**Add/refine**

- Reuse the current left list and tabbed detail; add family filter/identity labels for all five tool families.
- For families whose exact fields are unresolved, show only the confirmed common core and an honest “Por confirmar” state.
- Keep manual utilisation and verification tabs, but do not settle the six open Ferramentas questions in HTML.

### 5.7 Armazém

**Real visual authority**

- `32_ARMAZEM_01_VISUAL_AUTHORITY_armazem.html` — authority; the HTML is materially closer to the Manual than `32_ARMAZEM_00_README.md`.

**Already represented correctly**

- Registo, Consulta, Saídas programadas and Histórico areas.
- CM/MF/BQ selectors, four-digit positions, recent movements and search.
- BQ is present as a normal warehouse tool.
- Production-to-warehouse `% uso` reminder is shown without automatic calculation.
- Programmed exit list with item-level physical confirmation.
- Double-click opens the responsible tool detail; correction actions are outside tables.

**Missing**

- Exit destinations must be exactly Fabricação, Reparação and Sucata unless the owner resolves otherwise; the HTML uses `Outros` instead of Sucata.
- Repairer must be selected from the canonical directory for Saída → Reparação, not entered as free-form destination text.
- The authorised Responsável maintenance route for an existing BQ/Lot needs an explicit, bounded edit/read presentation while preserving Ferramentas ownership.
- Role-specific action distribution remains one of the four open Armazém questions and must not be over-specified.
- The distinction “Destino operacional ≠ Estado técnico” needs a persistent explanatory treatment near the movement form.

**Obsolete or misleading**

- `32_ARMAZEM_00_README.md` statements that BQ and programmed external flow are out of current scope. The Manual makes both functionally current.
- The README’s `MUST NOT direct tool-domain edits` needs refinement: an authorised Responsável may enter a Ferramentas-owned edit surface from Armazém; UI entry point does not transfer ownership.
- Any normal `Substituir` warehouse action remains obsolete.
- Native `confirm` and fake “prepared” toast logic are demo mechanics only.

**Add/refine**

- Keep the existing four-tab composition.
- Replace the destination free text with a destination selector plus contextual registered repairer/machine fields.
- Use a linked Ferramentas detail drawer/card for authorised master edits, visually branded as Ferramentas-owned data.
- Preserve “warning, never silent normalisation” for location conflicts.

### 5.8 Boquilhas

**Real visual authority**

- `31_BOQUILHAS_01_VISUAL_AUTHORITY_boquilhas.html` — authority; good functional coverage with targeted corrections.

**Already represented correctly**

- Registo, Boquilhas, Histórico and Definições plus the B1–C3 side panel.
- Select existing / create missing inline flow.
- Editable opening date with default date control.
- Multi-line selection during creation.
- Saída, Entrada, Não reparadas, Corrigir contagem and close actions.
- Movement balance column, history, repairer-by-line configuration and immutable close-snapshot concept.
- Same overall composition can serve Operador and Responsável because the Manual confirms identical Boquilhas actions.

**Missing**

- Saída needs an explicit repairer selection from the configured/canonical repairer list.
- The module purpose should consistently say “movimentos de reparação externa de BQ”, not generic BQ management.
- Return-overage discrepancy needs a visible non-blocking warning and preserved full quantity.
- The existing-record maintenance boundary should link to Armazém for Responsável rather than expose generic master editing here.

**Obsolete or misleading**

- Generic lifecycle states/actions for the BQ master: `Sucata` filter, general archive/scrap/restore meaning and unrestricted `Editar ficheiro`.
- “Editar ficheiro” is only correction of the repair-flow record; it is not reference/lot/line master editing.
- “Imprimir / Guardar PDF” has no currently confirmed document output in the Manual.
- “Responsável · Metrologia” must not look like an additional profile or special Boquilhas variant.
- Native confirmation dialogs are demo mechanics.

**Add/refine**

- Retain the side panel, active-lot summary and history layout.
- Rename generic lifecycle labels around the repair trace; keep “Arquivado” only as the result of closing a trace.
- Remove the master `Sucata` interpretation and unconfirmed PDF action.
- Use the existing warning/card language for the 20→25 over-return case.

### 5.9 Reparação Interna

**Real visual authority**

- `34_REPARACAO_INTERNA_01_VISUAL_AUTHORITY_reparacao-interna.html` — authority; the Manual explicitly identifies it as pre-final layout and says BQ UI detail is superseded.

**Already represented correctly**

- Fast B1–C3 line selection, full production reference and exact production context.
- CM/MF-only type selection.
- Authenticated repairer, own recent records, Consulta, correction and annulment flows.
- Missing Job On context does not block registration.
- 06:00/09:00 context rule is explained.
- Append-only correction intent and Job On-linked read-only consultation are represented.

**Missing**

- The visible registration context must be reduced to Line + full Reference + Production; Job On ID, revision, lot and stable IDs remain internal.
- The role boundary needs to be unmistakable: Operador/Controlador writes own occurrences; Responsável only gets the additional production-level read-only view through Job On.
- The no/ambiguous-context state should say that registration remains allowed without inventing an association.

**Obsolete or misleading**

- Per-line visible `CM · BQ · MF` breakdown. BQ may remain only as part of the complete production/reference identity; it is never an RI repair input.
- Any wording that suggests choosing a production manually. The user selects the line; RI resolves the applicable context.
- Any `Editar contexto` control, repair lifecycle/state machine, `Por reparar/Reparado` mutation or cross-dependency with Boquilhas.
- The linked Job On production query is a deferred delivery detail, not a completed design claim.

**Add/refine**

- Keep the line cards and rapid-entry rhythm, but show only the Manual’s visible context fields.
- Keep CM/MF segmented buttons and recent-own-record actions.
- Present the Responsável production consultation as a read-only Job On surface, not a second RI operating variant.

### 5.10 Reparação Externa

**Real visual authority**

- `35_REPARACAO_EXTERNA_01_VISUAL_AUTHORITY_moldes.html` — real visual authority.

**Supporting only**

- `35_REPARACAO_EXTERNA_02_SUPPORTING_LIFECYCLE_reparacao-externa-v1.html` — lifecycle reference only; not a composition target.

**Already represented correctly**

- Responsável-focused CM/MF batch preparation from a future planned production.
- Registo, Ferramentas, Histórico and Definições.
- Separate CM and MF selectors/drafts; no mixed batch.
- Tool picker, repairer, planned date and programmed lists.
- Programmed list state/progress, history and repairer configuration.
- No BQ tab in the canonical `moldes.html`.

**Missing**

- Make explicit that the batch remains editable by the Responsável in every lifecycle state.
- Fully express item-by-item warehouse confirmation, partial return and complete return without duplicating warehouse ownership.
- Keep repairer filtering by type and Machine/Line and preserve the chosen repairer snapshot.
- Do not add print/PDF until confirmed; it is explicitly not specified.

**Obsolete or misleading**

- Any “honest deferred BQ area” inside Reparação Externa. BQ is not an area/type here; its flow belongs to Boquilhas.
- Any claim that current active production is the source. The batch starts from a future planned production.
- Any state-based removal of edit capability.
- Any separate Envios tab; Envios is the batch lifecycle.
- `CancelarLista` remains deferred.

**Add/refine**

- Retain `moldes.html` composition and change the page identity to “Reparação Externa” with CM/MF batch wording; “Moldes” can remain a descriptive sublabel.
- Add the missing partial-return and warehouse-confirmation states using the existing programmed-list/detail patterns.

### 5.11 Tampões

**Real visual authority**

- `33_TAMPOES_01_VISUAL_AUTHORITY_tampoes.html` — real visual authority and the closest match to its Manual module.

**Already represented correctly**

- Autonomous Registo / Histórico / Opções structure.
- Main configuration/quantity table with Máquina(s), Diâmetro and Calote.
- One-click quantity actions and double-click configuration editing.
- New configuration creation, multi-machine values and editable option catalogues.
- Optional quantity categories, quantity movement and append-only history.
- No Planeamento, Job On, Production or Reference UI.

**Missing or held**

- No module-local functional gap was found in Pass 1.
- Cross-module hold: Job On §6.1 calls TP/Tampão production-specific Job On configuration while Tampões §16 says no Job On/Production/Reference relationship. Do not add cross-module integration to this HTML and do not remove Job On’s production field until the Owner reconciles the two concepts.

**Obsolete**

- Any historical `Planeamento`, reservation or Job On link.
- `tampoes-v38-standalone.html` remains superseded and is correctly absent from this package.

### 5.12 Admin

**Real visual authority**

- `13_ADMIN_01_VISUAL_AUTHORITY_admin.html` — authority; user/access editor requires major restructuring.

**Already represented correctly**

- Dedicated pure-Admin shell.
- Users, Templates de acesso, Aplicações and Auditoria internal areas.
- User list, create/edit modal, active/inactive state and password-reset action.
- Template list/detail and server-capability explanation.
- Applications availability/order surface.
- Append-only audit table with filters, selection, double-click detail and export action.
- Free-text header title is visually separated and described as non-authorising.

**Missing**

- Explicit functional-profile selector with exactly Admin, Operador/Controlador and Responsável.
- Association of one or more templates per user, with add/remove/change behaviour. The HTML offers a single `Template de acesso` dropdown.
- Separate name, email, functional profile, free title, active state and associated-templates controls.
- Last-active-admin self-lockout protection state/feedback.
- Applications copy must explain that availability/order is display/catalogue management and never grants access.

**Obsolete or misleading**

- The template detail lists `Peso` as a module and labels the template by a module count that includes internal areas. Templates must grant the single Controlo module; Peso/Pegamentos are internal variants/areas.
- A single template per user.
- Omitting the functional profile while using templates named “Operador”/“Responsável” risks collapsing profile and access.
- Native `confirm` for password reset.
- Any interpretation that Applications configuration itself grants module access.
- Any top-level História assignment.

**Add/refine**

- Keep the current four-tab Admin workspace.
- Expand the user editor with a three-profile selector and a reusable multi-template association control (searchable checklist/chips) using current field/card patterns.
- In template editing, list only the nine true modules; module-internal capabilities may remain subordinate detail.
- Replace native reset confirmation with the canonical application dialog.

### 5.13 História transversal

**Functional classification**

Read-only transversal audit/history surface, not a top-level assignable module.

**Visual source**

- `36_HISTORIA_01_VISUAL_AUTHORITY_historia.html` is usable as the visual authority for the transversal history surface only. It is not authority for module classification or primary-navigation placement.

**Already represented correctly**

- Read-only search/filter, entity list, selected context and readable timeline.
- Factual history with exact revision context.
- Clear distinction from Admin Auditoria and no ranking/scoring.

**Missing**

- Visibility must be explained and represented as the intersection of the user’s granted modules; administrative events remain separately gated.
- Pagination, before/after correction detail and standard loading/empty/error states.

**Obsolete or misleading**

- The active `História` entry in primary module navigation.
- `36_HISTORIA_00_README.md`, DES-016 task/acceptance and Design Lab cards that call História “Módulo 90”.
- The module filter must not present Peso/Pegamentos as independently granted modules; they can remain event-source/internal-area filters under Controlo.

**Add/refine**

- Keep the focused list/timeline composition, but enter it from authorised history/audit links rather than treating it as a tenth assignable module.
- Label event sources with parent module context, for example `Controlo · Peso`.

### 5.14 Design Laboratório

**Functional classification**

Permanent technical design-validation/regression surface; not a business module, not assignable and not part of daily operational navigation.

**Real authority**

- There is no primary module HTML authority.
- `90_DESIGN_LAB_02_DESIGN_SYSTEM.md` and `90_DESIGN_LAB_03_IMPLEMENTATION_CONTRACT.md` are the design authority.
- `90_DESIGN_LAB_01_SUPPORTING_COMPONENT_REVIEW.html` is supporting/historical only.

**Already represented correctly**

- The package has a shared design-system specification and a component review entry point.
- The review grid demonstrates the common visual language and links to module examples.

**Missing**

- A real component/state laboratory covering header, both navigation levels, tables, keyboard states, side-panel/drawer, forms, application dialogs, toasts, loading, empty, no-results, error, forbidden and responsive reflow.
- It must remain demo-only and must never claim persisted business success.

**Obsolete or misleading**

- The current component-review HTML contains stale module classifications and descriptions: História as “Módulo 90”, separate Peso/Pegamentos module numbering, Reparação Externa linking to its supporting rather than canonical HTML, and Tampões described with Planeamento.
- “Plans + Design-update”, “Recovery” and old completion labels are historical audit prose, not current product language.

## 6. Visual-authority register

### 6.1 Carry forward as visual authorities, with the corrections above

| Scope | HTML authority |
|---|---|
| Login | `12_LOGIN_01_VISUAL_AUTHORITY_login.html` |
| Admin | `13_ADMIN_01_VISUAL_AUTHORITY_admin.html` |
| Job On | `20_JOB_ON_01_VISUAL_AUTHORITY_job-on.html` |
| Job On print | `20_JOB_ON_02_VISUAL_AUTHORITY_PRINT_job-on-4-pages.html` |
| Controlo shell / Resumo | `21_CONTROLO_01_VISUAL_AUTHORITY_controlo.html` |
| Controlo · Peso Operador | `22_PESO_OPERADOR_01_VISUAL_AUTHORITY_peso-operador.html` |
| Controlo · Peso print | `22_PESO_OPERADOR_02_VISUAL_AUTHORITY_PRINT_peso.html` |
| Controlo · Peso Responsável | `23_PESO_RESPONSAVEL_01_VISUAL_AUTHORITY_peso-responsavel.html` |
| Controlo · Pegamentos | `24_PEGAMENTOS_01_VISUAL_AUTHORITY_pegamentos.html` |
| Ferramentas | `30_FERRAMENTAS_01_VISUAL_AUTHORITY_ferramentas.html` |
| Boquilhas | `31_BOQUILHAS_01_VISUAL_AUTHORITY_boquilhas.html` |
| Armazém | `32_ARMAZEM_01_VISUAL_AUTHORITY_armazem.html` |
| Tampões | `33_TAMPOES_01_VISUAL_AUTHORITY_tampoes.html` |
| Reparação Interna | `34_REPARACAO_INTERNA_01_VISUAL_AUTHORITY_reparacao-interna.html` |
| Reparação Externa | `35_REPARACAO_EXTERNA_01_VISUAL_AUTHORITY_moldes.html` |
| História transversal | `36_HISTORIA_01_VISUAL_AUTHORITY_historia.html` — composition only; not module/nav authority |

These files are authorities for visual composition, not permission, ownership, calculation or lifecycle rules. “Carry forward” does not mean copy every panel or demo behaviour unchanged; the module findings in §5 define the required pruning.

### 6.2 Supporting HTML only; do not promote or carry as canonical screens

- `35_REPARACAO_EXTERNA_02_SUPPORTING_LIFECYCLE_reparacao-externa-v1.html` — lifecycle support only.
- `90_DESIGN_LAB_01_SUPPORTING_COMPONENT_REVIEW.html` — historical component/index review only; contains stale module classifications.

### 6.3 Superseded HTML; do not carry forward

- `35_REPARACAO_EXTERNA_99_DO_NOT_IMPLEMENT_reparacao-v2.html` — combined Reparação/Boquilhas navigation, explicitly superseded.

### 6.4 Assets and dependencies

Carry forward the canonical visual assets needed by retained authorities:

- `0_ASSET_CANONICAL_DESIGN_SYSTEM.css`;
- `0_ASSET_CANONICAL_INTERACTIONS.js`;
- `0_ASSET_JOB_ON_REDESIGN.css`;
- `0_ASSET_JOB_ON_REDESIGN.js`;
- `0_ASSET_LOGO.png`;
- `0_ASSET_ARTICLE_BOTTLE.svg` as demo/reference imagery only, until wired to the master reference image source.

`0_ASSET_INTEGRATED_MOCKUP.css` and `0_ASSET_INTEGRATED_MOCKUP.js` are demo-integration assets, not final design-system authority. Several retained HTML files currently depend on them, so they cannot be removed from the old package in isolation. The next design pass should migrate those HTML files to canonical tokens/interactions and then stop carrying the integrated-mockup assets.

## 7. Historical/supporting old-design files that do not need to be carried into a cleaned visual-authority package

The following are useful as provenance or old execution packaging, but are not visual authorities and do not need to be duplicated into a cleaned next design package:

### 7.1 Global package/audit scaffolding

- `0_GLOBAL_READ_FIRST.md`
- `0_GLOBAL_IMPLEMENTATION_ORDER.md`
- `0_GLOBAL_CODER_EXECUTION_RULES.md`
- `0_GLOBAL_CODER_START_PROMPT.md`
- `0_GLOBAL_PACKAGE_MANIFEST.md`
- `0_GLOBAL_PACKAGE_VALIDATION.md`
- `0_GLOBAL_DEEPSEEK_CODER_READINESS_AUDIT.md`
- `99_FINAL_ACCEPTANCE_00_README.md`
- `99_FINAL_ACCEPTANCE_90_DES_TASK.md`
- `99_FINAL_ACCEPTANCE_91_ACCEPTANCE.md`

These describe the previous coder package, its paths, readiness and acceptance workflow. They are historical process evidence, not current Manual-vs-design authority.

### 7.2 Duplicate foundation/package copies

- `10_FOUNDATION_00_README.md`
- `10_FOUNDATION_03_DESIGN_SYSTEM.md`
- `10_FOUNDATION_04_IMPLEMENTATION_CONTRACT.md`
- `10_FOUNDATION_90_DES_TASK.md`
- `10_FOUNDATION_91_ACCEPTANCE.md`

Retain one canonical design-system/interaction source; do not carry duplicate foundation copies solely because the old coder package required them.

### 7.3 Module execution wrappers

For every numbered module package, the following file classes are execution/support material rather than visual authority:

- `*_00_README.md`
- `*_90_DES_TASK.md`
- `*_91_ACCEPTANCE.md`
- `*_HANDOFF_*.md`
- `*_BRIEF_*.md`
- `*_OWNER_DECISION_*.md`
- snapshots/data contracts used only as examples

Do not delete the originals: some contain provenance and closed decisions. For a cleaned forward design package, the current Manual should be the functional source and the corrected HTML should be the visual source; these old wrappers need only be referenced where they contain a still-useful supporting detail not yet absorbed into the Manual.

Specific examples that should remain supporting rather than canonical are:

- all Login/Admin/Shell handoffs;
- Job On briefs, print handoff, data-contract example and owner-decision notes;
- Controlo/Peso/Pegamentos handoffs and owner-decision notes;
- Ferramentas/Armazém/Tampões/Reparação briefs;
- `31_BOQUILHAS_02_HANDOFF_BEHAVIOR.md`;
- `36_HISTORIA_02_HANDOFF_HISTORY.md`.

### 7.4 Old functional SOT inside old-design

`0_GLOBAL_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md` should be retained only as historical/provenance material for this comparison. It must not be carried forward as a second live functional authority beside `Manual/`, because several of its package-era rules and scope statements have been refined by the current Manual.

## 8. Pass 1 hold points for the next design pass

Do not finalise the affected visual choices until these Manual items are resolved:

1. Job On family meanings/required fields, lifecycle status set, blank-template contents, “previous” duplication order, verification completion/cancellation, stock-vs-quantity and Tipo-vs-Processo.
2. Job On `% usage` snapshot timing, past-calendar movement source and selector lot eligibility.
3. Ferramentas lot-number rules, reason for technical-state change, repair/state transition, copying inactive verification rules, final master-field set and Armazém Entrada `Estado` synchronisation.
4. Armazém exact action distribution by profile, final Programadas structure, whether destination is mandatory and exact Entrada-state classification.
5. Job On TP/Tampão production-specific configuration versus autonomous Tampões with no Job On relationship.
6. Exact article-image placement across the four Job On print pages.

Everything else can proceed in the existing visual language with the module corrections documented above.
