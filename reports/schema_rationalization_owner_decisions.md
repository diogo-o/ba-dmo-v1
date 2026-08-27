# SCHEMA-RAT-02 — Owner Decisions (sharpened from SCHEMA-RAT-01 §11)

> **Type:** READ-ONLY EXTRACTION / SHARPENING — the only input read is `reports/schema_rationalization_target_architecture.md` (SCHEMA-RAT-01) plus the repository evidence it cites. No source, migration, SQL, or database object was modified; no commit or push. The only artifact produced by this task is this file.
>
> **Baseline:** `D:\BA-DMO`, branch `main`, HEAD `683765f6ea6ee7fbecd456d96b3c116ecaa08236`.
>
> **Source of decisions:** SCHEMA-RAT-01 §11 (16 owner decisions, IDs D-1…D-16). Every decision below reproduces the same question and sharpens it into the 17 required fields, using only evidence already established in SCHEMA-RAT-01 (§§2–5, 7–9, 12) — no new schema design is invented. Where SCHEMA-RAT-01 marked items `NEEDS_OWNER_DECISION` or `LEGACY`/`ORPHAN`, the sharpened verdict is given as **KEEP / REMOVE_LATER / OWNER_PRODUCT_DECISION** with the specific evidence.

**Field legend for each decision (1–17):** ID · Question · Current state · Competing authorities · Option A · Option B · Option C (only where a third alternative genuinely exists) · Recommended option · Why · Tables/columns affected · Current writers · Current readers · Migration/data risk · Becomes authoritative · Becomes derived/mirror/legacy · Reversible · Owner approval before N32.

★ = product/scope decision (business owner, not purely technical).

---

## D-1 — Functional-profile authority (TEMPLATE → PROFILE)

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-1 (SCHEMA-RAT-01 §11 #1) |
| 2 | **Question** | Where does the functional profile (Admin / Operador / Controlador / Responsável) authoritatively live — `access_template_profiles.functional_profile` (template-owned, N31) or `internal_users.profile_title` (per-user, pre-N31)? And who is its **single writer and single reader**? |
| 3 | **Current state** | Both columns exist. `profile_title` has **three writers**: (a) Web-layer `TemplateProfileStore.UpsertAsync` (raw SQL: upsert `access_template_profiles` then `UPDATE internal_users SET profile_title …` on a separate connection from `AdminTemplateService`); (b) `AdminUserService.UpdateUserAsync`/`CreateUserAsync` → `DapperAdminRepository.UpdateUserAsync` (user-level write; the UI no longer edits it — "é definido pelo template"); (c) migration-time N31 trigger/backfill + profile sync, plus `DapperInternalUserRepository.CreateBootstrapAdminAsync` hardcoding `'Admin'`. Runtime resolution (`IdentityResolutionService.ResolveAsync`) parses **only** `profile_title`; `access_template_profiles` is read only by `TemplateProfileStore` (Web). Self-lockout counting (`CountActiveAdminsOnAsync`) also filters on `u.profile_title = 'Admin'`. |
| 4 | **Competing authorities** | `access_template_profiles.functional_profile` (template-owned, N31-designated authority — not consulted at login) **vs** `internal_users.profile_title` (legacy per-user column — the operative authority at login via `IdentityResolutionService`). Also `access_templates.modules…capabilities[]` legacy arrays (inert; capability sets are profile-derived). |
| 5 | **Option A** | `access_template_profiles` is the single authority. One Application-boundary writer persists the profile; `internal_users.profile_title` becomes a derived mirror (synced in the same UoW during a transition window) and is **removed** in Phase F; `IdentityResolutionService` (and the self-lockout query) resolve the profile through `access_templates → access_template_profiles`. |
| 6 | **Option B** | Keep `profile_title` as the persisted authority; retire `access_template_profiles`, its CHECK, trigger and backfill (reverses N31). |
| 7 | **Option C** *(genuinely necessary)* | Status quo: both stores, three writers, runtime reading the legacy column. |
| 8 | **Recommended** | **Option A.** The functional profile belongs to the template (product rule: template = title + ONE profile + modules). `profile_title` should be **removed as a persisted authority** (kept only as a transient derived mirror during convergence, then dropped in Phase F). |
| 9 | **Why** | Matches the shipped N31 template-owned model and the product authority in SCHEMA-RAT-01 §1.2/§5; eliminates the 3-writer divergence (authority principle A12); the profile is a template attribute, not a user attribute — a per-user store of a template-owned fact is the definition of a mirror (A6); Option B would reverse an already-executed convergence; Option C perpetuates divergent profiles between the view and login resolution. |
| 10 | **Tables/columns affected** | `access_template_profiles` (`functional_profile`, `updated_at_utc`); `internal_users.profile_title` (mirror → removed); N31 function `ba_dmo_ensure_access_template_profile` + trigger (kept under A). |
| 11 | **Current writers** | `TemplateProfileStore` (Web raw SQL), `AdminUserService` → `DapperAdminRepository.UpdateUserAsync`, N31 trigger/backfill/sync, `DapperInternalUserRepository.CreateBootstrapAdminAsync`. |
| 12 | **Current readers** | `IdentityResolutionService.ResolveAsync` (login), `CountActiveAdminsOnAsync` (self-lockout), Admin user-list projection (`DapperAdminRepository.UserColumns`), `TemplateProfileStore` (reads the profile table), tests (`IdentityResolutionServiceTests`, `AdminWebAuthorizationTests`, `AdminUserServiceTests`). |
| 13 | **Migration/data risk** | MEDIUM. Reader switch is the login path; reconcile `profile_title ← access_template_profiles` first (SCHEMA-RAT-01 Phase B), then switch readers (Phase D), then drop the column (Phase F). Fail-closed on unexpected divergence (never invent profiles). |
| 14 | **Becomes authoritative** | `access_template_profiles.functional_profile` (template-owned; one profile per template via PK + CHECK). |
| 15 | **Becomes derived/mirror/legacy** | `internal_users.profile_title` → derived mirror → LEGACY_REMOVE_LATER (column). Legacy `capabilities[]` inside template `modules` remain inert/non-authoritative (see D-6 note). |
| 16 | **Reversible** | Yes in steps: writer consolidation and reader switch are code-level and revertible; the column drop is one-way (gated in Phase F, post-parity). |
| 17 | **Owner approval before N32** | **YES — P0.** Determines Phase A guard design, Phase C writer consolidation, and Phase D reader switch. |

---

## D-2 — User→template single store (USER → TEMPLATE)

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-2 (SCHEMA-RAT-01 §11 #2) |
| 2 | **Question** | Which structure owns "the user's single effective template": **A) `internal_users.template_id` as canonical direct FK** or **B) `internal_user_access_templates` as canonical relationship with one-user uniqueness**? Product rule (unchallengeable): **exactly ONE effective template per user**; changing the user's template REPLACES previous effective access; templates never accumulate; no hybrid Admin+Operador+Responsável model. |
| 3 | **Current state** | Both stores exist and are kept in sync in one UoW (`DapperAdminRepository.CreateInternalUserAsync` CTE; `ReplaceUserAccessTemplatesAsync` = guarded `UPDATE internal_users SET template_id = ids[0]` + `DELETE` + `INSERT` of junction rows). N31 made the junction **1:1** (`ux_internal_user_access_templates_actor UNIQUE(actor_id)`) and explicitly calls `internal_users.template_id` the "compatibility/authority pointer" (N31 header). `IdentityResolutionService` reads the junction list, falling back to a single record built from the `template_id` compatibility columns. A residual multi-template path remains in `AdminUserService.CreateUserAsync`/`ChangeTemplatesAsync` (`templateIds.Length > 1` → unhandled 23505 from the unique index), though every Web form sends exactly one template. |
| 4 | **Competing authorities** | `internal_users.template_id` (NOT NULL FK → `access_templates`; required for every user row) **vs** `internal_user_access_templates` (junction PK `(actor_id, template_id)`, UNIQUE `(actor_id)`). Both carry the same fact: the user's single template. |
| 5 | **Option A** | `internal_users.template_id` is the **canonical direct FK** — the single stored fact. The junction either (i) becomes an **append-only assignment-history table** (`actor_id`, `template_id`, `assigned_at_utc`, `assigned_by`; one row per assignment change; unique index replaced by a plain index) if template-assignment audit is required, or (ii) is removed entirely (assignment metadata folded into `internal_users` or dropped). |
| 6 | **Option B** | `internal_user_access_templates` is the **canonical relationship** with one-user uniqueness — all reads/writes go through the junction; `internal_users.template_id` becomes a compatibility mirror and is eventually dropped (or stays as a synced convenience column). |
| 7 | **Option C** *(genuinely necessary)* | Interim only, not a final target: keep both stores with a DB consistency guard (trigger/deferred constraint asserting `template_id = (SELECT template_id FROM internal_user_access_templates WHERE actor_id = …)`) while writers/readers migrate. |
| 8 | **Recommended** | **Option A — `internal_users.template_id` as canonical direct FK.** Junction → append-only history **only if** the owner confirms assignment-change audit is a requirement; otherwise junction removed. Option C guard for the transition window. |
| 9 | **Why** | Do **not** prefer a junction merely because it is normalized: at cardinality 1:1 a junction with a UNIQUE(actor_id) carries **zero extra semantics** — it is a pure mirror of `template_id` with an extra write path to keep in sync (A6/A9). Under the product rule (exactly one template), the relationship is a plain 1:1 fact, and a direct FK is the simplest correct storage. `template_id` is already NOT NULL + FK-constrained, is the N31 lineage pointer, is used by identity resolution fallback and by both admin write paths; Option B would force the higher-churn change (identity fallback, self-lockout joins, N31 semantics) to serve no added semantics. Option B only becomes attractive if multi-template is ever allowed — which the product explicitly rejects. If auditability of template changes is wanted, the junction's value is realized as an **append-only history** (A + history), strictly better than a 1:1 mirror. |
| 10 | **Tables/columns affected** | `internal_users.template_id`; `internal_user_access_templates` (junction rows + `ux_internal_user_access_templates_actor`); optional new `internal_user_template_history`; FK target `access_templates`. |
| 11 | **Current writers** | `DapperAdminRepository.CreateInternalUserAsync`, `ReplaceUserAccessTemplatesAsync`; `DapperInternalUserRepository.CreateBootstrapAdminAsync`; N31 DML (collapse + re-insert). |
| 12 | **Current readers** | `DapperInternalUserRepository.FindByAuthUserIdAsync` (junction primary, `template_id` fallback), `IdentityResolutionService` (junction list + fallback), `DapperAdminRepository.UserColumns` (junction subquery + `template_id`), `CountActiveAdminsOnAsync` (junction join), N31 sync DML. |
| 13 | **Migration/data risk** | MEDIUM. Phase B reconcile (junction == `template_id` per actor; fail-closed), Phase C single writer, Phase D reader switch (login + self-lockout), Phase F junction removal/re-shape (destructive, gated, row-count zero guard; if history chosen, the history conversion is additive). |
| 14 | **Becomes authoritative** | `internal_users.template_id` (direct FK) — the user's single effective template; DB-enforced exactly-one (NOT NULL FK + optional guard while the junction exists; on removal the FK itself is the constraint). |
| 15 | **Becomes derived/mirror/legacy** | The 1:1 junction row set → mirror eliminated (or converted to history). Nothing else becomes legacy. |
| 16 | **Reversible** | Yes up to Phase F (guard + reader switch are revertible); junction removal/reshaping is one-way after parity. |
| 17 | **Owner approval before N32** | **YES — P0.** Shapes the Phase A guard, Phase C writer, Phase D reader, and Phase F junction disposition. |

---

## D-3 — Enforce single-template at the Application edge

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-3 (SCHEMA-RAT-01 §11 #3) |
| 2 | **Question** | Should `AdminUserService` reject multiple template ids in `CreateUserAsync`/`ChangeTemplatesAsync`/`SaveUserAsync` with a typed domain error (`ADMIN_SINGLE_TEMPLATE`), or keep the plural path that today can raise an unhandled 23505? |
| 3 | **Current state** | Service methods accept `IReadOnlyList<string>` and forward all ids to `DapperAdminRepository.ReplaceUserAccessTemplatesAsync` (INSERT per id); N31's unique index `ux_internal_user_access_templates_actor` makes a second junction row a raw `PostgresException` 23505 → 500 with full rollback. All Web forms (`Users/Create`, `Users/Edit`) send exactly one template; the plural path is unreachable from the shipped UI but live at the service edge. |
| 4 | **Competing authorities** | None new — this decision closes the sync between the Application contract (plural) and the DB-enforced single-assignment rule (N31). |
| 5 | **Option A** | Enforce exactly-one at the service edge (reject `templateIds.Count != 1` with a typed error); the DB unique index stays as backstop. |
| 6 | **Option B** | Keep the plural service path as-is (relies on the DB unique index to fail; unhandled 23505 for any non-Web caller). |
| 7 | — | Option C not needed. |
| 8 | **Recommended** | **Option A.** |
| 9 | **Why** | One write path per fact (A12); a domain error is actionable where a raw 23505 is a 500; consistent with the product rule (one template) and with D-2 => A (template_id canonical); zero Web-visible change. |
| 10 | **Tables/columns affected** | Write path only: `internal_users`, `internal_user_access_templates` (no schema change). |
| 11 | **Current writers** | `AdminUserService.CreateUserAsync`/`ChangeTemplatesAsync`/`SaveUserAsync` → `DapperAdminRepository.ReplaceUserAccessTemplatesAsync`. |
| 12 | **Current readers** | Web forms (single template), Admin user edit page, `IdentityResolutionService` (fail-closed `ACCESS_TEMPLATE_AMBIGUOUS` if >1 row), relevant tests. |
| 13 | **Migration/data risk** | LOW-MEDIUM — contract change only; current UI already conforms; scripted reconcile not required. |
| 14 | **Becomes authoritative** | The "one template per user" rule at the Application boundary (DB index remains the backstop). |
| 15 | **Becomes derived/mirror/legacy** | The plural template-list contract becomes legacy (API surface cleaned up). |
| 16 | **Reversible** | Yes — pure code change. |
| 17 | **Owner approval before N32** | **YES — P1** (must resolve before the Phase C writer switch; not a P0 because it does not change the final schema shape). |

---

## D-4 — Job On write surface scope ★

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-4 (SCHEMA-RAT-01 §11 #4) |
| 2 | **Question** | Is the Job On **write family** — create, duplicate, save-revision, lifecycle transition, confirm-verification — in the shipped Web app (wire endpoints now), or explicitly deferred (keep repository-level, add no routes, mark dormant interfaces)? |
| 3 | **Current state** | The repository write surface is complete and, since HEAD 683765f, internally consistent (lifecycle timestamps persisted in one UoW with the audit event). But the Web surface exposes only images, current-context and document endpoints; no route calls create/duplicate/save/transition/confirm. The N25 constraints (`ck_job_on_lifecycle_consistent`, `uq_job_on_identity`) are therefore exercised only by tests, not at runtime. |
| 4 | **Competing authorities** | Not a data-authority question — a scope question between the implemented Application/repository layer and the shipped Web surface (no competing persistence stores). |
| 5 | **Option A** | Wire the write endpoints now (expose create/duplicate/save/transition/confirm under `jobon.edit`/`jobon.confirmar`). |
| 6 | **Option B** | Defer: keep repository-level, add no routes, explicitly mark the write surface dormant, and prove the lifecycle with real-PostgreSQL tests (extend the `BA_DMO_TEST_DATABASE` guard suite). |
| 7 | — | Option C not needed. |
| 8 | **Recommended** | **Option B (defer)** for the shipped app, with the real-PG lifecycle tests as a mandatory companion. Decision D-5 (dual audit) still applies at repository level regardless. |
| 9 | **Why** | Matches the currently shipped surface; the most complex schema area is best proven incrementally; the lifecycle defect class is already fixed at HEAD, so deferring is low-risk as long as tests prove the DB constraints; wiring now would expand per-tab functionality before the read surface is stable. |
| 10 | **Tables/columns affected** | `job_on`, `job_on_revision`/`component`/`field`/`row`, `job_on_verification_occurrence`, `job_on_audit_event` (no schema change). |
| 11 | **Current writers** | `DapperJobOnRepository` (write surface; no HTTP consumers). |
| 12 | **Current readers** | Images/context/document endpoints; context lookups (Peso/Pegamentos/Controlo/RI); tests. |
| 13 | **Migration/data risk** | Option B: LOW (no behavior change; tests added). Option A: MEDIUM (exposes the most complex schema area first). |
| 14 | **Becomes authoritative** | The Job On aggregate + revision graph as the production-context write source (unchanged either way). |
| 15 | **Becomes derived/mirror/legacy** | If B: the wire-able write service methods remain "dormant surface" (not a persistence mirror). |
| 16 | **Reversible** | Yes — scoping decision; can be revisited anytime. |
| 17 | **Owner approval before N32** | **YES — P1** (product scope; does not block N32 design since no schema change rides on it). |

---

## D-5 — Job On dual audit (domain history vs global audit projection)

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-5 (SCHEMA-RAT-01 §11 #5) |
| 2 | **Question** | Should Job On emit compact `audit_events` rows (module `jobon`) **in the same transaction** as every mutation, alongside the existing `job_on_audit_event`? Concretely: distinguish **domain history** from **global audit projection**, and decide whether dual emission creates **duplicate authority** — and how to avoid it. |
| 3 | **Current state** | `job_on_audit_event` is written for every Job On mutation (create/duplicate/save/image/lifecycle) with FKs to `job_on`/`job_on_revision`, actor, before/after snapshots (now JSON-valid via `AuditJson.Normalize` + `::jsonb` at HEAD). **No Job On flow writes `audit_events`**; `DapperHistoriaRepository` reads only `audit_events`. Consequence: transversal História is blind to Job On activity — an authority *gap* (missing projection), not a conflict. |
| 4 | **Competing authorities** | Two tables with **different concepts**: `job_on_audit_event` = domain event stream (category 1 in SCHEMA-RAT-01 §7: reconstruction + attribution, revision-linked, before/after detail) vs `audit_events` = global compliance/history projection (category 2: slim denormalized cross-module rows for História/Admin Auditoria). They are NOT interchangeable; neither replaces the other. Dual emission creates duplicate **records**, not duplicate **authority**, if and only if one side is the authoritative fact store and the other is a derived projection produced by the same writer. |
| 5 | **Option A** | **Dual-emit**: a single repository method writes `job_on_audit_event` (authority) AND a compact `audit_events` row (module `jobon`; action code, entity, result, actor, time, correlation id) in the same `DapperUnitOfWork`. |
| 6 | **Option B** | Keep module-only emission (current) — document the História gap as a known limitation. |
| 7 | **Option C** *(genuinely necessary)* | Drop `job_on_audit_event` and emit only `audit_events` (rejected: loses FK-rich reconstruction, revision attribution, before/after detail; would require re-shaping História queries). |
| 8 | **Recommended** | **Option A** — dual-emit with a **single writer** and **same-UoW** semantics. |
| 9 | **Why** | Domain history must stay domain-owned (SCHEMA-RAT-01 §7.3: do not centralize reconstruction into `audit_events`); the global layer needs Job On facts for História/Admin. Duplicate authority is avoided by four rules: (1) one writer method produces both rows in one transaction — there is never an independent editor of either side; (2) `job_on_audit_event` is defined as the authoritative fact store and `audit_events` as a **derived projection** of selected facts (no writing of audit_events rows for Job On anywhere else); (3) both tables are append-only (triggers) — no correction path diverges them; (4) a parity guard test asserts every `job_on_audit_event` fact of the projectionable set has exactly one `audit_events` row. With these, dual emission is a fan-out, not a mirror. |
| 10 | **Tables/columns affected** | `job_on_audit_event` (existing), `audit_events` (new write path for module `jobon`; existing columns suffice — no schema change). |
| 11 | **Current writers** | `DapperJobOnRepository` → `job_on_audit_event` only (lifecycle path already emits within the UoW). |
| 12 | **Current readers** | Job On views/PDF (audit stream), context lookups (revision), `DapperHistoriaRepository` (global), Admin Auditoria (global). |
| 13 | **Migration/data risk** | LOW-MEDIUM — additive write; backfill of historical Job On facts into `audit_events` is *optional* (recommend forward-only; backfill only if História completeness must be retroactive — owner choice). |
| 14 | **Becomes authoritative** | `job_on_audit_event` = Job On domain history authority; `audit_events` = derived global projection for História/Admin. |
| 15 | **Becomes derived/mirror/legacy** | `audit_events` rows for module `jobon` are the derived projection (not a mirror of reconstruction detail). |
| 16 | **Reversible** | Yes — additive code change; can be rolled back without schema damage. |
| 17 | **Owner approval before N32** | **YES — P1** (writer change in Phase C; independent of P0 schema shape). |

---

## D-6 — Module catalog authority and `module_catalog_mirror` (Applications)

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-6 (SCHEMA-RAT-01 §11 #6, plus §9 A5/A6) |
| 2 | **Question** | What does "**Applications is the canonical module catalog**" mean *technically*, and what happens to `module_catalog_mirror`? Resolve the authority relationship so that **code and database are not both writable authorities**. |
| 3 | **Current state** | The module catalog exists in three places with different roles: (1) **Aplicações** = the Admin page surface (`/admin/applications`) that *reflects* the catalog; (2) **in-code `CanonicalModuleCatalog`** = the compiled definition of what modules exist (12 entries; 10 assignable; capability ids; canonical order; initial routes; assignability rules incl. `peso`/`pegamentos`/`historia` non-assignable), validated at composition by `CatalogValidator`; (3) **`module_catalog_mirror`** = a persisted display projection (`module_id`, `display_name`, `display_order`, `active`, `synced_at_utc`) written one-way by `ModuleCatalogMirrorSynchronizer` via `AdminMirrorService`, with Admin display-order edits allowed; it is consulted by the Aplicações page and **never** by authorization (`AccessResolver` derives access from template grants ∩ catalog in code; RLS grants no module access; `ba_dmo_app` is a technical role only). |
| 4 | **Competing authorities** | `CanonicalModuleCatalog` (code; authoritative) vs `module_catalog_mirror` (DB; display read-model). There is no third authority: SQL never grants a module; `access_templates.modules` stores the *selection* from the catalog (validated against it), not the catalog itself. |
| 5 | **Option A** | Keep `module_catalog_mirror` as an **explicit, documented read-model**: its only writer remains the synchronizer (one-way from the compiled catalog); display-order edits in Admin are validated against the catalog and are display-only; the mirror is excluded from all authorization paths by contract and guard test. |
| 6 | **Option B** | **Eliminate the mirror**: drop the table/repository/service; Aplicações renders directly from `CanonicalModuleCatalog` (display order lives in code). |
| 7 | **Option C** *(genuinely necessary — rejected)* | Materialize the catalog as a DB table loaded at startup and edited through SQL/Admin (creates a second writable authority; rejected). |
| 8 | **Recommended** | **Option A now** (keep as read-model with the synchronizer as sole writer), with Option B as the later simplification when Admin display order no longer needs persistence. Never Option C. |
| 9 | **Why** | "Applications is the canonical module catalog" resolves to: **the in-code `CanonicalModuleCatalog` is the single technical authority** — the database defines no module, grants no access, and stores only a display projection; the Aplicações surface reflects authority, it does not hold it (SCHEMA-RAT-01 §5.1 rows: catalog authority = code). Keeping the mirror as a one-way read-model satisfies "do not leave code and database both writable" because the mirror's writes are display-only, catalog-validated, and never consulted for access; eliminating it (B) is the stricter option if the product prefers zero persistence of catalog display. |
| 10 | **Tables/columns affected** | `module_catalog_mirror` (+ `ix_module_catalog_mirror_order`); in-code `CanonicalModuleCatalog`/`CanonicalPageCatalog`; N02/N12 objects if removed. |
| 11 | **Current writers** | `DapperModuleCatalogMirrorRepository.UpsertAllAsync` (delete-stale + upsert in UoW) via `AdminMirrorService` + `ModuleCatalogMirrorSynchronizer`. |
| 12 | **Current readers** | `/admin/applications` page; tests (`ModuleCatalogMirrorSynchronizerTests`, `AdminAuditAndMirrorTests`, `AdminWebAuthorizationTests`). |
| 13 | **Migration/data risk** | LOW — content is re-syncable from code at any time; removing the mirror only affects the Aplicações display path (switch page to the compiled catalog). |
| 14 | **Becomes authoritative** | `CanonicalModuleCatalog` (in-code) — the one technical authority for module existence, assignability, display metadata and capabilities; "Applications is the canonical module catalog" = the Aplicações surface renders this authority. |
| 15 | **Becomes derived/mirror/legacy** | `module_catalog_mirror` = derived read-model (A) or removed (B); legacy `capabilities[]` inside `access_templates.modules` = inert legacy data (never consulted; already `[]`); mirror fields (`display_name/display_order/active`) are derived, never authoritative. |
| 16 | **Reversible** | Yes — read-model keep/remove is code+DDL-removal with trivial re-sync. |
| 17 | **Owner approval before N32** | **YES — P0** (must resolve before any N32 design: it fixes the "single technical authority for the catalog" rule that the default decision set requires, and decides whether Phase A/F touches `module_catalog_mirror`). |

---

## D-7 — `job_on_field_option` disposition ★

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-7 (SCHEMA-RAT-01 §11 #7; §8.6) |
| 2 | **Question** | Wire the Job On data-driven dropdown catalog (Definições surface: repository + API + page handler) or retire the table/domain record? |
| 3 | **Current state** | `job_on_field_option` (N05: `UNIQUE(family, field_key, option_value)`, `active`, index) has **zero code consumers** — no repository, service, endpoint, or test references it anywhere in `src/` (verified by grep in SCHEMA-RAT-01 §8.6). A *presentational* Definições UI exists in `jobon.js` (catalog-option CRUD elements) with no persistence path. The table is empty by construction. |
| 4 | **Competing authorities** | None (no competing store). The question is surface scope: a sound config-table pattern with no consumer. |
| 5 | **Option A** | **KEEP (dormant, future-owned)** — leave table + domain record as-is; wire later when the Definições dropdown surface is in the roadmap. |
| 6 | **Option B** | **REMOVE_LATER** — drop the table + domain record + presentational UI in Phase F if the product confirms no dropdown-catalog surface. |
| 7 | — | Option C not needed (wiring now without a product commitment is a variant of A). |
| 8 | **Recommended** | **OWNER_PRODUCT_DECISION — default A (KEEP dormant).** |
| 9 | **Why** | Evidence (SCHEMA-RAT-01 §8.6): zero writers/readers, zero data written, no migration risk either way; the config-table pattern matches the family/field dropdown requirement and the DMO data-driven-catalog convention; keeping it costs nothing; removing it is cheap and safe later; wiring it (repository + API + handler) is only worth doing when the Definições surface is actually shipped. |
| 10 | **Tables/columns affected** | `job_on_field_option` (+ unique/index). |
| 11 | **Current writers** | None. |
| 12 | **Current readers** | None. |
| 13 | **Migration/data risk** | NONE today (empty); if removed in Phase F, row-count-zero guard. |
| 14 | **Becomes authoritative** | If wired: the dropdown-config source for family/field options (validated against `ComponentFamily`/field keys). While dormant: nothing. |
| 15 | **Becomes derived/mirror/legacy** | If removed: the domain record `JobOnFieldOption` and the presentational UI become legacy. |
| 16 | **Reversible** | Yes — dormant keep is trivially reversible; removal is one-way but cheap (empty table). |
| 17 | **Owner approval before N32** | **Not required before N32 design — required only if Phase F removal is planned** (P2). |

---

## D-8 — `tampao_planos` disposition ★

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-8 (SCHEMA-RAT-01 §11 #8; §8.5) |
| 2 | **Question** | Wire the Tampões Planeamento surface (tab + `/api/tampoes/plan*` endpoints) or retire the planning feature and table? |
| 3 | **Current state** | Full stack implemented: domain `TampaoPlano`, `TampaoService.PlanearAsync`/`CancelarPlanoAsync`/`ListPlanosAsync` (+ audit codes `tampoes.planear`/`tampoes.plano.cancelar`), `DapperTampaoRepository` CRUD (`CreatePlanoAsync`/`GetPlanoByIdAsync`/`CancelPlanoAsync`/`ListPlanosAsync`), `tampao_planos` table (N10: FK config, `planned_qty>=1`, indexes, logical `job_on_id`/`production_code`). **Zero Web surface**: no Planeamento tab in `Index.cshtml`, no routes in `Program.cs`, `tampoes.js` never calls it; `TampaoWebApiTests.Planeamento_IsAbsentFromRenderedSurface_AndEndpoints` asserts 404s; stale `#planosTable` CSS remains. |
| 4 | **Competing authorities** | None — no competing store. Dormant/future-owned feature surface (planear ≠ reservar; cancelling a plan never touches balances). |
| 5 | **Option A** | **Wire it** (restore Planeamento tab + API + JS) — ready feature. |
| 6 | **Option B** | **REMOVE_LATER** — retire service methods, endpoints (none), and the table in Phase F. |
| 7 | **Option C** *(genuinely necessary)* | **KEEP dormant** (interim): implementation stays, no surface, documented as future-owned. |
| 8 | **Recommended** | **OWNER_PRODUCT_DECISION — default C (KEEP dormant)**; wire (A) when the planning feature is committed; retire (B) only if the product confirms no planning surface. |
| 9 | **Why** | Evidence (SCHEMA-RAT-01 §8.5): the implementation is complete and unit-tested ("planning does not reserve; cancel preserves balances"), so the marginal cost of wiring is an endpoint block + JS, not schema work; no data exists; retiring is cheap but destroys ready capability; keeping dormant preserves the tested code path at zero runtime cost. |
| 10 | **Tables/columns affected** | `tampao_planos` + its two indexes (or new API surface if wired). |
| 11 | **Current writers** | `DapperTampaoRepository` CRUD via `TampaoService` (no HTTP callers). |
| 12 | **Current readers** | `TampaoService.ListPlanosAsync` (service-level only); tests. |
| 13 | **Migration/data risk** | NONE today (no surface, no rows); WIRE → zero data migration (fresh feature data); RETIRE → row-count-zero guard in Phase F. |
| 14 | **Becomes authoritative** | If wired: the planning fact store (planned needs, independent of balances). While dormant: nothing. |
| 15 | **Becomes derived/mirror/legacy** | If removed: `TampaoPlano` domain record, planning service methods, DTOs and `#planosTable` CSS become legacy. |
| 16 | **Reversible** | Yes (dormant keep); removal one-way but cheap. |
| 17 | **Owner approval before N32** | **Not required before N32 design — product decision required before Phase F** (P2). |

---

## D-9 — `peso_comparacao_anterior` disposition

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-9 (SCHEMA-RAT-01 §11 #9; §8.1) |
| 2 | **Question** | Drop the dead "previous approved control" mirror table, or materialize it with a real writer? |
| 3 | **Current state** | `peso_comparacao_anterior` (N06: PK `peso_controlo_id` ON DELETE CASCADE, FK `previous_peso_controlo_id`, `previous_snapshot`, `deltas`, `resolved_at_utc`) is **never read or written** by any code (SCHEMA-RAT-01 §8.1 grep verification); it appears only in doc comments. The actual previous-approved resolution is the **live query** `DapperPesoRepository.GetPreviousApprovedAsync` over `peso_controlos` (`DapperPesoRepository.cs:417+`). The table is empty by construction. |
| 4 | **Competing authorities** | Declared persisted read path (table, never populated) vs live query (operative). Classic mirror of queryable information with no writer consensus (A6). |
| 5 | **Option A** | **REMOVE_LATER** — drop the table in Phase F with a row-count-zero guard; the live query remains the sole authority. |
| 6 | **Option B** | Materialize: add a writer + invalidation contract so the table becomes a genuine cached read path. |
| 7 | — | Option C not needed. |
| 8 | **Recommended** | **Option A — REMOVE_LATER (Phase F).** Verdict: `peso_comparacao_anterior` = **REMOVE_LATER** (MIRROR_ELIMINATE). |
| 9 | **Why** | Evidence (SCHEMA-RAT-01 §8.1/§3 row 24): zero writers, zero readers, zero tests, zero rows; the live query is correct (cross-line previous approved by date) and already indexed (`ix_peso_controlos_status_date`); keeping an unpopulated twin is duplicate authority risk with no benefit; materializing (B) adds a writer + invalidation problem with no demonstrated query-pressure. |
| 10 | **Tables/columns affected** | `peso_comparacao_anterior` (drop) — PK/FK on `peso_controlos` cascade. |
| 11 | **Current writers** | None. |
| 12 | **Current readers** | None (the live query `GetPreviousApprovedAsync` is the reader path; `PesoControl`/`IPesoRepository` doc comments reference the table's intended role only). |
| 13 | **Migration/data risk** | NONE — empty table; guarded drop is pure cleanup. |
| 14 | **Becomes authoritative** | `peso_controlos` (via the `GetPreviousApprovedAsync` live query) — previous-approved resolution. |
| 15 | **Becomes derived/mirror/legacy** | The table itself → legacy/removed; `GetApprovedControlsForJobOnAsync`/`GetPreviousApprovedAsync` repository surface is normal (used or unused methods to prune at code level). |
| 16 | **Reversible** | Removal is one-way but trivially reconstructible (table empty; DDL is in N06 history). |
| 17 | **Owner approval before N32** | **Not required before N32 design — approval required before the Phase F drop** (P2). |

---

## D-10 — Peso readings immutability under approved parents

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-10 (SCHEMA-RAT-01 §11 #10; §9 B3) |
| 2 | **Question** | Should `peso_leituras` of an approved `peso_controlos` be immutable (DB-guard and/or service rule), closing the silent-rewrite path in `UpdateControlAsync`? |
| 3 | **Current state** | N25 guards `peso_controlos` (approved rows: identity columns immutable, DELETE blocked via `ba_dmo_guard_peso_approved`), but `peso_leituras` has **no guard**; `DapperPesoRepository.UpdateControlAsync` deletes all leituras and re-inserts them (DELETE+INSERT inside a UoW). An approved control's readings can therefore still be rewritten — contrary to the immutability contract. |
| 4 | **Competing authorities** | None — this is a missing *guard* (constraint/invariant), not a competing store. |
| 5 | **Option A** | DB trigger on `peso_leituras` (BEFORE UPDATE/DELETE) raising when the parent control is approved + service-level assertion in `UpdateControlAsync`. |
| 6 | **Option B** | Service enforcement only (Application rule; no DDL). |
| 7 | — | Option C not needed. |
| 8 | **Recommended** | **Option A** (DB backstop mirroring the existing `ba_dmo_guard_peso_approved` pattern; service check as the primary gate). |
| 9 | **Why** | The immutable-approved fact contract is already DB-enforced for the control row; readings are part of the same fact. DB enforcement closes races and survives any future service path (A3/A12); consistency with the existing N25 guard style minimizes review burden. |
| 10 | **Tables/columns affected** | `peso_leituras` (new trigger), relationship to `peso_controlos` (read-only join). |
| 11 | **Current writers** | `DapperPesoRepository.CreateControlAsync`/`UpdateControlAsync` (DELETE+INSERT). |
| 12 | **Current readers** | control detail/compare listings, PDF, `ExtractSnapshotAverages`. |
| 13 | **Migration/data risk** | LOW-MEDIUM — new trigger may reject legitimate legacy flows if any code edits readings of approved controls (none known); Phase A additive. |
| 14 | **Becomes authoritative** | The approved-control fact (control + readings) as immutable. |
| 15 | **Becomes derived/mirror/legacy** | Nothing becomes mirror/legacy; the rewrite path becomes invalid. |
| 16 | **Reversible** | Yes — trigger/service rule can be dropped. |
| 17 | **Owner approval before N32** | **YES — P1** (writer-path change; Phase A DDL). |

---

## D-11 — Dormant columns: `modules_override` and `image_asset_id`

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-11 (SCHEMA-RAT-01 §11 #11; §8.3/§8.4) |
| 2 | **Question** | Remove the two dormant legacy columns — `internal_users.modules_override` (N26) and `job_on_revision.image_asset_id` (N05) — in Phase F, or keep them permanently as auditability remnants? |
| 3 | **Current state** | `modules_override`: added N26, **NULLed for all rows** by N27, never read at runtime (`IdentityResolutionService` ignores it; test `ModulesOverride_IsDormant_AndDoesNotReplaceTemplateModules`), still projected by `DapperAdminRepository.UserColumns` and `DapperInternalUserRepository.FindByAuthUserIdSql`, and its only writer `SetUserModulesOverrideAsync` has no callers; the N26-missing detection relies on catching SQLSTATE 42703 → `SchemaMigrationRequiredException`. `image_asset_id`: added N05, **superseded by N29** (`article_reference_images`); `IJobOnRepository.InsertImageMutationAsync` dead (no production callers; tests pin the no-revision-created contract); revision INSERT statements still carry the column. |
| 4 | **Competing authorities** | `modules_override` vs template modules (N27/N31 model — dormant mirror); `image_asset_id` vs `article_reference_images` (dormant mirror, resolved by N29). Both are A7 legacy data. |
| 5 | **Option A** | **REMOVE_LATER** — delete projections + dormant port methods first, replace the 42703 schema-gate with an explicit mechanism, then drop both columns in Phase F. |
| 6 | **Option B** | KEEP both columns indefinitely as documented auditability remnants (readable, never written). |
| 7 | — | Option C not needed. |
| 8 | **Recommended** | **Option A — REMOVE_LATER for both.** Verdict: `modules_override` = **REMOVE_LATER**; `image_asset_id` = **REMOVE_LATER**. |
| 9 | **Why** | Evidence (SCHEMA-RAT-01 §8.3/§8.4): zero live writers/readers, all legacy rows already NULL/null, both stores have a live replacement authority (template modules; `article_reference_images`). Keeping dormant columns preserves dead write surfaces and the fragile 42703 gate; removal is gated and testable. |
| 10 | **Tables/columns affected** | `internal_users.modules_override` (drop), `job_on_revision.image_asset_id` (drop); surrounding projections/ports, the 42703 gate. |
| 11 | **Current writers** | `SetUserModulesOverrideAsync` (uncalled), `DapperJobOnRepository.InsertImageMutationAsync` (uncalled), revision inserts (still write `image_asset_id`). |
| 12 | **Current readers** | `DapperAdminRepository.UserColumns`, `DapperInternalUserRepository.FindByAuthUserIdSql` (projection only), revision SELECTs. |
| 13 | **Migration/data risk** | LOW — rows NULL/absent; removal requires prior code cleanup (projections + gate replacement); Phase F destructive with guards. |
| 14 | **Becomes authoritative** | (Re-affirmed) template modules for user grants; `article_reference_images` for reference images. |
| 15 | **Becomes derived/mirror/legacy** | Both columns → legacy → REMOVE_LATER. |
| 16 | **Reversible** | Removal is one-way; both are inert so reconstruction is trivial if ever needed. |
| 17 | **Owner approval before N32** | **Not required before N32 design — required before Phase F drops** (P2). |

---

## D-12 — `pegamento_medicoes.contra_costura` nullability

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-12 (SCHEMA-RAT-01 §11 #12; §9 B7) |
| 2 | **Question** | Align the column to the domain (make `contra_costura` nullable with a domain-level rule for one-sided measurements) or align the domain to the column (require both `costura` and `contra_costura` for every measurement)? |
| 3 | **Current state** | Column is `NOT NULL` (N07); domain `PegamentoControlo`/measurement model supports one-sided measurements (`PegamentoMeasurementCalculator` handles `contra_costura` null), and `DapperPegamentoRepository` binds `ContraCostura ?? DBNull.Value`. A one-sided measurement insert therefore always raises 23502. |
| 4 | **Competing authorities** | DB constraint (NOT NULL) vs domain capability (one-sided allowed). Schema contradicts documented domain behavior. |
| 5 | **Option A** | Make the column nullable (Phase A DDL) + domain-level completeness rule (a measurement must have `costura`; `contra_costura` optional with explicit semantics). |
| 6 | **Option B** | Keep NOT NULL and require both values in the domain/API (drop the one-sided capability). |
| 7 | — | Option C not needed. |
| 8 | **Recommended** | **Option A** — column nullable, domain rule enforces the intended shape. |
| 9 | **Why** | The stated domain capability is one-sided measurements (SCHEMA-RAT-01 §9 B7, citing `PegamentoMeasurementCalculator`); the NOT NULL column silently breaks that capability; a nullable column + explicit domain rule is the smallest change that makes schema and behavior agree, and preserves historical rows (existing rows unchanged). |
| 10 | **Tables/columns affected** | `pegamento_medicoes.contra_costura` (nullability), related validator. |
| 11 | **Current writers** | `DapperPegamentoRepository` (binds nullable), `PegamentoControlo` domain. |
| 12 | **Current readers** | measurement listings, PDF renderer, averages calculation. |
| 13 | **Migration/data risk** | LOW — nullability change only; existing data valid both ways. |
| 14 | **Becomes authoritative** | The domain measurement rule (documented in code) + schema agreement. |
| 15 | **Becomes derived/mirror/legacy** | The NOT NULL constraint becomes legacy; no other mirror. |
| 16 | **Reversible** | Yes (Phase A DDL). |
| 17 | **Owner approval before N32** | **YES — P1** (Phase A DDL + writer/validator change). |

---

## D-13 — Audit co-transactionality policy

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-13 (SCHEMA-RAT-01 §11 #13; §9 B5) |
| 2 | **Question** | Adopt "business write + its audit row commit or roll back together" as a global policy for every module, and migrate the post-commit/separate-connection emitters onto their existing UoW/`InsertAuditEventAsync`-in-transaction pattern? |
| 3 | **Current state** | HEAD already applies in-UoW audit emission in the image and Job On lifecycle paths and normalizes payloads (`AuditJson.Normalize` + `::jsonb`). Several other modules still emit `audit_events` on a separate connection after commit (`DapperArmazemRepository`, `DapperRepairRepository` service paths, `DapperPesoRepository`, etc.), risking lost/duplicated audit rows on partial failure (SCHEMA-RAT-01 §1.2/§9). If D-5 = A, Job On emission joins the same rule. |
| 4 | **Competing authorities** | None — a transaction-shape policy; `audit_events` remains the single global audit store. |
| 5 | **Option A** | **Co-transactional everywhere**: business write + audit insert in one `DapperUnitOfWork`/`IDbUnitOfWork` scope across all modules. |
| 6 | **Option B** | Keep best-effort post-commit audit for some modules (accept audit gaps under failure). |
| 7 | — | Option C not needed. |
| 8 | **Recommended** | **Option A.** |
| 9 | **Why** | Audit is a fact of the write; post-commit emission makes audit lossy under failures (SCHEMA-RAT-01 §7.3/§9 B5); the UoW infrastructure and the HEAD conventions already exist — this is a mechanical migration of emitters, not new architecture; it also underpins D-5's dual-emit correctness. |
| 10 | **Tables/columns affected** | `audit_events` (+ `job_on_audit_event` if D-5=A); transaction shapes in `DapperArmazemRepository`, `DapperRepairRepository`, `DapperPesoRepository`, `DapperFerramentasRepository`, `DapperBoquilhasRepository`, `DapperTampaoRepository`, `DapperControloSheetRepository`, `DapperReparacaoInternaRepository`. |
| 11 | **Current writers** | Per-module `InsertAuditEventAsync` (mix of in-UoW and post-commit). |
| 12 | **Current readers** | `DapperHistoriaRepository`, Admin Auditoria (`QueryAuditAsync`/export). |
| 13 | **Migration/data risk** | MEDIUM — changes transaction shapes across modules; no data migration; regression risk on failure-path behavior. |
| 14 | **Becomes authoritative** | The business write + audit pair as one atomic fact. |
| 15 | **Becomes derived/mirror/legacy** | The separate-connection emission pattern becomes legacy. |
| 16 | **Reversible** | Yes — per-module code change. |
| 17 | **Owner approval before N32** | **YES — P1** (Phase C writer migration). |

---

## D-14 — Armazém 1:1-per-position invariant

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-14 (SCHEMA-RAT-01 §11 #14; §9 B2) |
| 2 | **Question** | Is "at most one active occupation per position" a hard rule (then enforce via a per-position partial unique index and/or `FOR UPDATE` on the repair-return path), or a soft convention? |
| 3 | **Current state** | `uq_warehouse_stock_active_occupation` enforces one active per **(position, tool_lote)**; nothing enforces one active **per position** across lots. `RegisterEntradaAsync` locks (location `FOR UPDATE` + active-stock check); the repair-return path (`DapperArmazemRepairMovementRepository.ConfirmReturnAsync`) checks occupancy **without `FOR UPDATE`** (TOCTOU — two concurrent returns of different lots to the same empty position can both pass). Task note: locking and schema constraints are separate design concerns (ARMAZEM-01 uses locking) — this decision is about the *invariant*, not about which mechanism ships. |
| 4 | **Competing authorities** | None — a missing invariant; current partial unique index expresses a weaker rule than the physical 1:1 occupancy intent. |
| 5 | **Option A** | Hard rule: add a partial unique index on `(warehouse_location_id) WHERE released_at_utc IS NULL` (DB backstop) and/or `FOR UPDATE` on the repair-return occupancy check (code backstop). |
| 6 | **Option B** | Soft convention: keep the per-(location, lot) index; accept that two different lots could share a position (no invariant). |
| 7 | — | Option C not needed. |
| 8 | **Recommended** | **Option A** (hard rule). |
| 9 | **Why** | Occupancy 1:1 per position is the physical meaning of the Armazém map (SCHEMA-RAT-01 §9 B2); the current index under-expresses it and the repair path has a real TOCTOU; enforcing the invariant is a small Phase A additive index plus a lock in one method (the UoW already exists). Locking choice (FOR UPDATE vs index) is an implementation detail per ARMAZEM-01. |
| 10 | **Tables/columns affected** | `warehouse_stock` (new partial unique or lock), `warehouse_locations` (lock target). |
| 11 | **Current writers** | `DapperArmazemRepository` (entrada/saida/replace), `DapperArmazemRepairMovementRepository` (pickup/return). |
| 12 | **Current readers** | occupancy queries (`GetActiveStockBy*`, `ConsultarPorPosicao`), repair ports. |
| 13 | **Migration/data risk** | LOW-MEDIUM — new partial unique could fail if legacy data already violates 1:1 (reconcile/guard first); additive. |
| 14 | **Becomes authoritative** | `warehouse_stock` active rows with 1:1 per position (DB backstop + locked transitions). |
| 15 | **Becomes derived/mirror/legacy** | The current per-(location,lot) index remains (subset) or is superseded; no mirror introduced. |
| 16 | **Reversible** | Yes — index drop / lock removal. |
| 17 | **Owner approval before N32** | **YES — P1** (Phase A DDL + concurrency change). |

---

## D-15 — RLS policy naming convention

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-15 (SCHEMA-RAT-01 §11 #15; §9 B7) |
| 2 | **Question** | Unify the RLS policy naming to a single convention (`ba_dmo_app_access` everywhere), or accept the two coexisting conventions? |
| 3 | **Current state** | N12/N25/N29 use `ba_dmo_app_access`; N27 and N31 use `{table}_app_access` (`internal_user_access_templates_app_access`, `access_template_profiles_app_access`). Semantics are identical (`FOR ALL TO ba_dmo_app USING (true) WITH CHECK (true)`). |
| 4 | **Competing authorities** | None — naming divergence only; no access-authority difference. |
| 5 | **Option A** | Unify: rename the two `{table}_app_access` policies to `ba_dmo_app_access` in a Phase A migration (drop/create) and document the convention for future migrations. |
| 6 | **Option B** | Accept divergence (document in the maps). |
| 7 | — | Option C not needed. |
| 8 | **Recommended** | **Option A** (cheap, improves grep/tooling and guard-test uniformity). |
| 9 | **Why** | Cosmetic but real tooling cost (SCHEMA-RAT-01 §9 B7, §3 policy-naming note); a single convention makes the RLS inventory test trivially assertable. |
| 10 | **Tables/columns affected** | Policy objects on `internal_user_access_templates`, `access_template_profiles`. |
| 11 | **Current writers** | Migration-time only (N27/N31). |
| 12 | **Current readers** | RLS engine + guard tests; no runtime code dependency on policy names. |
| 13 | **Migration/data risk** | NONE (drop/create same-semantics policy; no data). |
| 14 | **Becomes authoritative** | The unified naming convention (technical standard). |
| 15 | **Becomes derived/mirror/legacy** | The `{table}_app_access` names → legacy. |
| 16 | **Reversible** | Yes. |
| 17 | **Owner approval before N32** | **No (technical convention)** — resolve in Phase A with the DBA review; not a business decision (P2). |

---

## D-16 — Consolidated clean-install baseline refresh ★

| # | Field | Content |
|---|---|---|
| 1 | **Decision ID** | D-16 (SCHEMA-RAT-01 §11 #16; §14) |
| 2 | **Question** | Approve refreshing `database/consolidated_clean_install.sql` to the final N31(+) state as part of the convergence (Phase G): add N31 objects, the `article_reference_images` security stanza, refresh the header/provenance, and reproduce the consolidated-equivalence verification? |
| 3 | **Current state** | The consolidated baseline (1,666 lines) does **not** contain the N31 objects (`access_template_profiles`, `ba_dmo_ensure_access_template_profile`, `trg_access_templates_ensure_profile`, `ux_internal_user_access_templates_actor` — zero grep matches) and it **omits the RLS/policy/grants stanza for `article_reference_images`** (created at lines 452-470 with table+constraints+index, but absent from the `rls_tables`/`policy_tables` arrays and with no `ba_dmo_app` GRANT). The header still claims "migration family N01 … N24" and references the old test name; the trailing comment says "includes N25-N27" while the body already contains N28-N30 objects. The referenced `reports/consolidated_schema_equivalence.md` does not exist in the repository. |
| 4 | **Competing authorities** | Migration chain (authoritative object source) vs consolidated baseline (clean-install reproduction). Drift documented in SCHEMA-RAT-01 §14 — not an authority conflict in production, but a real divergence for clean installs (TemplateProfileStore would fail loudly on 42P01 for missing N31; `article_reference_images` would be RLS-less → Supabase default-privilege exposure). |
| 5 | **Option A** | Refresh now (as Phase G of the approved convergence): N31 final state + `article_reference_images` RLS/policy/grants + accurate header + reproduced equivalence report/test. |
| 6 | **Option B** | Defer the refresh until after Phases A-F (baseline is regenerated once from the final state). |
| 7 | — | Option C not needed. |
| 8 | **Recommended** | **Option A — approve the refresh** as part of the N32+ plan (executed last, in Phase G, not now). The *approval* is required before the implementation plan is locked. |
| 9 | **Why** | Clean installs are the only path that produces the full runtime schema without the migration runner; today they silently diverge (missing N31 → loud 42P01; missing RLS on `article_reference_images` → silent security divergence). Refreshing is non-destructive file regeneration and closes the divergence documented in SCHEMA-RAT-01 §14; doing it once at the end (Phase G) avoids double work while the converged objects land. |
| 10 | **Tables/columns affected** | No live database object — one SQL file + the equivalence test; content mirrors: `access_template_profiles`, `article_reference_images` (RLS/grants), N31 trigger/function/index(es), header. |
| 11 | **Current writers** | N/A (file is read-only evidence; refreshed only by an owner-approved change). |
| 12 | **Current readers** | Fresh-install tooling, CI equivalence checks, operators. |
| 13 | **Migration/data risk** | NONE to existing databases (file only); equivalence test must cover the final 61(+Δ) table state. |
| 14 | **Becomes authoritative** | The refreshed consolidated baseline = the clean-install reproduction of the migration chain's final state (migration chain remains the object authority). |
| 15 | **Becomes derived/mirror/legacy** | The stale N24-era header/claims and the N25-N27 comment trail become legacy. |
| 16 | **Reversible** | Yes — git-controlled file change. |
| 17 | **Owner approval before N32** | **YES — P1** (authorization for the implementation plan; the file edit itself happens in Phase G). |

---

# Decision Priority

**P0 — must resolve before any N32 design**

| ID | Decision | Why P0 |
|---|---|---|
| D-1 | Functional-profile authority | Fixes the target profile store → decides whether Phase A adds a guard and Phase C/D touch login resolution. |
| D-2 | User→template single store | Fixes the target user→template shape → decides Phase A guard, Phase F junction disposition, and the whole §5 target model. |
| D-6 | Module catalog authority + `module_catalog_mirror` | Fixes the "one technical authority for Applications" rule that the default set requires → decides whether N32 touches the mirror at all. |

**P1 — must resolve before writer/read-path switching**

| ID | Decision | Why P1 |
|---|---|---|
| D-3 | Single-template at the Application edge | Writer contract change in Phase C. |
| D-4 | Job On write surface scope ★ | Determines whether Phase C wires endpoints or only proves the repository layer. |
| D-5 | Job On dual audit | Writer change (dual-emit) in Phase C; depends on D-1/D-2 not being violated. |
| D-10 | Peso readings immutability | Writer-path guard + Phase A DDL. |
| D-12 | `contra_costura` nullability | Phase A DDL + validator change required before Pegamentos writers switch. |
| D-13 | Audit co-transactionality | Phase C writer migration across modules. |
| D-14 | Armazém 1:1-per-position | Phase A DDL + concurrency change. |
| D-16 | Consolidated baseline refresh | Approval gates the full N32+ implementation plan (executed in Phase G). |

**P2 — may be deferred until legacy removal**

| ID | Decision | Why P2 |
|---|---|---|
| D-7 | `job_on_field_option` disposition ★ | Dormant, zero risk; only matters at Phase F. |
| D-8 | `tampao_planos` disposition ★ | Dormant/future-owned; only matters at Phase F (or feature wiring). |
| D-9 | `peso_comparacao_anterior` disposition | Dead mirror; only the Phase F drop needs approval. |
| D-11 | Dormant columns (`modules_override`, `image_asset_id`) | Removal is Phase F; no N32-design dependency (their presence does not affect Phase A-E shape). |
| D-15 | RLS policy naming | Cosmetic; resolve in Phase A but no design dependency. |

---

# Proposed Default Decision Set

The smallest coherent set of recommendations that yields the target invariants. If no separate owner direction is given, these are the defaults to design N32+ against:

1. **D-1 = Option A** — `access_template_profiles` is the functional-profile authority; `internal_users.profile_title` becomes a derived mirror and is removed in Phase F (reader switch via template).
2. **D-2 = Option A** — `internal_users.template_id` is the canonical direct FK for the user's single template; the junction becomes append-only assignment history **only if** assignment-change audit is required, otherwise it is removed; a DB consistency guard enforces equality during the transition. **One template per user is enforced at the DB (NOT NULL FK + guard) and Application edges (D-3).**
3. **D-3 = Option A** — reject `templateIds.Count != 1` at the `AdminUserService` edge with a typed error.
4. **D-6 = Option A** — in-code `CanonicalModuleCatalog` is the single technical authority for Applications/module catalog; `module_catalog_mirror` is a one-way display read-model (synchronizer sole writer), never consulted for authorization; legacy `capabilities[]` inert and stripped. No code+DB dual writable authority.
5. **D-5 = Option A** — Job On dual-emits in one UoW: `job_on_audit_event` = domain history authority; `audit_events` = derived global projection (single writer, parity guard).
6. **D-9 = Option A** — `peso_comparacao_anterior` REMOVE_LATER (live query is the authority).
7. **D-11 = Option A** — `modules_override` and `image_asset_id` REMOVE_LATER (after projection + gate cleanup).
8. **D-7 = A (KEEP dormant)** and **D-8 = C (KEEP dormant)** — `job_on_field_option` and `tampao_planos` stay as documented dormant/future-owned structures; wiring or removal is a later product decision (P2).
9. **D-10 = A, D-12 = A, D-13 = A, D-14 = A, D-15 = A, D-16 = A** — readings immutability guard, nullable `contra_costura` + domain rule, co-transactional audit, hard 1:1-per-position Armazém invariant, unified RLS naming, consolidated baseline refresh in Phase G.
10. **D-4 = B (defer Job On write endpoints)** with real-PG lifecycle tests, unless the product commits the write surface (★).

**Resulting invariants (the target the defaults produce):**
- **One authority per business fact** — profile (template), user→template (`template_id`), template modules (`access_templates.modules`), previous-approved Peso (live query), images (`article_reference_images`), audit (domain streams + derived global projection), catalog (code).
- **One template per user** — `template_id` NOT NULL FK + Application/DB single-assignment enforcement; no accumulation, no hybrid.
- **One profile per template** — `access_template_profiles` PK + CHECK; profile_title removed.
- **Template-owned module access** — modules selected by template only; capabilities derived from profile; no per-user override, no stored capability authority.
- **Applications/module catalog has one technical authority** — the compiled `CanonicalModuleCatalog`; the DB defines/grants nothing; the mirror is read-only display.
- **No unnecessary collapse of legitimate normalized domain tables** — Job On revision graph, Tampões configuration/value/machine/balance/movement/planning split, Armazém position/fact/current triad, Boquilhas lote/trace/fact layers, repair registry/capability/default split and internal/external distinction are all KEEP per SCHEMA-RAT-01 §3; the only removals are mirrors, dormant columns, and decision-gated dormant surfaces.

---

## Closing verification (task requirements)

- **Output file created:** `reports/schema_rationalization_owner_decisions.md` (this file). No other file was modified.
- **Source read:** only `reports/schema_rationalization_target_architecture.md` (SCHEMA-RAT-01).
- **Decisions extracted:** all **16** owner decisions from SCHEMA-RAT-01 §11 (D-1…D-16), each with the 17 required fields; no alternatives invented beyond what SCHEMA-RAT-01 genuinely posed (Option C present only for D-1, D-2, D-5, D-6, D-8).
- **Explicit comparisons delivered where required:** USER→TEMPLATE (D-2, direct FK vs junction with one-user uniqueness — junction not preferred merely for being normalized); TEMPLATE→PROFILE (D-1 — removal of `profile_title` as persisted authority evaluated); MODULE CATALOG (D-6 — Applications vs in-code catalog vs `module_catalog_mirror` resolved to the compiled catalog as the one technical authority, no code+DB dual writable authority); JOB ON AUDIT (D-5 — domain history vs global audit projection; dual emission is fan-out, not duplicate authority, with the four avoidance rules).
- **Concrete verdicts for the six requested structures:** `peso_comparacao_anterior` = REMOVE_LATER; `job_on_field_option` = KEEP dormant (OWNER_PRODUCT_DECISION, default A); `tampao_planos` = KEEP dormant (OWNER_PRODUCT_DECISION, default C); `modules_override` = REMOVE_LATER; `image_asset_id` = REMOVE_LATER; legacy `capabilities[]`/`module_catalog_mirror` fields = inert legacy / derived read-model (see D-6) — all with evidence from SCHEMA-RAT-01.
- **Priority & default set delivered:** `# Decision Priority` (P0: D-1, D-2, D-6; P1: D-3…D-5, D-10, D-12…D-14, D-16; P2: D-7…D-9, D-11, D-15) and `# Proposed Default Decision Set` (10 defaults → the 6 target invariants).
- **Change confirmation:** no source, migration, SQL, or database object was modified; no DML/DDL; no commit or push. **STOP** — no implementation performed.