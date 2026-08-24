# BA DMO — Armazém Technical Map

MAP ID: MAP-09
Status: COMPLETE

## Navigation Index

| Layer | Section |
|---|---|
| Layer Summary | §2 |
| Domain | §3 |
| Application | §4 |
| Application Contracts / Ports | §5 |
| Authorization / Catalog | §6 |
| Infrastructure | §7 |
| Database | §8 |
| Migration Touchpoints | §9 |
| Web / Routes | §10 |
| Static Assets | §11 |
| Tests | §12 |
| Test Doubles / Helpers | §13 |
| Direct Armazém References | §14 |
| External Technical References | §15 |
| Target-to-Layer Index | §16 |
| Sources Verified | §17 |
| Counts | Counts |

## 1. Scope

Technical inventory of Armazém-specific objects across Domain, Application, Infrastructure, Database, Migrations, Web, Static Assets and Tests. Cross-layer navigation only; no end-to-end flow.

Design/SOT not used as evidence. Source: `src\`, `database\`, `tests\`.

## 2. Layer Summary

| Layer | Main Armazém Objects | Locations |
|---|---|---|
| Domain | ArmazemModuleCatalog, WarehouseToolDomain, WarehouseToolIdentity, WarehouseLocation, WarehouseStock, WarehouseMovement, WarehouseStockRules, ArmazemLocationOccupiedException | `src\BA.Dmo.Domain\Modules\Armazem\` |
| Application | ArmazemService, ArmazemAuthorizationGate, ArmazemRequests (+DTOs), IToolIdentityResolver, FerramentasArmazemToolIdentityResolver, IArmazemRepository, IArmazemRepairMovementPort | `src\BA.Dmo.Application\Modules\Armazem\` |
| Shared access catalog | armazem module/capability/page entries in CanonicalModuleCatalog, CanonicalPageCatalog | `src\BA.Dmo.Application\Shared\Access\` |
| Infrastructure | DapperArmazemRepository, DapperArmazemRepairMovementRepository | `src\BA.Dmo.Infrastructure\Access\` |
| Database | warehouse_locations, warehouse_stock, warehouse_movements (+indexes, trigger) | `database\migrations\N09_armazem.sql` |
| Web | /armazem page + API endpoints | `src\BA.Dmo.Web\Pages\Armazem\`, `src\BA.Dmo.Web\Program.cs` |
| Static assets | armazem.js, armazem-layout.css | `src\BA.Dmo.Web\wwwroot\scripts\`, `src\BA.Dmo.Web\wwwroot\styles\modules\` |
| Tests | 4 test classes + 3 support files | `tests\BA.Dmo.UnitTests\Modules\Armazem\` |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | YES | `src\BA.Dmo.Domain\Modules\Armazem\` |
| Application | YES | `src\BA.Dmo.Application\Modules\Armazem\`; shared access catalog (`Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`) |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\DapperArmazemRepository.cs`, `DapperArmazemRepairMovementRepository.cs` |
| Web | YES | `src\BA.Dmo.Web\Pages\Armazem\`; `src\BA.Dmo.Web\Program.cs`; `Authorization\ModuleAuthorizationHandler.cs` |
| Database | YES | `database\migrations\N09_armazem.sql`, `N12_rls.sql` |
| Tests | YES | `tests\BA.Dmo.UnitTests\Modules\Armazem\` |

This is technical navigation only; it does not explain workflow.

## 3. Domain Objects

Source: `src\BA.Dmo.Domain\Modules\Armazem\`.

| Type | Kind | Main Members / Methods | File |
|---|---|---|---|
| `ArmazemModuleCatalog` | static constants | `ModuleId = "armazem"`; `PositionCodePattern = @"^\d{4}$"` | ArmazemModuleCatalog.cs |
| `WarehouseToolDomain` | enum | `Ferramentas`, `Boquilhas` | WarehouseToolDomain.cs |
| `WarehouseToolIdentity` | sealed record | `Guid ToolId`, `WarehouseToolDomain Domain`, `string Type`, `string Reference`, `string Lot`, `string? TechnicalName` | WarehouseToolIdentity.cs |
| `WarehouseLocation` | sealed class | `Guid WarehouseLocationId`, `string Code`, `string? Kind`; static `IsValidPositionCode(string?)`, `NormalizePositionCode(string?)` | WarehouseLocation.cs |
| `WarehouseStock` | sealed class | `WarehouseStockId`, `WarehouseLocationId`, `ToolId`, `OccupiedSinceUtc`, `OccupiedBy`, `ReleasedAtUtc?`, `ReleasedBy?`; `bool IsActive => !ReleasedAtUtc.HasValue` | WarehouseStock.cs |
| `WarehouseMovement` | sealed class | `WarehouseMovementId`, `WarehouseStockId?`, `Direction`, `Qty decimal?`, `Destination?`, `ActorId?`, `OccurredAtUtc` | WarehouseMovement.cs |
| `WarehouseMovementDirection` | enum | `In`, `Out` | WarehouseMovement.cs |
| `WarehouseMovementDirectionCodec` | static class | `ToStorage(direction)` → `"in"|"out"`; `FromStorage(string?)` | WarehouseMovement.cs |
| `WarehouseStockRules` | static class | `IsPositionOccupied(IEnumerable<WarehouseStock>)`, `IsFora(…)`, `HasReferenceConflict(…, Guid candidateToolLoteId, string candidateReference, Func<Guid,string?> referenceResolver)` | WarehouseStockRules.cs |
| `ArmazemLocationOccupiedException` | sealed exception | ctor `(string message)` | ArmazemLocationOccupiedException.cs |

Domain references:

- `ArmazemModuleCatalog.PositionCodePattern` — referenced by `WarehouseLocation` (position-code regex) and `ArmazemModuleCatalog.ModuleId` by `ArmazemAuthorizationGate`.
- `WarehouseStockRules` uses `Domain.Shared.Kernel` (no entity type in module folder; shared kernel only).

## 4. Application Objects

Source: `src\BA.Dmo.Application\Modules\Armazem\`.

| Object | Kind | Constructor Dependencies | Public Methods | File |
|---|---|---|---|---|
| `ArmazemService` | sealed class (use-case surface) | `IArmazemRepository`, `IToolIdentityResolver`, `ArmazemAuthorizationGate`, `IClock` | `RegistrarEntradaAsync(RegistrarEntradaRequest)` → `Result<Guid,DomainError>`; `RegistrarSaidaAsync(RegistrarSaidaRequest)`; `SubstituirAsync(SubstituirRequest)`; `ConsultarAsync(ConsultarRequest)`; `HistoricoAsync(toolType, reference?, lot?)` | ArmazemService.cs |
| `ArmazemExecutor` | sealed record | — | `ActorId`, `DisplayName` | ArmazemAuthorizationGate.cs |
| `RegistrarEntradaRequest` | sealed record | — | `ToolType`, `Reference?`, `Lot?`, `PositionCode`, `Destination?`, `Observations?` | ArmazemRequests.cs |
| `RegistrarSaidaRequest` | sealed record | — | `ToolType`, `Reference?`, `Lot?`, `Destination?`, `Observations?` | ArmazemRequests.cs |
| `SubstituirRequest` | sealed record | — | `PositionCode`, `NewToolType`, `NewReference?`, `NewLot?`, `Observations?` | ArmazemRequests.cs |
| `ConsultarRequest` | sealed record | — | `ToolType?`, `Reference?`, `Lot?`, `PositionCode?` | ArmazemRequests.cs |
| `ArmazemSearchHit` | sealed record DTO | — | `WarehouseToolIdentity Tool`, `string? CurrentPositionCode`, `string LocationContext` | ArmazemRequests.cs |
| `ArmazemConsultationRow` | sealed record DTO | — | `Guid ToolId`, `Type`, `Reference`, `TechnicalName?`, `Lot`, `PositionCode?`, `LocationContext`, `HasReferenceConflict` | ArmazemRequests.cs |
| `ArmazemLocationRow` | sealed record DTO | — | `PositionCode`, `IReadOnlyList<ArmazemConsultationRow> Occupants`, `HasReferenceConflict` | ArmazemRequests.cs |
| `ArmazemHistoryEntry` | sealed record DTO | — | `Direction`, `PositionCode?`, `Destination?`, `Observations?`, `ActorId?`, `OccurredAtUtc` | ArmazemRequests.cs |

`ArmazemService` callbacks into `Domain` types: `WarehouseLocation` (normalize/validate), `WarehouseStock`, `WarehouseMovement`, `WarehouseStockRules`, `WarehouseToolIdentity`. Uses `Shared.Kernel.Result<,>`, `DomainError` (codes `ARMZ_LOCATION_CODE`, `ARMZ_POSITION_OCCUPIED`, `ARMZ_TOOL_NOT_IN_WAREHOUSE`, `ARMZ_POSITION_FREE`, `ARMZ_SEARCH_REQUIRED`, `ARMZ_TOOL_REQUIRED`, `ARMZ_TOOL_NOT_FOUND`).

## 5. Application Contracts / Ports

Source: `src\BA.Dmo.Application\Modules\Armazem\`.

| Interface | Main Methods | Implementations |
|---|---|---|
| `IToolIdentityResolver` | `SearchAsync(type, reference?, lot?, ct)` → `IReadOnlyList<WarehouseToolIdentity>`; `ResolveAsync(Guid toolId, ct)` → `WarehouseToolIdentity?` | `FerramentasArmazemToolIdentityResolver` (Armazém Application) |
| `IArmazemRepository` | locations: `GetOrCreateLocationAsync`, `GetLocationByCodeAsync`, `GetLocationByIdAsync`; stock: `GetActiveStockByLocationAsync`, `GetActiveStockByToolIdAsync`, `GetActiveStocksAsync`, `GetStockByLocationAsync`, `GetStockByToolIdAsync`; atomic writes: `RegisterEntradaAsync`, `RegisterSaidaAsync`, `ReplaceOccupationAsync`; history: `GetMovementHistoryAsync`; audit: `InsertAuditEventAsync` | `DapperArmazemRepository` (Infrastructure) |
| `IArmazemRepairMovementPort` | `ConfirmPickupAsync(IDbUnitOfWork, repairExitId, toolLoteId, actorId, outAtUtc, ct)` → `Result<bool,DomainError>`; `ConfirmReturnAsync(IDbUnitOfWork, repairExitId, toolLoteId, positionCode, actorId, inAtUtc, ct)` | `DapperArmazemRepairMovementRepository` (Infrastructure) |

## 6. Authorization / Catalog Objects

Source: `src\BA.Dmo.Application\Shared\Access\`, `src\BA.Dmo.Web\Authorization\`.

| Object | Identifier / Route | Role | File |
|---|---|---|---|
| `CanonicalModuleCatalog.ArmazemModuleId` | `"armazem"` | canonical module id | CanonicalModuleCatalog.cs |
| `ModuleDefinition(ArmazemModuleId, "Armazém", Module, order 50, "/armazem")` | `"/armazem"` | module catalog entry with no declared capability ids | CanonicalModuleCatalog.cs |
| `Descriptions[ArmazemModuleId]` | `"Gestão de armazém"` | display-only description | CanonicalModuleCatalog.cs |
| `CanonicalPageCatalog.ArmazemMapaPageId` | `"armazem.mapa"` | page id, route `/armazem`, `requiredCapabilityId: null`, route order 50 | CanonicalPageCatalog.cs |
| `ModulePolicies.Armazem` | `"BaDmo.Module.armazem"` | module-entry policy name | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| `ModuleRequirement` + `ModuleAuthorizationHandler` | `user.HasModule(moduleId)` | server-side module-entry requirement (fail closed) | ModuleAuthorizationHandler.cs |

No Armazém-specific capability ids present in source.

## 7. Infrastructure Objects

Source: `src\BA.Dmo.Infrastructure\Access\`.

### DapperArmazemRepository
Implements `IArmazemRepository`. Dependency: `IDbConnectionFactory`. Executes SQL via `DapperUnitOfWork.RunAsync` for multi-table writes (stock + movement; Substituir release+occupy).

Embedded SQL objects (table targets): `warehouse_locations`, `warehouse_stock`, `warehouse_movements`, `audit_events`.

Public methods: all `IArmazemRepository` methods. Key SQL:
- `GetOrCreateLocationAsync` — `INSERT INTO warehouse_locations … ON CONFLICT (code) DO NOTHING`, then `SELECT`.
- `RegisterEntradaAsync` — `SELECT … FOR UPDATE` on location row; active-occupant check; `INSERT INTO warehouse_stock`; `INSERT INTO warehouse_movements`; throws `ArmazemLocationOccupiedException` when occupied by a different or same tool.
- `RegisterSaidaAsync` / `ReplaceOccupationAsync` — `UPDATE warehouse_stock SET released_at_utc=…, released_by=… WHERE warehouse_stock_id=@Id AND released_at_utc IS NULL` guarded by `ConcurrencyGuard.EnsureSingleRowUpdated`.
- `GetMovementHistoryAsync` — `SELECT … FROM warehouse_movements m JOIN warehouse_stock s ON s.warehouse_stock_id = m.warehouse_stock_id WHERE s.tool_lote_id = @ToolId ORDER BY occurred_at_utc`.
- `InsertAuditEventAsync` — `INSERT INTO audit_events (… module_id='armazem', entity_type='armazem' …)`.

Mapping helpers: `MapLocation`, `MapStock`, `MapMovement`, `ToMovementWithStock`, `InsertMovementAsync`. Uses codec `WarehouseMovementDirectionCodec` for direction storage text.

### DapperArmazemRepairMovementRepository
Implements `IArmazemRepairMovementPort`. Dependency: `IDbConnectionFactory`. Writes `warehouse_stock` + `warehouse_movements` inside a caller-provided `IDbUnitOfWork`. Embedded SQL includes `INSERT INTO warehouse_movements (… repair_exit_id …)` with `Destination = "reparacao_externa"`.

## 8. Database Objects

Source: `database\migrations\N09_armazem.sql`.

| Object | Kind | Role | PK / References | Constraints / Indexes |
|---|---|---|---|---|
| `warehouse_locations` | table | physical positions | `warehouse_location_id` PK default `gen_random_uuid()`; `created_by` → `internal_users(actor_id)` | `code` NOT NULL UNIQUE; `kind` NULL; `created_at_utc`, `updated_at_utc` |
| `warehouse_stock` | table | occupation fact | `warehouse_stock_id` PK; `warehouse_location_id` → `warehouse_locations`; `tool_lote_id` → `tool_lotes`; `occupied_by`/`released_by` → `internal_users(actor_id)` | UNIQUE `uq_warehouse_stock_active_occupation (warehouse_location_id, tool_lote_id) WHERE released_at_utc IS NULL`; `ix_warehouse_stock_location`, `ix_warehouse_stock_tool_lote` |
| `warehouse_movements` | table | in/out facts (append-only) | `warehouse_movement_id` PK; `warehouse_stock_id` → `warehouse_stock`; `repair_exit_id` → `repair_exits`; `actor_id` → `internal_users(actor_id)` | CHECK `ck_warehouse_movements_direction direction IN ('in','out')`; `ix_warehouse_movements_stock`, `ix_warehouse_movements_occurred`; trigger `trg_warehouse_movements_append_only BEFORE UPDATE OR DELETE` → `ba_dmo_guard_append_only()` |

### Unique active-occupation structure
| Index | Target | Indexed Columns | Predicate | Source |
|---|---|---|---|---|
| `uq_warehouse_stock_active_occupation` | `warehouse_stock` | `(warehouse_location_id, tool_lote_id)` | `WHERE released_at_utc IS NULL` | N09_armazem.sql |

External DB objects referenced (not defined by N09): `tool_lotes` (N04_ferramentas.sql, via `warehouse_stock.tool_lote_id`), `repair_exits` (N08_reparacoes.sql, via `warehouse_movements.repair_exit_id`), `internal_users` (N01, via actor FKs), function `ba_dmo_guard_append_only()` (N01).

## 9. Migration Touchpoints

| Migration | Armazém Object(s) | Technical Change |
|---|---|---|
| N09_armazem.sql | warehouse_locations, warehouse_stock, warehouse_movements | Creates tables; adds UNIQUE/CHECK constraints; adds indexes `uq_warehouse_stock_active_occupation`, `ix_warehouse_stock_location`, `ix_warehouse_stock_tool_lote`, `ix_warehouse_movements_stock`, `ix_warehouse_movements_occurred`; creates trigger `trg_warehouse_movements_append_only` → `ba_dmo_guard_append_only()` |
| N12_rls.sql | warehouse_locations, warehouse_stock, warehouse_movements | Enables RLS on the 3 tables; creates policy `ba_dmo_app_access FOR ALL TO ba_dmo_app USING (true) WITH CHECK (true)` (shared wiring; not an Armazém-dedicated migration) |

## 10. Web / Routes

Source: `src\BA.Dmo.Web\Pages\Armazem\`, `src\BA.Dmo.Web\Program.cs`.

| Route | Technical Entry Point | Authorization | File |
|---|---|---|---|
| `/armazem` (GET) | `IndexModel.OnGet` → `Pages\Armazem\Index.cshtml` | `[Authorize(Policy = BaDmo.Web.Authorization.ModulePolicies.Armazem)]` | Index.cshtml / Index.cshtml.cs |
| `GET /api/armazem/consulta` | `ArmazemService.ConsultarAsync(new ConsultarRequest(...))` | `.RequireAuthorization(ModulePolicies.Armazem)` | Program.cs |
| `POST /api/armazem/entrada` | `ArmazemService.RegistrarEntradaAsync(request)` | `.RequireAuthorization(ModulePolicies.Armazem)` | Program.cs |
| `POST /api/armazem/saida` | `ArmazemService.RegistrarSaidaAsync(request)` | `.RequireAuthorization(ModulePolicies.Armazem)` | Program.cs |
| `POST /api/armazem/substituir` | `ArmazemService.SubstituirAsync(request)` | `.RequireAuthorization(ModulePolicies.Armazem)` | Program.cs |
| `GET /api/armazem/{toolType}/historico` | `ArmazemService.HistoricoAsync(toolType, reference, lot)` | `.RequireAuthorization(ModulePolicies.Armazem)` | Program.cs |

Shared Web wiring in `Program.cs`: DI registrations `IArmazemRepository→DapperArmazemRepository`, `IToolIdentityResolver→FerramentasArmazemToolIdentityResolver`, `ArmazemAuthorizationGate`, `ArmazemService`, `IArmazemRepairMovementPort→DapperArmazemRepairMovementRepository`.

`Pages\Armazem\Index.cshtml`: Razor page; loads `~/styles/modules/armazem-layout.css`, `~/scripts/armazem.js`; views Registo / Consulta / Programadas / Histórico; forms Entrada / Saída / Substituir; table `#consultationTable` / `#consultationBody`; testid `page-armazem`, `registo-empty`.

## 11. Static Assets

| File | Principal Functions / Selectors | API Routes Called | Location |
|---|---|---|---|
| armazem.js | wiring: tab toggle (`.armazem-tabs .tab`, `.armazem-view`), inline cards (`[data-open]`, `[data-close]`, `.armazem-card`), form read (`readForm`), submit handlers `entradaForm`/`saidaForm`/`substituirForm`, consultation `runSeek`/`renderRows` (`#consultationBody`, `#consultationEmpty`), toast `#toast`, `?position=` deep-link | `POST /api/armazem/entrada`, `POST /api/armazem/saida`, `POST /api/armazem/substituir`, `GET /api/armazem/consulta` | `src\BA.Dmo.Web\wwwroot\scripts\armazem.js` |
| armazem-layout.css | composition/layout only: `.armazem-page`, `.armazem-tabs`, `.armazem-view(.active)`, `.armazem-action-row`, `.armazem-card`, `.armazem-grid`, `.armazem-search`, `.armazem-results`; responsive breakpoints 900/980/720 | — (no calls) | `src\BA.Dmo.Web\wwwroot\styles\modules\armazem-layout.css` |

## 12. Tests

Source: `tests\BA.Dmo.UnitTests\Modules\Armazem\`.

| Test Class | Kind | Direct Target | Main Method Groups | File |
|---|---|---|---|---|
| `ArmazemServiceTests` | unit (xUnit) | `ArmazemService` (+`FakeArmazemRepository` atomic guard) | authorization fail-closed; Entrada (validate/occupy/atomic/concurrency); Saída; Substituir; Consulta / fora / two-reference warning; Repor | ArmazemServiceTests.cs |
| `ArmazemAuthorizationGateTests` | unit | `ArmazemAuthorizationGate.Require()` | module-grant success, without-module forbidden | ArmazemAuthorizationGateTests.cs |
| `WarehouseStockRulesTests` | unit | `WarehouseLocation`, `WarehouseStockRules` | position-code validation; IsPositionOccupied; IsFora; HasReferenceConflict | WarehouseStockRulesTests.cs |
| `FerramentasArmazemToolIdentityResolverTests` | unit | `FerramentasArmazemToolIdentityResolver` | CM/MF accepted and mapped; BQ/PU/CS empty; Resolve maps / missing → null | FerramentasArmazemToolIdentityResolverTests.cs |

External test targets (implement Armazém port, reside in Reparação Externa scope): `FakeArmazemRepair : IArmazemRepairMovementPort` (`tests\BA.Dmo.IntegrationTests\ReparacaoExterna\ReparacaoExternaWebApiTests.cs`), `FakeArmazemRepairMovementPort` (`tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaTestSupport.cs`).

Shared catalog/navigation tests referencing the armazem module/page (shared scope): `NavigationServiceTests`, `CanonicalPageCatalogTests`, `CanonicalModuleCatalogTests`, `AccessResolverTests` (UnitTests\Shared\Access), `ShellRoutingTests`, `HistoriaWebAuthorizationTests` (IntegrationTests\Access), `DesignSystemGuardTests` (lists `armazem-layout.css`).

## 13. Test Doubles / Helpers

Source: `tests\BA.Dmo.UnitTests\Modules\Armazem\`.

| Double / Helper | Implements / Provides | File |
|---|---|---|
| `FakeArmazemRepository` | `IArmazemRepository`; in-memory stock/movement/audit; `FailAtomicWrite` for fencing | FakeArmazemRepository.cs |
| `FakeToolIdentityResolver` | `IToolIdentityResolver`; preset `WarehouseToolIdentity` list | FakeToolIdentityResolver.cs |
| `ArmazemTestSupport` | `ArmazemFixedClock : IClock`; `ArmazemFakeAuthorship : IPersistenceAuthorshipAccessor`; `ArmazemCurrentUser : ICurrentUserAccessor` (Authorized/WithoutModule); `FakeFerramentasIdentityLookup : IFerramentasIdentityLookup` | ArmazemTestSupport.cs |

## 14. Direct Armazém References

One edge per relationship.

```
ArmazemService
→ IArmazemRepository
→ IToolIdentityResolver
→ ArmazemAuthorizationGate
→ IClock
→ WarehouseStockRules

IToolIdentityResolver
→ FerramentasArmazemToolIdentityResolver

FerramentasArmazemToolIdentityResolver
→ IFerramentasIdentityLookup

ArmazemAuthorizationGate
→ ArmazemModuleCatalog
→ ICurrentUserAccessor
→ IPersistenceAuthorshipAccessor

IArmazemRepository
→ DapperArmazemRepository

DapperArmazemRepository
→ warehouse_locations
→ warehouse_stock
→ warehouse_movements
→ audit_events

IArmazemRepairMovementPort
→ DapperArmazemRepairMovementRepository

DapperArmazemRepairMovementRepository
→ warehouse_stock
→ warehouse_movements
```

## 15. External Technical References

| Armazém Object | External Technical Reference | Reference Type |
|---|---|---|
| `warehouse_stock.tool_lote_id` | `tool_lotes.tool_lote_id` (Ferramentas table, N04) | DB FK |
| `warehouse_movements.repair_exit_id` | `repair_exits.repair_exit_id` (Reparação table, N08) | DB FK |
| `warehouse_locations.created_by` / `warehouse_stock.occupied_by` / `released_by` / `warehouse_movements.actor_id` | `internal_users(actor_id)` (N01) | DB FK |
| `trg_warehouse_movements_append_only` | `ba_dmo_guard_append_only()` function (N01) | DB function (trigger) |
| `FerramentasArmazemToolIdentityResolver` | `IFerramentasIdentityLookup`, `FerramentasToolType`, `FerramentasIdentityHit` | application port / constructor dependency |
| `WarehouseToolDomain.Ferramentas` | Ferramentas tool domain | enum/reference reuse |
| `DapperArmazemRepairMovementRepository.ConfirmPickupAsync/ConfirmReturnAsync` | consumed by `ReparacaoExternaService` | application port (test target) |
| `FakeArmazemRepairMovementPort`, `FakeArmazemRepair` | `IArmazemRepairMovementPort` (Reparação Externa tests) | test target |

## 16. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| ArmazemModuleCatalog | Domain | `src\BA.Dmo.Domain\Modules\Armazem\ArmazemModuleCatalog.cs` |
| WarehouseToolDomain | Domain | `src\BA.Dmo.Domain\Modules\Armazem\WarehouseToolDomain.cs` |
| WarehouseToolIdentity | Domain | `src\BA.Dmo.Domain\Modules\Armazem\WarehouseToolIdentity.cs` |
| WarehouseLocation | Domain | `src\BA.Dmo.Domain\Modules\Armazem\WarehouseLocation.cs` |
| WarehouseStock | Domain | `src\BA.Dmo.Domain\Modules\Armazem\WarehouseStock.cs` |
| WarehouseMovement (+Direction, Codec) | Domain | `src\BA.Dmo.Domain\Modules\Armazem\WarehouseMovement.cs` |
| WarehouseStockRules | Domain | `src\BA.Dmo.Domain\Modules\Armazem\WarehouseStockRules.cs` |
| ArmazemLocationOccupiedException | Domain | `src\BA.Dmo.Domain\Modules\Armazem\ArmazemLocationOccupiedException.cs` |
| ArmazemService | Application | `src\BA.Dmo.Application\Modules\Armazem\ArmazemService.cs` |
| ArmazemAuthorizationGate (+ArmazemExecutor) | Application | `src\BA.Dmo.Application\Modules\Armazem\ArmazemAuthorizationGate.cs` |
| ArmazemRequests (+DTOs) | Application | `src\BA.Dmo.Application\Modules\Armazem\ArmazemRequests.cs` |
| IToolIdentityResolver | Application port | `src\BA.Dmo.Application\Modules\Armazem\IToolIdentityResolver.cs` |
| FerramentasArmazemToolIdentityResolver | Application | `src\BA.Dmo.Application\Modules\Armazem\FerramentasArmazemToolIdentityResolver.cs` |
| IArmazemRepository | Application port | `src\BA.Dmo.Application\Modules\Armazem\IArmazemRepository.cs` |
| IArmazemRepairMovementPort | Application port | `src\BA.Dmo.Application\Modules\Armazem\IArmazemRepairMovementPort.cs` |
| DapperArmazemRepository | Infrastructure | `src\BA.Dmo.Infrastructure\Access\DapperArmazemRepository.cs` |
| DapperArmazemRepairMovementRepository | Infrastructure | `src\BA.Dmo.Infrastructure\Access\DapperArmazemRepairMovementRepository.cs` |
| /armazem page (IndexModel + Razor) | Web | `src\BA.Dmo.Web\Pages\Armazem\` |
| /api/armazem/* endpoints + DI | Web (shared wiring) | `src\BA.Dmo.Web\Program.cs` |
| ModulePolicies.Armazem | Web (shared wiring) | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| armazem module/page catalog entries | Application (shared) | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs` |
| armazem.js | Static asset | `src\BA.Dmo.Web\wwwroot\scripts\armazem.js` |
| armazem-layout.css | Static asset | `src\BA.Dmo.Web\wwwroot\styles\modules\armazem-layout.css` |
| warehouse_locations / warehouse_stock / warehouse_movements (+indexes, trigger) | Database | `database\migrations\N09_armazem.sql` |
| Test classes + support | Tests | `tests\BA.Dmo.UnitTests\Modules\Armazem\` |

## 17. Sources Verified

Directly inspected current source: all files under `src\BA.Dmo.Domain\Modules\Armazem\`, `src\BA.Dmo.Application\Modules\Armazem\`, `src\BA.Dmo.Infrastructure\Access\` (2 Armazém files), `src\BA.Dmo.Web\Pages\Armazem\`, `src\BA.Dmo.Web\wwwroot\scripts\armazem.js`, `src\BA.Dmo.Web\wwwroot\styles\modules\armazem-layout.css`; `Program.cs` DI + Armazém API routes; `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`; `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`; `database\migrations\N09_armazem.sql`, `database\migrations\N12_rls.sql`; `tests\BA.Dmo.UnitTests\Modules\Armazem\` (all files); referenced tests in `tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\`, `tests\BA.Dmo.IntegrationTests\ReparacaoExterna\`, `tests\BA.Dmo.IntegrationTests\Access\`. Each nav-level detail confirmed by `read`/`grep`/`glob`; line numbers not fabricated.

## Counts

| Category | Count |
|---|---|
| Domain Armazém files | 8 |
| Application Armazém files | 7 |
| Infrastructure Armazém files | 2 |
| Web dedicated page files | 2 |
| Static asset files | 2 |
| Shared Web wiring files carrying Armazém wiring | 2 (`Program.cs`, `ModuleAuthorizationHandler.cs`) |
| Shared Application catalog files carrying Armazém entries | 2 (`CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`) |
| Armazém DB objects | 9 (3 tables + 5 indexes + 1 trigger) |
| Armazém migration touchpoints | 2 (N09 dedicated; N12 shared RLS wiring) |
| Armazém test classes (dedicated) | 4 |
| Armazém test support/helper files (dedicated) | 3 |