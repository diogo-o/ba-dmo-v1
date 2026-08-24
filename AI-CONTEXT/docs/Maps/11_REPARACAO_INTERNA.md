# BA DMO — Reparação Interna Technical Map

MAP ID: MAP-11
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
- 14. Direct Reparação Interna References
- 15. External Technical References
- 16. Target-to-Layer Index
- 17. Sources Verified
- Counts

## 1. Scope

Technical inventory and navigation of Reparação Interna-specific objects across Domain, Application, Infrastructure, Database, Web/static assets and Tests, grounded in current source. Cross-layer navigation only; no end-to-end flow, no business-rule interpretation. Design/SOT not used as evidence.

## 2. Layer Summary

| Layer | Main Reparação Interna Objects | Locations |
|---|---|---|
| Domain | `ReparacaoInternaModuleCatalog`, `InternalRepairRecord`, `InternalRepairToolType`, `InternalRepairContext`, `InternalRepairContextResolution`, `InternalRepairResolutionKind`, `InternalRepairRules`, `ReparacaoInternaProductionProjection` | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` |
| Application | `ReparacaoInternaService`, `ReparacaoInternaAuthorizationGate`, `ReparacaoInternaExecutor`, `IReparacaoInternaRepository`, `IJobOnActiveContextLookup`, requests/DTOs | `src\BA.Dmo.Application\Modules\ReparacaoInterna\` |
| Infrastructure | `DapperReparacaoInternaRepository`, `DapperJobOnActiveContextLookup` | `src\BA.Dmo.Infrastructure\Access\` |
| Database | `internal_repair_records` (RI-specific); shared `repair_events` (scope 'interna'/'externa') | `database\migrations\N08_reparacoes.sql`, `N22_reparacao_interna_context.sql`, `N12_rls.sql` |
| Web | `/reparacao-interna` Razor page; `/api/reparacao-interna/*` endpoints + `/api/boquilhas/production-context` | `src\BA.Dmo.Web\Pages\ReparacaoInterna\`, `src\BA.Dmo.Web\Program.cs` |
| Static assets | `reparacao-interna.js`, `reparacao-interna-layout.css` | `src\BA.Dmo.Web\wwwroot\scripts\`, `src\BA.Dmo.Web\wwwroot\styles\modules\` |
| Tests | `ReparacaoInternaDomainTests`, `ReparacaoInternaServiceTests`, `ReparacaoInternaProductionProjectionTests`, `ReparacaoInternaWebApiTests` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\`, `tests\BA.Dmo.IntegrationTests\ReparacaoInterna\` |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | YES | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` |
| Application | YES | `src\BA.Dmo.Application\Modules\ReparacaoInterna\` |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\DapperReparacaoInternaRepository.cs`, `DapperJobOnActiveContextLookup.cs` |
| Web | YES | `src\BA.Dmo.Web\Pages\ReparacaoInterna\`; `src\BA.Dmo.Web\Program.cs`; `Authorization\ModuleAuthorizationHandler.cs` |
| Database | YES | `database\migrations\N08_reparacoes.sql`, `N22_reparacao_interna_context.sql`, `N12_rls.sql` |
| Tests | YES | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\`, `tests\BA.Dmo.IntegrationTests\ReparacaoInterna\` |

This is technical navigation only; it does not explain workflow.

## 3. Domain Objects

All under `src\BA.Dmo.Domain\Modules\ReparacaoInterna\`.

| Type | Kind | Key members | Notes |
|---|---|---|---|
| `ReparacaoInternaModuleCatalog` | static catalog | `ModuleId = "reparacao_interna"`; `CorrigirCapabilityId = "reparacao_interna.corrigir"`; `Lines = { B1, B2, B3, C1, C2, C3 }` | `ReparacaoInternaModuleCatalog.cs` |
| `InternalRepairRecord` | aggregate root | `InternalRepairRecordId`, `Line`, `JobOnId?`, `JobOnRevisionId?`, `ProductionCode?`, `Reference?`, `LotId?`, `ToolType`, `IndividualNumber`, `OperatorId?`, `OccurredAtUtc`, `CorrectionOfId?`, `BeforeSnapshot?`, `CorrectionReason?`, `CreatedBy?`, `CreatedAtUtc`, `IsCorrection`; methods `Create`, `CreateCorrection`; private `IsValidForCorrection` | `InternalRepairRecord.cs`; maps `internal_repair_records`; error codes `REPINT_LINE_UNKNOWN`, `REPINT_NUMBER_REQUIRED`, `REPINT_OPERATOR_REQUIRED`, `REPINT_INVALID_TYPE`, `REPINT_CORRECTION_CHAIN`, `REPINT_CORRECTOR_REQUIRED`. `CreateCorrection(line,...,recalibrateContext)` with `recalibrateContext` fallback logic |
| `InternalRepairToolType` | enum | `CM`, `MF` | `InternalRepairToolType.cs`; no BQ member |
| `InternalRepairToolTypeCodec` | static codec | `ToStorage(CM)->"CM"`, `(MF)->"MF"`; `FromStorage("CM")/"MF"`; other values throw `InvalidOperationException` | `InternalRepairToolType.cs`; rejects any `'BQ'` stored value |
| `InternalRepairContext` | record | `JobOnId`, `JobOnRevisionId`, `Line`, `ProductionCode`, `Reference`, `MachineCode?`, `CmLotIds`, `MfLotIds`, `BqLotIds`, `ActivatedFromUtc?`, `ValidToUtc?` | `InternalRepairContext.cs`; carries BQ lot context IDs inside the record |
| `InternalRepairContextResolution` | record | `Kind`; `Context?`; `Candidates`; `None()`, `Single(ctx)`, `Ambiguous(candidates)` | `InternalRepairContext.cs` |
| `InternalRepairResolutionKind` | enum | `None`, `Single`, `Ambiguous` | `InternalRepairContext.cs` |
| `InternalRepairContextCandidate` | record | `JobOnId`, `JobOnRevisionId`, `Line`, `ProductionCode`, `Reference`, `MachineCode?`, `ValidFromUtc?`, `ValidToUtc?` | `InternalRepairContext.cs` |
| `InternalRepairRules` | static rules | `ContextMismatchInfoCode = "REPINT_CONTEXT_MISMATCH_INFO"`; `NoActiveContextInfoCode = "REPINT_NO_ACTIVE_CONTEXT_INFO"`; `EvalCollectibleWhen(kind)` → always `Success`; `NumberInContextLot(context, type, pieceLotId)` (CM→`CmLotIds`, MF→`MfLotIds`, else `null`) | `InternalRepairRules.cs` |
| `Unit` | readonly record struct | `Value` | `InternalRepairRules.cs`; local result unit |
| `ReparacaoInternaProductionProjection` | static projection | `FactoryLocalOffsetUtc = +01:00`; `ActivationUtc(plannedStartAt)` → local start date at 09:00 UTC; `SelectEffective(candidates, at)` → most-recent active start with `ActivationUtc <= at`, no end-date test, null when none | `ReparacaoInternaProductionProjection.cs`; consumes `JobOn` (`IsActive`, `PlannedStartAt`) |

### 3.1 Tool-type inventor (recordable set)

Literal source values that define which types are recordable/selectable/validated in Reparação Interna.

| Source | Literal allowed values | Location |
|---|---|---|
| `InternalRepairToolType` enum | `CM`, `MF` | `InternalRepairToolType.cs` |
| `InternalRepairToolTypeCodec.ToStorage` | `"CM"`, `"MF"` | `InternalRepairToolType.cs` |
| `InternalRepairRecord.Create` guard | rejects `toolType is not (CM or MF)` → `REPINT_INVALID_TYPE` | `InternalRepairRecord.cs` |
| `InternalRepairRecord.CreateCorrection` guard | rejects `toolType is not (CM or MF)` → `REPINT_INVALID_TYPE` | `InternalRepairRecord.cs` |
| `ReparacaoInternaService.RegistrarReparacoesAsync` | rejects `request.ToolType is not (CM or MF)` → `REPINT_INVALID_TYPE` | `ReparacaoInternaService.cs` |
| `ParseInternalToolType` (Web) | `"CM"`→CM, `"MF"`→MF, else `null` (BQ unmapped) | `src\BA.Dmo.Web\Program.cs` |
| `ck_internal_repair_records_type` (N08 original) | `('CM','MF')` | `database\migrations\N08_reparacoes.sql` |
| `ck_internal_repair_records_type` (N22 redefined) | `('CM','MF','BQ')` | `database\migrations\N22_reparacao_interna_context.sql` |
| Static CSHTML register buttons | `data-type="CM"`, `data-type="MF"`, `data-type="BQ"` | `src\BA.Dmo.Web\Pages\ReparacaoInterna\Index.cshtml` |
| Static CSHTML correction type options | `<option value="CM">`, `<option value="MF">`, `<option value="BQ">` | `src\BA.Dmo.Web\Pages\ReparacaoInterna\Index.cshtml` |

BQ literal facts (source-grounded, mechanical):
- `InternalRepairContext.BqLotIds` (Guid list) — a context record member; sourced from `job_on_component` family `'BQ'` (`source_lot_id`) in `DapperJobOnActiveContextLookup.ReadRevisionContextAsync`.
- `DapperJobOnActiveContextLookup` query reads families `IN ('MP_CM','MF','BQ')`.
- `ReparacaoInternaProductionProjection`/context summary has no BQ repair-type branch; `InternalRepairToolType` declares only `CM` and `MF`, while `InternalRepairContext.BqLotIds` carries BQ source-lot ids.
- The active-context reference is read as the full reference string (e.g. `5447T173`); the domain/service preserve the full reference verbatim via `Reference`.

## 4. Application Objects

All under `src\BA.Dmo.Application\Modules\ReparacaoInterna\`.

| Object | Kind | Public methods | Constructor deps |
|---|---|---|---|
| `ReparacaoInternaService` | service | `ListLineCardsAsync(ct)`, `ResolveLineContextAsync(line, ct)`, `RegistrarReparacoesAsync(request, ct)`, `RegisterReparacaoAsync(request, ct)` (back-compat), `ListHistoryAsync(filter, ct)`, `GetDetailAsync(recordId, ct)`, `CorrigirReparacaoAsync(request, ct)` | `IReparacaoInternaRepository`, `IJobOnActiveContextLookup`, `IFerramentasPieceLookup`, `IRepairUnitOfWorkFactory`, `ReparacaoInternaAuthorizationGate`, `IClock` |
| `ReparacaoInternaAuthorizationGate` | gate | `Require()` → `Result<ReparacaoInternaExecutor, DomainError>`; `RequireCorrigir(actorId)` → requires module + capability | `ICurrentUserAccessor`, `IPersistenceAuthorshipAccessor` |
| `ReparacaoInternaExecutor` | record | `ActorId`, `DisplayName` | — |

Private helpers of `ReparacaoInternaService`: `ResolveEffectiveLotIdAsync`, `BuildDetail`, `TryMapToFerramentas` (CM→`FerramentasToolType.CM`, MF→`FerramentasToolType.MF`), `Serialize` (JSON for audit). Audit action codes written: `reparacao_interna.registrar` (result `succeeded`), `reparacao_interna.corrigir` (result `corrected`). Registered error codes: `REPINT_FORBIDDEN`, `REPINT_CORRIGIR_FORBIDDEN`, `REPINT_NUMBER_REQUIRED`, `REPINT_INVALID_TYPE`, `REPINT_NOT_FOUND`, `REPINT_CORRECTION_CHAIN`, `REPINT_SAVE_FAILED`.

Requests/DTOs (`ReparacaoInternaRequests.cs`): requests `RegisterReparacaoRequest(Line, ToolType, Numbers, OverrideProduction?, OverrideReference?)`, `CorrigirReparacaoRequest(RecordId, Line, ToolType, IndividualNumber, JobOnId?, JobOnRevisionId?, ProductionCode?, Reference?, LotId?, Reason?)`; filter `InternalRepairFilter(From, To, Line, JobOnId, ToolType?, Number, OperatorId, OnlyCorrected)`; DTOs `InternalRepairLineCard(Line, Reference?, ProductionCode?, HasActiveContext)`, `InternalRepairContextDto(Kind, JobOnId?, JobOnRevisionId?, ProductionCode?, Reference?, MachineCode?, ValidFromUtc?, ValidToUtc?, Candidates)`, `InternalRepairCandidateDto(...)`, `InternalRepairHistoryRow(RecordId, DataHora, Line, ProductionCode?, Reference?, Lote?, ToolType, IndividualNumber, OperatorId?, IsCorrected, ChainRootId?)`, `InternalRepairDetailDto(..., CorrectionChain)`.

## 5. Application Contracts / Ports

| Interface | Main methods | Location | Implementation(s) |
|---|---|---|---|
| `IReparacaoInternaRepository` | `InsertAsync(uow, record, ct)`, `GetByIdAsync(recordId, ct)`, `GetChainRootAsync(recordId, ct)`, `GetChainAsync(rootRecordId, ct)`, `ListAsync(from, to, line, jobOnId, type, number, operatorId, onlyCorrected, ct)`, `InsertRepairEventAsync(uow, id, notes, actor, occurredAtUtc, ct)`, `InsertAuditEventAsync(uow, action, entityType, entityId, jobOnId?, result, before?, after?, actor, occurredAtUtc, ct)` | `src\BA.Dmo.Application\Modules\ReparacaoInterna\IReparacaoInternaRepository.cs` | `DapperReparacaoInternaRepository` |
| `IJobOnActiveContextLookup` | `ResolveActiveAsync(line, at, ct)` → `InternalRepairContextResolution` | `src\BA.Dmo.Application\Modules\ReparacaoInterna\IJobOnActiveContextLookup.cs` | `DapperJobOnActiveContextLookup` |

`IReparacaoInternaRepository` writes: `internal_repair_records`, `repair_events` (scope `'interna'`), `audit_events` (module `reparacao_interna`) inside the shared `IDbUnitOfWork`.

## 6. Authorization / Catalog Objects

| Object | Literal value | Location |
|---|---|---|
| Module id (`ReparacaoInternaModuleCatalog.ModuleId`) | `reparacao_interna` | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\ReparacaoInternaModuleCatalog.cs` |
| Correction capability id (`ReparacaoInternaModuleCatalog.CorrigirCapabilityId`) | `reparacao_interna.corrigir` | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\ReparacaoInternaModuleCatalog.cs` |
| Canonical module id (`CanonicalModuleCatalog.ReparacaoInternaModuleId`) | `reparacao_interna` | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| Canonical capability (`CanonicalModuleCatalog.ReparacaoInternaCorrigirCapabilityId`) | `reparacao_interna.corrigir` | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| Module definition | `ModuleDefinition("reparacao_interna", "Reparação Interna", Module, order 60, "/reparacao-interna", Capability("reparacao_interna.corrigir"))` | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| Web module policy | `ModulePolicies.ReparacaoInterna = "BaDmo.Module.reparacao_interna"` | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| Server gate | `ReparacaoInternaAuthorizationGate.Require` / `.RequireCorrigir` | `src\BA.Dmo.Application\Modules\ReparacaoInterna\ReparacaoInternaAuthorizationGate.cs` |

## 7. Infrastructure Objects

All under `src\BA.Dmo.Infrastructure\Access\`.

| Class | Interface | Constructor deps | Public methods | DB objects |
|---|---|---|---|---|
| `DapperReparacaoInternaRepository` | `IReparacaoInternaRepository` | `IDbConnectionFactory` | `InsertAsync`, `GetByIdAsync`, `GetChainRootAsync`, `GetChainAsync`, `ListAsync`, `InsertRepairEventAsync`, `InsertAuditEventAsync`; private `MapRecord`, `DisposeAsync` | SELECT/INSERT on `internal_repair_records`; INSERT on `repair_events` (scope `'interna'`); INSERT on `audit_events` (module `reparacao_interna`). `ListAsync` uses `SELECT DISTINCT ON (root_id)` with `root_id = COALESCE(correction_of_id, internal_repair_record_id)`, latest valid per chain root |
| `DapperJobOnActiveContextLookup` | `IJobOnActiveContextLookup` | `IDbConnectionFactory`, `IJobOnRepository` | `ResolveActiveAsync`; private `ReadRevisionContextAsync`, `ExtractString`, `ExtractReference`, `RevisionContext` record | Reads `JobOn` active line set via `IJobOnRepository.GetActiveAsync(line)`; reads `job_on_revision` (`production_snapshot`, `reference_snapshot`, `machine_snapshot`); reads `job_on_component` (`family IN ('MP_CM','MF','BQ')`, `source_lot_id`) → `CmLotIds`/`MfLotIds`/`BqLotIds` |

Dapper SQL embedded in these two classes. No dedicated ERM/migration in Infrastructure.

## 8. Database Objects

RI-specific table: **`internal_repair_records`**.

| Object | Kind | Main technical role | PK / FKs | Constraints / indexes |
|---|---|---|---|---|
| `internal_repair_records` | table | stores quick internal repair records | PK `internal_repair_record_id`; self-FK `correction_of_id`; logical `job_on_id` (uuid, no FK); FK `job_on_revision_id → job_on_revision(job_on_revision_id)` | CHECK `ck_internal_repair_records_type` (N08 `('CM','MF')`; N22 redefined `('CM','MF','BQ')`); CHECK `ck_internal_repair_records_correction` `((correction_of_id IS NULL) = (before_snapshot IS NULL))`; indexes `ix_internal_repair_records_line`, `ix_internal_repair_records_job_on`, `ix_internal_repair_records_revision`; columns `job_on_revision_id`, `production_code`, `reference`, `lot_id` (added N22) |
| `repair_events` (shared repair table, scope `'interna'`/`'externa'`) | table | append-only repair history; RI writes scope `'interna'` | PK `repair_event_id`; FK `internal_repair_record_id → internal_repair_records` (forward FK `fk_repair_events_internal_record`); FK `repair_exit_item_id` (external) | CHECK `ck_repair_events_scope` `('interna','externa')`; index `ix_repair_events_internal`; append-only trigger `trg_repair_events_append_only` → `ba_dmo_guard_append_only()` |

RLS (N12): `internal_repair_records` and `repair_events` are listed in the `rls_tables` array and get `ALTER TABLE ... ENABLE ROW LEVEL SECURITY`.

Classification note: `repair_events` is shared with Reparação Externa (`repair_exit_item_id` FK, scope `'externa'`); it is a shared dependency, not an RI-only object. `internal_repair_records` is RI-specific.

RI-specific table:
- `internal_repair_records`

RI-specific indexes:
- `ix_internal_repair_records_line`
- `ix_internal_repair_records_job_on`
- `ix_internal_repair_records_revision`

RI-specific constraints (listed technically, not counted as DB objects):
- `ck_internal_repair_records_type` (N08 `('CM','MF')`; N22 redefined `('CM','MF','BQ')`)
- `ck_internal_repair_records_correction` `((correction_of_id IS NULL) = (before_snapshot IS NULL))`
- `correction_of_id` self-FK → `internal_repair_records`
- `fk_internal_repair_records_revision` → `job_on_revision(job_on_revision_id)`

Shared dependency:
- `repair_events` (scope `'interna'`/`'externa'`; `ix_repair_events_internal`; `trg_repair_events_append_only`)

RI-specific DB object count: **4** (1 table + 3 indexes + 0 triggers).

## 9. Migration Touchpoints

| Migration | Reparação Interna Object(s) | Technical Change |
|---|---|---|
| `N08_reparacoes.sql` | `internal_repair_records` | `CREATE TABLE` (base columns: id, line, job_on_id, tool_type, individual_number, operator_id, occurred_at_utc, correction_of_id, before_snapshot, correction_reason, created_at_utc, created_by); CHECK `ck_internal_repair_records_type ('CM','MF')`; CHECK `ck_internal_repair_records_correction`; indexes `ix_internal_repair_records_line`, `ix_internal_repair_records_job_on`; self-FK `correction_of_id → internal_repair_records` |
| `N08_reparacoes.sql` | `repair_events` | `internal_repair_record_id uuid NULL` column; index `ix_repair_events_internal`; FK `fk_repair_events_internal_record` → `internal_repair_records` (forward FK added in same script); `trg_repair_events_append_only` |
| `N12_rls.sql` | `internal_repair_records`, `repair_events` | RLS: both listed in `rls_tables`; `ALTER TABLE ... ENABLE ROW LEVEL SECURITY` |
| `N22_reparacao_interna_context.sql` | `internal_repair_records` | Drops and recreates `ck_internal_repair_records_type` as `('CM','MF','BQ')`; `ADD COLUMN job_on_revision_id`, `ADD COLUMN production_code`, `ADD COLUMN reference`, `ADD COLUMN lot_id`; index `ix_internal_repair_records_revision`; FK `fk_internal_repair_records_revision → job_on_revision(job_on_revision_id)` |

Total RI migration touchpoints: **3 distinct migration files** (`N08_reparacoes.sql`, `N12_rls.sql`, `N22_reparacao_interna_context.sql`). `N25_remediation.sql` does not modify RI-specific objects.

## 10. Web / Routes

Route surface: `src\BA.Dmo.Web\Pages\ReparacaoInterna\Index.cshtml` (`@page "/reparacao-interna"`), `Index.cshtml.cs` (`IndexModel.OnGet` sets `CanCorrigir` from `CanonicalModuleCatalog.ReparacaoInternaCorrigirCapabilityId`).

| Route | Technical Entry Point | Authorization | File |
|---|---|---|---|
| `/reparacao-interna` | Razor page `IndexModel.OnGet` | `[Authorize(Policy = ModulePolicies.ReparacaoInterna)]` | `src\BA.Dmo.Web\Pages\ReparacaoInterna\Index.cshtml` / `Index.cshtml.cs` |
| `GET /api/reparacao-interna/line-cards` | `ReparacaoInternaService.ListLineCardsAsync` | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` |
| `GET /api/reparacao-interna/context?line=` | `ReparacaoInternaService.ResolveLineContextAsync` | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` |
| `POST /api/reparacao-interna` | `ReparacaoInternaService.RegistrarReparacoesAsync` | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` |
| `GET /api/reparacao-interna/historico` | `ReparacaoInternaService.ListHistoryAsync` (uses `ParseInternalToolType`) | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` |
| `GET /api/reparacao-interna/{recordId:guid}` | `ReparacaoInternaService.GetDetailAsync` | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` |
| `POST /api/reparacao-interna/{recordId:guid}/corrigir` | `ReparacaoInternaService.CorrigirReparacaoAsync` | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` |
| `GET /api/boquilhas/production-context` | `ReparacaoInternaService.ListLineCardsAsync` | `ModulePolicies.Boquilhas` | `src\BA.Dmo.Web\Program.cs` (boquilhas.js consumer) |

Web text parser `ParseInternalToolType` defines recordable types for history filters: `"CM"`→CM, `"MF"`→MF, else `null` (BQ unmapped).

## 11. Static Assets

Dedicated:
- `src\BA.Dmo.Web\wwwroot\scripts\reparacao-interna.js` — renders Registo/Histórico tabs, line-card selector, context resolution, register (`/api/reparacao-interna`), history (`/api/reparacao-interna/historico`), detail (`/api/reparacao-interna/{id}`), correction (`/api/reparacao-interna/{id}/corrigir`). Contains `parseNumbers` (newline/comma/space split, repeats preserved), `collectFilter`. References `data-type` buttons including the static `BQ` type button; on register it sends `toolType: currentType` to the API.
- `src\BA.Dmo.Web\wwwroot\styles\modules\reparacao-interna-layout.css` — layout/composition class selectors carrying the `reparacao-interna-*` prefix (`.reparacao-interna-page`, `.reparacao-interna-tabs`, `.reparacao-interna-selector`, `.reparacao-interna-context-grid`, `.reparacao-interna-override`, `.reparacao-interna-filters`, `.reparacao-interna-correction`, `.reparacao-interna-detail`, `.reparacao-interna-pagination`, `.reparacao-interna-strong`, etc.).

Shared static consumers carrying RI rules:
- `src\BA.Dmo.Web\wwwroot\scripts\boquilhas.js` — calls `/api/boquilhas/production-context` (which invokes `ReparacaoInternaService.ListLineCardsAsync`) to render the Boquilhas line side-panel with production/reference context.

## 12. Tests

| Test class | Kind | Direct target | Main method groups | Location |
|---|---|---|---|---|
| `ReparacaoInternaDomainTests` | unit | `InternalRepairRecord`, `InternalRepairToolTypeCodec`, `InternalRepairRules`, `InternalRepairContext` | `Create_*`, `CreateCorrection_*`, `Rules_*` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaDomainTests.cs` |
| `ReparacaoInternaServiceTests` | unit | `ReparacaoInternaService` | `Register_*`, `ListLineCards_*`, `Corrigir_*`, `ListHistory_*`, `GetDetail_*` (fakes in-memory) | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaServiceTests.cs` |
| `ReparacaoInternaProductionProjectionTests` | unit | `ReparacaoInternaProductionProjection` | `ActivationUtc_*`, `SelectEffective_*` | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaProductionProjectionTests.cs` |
| `ReparacaoInternaWebApiTests` | integration (WebApplicationFactory) | `/api/reparacao-interna/*` endpoints + authorization guards | `Anonymous_IsDenied_*`, `AuthorizedRepIntUser_*`, `UserWithoutRepIntModule_IsDenied`, `Correcao_WithoutCorrigirCapability_IsForbidden` | `tests\BA.Dmo.IntegrationTests\ReparacaoInterna\ReparacaoInternaWebApiTests.cs` |

Test data tool-type values used: `CM`, `MF`; BQ rejected via cast `(InternalRepairToolType)99` in service/domain tests; full reference `5447T173` preserved as context.

## 13. Test Doubles / Helpers

Dedicated test support files:
- `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs` — `ReparacaoInternaFixedClock` (IClock), `ReparacaoInternaFakeAuthorship` (IPersistenceAuthorshipAccessor), `FakeReparacaoInternaUowFactory`, `FakeReparacaoInternaUnitOfWork`, `ReparacaoInternaCurrentUser` (module + corrigir capability), `FakeReparacaoInternaRepository` (IReparacaoInternaRepository, `FailInsert` flag), `FakeJobOnActiveContextLookup` (IJobOnActiveContextLookup; `SeedSingle/SeedNone/SeedAmbiguous`, `Context` builder), `FakeFerramentasPieceLookup` (IFerramentasPieceLookup; CM/MF pieces).

Nested in `ReparacaoInternaWebApiTests.cs` (integration support, in-file): `ReparacaoInternaWebApiTests.RepIntFixture`, `FakeAuthAdapter`, `FakeRepIntRepo`, `FakeContextLookup`, `FakePieceLookup`, `FakeUowFactory`, `FakeUow`, `FakeIdentityRepository`, `ValidRepIntUser`, `UserWithoutRepInt`.

## 14. Direct Reparação Interna References

One edge per relationship.

- `ReparacaoInternaService` → `IReparacaoInternaRepository` (constructor dependency)
- `ReparacaoInternaService` → `IJobOnActiveContextLookup` (constructor dependency)
- `ReparacaoInternaService` → `IFerramentasPieceLookup` (constructor dependency, external)
- `ReparacaoInternaService` → `IRepairUnitOfWorkFactory` (constructor dependency)
- `ReparacaoInternaService` → `ReparacaoInternaAuthorizationGate` (constructor dependency)
- `ReparacaoInternaService` → `IClock` (constructor dependency)
- `ReparacaoInternaService` → `ReparacaoInternaModuleCatalog.Lines` (line selector)
- `ReparacaoInternaService` → `InternalRepairToolTypeCodec` (ToStorage)
- `IReparacaoInternaRepository` → `DapperReparacaoInternaRepository` (DI registration)
- `IJobOnActiveContextLookup` → `DapperJobOnActiveContextLookup` (DI registration)
- `DapperReparacaoInternaRepository` → `internal_repair_records` (SQL reads/writes)
- `DapperReparacaoInternaRepository` → `repair_events` scope `'interna'` (SQL write)
- `DapperReparacaoInternaRepository` → `audit_events` module `reparacao_interna` (SQL write)
- `DapperJobOnActiveContextLookup` → `ReparacaoInternaProductionProjection.SelectEffective` (projection call)
- `DapperJobOnActiveContextLookup` → `job_on_revision` (SQL read)
- `DapperJobOnActiveContextLookup` → `job_on_component` families `MP_CM/MF/BQ` (SQL read)
- `ReparacaoInternaAuthorizationGate` → `ICurrentUserAccessor` + `IPersistenceAuthorshipAccessor` (constructor deps)
- `InternalRepairRecord` → `ReparacaoInternaModuleCatalog.Lines` (line validation)
- `InternalRepairRecord` → `job_on_revision` (FK via `JobOnRevisionId`)

## 15. External Technical References

| Reparação Interna Object | External Technical Reference | Reference Type |
|---|---|---|
| `ReparacaoInternaService` | `IFerramentasPieceLookup` (Ferramentas, read-only) | constructor dependency / application port |
| `ReparacaoInternaService` | `FerramentasToolType.CM`/`FerramentasToolType.MF` (`TryMapToFerramentas`) | enum/reference reuse |
| `InternalRepairContext` | `job_on_component` family `'BQ'` `source_lot_id` (`BqLotIds`) | query join / identifier field (context only) |
| `DapperJobOnActiveContextLookup` | `IJobOnRepository` (Job On) | constructor dependency / application port |
| `DapperJobOnActiveContextLookup` | `job_on_revision` (`production_snapshot`, `reference_snapshot`, `machine_snapshot`) | query read |
| `internal_repair_records.job_on_revision_id` | `job_on_revision(job_on_revision_id)` | DB FK |
| `internal_repair_records.job_on_id` | Job On logical link (uuid, no FK) | identifier field |
| `ReparacaoInternaService.ListLineCardsAsync` | `GET /api/boquilhas/production-context` (Boquilhas route calling the RI service) | route reference |
| `ReparacaoInternaService.ListLineCardsAsync` | `boquilhas.js` consumer | shared static consumer |
| `repair_events` scope `'interna'` | `repair_exit_item_id` FK + scope `'externa'` (Reparação Externa) | shared repair table dependency |
| `DapperReparacaoInternaRepository` | `audit_events` (global) | constructor-free query/app write |
| `ReparacaoInternaWebApiTests` | `IFerramentasPieceLookup`, `IRepairUnitOfWorkFactory`, `IJobOnActiveContextLookup` fakes | test target/support |
| `reparacao_interna` module id | `Pages\Historia\Index.cshtml` `ModuleLabel` switch: `"reparacao_interna" => "Reparação Interna"` | shared Web consumer (module label mapping) |

## 16. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `ReparacaoInternaModuleCatalog` | Domain | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` |
| `InternalRepairRecord` | Domain | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` |
| `InternalRepairToolType` + codec | Domain | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` |
| `InternalRepairContext` + `InternalRepairContextResolution` + `InternalRepairResolutionKind` + `InternalRepairContextCandidate` | Domain | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` |
| `InternalRepairRules` | Domain | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` |
| `ReparacaoInternaProductionProjection` | Domain | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` |
| `ReparacaoInternaService` | Application | `src\BA.Dmo.Application\Modules\ReparacaoInterna\` |
| `ReparacaoInternaAuthorizationGate` / `ReparacaoInternaExecutor` | Application | `src\BA.Dmo.Application\Modules\ReparacaoInterna\` |
| `IReparacaoInternaRepository` | Application (port) | `src\BA.Dmo.Application\Modules\ReparacaoInterna\` |
| `IJobOnActiveContextLookup` | Application (port) | `src\BA.Dmo.Application\Modules\ReparacaoInterna\` |
| RI requests + DTOs | Application | `src\BA.Dmo.Application\Modules\ReparacaoInterna\ReparacaoInternaRequests.cs` |
| `DapperReparacaoInternaRepository` | Infrastructure | `src\BA.Dmo.Infrastructure\Access\` |
| `DapperJobOnActiveContextLookup` | Infrastructure | `src\BA.Dmo.Infrastructure\Access\` |
| `internal_repair_records` | Database | `database\migrations\N08_reparacoes.sql`, `N22_reparacao_interna_context.sql` |
| `/reparacao-interna` page + `/api/reparacao-interna/*` | Web | `src\BA.Dmo.Web\Pages\ReparacaoInterna\`, `src\BA.Dmo.Web\Program.cs` |
| `ModulePolicies.ReparacaoInterna` | Web (authorization) | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| `reparacao-interna.js` / `reparacao-interna-layout.css` | Static assets | `src\BA.Dmo.Web\wwwroot\` |
| RI tests | Tests | `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\`, `tests\BA.Dmo.IntegrationTests\ReparacaoInterna\` |

## 17. Sources Verified

- `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` (6 files)
- `src\BA.Dmo.Application\Modules\ReparacaoInterna\` (5 files)
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperReparacaoInternaRepository.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperJobOnActiveContextLookup.cs`
- `src\BA.Dmo.Web\Program.cs` (DI + `/api/reparacao-interna/*` + `/api/boquilhas/production-context` + `ParseInternalToolType`)
- `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`
- `src\BA.Dmo.Web\Pages\ReparacaoInterna\Index.cshtml` / `Index.cshtml.cs`
- `src\BA.Dmo.Web\wwwroot\scripts\reparacao-interna.js`
- `src\BA.Dmo.Web\wwwroot\styles\modules\reparacao-interna-layout.css`
- `src\BA.Dmo.Web\wwwroot\scripts\boquilhas.js` (lines 81–104)
- `database\migrations\N08_reparacoes.sql`, `N12_rls.sql`, `N22_reparacao_interna_context.sql`, `N25_remediation.sql`
- `tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\` (4 files)
- `tests\BA.Dmo.IntegrationTests\ReparacaoInterna\ReparacaoInternaWebApiTests.cs`

## Counts

- Domain Reparação Interna files: **6**
- Application Reparação Interna files: **5**
- Infrastructure Reparação Interna files: **2**
- Dedicated Web page files: **2** (`Index.cshtml`, `Index.cshtml.cs`)
- Dedicated static asset files: **2** (`reparacao-interna.js`, `reparacao-interna-layout.css`)
- Shared Web wiring files: **2** (`Program.cs`, `ModuleAuthorizationHandler.cs`)
- Shared static asset files carrying RI rules: **1** (`wwwroot\scripts\boquilhas.js`)
- Shared Application catalog files: **1** (`src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`)
- Reparação Interna DB objects: 1 RI-specific table + 3 indexes + 0 triggers = **4** (on `internal_repair_records`; shared `repair_events` counted as shared dependency, not RI-specific)
- Reparação Interna migration touchpoints: **3 distinct migration files** (`N08_reparacoes.sql`, `N12_rls.sql`, `N22_reparacao_interna_context.sql`)
- Reparação Interna test classes: **4**
- Reparação Interna test support files: **1** dedicated (`ReparacaoInternaTestSupport.cs`) + 1 in-file integration fixture (`ReparacaoInternaWebApiTests.cs`)