# BA DMO — Reparação Interna Technical Map

MAP ID: MAP-11
Status: COMPLETE

## Cross-References

Related technical maps (cross-layer navigation and reconciliation point):

- [`00_INDEX.md`](00_INDEX.md) — map registry
- [`01_DOMAIN.md`](01_DOMAIN.md) — domain layer
- [`02_DATABASE.md`](02_DATABASE.md) — database objects
- [`03_MIGRATIONS.md`](03_MIGRATIONS.md) — migration families
- [`04_DAPPER_INFRASTRUCTURE.md`](04_DAPPER_INFRASTRUCTURE.md) — Dapper persistence
- [`05_TESTS.md`](05_TESTS.md) — test suites
- [`06_JOB_ON.md`](06_JOB_ON.md) — Job On (active-context dependency via `IJobOnActiveContextLookup`)
- [`12_REPARACAO_EXTERNA.md`](12_REPARACAO_EXTERNA.md) — Reparação Externa (shared `repair_events` dependency)
- [`19_APPLICATION.md`](19_APPLICATION.md) — application services
- [`20_WEB.md`](20_WEB.md) — web/API surface

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
- 18. Findings / NEEDS REVIEW
- Counts

## 1. Scope

Technical inventory and navigation of Reparação Interna-specific objects across Domain, Application, Infrastructure, Database, Web/static assets and Tests, grounded in current source. Cross-layer navigation only; no end-to-end flow, no business-rule interpretation. Design/SOT not used as evidence.

## 2. Layer Summary

| Layer | Main Reparação Interna Objects | Locations |
|---|---|---|
| Domain | `ReparacaoInternaModuleCatalog`, `InternalRepairRecord`, `InternalRepairToolType`, `InternalRepairContext`, `InternalRepairContextResolution`, `InternalRepairResolutionKind`, `InternalRepairRules`, `ReparacaoInternaProductionProjection` | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` |
| Application | `ReparacaoInternaService`, `ReparacaoInternaAuthorizationGate`, `ReparacaoInternaExecutor`, `IReparacaoInternaRepository`, `IJobOnActiveContextLookup`, requests/DTOs | `src\BA.Dmo.Application\Modules\ReparacaoInterna\` |
| Infrastructure | `DapperReparacaoInternaRepository`, `DapperJobOnActiveContextLookup` | `src\BA.Dmo.Infrastructure\Access\` |
| Database | `internal_repair_records` (RI-specific); shared `repair_events` (scope 'interna'/'externa') | `database\migrations\N08_reparacoes.sql`, `N22_reparacao_interna_context.sql`, `N28_reparacao_interna_cm_mf_only.sql`, `N12_rls.sql` |
| Web | `/reparacao-interna` Razor page; `/api/reparacao-interna/*` endpoints + `/api/boquilhas/production-context` | `src\BA.Dmo.Web\Pages\ReparacaoInterna\`, `src\BA.Dmo.Web\Program.cs` |
| Static assets | `reparacao-interna.js`, `reparacao-interna-layout.css` | `src\BA.Dmo.Web\wwwroot\scripts\`, `src\BA.Dmo.Web\wwwroot\styles\modules\` |
| Tests | `ReparacaoInternaDomainTests`, `ReparacaoInternaServiceTests`, `ReparacaoInternaProductionProjectionTests`, `ReparacaoInternaWebApiTests` | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\ReparacaoInterna\` |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | YES | `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` |
| Application | YES | `src\BA.Dmo.Application\Modules\ReparacaoInterna\` |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\DapperReparacaoInternaRepository.cs`, `DapperJobOnActiveContextLookup.cs` |
| Web | YES | `src\BA.Dmo.Web\Pages\ReparacaoInterna\`; `src\BA.Dmo.Web\Program.cs`; `Authorization\ModuleAuthorizationHandler.cs` |
| Database | YES | `database\migrations\N08_reparacoes.sql`, `N22_reparacao_interna_context.sql`, `N28_reparacao_interna_cm_mf_only.sql`, `N12_rls.sql` |
| Tests | YES | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\ReparacaoInterna\` |

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
| `InternalRepairRules` | static rules | `ContextMismatchInfoCode = "REPINT_CONTEXT_MISMATCH_INFO"`; `NoActiveContextInfoCode = "REPINT_NO_ACTIVE_CONTEXT_INFO"`; `EvalCollectibleWhen(kind)` → always `Success`; `NumberInContextLot(context, type, pieceLotId)` (CM→`CmLotIds`, MF→`MfLotIds`, else `null`) | `InternalRepairRules.cs`; no `src\` caller outside this file (service does not reference it) — see §18 R4 |
| `Unit` | readonly record struct | `Value` | `InternalRepairRules.cs`; local result unit |
| `ReparacaoInternaProductionProjection` | static projection | `FactoryLocalOffsetUtc = +01:00`; `ActivationUtc(plannedStartAt)` → local start date at 09:00 UTC; `SelectEffective(candidates, at)` → most-recent active start with `ActivationUtc <= at`, no end-date test, null when none | `ReparacaoInternaProductionProjection.cs`; consumes `JobOn` (`IsActive`, `PlannedStartAt`) |

### 3.1 Tool-type inventor (recordable set)

Literal source values that define which types are recordable/selectable/validated in Reparação Interna. **Current authoritative rule (N28 owner decision): CM/MF ONLY — BQ is never a recordable internal repair type.**

| Source | Literal allowed values | Location |
|---|---|---|
| `InternalRepairToolType` enum | `CM`, `MF` | `InternalRepairToolType.cs` |
| `InternalRepairToolTypeCodec.ToStorage` | `"CM"`, `"MF"` | `InternalRepairToolType.cs` |
| `InternalRepairToolTypeCodec.FromStorage` | throws on any persisted value other than `"CM"`/`"MF"` (a legacy `'BQ'` row is invalid under CM/MF-only) | `InternalRepairToolType.cs` |
| `InternalRepairRecord.Create` guard | rejects `toolType is not (CM or MF)` → `REPINT_INVALID_TYPE` | `InternalRepairRecord.cs` |
| `InternalRepairRecord.CreateCorrection` guard | rejects `toolType is not (CM or MF)` → `REPINT_INVALID_TYPE` | `InternalRepairRecord.cs` |
| `ReparacaoInternaService.RegistrarReparacoesAsync` | rejects `request.ToolType is not (CM or MF)` → `REPINT_INVALID_TYPE` | `ReparacaoInternaService.cs` |
| `ParseInternalToolType` (Web) | `"CM"`→CM, `"MF"`→MF, else `null` (BQ unmapped) | `src\BA.Dmo.Web\Program.cs` lines 1081–1087 |
| `ck_internal_repair_records_type` (N08 original) | `('CM','MF')` | `database\migrations\N08_reparacoes.sql` |
| `ck_internal_repair_records_type` (N22 redefined) | `('CM','MF','BQ')` (temporarily widened, OWNER DECISION R009) | `database\migrations\N22_reparacao_interna_context.sql` |
| `ck_internal_repair_records_type` (N28 redefined — CURRENT) | `('CM','MF')` `NOT VALID` + `VALIDATE`, with fail-closed guard raising if any non-CM/MF row exists | `database\migrations\N28_reparacao_interna_cm_mf_only.sql` |
| Static CSHTML register buttons | `data-type="CM"`, `data-type="MF"` ONLY (the N22-era `data-type="BQ"` button is REMOVED) | `src\BA.Dmo.Web\Pages\ReparacaoInterna\Index.cshtml` lines 48–51 |
| Static CSHTML correction type options | `<option value="CM">`, `<option value="MF">` ONLY (the N22-era `<option value="BQ">` is REMOVED) | `src\BA.Dmo.Web\Pages\ReparacaoInterna\Index.cshtml` lines 207–213 |
| Integration test | `ActiveSurface_ExposesOnlyCmMf_AndApiRejectsBq` — asserts the active surface exposes only CM/MF and the API rejects BQ | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\ReparacaoInterna\ReparacaoInternaWebApiTests.cs` |

BQ context-only facts (source-grounded, mechanical):
- `InternalRepairContext.BqLotIds` (Guid list) — a context record member; sourced from `job_on_component` family `'BQ'` (`source_lot_id`) in `DapperJobOnActiveContextLookup.ReadRevisionContextAsync`.
- `DapperJobOnActiveContextLookup` query reads families `IN ('MP_CM','MF','BQ')`.
- `ReparacaoInternaProductionProjection`/context summary has no BQ repair-type branch; `InternalRepairToolType` declares only `CM` and `MF`, while `InternalRepairContext.BqLotIds` carries BQ source-lot ids.
- The active-context reference is read as the full reference string (e.g. `5447T173`); the domain/service preserve the full reference verbatim via `Reference`. The `T173` suffix inside a full reference is context-only — never a recordable BQ type (service comment, `ReparacaoInternaService.cs` lines 147–152; unit test `Register_FullReference_KeepsContextOnlySuffix`).
- N28 (`N28_reparacao_interna_cm_mf_only.sql`, current state) restores the CM/MF-only CHECK; the consolidated build (`database\consolidated_clean_install.sql`) embodies the same final state (`ck_internal_repair_records_type CHECK (tool_type IN ('CM', 'MF'))`, line 853).

## 4. Application Objects

All under `src\BA.Dmo.Application\Modules\ReparacaoInterna\`.

| Object | Kind | Public methods | Constructor deps |
|---|---|---|---|
| `ReparacaoInternaService` | service | `ListLineCardsAsync(ct)`, `ResolveLineContextAsync(line, ct)`, `RegistrarReparacoesAsync(request, ct)`, `RegisterReparacaoAsync(request, ct)` (back-compat alias delegating to `RegistrarReparacoesAsync`), `ListHistoryAsync(filter, ct)`, `GetDetailAsync(recordId, ct)`, `CorrigirReparacaoAsync(request, ct)` | `IReparacaoInternaRepository`, `IJobOnActiveContextLookup`, `IFerramentasPieceLookup`, `IRepairUnitOfWorkFactory`, `ReparacaoInternaAuthorizationGate`, `IClock` |
| `ReparacaoInternaAuthorizationGate` | gate | `Require()` → `Result<ReparacaoInternaExecutor, DomainError>`; `RequireCorrigir(actorId)` → requires module + capability | `ICurrentUserAccessor`, `IPersistenceAuthorshipAccessor` |
| `ReparacaoInternaExecutor` | record | `ActorId`, `DisplayName` | — |

Private helpers of `ReparacaoInternaService`: `ResolveEffectiveLotIdAsync`, `BuildDetail`, `TryMapToFerramentas` (CM→`FerramentasToolType.CM`, MF→`FerramentasToolType.MF`; no BQ branch), `Serialize` (JSON for audit). Audit action codes written: `reparacao_interna.registrar` (result `succeeded`), `reparacao_interna.corrigir` (result `corrected`). Registered error codes: `REPINT_FORBIDDEN`, `REPINT_CORRIGIR_FORBIDDEN`, `REPINT_NUMBER_REQUIRED`, `REPINT_INVALID_TYPE`, `REPINT_NOT_FOUND`, `REPINT_CORRECTION_CHAIN`, `REPINT_SAVE_FAILED` (+ domain codes `REPINT_LINE_UNKNOWN`, `REPINT_OPERATOR_REQUIRED`, `REPINT_CORRECTOR_REQUIRED`; web-layer `REPINT_CONTEXT_READ_ONLY`).

Requests/DTOs (`ReparacaoInternaRequests.cs`): requests `RegisterReparacaoRequest(Line, ToolType, Numbers, OverrideProduction?, OverrideReference?)`, `CorrigirReparacaoRequest(RecordId, Line, ToolType, IndividualNumber, JobOnId?, JobOnRevisionId?, ProductionCode?, Reference?, LotId?, Reason?)`; filter `InternalRepairFilter(From, To, Line, JobOnId, ToolType?, Number, OperatorId, OnlyCorrected)`; DTOs `InternalRepairLineCard(Line, Reference?, ProductionCode?, HasActiveContext)`, `InternalRepairContextDto(Kind, JobOnId?, JobOnRevisionId?, ProductionCode?, Reference?, MachineCode?, ValidFromUtc?, ValidToUtc?, Candidates)`, `InternalRepairCandidateDto(...)`, `InternalRepairHistoryRow(RecordId, DataHora, Line, ProductionCode?, Reference?, Lote?, ToolType, IndividualNumber, OperatorId?, IsCorrected, ChainRootId?)`, `InternalRepairDetailDto(..., CorrectionChain)`.

Note: `CorrigirReparacaoAsync` recalibrates the context when the correction moves the record to a different line (R009/C3): fields left null are re-resolved from the NEW line's Single active production; LotId is never auto-derived on a line move (service lines 315–343). The correction request's context fields and the register request's `OverrideProduction`/`OverrideReference` are rejected server-side by the routes with `REPINT_CONTEXT_READ_ONLY` (see §10).

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
| Canonical module id (`CanonicalModuleCatalog.ReparacaoInternaModuleId`) | `reparacao_interna` | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` line 24 |
| Canonical capability (`CanonicalModuleCatalog.ReparacaoInternaCorrigirCapabilityId`) | `reparacao_interna.corrigir` | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` line 36 |
| Module definition | `ModuleDefinition("reparacao_interna", "Reparação Interna", Module, order 60, "/reparacao-interna", Capability("reparacao_interna.corrigir"))` | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` lines 118–119 |
| Canonical page | `CanonicalPageCatalog.ReparacaoInternaRegistoPageId = "reparacao_interna.registo"` (line 20); `PageDefinition(..., "/reparacao-interna", ...)` (line 66) | `src\BA.Dmo.Application\Shared\Access\CanonicalPageCatalog.cs` |
| Web module policy | `ModulePolicies.ReparacaoInterna = "BaDmo.Module.reparacao_interna"` | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` line 60 |
| Server gate | `ReparacaoInternaAuthorizationGate.Require` / `.RequireCorrigir` | `src\BA.Dmo.Application\Modules\ReparacaoInterna\ReparacaoInternaAuthorizationGate.cs` |

## 7. Infrastructure Objects

All under `src\BA.Dmo.Infrastructure\Access\`.

| Class | Interface | Constructor deps | Public methods | DB objects |
|---|---|---|---|---|
| `DapperReparacaoInternaRepository` | `IReparacaoInternaRepository` | `IDbConnectionFactory` | `InsertAsync`, `GetByIdAsync`, `GetChainRootAsync`, `GetChainAsync`, `ListAsync`, `InsertRepairEventAsync`, `InsertAuditEventAsync`; private `MapRecord`, `DisposeAsync` | SELECT/INSERT on `internal_repair_records`; INSERT on `repair_events` (scope `'interna'`); INSERT on `audit_events` (module `reparacao_interna`). `ListAsync` uses `SELECT DISTINCT ON (root_id)` with `root_id = COALESCE(correction_of_id, internal_repair_record_id)`, latest valid per chain root |
| `DapperJobOnActiveContextLookup` | `IJobOnActiveContextLookup` | `IDbConnectionFactory`, `IJobOnRepository` | `ResolveActiveAsync`; private `ReadRevisionContextAsync`, `ExtractString`, `ExtractReference`, `RevisionContext` record | Reads `JobOn` active line set via `IJobOnRepository.GetActiveAsync(line)` (no from/to filter — GAP 1 fix); feeds `ReparacaoInternaProductionProjection.SelectEffective`; reads `job_on_revision` (`production_snapshot`, `reference_snapshot`, `machine_snapshot`); reads `job_on_component` (`family IN ('MP_CM','MF','BQ')`, `source_lot_id`) → `CmLotIds`/`MfLotIds`/`BqLotIds` |

Dapper SQL embedded in these two classes. No dedicated ERM/migration in Infrastructure.

## 8. Database Objects

RI-specific table: **`internal_repair_records`**.

| Object | Kind | Main technical role | PK / FKs | Constraints / indexes |
|---|---|---|---|---|
| `internal_repair_records` | table | stores quick internal repair records | PK `internal_repair_record_id`; self-FK `correction_of_id`; logical `job_on_id` (uuid, no FK); FK `job_on_revision_id → job_on_revision(job_on_revision_id)` | CHECK `ck_internal_repair_records_type` (N08 `('CM','MF')`; N22 redefined `('CM','MF','BQ')`; **N28 redefined back to `('CM','MF')` NOT VALID + VALIDATE** — current state); CHECK `ck_internal_repair_records_correction` `((correction_of_id IS NULL) = (before_snapshot IS NULL))`; indexes `ix_internal_repair_records_line`, `ix_internal_repair_records_job_on`, `ix_internal_repair_records_revision`; columns `job_on_revision_id`, `production_code`, `reference`, `lot_id` (added N22) |
| `repair_events` (shared repair table, scope `'interna'`/`'externa'`) | table | append-only repair history; RI writes scope `'interna'` | PK `repair_event_id`; FK `internal_repair_record_id → internal_repair_records` (forward FK `fk_repair_events_internal_record`); FK `repair_exit_item_id` (external) | CHECK `ck_repair_events_scope` `('interna','externa')`; index `ix_repair_events_internal`; append-only trigger `trg_repair_events_append_only` → `ba_dmo_guard_append_only()` |

RLS (N12): `internal_repair_records` and `repair_events` are listed in the `rls_tables` array (N12 lines 54–55) and get `ALTER TABLE ... ENABLE ROW LEVEL SECURITY`; both are in the `policy_tables` array for `ba_dmo_app_access` (N12 lines 120–121). (N08 siblings `repairers`, `line_repairer_defaults`, `repair_exits`, `repair_exit_items` are also in those arrays.)

Classification note: `repair_events` is shared with Reparação Externa (`repair_exit_item_id` FK, scope `'externa'`); it is a shared dependency, not an RI-only object. `internal_repair_records` is RI-specific.

RI-specific table:
- `internal_repair_records`

RI-specific indexes:
- `ix_internal_repair_records_line`
- `ix_internal_repair_records_job_on`
- `ix_internal_repair_records_revision`

RI-specific constraints (listed technically, not counted as DB objects):
- `ck_internal_repair_records_type` (N08 `('CM','MF')`; N22 redefined `('CM','MF','BQ')`; N28 current `('CM','MF')`)
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
| `N12_rls.sql` | `internal_repair_records`, `repair_events` | RLS: both listed in `rls_tables`/`policy_tables`; `ALTER TABLE ... ENABLE ROW LEVEL SECURITY` + `ba_dmo_app_access` policy |
| `N22_reparacao_interna_context.sql` | `internal_repair_records` | Drops and recreates `ck_internal_repair_records_type` as `('CM','MF','BQ')`; `ADD COLUMN job_on_revision_id`, `ADD COLUMN production_code`, `ADD COLUMN reference`, `ADD COLUMN lot_id`; index `ix_internal_repair_records_revision`; FK `fk_internal_repair_records_revision → job_on_revision(job_on_revision_id)` |
| `N28_reparacao_interna_cm_mf_only.sql` | `internal_repair_records` | CM/MF-only convergence (owner decision): fail-closed guard `RAISE EXCEPTION` if any `tool_type NOT IN ('CM','MF')` exists; `DROP CONSTRAINT` + `ADD CONSTRAINT ck_internal_repair_records_type CHECK (tool_type IN ('CM','MF')) NOT VALID` + `VALIDATE CONSTRAINT` |

Total RI migration touchpoints: **4 distinct migration files** (`N08_reparacoes.sql`, `N12_rls.sql`, `N22_reparacao_interna_context.sql`, `N28_reparacao_interna_cm_mf_only.sql`). `N25_remediation.sql` does not modify RI-specific objects (it does touch `job_on_revision`/`job_on_component` family append-only triggers, which are Job On objects outside this vertical slice).

## 10. Web / Routes

Route surface: `src\BA.Dmo.Web\Pages\ReparacaoInterna\Index.cshtml` (`@page "/reparacao-interna"`), `Index.cshtml.cs` (`IndexModel.OnGet` sets `CanCorrigir` from `user.HasCapability(CanonicalModuleCatalog.ReparacaoInternaCorrigirCapabilityId)`).

| Route | Technical Entry Point | Authorization | File / line |
|---|---|---|---|
| `/reparacao-interna` | Razor page `IndexModel.OnGet` | `[Authorize(Policy = ModulePolicies.ReparacaoInterna)]` | `src\BA.Dmo.Web\Pages\ReparacaoInterna\Index.cshtml` / `Index.cshtml.cs` |
| `GET /api/reparacao-interna/line-cards` | `ReparacaoInternaService.ListLineCardsAsync` | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` line 1090 |
| `GET /api/reparacao-interna/context?line=` | `ReparacaoInternaService.ResolveLineContextAsync` | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` line 1099 |
| `POST /api/reparacao-interna` | `ReparacaoInternaService.RegistrarReparacoesAsync` (rejects `OverrideProduction`/`OverrideReference` → `REPINT_CONTEXT_READ_ONLY` lines 1112–1118) | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` line 1109 |
| `GET /api/reparacao-interna/historico` | `ReparacaoInternaService.ListHistoryAsync` (uses `ParseInternalToolType`) | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` line 1126 |
| `GET /api/reparacao-interna/{recordId:guid}` | `ReparacaoInternaService.GetDetailAsync` | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` line 1139 |
| `POST /api/reparacao-interna/{recordId:guid}/corrigir` | `ReparacaoInternaService.CorrigirReparacaoAsync` (rejects non-null context overrides → `REPINT_CONTEXT_READ_ONLY` lines 1152–1161) | `ModulePolicies.ReparacaoInterna` | `src\BA.Dmo.Web\Program.cs` line 1149 |
| `GET /api/boquilhas/production-context` | `ReparacaoInternaService.ListLineCardsAsync` | `ModulePolicies.Boquilhas` | `src\BA.Dmo.Web\Program.cs` line 1455 (boquilhas.js consumer) |

Web text parser `ParseInternalToolType` defines recordable types for history filters: `"CM"`→CM, `"MF"`→MF, else `null` (BQ unmapped) — `src\BA.Dmo.Web\Program.cs` lines 1081–1087 (local static function above the RI endpoint block).

## 11. Static Assets

Dedicated:
- `src\BA.Dmo.Web\wwwroot\scripts\reparacao-interna.js` — renders Registo/Histórico tabs, full-width line-card selector, context resolution (`GET /api/reparacao-interna/context`), type choice CM/MF (NO BQ button — the static `data-type="BQ"` button was removed), register summary card then confirm (`POST /api/reparacao-interna`), history (`GET /api/reparacao-interna/historico`) with client-side pagination (`pageState.from/pageSize=20`, prev/next), row select + double-click detail (`GET /api/reparacao-interna/{id}`), correction (`POST /api/reparacao-interna/{id}/corrigir`, gated by the `data-corrigir` button only rendered when `Model.CanCorrigir`), link init from `?jobOnId=`/`?line=`/`?view=` query params. Contains `parseNumbers` (split on whitespace/comma/semicolon `[\s,;]+`, repeats preserved), `collectFilter`. On register it sends `toolType: currentType` (CM/MF only) to the API.
- `src\BA.Dmo.Web\wwwroot\styles\modules\reparacao-interna-layout.css` — layout/composition class selectors carrying the `reparacao-interna-*` prefix (`.reparacao-interna-page`, `.reparacao-interna-tabs`, `.reparacao-interna-selector`, `.reparacao-interna-context`, `.reparacao-interna-context-grid`, `.reparacao-interna-type-choice`, `.reparacao-interna-strong`, `.reparacao-interna-filters`, `.reparacao-interna-inline`, `.reparacao-interna-action-row`, `.reparacao-interna-actions`, `.reparacao-interna-pagination`, `.reparacao-interna-original-note`, `.reparacao-interna-correction`, `.reparacao-interna-detail`, plus `.line-choice`/`.line-card` and `#historyTable tbody tr.selected`).

Shared static consumers carrying RI rules:
- `src\BA.Dmo.Web\wwwroot\scripts\boquilhas.js` — calls `/api/boquilhas/production-context` (which invokes `ReparacaoInternaService.ListLineCardsAsync`) to render the Boquilhas line side-panel with production/reference context (line 412).

## 12. Tests

| Test class | Kind | Direct target | Main method groups | Location |
|---|---|---|---|---|
| `ReparacaoInternaDomainTests` | unit | `InternalRepairRecord`, `InternalRepairToolTypeCodec`, `InternalRepairRules`, `InternalRepairContext` | `Create_*` (valid+context, no-context no-hard-block, `Create_NonCMorMFType_IsRejected`, structural invalid, no operator, server-side operator/time), `CreateCorrection_*` (preserves original, of-a-correction fails), `Rules_*` (never blocks, number-in-context-lot match/mismatch) | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaDomainTests.cs` |
| `ReparacaoInternaServiceTests` | unit | `ReparacaoInternaService` | `Register_*` (single context, repeated numbers, no-active-context, ambiguous, **BQ rejected as repair type**, full-reference context-only suffix, outside-lot-scope no-hard-block, save-failure, without-module fail-closed), `ListLineCards_*`, `Corrigir_*` (with capability, without capability forbidden, overriding context, line-changed auto-recalibrate, line-changed-to-no-production clean null context), `ListHistory_*` (latest-valid per chain + only-corrected, **uses persisted context not live re-resolution**), `GetDetail_ReturnsChain` (fakes in-memory) | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaServiceTests.cs` |
| `ReparacaoInternaProductionProjectionTests` | unit | `ReparacaoInternaProductionProjection` | `ActivationUtc_Is0920Local_OnTheStartDate`, `SelectEffective_*` (most-recent supersedes no-end-date, none activated → null, line-scoped, ignores non-active states) | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaProductionProjectionTests.cs` |
| `ReparacaoInternaWebApiTests` | integration (WebApplicationFactory) | `/api/reparacao-interna/*` endpoints + authorization guards | `Anonymous_IsDenied_RedirectsToLogin` (theory over paths), `AuthorizedRepIntUser_LineCards_IsAdmitted`, **`ActiveSurface_ExposesOnlyCmMf_AndApiRejectsBq`**, **`ProductionContext_IsReadOnly_InUiAndApi`**, `UserWithoutRepIntModule_IsDenied`, `Correcao_WithoutCorrigirCapability_IsForbidden` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\ReparacaoInterna\ReparacaoInternaWebApiTests.cs` |

Test data tool-type values used: `CM`, `MF`; BQ rejected via cast `(InternalRepairToolType)99` in service/domain tests and/or explicit BQ input in the CM/MF-only surface test; full reference `5447T173` preserved as context.

## 13. Test Doubles / Helpers

Dedicated test support files:
- `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\ReparacaoInternaTestSupport.cs` — `ReparacaoInternaFixedClock` (IClock), `ReparacaoInternaFakeAuthorship` (IPersistenceAuthorshipAccessor), `FakeReparacaoInternaUowFactory`, `FakeReparacaoInternaUnitOfWork`, `ReparacaoInternaCurrentUser` (module + corrigir capability), `FakeReparacaoInternaRepository` (IReparacaoInternaRepository, `FailInsert` flag), `FakeJobOnActiveContextLookup` (IJobOnActiveContextLookup; `SeedSingle/SeedNone/SeedAmbiguous`, `Context` builder), `FakeFerramentasPieceLookup` (IFerramentasPieceLookup; `Seed` CM/MF pieces by type + number + parent lot).

Nested in `ReparacaoInternaWebApiTests.cs` (integration support, in-file): `ReparacaoInternaWebApiTests.RepIntFixture`, `FakeAuthAdapter`, `FakeRepIntRepo`, `FakeContextLookup`, `FakePieceLookup`, `FakeUowFactory`, `FakeUow`, `FakeIdentityRepository`, `ValidRepIntUser`, `UserWithoutRepInt`.

## 14. Direct Reparação Interna References

One edge per relationship.

- `ReparacaoInternaService` → `IReparacaoInternaRepository` (constructor dependency)
- `ReparacaoInternaService` → `IJobOnActiveContextLookup` (constructor dependency)
- `ReparacaoInternaService` → `IFerramentasPieceLookup` (constructor dependency, external; `ResolveEffectiveLotIdAsync` uses `SearchAsync` + `ToolLoteId`)
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
| `ReparacaoInternaService.ListLineCardsAsync` | `boquilhas.js` consumer (line 412) | shared static consumer |
| `repair_events` scope `'interna'` | `repair_exit_item_id` FK + scope `'externa'` (Reparação Externa) | shared repair table dependency |
| `DapperReparacaoInternaRepository` | `audit_events` (global) | constructor-free query/app write |
| `ReparacaoInternaWebApiTests` | `IFerramentasPieceLookup`, `IRepairUnitOfWorkFactory`, `IJobOnActiveContextLookup` fakes | test target/support |
| `reparacao_interna` module id | `Pages\Historia\Index.cshtml` `ModuleLabel` switch: `"reparacao_interna" => "Reparação Interna"` (line 77) | shared Web consumer (module label mapping) |
| `internal_repair_records` / `repair_events` | `N12_rls.sql` RLS (shared with Reparação Externa tables in the same arrays) | DB policy dependency |

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
| `internal_repair_records` | Database | `database\migrations\N08_reparacoes.sql`, `N22_reparacao_interna_context.sql`, `N28_reparacao_interna_cm_mf_only.sql` |
| `/reparacao-interna` page + `/api/reparacao-interna/*` | Web | `src\BA.Dmo.Web\Pages\ReparacaoInterna\`, `src\BA.Dmo.Web\Program.cs` |
| `ModulePolicies.ReparacaoInterna` | Web (authorization) | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| `reparacao-interna.js` / `reparacao-interna-layout.css` | Static assets | `src\BA.Dmo.Web\wwwroot\` |
| RI tests | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\ReparacaoInterna\` |

## 17. Sources Verified

- `src\BA.Dmo.Domain\Modules\ReparacaoInterna\` (6 files)
- `src\BA.Dmo.Application\Modules\ReparacaoInterna\` (5 files)
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperReparacaoInternaRepository.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperJobOnActiveContextLookup.cs`
- `src\BA.Dmo.Web\Program.cs` (DI lines 239–242; RI endpoints lines 1081–1170; `/api/boquilhas/production-context` line 1455; `ParseInternalToolType` lines 1081–1087)
- `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`
- `src\BA.Dmo.Web\Pages\ReparacaoInterna\Index.cshtml` / `Index.cshtml.cs`
- `src\BA.Dmo.Web\wwwroot\scripts\reparacao-interna.js`
- `src\BA.Dmo.Web\wwwroot\styles\modules\reparacao-interna-layout.css`
- `src\BA.Dmo.Web\wwwroot\scripts\boquilhas.js` (production-context consumer, line 412)
- `database\migrations\N08_reparacoes.sql`, `N12_rls.sql`, `N22_reparacao_interna_context.sql`, `N28_reparacao_interna_cm_mf_only.sql`, `N25_remediation.sql`, `N05_jobon.sql` (`job_on_revision`, `job_on_component` family CHECK), `database\consolidated_clean_install.sql` (final CM/MF-only CHECK + N22 columns + RLS arrays)
- `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoInterna\` (4 files)
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\ReparacaoInterna\ReparacaoInternaWebApiTests.cs`

## 18. Findings / NEEDS REVIEW

Reconciliation findings (evidence-grounded; NO source changes made; no deletion recommended):

- **R1 — INTENTIONAL NORMALIZATION (recorded, sequence N22 → N28)**: the DB tool-type CHECK was widened to `('CM','MF','BQ')` by N22 (owner decision R009) and later re-narrowed by N28 back to `('CM','MF')` (`NOT VALID` + `VALIDATE`, fail-closed guard `RAISE EXCEPTION` on any non-CM/MF row — `database\migrations\N28_reparacao_interna_cm_mf_only.sql`). This converges the DB with the Domain/Application/Web rule (N28 header comment). `database\consolidated_clean_install.sql` (line 853) embodies the final CM/MF-only state. No action — recorded for traceability.
- **R2 — STALE SOURCE COMMENTS — NEEDS REVIEW (documentation drift, not functional)**: `InternalRepairToolType.cs` class doc still cites "N22 CHECK `tool_type IN ('CM','MF','BQ')`" and `InternalRepairRecord.cs` member doc says "Tool type CM/MF/BQ" (line 50). Runtime behavior is CM/MF-only everywhere (enum, codec throw, `Create`/`CreateCorrection` guards, service guard, `ParseInternalToolType`, N28 CHECK). Comments predate the N28 convergence.
- **R3 — CONFIRMED CURRENT (CM/MF-only matrix)**: the recordable-type rule is consistent across `InternalRepairToolType` enum + codec, `InternalRepairRecord.Create`/`CreateCorrection` guards, `ReparacaoInternaService.RegistrarReparacoesAsync` guard, web `ParseInternalToolType`, N28 DB CHECK, and integration test `ActiveSurface_ExposesOnlyCmMf_AndApiRejectsBq`. The CSHTML surfaces no longer contain BQ buttons/options (the N22-era `data-type="BQ"` static button and `<option value="BQ">` were removed).
- **R4 — ORPHAN CANDIDATE — NEEDS AUDIT**: `InternalRepairRules` (`EvalCollectibleWhen`, `NumberInContextLot`, `ContextMismatchInfoCode`, `NoActiveContextInfoCode`) has NO caller in `src\` outside the file itself (grep: only the declaration file matches); its members are exercised by unit tests only. The service implements the no-hard-block behaviour inline. UNKNOWN / OWNER DECISION REQUIRED.
- **R5 — UNKNOWN / OWNER DECISION REQUIRED (low)**: `ReparacaoInternaService.RegisterReparacaoAsync` is a back-compat single-entry alias delegating to `RegistrarReparacoesAsync`; no `Program.cs` route references it (`POST /api/reparacao-interna` binds `RegistrarReparacoesAsync`). Keep or drop is an owner decision.
- **R6 — CONFIRMED CURRENT (cross-module note)**: the Job On active-context dependency (`IJobOnActiveContextLookup` → `DapperJobOnActiveContextLookup` → `IJobOnRepository` + `job_on_revision` + `job_on_component` families `MP_CM/MF/BQ`) is read-only and never writes Job On; N25 does not modify RI-specific objects but does add append-only triggers to the `job_on_revision`/`job_on_component` family (Job On slice, cross-referenced via `06_JOB_ON.md`).

## Counts

- Domain Reparação Interna files: **6**
- Application Reparação Interna files: **5**
- Infrastructure Reparação Interna files: **2**
- Dedicated Web page files: **2** (`Index.cshtml`, `Index.cshtml.cs`)
- Dedicated static asset files: **2** (`reparacao-interna.js`, `reparacao-interna-layout.css`)
- Shared Web wiring files: **2** (`Program.cs`, `ModuleAuthorizationHandler.cs`)
- Shared static asset files carrying RI rules: **1** (`wwwroot\scripts\boquilhas.js`)
- Shared Application catalog files: **2** (`src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`)
- Reparação Interna DB objects: 1 RI-specific table + 3 indexes + 0 triggers = **4** (on `internal_repair_records`; shared `repair_events` counted as shared dependency, not RI-specific)
- Reparação Interna migration touchpoints: **4 distinct migration files** (`N08_reparacoes.sql`, `N12_rls.sql`, `N22_reparacao_interna_context.sql`, `N28_reparacao_interna_cm_mf_only.sql`)
- Reparação Interna test classes: **4**
- Reparação Interna test support files: **1** dedicated (`ReparacaoInternaTestSupport.cs`) + 1 in-file integration fixture (`ReparacaoInternaWebApiTests.cs`)