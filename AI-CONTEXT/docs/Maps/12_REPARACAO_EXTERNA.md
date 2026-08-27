# BA DMO — Reparação Externa Technical Map

MAP ID: MAP-12
Status: COMPLETE

Canonical Module: Reparação Externa
Index order: 7
Index route: `/reparacao-externa`

## Navigation Index

- 1. Scope
- 2. Layer Summary
- 3. Domain Objects
- 4. Application Objects
- 5. Application Contracts / Ports
- 6. Authorization / Catalog Objects
- 7. User Surfaces
- 8. Infrastructure Objects
- 9. Database Objects
- 10. Migration Touchpoints
- 11. Web / Routes
- 12. Static Assets
- 13. Tests
- 14. Test Doubles / Helpers
- 15. Direct Reparação Externa References
- 16. External Technical References
- 17. Target-to-Layer Index
- 18. Sources Verified
- Counts

## Cross-References

- `maps\00_INDEX.md` (module order 7, status COMPLETE; MAP-12)
- `maps\01_DOMAIN.md` (Reparação Externa domain folder/types, §13)
- `maps\02_DATABASE.md` (DB objects), `maps\03_MIGRATIONS.md` (N08/N09/N12/N18/N20/N25 touchpoints), `maps\04_DAPPER_INFRASTRUCTURE.md` (Dapper adapters)
- `maps\05_TESTS.md` (test layout), `maps\19_APPLICATION.md` (service/ports), `maps\20_WEB.md` (page/routes/static assets)
- `maps\08_FERRAMENTAS.md` (tool-piece resolver: `IToolPieceResolver`/`FerramentasRepairToolPieceResolver` over `IFerramentasPieceLookup`, Ferramentas `physical_pieces`/`tool_lotes`)
- `maps\09_ARMAZEM.md` (repair movements: `IArmazemRepairMovementPort` → `warehouse_stock`/`warehouse_movements`, FK `repair_exit_id`)

## 1. Scope

Reparação Externa is one canonical top-level module (INDEX order 7). The module's source contains: exit lists (`repair_exits`), exit items (`repair_exit_items`), repairers, line repairer defaults, the repair status machine, and a single page surface with six tab sections. Repair types present: CM, MF, BQ. Application service accepts CM and MF; BQ is not handled by the service (`REPEXT_TYPE_SCOPE`).

Shared repair infrastructure (`repairers`, `line_repairer_defaults`, `repair_events`, `repairer_repair_types`) is classified as shared dependency, not Reparação Externa-specific.

## 2. Layer Summary

| Layer | Location | Count |
|---|---|---|
| Domain | `src\BA.Dmo.Domain\Modules\ReparacaoExterna\` | 10 |
| Application | `src\BA.Dmo.Application\Modules\ReparacaoExterna\` | 6 |
| Infrastructure | `src\BA.Dmo.Infrastructure\Access\` (RE-specific: DapperRepairRepository; shared dependency: DapperRepairUnitOfWorkFactory) | 1 RE-specific (+1 shared) |
| Web pages | `src\BA.Dmo.Web\Pages\ReparacaoExterna\` | 4 |
| Static assets | `wwwroot\scripts\reparacao-externa.js`, `wwwroot\styles\modules\reparacao-externa-layout.css` | 2 |
| Tests | `AI-CONTEXT\docs\tests\...\ReparacaoExterna\` | 7 |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | YES | `src\BA.Dmo.Domain\Modules\ReparacaoExterna\` |
| Application | YES | `src\BA.Dmo.Application\Modules\ReparacaoExterna\` |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\DapperRepairRepository.cs` (shared `DapperRepairUnitOfWorkFactory` as dependency) |
| Web | YES | `src\BA.Dmo.Web\Pages\ReparacaoExterna\`; `src\BA.Dmo.Web\Program.cs`; `Authorization\ModuleAuthorizationHandler.cs` |
| Database | YES | `database\migrations\N08_reparacoes.sql`, `N09_armazem.sql`, `N25_remediation.sql` |
| Tests | YES | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\ReparacaoExterna\` |

This is technical navigation only; it does not explain workflow.

## 3. Domain Objects

Location: `src\BA.Dmo.Domain\Modules\ReparacaoExterna\`

| Object | Kind | Notes / members | File |
|---|---|---|---|
| `ReparacaoExternaModuleCatalog` | static catalog | `ModuleId = "reparacao_externa"`; `RepairTypes = { "BQ", "CM", "MF" }` | ReparacaoExternaModuleCatalog.cs |
| `RepairType` | enum | `BQ`, `CM`, `MF` | RepairType.cs |
| `RepairTypeCodec` | static codec | `ToStorage`/`FromStorage` over text `"BQ"|"CM"|"MF"` | RepairType.cs |
| `RepairExitStatus` | enum | `Preparacao`, `ARetirar`, `Enviado`, `RetornoParcial`, `Concluido`, `Cancelado` | RepairExitStatus.cs |
| `RepairExitStatusCodec` | static codec | storage `preparacao|a_retirar|enviado|retorno_parcial|concluido|cancelado` | RepairExitStatus.cs |
| `RepairExit` | aggregate root (sealed class) | `RepairExitId`, `RepairType`, `RepairerId`, `RepairerSnapshot`, `PlannedDate`, `Status`, `CreatedAtUtc/By`, `UpdatedAtUtc`, `Items`; statics `Create`, `ValidateNotAlreadyInOpenExit`; members `IsPreparing`, `IsOpen`; error `REPEXT_ITEM_IN_OPEN_EXIT` | RepairExit.cs |
| `RepairExitItem` | entity (sealed class) | `RepairExitItemId`, `RepairExitId`, `BqLoteId`, `PhysicalPieceId`, `Qty`, `IndividualNumber`, `Picked`, `OutAtUtc`, `OutOperatorId`, `InAtUtc`, `InOperatorId`, `Status` (default `pendente`); statics `CreateCmMf`; methods `ConfirmPickedOut`, `ConfirmReturned`; properties `IsPickedOut`, `IsReturned`; errors `REPEXT_ITEM_KIND`, `REPEXT_ITEM_NUMBER_REQUIRED`, `REPEXT_ITEM_ALREADY_RETURNED` | RepairExitItem.cs |
| `RepairExitRules` | static classification | `HasUnknownLocation(string positionCode)`; consts `DuplicateInOpenExitCode = "REPEXT_ITEM_IN_OPEN_EXIT"`, `ReturnWithoutExitCode = "REPEXT_RETURN_WITHOUT_EXIT"` | RepairExitRules.cs |
| `RepairExitStatusMachine` | static state machine | `ConfirmPickup(current, itemsBefore, confirmed)`; `ConfirmReturn(current, itemsAfter)`; errors `REPEXT_CYCLE_CLOSED`, `REPEXT_CYCLE_PARTIAL`, `REPEXT_CYCLE_CANCELED` | RepairExitStatusMachine.cs |
| `Repairer` | canonical repairer (sealed class) | `RepairerId`, `Name`, `Active` (default true), `SupportedTypes` (hash set), `CreatedAtUtc/UpdatedAtUtc` | Repairer.cs |
| `RepairerSnapshot` | immutable record | `(Guid RepairerId, string Name, bool Active)` | RepairerSnapshot.cs |
| `LineRepairerDefault` | record-like (sealed class) | `Line`, `ToolType`, `RepairerId`, `UpdatedAtUtc`, `UpdatedBy` | LineRepairerDefault.cs |

State flow (field of the status machine): `Preparacao → ARetirar → Enviado → RetornoParcial → Concluido`. `Cancelado` has no source-defined transition/authorization rules; the storage value is present in the codec and `repair_exits.status` CHECK constraint.

Supported types literal set: `CM`, `MF`, `BQ`.

## 4. Application Objects

Location: `src\BA.Dmo.Application\Modules\ReparacaoExterna\`

| Object | Kind | Principal members | File |
|---|---|---|---|
| `ReparacaoExternaAuthorizationGate` | gate | ctor `(ICurrentUserAccessor, IPersistenceAuthorshipAccessor)`; `Require()` returns `Result<ReparacaoExternaExecutor, DomainError>`; fail-closed errors `REPEXT_FORBIDDEN` | ReparacaoExternaAuthorizationGate.cs |
| `ReparacaoExternaExecutor` | record | `(string ActorId, string DisplayName)` | ReparacaoExternaAuthorizationGate.cs |
| `IToolPieceResolver` | port | `SearchAsync(RepairType, reference, lot, number, ct)`, `ResolveAsync(Guid physicalPieceId, ct)` | IToolPieceResolver.cs |
| `RepairToolIdentity` | projection record | `(PhysicalPieceId, ToolLoteId, ToolReferenceId, Type, Reference, Lot, Number, TechnicalName)` | IToolPieceResolver.cs |
| `FerramentasRepairToolPieceResolver` | adapter implementing `IToolPieceResolver` | maps `RepairType.CM→FerramentasToolType.CM`, `RepairType.MF→MF`; BQ rejected; ctor `(IFerramentasPieceLookup)` | FerramentasRepairToolPieceResolver.cs |
| `IRepairRepository` | port | create/hydrate/list exits; add/get/delete items; coordinated `ConfirmItemPickedAsync`, `ConfirmItemReturnedAsync`, `UpdateExitStatusAsync`, `InsertRepairEventAsync` (all take `IDbUnitOfWork`); repairer CRUD; `UpsertLineDefaultAsync`/`ListLineDefaultsAsync`; `SetRepairerRepairTypesAsync`/`ListRepairerRepairTypesAsync`; `InsertAuditEventAsync` | IRepairRepository.cs |
| `ReparacaoExternaService` | service | public methods: `SearchToolsAsync`, `CreateExitAsync`, `AddItemAsync`, `RemoveItemAsync`, `DisponibilizarExitAsync`, `ConfirmPickupAsync`, `ConfirmReturnAsync`, `ListExitsAsync`, `GetExitAsync`, `ListRepairersAsync`, `ListLineDefaultsAsync`, `GetHistoryAsync`, `CreateRepairerAsync`, `UpdateRepairerAsync`, `DeactivateRepairerAsync`, `UpsertLineDefaultAsync` | ReparacaoExternaService.cs |
| Commands (records) | requests | `CreateExitRequest`, `NewExitItemRequest`, `AddExitItemRequest`, `RemoveExitItemRequest`, `DisponibilizarExitRequest`, `ConfirmPickupRequest`, `ConfirmReturnRequest`, `CreateRepairerRequest`, `UpdateRepairerRequest`, `DeactivateRepairerRequest`, `UpsertLineDefaultRequest` | ReparacaoExternaRequests.cs |
| DTOs (records) | projections | `RepairExitItemDto`, `RepairExitDto`, `RepairerDto`, `LineRepairerDefaultDto`, `RepairHistoryRow` | ReparacaoExternaRequests.cs |

`ReparacaoExternaService` ctor dependencies: `IRepairRepository`, `IToolPieceResolver`, `IArmazemRepairMovementPort`, `IRepairUnitOfWorkFactory`, `ReparacaoExternaAuthorizationGate`, `IClock`.

Application error codes emitted by the service: `REPEXT_TYPE_SCOPE`, `REPEXT_LIST_NOT_EDITABLE`, `REPEXT_PIECE_NOT_FOUND`, `REPEXT_PIECE_NUMBER_MISMATCH`, `REPEXT_ITEM_MOVED`, `REPEXT_NOT_PREPARING`, `REPEXT_EMPTY_LIST`, `REPEXT_ITEM_ALREADY_RETURNED`, `REPEXT_POSITION_CODE`, `REPEXT_NOT_FOUND`, `REPEXT_REPAIRER_NAME_REQUIRED`, `REPEXT_REPAIRER_INACTIVE`, `REPEXT_REPAIRER_TYPE_INVALID`, plus domain constants/errors and `REPEXT_RETURN_WITHOUT_EXIT`, `REPEXT_ITEM_IN_OPEN_EXIT`, `REPEXT_PIECE_NOT_FOUND`.

`NormalizeSupportedTypes` literal set: `CM`, `MF`, `BQ`.

Audit action_code literals written by the Dapper repository (`module_id = 'reparacao_externa'`, `entity_type = 'reparacao_externa'`): `reparacao_externa.lista.criar`, `.lista.item`, `.lista.item.remover`, `.lista.disponibilizar`, `.item.recolhido`, `.item.retornado`, `.reparador.criar`, `.reparador.editar`, `.reparador.desativar`, `.linha.defeito`.

## 5. Application Contracts / Ports

| Port | Principal methods | Path | Implementation(s) | Direct external dependency |
|---|---|---|---|---|
| `IRepairRepository` | create/list/get exits/items, coordinated item + status + event writes within `IDbUnitOfWork`, repairer CRUD + types, audit | `src\BA.Dmo.Application\Modules\ReparacaoExterna\IRepairRepository.cs` | `DapperRepairRepository` | `IDbConnectionFactory`; DB `repair_exits`, `repair_exit_items`, `repairers`, `line_repairer_defaults`, `repairer_repair_types`, `repair_events`, `audit_events` |
| `IToolPieceResolver` | `SearchAsync`, `ResolveAsync` | `src\BA.Dmo.Application\Modules\ReparacaoExterna\IToolPieceResolver.cs` | `FerramentasRepairToolPieceResolver` | `IFerramentasPieceLookup` (port declared in the Ferramentas module) |
| `IRepairUnitOfWorkFactory` (shared) | `BeginAsync` | `src\BA.Dmo.Application\Shared\Persistence\IRepairUnitOfWorkFactory.cs` | `DapperRepairUnitOfWorkFactory` | `IDbConnectionFactory` → `DapperUnitOfWork` |
| `IArmazemRepairMovementPort` (external, port declared in the Armazém module) | `ConfirmPickupAsync`, `ConfirmReturnAsync` (both take `IDbUnitOfWork`) | `src\BA.Dmo.Application\Modules\Armazem\IArmazemRepairMovementPort.cs` | `DapperArmazemRepairMovementRepository` | writes `warehouse_stock`, `warehouse_movements`; FK `repair_exit_id` |

## 6. Authorization / Catalog Objects

| Object | Value | Location |
|---|---|---|
| Module id | `reparacao_externa` (`CanonicalModuleCatalog.ReparacaoExternaModuleId`) | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| Module definition | `(reparacao_externa, "Reparação Externa", ModuleKind.Module, order 70, "/reparacao-externa")`, no declared capabilities | CanonicalModuleCatalog.cs |
| Page id | `reparacao_externa.listas` (`CanonicalPageCatalog.ReparacaoExternaListasPageId`), route `/reparacao-externa`, `requiredCapabilityId: null`, displayOrder 70 | `src\BA.Dmo.Application\Shared\Access\CanonicalPageCatalog.cs` |
| Module entry policy | `BaDmo.Module.reparacao_externa` (`ModulePolicies.ReparacaoExterna`) | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| Module authorization handler | `ModuleAuthorizationHandler` — succeeds when `user.HasModule(requirement.ModuleId)`; fail closed | ModuleAuthorizationHandler.cs |
| Web page policy attribute | `[Authorize(Policy = ModulePolicies.ReparacaoExterna)]` on `Index.cshtml` | `src\BA.Dmo.Web\Pages\ReparacaoExterna\Index.cshtml` |
| Application gate | `ReparacaoExternaAuthorizationGate.Require()` calls `user.HasModule("reparacao_externa")`; returns `REPEXT_FORBIDDEN` when no identity/module/actor | `src\BA.Dmo.Application\Modules\ReparacaoExterna\ReparacaoExternaAuthorizationGate.cs` |
| Capability policy | none declared for Reparação Externa | `CapabilityPolicies` in ModuleAuthorizationHandler.cs |

Fails closed (no resolved identity → `REPEXT_FORBIDDEN`).

## 7. User Surfaces

Source defines a single module surface. Evidence: one page route `/reparacao-externa`; single `IndexModel` with a parameterless `OnGet`; single auth gate based only on module presence; no capability/profile/capability-based rendering in the page, code-behind or `reparacao-externa.js`; the six tab views are structural tabs inside one shared page.

User Surface: **Shared**

The six tab sections in `Index.cshtml` are shared: `boquilhas`, `contra-moldes` (CM list builder), `moldes-finais` (MF list builder), `envios`, `historico`, `definicoes`. All rendered for every authorized user; no profile distinction in source.

## 8. Infrastructure Objects

RE-specific:

| Object | Implements | Principal behavior | DB objects referenced | Location |
|---|---|---|---|---|
| `DapperRepairRepository` | `IRepairRepository` | Dapper CRUD for exit lists/items, repairers, line defaults, repairer types, audit; coordinated writes via shared `IDbUnitOfWork`; `InsertRepairEventAsync` writes `repair_events` with `repair_scope = 'externa'` | `repair_exits`, `repair_exit_items`, `repairers`, `line_repairer_defaults`, `repairer_repair_types`, `repair_events`, `audit_events` | `src\BA.Dmo.Infrastructure\Access\DapperRepairRepository.cs` |

Shared infrastructure dependency (implements the shared `IRepairUnitOfWorkFactory` declared in `Application\Shared\Persistence`; not counted as Reparação Externa-specific):

| Object | Implements | Principal behavior | DB objects referenced | Location |
|---|---|---|---|---|
| `DapperRepairUnitOfWorkFactory` | `IRepairUnitOfWorkFactory` | opens `DapperUnitOfWork.BeginAsync` for coordinated repair+Armazém write | — | `src\BA.Dmo.Infrastructure\Access\DapperRepairUnitOfWorkFactory.cs` |

External infra adapters consumed (declared/implemented in other modules — see §16): `DapperFerramentasPieceLookup` (Ferramentas piece lookup), `DapperArmazemRepairMovementRepository` (Armazém repair movement port).

## 9. Database Objects

Reparação Externa-specific tables (created in `N08_reparacoes.sql`):

### repair_exits
- Main technical role: external repair exit list (batch/shipment) aggregate row.
- PK: `repair_exit_id` (uuid).
- Relevant FKs: `repairer_id → repairers(repairer_id)`; `created_by → internal_users(actor_id)`.
- CHECK constraints (listed separately, not counted in DB object total): `ck_repair_exits_type (repair_type IN ('BQ','CM','MF'))`; `ck_repair_exits_status (status IN ('preparacao','a_retirar','enviado','retorno_parcial','concluido','cancelado'))`.
- Indexes: `ix_repair_exits_status (status)`; `ix_repair_exits_planned_date (planned_date)`.
- Other: column `repairer_snapshot jsonb` stores the per-send `RepairerSnapshot`.

### repair_exit_items
- Main technical role: item of an external exit list; BQ by quantity / CM-MF by individual number.
- PK: `repair_exit_item_id` (uuid).
- Relevant FKs: `repair_exit_id → repair_exits`; `bq_lote_id → bq_lotes`; `physical_piece_id → physical_pieces`; `out_operator_id → internal_users`; `in_operator_id → internal_users`.
- CHECK constraints: `ck_repair_exit_items_qty (qty IS NULL OR qty >= 0)`; `ck_repair_exit_items_kind ((bq_lote_id, physical_piece_id, qty, individual_number) XOR kind)`; `ck_repair_exit_items_status (status IN ('pendente','em_reparacao','devolvido'))` added by `N25_remediation.sql`.
- Index: `ix_repair_exit_items_exit (repair_exit_id)`.
- Per-item status domain values written by source: `pendente → em_reparacao → devolvido`.
- References `repair_events` (shared) via `repair_exit_item_id`.

### DB object count model
- RE-specific tables: 2 (`repair_exits`, `repair_exit_items`)
- RE-specific indexes: 3 (`ix_repair_exits_status`, `ix_repair_exits_planned_date`, `ix_repair_exit_items_exit`)
- RE-specific triggers: 0
- **RE-specific DB objects = 2 tables + 3 indexes + 0 triggers = 5**
- Constraints (CHECK/FK/UNIQUE/PK) listed separately, not counted.

Shared repair DB objects (not counted as RE-specific): `repairers`, `line_repairer_defaults`, `repair_events` (with `trg_repair_events_append_only`), `repairer_repair_types` (N20). Reparação Interna-specific: `internal_repair_records`.

### repair_events classification (shared)
`repair_events` is created in `N08_reparacoes.sql` with `repair_scope IN ('interna','externa')` — it serves both repair scopes. It is a shared dependency of Reparação Externa. Reparação Externa writes it mechanically via `DapperRepairRepository.InsertRepairEventAsync(... repair_scope='externa' ...)`. The table, `ix_repair_events_exit_item`, `ix_repair_events_internal` and `trg_repair_events_append_only` are shared, not counted in the RE-specific DB object total.

### RLS
`N12_rls.sql` enables RLS and creates a single technical policy `ba_dmo_app_access` (`FOR ALL ... USING (true)`) on both RE tables (`repair_exits`, `repair_exit_items`) and on shared repair tables (`repairers`, `line_repairer_defaults`, `repair_events`, `internal_repair_records`). `repairer_repair_types` (created in N20, after N12) receives RLS + `ba_dmo_app_access` in `N25_remediation.sql` §2 (post-N12 late table). No per-module/per-user policy exists; RLS is a shared technical layer.

## 10. Migration Touchpoints

Reparação Externa migration touchpoints (distinct files directly touching RE-specific objects):

| Migration | Reparação Externa Object(s) | Technical Change |
|---|---|---|
| N08_reparacoes.sql | `repair_exits`, `repair_exit_items` | creates both RE tables + indexes + CHECK constraints |
| N09_armazem.sql | `repair_exits` | `warehouse_movements.repair_exit_id` FK references `repair_exits` |
| N12_rls.sql | `repair_exits`, `repair_exit_items` | enables RLS, creates `ba_dmo_app_access` policy |
| N25_remediation.sql | `repair_exit_items` | adds `ck_repair_exit_items_status` CHECK |

Reparação Externa migration touchpoints: **4 distinct migration files**

Shared repair-object migrations (not RE-specific): `N20_repairer_repair_types.sql` (creates `repairer_repair_types`; RLS + policy added later by N25 §2), `N18_bq_repairer.sql` (adds `bq_movements.noted_repairer_id → repairers`, Boquilhas module).

## 11. Web / Routes

Route surface: `src\BA.Dmo.Web\Pages\ReparacaoExterna\` (page `@page "/reparacao-externa"`, policy `ModulePolicies.ReparacaoExterna`).

API endpoints (in `src\BA.Dmo.Web\Program.cs`, all `.RequireAuthorization(ModulePolicies.ReparacaoExterna)`):

| Route | HTTP | Technical entry point (service method) | Authorization | File |
|---|---|---|---|---|
| `/reparacao-externa` | GET page | `IndexModel.OnGet` (empty) | module policy | Pages\ReparacaoExterna\Index.cshtml(.cs) |
| `/api/reparacao-externa/tools` | GET | `ReparacaoExternaService.SearchToolsAsync` | module policy | Program.cs |
| `/api/reparacao-externa` | POST | `CreateExitAsync` | module policy | Program.cs |
| `/api/reparacao-externa` | GET | `ListExitsAsync` | module policy | Program.cs |
| `/api/reparacao-externa/{exitId:guid}` | GET | `GetExitAsync` | module policy | Program.cs |
| `/api/reparacao-externa/{exitId:guid}/items` | POST | `AddItemAsync` | module policy | Program.cs |
| `/api/reparacao-externa/{exitId:guid}/items/{itemId:guid}` | DELETE | `RemoveItemAsync` | module policy | Program.cs |
| `/api/reparacao-externa/{exitId:guid}/disponibilizar` | POST | `DisponibilizarExitAsync` | module policy | Program.cs |
| `/api/reparacao-externa/items/{itemId:guid}/recolha` | POST | `ConfirmPickupAsync` | module policy | Program.cs |
| `/api/reparacao-externa/items/{itemId:guid}/retorno` | POST | `ConfirmReturnAsync` | module policy | Program.cs |
| `/api/reparacao-externa/historico` | GET | `GetHistoryAsync` | module policy | Program.cs |
| `/api/reparacao-externa/repairers` | GET | `ListRepairersAsync` | module policy | Program.cs |
| `/api/reparacao-externa/repairers` | POST | `CreateRepairerAsync` | module policy | Program.cs |
| `/api/reparacao-externa/repairers/{repairerId:guid}` | PUT | `UpdateRepairerAsync` | module policy | Program.cs |
| `/api/reparacao-externa/repairers/{repairerId:guid}/deactivate` | POST | `DeactivateRepairerAsync` | module policy | Program.cs |
| `/api/reparacao-externa/line-defaults` | GET | `ListLineDefaultsAsync` | module policy | Program.cs |
| `/api/reparacao-externa/line-defaults` | POST | `UpsertLineDefaultAsync` | module policy | Program.cs |

Query helpers in `Program.cs`: `ParseRepairType` (BQ/CM/MF), `ParseRepairStatus` (six storage values).

Shared web wiring: `src\BA.Dmo.Web\Program.cs` hosts the RE API endpoint mappings, DI registration (`IRepairRepository→DapperRepairRepository`, `IToolPieceResolver→FerramentasRepairToolPieceResolver`, `IRepairUnitOfWorkFactory→DapperRepairUnitOfWorkFactory`, gate, service) and the module policy constant. Navigation (`_Navigation.cshtml`) renders modules generically from `IShellService`; no RE-specific wiring there.

## 12. Static Assets

Dedicated static asset files:

| Asset | Principal content | API routes called / selectors | Path |
|---|---|---|---|
| `reparacao-externa.js` | tab switching; CM/MF list builder search→add→create; envios list/detail + pickup/return; histórico; definições repairer management + line associations; localStorage last-seen repairer default | `GET /api/reparacao-externa/tools`, `POST /api/reparacao-externa`, `GET/POST /api/reparacao-externa/repairers`, `GET /api/reparacao-externa/historico`, `GET /api/reparacao-externa/{id}`, `POST .../disponibilizar`, `POST .../items/{id}/recolha`, `POST .../items/{id}/retorno`, `POST .../line-defaults` | `src\BA.Dmo.Web\wwwroot\scripts\reparacao-externa.js` |
| `reparacao-externa-layout.css` | module layout/composition only (grid, tabs, views, line-association list, autocomplete) | selectors `.reparacao-externa-*` | `src\BA.Dmo.Web\wwwroot\styles\modules\reparacao-externa-layout.css` |

Shared static consumers/references: the page uses shared `dmo-*` components (`.dmo-button`, `.dmo-card`, `.dmo-table`, `.dmo-field`, `.dmo-toast`) defined in the shared DMO CSS layer (`wwwroot\styles\dmo-*.css`).

## 13. Tests

| Test class | Kind | Direct target | Main method groups | Location |
|---|---|---|---|---|
| `ReparacaoExternaAuthorizationGateTests` | unit | `ReparacaoExternaAuthorizationGate.Require()` | module-grant success; no-identity fail-closed; no-module fail-closed | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaAuthorizationGateTests.cs` |
| `RepairExitStatusMachineTests` | unit | `RepairExitStatusMachine.ConfirmPickup/ConfirmReturn` | pickup on closed cycle; first pickup→ARetirar; all picked→Enviado; pickup after return rejected; partial→RetornoParcial; all→Concluido; return on cancelled rejected | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\RepairExitStatusMachineTests.cs` |
| `ReparacaoExternaServiceTests` | unit | `ReparacaoExternaService` | authorization fail-closed; BQ scope rejection; repairer snapshot; duplicate-in-open-exit; add/remove guards; atomic pickup/return with Armazém; partial/all return; position validation; deactivate-repairer; inactive line default | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\ReparacaoExternaServiceTests.cs` |
| `RepairerCapabilityTests` | unit | `ReparacaoExternaService` repairer-type capability | multi-type support; invalid type rejection; update types; list types; capability separate from line default | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\RepairerCapabilityTests.cs` |
| `ReparacaoExternaWebApiTests` | integration (WebApplicationFactory) | `/api/reparacao-externa/*` endpoints + module-policy guards | anonymous denied→login; authorized admitted; user without module denied→access-denied | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\ReparacaoExterna\ReparacaoExternaWebApiTests.cs` |

Test class count: **5**.

## 14. Test Doubles / Helpers

Dedicated support files (under `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\`):

| File | Contents |
|---|---|
| `ReparacaoExternaTestSupport.cs` | `ReparacaoExternaFixedClock` (IClock), `ReparacaoExternaFakeAuthorship` (IPersistenceAuthorshipAccessor), `ReparacaoExternaCurrentUser` (ICurrentUserAccessor; `Authorized()`/`WithoutModule()`), `FakeRepairUnitOfWorkFactory`, `FakeUnitOfWork` (IDbUnitOfWork no-op), `FakeArmazemRepairMovementPort` (IArmazemRepairMovementPort with Pickups/Returns + FailOnPickup/FailOnReturn), `FakeToolPieceResolver` (IToolPieceResolver with `Seed`) |
| `FakeRepairRepository.cs` | in-memory `IRepairRepository`; models exits/items/repairers/line defaults/types; records `CoordinatedWrites`; `FailItemWrite` switch; `Clone` helper |

Dedicated test support files: **2**.

In-file fixtures/helpers (nested fakes and builders inside test classes): `ReparacaoExternaWebApiTests` (nested `RepExtFixture`, `FakeAuthAdapter`, `FakeRepairRepo`, `FakeToolResolver`, `FakeArmazemRepair`, `FakeUowFactory`, `FakeUow`, `FakeIdentityRepository`); `ReparacaoExternaServiceTests` (`SeedRepairer`, `SeedPiece`, `CreateListAsync`); `RepairerCapabilityTests` (`Build`); `RepairExitStatusMachineTests` (`Item` builder). In-file test fixture files: **4**.

## 15. Direct Reparação Externa References

One edge per relationship (module-internal edges):

- `ReparacaoExternaAuthorizationGate` → `ReparacaoExternaModuleCatalog.ModuleId`
- `ReparacaoExternaService` → `IRepairRepository`
- `ReparacaoExternaService` → `IToolPieceResolver`
- `IToolPieceResolver` → `FerramentasRepairToolPieceResolver` (implementation)
- `IRepairRepository` → `DapperRepairRepository` (implementation)
- `DapperRepairRepository` → `repair_exits`
- `DapperRepairRepository` → `repair_exit_items`
- `DapperRepairRepository` → `repairers`
- `DapperRepairRepository` → `line_repairer_defaults`
- `DapperRepairRepository` → `repairer_repair_types`
- `DapperRepairRepository` → `repair_events` (shared, writes `repair_scope='externa'`)
- `DapperRepairRepository` → `audit_events`
- `RepairExit` → `RepairExitItem` (aggregate to items)
- `RepairExit` → `RepairerSnapshot` (stored per-exit)
- `RepairExitStatusMachine` → `RepairExitStatus`

## 16. External Technical References

| Reparação Externa Object | External Technical Reference | Reference Type |
|---|---|---|
| `ReparacaoExternaService` | `IArmazemRepairMovementPort` (Armazém) | application port |
| `ReparacaoExternaService` | `IRepairUnitOfWorkFactory` (shared Persistence) | application port |
| `ReparacaoExternaService` | `IClock` (Shared\Kernel) | constructor dependency |
| `ReparacaoExternaService` | `ICurrentUserAccessor`, `IPersistenceAuthorshipAccessor` (Shared\Access) | constructor dependency |
| `FerramentasRepairToolPieceResolver` | `IFerramentasPieceLookup` (Ferramentas) | application port |
| `FerramentasRepairToolPieceResolver` | `FerramentasToolType.CM/MF` (Ferramentas Domain) | enum/reference reuse |
| `IRepairRepository` | `IDbUnitOfWork` (Shared\Persistence) | application port |
| `DapperRepairRepository` | `repair_events` (shared N08) | shared DB dependency |
| `DapperRepairRepository` | `repairers` (shared N08) | shared DB dependency |
| `DapperRepairRepository` | `line_repairer_defaults` (shared N08) | shared DB dependency |
| `DapperRepairRepository` | `repairer_repair_types` (shared N20) | shared DB dependency |
| `DapperRepairRepository` | `audit_events` (shared catalog) | shared DB dependency |
| `DapperRepairRepository` | `internal_users` (actor FKs) | DB FK |
| `repair_exit_items` | `bq_lotes` (Boquilhas) | DB FK |
| `repair_exit_items` | `physical_pieces` (Ferramentas) | DB FK |
| `repair_exit_items` | `repair_events` (`repair_exit_item_id`) | DB FK |
| `repair_exits` | `warehouse_movements.repair_exit_id` (Armazém) | DB FK |
| `repairers` | `bq_movements.noted_repairer_id` (Boquilhas N18) | DB FK |
| `repair_exits`, `repair_exit_items` | RLS `ba_dmo_app_access` policy (N12) | shared DB dependency |
| `ReparacaoExternaListBuilderModel` | `ReparacaoExternaListBuilderModel` partial + `reparacao-externa.js` calls | shared static consumer / route reference |
| `DapperFerramentasPieceLookup` | implements `IFerramentasPieceLookup` (adapter for RE resolver) | constructor dependency (external infra) |
| `DapperArmazemRepairMovementRepository` | implements `IArmazemRepairMovementPort` (port of the Armazém module) | application port (external infra) |

## 17. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `RepairType` / `RepairTypeCodec` | Domain | `src\BA.Dmo.Domain\Modules\ReparacaoExterna\RepairType.cs` |
| `RepairExitStatus` / `RepairExitStatusCodec` | Domain | `...\RepairExitStatus.cs` |
| `RepairExit` | Domain | `...\RepairExit.cs` |
| `RepairExitStatusMachine` | Domain | `...\RepairExitStatusMachine.cs` |
| `RepairExitItem` | Domain | `...\RepairExitItem.cs` |
| `Repairer` / `RepairerSnapshot` | Domain | `...\Repairer.cs` / `...\RepairerSnapshot.cs` |
| `LineRepairerDefault` | Domain | `...\LineRepairerDefault.cs` |
| `RepairExitRules` | Domain | `...\RepairExitRules.cs` |
| `ReparacaoExternaModuleCatalog` | Domain | `...\ReparacaoExternaModuleCatalog.cs` |
| `ReparacaoExternaService` | Application | `src\BA.Dmo.Application\Modules\ReparacaoExterna\ReparacaoExternaService.cs` |
| `ReparacaoExternaAuthorizationGate` | Application | `...\ReparacaoExternaAuthorizationGate.cs` |
| `IRepairRepository` | Application | `...\IRepairRepository.cs` |
| `IToolPieceResolver` / `RepairToolIdentity` | Application | `...\IToolPieceResolver.cs` |
| `FerramentasRepairToolPieceResolver` | Application | `...\FerramentasRepairToolPieceResolver.cs` |
| Requests / DTOs | Application | `...\ReparacaoExternaRequests.cs` |
| `DapperRepairRepository` | Infrastructure (RE-specific) | `src\BA.Dmo.Infrastructure\Access\DapperRepairRepository.cs` |
| `DapperRepairUnitOfWorkFactory` | Infrastructure (shared dependency) | `...\DapperRepairUnitOfWorkFactory.cs` |
| `repair_exits` / `repair_exit_items` | Database | `database\migrations\N08_reparacoes.sql` (+N09/N12/N25) |
| Page + partial + model | Web pages | `src\BA.Dmo.Web\Pages\ReparacaoExterna\` |
| API endpoints + module policy | Shared web wiring | `src\BA.Dmo.Web\Program.cs` |
| `reparacao-externa.js` / `.css` | Static assets | `wwwroot\scripts\...` / `wwwroot\styles\modules\...` |
| Reparação Externa tests | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\ReparacaoExterna\` |

## 18. Sources Verified

- `maps\00_INDEX.md` (structural contract; Reparação Externa order 7, status COMPLETE; MAP-12)
- `src\BA.Dmo.Domain\Modules\ReparacaoExterna\` (10 files)
- `src\BA.Dmo.Application\Modules\ReparacaoExterna\` (6 files)
- `src\BA.Dmo.Application\Shared\Persistence\IRepairUnitOfWorkFactory.cs`
- `src\BA.Dmo.Application\Modules\Armazem\IArmazemRepairMovementPort.cs`
- `src\BA.Dmo.Application\Modules\Ferramentas\IFerramentasPieceLookup.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperRepairRepository.cs`, `DapperRepairUnitOfWorkFactory.cs`, `DapperArmazemRepairMovementRepository.cs`, `DapperFerramentasPieceLookup.cs`
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`
- `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`
- `src\BA.Dmo.Web\Program.cs` (DI + RE API endpoints)
- `src\BA.Dmo.Web\Pages\ReparacaoExterna\` (Index.cshtml, Index.cshtml.cs, _RepairListBuilder.cshtml, ReparacaoExternaListBuilderModel.cs)
- `src\BA.Dmo.Web\Pages\Shared\_Navigation.cshtml`
- `src\BA.Dmo.Web\wwwroot\scripts\reparacao-externa.js`, `wwwroot\styles\modules\reparacao-externa-layout.css`
- `database\migrations\N08_reparacoes.sql`, N09, N12, N18, N20, N25
- `database\consolidated_clean_install.sql`
- `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\ReparacaoExterna\` (6 files), `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\ReparacaoExterna\` (1 file)
- Cross-referenced: `maps\08_FERRAMENTAS.md` (piece lookup), `maps\09_ARMAZEM.md` (repair movement port)

## Counts

- Domain Reparação Externa files: 10
- Application Reparação Externa files: 6
- Infrastructure Reparação Externa files: 1 (`DapperRepairRepository`)
- Shared infrastructure dependency: 1 (`DapperRepairUnitOfWorkFactory`, implements shared `IRepairUnitOfWorkFactory`)
- Web dedicated page files: 4
- Dedicated static asset files: 2
- Shared web wiring files: 1 (`Program.cs`)
- Shared static asset files: shared DMO CSS layer (`dmo-*.css`)
- Shared application catalog files: `CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs` (shared Access catalog)
- Shared application persistence port: `IRepairUnitOfWorkFactory` in `Application\Shared\Persistence` (not a catalog file)
- Reparação Externa-specific DB tables: 2
- Reparação Externa-specific indexes: 3
- Reparação Externa-specific triggers: 0
- Reparação Externa-specific DB objects: 5 (2 tables + 3 indexes + 0 triggers)
- Shared repair DB objects: repairers, line_repairer_defaults, repair_events (+ trg_repair_events_append_only), repairer_repair_types
- Distinct migration files: 4 (N08, N09, N12, N25)
- Test classes: 5
- Dedicated test support files: 2
- In-file test fixtures: 4
- Source-visible user surfaces: 1 (Shared)