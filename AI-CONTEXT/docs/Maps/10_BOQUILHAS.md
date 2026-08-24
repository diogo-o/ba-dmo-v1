# BA DMO — Boquilhas Technical Map

MAP ID: MAP-10
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
- 14. Direct Boquilhas References
- 15. External Technical References
- 16. Target-to-Layer Index
- 17. Sources Verified
- Counts

## 1. Scope

Technical inventory and navigation of Boquilhas-specific objects across Domain, Application, Infrastructure, Database, Web/static assets and Tests, grounded in current source. Cross-layer navigation only; no end-to-end flow, no business-rule interpretation. Design/SOT not used as evidence.

## 2. Layer Summary

| Layer | Main Boquilhas Objects | Locations |
|---|---|---|
| Domain | `BqLote`, `BqTrace`, `BqMovement`, `BqSaldos`, `BqDiscrepancy`, `BqRepairer`, `BoquilhasModuleCatalog`, `BqRules`, `BqInventoryCalculator`, enums + codecs | `src\BA.Dmo.Domain\Modules\Boquilhas\` |
| Application | `BoquilhasService`, `BqAuthorizationGate`, `BqRequests`, `IBoquilhasRepository`, `IBoquilhasUnitOfWorkFactory` | `src\BA.Dmo.Application\Modules\Boquilhas\` |
| Infrastructure | `DapperBoquilhasRepository`, `DapperBoquilhasUnitOfWorkFactory` | `src\BA.Dmo.Infrastructure\Access\` |
| Database | `bq_lotes`, `bq_traces`, `bq_movements`, `bq_discrepancies`, `bq_lifecycle_history`, `bq_utilisation_readings` + indexes + triggers | `database\migrations\N03_bq.sql`, `N18_bq_repairer.sql`, `N25_remediation.sql` |
| Web | `/boquilhas` Razor page; `/api/boquilhas/*` endpoints | `src\BA.Dmo.Web\Pages\Boquilhas\`, `src\BA.Dmo.Web\Program.cs` |
| Static assets | `boquilhas.js` | `src\BA.Dmo.Web\wwwroot\scripts\boquilhas.js` |
| Tests | `BoquilhasServiceTests`, `BqInventoryCalculatorTests`, `BqAuthorizationGateTests`, `BoquilhasWebAuthorizationTests` | `tests\BA.Dmo.UnitTests\Modules\Boquilhas\`, `tests\BA.Dmo.IntegrationTests\Access\` |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | YES | `src\BA.Dmo.Domain\Modules\Boquilhas\` |
| Application | YES | `src\BA.Dmo.Application\Modules\Boquilhas\` |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\DapperBoquilhasRepository.cs`, `DapperBoquilhasUnitOfWorkFactory.cs` |
| Web | YES | `src\BA.Dmo.Web\Pages\Boquilhas\`; `src\BA.Dmo.Web\Program.cs`; `Authorization\ModuleAuthorizationHandler.cs` |
| Database | YES | `database\migrations\N03_bq.sql`, `N12_rls.sql`, `N18_bq_repairer.sql`, `N25_remediation.sql` |
| Tests | YES | `tests\BA.Dmo.UnitTests\Modules\Boquilhas\`, `tests\BA.Dmo.IntegrationTests\Access\` |

This is technical navigation only; it does not explain workflow.

## 3. Domain Objects

All under `src\BA.Dmo.Domain\Modules\Boquilhas\`.

| Type | Kind | Key members | Notes |
|---|---|---|---|
| `BoquilhasModuleCatalog` | static catalog | `ModuleId="boquilhas"`; `Lines` = B1/C1/C2/C3/B2/B3; `ReferencePattern` = `^[A-Z][0-9]{3}$`; `CanonicalPageSizes` = {20,40,60}; `ReferenceInvalidCode="BQ_REFERENCE_INVALID"` | `BoquilhasModuleCatalog.cs` |
| `BqLote` | entity | `BqLoteId`, `Reference`, `BatchCode`, `AllowedLines`, `LifecycleState`, `CreatedBy`, `CreatedAtUtc`, `UpdatedAtUtc` | `BqLote.cs`; maps `bq_lotes` |
| `BqCloseSnapshot` | record | `BqLoteId`, `Reference`, `BatchCode`, `Purpose`, `StartLine`, `AllowedLines`, `SaldosJson`, `MovementCount`, `Reason`, `ClosedBy`, `ClosedAtUtc` | `BqLote.cs`; immutable close snapshot |
| `BqLifecycleState` + `BqLifecycleStateCodec` | enum + codec | `Available/Archived/Scrapped` ←→ `available/archived/scrapped` | `BqLifecycleState.cs` |
| `BqTrace` | entity | `BqTraceId`, `BqLoteId`, `Status`, `Purpose`, `StartLine`, `SapStart`, `SapEnd`, `ReopenHistory`, `DeletedMovements`, `CreatedBy`, `CreatedAtUtc`, `UpdatedAtUtc` | `BqTrace.cs`; maps `bq_traces` |
| `BqTraceStatus` + codec | enum + codec | `Active/Closed` ←→ `active/closed` | `BqTrace.cs` |
| `BqTracePurpose` + codec | enum + codec | `Production/Repair` ←→ `production/repair` | `BqTrace.cs` |
| `BqMovement` | entity | `BqMovementId`, `BqTraceId`, `MovementType`, `Qty`, `ExceptionalReceivedQty`, `Line`, `RepairerId`, `Notes`, `OccurredAtUtc`, `ActorId` | `BqMovementType.cs`; maps `bq_movements` |
| `BqMovementType` + codec | enum + codec | `Inicio/Saida/Entrada/Irreparavel/Linha/Contagem/Fim` ←→ `inicio/saida/entrada/irreparavel/linha/contagem/fim` | `BqMovementType.cs` |
| `BqSaldos` | entity/value | `Prod`, `Repair`, `Irreparable`, `ExceptionalReceived`, `TransactionalBalance`, `PhysicalInventory` (computed), `Clone()` | `BqSaldos.cs` |
| `BqDiscrepancy` | entity | `BqDiscrepancyId`, `BqLoteId`, `BqTraceId`, `ExpectedQty`, `ActualQty`, `ExcessQty`, `Status`, `ResolutionNote`, `ResolvedBy`, `ResolvedAtUtc`, `CreatedBy`, `CreatedAtUtc` | `BqDiscrepancy.cs`; maps `bq_discrepancies` |
| `BqDiscrepancyStatus` + codec | enum + codec | `Open/UnderReview/Resolved` ←→ `open/under_review/resolved` | `BqDiscrepancy.cs` |
| `BqLifecycleEvent` + `BqLifecycleEventKind` + codec | entity + enum + codec | `Archived/Scrapped/Restored/Retired` ←→ `archived/scrapped/restored/retired`; `BqLoteId`, `Reason`, `ActorId`, `OccurredAtUtc` | `BqDiscrepancy.cs`; maps `bq_lifecycle_history` |
| `BqUtilisationReading` + `BqUtilisationReadingKind` + codec | entity + enum + codec | `Initial/Final` ←→ `initial/final`; `BqUtilisationReadingId`, `BqTraceId`, `Value`, `ActorId`, `OccurredAtUtc` | `BqUtilisationReading.cs`; maps `bq_utilisation_readings` |
| `BqRepairer` | entity | `RepairerId`, `Name`, `Active`, `SupportedTypes`, `CreatedBy`, `CreatedAtUtc`, `UpdatedAtUtc` | `BqRepairer.cs`; persists via canonical `repairers` |
| `BqLineRepairerDefault` | entity | `Line`, `DefaultRepairerId`, `AllowedRepairerIds` | `BqRepairer.cs`; persists via `line_repairer_defaults` |
| `BqRules` | static rules | error codes `BQ_*`; `ValidateQuantity`, `ValidateUtilisation`, `IsValidReference` | `BqRules.cs` |
| `BqInventoryCalculator` | static calculator | `ReturnReconciliation` struct; `ReconcileReturn`, `Apply` | `BqRules.cs` |

## 4. Application Objects

All under `src\BA.Dmo.Application\Modules\Boquilhas\`.

| Object | Kind | Public methods (Service) | Constructor deps |
|---|---|---|---|
| `BoquilhasService` | service | `CreateLoteWithTraceAsync`, `RegisterMovementAsync`, `GetLotSummaryAsync`, `ListMovementsAsync`, `ListLotesAsync`, `CloseTraceAsync`, `ReopenTraceAsync`, `EditLoteAsync`, `ApplyLifecycleAsync`, `ListDiscrepanciesAsync`, `ResolveDiscrepancyAsync`, `ListRepairersAsync`, `CreateRepairerAsync`, `UpdateRepairerAsync`, `SetLineRepairerDefaultAsync` | `IBoquilhasRepository`, `IBoquilhasUnitOfWorkFactory`, `BqAuthorizationGate`, `IClock` |
| `BqAuthorizationGate` | gate | `Require()` → `Result<BqExecutor, DomainError>` | `ICurrentUserAccessor`, `IPersistenceAuthorshipAccessor` |
| `BqExecutor` | record | `ActorId`, `DisplayName` | — |

Request/DTO records (`BqRequests.cs`): requests `CreateBqLoteRequest`, `RegisterBqMovementRequest`, `CloseBqTraceRequest`, `ReopenBqTraceRequest`, `EditBqLoteRequest`, `BqLifecycleRequest`, `ResolveBqDiscrepancyRequest`, `VoidBqMovementRequest`, `CreateBqRepairerRequest`, `UpdateBqRepairerRequest`, `SetLineRepairerDefaultRequest`; DTOs `BqLoteDto`, `BqMovementRowDto`, `BqSaldosDto`, `BqDiscrepancyDto`, `BqRepairerDto`, `BqLineRepairerDefaultDto`, `BqTraceDto`, `BqLotSummaryDto`.

`BoquilhasService` private helpers: `ComputeSaldos`, `MapLote`, `MapSaldos`, `SerializeSaldos`, `ToCanonicalLine`, `ToCanonicalLineOrNull`, `NormalizeNull`, `AuditActionFor`, `NotFound`, `EnrichMovementRowsAsync`. Audit action codes written: `boquilhas.lote.criar`, `boquilhas.lote.editar`, `boquilhas.trace.fechar`, `boquilhas.trace.reabrir`, `boquilhas.discrepancia.resolver`, `boquilhas.lote.{kind}`, `boquilhas.movimento.*` (saida/entrada/irreparavel/linha/contagem/default).

## 5. Application Contracts / Ports

| Interface | Methods (main) | Path | Implementation |
|---|---|---|---|
| `IBoquilhasRepository` | Lotes: `GetLoteByIdAsync`, `GetLoteByReferenceBatchAsync`, `ListLotesAsync`, `CountLotesAsync`, `CreateLoteAsync`, `UpdateLoteAsync`, `UpdateLifecycleStateAsync`, `InsertLifecycleEventAsync`; Traces: `GetTraceByIdAsync`, `GetActiveTraceForLoteAsync`, `GetLastClosedOrActiveTraceAsync`, `GetTraceForMovementAsync`, `CreateTraceAsync`, `CloseTraceAsync`, `ReopenTraceAsync`, `AppendReopenHistoryAsync`; Movements: `InsertMovementAsync`, `ListMovementsForTraceAsync`, `ListMovementsByLoteAsync`, `ListMovementsAsync`, `CountMovementsAsync`, `VoidMovementAsync`, `ListVoidedMovementIdsAsync`; Utilisation: `InsertUtilisationReadingAsync`, `GetUtilisationReadingAsync`; Discrepancies: `GetOpenDiscrepancyForTraceAsync`, `InsertDiscrepancyAsync`, `UpdateDiscrepancyAsync`, `ListDiscrepanciesAsync`; Repairers: `ListRepairersAsync`, `GetRepairerByIdAsync`, `CreateRepairerAsync`, `UpdateRepairerAsync`, `GetLineRepairerDefaultAsync`, `SetLineRepairerDefaultAsync`; `InsertAuditEventAsync` | `src\BA.Dmo.Application\Modules\Boquilhas\IBoquilhasRepository.cs` | `DapperBoquilhasRepository` |
| `IBoquilhasUnitOfWorkFactory` | `BeginAsync` | `src\BA.Dmo.Application\Modules\Boquilhas\IBoquilhasUnitOfWorkFactory.cs` | `DapperBoquilhasUnitOfWorkFactory` |

Filter records: `BqLoteFilter` (Search, OnlyAvailable, LifecycleState, Page, PageSize), `BqHistoryFilter` (BqLoteId, Search, MovementType, RepairerId, From, To, Page, PageSize) — in `IBoquilhasRepository.cs`.

## 6. Authorization / Catalog Objects

| Identifier | Value | Source |
|---|---|---|
| Module id | `BoquilhasModuleCatalog.ModuleId = "boquilhas"` | `src\BA.Dmo.Domain\Modules\Boquilhas\BoquilhasModuleCatalog.cs` |
| Canonical module | `CanonicalModuleCatalog.BoquilhasModuleId = "boquilhas"`; `ModuleDefinition(BoquilhasModuleId, "Boquilhas", Module, 10, "/boquilhas")` (no capabilities) | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` entries 18, 73, 98 |
| Canonical page | `CanonicalPageCatalog.BoquilhasRegistoPageId = "boquilhas.registo"`; `PageDefinition(..., "/boquilhas", requiredCapabilityId: null, displayOrder: 10)` | `src\BA.Dmo.Application\Shared\Access\CanonicalPageCatalog.cs` entries 13, 38 |
| Web policy | `ModulePolicies.Boquilhas = "BaDmo.Module.boquilhas"` | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` line 54 |
| Policy registration | `options.AddPolicy(ModulePolicies.Prefix + moduleId, ...)` for each canonical module | `src\BA.Dmo.Web\Program.cs` line 115 |
| Route guard | Razor `[Authorize(Policy = ModulePolicies.Boquilhas)]` | `src\BA.Dmo.Web\Pages\Boquilhas\Index.cshtml` line 5 |
| Server gate | `BqAuthorizationGate.Require()` returns `BQ_FORBIDDEN` when module not granted | `src\BA.Dmo.Application\Modules\Boquilhas\BqAuthorizationGate.cs` |

No Boquilhas-specific capability ids are declared in the current canonical module definition. `BqAuthorizationGate.Require()` checks the `boquilhas` module grant.

## 7. Infrastructure Objects

All under `src\BA.Dmo.Infrastructure\Access\`.

| Class | Implements | Constructor deps | Purpose / storage |
|---|---|---|---|
| `DapperBoquilhasRepository` | `IBoquilhasRepository` | `IDbConnectionFactory` | Dapper persistence over `bq_*`; read queries self-manage a connection; multi-row writes run on the shared `IDbUnitOfWork`. DB objects referenced: `bq_lotes`, `bq_traces`, `bq_movements`, `bq_discrepancies`, `bq_lifecycle_history`, `bq_utilisation_readings`, `repairers`, `repairer_repair_types`, `line_repairer_defaults`, `audit_events` |
| `DapperBoquilhasUnitOfWorkFactory` | `IBoquilhasUnitOfWorkFactory` | `IDbConnectionFactory` | Wraps `DapperUnitOfWork.BeginAsync` for atomic BQ writes |

Key Dapper behavior: `bq_movements`/`bq_lifecycle_history` append-only (DB triggers); `noted_repairer_id` column maps `BqMovement.RepairerId`; void recorded in `bq_traces.deleted_movements` JSONB; `reopen_history` JSONB appended via `jsonb_build_object`; `audit_events` insert hard-codes `module_id = 'boquilhas'`. Line default for `tool_type='BQ'` in `line_repairer_defaults`; a NULL default deletes that row. Hydration helpers: `MapLote`, `MapTrace`, `MapMovement`, `MapDiscrepancy`, `MapRepairerWithTypes`, `MapRepairer`, `ParseGuidJsonArray`, `LikePattern`.

## 8. Database Objects

Source: `database\migrations\N03_bq.sql` (tables/indexes/triggers), `N18_bq_repairer.sql` (column), `N25_remediation.sql` (partial index), `N12_rls.sql` (RLS/policy). Also mirrored in `database\consolidated_clean_install.sql`.

| Object | Kind | Storage | PK / constraints | FK |
|---|---|---|---|---|
| `bq_lotes` | table | master lot identity | PK `bq_lote_id`; `uq_bq_lotes_reference_batch` UNIQUE(reference,batch_code); `ck_bq_lotes_reference` (`^[A-Z][0-9]{3}$`); `ck_bq_lotes_lifecycle` (available/archived/scrapped) | `created_by` → `internal_users(actor_id)` |
| `bq_traces` | table | trace per lot | PK `bq_trace_id`; `ck_bq_traces_status` (active/closed); `ck_bq_traces_purpose` (production/repair); `ck_bq_traces_sap_start` / `_sap_end` (0–100) | `bq_lote_id` → `bq_lotes`; `created_by` → `internal_users` |
| `bq_movements` | table | append-only movement facts | PK `bq_movement_id`; `ck_bq_movements_type` (inicio/saida/entrada/irreparavel/linha/contagem/fim); `ck_bq_movements_qty`; `ck_bq_movements_exceptional` | `bq_trace_id` → `bq_traces`; `noted_repairer_id` → `repairers` (N18); `actor_id` → `internal_users` |
| `bq_discrepancies` | table | return-excess record | PK `bq_discrepancy_id`; `ck_bq_discrepancies_status` (open/under_review/resolved) | `bq_lote_id` → `bq_lotes`; `bq_trace_id` → `bq_traces`; `resolved_by`/`created_by` → `internal_users` |
| `bq_lifecycle_history` | table | lifecycle audit events | PK `bq_lifecycle_history_id`; `ck_bq_lifecycle_history_event` (archived/scrapped/restored/retired) | `bq_lote_id` → `bq_lotes`; `actor_id` → `internal_users` |
| `bq_utilisation_readings` | table | manual utilisation readings | PK `bq_utilisation_reading_id`; `ck_bq_utilisation_readings_kind` (initial/final); `ck_bq_utilisation_readings_value` (0–100) | `bq_trace_id` → `bq_traces`; `actor_id` → `internal_users` |

Indexes:
- `ix_bq_lotes_lifecycle` on `bq_lotes(lifecycle_state)`
- `ix_bq_traces_lote` on `bq_traces(bq_lote_id)`
- `ix_bq_traces_status` on `bq_traces(status)`
- `ix_bq_movements_trace` on `bq_movements(bq_trace_id)`
- `ix_bq_movements_occurred` on `bq_movements(occurred_at_utc)`
- `ix_bq_discrepancies_lote` on `bq_discrepancies(bq_lote_id)`
- `ix_bq_discrepancies_status` on `bq_discrepancies(status)`
- `ix_bq_lifecycle_history_lote` on `bq_lifecycle_history(bq_lote_id)`
- `ix_bq_utilisation_readings_trace` on `bq_utilisation_readings(bq_trace_id)`
- `uq_bq_traces_active` partial UNIQUE on `bq_traces(bq_lote_id)` `WHERE status = 'active'` (N25)

Triggers:
- `trg_bq_movements_append_only` = `ba_dmo_guard_append_only()` on `bq_movements`
- `trg_bq_lifecycle_history_append_only` = `ba_dmo_guard_append_only()` on `bq_lifecycle_history`
- `trg_bq_utilisation_readings_append_only` = `ba_dmo_guard_append_only()` on `bq_utilisation_readings`

`bq_*` tables are not referenced by RLS/policy/GRANT statements in N03, N18 or N25; `database\migrations\N12_rls.sql` explicitly references all six `bq_*` tables — it enables RLS on them (DO-loop `rls_tables` array) and creates the single technical `ba_dmo_app_access` policy for `ba_dmo_app` on each (`policy_tables` array).

## 9. Migration Touchpoints

| Migration | Boquilhas Object(s) | Technical Change |
|---|---|---|
| `database\migrations\N03_bq.sql` | `bq_lotes`, `bq_traces`, `bq_movements`, `bq_discrepancies`, `bq_lifecycle_history`, `bq_utilisation_readings` | CREATE TABLE (all six); 9 non-unique `CREATE INDEX IF NOT EXISTS ix_*`; DROP/CREATE 3 append-only triggers; UNIQUE/CHECK constraints |
| `database\migrations\N12_rls.sql` | `bq_lotes`, `bq_traces`, `bq_movements`, `bq_discrepancies`, `bq_lifecycle_history`, `bq_utilisation_readings` | DO loop `ENABLE ROW LEVEL SECURITY` on each `bq_*` (`rls_tables` array); DO loop `DROP POLICY IF EXISTS ba_dmo_app_access` + `CREATE POLICY ba_dmo_app_access FOR ALL TO ba_dmo_app USING (true) WITH CHECK (true)` on each `bq_*` (`policy_tables` array) |
| `database\migrations\N18_bq_repairer.sql` | `bq_movements` | `ALTER TABLE ... ADD COLUMN IF NOT EXISTS noted_repairer_id uuid NULL REFERENCES repairers(repairer_id)` |
| `database\migrations\N25_remediation.sql` | `bq_traces` | §1.4 `CREATE UNIQUE INDEX IF NOT EXISTS uq_bq_traces_active ON bq_traces (bq_lote_id) WHERE status = 'active'` |

`database\consolidated_clean_install.sql` is a separate consolidated-schema build file. It mirrors N03 tables/indexes/triggers, the N18 `noted_repairer_id` FK (`bq_movements_noted_repairer_id_fkey`), the N25 `uq_bq_traces_active` partial index, and the N12 RLS/policy/GRANT for the `bq_*` tables (its own "§12 RLS + security contract" section, `rls_tables`/`policy_tables` arrays); the RLS/GRANT statements originate from N12, not from N03/N18/N25.

## 10. Web / Routes

| Route | Technical Entry Point | Authorization | File |
|---|---|---|---|
| `/boquilhas` | `IndexModel.OnGet` (Razor page) | `ModulePolicies.Boquilhas` | `src\BA.Dmo.Web\Pages\Boquilhas\Index.cshtml` + `Index.cshtml.cs` |
| `GET /api/boquilhas/production-context` | `ReparacaoInternaService.ListLineCardsAsync` | `ModulePolicies.Boquilhas` | `src\BA.Dmo.Web\Program.cs` line 1445 |
| `POST /api/boquilhas/lotes` | `BoquilhasService.CreateLoteWithTraceAsync` | `ModulePolicies.Boquilhas` | line 1454 |
| `GET /api/boquilhas/lotes` | `BoquilhasService.ListLotesAsync` | `ModulePolicies.Boquilhas` | line 1463 |
| `GET /api/boquilhas/lotes/{lotId:guid}` | `BoquilhasService.GetLotSummaryAsync` | `ModulePolicies.Boquilhas` | line 1482 |
| `POST /api/boquilhas/movements` | `BoquilhasService.RegisterMovementAsync` | `ModulePolicies.Boquilhas` | line 1491 |
| `GET /api/boquilhas/movements` | `BoquilhasService.ListMovementsAsync` | `ModulePolicies.Boquilhas` | line 1500 |
| `POST /api/boquilhas/traces/{traceId:guid}/close` | `BoquilhasService.CloseTraceAsync` | `ModulePolicies.Boquilhas` | line 1517 |
| `POST /api/boquilhas/traces/{traceId:guid}/reopen` | `BoquilhasService.ReopenTraceAsync` | `ModulePolicies.Boquilhas` | line 1526 |
| `PUT /api/boquilhas/lotes/{lotId:guid}` | `BoquilhasService.EditLoteAsync` | `ModulePolicies.Boquilhas` | line 1535 |
| `POST /api/boquilhas/lotes/{lotId:guid}/lifecycle` | `BoquilhasService.ApplyLifecycleAsync` | `ModulePolicies.Boquilhas` | line 1544 |
| `GET /api/boquilhas/discrepancies` | `BoquilhasService.ListDiscrepanciesAsync` | `ModulePolicies.Boquilhas` | line 1553 |
| `POST /api/boquilhas/discrepancies/{discrepancyId:guid}/resolve` | `BoquilhasService.ResolveDiscrepancyAsync` | `ModulePolicies.Boquilhas` | line 1561 |
| `GET /api/boquilhas/repairers` | `BoquilhasService.ListRepairersAsync` | `ModulePolicies.Boquilhas` | line 1571 |
| `POST /api/boquilhas/repairers` | `BoquilhasService.CreateRepairerAsync` | `ModulePolicies.Boquilhas` | line 1579 |
| `PUT /api/boquilhas/repairers/{repairerId:guid}` | `BoquilhasService.UpdateRepairerAsync` | `ModulePolicies.Boquilhas` | line 1587 |
| `POST /api/boquilhas/lines` | `BoquilhasService.SetLineRepairerDefaultAsync` | `ModulePolicies.Boquilhas` | line 1596 |

DI wiring (all `AddScoped`) in `src\BA.Dmo.Web\Program.cs` lines 266–269: `IBoquilhasRepository → DapperBoquilhasRepository`, `IBoquilhasUnitOfWorkFactory → DapperBoquilhasUnitOfWorkFactory`, `BqAuthorizationGate`, `BoquilhasService`.

Razor page `Index.cshtml` renders tabs Registo / Boquilhas / Histórico / Definições and a fixed B1–C3 line side panel; loads `boquilhas.js`; no page-local stylesheet.

## 11. Static Assets

| File | Location | Principal functions | API routes called |
|---|---|---|---|
| `boquilhas.js` | `src\BA.Dmo.Web\wwwroot\scripts\boquilhas.js` | wiring only; tab switching; `loadLinePanel`, `loadSearch`, `openLot`, `loadLotMovements`, `loadDiscrepancies`, `resolveDiscrepancy`, `openMovementModal`, `closeTrace`, `loadBoquilhasCards`, `loadHistory`, `loadDefinicoes`, `setRepairerActive`, modal/toast helpers | `GET/POST /api/boquilhas/*` (production-context, lotes, movements, traces{}, discrepancies{}, repairers) |

Selectors: `#linePanel`, `#searchLot`, `#searchResults`, `#createPanel`, `#lotResumo`, `#bqWarnings`, `#bqMovements`, `#boquilhasCards`, `#hTable/#hBody`, `#repairerList`, `.boquilhas-tabs`, `.boquilhas-view`, `[data-open-lot]`, `[data-act]`, `[data-resolve]`.

Shared static assets carrying Boquilhas-specific rules: `src\BA.Dmo.Web\wwwroot\styles\dmo-components.css` (`.boquilhas-*` classes); `src\BA.Dmo.Web\wwwroot\styles\dmo-tokens.css` (sidebar/line-card tokens + reference comments). No dedicated Boquilhas stylesheet exists under `wwwroot\styles\modules\`.

## 12. Tests

| Test class | Kind | Direct target | Main groups |
|---|---|---|---|
| `BoquilhasServiceTests` | Unit | `BoquilhasService` | atomic create (lot+trace+START), duplicate-lot block, reference validation, 20→25 excess-return (full acceptance + discrepancy), exact return no-discrepancy, dispatch exceeding production block, movement on closed trace block, close/reopen, lifecycle (active-trace block, archive), list enrichment (reference/lote/repairer/running saldo), search filters |
| `BqInventoryCalculatorTests` | Unit | `BqInventoryCalculator`, `BqSaldos`, `BqRules` | classify `ReconcileReturn` (20→25/exact/below), full lifecycle trace, dispatch/non-repairable block, line-change no-balance, physical inventory includes exceptional |
| `BqAuthorizationGateTests` | Unit | `BqAuthorizationGate` | `Require` with/without module (authorized/forbidden) |
| `BoquilhasWebAuthorizationTests` | Integration (Web) | `/boquilhas` page + `/api/boquilhas/*` | unauth redirect, without-module deny, with-module render, create-lot then 20→25 full return + discrepancy, dispatch exceeding production BadRequest |

Paths:
- `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BoquilhasServiceTests.cs`
- `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqInventoryCalculatorTests.cs`
- `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqAuthorizationGateTests.cs`
- `tests\BA.Dmo.IntegrationTests\Access\BoquilhasWebAuthorizationTests.cs`

## 13. Test Doubles / Helpers

| File | Helpers | Purpose |
|---|---|---|
| `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqTestSupport.cs` | `BqFixedClock` (IClock), `BqFakeAuthorship` (IPersistenceAuthorshipAccessor), `BqCurrentUser` (ICurrentUserAccessor, `Authorized()`/`WithoutModule()`), `FakeBqUnitOfWork`, `FakeBqUnitOfWorkFactory`, `FakeBoquilhasRepository` (+ `SeedLote`, `SeedActiveTrace`, `SeedRepairer`) | in-memory doubles for Boquilhas unit tests |
| `tests\BA.Dmo.IntegrationTests\Access\FakeBoquilhasWebRepository.cs` | `FakeBoquilhasWebRepository` (IBoquilhasRepository) with `Reset()` | in-memory repo for the Web integration fixture |

Nested in `BoquilhasWebAuthorizationTests.cs` (same file): `FakeBqWebUnitOfWorkFactory`, `FakeBqWebUnitOfWork`, `FakeAuthAdapter`, `FakeIdentityRepository`, `CreateLoteResponse`, `BoquilhasFixture`.

## 14. Direct Boquilhas References

One edge per relationship.

```
BoquilhasModuleCatalog.ModuleId = "boquilhas"   (Domain → Application catalog)
BoquilhasService                   → IBoquilhasRepository
BoquilhasService                   → IBoquilhasUnitOfWorkFactory
BoquilhasService                   → BqAuthorizationGate
BoquilhasService                   → BqRules
BoquilhasService                   → BqInventoryCalculator
BoquilhasService                   → IClock
BqAuthorizationGate                → ICurrentUserAccessor
BqAuthorizationGate                → IPersistenceAuthorshipAccessor
DapperBoquilhasRepository          → IBoquilhasRepository
DapperBoquilhasRepository          → bq_lotes
DapperBoquilhasRepository          → bq_traces
DapperBoquilhasRepository          → bq_movements
DapperBoquilhasRepository          → bq_discrepancies
DapperBoquilhasRepository          → bq_lifecycle_history
DapperBoquilhasRepository          → bq_utilisation_readings
DapperBoquilhasRepository          → audit_events
DapperBoquilhasRepository          → repairers
DapperBoquilhasRepository          → repairer_repair_types
DapperBoquilhasRepository          → line_repairer_defaults
DapperBoquilhasUnitOfWorkFactory   → IBoquilhasUnitOfWorkFactory
DapperBoquilhasUnitOfWorkFactory   → IDbConnectionFactory
bq_traces                          → bq_lotes          (FK bq_lote_id)
bq_movements                       → bq_traces         (FK bq_trace_id)
bq_movements                       → repairers         (FK noted_repairer_id)
bq_discrepancies                   → bq_lotes          (FK bq_lote_id)
bq_discrepancies                   → bq_traces         (FK bq_trace_id)
bq_lifecycle_history               → bq_lotes          (FK bq_lote_id)
bq_utilisation_readings            → bq_traces         (FK bq_trace_id)
uq_bq_lotes_reference_batch / ck_bq_lotes_reference → bq_lotes   (UNIQUE/CHECK)
uq_bq_traces_active                → bq_traces         (partial UNIQUE index)
/boquilhas (Razor)                 → ModulePolicies.Boquilhas
/api/boquilhas/*                   → BoquilhasService
/api/boquilhas/*                   → ModulePolicies.Boquilhas
/boquilhas page                    → boquilhas.js
boquilhas.js                       → /api/boquilhas/*
Program.cs (DI)                    → IBoquilhasRepository / DapperBoquilhasRepository
Program.cs (DI)                    → IBoquilhasUnitOfWorkFactory / DapperBoquilhasUnitOfWorkFactory
Program.cs (DI)                    → BoquilhasService
```

## 15. External Technical References

| Boquilhas Object | External Technical Reference | Reference Type |
|---|---|---|
| `bq_lotes.created_by`, `bq_traces.created_by`, `bq_movements.actor_id`, `bq_discrepancies.resolved_by`/`created_by`, `bq_lifecycle_history.actor_id`, `bq_utilisation_readings.actor_id` | `internal_users` (actor_id) | DB FK |
| `bq_movements.noted_repairer_id` (N18) | `repairers` (repairer_id) | DB FK |
| `DapperBoquilhasRepository` | `repairers`, `repairer_repair_types`, `line_repairer_defaults` (tool_type='BQ') | query join / identifier field |
| `DapperBoquilhasRepository.InsertAuditEventAsync` | `audit_events` (module_id='boquilhas') | query insert |
| `boquilhas.js`/`/api/boquilhas/production-context` → `ReparacaoInternaService.ListLineCardsAsync` | Reparação Interna service | route reference / application service |
| `jobon.js` (line 493) | `GET /api/boquilhas/production-context` | route call — External Technical Reference only (not counted as Boquilhas static asset or wiring) |
| `BoquilhasService` uses `BqHistoryFilter`/`BqLoteFilter` via `BqMovementTypeCodec` | Shared `IDbUnitOfWork`, `IDbConnectionFactory`, `IClock`, `Result`, `DomainError` | application port / shared kernel |
| `FerramentasToolType.BQ` | Ferramentas enum literal `"BQ"` | enum/reference reuse (generic Ferramentas, not Boquilhas) |
| `ba_dmo_guard_append_only()` | shared DB function (N01_identity.sql) | trigger function reuse |
| `CanonicalModuleCatalog.BoquilhasModuleId` referenced by `HistoriaModuleCatalog` (line 27) | História catalog | enum/module-id reuse |
| `/api/boquilhas/lotes/{lotId:guid}` `BqLifecycleState` literal mapping | `BqLifecycleState` / `BqLifecycleStateCodec` | identifier reuse |

## 16. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `BoquilhasModuleCatalog` | Domain | `src\BA.Dmo.Domain\Modules\Boquilhas\BoquilhasModuleCatalog.cs` |
| `BqLote` / `BqCloseSnapshot` | Domain | `src\BA.Dmo.Domain\Modules\Boquilhas\BqLote.cs` |
| `BqTrace` (+ enums/codecs) | Domain | `src\BA.Dmo.Domain\Modules\Boquilhas\BqTrace.cs` |
| `BqMovement` (+ `BqMovementType`) | Domain | `src\BA.Dmo.Domain\Modules\Boquilhas\BqMovementType.cs` |
| `BqSaldos` | Domain | `src\BA.Dmo.Domain\Modules\Boquilhas\BqSaldos.cs` |
| `BqDiscrepancy` / `BqLifecycleEvent` | Domain | `src\BA.Dmo.Domain\Modules\Boquilhas\BqDiscrepancy.cs` |
| `BqRepairer` / `BqLineRepairerDefault` | Domain | `src\BA.Dmo.Domain\Modules\Boquilhas\BqRepairer.cs` |
| `BqUtilisationReading` | Domain | `src\BA.Dmo.Domain\Modules\Boquilhas\BqUtilisationReading.cs` |
| `BqRules` / `BqInventoryCalculator` | Domain | `src\BA.Dmo.Domain\Modules\Boquilhas\BqRules.cs` |
| `BqLifecycleState` | Domain | `src\BA.Dmo.Domain\Modules\Boquilhas\BqLifecycleState.cs` |
| `BoquilhasService` | Application | `src\BA.Dmo.Application\Modules\Boquilhas\BoquilhasService.cs` |
| `BqAuthorizationGate` / `BqExecutor` | Application | `src\BA.Dmo.Application\Modules\Boquilhas\BqAuthorizationGate.cs` |
| `BqRequests` (requests + DTOs) | Application | `src\BA.Dmo.Application\Modules\Boquilhas\BqRequests.cs` |
| `IBoquilhasRepository` (+ filters) | Application | `src\BA.Dmo.Application\Modules\Boquilhas\IBoquilhasRepository.cs` |
| `IBoquilhasUnitOfWorkFactory` | Application | `src\BA.Dmo.Application\Modules\Boquilhas\IBoquilhasUnitOfWorkFactory.cs` |
| `DapperBoquilhasRepository` | Infrastructure | `src\BA.Dmo.Infrastructure\Access\DapperBoquilhasRepository.cs` |
| `DapperBoquilhasUnitOfWorkFactory` | Infrastructure | `src\BA.Dmo.Infrastructure\Access\DapperBoquilhasUnitOfWorkFactory.cs` |
| `bq_*` tables | Database | `database\migrations\N03_bq.sql` |
| `uq_bq_traces_active` | Database | `database\migrations\N25_remediation.sql` |
| `/boquilhas` page | Web | `src\BA.Dmo.Web\Pages\Boquilhas\Index.cshtml` |
| `IndexModel` | Web | `src\BA.Dmo.Web\Pages\Boquilhas\Index.cshtml.cs` |
| `/api/boquilhas/*` endpoints | Web | `src\BA.Dmo.Web\Program.cs` |
| `ModulePolicies.Boquilhas` | Web | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| `boquilhas.js` | Static assets | `src\BA.Dmo.Web\wwwroot\scripts\boquilhas.js` |
| `.boquilhas-*` CSS / tokens | Static assets (shared) | `src\BA.Dmo.Web\wwwroot\styles\dmo-components.css`, `dmo-tokens.css` |
| `BoquilhasServiceTests` etc. | Tests | `tests\BA.Dmo.UnitTests\Modules\Boquilhas\` |
| `BoquilhasWebAuthorizationTests` | Tests | `tests\BA.Dmo.IntegrationTests\Access\BoquilhasWebAuthorizationTests.cs` |
| `BqTestSupport`, `FakeBoquilhasWebRepository` | Tests (support) | `tests\BA.Dmo.UnitTests\Modules\Boquilhas\BqTestSupport.cs`, `tests\BA.Dmo.IntegrationTests\Access\FakeBoquilhasWebRepository.cs` |

## 17. Sources Verified

- `maps\00_INDEX.md` (mapping contract/registry only).
- `src\BA.Dmo.Domain\Modules\Boquilhas\` (10 files).
- `src\BA.Dmo.Application\Modules\Boquilhas\` (5 files).
- `src\BA.Dmo.Infrastructure\Access\DapperBoquilhasRepository.cs`, `DapperBoquilhasUnitOfWorkFactory.cs`.
- `database\migrations\N03_bq.sql`, `N12_rls.sql`, `N18_bq_repairer.sql`, `N25_remediation.sql`, `N01_identity.sql` (`ba_dmo_guard_append_only`), `database\consolidated_clean_install.sql`.
- `src\BA.Dmo.Web\Program.cs`, `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`, `src\BA.Dmo.Web\Pages\Boquilhas\Index.cshtml` (+ `.cs`), `src\BA.Dmo.Web\wwwroot\scripts\boquilhas.js`, `src\BA.Dmo.Web\wwwroot\styles\dmo-components.css`, `dmo-tokens.css`.
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`.
- `tests\BA.Dmo.UnitTests\Modules\Boquilhas\` (3 test classes + `BqTestSupport.cs`), `tests\BA.Dmo.IntegrationTests\Access\BoquilhasWebAuthorizationTests.cs`, `FakeBoquilhasWebRepository.cs`.

## Counts

- Domain Boquilhas files: 10
- Application Boquilhas files: 5
- Infrastructure Boquilhas files: 2
- Web dedicated page files: 2 (Index.cshtml + Index.cshtml.cs)
- Static asset files (dedicated): 1 (boquilhas.js)
- Shared Web wiring files carrying Boquilhas wiring: 2 (Program.cs, ModuleAuthorizationHandler.cs)
- Shared static asset files carrying Boquilhas-specific CSS/tokens: 2 (dmo-components.css, dmo-tokens.css)
- Shared Application catalog files carrying Boquilhas entries: 2 (CanonicalModuleCatalog.cs, CanonicalPageCatalog.cs)
- Boquilhas DB objects: 19 (6 tables + 10 indexes (9 non-unique indexes from N03 + 1 partial UNIQUE index `uq_bq_traces_active` from N25) + 3 triggers)
- Boquilhas DB tables: 6 (bq_lotes, bq_traces, bq_movements, bq_discrepancies, bq_lifecycle_history, bq_utilisation_readings)
- Associated indexes: 10 (N03 = 9 non-unique + N25 = 1 partial UNIQUE; listed §8); triggers: 3 (listed §8)
- Boquilhas migration touchpoints: 4 (N03_bq.sql, N12_rls.sql, N18_bq_repairer.sql, N25_remediation.sql)
- Boquilhas test classes: 4
- Boquilhas test support/helper files: 2 (BqTestSupport.cs, FakeBoquilhasWebRepository.cs)