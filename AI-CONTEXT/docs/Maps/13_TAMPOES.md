# BA DMO — Tampões Technical Map

MAP ID: MAP-13
Status: COMPLETE

Canonical Module: Tampões
Index order: 8
Index route: `/tampoes`

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
- 15. Direct Tampões References
- 16. External Technical References
- 17. Target-to-Layer Index
- 18. Sources Verified
- Counts

## 1. Scope

Tampões is one canonical top-level module (INDEX order 8). The module source contains: configurable comparable fields (`tampao_field_defs`, `tampao_field_values`), technical configurations (`tampao_configurations`), two balances per configuration (`tampao_saldos`), immutable quantity movements (`tampao_movements`), planned needs (`tampao_planos`), and the multi-machine association (`tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event`). A single page surface exposes six tab sections (Registo, Consulta, Planeamento, Histórico, Linhas e Máquinas, Opções). The authorization gate checks `user.HasModule("tampoes")`; no capability or profile split exists.

## 2. Layer Summary

| Layer | Location | Count |
|---|---|---|
| Domain | `src\BA.Dmo.Domain\Modules\Tampoes\` | 11 |
| Application | `src\BA.Dmo.Application\Modules\Tampoes\` | 5 |
| Infrastructure | `src\BA.Dmo.Infrastructure\Access\` (DapperTampaoRepository, DapperTampoesUnitOfWorkFactory) | 2 |
| Web pages | `src\BA.Dmo.Web\Pages\Tampoes\` | 2 |
| Static assets | `wwwroot\scripts\tampoes.js`, `wwwroot\styles\modules\tampoes-layout.css` | 2 |
| Tests | `tests\...\Modules\Tampoes\`, `tests\...\IntegrationTests\Tampoes\` | 4 |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | YES | `src\BA.Dmo.Domain\Modules\Tampoes\` |
| Application | YES | `src\BA.Dmo.Application\Modules\Tampoes\` |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\DapperTampaoRepository.cs`, `DapperTampoesUnitOfWorkFactory.cs` |
| Web | YES | `src\BA.Dmo.Web\Pages\Tampoes\`; `src\BA.Dmo.Web\Program.cs`; `Authorization\ModuleAuthorizationHandler.cs` |
| Database | YES | `database\migrations\N10_tampoes.sql`, `N21_tampoes_machines.sql`, `N12_rls.sql`, `N25_remediation.sql` |
| Tests | YES | `tests\BA.Dmo.UnitTests\Modules\Tampoes\`, `tests\BA.Dmo.IntegrationTests\Tampoes\` |

This is technical navigation only; it does not explain workflow.

## 3. Domain Objects

Location: `src\BA.Dmo.Domain\Modules\Tampoes\`

| Object | Kind | Notes / members | File |
|---|---|---|---|
| `TampoesModuleCatalog` | static catalog | `ModuleId = "tampoes"`; `DefaultDiameterField = "Diâmetro"`, `DefaultCaloteField = "Profundidade/Calote"` | TampoesModuleCatalog.cs |
| `TampaoFieldDef` | entity (sealed class) | `TampaoFieldDefId`, `FieldName`, `Unit`, `PrecisionDigits`, `DisplayOrder`, `Active` (default true), `CreatedAtUtc/UpdatedAtUtc` | TampaoFieldDef.cs |
| `TampaoFieldValue` | entity (sealed class) | `TampaoFieldValueId`, `TampaoFieldDefId`, `ValueNumeric`, `ValueLabel`, `DisplayOrder`, `Active` | TampaoFieldValue.cs |
| `TampaoConfiguration` | entity (sealed class) | `TampaoConfigurationId`, `Values` (`IReadOnlyDictionary<string,decimal>` sorted), `Active`, `CreatedAtUtc`, `CreatedBy`; method `DiffersFrom(TampaoConfiguration)` | TampaoConfiguration.cs |
| `TampaoConfigurationKey` | static codec | `Serialize(IReadOnlyDictionary<string,decimal>)` returns deterministic JSON key (sorted, normalized 4-dp) | TampaoConfiguration.cs |
| `TampaoBalanceKind` | enum | `Enchidos`, `PorEncher` | TampaoBalanceKind.cs |
| `TampaoBalanceKindCodec` | static codec | `ToKey`/`FromKey` over `"enchidos"|"por_encher"` | TampaoBalanceKind.cs |
| `TampaoSaldo` | entity (sealed class) | `TampaoSaldoId`, `TampaoConfigurationId`, `Enchidos`, `PorEncher`, `UpdatedAtUtc`; `Get(TampaoBalanceKind)`, `IsNonNegative` | TampaoSaldo.cs |
| `TampaoMovementType` | enum | `Adicionar`, `Remover`, `AlterarEstado`, `AlterarConfiguracao` | TampaoMovementType.cs |
| `TampaoMovementTypeCodec` | static codec | `ToStorage`/`FromStorage` over `"adicionar"|"remover"|"alterar_estado"|"alterar_configuracao"` | TampaoMovementType.cs |
| `TampaoMovement` | entity (sealed class) | `TampaoMovementId`, `MovementType`, `OriginConfigurationId?`, `DestinationConfigurationId?`, `Qty`, `BalancesBefore`, `BalancesAfter` (jsonb), `ActorId`, `OccurredAtUtc`; `IsSingleBalance` | TampaoMovement.cs |
| `TampaoPlano` | entity (sealed class) | `TampaoPlanoId`, `TampaoConfigurationId`, `PlannedQty`, `PlannedForDate?`, `JobOnId?`, `ProductionCode?`, `Notes?`, `Canceled` (default false), `CreatedAtUtc/By`, `UpdatedAtUtc` | TampaoPlano.cs |
| `TampaoRules` | static rules | consts `NegativeBalanceCode`, `DestinationEqualsOriginCode`, `InvalidQuantityCode`, `InsufficientOriginCode`, `NoCharacteristicChangedCode`; methods `ValidateQuantity`, `ApplySingleBalanceChange`, `ResolveStateOrigin`, `ApplyBalanceTransfer`, `ValidateConfigurationTransform`, `NormalizeValue` | TampaoRules.cs |
| `TampaoMachine` | static canonical machine set | consts `B1`..`C3`; `All` (B1–C3); `IsValid(string)`, `Validate(string)` (error `TAMPAO_INVALID_MACHINE`) | TampaoMachine.cs |
| `TampaoConfigurationNote` | entity (sealed class) | `TampaoConfigurationNoteId`, `TampaoConfigurationId`, `Note`, `ActorId?`, `OccurredAtUtc` | TampaoMachine.cs |
| `TampaoMachineEvent` | entity (sealed class) | `TampaoConfigurationMachineEventId`, `TampaoConfigurationId`, `Machine`, `Action` ("added"/"removed"), `ActorId?`, `OccurredAtUtc` | TampaoMachine.cs |

## 4. Application Objects

Location: `src\BA.Dmo.Application\Modules\Tampoes\`

| Object | Kind | Principal members | File |
|---|---|---|---|
| `TampaoAuthorizationGate` | gate | ctor `(ICurrentUserAccessor, IPersistenceAuthorshipAccessor)`; `Require()` returns `Result<TampaoExecutor, DomainError>`; fail-closed error `TAMPAO_FORBIDDEN` | TampaoAuthorizationGate.cs |
| `TampaoExecutor` | record | `(string ActorId, string DisplayName)` | TampaoAuthorizationGate.cs |
| `ITampaoRepository` | port | field defs/values CRUD; configurations/saldos read + `CreateConfigurationAsync`; `GetSaldoInTransactionAsync`, `SetSaldoAsync`, `InsertMovementAsync` (within `IDbUnitOfWork`); movement list; machine/note methods; planning CRUD; `InsertAuditEventAsync` | ITampaoRepository.cs |
| `ITampoesUnitOfWorkFactory` | port | `BeginAsync(CancellationToken)` → `IDbUnitOfWork` | ITampoesUnitOfWorkFactory.cs |
| `TampaoService` | service | public methods: `ConsultarAsync`, `GetConfigurationAsync`, `GetConfigurationDetailAsync`, `SetConfigurationMachinesAsync`, `AddConfigurationNoteAsync`, `AdicionarQuantidadeAsync`, `RemoverQuantidadeAsync`, `AlterarEstadoAsync`, `AlterarConfiguracaoAsync`, `PlanearAsync`, `CancelarPlanoAsync`, `ListPlanosAsync`, `ListMovimentosAsync`, `ListFieldDefsAsync`, `ListFieldValuesAsync`, `CreateFieldDefAsync`, `UpdateFieldDefAsync`, `CreateFieldValueAsync`, `UpdateFieldValueAsync` | TampaoService.cs |
| Requests | records (commands) | `AdicionarQuantidadeRequest`, `RemoverQuantidadeRequest`, `AlterarEstadoRequest`, `AlterarConfiguracaoRequest`, `PlanearRequest`, `CancelarPlanoRequest`, `SetConfigurationMachinesRequest`, `AddConfigurationNoteRequest` | TampaoRequests.cs |
| Query filters | records | `ConsultaFilter(Guid? ConfigurationId, string? Machine)`, `PlanoFilter(Guid? ConfigurationId, DateOnly? From, DateOnly? To, bool IncludeCanceled)` | TampaoRequests.cs |
| DTOs | records | `TampaoFieldDefDto`, `TampaoFieldValueDto`, `TampaoConfigurationDto`, `TampaoConfigurationDetailDto`, `TampaoMovimentoDto`, `TampaoPlanoDto`, `TampaoMachineDto`, `TampaoConfigurationNoteDto`, `TampaoMachineEventDto` | TampaoRequests.cs |

`TampaoService` ctor dependencies: `ITampaoRepository`, `ITampoesUnitOfWorkFactory`, `TampaoAuthorizationGate`, `IClock`.

Application/domain error codes emitted: `TAMPAO_FORBIDDEN`, `TAMPAO_NEGATIVE_BALANCE`, `TAMPAO_DESTINATION_EQUALS_ORIGIN`, `TAMPAO_INVALID_QUANTITY`, `TAMPAO_INSUFFICIENT_ORIGIN`, `TAMPAO_NO_CHARACTERISTIC_CHANGED`, `TAMPAO_INVALID_MACHINE`, `TAMPAO_NOTE_REQUIRED`, `TAMPAO_FIELD_NAME_REQUIRED`, `TAMPAO_SAVE_FAILED`, `TAMPAO_NOT_FOUND`.

Audit action_code literals written via `InsertAuditEventAsync` (`module_id = 'tampoes'`): `tampoes.quantidade.adicionar`, `tampoes.quantidade.remover`, `tampoes.estado.alterar`, `tampoes.configuracao.alterar`, `tampoes.configuracao.maquinas`, `tampoes.configuracao.observacao`, `tampoes.planear`, `tampoes.plano.cancelar`. Entity types written: `tampao_configuration`, `tampao_plano`.

## 5. Application Contracts / Ports

| Port | Principal methods | Path | Implementation(s) | Direct external dependency |
|---|---|---|---|---|
| `ITampaoRepository` | field/value/config/saldo/movement/plano CRUD; machine/note methods; `InsertAuditEventAsync`; atomic writes within `IDbUnitOfWork` | `src\BA.Dmo.Application\Modules\Tampoes\ITampaoRepository.cs` | `DapperTampaoRepository` | `IDbConnectionFactory`; DB `tampao_*`, `audit_events` |
| `ITampoesUnitOfWorkFactory` | `BeginAsync` | `src\BA.Dmo.Application\Modules\Tampoes\ITampoesUnitOfWorkFactory.cs` | `DapperTampoesUnitOfWorkFactory` | `IDbConnectionFactory` → `DapperUnitOfWork` |

## 6. Authorization / Catalog Objects

| Object | Value | Location |
|---|---|---|
| Module id | `tampoes` (`CanonicalModuleCatalog.TampoesModuleId`) | `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` |
| Module definition | `(tampoes, "Tampões", ModuleKind.Module, order 80, "/tampoes")`, no declared capabilities | CanonicalModuleCatalog.cs |
| Page id | `tampoes.quantidades` (`CanonicalPageCatalog.TampoesQuantidadesPageId`), route `/tampoes`, `requiredCapabilityId: null`, displayOrder 80 | `src\BA.Dmo.Application\Shared\Access\CanonicalPageCatalog.cs` |
| Module entry policy | `BaDmo.Module.tampoes` (`ModulePolicies.Tampoes`) | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| Module authorization handler | `ModuleAuthorizationHandler` — succeeds when `user.HasModule(requirement.ModuleId)`; fail closed | ModuleAuthorizationHandler.cs |
| Web page policy attribute | `[Authorize(Policy = ModulePolicies.Tampoes)]` on `Index.cshtml` | `src\BA.Dmo.Web\Pages\Tampoes\Index.cshtml` |
| Application gate | `TampaoAuthorizationGate.Require()` calls `user.HasModule(TampoesModuleCatalog.ModuleId)`; returns `TAMPAO_FORBIDDEN` when no identity/module/actor | `src\BA.Dmo.Application\Modules\Tampoes\TampaoAuthorizationGate.cs` |
| Capability policy | none declared for Tampões | `CapabilityPolicies` in ModuleAuthorizationHandler.cs |

All endpoints in `Program.cs` use `.RequireAuthorization(ModulePolicies.Tampoes)`. Fails closed (no resolved identity → `TAMPAO_FORBIDDEN`).

## 7. User Surfaces

Source defines a single module surface. Evidence: one page route `/tampoes`; one `IndexModel`; the authorization gate checks `user.HasModule("tampoes")`; no capability or profile checks are present in the inspected page/code-behind/gate/JS. The six tab views are rendered in the one shared page.

User Surface: **Shared**

The six tab sections in `Index.cshtml` are shared: `registo`, `consulta`, `planeamento`, `historico`, `linhas`, `opcoes`. All rendered for every authorized user; no profile distinction in source.

## 8. Infrastructure Objects

| Object | Implements | Principal behavior | DB objects referenced | Location |
|---|---|---|---|---|
| `DapperTampaoRepository` | `ITampaoRepository` | Dapper CRUD for field defs/values, configurations, saldos, movements, planos, machines, notes, machine events, audit; atomic writes via shared `IDbUnitOfWork`; `GetSaldoInTransactionAsync` runs `SELECT ... FROM tampao_saldos ... FOR UPDATE` (explicit row lock); `SetSaldoAsync` upsert `ON CONFLICT (tampao_configuration_id) DO UPDATE` | `tampao_field_defs`, `tampao_field_values`, `tampao_configurations`, `tampao_saldos`, `tampao_movements`, `tampao_planos`, `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event`, `audit_events` | `src\BA.Dmo.Infrastructure\Access\DapperTampaoRepository.cs` |
| `DapperTampoesUnitOfWorkFactory` | `ITampoesUnitOfWorkFactory` | `BeginAsync` → `DapperUnitOfWork.BeginAsync` for the atomic Tampões write | `IDbConnectionFactory` → `DapperUnitOfWork` | `src\BA.Dmo.Infrastructure\Access\DapperTampoesUnitOfWorkFactory.cs` |

Shared infrastructure dependencies (referenced, not Tampões-specific): `IDbConnectionFactory` (`src\BA.Dmo.Infrastructure\Persistence\DatabaseConnectionSettings.cs`), `DapperUnitOfWork` (`src\BA.Dmo.Infrastructure\Persistence\DapperUnitOfWork.cs`), `PersistenceMappings` type bridging (`DateTimeOffsetHandler`, `.ToDateTimeOffset()`).

## 9. Database Objects

Tampões-specific tables (N10: 6, N21: 3):

### N10 tables (introduced in N10_tampoes.sql)
- `tampao_field_defs` — PK `tampao_field_def_id uuid`; UNIQUE `field_name`; columns `unit`, `precision_digits`, `display_order`, `active`, `created_at_utc`, `updated_at_utc`.
- `tampao_field_values` — PK `tampao_field_value_id uuid`; FK `tampao_field_def_id → tampao_field_defs`; UNIQUE `uq_tampao_field_values (tampao_field_def_id, value_numeric)`; INDEX `ix_tampao_field_values_field (tampao_field_def_id, active, value_numeric)`.
- `tampao_configurations` — PK `tampao_configuration_id uuid`; FK `created_by → internal_users (actor_id)`; UNIQUE `uq_tampao_configurations_values (values_json)`.
- `tampao_saldos` — PK `tampao_saldo_id uuid`; `tampao_configuration_id uuid NOT NULL UNIQUE` FK → `tampao_configurations` (1:1); CHECK `ck_tampao_saldos_enchidos (enchidos >= 0)`, `ck_tampao_saldos_por_encher (por_encher >= 0)`.
- `tampao_movements` — PK `tampao_movement_id uuid`; FKs `origin_configuration_id → tampao_configurations`, `destination_configuration_id → tampao_configurations`, `actor_id → internal_users (actor_id)`; CHECK `ck_tampao_movements_type`, `ck_tampao_movements_qty (qty >= 1)`; INDEX `ix_tampao_movements_origin`, `ix_tampao_movements_occurred`; TRIGGER `trg_tampao_movements_append_only` (BEFORE UPDATE OR DELETE → `ba_dmo_guard_append_only`).
- `tampao_planos` — PK `tampao_plano_id uuid`; FK `tampao_configuration_id → tampao_configurations`, `created_by → internal_users (actor_id)`; `job_on_id uuid` (no FK); CHECK `ck_tampao_planos_qty (planned_qty >= 1)`; INDEX `ix_tampao_planos_configuration`, `ix_tampao_planos_date`.

### N21 tables (introduced in N21_tampoes_machines.sql)
- `tampao_configuration_machines` — composite PK `(tampao_configuration_id, machine)`; FK `tampao_configuration_id → tampao_configurations`; CHECK `ck_tampao_configuration_machines_machine (machine IN ('B1','B2','B3','C1','C2','C3'))`; INDEX `ix_tampao_configuration_machines_machine`.
- `tampao_configuration_notes` — PK `tampao_configuration_note_id uuid`; FK `tampao_configuration_id → tampao_configurations`, `actor_id → internal_users (actor_id)`; INDEX `ix_tampao_configuration_notes_config (tampao_configuration_id, occurred_at_utc)`; TRIGGER `trg_tampao_configuration_notes_append_only`.
- `tampao_configuration_machine_event` — PK `tampao_configuration_machine_event_id uuid`; FK `tampao_configuration_id → tampao_configurations`, `actor_id → internal_users (actor_id)`; CHECK `ck_tampao_configuration_machine_event_action (action IN ('added','removed'))`, `ck_tampao_configuration_machine_event_machine (machine IN ('B1'..'C3'))`; INDEX `ix_tampao_configuration_machine_event_config`; TRIGGER `trg_tampao_configuration_machine_event_append_only`.

### DB object count model
- Tampões-specific tables: 9 (`tampao_field_defs`, `tampao_field_values`, `tampao_configurations`, `tampao_saldos`, `tampao_movements`, `tampao_planos`, `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event`)
- Tampões-specific indexes: 8 (`ix_tampao_field_values_field`, `ix_tampao_movements_origin`, `ix_tampao_movements_occurred`, `ix_tampao_planos_configuration`, `ix_tampao_planos_date`, `ix_tampao_configuration_machines_machine`, `ix_tampao_configuration_notes_config`, `ix_tampao_configuration_machine_event_config`)
- Tampões-specific triggers: 3 (`trg_tampao_movements_append_only`, `trg_tampao_configuration_notes_append_only`, `trg_tampao_configuration_machine_event_append_only`)
- **Tampões-specific DB objects = 9 tables + 8 indexes + 3 triggers = 20**
- Constraints (CHECK/FK/UNIQUE/PK) listed separately, not counted.

Shared DB objects referenced (not counted as Tampões-specific): `internal_users` (actor FKs), `audit_events` (written by `DapperTampaoRepository.InsertAuditEventAsync` with `module_id = 'tampoes'`), `codecs`/check `ba_dmo_guard_append_only` function (shared).

### RLS
`N12_rls.sql` enables RLS and creates the technical policy `ba_dmo_app_access` (`FOR ALL ... USING (true)`) on the 6 N10 tables. `N25_remediation.sql` enables RLS + `ba_dmo_app_access` on the 3 N21 tables (late tables added after N12). No per-module/per-user policy; RLS is a shared technical layer.

## 10. Migration Touchpoints

Tampões migration touchpoints (distinct files directly touching Tampões-specific objects):

| Migration | Tampões Object(s) | Technical Change |
|---|---|---|
| N10_tampoes.sql | `tampao_field_defs`, `tampao_field_values`, `tampao_configurations`, `tampao_saldos`, `tampao_movements`, `tampao_planos` | creates 6 tables + 5 indexes + CHECK/UNIQUE constraints + `trg_tampao_movements_append_only` |
| N12_rls.sql | 6 N10 tables | enables RLS, creates `ba_dmo_app_access` policy |
| N21_tampoes_machines.sql | `tampao_configuration_machines`, `tampao_configuration_notes`, `tampao_configuration_machine_event` | creates 3 tables + 3 indexes + CHECK constraints + `trg_tampao_configuration_notes_append_only` + `trg_tampao_configuration_machine_event_append_only` |
| N25_remediation.sql | 3 N21 tables | enables RLS, creates `ba_dmo_app_access` policy, REVOKE, explicit `ba_dmo_app` DML grants |

Tampões migration touchpoints: **4 distinct migration files**

## 11. Web / Routes

Route surface: `src\BA.Dmo.Web\Pages\Tampoes\` (page `@page "/tampoes"`, policy `ModulePolicies.Tampoes`).

API endpoints (in `src\BA.Dmo.Web\Program.cs`, all `.RequireAuthorization(ModulePolicies.Tampoes)`):

| Route | HTTP | Technical entry point (service method) | Authorization | File |
|---|---|---|---|---|
| `/tampoes` | GET page | `IndexModel.OnGet` (empty) | module policy | Pages\Tampoes\Index.cshtml(.cs) |
| `/api/tampoes/consulta` | GET | `ConsultarAsync` | module policy | Program.cs |
| `/api/tampoes/configuracao/{configurationId:guid}` | GET | `GetConfigurationAsync` | module policy | Program.cs |
| `/api/tampoes/configuracao/{configurationId:guid}/detalhe` | GET | `GetConfigurationDetailAsync` | module policy | Program.cs |
| `/api/tampoes/configuracao/{configurationId:guid}/maquinas` | POST | `SetConfigurationMachinesAsync` | module policy | Program.cs |
| `/api/tampoes/configuracao/{configurationId:guid}/observacao` | POST | `AddConfigurationNoteAsync` | module policy | Program.cs |
| `/api/tampoes/quantidade/adicionar` | POST | `AdicionarQuantidadeAsync` | module policy | Program.cs |
| `/api/tampoes/quantidade/remover` | POST | `RemoverQuantidadeAsync` | module policy | Program.cs |
| `/api/tampoes/estado/alterar` | POST | `AlterarEstadoAsync` | module policy | Program.cs |
| `/api/tampoes/configuracao/alterar` | POST | `AlterarConfiguracaoAsync` | module policy | Program.cs |
| `/api/tampoes/planos` | GET | `ListPlanosAsync` | module policy | Program.cs |
| `/api/tampoes/planear` | POST | `PlanearAsync` | module policy | Program.cs |
| `/api/tampoes/planos/{planoId:guid}/cancelar` | POST | `CancelarPlanoAsync` | module policy | Program.cs |
| `/api/tampoes/movimentos` | GET | `ListMovimentosAsync` | module policy | Program.cs |
| `/api/tampoes/opcoes/fields` | GET | `ListFieldDefsAsync` | module policy | Program.cs |
| `/api/tampoes/opcoes/fields/{fieldDefId:guid}/values` | GET | `ListFieldValuesAsync` | module policy | Program.cs |
| `/api/tampoes/opcoes/fields` | POST | `CreateFieldDefAsync` | module policy | Program.cs |
| `/api/tampoes/opcoes/values` | POST | `CreateFieldValueAsync` | module policy | Program.cs |

Query helpers in `Program.cs`: `ParseTampaoBalance` (Enchidos/PorEncher), `ParseTampaoMovementType` (adicionar/remover/alterar_estado/alterar_configuracao).

Shared web wiring: `src\BA.Dmo.Web\Program.cs` hosts the Tampões API endpoint mappings and DI registration (`ITampoesUnitOfWorkFactory→DapperTampoesUnitOfWorkFactory`, `ITampaoRepository→DapperTampaoRepository`, gate, service). Navigation (`_Navigation.cshtml`) renders modules generically from `IShellService`; no Tampões-specific wiring there.

## 12. Static Assets

Dedicated static asset files:

| Asset | Principal content | API routes called / selectors | Path |
|---|---|---|---|
| `tampoes.js` | tab switching; Registo add/remove; Consulta select/double-click + balance blocks + alterar estado/configuração cards + detail sheet (machines/comments/history); Planeamento planear/cancel; Histórico filters; Opções field/value management; Linhas e Máquinas panel/detail | `GET /api/tampoes/consulta`, `GET/POST /api/tampoes/opcoes/fields...`, `POST /api/tampoes/quantidade/*`, `POST /api/tampoes/estado/alterar`, `POST /api/tampoes/configuracao/alterar`, `GET /api/tampoes/configuracao/{id}/detalhe`, `POST .../maquinas`, `POST .../observacao`, `GET /api/tampoes/planos`, `POST /api/tampoes/planear`, `POST /api/tampoes/planos/{id}/cancelar`, `GET /api/tampoes/movimentos` | `src\BA.Dmo.Web\wwwroot\scripts\tampoes.js` |
| `tampoes-layout.css` | module layout/composition only (tabs, views, inline cards, saldo blocks, detail, linhas grid) | selectors `.tampoes-*`, `#consultaTable`, `#planosTable` | `src\BA.Dmo.Web\wwwroot\styles\modules\tampoes-layout.css` |

Shared static consumers/references: the page uses shared `dmo-*` components (`.dmo-button`, `.dmo-card`, `.dmo-table`, `.dmo-field`, `.dmo-toast`, `.dmo-*`) defined in the shared DMO CSS layer (`wwwroot\styles\dmo-*.css`). The Linhas tab renders machine options from `BA.Dmo.Domain.Modules.Boquilhas.BoquilhasModuleCatalog.Lines` (`Index.cshtml` Razor `@foreach`).

## 13. Tests

| Test class | Kind | Direct target | Main method groups | Location |
|---|---|---|---|---|
| `TampaoDomainTests` | unit | `TampaoRules` + `TampaoConfigurationKey` | value normalization; key insertion-order stability; `ValidateQuantity`; `ApplySingleBalanceChange` never negative; `ResolveStateOrigin` opposite/in-sufficient; `ApplyBalanceTransfer` destination-equals-origin blocked; `ValidateConfigurationTransform` requires different id + changed characteristic | `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoDomainTests.cs` |
| `TampaoServiceTests` | unit | `TampaoService` | add/remove chosen balance + movement; save-failure preserves input; alterar estado atomic single movement; insufficient origin blocked; alterar configuração create/reuse destination; no-characteristic changed blocked; planning does not reserve; cancel plan preserves balances; deactivate field value keeps configs/history; authorization fail-closed; movement filter by type | `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoServiceTests.cs` |
| `TampaoMachineTests` | unit | `TampaoService` (machines/notes/detail) | assign/remove machines B1–C3; invalid machine rejected; notes persist + history kept; machine filter returns record once; no config duplication; detail sheet returns machines/notes/events; invalid machine filter rejected | `tests\BA.Dmo.UnitTests\Modules\Tampoes\TampaoMachineTests.cs` |
| `TampaoWebApiTests` | integration (WebApplicationFactory) | `/api/tampoes/*` endpoints + module-policy guards | anonymous denied→login; authorized tampoes user admitted; user without module denied→access-denied | `tests\BA.Dmo.IntegrationTests\Tampoes\TampaoWebApiTests.cs` |

Test class count: **4**.

## 14. Test Doubles / Helpers

Dedicated support file:

| File | Contents |
|---|---|
| `TampaoTestSupport.cs` | `TampaoFixedClock` (IClock), `TampaoFakeAuthorship` (IPersistenceAuthorshipAccessor), `TampaoCurrentUser` (ICurrentUserAccessor, `Authorized()`/`WithoutModule()`), `FakeTampoesUnitOfWorkFactory`, `FakeTampaoUnitOfWork` (IDbUnitOfWork no-op), `FakeTampaoRepository` (in-memory ITampaoRepository with FieldDefs/FieldValues/Configurations/Saldos/Movements/Planos/AuditEvents/MachineEvents/ConfigurationNotes/ConfigurationMachines; `FailTransaction` switch; `SeedConfiguration` builder) |

Dedicated test support files: **1**.

In-file fixtures/helpers (nested fakes and builders inside test classes): `TampaoWebApiTests` (nested `TampoesFixture`, `FakeAuthAdapter`, `FakeRepo`, `FakeUowFactory`, `FakeUow`, `FakeIdentityRepository`). In-file test fixture files: **1**.

## 15. Direct Tampões References

One edge per relationship (module-internal edges):

- `TampaoAuthorizationGate` → `TampoesModuleCatalog.ModuleId`
- `TampaoService` → `ITampaoRepository`
- `TampaoService` → `ITampoesUnitOfWorkFactory`
- `TampaoService` → `TampaoAuthorizationGate`
- `ITampaoRepository` → `DapperTampaoRepository` (implementation)
- `ITampoesUnitOfWorkFactory` → `DapperTampoesUnitOfWorkFactory` (implementation)
- `DapperTampaoRepository` → `tampao_field_defs`
- `DapperTampaoRepository` → `tampao_field_values`
- `DapperTampaoRepository` → `tampao_configurations`
- `DapperTampaoRepository` → `tampao_saldos`
- `DapperTampaoRepository` → `tampao_movements`
- `DapperTampaoRepository` → `tampao_planos`
- `DapperTampaoRepository` → `tampao_configuration_machines`
- `DapperTampaoRepository` → `tampao_configuration_notes`
- `DapperTampaoRepository` → `tampao_configuration_machine_event`
- `DapperTampaoRepository` → `audit_events` (module 'tampoes')
- `TampaoConfigurationKey` → `TampaoConfiguration.Values` (serialization)
- `TampaoService` → `TampaoConfigurationKey.Serialize`
- `TampaoService` → `TampaoRules`
- `TampaoRules` → `TampaoSaldo.Get`
- `TampaoService` → `TampaoMachine.Validate`
- `DapperTampaoRepository` → `TampaoMovementTypeCodec` / `TampaoBalanceKindCodec`

## 16. External Technical References

| Tampões Object | External Technical Reference | Reference Type |
|---|---|---|
| `TampaoService` | `IClock` (Shared\Kernel) | constructor dependency |
| `TampaoAuthorizationGate` | `ICurrentUserAccessor` (Shared\Access) | constructor dependency |
| `TampaoAuthorizationGate` | `IPersistenceAuthorshipAccessor` (Shared\Persistence) | constructor dependency |
| `TampaoService` | `IDbUnitOfWork` (Shared\Persistence, via `ITampoesUnitOfWorkFactory`) | application port |
| `DapperTampoesUnitOfWorkFactory` | `DapperUnitOfWork` (Infrastructure\Persistence) | constructor dependency (external infra) |
| `DapperTampaoRepository` / `DapperTampoesUnitOfWorkFactory` | `IDbConnectionFactory` (Infrastructure\Persistence) | constructor dependency (external infra) |
| `DapperTampaoRepository` | `audit_events` (shared catalog) | shared DB dependency |
| `tampao_configurations` | `internal_users` (actor `created_by` FK) | DB FK |
| `tampao_movements` | `internal_users` (`actor_id` FK) | DB FK |
| `tampao_planos` | `internal_users` (`created_by` FK); `job_on_id` (no FK, read-only reference) | DB FK / shared reference |
| `tampao_configuration_notes` | `internal_users` (`actor_id` FK) | DB FK |
| `tampao_configuration_machine_event` | `internal_users` (`actor_id` FK) | DB FK |
| `tampao_*` (N10) | RLS `ba_dmo_app_access` policy (N12) | shared DB dependency |
| `tampao_*` (N21) | RLS `ba_dmo_app_access` policy (N25) | shared DB dependency |
| `tampao_movements` / `tampao_configuration_notes` / `tampao_configuration_machine_event` | `ba_dmo_guard_append_only` trigger function (shared) | shared DB dependency |
| `tampao_planos` | `JobOnId` ↔ Job On (read-only, no FK) | shared reference |
| `Index.cshtml` Linhas tab | `BoquilhasModuleCatalog.Lines` (Boquilhas Domain) | shared static consumer / enum reuse |
| `CanonicalModuleCatalog` | `TampoesModuleId`, module definition | shared application catalog |
| `CanonicalPageCatalog` | `TampoesQuantidadesPageId`, route `/tampoes` | shared application catalog |
| `ModuleAuthorizationHandler` (Web) | `CanonicalModuleCatalog.TampoesModuleId` policy constant | shared web wiring |

## 17. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `TampoesModuleCatalog` | Domain | `src\BA.Dmo.Domain\Modules\Tampoes\TampoesModuleCatalog.cs` |
| `TampaoFieldDef` | Domain | `...\TampaoFieldDef.cs` |
| `TampaoFieldValue` | Domain | `...\TampaoFieldValue.cs` |
| `TampaoConfiguration` / `TampaoConfigurationKey` | Domain | `...\TampaoConfiguration.cs` |
| `TampaoBalanceKind` / codec | Domain | `...\TampaoBalanceKind.cs` |
| `TampaoSaldo` | Domain | `...\TampaoSaldo.cs` |
| `TampaoMovementType` / codec | Domain | `...\TampaoMovementType.cs` |
| `TampaoMovement` | Domain | `...\TampaoMovement.cs` |
| `TampaoPlano` | Domain | `...\TampaoPlano.cs` |
| `TampaoRules` | Domain | `...\TampaoRules.cs` |
| `TampaoMachine` / `TampaoConfigurationNote` / `TampaoMachineEvent` | Domain | `...\TampaoMachine.cs` |
| `TampaoService` | Application | `src\BA.Dmo.Application\Modules\Tampoes\TampaoService.cs` |
| `TampaoAuthorizationGate` / `TampaoExecutor` | Application | `...\TampaoAuthorizationGate.cs` |
| `ITampaoRepository` | Application | `...\ITampaoRepository.cs` |
| `ITampoesUnitOfWorkFactory` | Application | `...\ITampoesUnitOfWorkFactory.cs` |
| Requests / DTOs | Application | `...\TampaoRequests.cs` |
| `DapperTampaoRepository` | Infrastructure (Tampões-specific) | `src\BA.Dmo.Infrastructure\Access\DapperTampaoRepository.cs` |
| `DapperTampoesUnitOfWorkFactory` | Infrastructure (Tampões-specific) | `...\DapperTampoesUnitOfWorkFactory.cs` |
| `tampao_*` (9 tables / 8 indexes / 3 triggers) | Database | `database\migrations\N10_tampoes.sql`, `N21_tampoes_machines.sql` (+N12/N25 RLS) |
| Page `Index.cshtml` / `Index.cshtml.cs` | Web pages | `src\BA.Dmo.Web\Pages\Tampoes\` |
| API endpoints + module policy + DI | Shared web wiring | `src\BA.Dmo.Web\Program.cs` |
| `tampoes.js` / `tampoes-layout.css` | Static assets | `wwwroot\scripts\...` / `wwwroot\styles\modules\...` |
| Tampões tests | Tests | `tests\BA.Dmo.UnitTests\Modules\Tampoes\`, `tests\BA.Dmo.IntegrationTests\Tampoes\` |

## 18. Sources Verified

- `maps\00_INDEX.md` (structural contract; Tampões order 8, status COMPLETE)
- `src\BA.Dmo.Domain\Modules\Tampoes\` (11 files)
- `src\BA.Dmo.Application\Modules\Tampoes\` (5 files)
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperTampaoRepository.cs`, `DapperTampoesUnitOfWorkFactory.cs`
- `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`
- `src\BA.Dmo.Web\Program.cs` (DI + Tampões API endpoints)
- `src\BA.Dmo.Web\Pages\Tampoes\` (Index.cshtml, Index.cshtml.cs)
- `src\BA.Dmo.Web\Pages\Shared\_Navigation.cshtml`
- `src\BA.Dmo.Web\wwwroot\scripts\tampoes.js`, `wwwroot\styles\modules\tampoes-layout.css`
- `database\migrations\N10_tampoes.sql`, `N12_rls.sql`, `N21_tampoes_machines.sql`, `N25_remediation.sql`
- `database\consolidated_clean_install.sql`
- `tests\BA.Dmo.UnitTests\Modules\Tampoes\` (4 files), `tests\BA.Dmo.IntegrationTests\Tampoes\` (1 file)

## Counts

- Domain Tampões files: 11
- Application Tampões files: 5
- Infrastructure Tampões files: 2
- Shared infrastructure dependencies: NONE (DapperTampaoRepository and DapperTampoesUnitOfWorkFactory are both Tampões-specific; they reference shared persistence infra `IDbConnectionFactory`/`DapperUnitOfWork`)
- Web dedicated page files: 2
- Dedicated static asset files: 2
- Shared web wiring files: 1 (`Program.cs`)
- Shared static asset files: shared DMO CSS layer (`dmo-*.css`)
- Shared application catalog files: `CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs` (shared Access catalog)
- Tampões-specific DB tables: 9
- Tampões-specific indexes: 8
- Tampões-specific triggers: 3
- Tampões-specific DB objects: 20 (9 tables + 8 indexes + 3 triggers)
- Shared DB dependencies: `internal_users`, `audit_events`, `ba_dmo_guard_append_only` function
- Distinct migration files: 4 (N10, N12, N21, N25)
- Test classes: 4
- Dedicated test support files: 1
- In-file test fixtures: 1
- Source-visible user surfaces: 1 (Shared)