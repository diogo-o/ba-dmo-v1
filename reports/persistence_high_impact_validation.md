# Persistence High-Impact Findings — Validation

> READ-ONLY VALIDATION. Nothing was modified: no code, migrations, tests, or database objects; no SQL applied.
> Scope: validates **only** the seven high-impact findings from `reports/persistence_cross_reference_audit.md` against the current source at HEAD `847830824262bc42aadfc9a34d9c4d9bdc058baf` (branch `main`). Legacy/orphan candidates were deliberately not re-investigated.
> Method: targeted re-reading of the exact code paths, SQL text, and migration files cited below; grep verification of writers/readers; no live database was available in this session (no `BA_DMO_DB_CONNECTION_STRING`/`DATABASE_URL`, no local PostgreSQL), so runtime behavior at the PostgreSQL level is reasoned from the SQL/constraints and marked where test-proof is missing.

## Validation summary

| ID | Finding | Verdict |
|---|---|---|
| VAL-01 | Job On lifecycle constraint vs `UpdateLifecycleStateAsync` | **CONFIRMED BUG** (latent — no Web route currently invokes the transition) |
| VAL-02 | N31 single-template constraint vs remaining multi-template write paths | **CONFIRMED ARCHITECTURAL DEBT** |
| VAL-03 | Functional profile write/read authority | **CONFIRMED ARCHITECTURAL DEBT** |
| VAL-04 | JSONB binding inconsistencies | **CONFIRMED BUG** (non-JSON payloads into jsonb) + **CONFIRMED ARCHITECTURAL DEBT** (cast-less convention) |
| VAL-05 | Repair multi-step transaction atomicity | **CONFIRMED BUG** (audit step throws on non-JSON payload → partial state) + **CONFIRMED ARCHITECTURAL DEBT** (non-atomic multi-command flows) |
| VAL-06 | Armazém re-occupation concurrency race | **CONFIRMED BUG** (TOCTOU; concurrency-dependent) |
| VAL-07 | N28/N29/N30 `BEGIN;…COMMIT;` under the migration runner | **CONFIRMED ARCHITECTURAL DEBT** (runner transaction guarantee silently voided; unproven by tests) |

---

## VAL-01 — Job On lifecycle constraint vs `UpdateLifecycleStateAsync`

**VERDICT: CONFIRMED BUG** (latent — the write path is live in the Application layer but no Web/CLI route currently reaches it).

**EXACT CODE PATHS**
- `src\BA.Dmo.Infrastructure\Access\DapperJobOnRepository.cs:183-196` — `UpdateLifecycleStateAsync(Guid id, JobOnLifecycleState newState, string actorId, CancellationToken)`: `UPDATE job_on SET status = @NewState WHERE job_on_id = @Id;` — **status only**; `closed_at_utc`/`canceled_at_utc` are never in the SET list and have **no writer anywhere in `src\`** (grep across the whole tree: the columns appear only in `SELECT` projections at `DapperJobOnRepository.cs:75-78,122-125,159-162` and in the domain hydrator `JobOn.cs:47-50`; zero INSERT columns, zero UPDATE SET).
- `src\BA.Dmo.Application\Modules\JobOn\JobOnService.cs:232-260` — `TransitionAsync(TransitionJobOnRequest)`: `jobOn.TransitionTo(request.NewState)` (`:247`) → `_repository.UpdateLifecycleStateAsync(jobOn.Id, jobOn.LifecycleState, …)` (`:255-256`) → `InsertAuditEventAsync(..., jobOn.LifecycleState.ToString(), ...)` (`:257-258`). This is the **only** caller of `UpdateLifecycleStateAsync`.
- `src\BA.Dmo.Domain\Modules\JobOn\JobOn.cs:148-169` — `TransitionTo` permits `Fechado` (from `EmFabrico`) and `Cancelado` (from `Rascunho`/`Planeado`) but mutates only the in-memory state.
- `src\BA.Dmo.Domain\Modules\JobOn\JobOn.cs:171-191` — `Close(DateTime)` / `Cancel(string, string, DateTime)` **do set** `ClosedAtUtc`/`CancelledAtUtc`/`CancelledBy`/`CancelReason`, but **no source file calls them** (grep: definitions only).

**EXACT METHODS**
`JobOnService.TransitionAsync`, `DapperJobOnRepository.UpdateLifecycleStateAsync`, `JobOn.TransitionTo`, `JobOn.Close`, `JobOn.Cancel` (unused), `JobOnLifecycleStateCodec.ToStorage`.

**EXACT MIGRATIONS / CONSTRAINTS**
- `database\migrations\N25_remediation.sql:70-82` — `ck_job_on_lifecycle_consistent` on `job_on`: `(status = 'fechado') = (closed_at_utc IS NOT NULL) AND (status = 'cancelado') = (canceled_at_utc IS NOT NULL)`.
- `database\migrations\N05_jobon.sql:28-31` — `job_on.closed_at_utc`, `job_on.canceled_at_utc`, `job_on.canceled_by`, `job_on.cancel_reason` (nullable columns, no application writer).
- `database\migrations\N25_remediation.sql:60-62` — partial unique `uq_job_on_identity (production_code, machine_code) WHERE canceled_at_utc IS NULL`, whose exemption predicate depends on `canceled_at_utc` being written (it never is).

**RUNTIME FAILURE SCENARIO**
Any invocation of `JobOnService.TransitionAsync` with `request.NewState = Fechado | Cancelado` executes `UPDATE job_on SET status = 'fechado'` (or `'cancelado'`) while `closed_at_utc`/`canceled_at_utc` stay NULL → PostgreSQL CHECK violation **23514** → `PostgresException` propagates unwrapped → request fails (500). The `rascunho → planeado → em_fabrico` transitions pass (the CHECK only constrains the two terminal states). Consequently: (a) the terminal lifecycle states are unreachable at the database level; (b) `uq_job_on_identity`'s cancellation exemption can never hold, so a canceled job's `(production_code, machine_code)` identity can never be re-issued; (c) if the transition DID succeed, `InsertAuditEventAsync` would additionally fail on the non-JSON payload (see VAL-04). Today the failure is latent because no Web route invokes `TransitionAsync` (`Program.cs` registers Job On endpoints only for image attach/replace/remove, current context, and document generation; `jobon.js` save is a presentational close).

**MINIMUM SAFE FIX SCOPE**
Extend the transition write to persist the timestamps in the **same statement/transaction**: `UpdateLifecycleStateAsync` (or a dedicated repository method) must set `closed_at_utc = now()` when the new state is `fechado`, `canceled_at_utc`/`canceled_by`/`cancel_reason` when `cancelado`, and NULL otherwise — deriving the values from the domain's already-existing `JobOn.Close()`/`JobOn.Cancel()` methods; the audit insert must be moved into the same unit of work (see VAL-04/VAL-05). No schema change is required if the CHECK is kept; relaxing the CHECK instead is an owner decision, not the minimal fix.

**TESTS THAT SHOULD PROVE THE FIX**
- DB-level (extend `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Integrity\RemediationGuardTests.cs`, `BA_DMO_TEST_DATABASE` pattern): `UPDATE job_on SET status='fechado'` with `closed_at_utc` NULL → expect SQLSTATE 23514; with `closed_at_utc` set → success; same pair for `cancelado`.
- Repository-level (ADO.NET-double pattern of `Access\DapperAdminRepositoryProjectionTests.cs`): assert `UpdateLifecycleStateAsync(id, Fechado, …)` issues a single parameterized `UPDATE` that sets both `status` and `closed_at_utc`.
- Service-level: `JobOnServiceTests.TransitionAsync(Fechado)` → repository receives the terminal state plus timestamp; transition + audit commit/rollback together.

---

## VAL-02 — N31 single-template constraint vs remaining multi-template write paths

**VERDICT: CONFIRMED ARCHITECTURAL DEBT** (a live multi-template write surface contradicts the N31 single-assignment constraint; it is not triggerable through the current Web UI, but any future caller — or a regression in the page handlers — produces an unhandled 23505).

**EXACT CODE PATHS**
- `src\BA.Dmo.Infrastructure\Access\DapperAdminRepository.cs:244-303` — `ReplaceUserAccessTemplatesAsync(string actorId, IReadOnlyList<string> templateIds, …)`: guarded `UPDATE internal_users SET template_id = ids[0]` → `DELETE FROM internal_user_access_templates WHERE actor_id = @ActorId` → `INSERT … SELECT @ActorId, template_id FROM unnest(@TemplateIds::text[])` — inserts **every** submitted id (`:277-288`).
- `src\BA.Dmo.Application\Modules\Admin\AdminUserService.cs:200-241` — `CreateUserAsync`: accepts a list `request.AssignedTemplateIds`; writes `templateIds[0]` into `internal_users.template_id` (`:228`) and, **if `templateIds.Length > 1`**, calls `ReplaceUserAccessTemplatesAsync(actorId, templateIds, …)` (`:233-241`).
- `src\BA.Dmo.Application\Modules\Admin\AdminUserService.cs:320-378` — `ChangeTemplatesAsync` (plural) and its single wrapper `ChangeTemplateAsync` (`:313-318`): normalizes the full list and forwards it to `ReplaceUserAccessTemplatesAsync` (`:350-352`).
- `src\BA.Dmo.Application\Modules\Admin\AdminUserService.cs:429-476` — `SaveUserAsync`: `UPDATE` user, then `ChangeTemplatesAsync` with the full normalized list when the assigned set changed (`:448-463`).
- Web entry points currently reduce to **exactly one** template: `Pages\Admin\Users\Create.cshtml.cs:55-92` (single `templateId` select; legacy plural `templateIds` reduced via `FirstOrDefault` at `:62-64`; passes `[selectedTemplateId]` at `:92`), `Pages\Admin\Users\Edit.cshtml.cs:45-71` (single select, `[templateId]`), `Create.cshtml`/`Edit.cshtml` (single `<select id="templateId">`).

**EXACT METHODS**
`AdminUserService.CreateUserAsync`, `AdminUserService.ChangeTemplatesAsync`/`ChangeTemplateAsync`, `AdminUserService.SaveUserAsync`, `DapperAdminRepository.ReplaceUserAccessTemplatesAsync`, `DapperAdminRepository.CreateInternalUserAsync` (single-id path), `IdentityResolutionService.ResolveAsync`.

**EXACT MIGRATIONS / CONSTRAINTS**
- `database\migrations\N27_access_convergence.sql:8-17` — junction `internal_user_access_templates` with PK `(actor_id, template_id)` (one-or-more model).
- `database\migrations\N31_template_profiles_single_assignment.sql:75-88` — deletes hybrid assignments, re-inserts the single effective `template_id` row, then `CREATE UNIQUE INDEX ux_internal_user_access_templates_actor ON internal_user_access_templates (actor_id)` — final model: **one row per user**.

**RUNTIME FAILURE SCENARIO**
Calling `CreateUserAsync`/`ChangeTemplatesAsync`/`SaveUserAsync` with two client-supplied template ids → inside the same UoW: `internal_users.template_id` updated, junction DELETE, then the **second** junction INSERT violates `ux_internal_user_access_templates_actor` → `PostgresException` 23505, **unhandled** (no catch in the service besides `ConcurrencyConflictException` and `LockoutViolationException`) → 500, with the whole write rolled back. Independently, if a user ever carries more than one junction row, `IdentityResolutionService.ResolveAsync` (`IdentityResolutionService.cs:103-113`) fails closed with `ACCESS_TEMPLATE_AMBIGUOUS`, making the user un-loginable. Current Web forms never send more than one id, so the defect is currently unreachable from the shipped UI — architectural debt rather than a live 500.

**MINIMUM SAFE FIX SCOPE**
Enforce exactly-one at the Application contract edge: reject `templateIds.Count != 1` in `CreateUserAsync`/`ChangeTemplatesAsync`/`SaveUserAsync` with an explicit domain error (e.g. `ADMIN_SINGLE_TEMPLATE`), and/or collapse `ReplaceUserAccessTemplatesAsync` to single-template semantics; remove the plural `templateIds` reduction in `Create.cshtml.cs`. The N31 DB constraint stays as the backstop.

**TESTS THAT SHOULD PROVE THE FIX**
- `AdminUserServiceTests`: `ChangeTemplatesAsync`/`CreateUserAsync` with two ids → typed domain error (not a 500).
- DB-level (`RemediationGuardTests` pattern): inserting a second junction row for one actor → SQLSTATE 23505.
- Repository-level (ADO.NET-double): `ReplaceUserAccessTemplatesAsync` issues at most one junction INSERT per actor.
- `IdentityResolutionServiceTests`: single-template input resolves; two-row input already covered by `MultipleAssignedTemplates_FailsClosedAsAmbiguous`.

---

## VAL-03 — Functional profile write/read authority

**VERDICT: CONFIRMED ARCHITECTURAL DEBT** (three writers of the mirrored fact, direct SQL from the Web layer, runtime resolution never reads the N31 table).

**EXACT CODE PATHS**
- Writers of `internal_users.profile_title`: (1) `AdminUserService.UpdateUserAsync` (`AdminUserService.cs:257-311`) and `CreateUserAsync` (`:195-255`) → `DapperAdminRepository.UpdateUserAsync` SQL `UPDATE internal_users SET display_name = …, profile_title = @ProfileTitle, …` (`DapperAdminRepository.cs:199-233`; user-level profile field no longer rendered by the Web UI — `Edit.cshtml:80` “é definido pelo template”); (2) `Pages\Admin\TemplateProfileStore.cs:98-136` — `UpsertAsync` raw SQL: `INSERT INTO access_template_profiles … ON CONFLICT (template_id) DO UPDATE …` **plus a companion** `UPDATE internal_users SET profile_title = @FunctionalProfile … WHERE template_id = @TemplateId AND profile_title IS DISTINCT FROM …` (`:111-123`); (3) `DapperInternalUserRepository.CreateBootstrapAdminAsync` (hardcoded `'Admin'`, `DapperInternalUserRepository.cs:56-65`); migration-time: `N31:92-97`.
- Writers of `access_template_profiles`: N31 trigger function `ba_dmo_ensure_access_template_profile` on `access_templates` INSERT (`N31:24-46`), N31 backfill (`N31:51-70`), and `TemplateProfileStore.UpsertAsync`.
- Orchestration in the Web layer: `Pages\Admin\Templates\Edit.cshtml.cs:77-168` — `OnPostAsync` calls `AdminTemplateService.CreateAsync/UpdateAsync` (Application layer, own connection) **then** `TemplateProfileStore.UpsertAsync` (raw SQL, separate connection) — **not co-transactional** (`:137-147`, `:158-167`).
- Readers: `DapperInternalUserRepository.FindByAuthUserIdSql` selects `u.profile_title` (`DapperInternalUserRepository.cs:16-32`) → `IdentityResolutionService.ResolveAsync` parses it (`IdentityResolutionService.cs:115-135`) → `AccessResolver.Resolve([template], profile)`. **`access_template_profiles` is read only by `TemplateProfileStore.GetAsync/ListAsync` (Web layer); no Application/Infrastructure code reads it at identity resolution time.**

**EXACT METHODS**
`TemplateProfileStore.UpsertAsync/GetAsync/ListAsync`, `AdminTemplateService.CreateAsync/UpdateAsync`, `AdminUserService.UpdateUserAsync/CreateUserAsync`, `DapperAdminRepository.UpdateUserAsync`, `DapperInternalUserRepository.FindByAuthUserIdAsync`, `IdentityResolutionService.ResolveAsync`, `ba_dmo_ensure_access_template_profile` (N31).

**EXACT MIGRATIONS / CONSTRAINTS**
- `database\migrations\N31_template_profiles_single_assignment.sql:13-19` — `access_template_profiles` with `ck_access_template_profiles_functional_profile` (`functional_profile IN ('Admin','Operador / Controlador','Responsável')`).
- `database\migrations\N31_template_profiles_single_assignment.sql:24-46` (trigger), `:51-70` (backfill), `:90-97` (profile_title sync — **migration-time only**).
- `database\migrations\N27_access_convergence.sql:113-120` — `internal_users.profile_title SET NOT NULL` + `ck_internal_users_functional_profile`.

**RUNTIME FAILURE SCENARIO**
(1) Template edit via `/admin/templates` splits one logical write across two connections: the template row commits in `AdminTemplateService.UpdateAsync`; a crash between it and `TemplateProfileStore.UpsertAsync` leaves the profile stale (new templates are healed by the N31 trigger; **template updates are not**). (2) `internal_users.profile_title` still accepts a user-level value through `AdminUserService.UpdateUserAsync`/`CreateUserAsync` (contract remains; UI no longer sends it) while `TemplateProfileStore.UpsertAsync` overwrites `profile_title` for every user of a template — the two write paths can fight if both are ever invoked. (3) Because runtime resolution reads `profile_title` and never `access_template_profiles`, any divergence between the two tables changes effective access without the N31 table being consulted — the “template-owned profile” is therefore not the operative source of truth at login.

**MINIMUM SAFE FIX SCOPE**
Designate a single owner. Either (a) move profile persistence into the Application/`IAdminRepository` boundary: one repository method that upserts `access_template_profiles` **and** syncs `internal_users.profile_title` in a single UoW, and make `IdentityResolutionService` (or the identity Dapper query) resolve the profile from `access_template_profiles`; remove the direct-SQL store from the Web layer; or (b) keep `profile_title` as the source and retire the N31 table/trigger. Drop the user-level `profile_title` write path from the service contract if the template owns the profile.

**TESTS THAT SHOULD PROVE THE FIX**
- DB-level: inserting an `access_templates` row auto-creates its `access_template_profiles` row (N31 trigger) — `RemediationGuardTests` extension.
- Repository/Application-level: updating a template's profile updates `access_template_profiles` + synced `internal_users.profile_title` atomically (assert both via an ADO.NET-double or DB test).
- `IdentityResolutionServiceTests`: resolution reflects the template-owned profile; a divergence between the two tables is either impossible (atomic write) or resolved deterministically.

---

## VAL-04 — JSONB binding inconsistencies

**VERDICT: CONFIRMED BUG** (non-JSON strings bound into jsonb columns in the Job On audit paths and in module audit inserts) **+ CONFIRMED ARCHITECTURAL DEBT** (cast-less jsonb binding convention across Peso/Pegamentos, functionally OK today only while content parses as JSON and Npgsql's server-side parameter-type inference resolves the column type; nothing proves it against real PostgreSQL).

**EXACT CODE PATHS (bug sites — non-JSON content into jsonb)**
- `src\BA.Dmo.Infrastructure\Access\DapperJobOnRepository.cs:667-669` — `DuplicateAtomicallyAsync`: `InsertAuditEventCoreAsync(…, afterSnapshot: sourceJobOnId.ToString(), …)` — a bare GUID.
- `src\BA.Dmo.Application\Modules\JobOn\JobOnService.cs:257-258` — `TransitionAsync`: `afterSnapshot = jobOn.LifecycleState.ToString()` — enum name such as `Fechado`.
- Both feed `InsertAuditEventAsync`/`InsertAuditEventCoreAsync` (`DapperJobOnRepository.cs:485-509`, `:887-911`): `INSERT INTO job_on_audit_event (…, before_snapshot, after_snapshot, …)` with string parameters and **no cast** — `before_snapshot`/`after_snapshot` are `jsonb` (`N05:199-202`).
- Module audit inserts with non-JSON payloads into `audit_events.before_summary`/`after_summary` (jsonb, N01): `DapperRepairRepository.cs:386-401` fed by `ReparacaoExternaService.cs:89-90` (`$"{type}|{repairerId}"`), `:136-137` (`$"{Reference}|{Lot}|{Number}"`), `:163-164` (Guid), `:195-196`, `:255-256`, `:318-319`, `:413` (display name), `:442`, `:456`, `:482`; `DapperArmazemRepository.cs:433-455` fed by `ArmazemService` (`$"{Reference}|{Lot}"`, etc.).
- **Contrast (correct convention):** `DapperAdminRepository.cs:570-596` (`@BeforeSummary::jsonb, @AfterSummary::jsonb`), `DapperArticleReferenceImageRepository.cs:162-170` (`CAST(@BeforeSnapshot AS jsonb)`), `DapperInternalUserRepository.cs:43,51` (`@AdminGrantPattern::jsonb`).

**EXACT CODE PATHS (debt sites — cast-less but JSON-valid content)**
- `DapperPesoRepository.cs:206-239` (`CreateControlAsync`: `measurements_snapshot`, `approval_log`, `previous_control`, `comparison_decisions`, `cm_snapshot` bound as strings; content from `BuildMeasurementsSnapshot` = `JsonSerializer.Serialize` at `:655-683`, `ApprovalLogJson ?? "[]"`).
- `DapperPegamentoRepository.cs:55-96` (`CreateAsync`: `reference_snapshot`/`cm_snapshot`/`bq_snapshot`/`mf_snapshot` via `SerializeJson`/`SerializeToolSnapshot`; valid JSON).
- `DapperBoquilhasRepository.cs:206-250` (trace `reopen_history`/`deleted_movements` are written via `jsonb_build_object`/`||` operators — valid; column bind of serialized strings where used).

**EXACT MIGRATIONS / CONSTRAINTS**
- `N01_identity.sql:114-115` — `audit_events.before_summary`/`after_summary jsonb` (no shape CHECK).
- `N05_jobon.sql:199-202` — `job_on_audit_event.before_snapshot`/`after_snapshot jsonb`.
- `N06_peso.sql:89-93` and `N07_pegamentos.sql:33-38,58-66` — snapshot/measurement jsonb columns for the debt sites.

**RUNTIME FAILURE SCENARIO**
For the bug sites: Npgsql resolves the untyped string parameter to `jsonb` when the VALUES context targets a jsonb column (server-side parameter-type inference); Postgres then **parses the payload as JSON** — `"Fechado"`, `"3c5d…"` (bare GUID), `"CM|4b9e…"`, `"REF|LOT|NUM"` are not valid JSON → **22P02 invalid input syntax for type json**; in call paths where the parameter instead stays `text`, the assignment fails with **42804 column is of type jsonb but expression is of type text**. Either mechanism fails the INSERT. Example: `POST /api/reparacao-externa` (create exit) → exit + items committed → final `InsertAuditEventAsync(…, "CM|…", …)` throws → 500 with the exit persisted un-audited (see VAL-05). The exact Npgsql mechanism is not proven by any test (no test executes these SQL paths), but **both plausible mechanisms fail**, so the defect is confirmed; confidence in the failure, HIGH; precise SQLSTATE, 22P02 vs 42804 — pending a live-PG probe.
For the debt sites: content is valid JSON today, so these INSERTs currently succeed; the risk is convention fragility (a future non-JSON value, a comparison context without an assignment cast — e.g. `DapperTampaoRepository.cs:145` `WHERE values_json = @ValuesJson` — or an Npgsql change).

**MINIMUM SAFE FIX SCOPE**
(1) Serialize every audit payload with `JsonSerializer` (or pass NULL) at the call sites listed above and in `JobOnService.TransitionAsync`/`DapperJobOnRepository.DuplicateAtomicallyAsync`; (2) adopt one explicit binding convention — `::jsonb`/`CAST(… AS jsonb)` or `NpgsqlDbType.Jsonb` — at every jsonb parameter site (apply to the debt sites and the comparison site in `DapperTampaoRepository`); (3) add a guard/static test enumerating audit payload builders.

**TESTS THAT SHOULD PROVE THE FIX**
- DB-level (`RemediationGuardTests` pattern): `INSERT INTO audit_events (…, before_summary) VALUES (…, 'CM|abc')` → expect 22P02; with `'{"k":"v"}'` → success.
- ADO.NET-double (IssuedSql-capture pattern of `DapperAdminRepositoryProjectionTests`): the audit INSERTs of `DapperJobOnRepository`/`DapperRepairRepository`/`DapperArmazemRepository` carry a jsonb cast and JSON-parseable payloads.
- Service tests: `TransitionAsync(Fechado)` and `DuplicateAsync` assert the audit snapshot is serialized JSON before reaching the repository.

---

## VAL-05 — Repair multi-step transaction atomicity

**VERDICT: CONFIRMED BUG** (the final audit step of the repair flows throws on a non-JSON payload — VAL-04 — after the business rows already committed, producing partial state) **+ CONFIRMED ARCHITECTURAL DEBT** (multi-command flows with no transaction).

**EXACT CODE PATHS**
- `src\BA.Dmo.Application\Modules\ReparacaoExterna\ReparacaoExternaService.cs:62-92` — `CreateExitAsync`: `CreateExitAsync(exit)` (`:81`, own connection, autocommit) → per-item `AddItemCoreAsync` (`:83-87`, each item its own connection+commands) → `InsertAuditEventAsync(…, $"{type}|{repairerId}", …)` (`:89-90`). No `IRepairUnitOfWorkFactory` scope.
- `src\BA.Dmo.Application\Modules\ReparacaoExterna\ReparacaoExternaService.cs:104-139` — `AddItemCoreAsync`: domain checks → `AddItemAsync` (`:135`) → audit (`:136-137`) — separate connections.
- `src\BA.Dmo.Application\Modules\ReparacaoExterna\ReparacaoExternaService.cs:141-166` — `RemoveItemAsync`: `DeleteItemAsync` (`:162`) → audit (`:163-164`).
- `src\BA.Dmo.Application\Modules\ReparacaoExterna\ReparacaoExternaService.cs:410-456` — `CreateRepairerAsync`/`UpdateRepairerAsync`: `SetRepairerRepairTypesAsync` (`:412`,`:439`) → audit (`:413`,`:442`).
- `src\BA.Dmo.Infrastructure\Access\DapperRepairRepository.cs:354-370` — `SetRepairerRepairTypesAsync`: `DELETE FROM repairer_repair_types WHERE repairer_id = @RepairerId` then one `INSERT` per type — **one connection, no transaction** (`Open` + autocommit).
- `src\BA.Dmo.Infrastructure\Access\DapperRepairRepository.cs:386-401` — `InsertAuditEventAsync` binds `@Before`/`@After` strings into `audit_events.before_summary`/`after_summary` (jsonb) without cast (see VAL-04).
- Contrast: coordinated pickup/return writes DO use the caller-provided `IDbUnitOfWork` (`DapperRepairRepository.ConfirmItemPickedAsync/ConfirmItemReturnedAsync/UpdateExitStatusAsync/InsertRepairEventAsync`; `DapperArmazemRepairMovementRepository`), and `IRepairUnitOfWorkFactory` is registered (`Program.cs:231`) — the tooling exists but is not applied to the setup paths.

**EXACT METHODS**
`ReparacaoExternaService.CreateExitAsync`, `AddItemCoreAsync`, `RemoveItemAsync`, `CreateRepairerAsync`, `UpdateRepairerAsync`, `DapperRepairRepository.SetRepairerRepairTypesAsync`, `DapperRepairRepository.InsertAuditEventAsync`.

**EXACT MIGRATIONS / CONSTRAINTS**
- `N08_reparacoes.sql` — `repair_exits` (`:40-56`), `repair_exit_items` (`:64-83`), `repairers` (`:13-19`), `repairer_repair_types` (`N20:13-17`, PK `(repairer_id, repair_type)`).
- `N01_identity.sql:114-115` — `audit_events.before_summary`/`after_summary jsonb` (payload-shape risk; no CHECK helps).

**RUNTIME FAILURE SCENARIO**
(1) `POST /api/reparacao-externa` with 2 valid + 1 failing item (e.g. duplicate-in-open-exit, `ExistsItemInOpenExitAsync`) → exit + 2 items persisted, API returns an error — **partial list**; (2) all items valid → the final audit INSERT throws (VAL-04 payload) → **500 after the exit and items committed, un-audited**; (3) `UpdateRepairerAsync` with 3 supported types where the 2nd INSERT fails → the repairer keeps a partial capability set; (4) any audit-insert failure after `RemoveItemAsync`/`DisponibilizarExitAsync`/pickup/return leaves the mutation un-audited (audits are post-commit, separate connections).

**MINIMUM SAFE FIX SCOPE**
Wrap the setup flows in one `IRepairUnitOfWorkFactory`/`DapperUnitOfWork` scope so exit + items + audit commit/roll back together (`CreateExitAsync`, `AddItemCoreAsync`, `RemoveItemAsync`, repairer create/update); make `SetRepairerRepairTypesAsync` transactional (DELETE + inserts in one transaction); fix the audit payloads to valid JSON (VAL-04) so the co-transactional audit inserts stop throwing.

**TESTS THAT SHOULD PROVE THE FIX**
- Service-level (fake repository with a failure injected on the 2nd item / on the audit step): assert **no** `repair_exits` row remains after failure (rollback) or that the failure occurs before any commit.
- Repository-level (ADO.NET-double): `SetRepairerRepairTypesAsync` participates in one transaction (all statements share a transaction object).
- DB-level: `audit_events` payload probe from VAL-04.

---

## VAL-06 — Armazém re-occupation concurrency race

**VERDICT: CONFIRMED BUG** (TOCTOU: concurrent returns can create two active occupations of one position; same-lot double-return raises an unhandled 23505).

**EXACT CODE PATHS**
- `src\BA.Dmo.Infrastructure\Access\DapperArmazemRepairMovementRepository.cs:67-113` — `ConfirmReturnAsync(IDbUnitOfWork uow, Guid repairExitId, Guid toolLoteId, string positionCode, …)`: occupancy check is `SELECT warehouse_stock_id, tool_lote_id FROM warehouse_stock WHERE warehouse_location_id = @LocationId AND released_at_utc IS NULL ORDER BY occupied_since_utc ASC LIMIT 1` (`:77-83`) — **no `FOR UPDATE`**, no location-row lock; when `existing is null` it `INSERT`s a new active `warehouse_stock` row (`:92-107`).
- `src\BA.Dmo.Infrastructure\Access\DapperArmazemRepository.cs:174-233` — `RegisterEntradaAsync` contains the **explicit TOCTOU fix** this path lacks: `SELECT warehouse_location_id FROM warehouse_locations WHERE warehouse_location_id = @LocationId FOR UPDATE` (`:185-191`) + active-stock `SELECT … FOR UPDATE` (`:197-204`) inside `DapperUnitOfWork.RunAsync` (comment at `:179-184` documents the exact race being closed).
- Caller: `ReparacaoExternaService.ConfirmReturnAsync` (`ReparacaoExternaService.cs:302-320` area) opens a `DapperUnitOfWork` via `IRepairUnitOfWorkFactory` and calls the port — the transaction exists, only the locking is missing.

**EXACT METHODS**
`DapperArmazemRepairMovementRepository.ConfirmReturnAsync`, `DapperArmazemRepairMovementRepository.ConfirmPickupAsync` (safe: guarded `UPDATE … WHERE released_at_utc IS NULL RETURNING` at `:50-64`), `DapperArmazemRepository.RegisterEntradaAsync`, `ArmazemLocationOccupiedException`.

**EXACT MIGRATIONS / CONSTRAINTS**
- `N09_armazem.sql:27-39` — `uq_warehouse_stock_active_occupation` partial unique on `(warehouse_location_id, tool_lote_id) WHERE released_at_utc IS NULL`. It is **per (location, tool lot)**, so it cannot enforce 1:1 **per location** for two different lots.

**RUNTIME FAILURE SCENARIO**
Two concurrent `ConfirmReturnAsync` calls for **different tool lots onto the same empty position** both read `existing = null` → both INSERT active rows with the same `warehouse_location_id` and different `tool_lote_id` → the partial unique index does **not** fire → **two active occupations of one position** (GLM-ARM-04 “occupancy 1:1” violated; subsequent `ConsultarPorPosicao`/pickup order by `occupied_since_utc LIMIT 1` resolves arbitrarily). Two concurrent returns of the **same lot** to the empty position → second INSERT violates the partial unique index → unhandled `PostgresException` 23505 → 500 (the UoW rolls back, but the error surfaces raw).

**MINIMUM SAFE FIX SCOPE**
Mirror `RegisterEntradaAsync`: at the start of `ConfirmReturnAsync`'s occupancy check, lock the always-present location row (`SELECT warehouse_location_id FROM warehouse_locations WHERE warehouse_location_id = @LocationId FOR UPDATE` inside the existing caller-provided UoW) before the occupant SELECT+INSERT; optionally catch 23505 on the occupy INSERT and translate to the existing `ARMZ_REPAIR_POSITION_OCCUPIED` domain error. (Alternative backstop: a partial unique index on `(warehouse_location_id)` for active rows — a schema change, owner decision.)

**TESTS THAT SHOULD PROVE THE FIX**
- Concurrency integration test (two tasks, `Barrier`, same empty position, different lots) against the repo/port: assert exactly **one** active `warehouse_stock` row remains after both finish.
- DB-level: same-lot double insert → SQLSTATE 23505 (extends `RemediationGuardTests`).
- Unit: `ConfirmReturnAsync` acquires the location lock before the occupant check (assert the SQL includes `FOR UPDATE` via an ADO.NET double, mirroring the `RegisterEntradaAsync` comment).

---

## VAL-07 — N28/N29/N30 `BEGIN;…COMMIT;` behavior under the migration runner

**VERDICT: CONFIRMED ARCHITECTURAL DEBT** (the scripts' explicit transaction-control statements silently void the runner's documented whole-script single-transaction guarantee; the semantics are exercised by no test; consequences are latent rather than an observed failure today).

**EXACT CODE PATHS**
- `src\BA.Dmo.Infrastructure\Persistence\Migrations\NpgsqlMigrationScriptGateway.cs:71-90` — `ExecuteScriptAsync`: `await using var transaction = await Connection.BeginTransactionAsync(ct)` → `new NpgsqlCommand(wholeScript, Connection, transaction)` → `ExecuteNonQueryAsync` → `CommitAsync` (rollback on failure). The entire file is sent as **one command** (no splitting).
- `src\BA.Dmo.Infrastructure\Persistence\Migrations\MigrationRunner.cs:51-93` — per file: SHA-256 check → `ExecuteScriptAsync(wholeScript)` → record in `schema_migrations` only after success.
- Scripts containing `BEGIN;`/`COMMIT;` (verified: the **only** files in the family): `database\migrations\N28_reparacao_interna_cm_mf_only.sql:12,37`; `N29_jobon_reference_images.sql:11,157`; `N30_jobon_reference_image_updated_by_index.sql:4,9`.

**EXACT MIGRATIONS / CONSTRAINTS**
- N28: fail-closed `RAISE EXCEPTION` guard (`:14-24`), drop + `ADD CONSTRAINT … CHECK (tool_type IN ('CM','MF')) NOT VALID` + `VALIDATE` (`:26-35`).
- N29: two fail-closed guards (`:31-66`, `:69-104`), legacy-promotion `INSERT … ON CONFLICT DO NOTHING` (`:106-137`), RLS/policy/grants (`:139-155`).
- N30: single `CREATE INDEX` (`:6-8`).
- Gateway behavior reference: `NpgsqlMigrationScriptGateway.cs:75-77` comment documents “One transaction per migration: success commits, failure rolls back”.

**RUNTIME FAILURE SCENARIO**
The gateway begins a transaction; the whole script then runs inside it. PostgreSQL **ignores the inner `BEGIN;`** (warning “there is already a transaction in progress”) and the script's **`COMMIT;` commits the gateway's transaction**; all statements in N28–N30 precede the inner COMMIT, so each file is effectively atomic and applies fully; the gateway's trailing `transaction.CommitAsync()` then executes `COMMIT` with no transaction in progress — a warning-level no-op — and the migration is recorded as applied. Therefore **no failure is expected today**. The debt/risk surfaces as: (1) any future statement added **after** an inner `COMMIT` runs in autocommit and can never be rolled back by the gateway, silently breaking the runner's all-or-nothing contract; (2) the N29 fail-closed guards and the N28 `VALIDATE` run before the inner COMMIT, so failures there do roll back correctly today; (3) the whole mechanism (inner transaction-control statements + trailing gateway COMMIT) is **unproven** — no test executes these files or the real gateway (`MigrationRunnerTests` use `FakeMigrationGateway`; `RemediationGuardTests` covers N25-era behavior only; no real-PG migration execution test exists), so a server/protocol variance (e.g. Npgsql treating the transaction state differently) would surface only at deploy time.

**MINIMUM SAFE FIX SCOPE**
Remove `BEGIN;`/`COMMIT;` from N28/N29/N30 (the runner already provides the single transaction; the explicit statements add nothing but risk), keeping the file bodies identical otherwise; and/or harden the gateway/tests so the semantics of client-supplied transaction-control statements are explicit. Add a real-PostgreSQL migration test that applies N01–N31 (or at least N28–N30) through `MigrationRunner` + `NpgsqlMigrationScriptGateway` and asserts each file applies and is recorded exactly once.

**TESTS THAT SHOULD PROVE THE FIX**
- Real-PG test (extend the `BA_DMO_TEST_DATABASE` pattern): run `MigrationRunner` over the full family on an empty database → N28/N29/N30 applied, recorded once, schema converges (61 tables); re-run skips all.
- A regression probe: a script with statements after an inner `COMMIT` either is rejected up-front or rolls back atomically (decide and lock the semantics).
- Existing static guards (`MigrationDiscoveryTests.N28/N29/N30_*`) remain valid as content contracts.

---

## Validation metadata

- Findings validated: 7 (VAL-01…VAL-07); evidence re-read from current source at HEAD `8478308`; no live database available (claims about PostgreSQL runtime behavior are reasoned from SQL/constraints and explicitly flagged where unproven by tests).
- Scope honored: no legacy/orphan candidates investigated beyond the seven findings; nothing modified; no SQL executed; only `reports\persistence_high_impact_validation.md` created.
- Related findings in the audit report (cross-references, not re-validated here): PA-JOBON-01/-02, PA-DAP-01/-02/-07/-09/-10, PA-DS-02, PA-MC-02, PA-CB-02.