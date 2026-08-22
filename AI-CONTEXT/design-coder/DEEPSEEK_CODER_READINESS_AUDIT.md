# DEEPSEEK CODER READINESS AUDIT — READ-ONLY

**Mode:** READ-ONLY. No package/app/DB file modified.
**Workspace:** `D:\BA-DMO-RECOVERY`
**Package under audit:** `AI-CONTEXT\design-coder`
**Authorities consumed:** functional SOT → design-coder package → Dapper audit → DB support audit → current app (only where a package file names it).
**Objective:** Determine whether the current design-coder package is safe and unambiguous enough for Codex to implement the final design, after the proven pre-design app fixes are applied. Not a re-audit of Dapper or DB.

---

## 1. Package spine read

All root authorities read: `00_READ_FIRST.md`, `01_IMPLEMENTATION_ORDER.md`, `05_CODER_EXECUTION_RULES.md`, `PACKAGE_MANIFEST.md`, `PACKAGE_VALIDATION.md`, `CODER_START_PROMPT.md`, `02_FUNCTIONAL_RULES_SOURCE_OF_TRUTH.md` (package copy == authority byte-identical), `03_GLOBAL_DESIGN_SYSTEM.md`, `04_GLOBAL_IMPLEMENTATION_CONTRACT.md`. All 18 module folders inspected.

The package is well-organized, deterministic, and self-contained by design. Every module has a single READ order, one explicit visual authority, the functional SOT as functional authority, a DES task, an acceptance file, current-app paths, MUST PRESERVE / MUST NOT / DO NOT USE, stop conditions, and a uniform four-viewport screenshot acceptance.

---

## 2. Package-wide module check

Legend — VISUAL/FUNCTIONAL AUTHORITY (package-resolved), DES TASK (module), ACCEPTANCE (local), STALE STATUS / HIDDEN SOURCE (see §4/§7).

| MODULE | READ ORDER | VISUAL AUTHORITY | FUNCTIONAL AUTHORITY | DES TASK | ACCEPTANCE | STALE STATUS? | HIDDEN SOURCE? | STATUS |
|---|---|---|---|---|---|---|---|---|
| 10_FOUNDATION | css→js→sys→ctr→task→acc | CSS/JS/system (no module HTML) | SOT | DES-001 | 4-viewport local | No | No | READY |
| 11_SHELL | handoffs→task→acc | Global system (no one Shell HTML) | SOT | DES-002 | 4-viewport | No | No | READY |
| 12_LOGIN | html→handoff→task→acc | login.html | SOT | DES-003 | 4-viewport | No | No | READY |
| 13_ADMIN | html→handoffs→task→acc | admin.html | SOT | DES-004 | 4-viewport | No | No | READY |
| 20_JOB_ON | html→print→briefs→contract→verif→print→shared→image→task→acc | job-on.html + 4-page print | SOT | DES-005 | 4-viewport | **YES (Q-002)** | **YES (print/brief per-revision image)** | **NOT READY** |
| 21_CONTROLO | html→handoffs→shared→task→acc | controlo.html | SOT | DES-006 | 4-viewport | No | No | READY |
| 22_PESO_OPERADOR | html→print→handoff→decisions→task→acc | peso-operador.html + print | SOT | DES-007 | 4-viewport | No | No | READY |
| 23_PESO_RESPONSAVEL | html→handoff→decision→task→acc | peso-responsavel.html | SOT | DES-008 | 4-viewport | No | No | READY |
| 24_PEGAMENTOS | html→handoff→snap→task→acc | pegamentos.html | SOT | DES-009 | 4-viewport | No | No | READY |
| 30_FERRAMENTAS | html→brief→sap→task→acc | ferramentas.html | SOT | DES-010 | 4-viewport | **YES (Q-001, stale-only)** | **YES (verification-brief dangling cross-ref)** | READY WITH MINOR PACKAGE FIX |
| 31_BOQUILHAS | html→handoff→task→acc | boquilhas.html | SOT | DES-011 | 4-viewport | No | No | READY |
| 32_ARMAZEM | html→brief→sap-alert→task→acc | armazem.html | SOT | DES-012 | 4-viewport | No | No | READY |
| 33_TAMPOES | html→brief→task→acc | tampoes.html | SOT | DES-013 | 4-viewport | No | No | READY |
| 34_REPARACAO_INTERNA | html→brief→cm-mf→task→acc | reparacao-interna.html | SOT | DES-014 | 4-viewport | No | No | READY (package) |
| 35_REPARACAO_EXTERNA | moldes→v1→brief→do-not→task→acc | moldes.html | SOT | DES-015 | 4-viewport | No | No | READY |
| 36_HISTORIA | html→handoff→task→acc | historia.html | SOT | DES-016 | 4-viewport | No | No | READY |
| 90_DESIGN_LAB | review→sys→ctr→task→acc | system/contract docs; review = supporting | SOT | DES-017 | 4-viewport | No | No | READY |
| 99_FINAL_ACCEPTANCE | task→acc | all module authorities | SOT | DES-018 | 4-viewport | **YES (Q-001/Q-002 no-op)** | No | READY WITH MINOR PACKAGE FIX |

---

## 3. Stale Q-001 / Q-002 issue — explicit verification

### 20_JOB_ON / 90_DES_TASK.md

- **STILL SAYS:** `STATUS: READY WITH ISOLATED Q-002` and the BLOCKERS line reads: *"Q-002 only for whether image is reference-shared or revision snapshot; **until answered, retain current image behavior and do not copy across productions.**"*
- **Resolved reality in-package:** `08_OWNER_DECISION_ARTICLE_IMAGE.md` = **Q-002 is resolved**: image belongs to master article/reference, reusable across productions of that reference, print consumes it, only the required sheet displays it, explicitly **"do not model a separate image per Job On revision."** `00_READ_FIRST.md` and `PACKAGE_VALIDATION.md` both state the resolution.
- **The internal contradiction:** the DES-task BLOCKERS instruction to "retain current image behavior" is exactly the *opposite* of the resolved decision. The DB/Dapper audit confirms today's `job_on_revision.image_asset_id` resolves from `current_revision` (per-revision). So "retain current image behavior" = retain per-revision = **implement the old behavior Q-002 forbids**.

**CLASSIFICATION: REAL CONTRADICTION (HIGH).** The stale status wording, the BLOCKERS "retain current image behavior" directive, and `06_HANDOFF_PRINT.md`'s per-revision print instruction (`20_JOB_ON/06_HANDOFF_PRINT.md` §2: *"uma revisão histórica imprime a imagem que lhe pertence, não a imagem corrente da Referência"*) combine to steer Codex toward per-revision image, directly contradicting the resolved owner decision. The README's critical-rule line and `08` do state reference-scoped, but they are **outranked in read order by `06`** which Codex reads first and which governs the print surface Q-002 is about. **HIGH risk Codex implements old behavior.**

### 30_FERRAMENTAS / 90_DES_TASK.md

- **STILL SAYS:** `STATUS: READY WITH ISOLATED Q-001` and BLOCKERS: *"Q-001 affects only activation of Utilização UI."*
- **Resolved in-package:** `03_OWNER_DECISION_SAP_UTILISATION.md` = Q-001 resolved: activate Utilização UI, manual SAP %, never calculated, no future auto-SAP, Armazém may consume.
- The DES-task BLOCKERS wording *itself* already states the resolved outcome ("affects only activation of Utilização UI" + the IMPLEMENTATION SCOPE lists "optional Utilização tab per Q-001"). **There is no instruction to implement old/no-Utilização behavior:** the README's critical rule and `03` both say to activate it.

**CLASSIFICATION: STALE STATUS ONLY (SAFE).** The `READY WITH ISOLATED Q-001` header is stale and should read "READY", but the surrounding text, README, and owner decision are mutually consistent and resolve to "activate Utilização." No high risk. Minor package fix = update the status header and remove the stale "optional per Q-001" phrasing.

### 99_FINAL_ACCEPTANCE / 90_DES_TASK.md

- BLOCKERS line reads `Q-001/Q-002 only for their isolated surfaces.` Both are already resolved; this is a harmless no-op pointer but is **stale**. Low/no risk. Minor package fix to drop it.

---

## 4. Deep simulation — four modules only

### DES-005 JOB ON
- **VISUAL AUTHORITY:** `01_VISUAL_AUTHORITY_job-on.html` + `02_VISUAL_AUTHORITY_PRINT_job-on-4-pages.html` (present). Supporting: `03` brief, `04` data contract, `05` verifications, `06` print handoff, `07`/`08` owner decisions.
- **FUNCTIONAL AUTHORITY:** SOT §3 (Job On) — immutable revisions, atomic aggregate, per-user current-open, deterministic machine-colour.
- **CURRENT APP FILES REFERENCED:** `Pages\JobOn\*.cshtml`, `wwwroot\styles\modules\jobon-layout.css`, `wwwroot\scripts\jobon.js`, JobOn services/PDF renderer (all present; `JobOn\Index.cshtml` confirmed).
- **PAGE ANATOMY:** calendar + production list (Planeamento); fixed context card; family grid (MP/CM, MF, BQ, PU, CAL, AN, ARR, PI, CS, TP, FO); editor; verification states; history/settings; 4-page print.
- **INTERACTIONS:** create/duplicate (incl. duplicate-anterior, novo-em-branco, duplicar-histórico), consulta↔edição modes, tool-lot selector filtered by ref+machine, live-state decorator (read-only), date-end inline change, confirmation checks, 4-page print.
- **DATA REQUIRED:** hydrated full aggregate (components/fields/CAL rows/verifications), exact current-open context, per-user current (`jobon_user_current`), **master reference + reference-scoped image (Q-002)**.
- **RESPONSIVE CONTRACT:** desktop 3–4 col family grid, tablet 2 col, mobile 1 col; fixed context retained; **no page horizontal scroll.**
- **MUST PRESERVE:** immutable exact revisions; atomic full aggregate; historical tools; master-domain ownership; exact context for Peso/Pegamentos/Controlo; reference-owned image.
- **MUST NOT:** master-tool edits; historical reinterpretation; internal-ID exposure; **schema redesign**; **per-revision image model**; image on every sheet.
- **STOP CONDITIONS:** data unavailable / schema change needed / authority conflict / rule must be invented.
- **KNOWN DAPPER/APP LIMITATIONS (from audits, NOT re-audited):** `GetByIdAsync` does not hydrate components/fields/CAL/verifications → PDF/duplicate/edit/Confirmar see empty children (**PRE-DESIGN BLOCKER**); save/duplicate not atomic (**PRE-DESIGN**); print PDF built from unhydrated aggregate; Q-002 master-reference image is a schema dependency (SCHEMA-DEP).
- **CAN CODEX IMPLEMENT WITHOUT DESIGN SEARCH?** Package content is sufficient to author DES-005 (all visuals/briefs/contracts present). **YES — pending (a) the proven pre-design hydration+atomicity fixes and (b) resolution of the internal per-revision vs reference-scoped image contradiction within the module (see §3).**

### DES-006 CONTROLO
- **VISUAL AUTHORITY:** `01_VISUAL_AUTHORITY_controlo.html` (present).
- **FUNCTIONAL AUTHORITY:** SOT §10 (R010/R012) — one active-production card binds Resumo/Peso/Comparação/Pegamentos/Histórico; no second selector/calendar; free-mode without fake production.
- **CURRENT APP FILES REFERENCED:** `Pages\Controlo\*.cshtml`, `controlo-layout.css`, `controlo.js`, Controlo services/lookups (Index.cshtml present).
- **PAGE ANATOMY:** active-production card + bound tabs; free-mode read-only consultation; Histórico tab with calendar + combined Resumo/Peso/Pegamentos document filters.
- **INTERACTIONS:** tab binding to exact `job_on_id + job_on_revision_id`; per-result OK/NOK + observation + manual MCaliper link; draft→submitted→approved/rejected with reopen; free-mode queries; document manifest states.
- **DATA REQUIRED:** exact revision anchor; snapshot components; per-component result + MCaliper URL; append-only sheet events; Peso/Pegamentos Doc manifests.
- **RESPONSIVE CONTRACT:** one card; tabs all share context; mobile reorganizes without page horizontal scroll; free mode distinct from bound.
- **MUST PRESERVE:** exact `job_on_id + job_on_revision_id`; snapshot components; append-only workflow/history; useful free mode.
- **MUST NOT:** second selector; second calendar; silent production selection; click-to-release context; security mis-parse of MCaliper URLs (allow empty/authorized schemes).
- **STOP CONDITIONS:** authority conflict / data unavailable / schema change needed.
- **KNOWN DAPPER/APP LIMITATIONS:** Controlo persistence **COMPLETE & ATOMIC** per Dapper audit (no pre-design block). Minor: free-mode read surfacing (read/DTO); optional MCaliper link-history ledger (clean-baseline).
- **CAN CODEX IMPLEMENT WITHOUT DESIGN SEARCH?** **YES.** Fully specified; persistence ready; no in-package ambiguity beyond the MCaliper URL being a single field (the handoff supplies the association contract).

### DES-010 FERRAMENTAS
- **VISUAL AUTHORITY:** `01_VISUAL_AUTHORITY_ferramentas.html` (present; includes Referência/Lotes/Verificações/Utilização/Histórico tabs).
- **FUNCTIONAL AUTHORITY:** SOT §5 (Ferramentas) + resolved Q-001 (manual SAP, append-only, never calculated).
- **CURRENT APP FILES REFERENCED:** `Pages\Ferramentas\*.cshtml` (Index/Criar/Ficha/_ReferenceList present), ferramentas-layout.css, ferramentas.js.
- **PAGE ANATOMY:** compact reference list; create-reference/first-lot page; five-tab detail; duplicate-lot flow.
- **INTERACTIONS:** search (ref/tech-name/lot/drawing/line/process/owner-plant); create ref+first lot atomically; duplicate-lot (inherited-protected + copied-editable); Verificações tab (add/edit/reset/disable); Utilização tab (manual SAP %, append-only).
- **DATA REQUIRED:** CM/MF master refs, lotes, physical pieces, check rules/occurrences, tool_usage_records.
- **RESPONSIVE CONTRACT:** list + tabbed detail match HTML at all four viewports; no horizontal scroll.
- **MUST PRESERVE:** separate CM/MF identities; stable ref/lote IDs; append-only verification & usage; master-vs-lote ownership.
- **MUST NOT:** merge CM/MF; infer drawing codes; copy checks/history/occurrences on duplicate; create warehouse identity; calculate utilisation.
- **STOP CONDITIONS:** standard.
- **KNOWN DAPPER/APP LIMITATIONS:** current-location/status not projected (during-design read/DTO, data exists); SAP utilisation repo complete; atomic ref-lote create done.
- **CAN CODEX IMPLEMENT WITHOUT DESIGN SEARCH?** **YES** for the UI. One in-package gap: the Verificações tab behavior is defined in `20_JOB_ON/05_BRIEF_VERIFICATIONS.md`, but `30_FERRAMENTAS/02_BRIEF_REGISTRATION.md` (and DES-010 "registration/verification briefs") cite the external `JOB_ON_VERIFICACOES_DESIGN_BRIEF.md` which is **not** inside Ferramentas — Codex must be told the verification source is the Job On copy. Otherwise the same content exists in-package. **Package fix, not an app gap.**

### DES-014 REPARAÇÃO INTERNA
- **VISUAL AUTHORITY:** `01_VISUAL_AUTHORITY_reparacao-interna.html` (present).
- **FUNCTIONAL AUTHORITY:** SOT §9 + `03_OWNER_DECISION_CM_MF_ONLY.md` (long, detailed, Portuguese; complete) — CM/MF only; BQ context-only; `5447T173` full; repeated numbers; 06:00/09:00 line projection; append-only correction; no hard blocks.
- **CURRENT APP FILES REFERENCED:** `Pages\ReparacaoInterna\*.cshtml`, reparacao-interna-layout.css, reparacao-interna.js, service/context lookup (Index present).
- **PAGE ANATOMY:** B1–C3 production cards (full reference / "Sem Job On ativo"); context panel; line→CM/MF→number→OK rapid entry; recent records; Consulta tab; correction chain; no-production context.
- **INTERACTIONS:** card select → context projection; CM/MF + number entry; OK persists immediately; number focus cleared; Consulta filters; select/double-click; Correger registo / Apagar (anulação auditável).
- **DATA REQUIRED:** line cards w/ active reference; exact revision context; `5447T173` complete reference; repeated occurrences; correction `correction_of_id` chain.
- **RESPONSIVE CONTRACT:** three-column grid desktop/mid, one column mobile; selector card full-width above context panel; **no overflow / no horizontal scroll** (layout rule explicit).
- **MUST PRESERVE:** CM/MF only; full reference; repeated numbers; no hard blocks; recalibrated correction context; own-record visibility.
- **MUST NOT:** select/process BQ; truncate to `5447`; deduplicate numbers; block no-production; mutate Job On; hard-delete historical fact.
- **STOP CONDITIONS:** standard.
- **KNOWN DAPPER/APP LIMITATIONS:** repository chain reads + atomic; **Application/Domain/HTTP still model BQ as recordable** (PRE-DESIGN BLOCKER — must strip BQ); N22 `tool_type CHECK ('CM','MF','BQ')` (SCHEMA-DEP revert).
- **CAN CODEX IMPLEMENT WITHOUT DESIGN SEARCH?** **YES** once the proven BQ-stripping pre-design fix is applied. The owner decision is explicit and the brief is complete; no in-package ambiguity for the UI recomposition itself.

---

## 5. Critical rule assertion

| RULE | CLEAR | CONFLICT | NOTES |
|---|---|---|---|
| **CONTROLO — current-open Job On** | YES | NO | R011 binding explicit; handoff + DES task state consume exact current-open. |
| **CONTROLO — exact revision across tabs** | YES | NO | `job_on_id + job_on_revision_id` FK-pinned; every tab uses same context. |
| **CONTROLO — no second selector / no second calendar** | YES | NO | Explicit MUST NOT in README + handoff §2. |
| **CONTROLO — free mode, no fake production** | YES | NO | Required; read-only endpoints exist. |
| **RI — CM/MF only** | YES | NO (in-package) | Clear in SOT/owner-decision; **pre-design app/domain must strip BQ** (proven app fix). |
| **RI — BQ not recordable** | YES | NO | Owner decision explicit. |
| **RI — full reference visible** | YES | NO | `5447T173` must show complete. |
| **RI — BQ context-only** | YES | NO | T173 context only; explicit. |
| **RI — repeated numbers valid** | YES | NO | No uniqueness; explicit. |
| **RI — corrections append facts** | YES | NO | `correction_of_id`, original preserved, audit. |
| **RI — no hard operational blocks** | YES | NO | Explicit. |
| **SAP UTILISATION — manually read from SAP** | YES | NO | Resolved Q-001. |
| **SAP — manually entered** | YES | NO | Resolved. |
| **SAP — never calculated** | YES | NO | Resolved; MUST NOT calculate. |
| **SAP — append-only factual** | YES | NO | `tool_usage_records` append-only. |
| **SAP — future auto-integration out of scope** | YES | NO | Resolved. |
| **SAP — Armazém may consume stored value** | YES | NO | Resolved (alert read/DTO). |
| **JOB ON IMAGE — master article/reference ownership** | **NO (in-package conflict)** | **YES (internal)** | Owner decision `08` + README critical rule say reference-owned; BUT `06_HANDOFF_PRINT` and the stale DES-005 BLOCKERS say retain per-revision → **conflict**; see §3. |
| **JOB ON IMAGE — not per revision** | **NO** | **YES** | `06` print handoff explicitly describes per-revision behavior. |
| **JOB ON IMAGE — selected from company/server directory** | YES | NO | Owner decision. |
| **JOB ON IMAGE — print consumes it** | YES | NO | Owner decision; print gov't only required sheet. |
| **JOB ON IMAGE — only required printed sheet displays it** | YES | NO | Explicit (only Job On print sheet). |
| **PESO — server-side calculations only** | YES | NO | SOT §11; repository confirms C# WeightCalculator. |
| **PESO — explicit CM pair** | YES | NO | Owner decision; per-CM pairing explicit. |
| **PESO — glass weight only** | YES | NO | MUST NOT water/capacity/global-average. |
| **PEGAMENTOS — exact inherited revision** | YES | NO | FK-pinned, immutable. |
| **PEGAMENTOS — CM/BQ/MF not manually reselected** | YES | NO | Explicit MUST NOT. |

---

## 6. Critical rule assertion — conflicts summary

The **only** genuine in-package conflict is the **Job On article-image ownership** (per §3): owner-decision `08`/README critical-rule say reference-scoped, while `06_HANDOFF_PRINT` §2 and the stale DES-005 BLOCKERS direct per-revision behavior. All other critical rules are unambiguous. The RI CM/MF-only and Peso server-side rules are clear in-package; their remaining risk is the proven **application-code** BQ leak, not a package ambiguity.

---

## 7. Hidden-source / escape-hatch check

Target: 0 PACKAGE GAP, 0 HIGH hidden-source risk.

| Reference | Location | Class |
|---|---|---|
| `docs/IMPLEMENTATION_STATE.md` (design claims historical) | SOT header + §end (file exists at repo root, not AI-CONTEXT) | **SAFE HISTORICAL POINTER** — explicitly branded historical; SOT restates current facts; package never instructs Codex to read it. |
| `AUDIT-REVIEWED.md` | SOT §3/§7/§8/§15 (file exists at repo root) | **SAFE HISTORICAL POINTER / unnecessary escape hatch** — §15 restates the hardening items in full text; chasing the file is unnecessary for DES work. Not referenced by any module README/DES task. |
| `refine-v1.md` | SOT §15 (file exists at repo root) | **SAFE HISTORICAL POINTER** — same as AUDIT-REVIEWED; §15 restates the backlog items. Not module-referenced. |
| `LEGACY_PESO_VERIFIED_BEHAVIOR.md` | SOT §11 (file exists at repo root) | **SAFE HISTORICAL POINTER** — §11 restates the authoritative Peso rules (server calc, pairing). Not module-referenced. |
| `job-on-v48-folha-producao.html` (absent) | `20_JOB_ON\00_README.md` "DO NOT USE" | **SAFE** — named only to forbid substituting it; README explains absence and the final authorities are present. |
| `JOB_ON_VERIFICACOES_DESIGN_BRIEF.md` (outside Ferramentas) | `30_FERRAMENTAS\02_BRIEF_REGISTRATION.md` §8/§12; `DES-010` "registration/verification briefs" | **SAFE — content in-package** (same file copied as `20_JOB_ON\05_BRIEF_VERIFICATIONS.md`) but **PACKAGE GAP** (no in-module cross-link; Codex must be told to reuse the Job On copy). |
| `reparacao-v2.html` / `reparacao-externa-v1.html` | `35_...\99_DO_NOT_IMPLEMENT_reparacao-v2.html`, `02_SUPPORTING_...v1.html` | **SAFE** — copied with explicit forbidden/supporting classification. |
| `tampoes-v38-standalone.html` | `33_TAMPOES\00_README.md` DO NOT USE | **SAFE** — explicitly forbidden, not copied. |
| `integrated-mockup.css/.js`, `design-review.html` | 10_FOUNDATION / 12_LOGIN / 90 DESIGN_LAB DO NOT USE | **SAFE** — forbidden items. |
| Current app files (Pages/wwwroot) | module READMEs "CURRENT APP LOCATION" | **LEGITIMATE APP DEPENDENCY** — all confirmed present. |

**Result: PACKAGE GAPS = 1** (Ferramentas → verification-brief source not cross-linked into the module; content exists in-package under Job On). **HIGH hidden-source risk = 0.** The three historical files exist at repo root but the strong no-search gate (00_READ_FIRST §5, 05 E) plus the SOT restating their content keep Codex inside the package. The only risk is if a Codex-agent with whole-repo access follows the `AUDIT-REVIEWED/refine-v1` pointers and pulls contradicting historical instructions — mitigated because §15 re-states the requirements and no module references them.

---

## 8. Wrong-file risk

| MODULE | RISK | SOURCE OF CONFUSION | SEVERITY | PACKAGE FIX |
|---|---|---|---|---|
| 20_JOB_ON | Codex implements **per-revision** article image (old behavior) instead of reference-scoped | `06_HANDOFF_PRINT.md` §2 per-revision print instruction + stale DES-005 BLOCKERS "retain current image behavior"; DB audit confirms app currently per-revision | **HIGH** | Fix `06` print handoff to reference-scoped image; correct DES-005 status/BLOCKERS to the resolved Q-002; add cross-ref from DES task to `08`. |
| 20_JOB_ON | Codex treats image ownership as unresolved | `03_BRIEF_JOB_ON.md` "Imagem do artigo" still says "propriedade ainda deve ser confirmada ... imagem comum da Referência ou snapshot específico" | MEDIUM | Remove/extend the "unresolved" phrasing in `03` to bind to `08`. |
| 30_FERRAMENTAS | Codex skips or gates Utilização tab (treats Q-001 as not-activated) | Stale `READY WITH ISOLATED Q-001` header + "optional Utilização tab per Q-001" in DES-010 | LOW–MED | Retitle DES-010 to READY; change "optional per Q-001" to "activate". |
| 30_FERRAMENTAS | Codex cannot find verification-brief source and either invents behavior or searches the repo | DES-010 "verification briefs" + `02_BRIEF` citing external `JOB_ON_VERIFICACOES_DESIGN_BRIEF.md` not in-module | MED | Cross-link `20_JOB_ON/05_BRIEF_VERIFICATIONS.md` from Ferramentas README/task. |
| 99_FINAL_ACCEPTANCE | Codex re-opens Q-001/Q-002 as active blockers | `90_DES_TASK.md` BLOCKERS "Q-001/Q-002 only for their isolated surfaces" | LOW | Drop the line; both resolved. |
| General | A whole-repo-search Codex pulls `AUDIT-REVIEWED/refine-v1` historical contradictions | SOT §15 pointers to repo-root files | LOW–MED | Add an explicit "these are historical; do not open" note in 00_READ_FIRST (optional tightening). |

---

## 9. Dapper / DB readiness integration (consumed, not re-audited)

Describes whether the **package** is ready per-module and maps the two audits' proven findings to DES tasks.

| DES | PACKAGE READY? | APP/DAPPER READY? | DB READY? | PRE-DESIGN FIX? | DURING-DES FIX? | SCHEMA DEP? | FINAL EXECUTION STATUS |
|---|---|---|---|---|---|---|---|
| 001 Foundation | YES | YES | YES | – | – | – | **OK after pre-design** |
| 002 Shell | YES | YES | YES | – | – | – | **OK** |
| 003 Login | YES | YES | YES | – | – | – | **OK** |
| 004 Admin | YES | YES | YES | – (X12 cosmetic DTO optional) | – | – | **OK** |
| 005 Job On | **NO (image conflict + stale Q-002)** | **NO** (aggregate hydration + atomic save/duplicate missing) | **NO** (master-ref + reference image = genuine schema dep) | **YES — hydration + atomic save/duplication** | live-tool decorator read/DTO | **YES — Q-002** | **BLOCKED until pre-design app + schema-dependency resolved** |
| 006 Controlo | YES | YES | YES | – | free-mode surfacing (read/DTO) | – (optional MCaliper ledger) | **OK** |
| 007 Peso Operador | YES | YES | YES | – | – | – (optional jsonb→relational) | **OK** |
| 008 Peso Responsável | YES | YES | YES | – | – | – | **OK** |
| 009 Pegamentos | YES | YES | YES | – | minor list/read DTO | – | **OK** |
| 010 Ferramentas | **READY W/ MINOR PACKAGE FIX** | YES (usage) | YES | – | current-location/status read/DTO | – | **OK (after module cross-link)** |
| 011 Boquilhas | YES | YES | YES | – | – | – | **OK** |
| 012 Armazém | YES | YES | YES | – | alert read/DTO; 1:1 TOCTOU hardening (B) | – | **OK** |
| 013 Tampões | YES | YES | YES | – | `SetSaldoAsync` delta/FOR UPDATE (B) | – | **OK** |
| 014 R.Interna | YES (package) | **NO** (Application/Domain/HTTP still model BQ) | **NO** (N22 CHECK revert) | **YES — strip BQ from app contract/domain/HTTP** | – | **YES — N22 revert** | **OK after pre-design BQ strip** |
| 015 R.Externa | YES | YES | YES | – | – | – | **OK** |
| 016 História | YES | YES | YES | – | – | – | **OK** |
| 017 DesignLab | YES | YES (n/a) | n/a | – | – | – | **OK** |
| 018 Final | **READY W/ MINOR PACKAGE FIX** | depends on prior | depends | – | – | – | **OK after prior DES** |

---

## 10. Screenshot acceptance

Verified acceptance files require all four viewports (1440×900, 980×900, 720×900, 375×812), same-viewport authority↔implementation capture, overlay/diff, correction pass, and the negative rule "No build/tests as visual proof" (tests alone are not visual acceptance). Every module acceptance follows this template uniformly. **Only actual gaps reported below** (none are missing-viewport; the only issue is wording in the two DES tasks that slightly predate the uniform template but still carry the four viewports in their VERIFICATION text).

**No viewport/screenshot gaps found.** All 18 modules (and the final pass) mandate the four viewports plus diff/overlay/correction. The two stale DES tasks (DES-005 JOB ON, DES-010 FERRAMENTAS) still phrase acceptance in the numbered list of the same four viewports in their VERIFICATION line, so no screenshot regression is introduced.

---

## 11. Risk register (real risks only, ≤15)

| RISK | SEVERITY | MODULE | CAUSE | IMPACT | PACKAGE FIX |
|---|---|---|---|---|---|
| 1. Per-revision vs reference-scoped image contradiction | **HIGH** | 20_JOB_ON | `06_HANDOFF_PRINT` per-revision instruction + stale DES-005 "retain current image behavior" contradict resolved Q-002 | Codex implements old per-revision image; Q-002 not met | Fix `06` print handoff + DES-005 status/BLOCKERS to reference-scoped; cross-ref `08`. |
| 2. Image ownership presented as unresolved | MED | 20_JOB_ON | `03_BRIEF` "propriedade ainda deve ser confirmada" | Codex halts or invents ownership | Bind `03` to resolved `08`. |
| 3. Ferramentas verification source not in-module | MED | 30_FERRAMENTAS | DES-010 "verification briefs" + `02_BRIEF` cites external name; not copied into module | Codex searches repo or invents verification behavior | Cross-link `20_JOB_ON/05_BRIEF_VERIFICATIONS.md`. |
| 4. Stale `READY WITH ISOLATED Q-00x` headers | LOW–MED | 20_JOB_ON, 30_FERRAMENTAS, 99 | Status not updated after owner-decision resolution | Confuses "is this resolved" judgements; DES-005 one is dangerous | Retitle to READY / corrected. |
| 5. Historical-file escape hatch | LOW–MED | Global (SOT §15) | SOT pointers to repo-root `AUDIT-REVIEWED`/`refine-v1` | Whole-repo Codex may pull contradicting history | Add explicit "historical; do not open" note (tightening). |
| 6. Schema dependence for Q-002 not solvable by repo | MED | 20_JOB_ON | No master-reference entity; image revision-scoped | DES-005 image surface cannot be built as designed | Pre-design schema execution for master-reference; package already flags it. |
| 7. Aggregation hydration/atomicity pre-design | HIGH | 20_JOB_ON | app/Dapper gaps (proven) | PDF/duplicate/edit depend on empty aggregate | Proven pre-design app fix. |
| 8. RI BQ app-contract leak | HIGH | 34 | app/Domain/HTTP model BQ (proven) | DES-014 could persist BQ despite CM/MF-only package rule | Proven pre-design app fix (strip BQ). |
| 9. Ferramentas current-location read gap | LOW–MED | 30 | data exists, not projected | current-state surface incomplete | During-DES read/DTO. |
| 10. Armazém alert + 1:1 TOCTOU | LOW–MED | 32 | alert read absent; check-then-insert | alert card + concurrency | During-DES read/DTO + Dapper hardening. |
| 11. Tampões lost-update | LOW | 33 | `SetSaldoAsync` absolute rewrite | balance drift under concurrency | During-DES delta/FOR UPDATE. |
| 12. Peso comparison read is jsonb | LOW | 22/23 | jsonb `comparison_decisions` | per-CM pairing surfaced via service (works) | Optional clean-baseline; non-blocking. |
| 13. Job On print built from unhydrated aggregate | HIGH | 20_JOB_ON | same hydration gap | print tool sections empty | Proven pre-design hydration fix. |

(6,7,8,13 are app/Dapper issues the audits already proved; included here because they gate DES execution, not because the package is at fault.)

---

## 12. Scores

**Clarity/readiness (0–10):**

| Metric | Score |
|---|---|
| PACKAGE ORGANIZATION | 9 |
| VISUAL AUTHORITY CLARITY | 8 |
| FUNCTIONAL AUTHORITY CLARITY | 9 |
| READ ORDER CLARITY | 9 |
| INTERACTION CLARITY | 8 |
| RESPONSIVE CLARITY | 9 |
| SCREENSHOT ACCEPTANCE | 9 |
| CODEX EXECUTION SAFETY | 5 |

**Risk (10 = worse):**

| Metric | Score |
|---|---|
| WRONG-FILE RISK | 5 |
| HIDDEN-SOURCE RISK | 2 |
| STALE-INSTRUCTION RISK | 7 |
| AGENT-INVENTION RISK | 5 |
| OVERALL EXECUTION RISK | 6 |

---

## 13. Top 5 package fixes (material, not polish)

1. **Resolve the Job On article-image contradiction in `20_JOB_ON`:** fix `06_HANDOFF_PRINT.md` to describe a **reference-scoped** image (not per-revision), correct the DES-005 STATUS/BLOCKERS ("READY WITH ISOLATED Q-002" and "until answered, retain current image behavior") to the **resolved** reference-owned Q-002, and cross-reference `08_OWNER_DECISION_ARTICLE_IMAGE.md` from the DES task. This is the one HIGH wrong-file/stale-instruction risk in the package.
2. **Bind `03_BRIEF_JOB_ON.md`'s "Imagem do artigo"** (currently "propriedade ainda deve ser confirmada" — presents both options) to the resolved `08` owner decision so no subagent treats the image as an open question.
3. **Cross-link the Ferramentas verification source:** point `30_FERRAMENTAS\00_README.md` and/or DES-010 to `20_JOB_ON/05_BRIEF_VERIFICATIONS.md` (the in-package copy of the verification contract) and drop the dangling `JOB_ON_VERIFICACOES_DESIGN_BRIEF.md` citation from `02_BRIEF_REGISTRATION.md` §8/§12 — closes the one PACKAGE GAP without adding sources.
4. **Retitle the stale `READY WITH ISOLATED Q-00x` headers:** `20_JOB_ON`, `30_FERRAMENTAS`, and `99_FINAL_ACCEPTANCE` all still carry Q-status wording after both Q-001/Q-002 are resolved locally; change to READY and remove the "optional per Q-001" phrasing in DES-010.
5. **Add an explicit historical-file guard:** in `00_READ_FIRST.md`, state that `AUDIT-REVIEWED.md`, `refine-v1.md`, `IMPLEMENTATION_STATE.md`, and `LEGACY_PESO_VERIFIED_BEHAVIOR.md` (outside AI-CONTEXT) are historical and must not be opened for DES work, to close the escape hatch for whole-repo Codex agents.

---

## 14. Final verdict

**VERDICT: `READY AFTER SMALL PACKAGE FIXES`.**

- **CRITICAL PACKAGE RISKS:** 0 (the two critical execution risks — Job On hydration/atomicity and RI BQ-stripping — are proven **app/Dapper** gaps, already identified by the audits, not package ambiguities).
- **HIGH PACKAGE RISKS:** 1 — the per-revision vs reference-scoped Job On image contradiction (`06_HANDOFF_PRINT` + stale DES-005 BLOCKERS vs resolved Q-002).
- **REAL CONTRADICTIONS:** 1 — Job On article image ownership (in-package: `06` print handoff and stale DES-005 BLOCKERS conflict with `08` owner decision / README critical rule). All other critical rules are unambiguous.
- **PACKAGE GAPS:** 1 — Ferramentas verification-brief source not cross-linked into the module (content exists in-package under Job On).
- **HIDDEN-SOURCE RISKS:** 0 HIGH (three historical repo-root files exist but are only safe/historical pointers; SOT restates their content; no module requires them).
- **STALE DES TASKS:** 3 status lines — `20_JOB_ON` (HIGH, because it instructs retaining old image behavior), `30_FERRAMENTAS` (stale-only, safe), `99_FINAL_ACCEPTANCE` (stale-only, no-op).
- **MODULES READY:** 10_FOUNDATION, 11_SHELL, 12_LOGIN, 13_ADMIN, 21_CONTROLO, 22_PESO_OPERADOR, 23_PESO_RESPONSAVEL, 24_PEGAMENTOS, 31_BOQUILHAS, 32_ARMAZEM, 33_TAMPOES, 34_REPARACAO_INTERNA, 35_REPARACAO_EXTERNA, 36_HISTORIA, 90_DESIGN_LAB (15 in-package-ready; DES-014 and DES-012/013 pending their proven app fixes).
- **MODULES NOT READY (package-side):** `20_JOB_ON` (image contradiction + stale status) until fix #1/#2; `30_FERRAMENTAS` (minor cross-link + stale status) until fix #3/#4.

### WOULD YOU HAND THIS PACKAGE TO CODEX AFTER THE PROVEN PRE-DESIGN APP FIXES ARE APPLIED?

**NO — not yet; only after 3 small package fixes AND the proven pre-design app fixes.**

The package is structurally excellent, deterministic, self-contained, and unambiguous for 15 of 18 modules once the app-side pre-design fixes are applied. But two things must happen first:

1. **Package side (quick, no redesign):** resolve the Job On article-image contradiction (`06` print handoff + stale DES-005 "retain current image behavior" direct per-revision implementation), bind the `03` brief image ownership, cross-link the Ferramentas verification source, retitle three stale Q-status lines, and add the historical-file guard. Without fix #1, a Codex implementing the Job On print — the exact surface Q-002 governs — is **more likely to follow the per-revision instruction than the resolved reference-scoped decision**, which would silently reintroduce the precise behavior Q-002 was raised to forbid.

2. **App side (already proven by the audits):** Job On aggregate hydration + atomic save/duplication (gates DES-005 plus Peso/Pegamentos/Controlo context), and RI BQ-stripping at the Application/Domain/HTTP layer (gates DES-014). These are the two decisive pre-design fixes; they are not package defects. The schema dependencies (Q-002 master-reference + N22 revert) must also be executed for the clean baseline.

After those 3 package edits and the 2 pre-design app fixes, this package is safe, unambiguous, and Codex-ready — the remaining module work is otherwise all `READY`/`OK`. The single material defect that would actually cause Codex to implement the old behavior is the Job On article-image instruction, and it is a small, contained edit.

---
*End of audit. Read-only; no package, app, database, schema, migration, test, or Git object was modified.*