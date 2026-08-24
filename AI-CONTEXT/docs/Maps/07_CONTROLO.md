# BA DMO — Controlo Technical Map

MAP ID: MAP-07
Status: COMPLETE

## Navigation Index

- 1. Scope
- 2. Layer Summary
- 3. Domain Objects
- 4. Application Objects
- 5. Application Contracts / Ports
- 6. Authorization / Catalog Objects
- 7. Infrastructure Objects
- 8. Database Objects
- 9. Migration Touchpoints
- 10. Web / Routes
- 11. Static Assets
- 12. Tests
- 13. Test Doubles / Helpers
- 14. Controlo References to Job On
- 15. Direct Controlo References
- 16. External Technical References
- 17. Target-to-Layer Index
- 18. Sources Verified

## 1. Scope

Module navigation map for Controlo-specific technical objects across Domain, Application, Infrastructure, Database, Web, static assets and Tests.

- Domain: `src\BA.Dmo.Domain\Modules\Controlo\` — `ControloFolha*` types (`ControloFolha`, `ControloFolhaContext/Component`, `ControloFolhaItem`, `ControloFolhaState/Decision/codec`, `ControloSheetModuleCatalog`, `ControloUnit`).
- Application: `src\BA.Dmo.Application\Modules\Controlo\` — `ControloSheetService`, `ControloSheetAuthorizationGate`, request/DTO records, `IControloSheetRepository`, `IControloProductionContextLookup`.
- Infrastructure: `src\BA.Dmo.Infrastructure\Access\` — `DapperControloSheetRepository`, `DapperControloProductionContextLookup`.
- Database: `database\migrations\N23_controlo_folha.sql` — `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events` + trigger; RLS/grants in `N25_remediation.sql`.
- Web: `src\BA.Dmo.Web\Pages\Controlo\` (Razor page + code-behind), `/api/controlo/*` endpoints + DI in `Program.cs`.
- Static assets: `wwwroot\scripts\controlo.js`, `wwwroot\styles\modules\controlo-layout.css`.
- Tests: `tests\BA.Dmo.UnitTests\Modules\Controlo\`.

Shared infrastructure (`IDbConnectionFactory`, `IRepairUnitOfWorkFactory`, `IClock`, accessor interfaces, `ba_dmo_guard_append_only`) is referenced only where Controlo consumes it, not remapped. Design/SOT not used as evidence; content grounded in current `src\`, `database\`, `tests\` source.

## 2. Layer Summary

| Layer | Main Controlo Objects | Locations |
|---|---|---|
| Domain | ControloFolha, ControloFolhaContext, ControloFolhaItem, ControloFolhaState, ControloFolhaStateCodec, ControloSheetModuleCatalog, ControloUnit | `src\BA.Dmo.Domain\Modules\Controlo\` |
| Application | ControloSheetService, ControloSheetServiceRequests (records), ControloSheetAuthorizationGate, IControloSheetRepository, IControloProductionContextLookup | `src\BA.Dmo.Application\Modules\Controlo\` |
| Authorization/Catalog | ControloSheetModuleCatalog, CanonicalModuleCatalog Controlo entry + controlo.* capabilities, ModulePolicies.Controlo, CapabilityPolicies.Controlo* | `src\BA.Dmo.Domain\Modules\Controlo\ControloSheetModuleCatalog.cs`, `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| Infrastructure | DapperControloSheetRepository, DapperControloProductionContextLookup | `src\BA.Dmo.Infrastructure\Access\` |
| Database | controlo_sheets, controlo_sheet_items, controlo_sheet_events, trg_controlo_sheet_events_append_only | `database\migrations\N23_controlo_folha.sql`, `database\consolidated_clean_install.sql` |
| Migrations | N23 (create), N25 (RLS/policy/grants) | `database\migrations\N23_controlo_folha.sql`, `database\migrations\N25_remediation.sql` |
| Web | Pages\Controlo\Index (Razor page), Program.cs controlo endpoints + DI | `src\BA.Dmo.Web\Pages\Controlo\`, `src\BA.Dmo.Web\Program.cs` |
| Static Assets | `wwwroot\scripts\controlo.js`, `wwwroot\styles\modules\controlo-layout.css` | `src\BA.Dmo.Web\wwwroot\` |
| Tests | ControloFolhaTests, ControloSheetServiceTests, ControloTestSupport | `tests\BA.Dmo.UnitTests\Modules\Controlo\` |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | YES | `src\BA.Dmo.Domain\Modules\Controlo\` |
| Application | YES | `src\BA.Dmo.Application\Modules\Controlo\`; `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\DapperControloSheetRepository.cs`, `DapperControloProductionContextLookup.cs` |
| Web | YES | `src\BA.Dmo.Web\Pages\Controlo\`; `src\BA.Dmo.Web\Program.cs`; `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| Database | YES | `database\migrations\N23_controlo_folha.sql`, `N25_remediation.sql` |
| Tests | YES | `tests\BA.Dmo.UnitTests\Modules\Controlo\` |

This is technical navigation only; it does not explain workflow.

## 3. Domain Objects

Location: `src\BA.Dmo.Domain\Modules\Controlo\`

### ControloFolha (aggregate root) — `ControloFolha.cs`
- Identifiers: `ControloSheetId` (Guid pk, default `Guid.NewGuid()`); pins `JobOnId`, `JobOnRevisionId` (Guid).
- Fields: `ProductionCode`, `Reference`, `MachineCode`, `DisplayId` (format `Controlo_<PROD>_<REF>_<MAQUINA>`), `State` (`ControloFolhaState`), `Items: IReadOnlyList<ControloFolhaItem>`, `Events: IReadOnlyList<ControloFolhaEvent>`, actors/timestamps (`CreatedBy`, `CreatedAtUtc`, `SubmittedBy/AtUtc/Note`, `DecidedBy/AtUtc/Note`, `Decision`, `UpdatedAtUtc`).
- Computed: `HasBeenSubmitted`, `HasBeenDecided`.
- Public static factory: `Create(ControloFolhaProductionContext, string actorId, DateTimeOffset now) -> Result<ControloFolha, DomainError>`. Validation error codes: `CONTROLO_CONTEXT_REQUIRED`, `CONTROLO_ACTOR_REQUIRED`.
- Mutations: `ApplyItemControls(IEnumerable<ControloFolhaItemControlEdit>, now)` (unknown item id → ignored); `Submit(actorId, note, now) -> Result<ControloUnit, DomainError>` (codes `CONTROLO_DECIDED`); `Reopen(actorId, now)` (code `CONTROLO_ALREADY_DRAFT`); `Decide(ControloFolhaDecision, actorId, note, now)` (code `CONTROLO_NOT_SUBMITTED`).
- Eventing: `RecordEvent(ControloFolhaEvent)`, internal `AppendEvent`, `SetEvents`, `SetItems`; internal `BuildDisplayId(context)`.
- Records: `ControloFolhaItemControlEdit(Guid ItemId, string? Result, string? Observation, string? McaliperLink)`; `ControloFolhaEvent(Guid ControloSheetEventId, Guid ControloSheetId, string EventType, string? ActorId, DateTimeOffset OccurredAtUtc, string? BeforeSummary, string? AfterSummary, string? Note)`.

### ControloFolhaContext (immutable production context) — `ControloFolhaContext.cs`
- `ControloFolhaProductionContext(Guid JobOnId, Guid JobOnRevisionId, string ProductionCode, string Reference, string MachineCode, IReadOnlyList<ControloFolhaComponent> Components)`.
- `ControloFolhaComponent(string Family, Guid? SourceToolId, Guid? SourceLotId, string? ReferenceSnapshot, string? LotSnapshot, string? TechnicalNameSnapshot)`.

### ControloFolhaItem — `ControloFolhaItem.cs`
- Identifier `ControloSheetItemId`; FK `ControloSheetId`.
- Snapshot fields: `Family` (MP_CM/MF/BQ), `SourceToolId`, `SourceLotId`, `ReferenceSnapshot`, `LotSnapshot`, `TechnicalNameSnapshot`.
- Control fields: `Result` (OK/NOK), `Observation`, `McaliperLink` (persisted as typed, no integration).
- Methods: `ApplyControl(result, observation, mcaliperLink)`; static `SnapshotFromComponent(sheetId, family, sourceToolId, sourceLotId, referenceSnapshot, lotSnapshot, technicalNameSnapshot)`; private `NormalizeResult` (uppercases, returns only `OK`/`NOK`, else null).

### ControloFolhaState — `ControloFolhaState.cs`
- Enum `ControloFolhaState`: `Rascunho`, `Submetido`, `Aprovado`, `Rejeitado`.
- Enum `ControloFolhaDecision`: `Aprovado`, `Rejeitado`.
- Codec `ControloFolhaStateCodec` (static): `ToStorage(ControloFolhaState)`, `FromStorage(string)` (text: `rascunho/submetido/aprovado/rejeitado`), `ToStorage(ControloFolhaDecision)`, `FromStorageDecision(string)`.

### ControloSheetModuleCatalog (module constants) — `ControloSheetModuleCatalog.cs`
- `AreaId = "controlo"`; capabilities `ViewCapabilityId = "controlo.view"`, `EditCapabilityId = "controlo.edit"`, `SubmitCapabilityId = "controlo.submit"`, `ReviewCapabilityId = "controlo.review"`.
- `Statuses = ["rascunho","submetido","aprovado","rejeitado"]`; `ComponentFamilies = ["MP_CM","MF","BQ"]`.

### ControloUnit — `ControloUnit.cs`
- `readonly record struct ControloUnit` with `static ControloUnit Value`.

Domain files: 6.

## 4. Application Objects

Location: `src\BA.Dmo.Application\Modules\Controlo\`

### ControloSheetService — `ControloSheetService.cs`
Constructor dependencies: `IControloSheetRepository`, `IControloProductionContextLookup`, `IRepairUnitOfWorkFactory`, `ControloSheetAuthorizationGate`, `IClock`.
Public methods (each gates a capability via `_gate.RequireCapability(...)`):
- `CreateAsync(CreateControloSheetRequest, ct)` → edit; inserts sheet + `"criar"` event in one UoW. Error `CONTROLO_SAVE_FAILED`.
- `GetDetailAsync(Guid sheetId, ct)` → view; returns `ControloSheetDto`; error `CONTROLO_NOT_FOUND`.
- `GetForProductionAsync(Guid jobOnId, ct)` → view; create-or-load for the production.
- `GetForProductionByContextAsync(string productionCode, string? machineCode, ct)` → view; resolves Job On internally by production/machine.
- `UpdateItemsAsync(UpdateControloSheetItemsRequest, ct)` → edit; records `"editar"` event.
- `SubmitAsync(SubmitControloSheetRequest, ct)` → submit; records `"submeter"` event.
- `ReopenAsync(ReopenControloSheetRequest, ct)` → edit; records `"reeabrir"` event.
- `DecideAsync(DecideControloSheetRequest, ct)` → review; records `"decidir"` event.
- `ListSheetsAsync(from, to, machineCode, jobOnId, status, ct)` → view.
- Private `PersistEditAsync`, `MapToDto`, `SerializeSummary` (JSON snapshot for event before/after).

### ControloSheetRequests — `ControloSheetRequests.cs`
Commands (records): `CreateControloSheetRequest(Guid JobOnId)`, `UpdateControloSheetItemsRequest(Guid SheetId, IReadOnlyList<ControloFolhaItemControlEdit> Edits)`, `SubmitControloSheetRequest(Guid SheetId, string? Note)`, `ReopenControloSheetRequest(Guid SheetId)`, `DecideControloSheetRequest(Guid SheetId, ControloFolhaDecision Decision, string? Note)`.
DTOs (records): `ControloSheetDto`, `ControloSheetItemDto`, `ControloSheetEventDto`.

### ControloSheetAuthorizationGate — `ControloSheetAuthorizationGate.cs`
Constructor: `ICurrentUserAccessor`, `IPersistenceAuthorshipAccessor`.
- `const string SurfaceModuleId = "peso"`; `RequireCapability` checks `user.HasModule("peso")`.
- `RequireSurface()` → `RequireCapability(null)`.
- `RequireCapability(string? capabilityId) -> Result<ControloSheetExecutor, DomainError>`: resolves identity, checks `user.HasModule("peso")`, optional `user.HasCapability(capabilityId)`, resolves canonical `actor_id` via authorship. Error codes `CONTROLO_FORBIDDEN`, `CONTROLO_CAPABILITY_<ID>_FORBIDDEN`.
- Record `ControloSheetExecutor(string ActorId, string DisplayName)`.

Application Controlo files: 5.

## 5. Application Contracts / Ports

### IControloSheetRepository — `IControloSheetRepository.cs`
- `InsertAsync(IDbUnitOfWork, ControloFolha, ct) -> Task<Guid>` (transactional with items).
- `GetByIdAsync(Guid, ct) -> Task<ControloFolha?>`.
- `GetForProductionAsync(Guid jobOnId, Guid? jobOnRevisionId, ct) -> Task<ControloFolha?>` (latest).
- `ListByProductionAsync(Guid jobOnId, ct) -> Task<IReadOnlyList<ControloFolha>>`.
- `ListAsync(DateTimeOffset? from, to, string? machineCode, Guid? jobOnId, string? status, ct) -> Task<IReadOnlyList<ControloFolha>>`.
- `UpdateAsync(IDbUnitOfWork, ControloFolha, IReadOnlyList<ControloFolhaItem> currentItems, ct)`.
- `InsertEventAsync(IDbUnitOfWork, ControloFolhaEvent, ct)` (append-only).
- Implemented by: `DapperControloSheetRepository`.

### IControloProductionContextLookup — `IControloProductionContextLookup.cs`
- `ResolveAsync(Guid jobOnId, ct) -> Task<Result<ControloFolhaProductionContext, DomainError>>` (current revision).
- `ResolveByProductionAsync(string productionCode, string? machineCode, ct) -> Task<Result<ControloFolhaProductionContext, DomainError>>`.
- Implemented by: `DapperControloProductionContextLookup`.

Shared persistence contract consumed (not Controlo-specific, not remapped): `IDbUnitOfWork` (`IRepairUnitOfWorkFactory.BeginAsync`).

## 6. Authorization / Catalog Objects

| Identifier | Value | Source | Role |
|---|---|---|---|
| Controlo area `ModuleKind` | `FunctionalArea` | `src\BA.Dmo.Domain\Shared\Access\ModuleKind.cs` | ModuleKind value for the Controlo catalog entry |
| Controlo area id | `controlo` | `CanonicalModuleCatalog.ControloAreaId` | Module catalog id; route `/controlo`; order 20 |
| Controlo capabilities | `controlo.view`, `controlo.edit`, `controlo.submit`, `controlo.review` | `CanonicalModuleCatalog.ControloView/Edit/Submit/ReviewCapabilityId` | Declared capabilities of the Controlo area |
| Area children | `[peso, pegamentos]` | `CanonicalModuleCatalog.AreaChildren[ControloAreaId]` | Canonical child module ids |
| Module policy | `ModulePolicies.Controlo = "BaDmo.Module.controlo"` | `ModuleAuthorizationHandler.cs` line 55 | Route module policy (page/route entry) |
| Capability policies | `CapabilityPolicies.ControloView/Edit/Submit/Review` (`BaDmo.Capability.controlo.*`) | `ModuleAuthorizationHandler.cs` lines 81–84 | Route-level capability policies |
| Policy registration | one `ModulePolicies.Prefix + moduleId` and one `CapabilityPolicies.Prefix + capabilityId` per canonical catalog entry | `Program.cs` lines 112–125 | Composition root builds all module/capability policies from `CanonicalModuleCatalog` |
| Sheet surface module | `SurfaceModuleId = "peso"` | `ControloSheetAuthorizationGate.cs` | Module id checked by `ControloSheetAuthorizationGate` via `user.HasModule("peso")` |

Note: `CanonicalPageCatalog` (Application Shared/Access) contains no `/controlo` page entry; the `/controlo` page route is served by the Razor Page `Pages\Controlo\Index` gated by `ModulePolicies.Peso`.

## 7. Infrastructure Objects

Location: `src\BA.Dmo.Infrastructure\Access\`

### DapperControloSheetRepository : IControloSheetRepository — `DapperControloSheetRepository.cs`
- Constructor: `IDbConnectionFactory`.
- Methods: `InsertAsync` (INSERT `controlo_sheets` then items via `InsertItemsAsync`), `GetByIdAsync`, `GetForProductionAsync`, `ListByProductionAsync`, `ListAsync`, `UpdateAsync` (UPDATE header + clear then UPDATE item control facts `result/observation/mcaliper_link`), `InsertEventAsync` (INSERT `controlo_sheet_events`).
- Private: `InsertItemsAsync`, `LoadItemsAndEventsAsync`, `MapHeader` (hydrates `ControloFolha` from row using `ControloFolhaStateCodec`), `DisposeAsync`.
- Embedded SQL tables: `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events`.
- State mapping: domain enum ↔ stored text via `ControloFolhaStateCodec`.

### DapperControloProductionContextLookup : IControloProductionContextLookup — `DapperControloProductionContextLookup.cs`
- Constructor: `IDbConnectionFactory`, `IJobOnRepository`.
- Methods: `ResolveAsync` (via `_jobOnRepository.GetByIdAsync`), `ResolveByProductionAsync` (via `_jobOnRepository.GetByProductionCodeAsync` + machine match; error `CONTROLO_MACHINE_MISMATCH`), private `ResolveJobOnAsync`.
- Reads Job On read model only: `job_on_revision` snapshots + `job_on_component` rows filtered to families `MP_CM`, `MF`, `BQ`.
- Errors: `CONTROLO_JOBON_NOT_FOUND`, `CONTROLO_NO_REVISION`, `CONTROLO_REVISION_MISSING`, `CONTROLO_CONTEXT_INCOMPLETE`.
- JSON helpers: `ExtractString` (JSON doc property), `ExtractReference`.

Infrastructure Controlo files: 2.

## 8. Database Objects

Source: `database\migrations\N23_controlo_folha.sql` (mirrored in `database\consolidated_clean_install.sql`).

| Object | Kind | Main technical role | PK | Important FKs | Notes |
|---|---|---|---|---|---|
| `controlo_sheets` | Table | One production control summary sheet | `controlo_sheet_id uuid` | `job_on_id → job_on`, `job_on_revision_id → job_on_revision`, `created_by/submitted_by/decided_by → internal_users` | CHECK `ck_controlo_sheets_status` (`rascunho/submetido/aprovado/rejeitado`); CHECK `ck_controlo_sheets_decision` (decision triad consistent); `display_id` document id; indexes `ix_controlo_sheets_job_on/revision/production/status` |
| `controlo_sheet_items` | Table | Per-component/tool snapshot + control result | `controlo_sheet_item_id uuid` | `controlo_sheet_id → controlo_sheets ON DELETE CASCADE`, `source_tool_id → tool_references`, `source_lot_id → tool_lotes` | CHECK `ck_controlo_sheet_items_result` (NULL/OK/NOK); indexes `ix_controlo_sheet_items_sheet/family` |
| `controlo_sheet_events` | Table | Append-only audit of create/edit/submit/reopen/decide | `controlo_sheet_event_id uuid` | `controlo_sheet_id → controlo_sheets ON DELETE CASCADE`, `actor_id → internal_users` | CHECK `ck_controlo_sheet_events_type` (`criar/editar/submeter/reeabrir/decidir`); `before_summary/after_summary jsonb`; trigger below |
| `trg_controlo_sheet_events_append_only` | Trigger | Blocks UPDATE/DELETE on `controlo_sheet_events` | — | — | `BEFORE UPDATE OR DELETE ... EXECUTE FUNCTION ba_dmo_guard_append_only()` (shared function defined in `N01_identity.sql`) |

Database Controlo objects: 3 tables + 1 trigger = 4.

## 9. Migration Touchpoints

| Migration | Controlo Object(s) | Technical Change |
|---|---|---|
| `database\migrations\N23_controlo_folha.sql` | `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events`, trigger | `CREATE TABLE IF NOT EXISTS` ×3; CHECK constraints ×3; indexes ×7; `DROP TRIGGER IF EXISTS` + `CREATE TRIGGER trg_controlo_sheet_events_append_only` using `ba_dmo_guard_append_only` |
| `database\migrations\N25_remediation.sql` | `controlo_sheets`, `controlo_sheet_items`, `controlo_sheet_events` | Adds to late-table list: `ALTER TABLE ... ENABLE ROW LEVEL SECURITY`; `CREATE POLICY ba_dmo_app_access ... FOR ALL TO ba_dmo_app`; `REVOKE ALL ... FROM anon/authenticated`; `GRANT SELECT, INSERT, UPDATE, DELETE ... TO ba_dmo_app` |

`database\consolidated_clean_install.sql` carries the same Controlo DDL (tables at lines 1119–1185) and the same RLS/policy/grants stanzas (lines 1537–1578) as a fresh-build artifact. Migration family Controlo touchpoints: 2 files.

## 10. Web / Routes

### Razor Page — `src\BA.Dmo.Web\Pages\Controlo\Index.cshtml` + `Index.cshtml.cs`
- Route: `@page "/controlo"`; authorized by `ModulePolicies.Peso` attribute (line 3).
- `IndexModel : PageModel` — constructor `ICurrentUserAccessor`; `OnGet(Guid? jobOn)` sets `ProjectedJobOnId`, `CanEdit` (`controlo.edit`), `CanSubmit` (`controlo.submit`), `CanReview` (`controlo.review`).
- Markup surfaces: active-production card, workspace tabs (`resumo/peso/comparacao/pegamentos/historico`), Resumo items/history tables, Peso/Comparação/Pegamentos embed sections. Loads `~/scripts/controlo.js` with `defer`.

### API Endpoints — `src\BA.Dmo.Web\Program.cs` (lines 1135–1216)
All invoke `ControloSheetService` and `RequireAuthorization(ModulePolicies.Peso)`; operations additionally gated server-side by the `controlo.*` capability via `ControloSheetAuthorizationGate`.

| Route | Technical Entry Point | Authorization | File |
|---|---|---|---|
| `GET /api/controlo/production` | `ControloSheetService.GetForProductionAsync(jobOnId)` | ModulePolicies.Peso | Program.cs:1135 |
| `GET /api/controlo/list` | `ControloSheetService.ListSheetsAsync(from,to,machine,jobOn,status)` | ModulePolicies.Peso | Program.cs:1144 |
| `GET /api/controlo/by-production` | `ControloSheetService.GetForProductionByContextAsync(production,machine)` | ModulePolicies.Peso | Program.cs:1155 |
| `POST /api/controlo` | `ControloSheetService.CreateAsync(request)` | ModulePolicies.Peso | Program.cs:1164 |
| `GET /api/controlo/{sheetId:guid}` | `ControloSheetService.GetDetailAsync(sheetId)` | ModulePolicies.Peso | Program.cs:1173 |
| `POST /api/controlo/{sheetId:guid}/items` | `ControloSheetService.UpdateItemsAsync(...)` | ModulePolicies.Peso | Program.cs:1182 |
| `POST /api/controlo/{sheetId:guid}/submit` | `ControloSheetService.SubmitAsync(...)` | ModulePolicies.Peso | Program.cs:1192 |
| `POST /api/controlo/{sheetId:guid}/reopen` | `ControloSheetService.ReopenAsync(...)` | ModulePolicies.Peso | Program.cs:1201 |
| `POST /api/controlo/{sheetId:guid}/decide` | `ControloSheetService.DecideAsync(...)` | ModulePolicies.Peso | Program.cs:1210 |

### Composition Root (DI) — `src\BA.Dmo.Web\Program.cs` (lines 241–244)
- `IControloProductionContextLookup → DapperControloProductionContextLookup`
- `IControloSheetRepository → DapperControloSheetRepository`
- `ControloSheetAuthorizationGate` (Scoped)
- `ControloSheetService` (Scoped)

Web dedicated page files: 2 (`Pages\Controlo\Index.cshtml`, `Pages\Controlo\Index.cshtml.cs`); static asset files: 2 (see Static Assets). Shared Web files carrying Controlo wiring (`Program.cs`, `ModuleAuthorizationHandler.cs`) are not dedicated Controlo files and are not counted.

## 11. Static Assets

### `src\BA.Dmo.Web\wwwroot\scripts\controlo.js`
IIFE (`BA DMO — Controlo unified production workspace wiring`). Local functions / selectors:
- Selectors: `#toast`, `#canEdit/#canSubmit/#canReview`, `#btnCarregarJobOn`, `#activeCard`, `#cardDisplay/#cardSub`, `.controlo-tabs .tab`, `.controlo-tab-view`, `#controloEmpty/#controloLoading/#controloError`, `#resumoNeedsContext`, `#controloContext`, `#controloItemsCard`, `#controloHistoryCard`, `#controloItems tbody`, `#controloHistory tbody`, `#controloActions`, `#btnOpenPeso`, `#btnOpenComparacao`, `#btnOpenPegamentos`, `#controloHistoryTable tbody`, `#historyEmpty`.
- Functions: `esc`, `showToast`, `api`, `jsonPost`, `stateLabel`, `showEmpty`, `activateCard`, `detachCard`, `clearCard`, `refreshTabStates`, `loadResumo`, `renderItems`, `renderHistory`, `renderActions`, `collectEdits`, `handleAction`, `loadHistoryList`, `fmtDT`, `activateFromJobOnId`, `init`.
- API endpoints called: `GET /api/jobon/current`, `GET /api/controlo/production?jobOnId=`, `POST /api/controlo/{id}/items`, `POST /api/controlo/{id}/submit`, `POST /api/controlo/{id}/reopen`, `POST /api/controlo/{id}/decide`, `GET /api/controlo/list`.
- DOM data-writes: item rows built from `sheet.items`; history from `sheet.events`; capability flags gate action buttons.

### `src\BA.Dmo.Web\wwwroot\styles\modules\controlo-layout.css`
Layout/composition only (uses shared `--dmo-*` tokens): `.controlo-page`, `.controlo-active-card(-body)`, `.controlo-card-title/line/sub/actions`, `.controlo-hint`, `.controlo-tabs`, `.controlo-tab-view(.active)`, `.controlo-context-grid`, `.controlo-items-card`, `.controlo-history-card`, `.controlo-tab-note`, `.controlo-tab-embed`; responsive blocks at `@media (max-width: 720px)` and `520px`.

Static assets: MAPPED.

## 12. Tests

Location: `tests\BA.Dmo.UnitTests\Modules\Controlo\`

### ControloFolhaTests — unit (domain invariants) — `ControloFolhaTests.cs`
Target: `ControloFolha`/`ControloFolhaItem`/`ControloFolhaState`. Method groups: creation snapshots components + pins revision; Create without context fails (`CONTROLO_CONTEXT_REQUIRED`); submit→decide approved flow; decide without submission fails (`CONTROLO_NOT_SUBMITTED`); submit-after-decision rejected + reopen allows resubmit; edit after submission allowed + result applied; `RecordEvent` append-only.

### ControloSheetServiceTests — unit (application use cases) — `ControloSheetServiceTests.cs`
Target: `ControloSheetService` + gate + contracts. Method groups: get-for-production creates from context; update items applies control + leaves state + `"editar"` event; submit→review decide flow; reopen after submission returns to draft (`"reeabrir"`); create without edit capability forbidden; get-for-production-by-context resolves without re-selection; list works in free mode.

Controlo test classes: 2.

## 13. Test Doubles / Helpers

`tests\BA.Dmo.UnitTests\Modules\Controlo\ControloTestSupport.cs`:
- `ControloFixedClock : IClock` (fixed UTC for deterministic tests).
- `ControloFakeAuthorship : IPersistenceAuthorshipAccessor`.
- `FakeControloUowFactory : IRepairUnitOfWorkFactory`; `FakeControloUow : IDbUnitOfWork` (in-memory no-ops).
- `ControloCurrentUser : ICurrentUserAccessor` — factory helpers `View()`, `Edit()`, `Review()`, `WithoutSurface()`.
- `FakeControloSheetRepository : IControloSheetRepository` (in-memory; `FailWrite` simulates write failure).
- `FakeControloProductionContextLookup : IControloProductionContextLookup` (in-memory `ByJobOn`); static `Context(...)` builder.
- `ControloTestBuilder.Build(...)` — builds `ControloSheetService` over the fakes.

## 14. Controlo References to Job On

| Controlo Object | Job On Reference | Reference Type |
|---|---|---|
| `ControloFolha` | `JobOnId`, `JobOnRevisionId` | Domain identifier columns/FKs |
| `ControloFolhaProductionContext` | `JobOnId`, `JobOnRevisionId` | Domain record fields |
| `IControloSheetRepository` | `GetForProductionAsync(jobOnId, jobOnRevisionId)` | Application port parameter |
| `DapperControloSheetRepository` | embeds `job_on_id`, `job_on_revision_id` columns in `controlo_sheets` SQL read/write | Infrastructure SQL |
| `DapperControloProductionContextLookup` | `IJobOnRepository`; reads `job_on_revision`, `job_on_component` | Infrastructure constructor + read model SQL |
| `controlo_sheets` | `job_on_id → job_on`, `job_on_revision_id → job_on_revision` | DB FKs (N23) |
| `CreateControloSheetRequest` | `Guid JobOnId` | Application command field |

## 15. Direct Controlo References

Mechanical source-visible relationships:

```
ControloSheetService
  → IControloSheetRepository
  → IControloProductionContextLookup
  → ControloSheetAuthorizationGate
  → IRepairUnitOfWorkFactory (shared)
  → IClock (shared)

IControloSheetRepository
  → DapperControloSheetRepository

IControloProductionContextLookup
  → DapperControloProductionContextLookup

DapperControloProductionContextLookup
  → IJobOnRepository (Job On)

ControloSheetAuthorizationGate
  → ICurrentUserAccessor
  → IPersistenceAuthorshipAccessor

DapperControloSheetRepository
  → controlo_sheets, controlo_sheet_items, controlo_sheet_events

ControloSheetService / Gate
  → ControloSheetModuleCatalog (capability ids)

Index.cshtml
  → controlo.js

controlo.js
  → /api/controlo/*

/api/controlo/*
  → ControloSheetService

Program.cs
  → DI registration IControloSheetRepository : DapperControloSheetRepository
  → DI registration IControloProductionContextLookup : DapperControloProductionContextLookup
  → DI registration ControloSheetService, ControloSheetAuthorizationGate
  → /api/controlo/* endpoints (ModulePolicies.Peso)
  → ModulePolicies.Controlo / CapabilityPolicies.Controlo* (ModuleAuthorizationHandler)
```

## 16. External Technical References

| Controlo Object | External Technical Reference | Reference Type |
|---|---|---|
| `ControloFolha` | `Domain.Shared.Kernel.Result<T,E>` / `DomainError` | Domain shared kernel usage |
| `ControloFolha.Item.SourceToolId` | `tool_references` (FK on `controlo_sheet_items.source_tool_id`) | DB FK |
| `ControloFolha.Item.SourceLotId` | `tool_lotes` (FK on `controlo_sheet_items.source_lot_id`) | DB FK |
| `controlo_sheets` | `internal_users` (created_by/submitted_by/decided_by FKs) | DB FK |
| `controlo_sheet_events.trigger` | `ba_dmo_guard_append_only` (shared function, N01) | DB function reference |
| `DapperControloSheetRepository.UpdateAsync` | clears/sets item columns; uses shared `IDbConnectionFactory`/`Db` (Dapper) | Infrastructure dependency |
| `ControloSheetAuthorizationGate` | checks `user.HasModule("peso")`; `CanonicalModuleCatalog.AreaChildren[controlo] = [peso, pegamentos]` | Authorization cross-module |
| `ControloSheetService.GetForProductionByContextAsync` | `IControloProductionContextLookup.ResolveByProductionAsync` reads Job On production code/machine | Application port cross-module (Job On) |
| Web tabs | `/peso`, `/pegamentos` navigation targets (`controlo.js` `window.location.href`) | Static asset route references |

## 17. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| ControloFolha | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloFolha.cs` |
| ControloFolhaContext | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloFolhaContext.cs` |
| ControloFolhaItem | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloFolhaItem.cs` |
| ControloFolhaState / Decision / Codec | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloFolhaState.cs` |
| ControloSheetModuleCatalog | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloSheetModuleCatalog.cs` |
| ControloUnit | Domain | `src\BA.Dmo.Domain\Modules\Controlo\ControloUnit.cs` |
| ControloSheetService | Application | `src\BA.Dmo.Application\Modules\Controlo\ControloSheetService.cs` |
| ControloSheetRequests (commands/DTOs) | Application | `src\BA.Dmo.Application\Modules\Controlo\ControloSheetRequests.cs` |
| ControloSheetAuthorizationGate | Application | `src\BA.Dmo.Application\Modules\Controlo\ControloSheetAuthorizationGate.cs` |
| IControloSheetRepository | Application | `src\BA.Dmo.Application\Modules\Controlo\IControloSheetRepository.cs` |
| IControloProductionContextLookup | Application | `src\BA.Dmo.Application\Modules\Controlo\IControloProductionContextLookup.cs` |
| CanonicalModuleCatalog controlo entry | Application (shared catalog) | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| DapperControloSheetRepository | Infrastructure | `src\BA.Dmo.Infrastructure\Access\DapperControloSheetRepository.cs` |
| DapperControloProductionContextLookup | Infrastructure | `src\BA.Dmo.Infrastructure\Access\DapperControloProductionContextLookup.cs` |
| controlo_sheets / _items / _events / trigger | Database | `database\migrations\N23_controlo_folha.sql` |
| Pages\Controlo\Index | Web | `src\BA.Dmo.Web\Pages\Controlo\` |
| /api/controlo/* endpoints + DI | Web | `src\BA.Dmo.Web\Program.cs` |
| ModulePolicies/CapabilityPolicies Controlo | Web (authorization) | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| controlo.js | Static asset | `src\BA.Dmo.Web\wwwroot\scripts\controlo.js` |
| controlo-layout.css | Static asset | `src\BA.Dmo.Web\wwwroot\styles\modules\controlo-layout.css` |
| ControloFolhaTests / ControloSheetServiceTests / ControloTestSupport | Tests | `tests\BA.Dmo.UnitTests\Modules\Controlo\` |

## 18. Sources Verified

- `maps\00_INDEX.md` (mapping contract/registry).
- `src\BA.Dmo.Domain\Modules\Controlo\` (6 files).
- `src\BA.Dmo.Application\Modules\Controlo\` (5 files).
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `ModuleKind.cs`.
- `src\BA.Dmo.Infrastructure\Access\DapperControloSheetRepository.cs`, `DapperControloProductionContextLookup.cs`.
- `src\BA.Dmo.Web\Program.cs` (DI 241–244; endpoints 1135–1216; policies 112–125).
- `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`.
- `src\BA.Dmo.Web\Pages\Controlo\Index.cshtml`, `Index.cshtml.cs`.
- `src\BA.Dmo.Web\wwwroot\scripts\controlo.js`, `wwwroot\styles\modules\controlo-layout.css`.
- `database\migrations\N23_controlo_folha.sql`, `N25_remediation.sql`, `N01_identity.sql` (function reference).
- `database\consolidated_clean_install.sql`.
- `tests\BA.Dmo.UnitTests\Modules\Controlo\ControloFolhaTests.cs`, `ControloSheetServiceTests.cs`, `ControloTestSupport.cs`.

## Counts

- Domain Controlo files: 6
- Application Controlo files: 5
- Infrastructure Controlo files: 2
- Web dedicated page files: 2 (`Pages\Controlo\Index.cshtml`, `Pages\Controlo\Index.cshtml.cs`)
- Static asset files: 2 (`wwwroot\scripts\controlo.js`, `wwwroot\styles\modules\controlo-layout.css`)
- Shared Web files with Controlo wiring (not counted as dedicated): `Program.cs`, `ModuleAuthorizationHandler.cs`
- Controlo DB objects: 4 (3 tables + 1 trigger)
- Controlo migration touchpoints: 2 migration files (N23, N25)
- Controlo test classes: 2 (plus 1 support/helper file)