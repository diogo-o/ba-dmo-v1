# BA DMO — História Technical Map

MAP ID: MAP-14
Status: COMPLETE

Related maps: `00_INDEX.md` (registry) · `01_DOMAIN.md` · `02_DATABASE.md` · `03_MIGRATIONS.md` · `04_DAPPER_INFRASTRUCTURE.md` · `05_TESTS.md` · `19_APPLICATION.md` · `20_WEB.md` · `06_JOB_ON.md` (largest origin of audited facts) · `16_USERS_ACCESS.md` (TD-24 module/capability grants that define the visible scope).

## Navigation Index

- [1. Scope](#1-scope)
- [2. Layer Summary](#2-layer-summary)
- [3. Domain Objects](#3-domain-objects)
- [4. Application Objects](#4-application-objects)
- [5. Application Contracts / Ports](#5-application-contracts--ports)
- [6. Authorization / Catalog Objects](#6-authorization--catalog-objects)
- [7. User Surfaces](#7-user-surfaces)
- [8. Infrastructure Objects](#8-infrastructure-objects)
- [9. Database Objects](#9-database-objects)
- [10. Migration Touchpoints](#10-migration-touchpoints)
- [11. Web / Routes](#11-web--routes)
- [12. Static Assets](#12-static-assets)
- [13. Tests](#13-tests)
- [14. Test Doubles / Helpers](#14-test-doubles--helpers)
- [15. Direct História References](#15-direct-história-references)
- [16. External Technical References](#16-external-technical-references)
- [17. Target-to-Layer Index](#17-target-to-layer-index)
- [18. Sources Verified](#18-sources-verified)
- [Counts](#counts)

## 1. Scope

This map inventories the História transversal-read module. História exposes one Razor page (`/historia`) and two read-only JSON endpoints under `/api/historia`. Its Application/Infrastructure source reads the shared `audit_events` table. The technical source lives in the Application, Infrastructure and Web layers; no dedicated História Domain object or História-specific database object exists.

The map covers only what exists in source: objects, locations, members, routes, DB references, migrations, tests and direct/external references. It does not explain end-to-end flow.

## 2. Layer Summary

| Layer | Dedicated História objects | Location |
|---|---|---|
| Domain | 0 | — |
| Application | 5 | `src\BA.Dmo.Application\Modules\Historia\` |
| Infrastructure | 1 | `src\BA.Dmo.Infrastructure\Access\DapperHistoriaRepository.cs` |
| Database | 0 (reads shared `audit_events`) | — |
| Migrations | 0 dedicated | — |
| Web pages | 2 | `src\BA.Dmo.Web\Pages\Historia\` |
| Web endpoints | 2 | `src\BA.Dmo.Web\Program.cs` |
| Static assets | 0 dedicated (shared CSS selectors) | `src\BA.Dmo.Web\wwwroot\styles\dmo-components.css` |
| Tests | 3 classes + doubles | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Historia\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\` |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | NO | — (no dedicated História Domain object) |
| Application | YES | `src\BA.Dmo.Application\Modules\Historia\` |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\DapperHistoriaRepository.cs` |
| Web | YES | `src\BA.Dmo.Web\Pages\Historia\`; `src\BA.Dmo.Web\Program.cs`; `Authorization\ModuleAuthorizationHandler.cs` |
| Database | NO | — (reads shared `audit_events`; no História-specific DB object) |
| Tests | YES | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Historia\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\` |

This is technical navigation only; it does not explain workflow. `Present = NO` is a valid, source-verified value.

## 3. Domain Objects

No dedicated História Domain object exists — current source fact, re-verified 2026-08-27: `src\BA.Dmo.Domain\Modules\` contains NO `Historia` folder (only `Armazem`, `Boquilhas`, `Controlo`, `Ferramentas`, `JobOn`, `Pegamentos`, `Peso`, `ReparacaoExterna`, `ReparacaoInterna`, `Tampoes`). História consumes only shared Domain kernel/access types (see section 16).

## 4. Application Objects

All under `src\BA.Dmo.Application\Modules\Historia\`.

### HistoriaService
- File: `HistoriaService.cs`
- Type: `public sealed class HistoriaService`
- Constructor dependencies:
  - `HistoriaAuthorizationGate _gate`
  - `IHistoriaRepository _repository`
- Public methods:
  - `Result<HistoriaScope, DomainError> Authorization()` — resolves the `historia` module scope by invoking `_gate.Require()`.
  - `Task<Result<HistoriaQueryResult, DomainError>> QueryAsync(HistoriaFilter filter, CancellationToken)` — authorized grouped query; validates page size against `HistoriaModuleCatalog.CanonicalPageSizes` and page >= 1; forwards the gate scope to the repository.
  - `Task<Result<IReadOnlyList<HistoriaEntryRow>, DomainError>> QueryFlatAsync(HistoriaFilter filter, CancellationToken)` — authorized flat query; validates page >= 1; forwards the gate scope.
- Error codes produced: `HISTORIA_PAGE_SIZE_INVALID`, `HISTORIA_PAGE_INVALID` (validation); `HISTORIA_FORBIDDEN` (via gate).

### HistoriaAuthorizationGate
- File: `HistoriaAuthorizationGate.cs`
- Type: `public sealed class HistoriaAuthorizationGate`
- Constructor dependency: `ICurrentUserAccessor _currentUserAccessor`
- Public method: `Result<HistoriaScope, DomainError> Require()`
- Behavior:
  - Returns `DomainError.Forbidden("HISTORIA_FORBIDDEN", ...)` when no resolved identity or when the identity lacks `HistoriaModuleCatalog.ModuleId`.
  - Computes visible origin modules = `HistoriaModuleCatalog.OriginModuleIds` intersected with the identity's granted modules (`user.HasModule`), ordered ordinal ascending.
  - Computes `includeAdmin = user.HasCapability(CanonicalCapabilities.AuditView)`.
  - Returns `HistoriaScope(visible, includeAdmin)`.
- Records in file:
  - `public sealed record HistoriaScope(IReadOnlyCollection<string> VisibleOriginModuleIds, bool IncludeAdminWithAuditView)`

### HistoriaModuleCatalog
- File: `HistoriaModuleCatalog.cs`
- Type: `public static class HistoriaModuleCatalog`
- Constants / members:
  - `public const string ModuleId = "historia"`
  - `public static readonly string[] OriginModuleIds` (jobon, boquilhas, peso, pegamentos, ferramentas, armazem, reparacao_interna, reparacao_externa, tampoes)
  - `public static readonly int[] CanonicalPageSizes = { 20, 40, 60 }`

### HistoriaModels
- File: `HistoriaModels.cs`
- Records:
  - `public sealed record HistoriaFilter(string? Query, string? EntityType, string? EntityId, string? ModuleId, string? ActionCode, string? Actor, string? Result, DateTimeOffset? FromUtc, DateTimeOffset? ToUtc, int Page, int PageSize)` with `static bool IsValidPageSize(int)`.
  - `public sealed record HistoriaEntryRow(DateTimeOffset OccurredAtUtc, int Year, string? ActorUserId, string? ActorNameSnapshot, string ModuleId, string ActionCode, string EntityType, string EntityId, string? EntityLabelSnapshot, string Result, string? Reason, Guid? JobOnId, Guid? RevisionId, string? BeforeSummary, string? AfterSummary)`
  - `public sealed record HistoriaGroupRow(string GroupKey, string EntityLabel, string ModuleId, string EntityType, string EntityId, IReadOnlyList<HistoriaEntryRow> Events)` with computed `DateTimeOffset LatestAtUtc`.
  - `public sealed record HistoriaQueryResult(IReadOnlyList<HistoriaGroupRow> Groups, int TotalCount, int Page, int PageSize)`

## 5. Application Contracts / Ports

### IHistoriaRepository
- File: `HistoriaApplication\...\Historia\IHistoriaRepository.cs` (`src\BA.Dmo.Application\Modules\Historia\IHistoriaRepository.cs`)
- Type: `public interface IHistoriaRepository`
- Methods:
  - `Task<HistoriaQueryResult> QueryAsync(HistoriaFilter filter, IReadOnlyCollection<string> visibleModuleIds, bool includeAdminWithAuditView, CancellationToken cancellationToken = default)`
  - `Task<IReadOnlyList<HistoriaEntryRow>> QueryFlatAsync(HistoriaFilter filter, IReadOnlyCollection<string> visibleModuleIds, bool includeAdminWithAuditView, CancellationToken cancellationToken = default)`
- Implementation (registering): `DapperHistoriaRepository` (see section 8).

## 6. Authorization / Catalog Objects

- Module id: `historia` (`HistoriaModuleCatalog.ModuleId`, `CanonicalModuleCatalog.HistoriaModuleId`).
- Canonical module entry: `new ModuleDefinition(HistoriaModuleId, "História", ModuleKind.Module, 90, "/historia", isAssignable: false)` in `CanonicalModuleCatalog.Build()` (`src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, lines 123-125). Note: catalog numeric order is `90`; the 9th slot in the 10-module canonical registry is História (00_INDEX.md). No capabilities, non-assignable (GLM-HIST-02).
- Page id: `historia.consulta` (`CanonicalPageCatalog.HistoriaConsultaPageId`), page entry `new PageDefinition(HistoriaConsultaPageId, HistoriaModuleId, "/historia", requiredCapabilityId: null, displayOrder: 90)` in `CanonicalPageCatalog.Build()` (`src\BA.Dmo.Application\Shared\Access\CanonicalPageCatalog.cs`, line 75).
- Capabilities: none on the História module entry itself (`requiredCapabilityId: null`; module entry has no capability list). The gate checks the shared `audit.view` capability (`CanonicalCapabilities.AuditView` in `src\BA.Dmo.Application\Modules\Admin\AdminUserService.cs`, line 595) to include admin events.
- Web policy: `ModulePolicies.Historia = "BaDmo.Module.historia"` (`src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`, line 63).
- Policy enforcement:
  - Razor page `@attribute [Authorize(Policy = ModulePolicies.Historia)]` — `Index.cshtml`.
  - `app.MapGet("/api/historia", ...).RequireAuthorization(ModulePolicies.Historia)` and `app.MapGet("/api/historia/events", ...).RequireAuthorization(ModulePolicies.Historia)` — `Program.cs` lines 1412 / 1428.
  - Server-side re-check: `HistoriaAuthorizationGate.Require()`.

## 7. User Surfaces

**Shared.** Source exposes a single Razor page (`/historia`, one `PageModel`) and two JSON endpoints with no profile/role-specific page variant. The `audit.view` capability changes whether admin events are included in the returned scope (`IncludeAdminWithAuditView`), but it is not a distinct rendered surface, route or control variant. No Operador/Responsável/Admin page surfaces exist in the História source.

## 8. Infrastructure Objects

### DapperHistoriaRepository
- File: `src\BA.Dmo.Infrastructure\Access\DapperHistoriaRepository.cs`
- Type: `public sealed class DapperHistoriaRepository : IHistoriaRepository`
- Constructor dependency: `IDbConnectionFactory _connectionFactory`
- Read adapter: implements the read-only port. Reads EXACTLY ONE table — the shared `audit_events` (both `QueryAsync` and `QueryFlatAsync`); no JOINs against `job_on`/`job_on_*`, repairs, or any other module domain table. Cross-module coverage arrives exclusively through the persisted audit-fact columns (`module_id`, `entity_type`, `entity_id`, `entity_label_snapshot`, `job_on_id`, `revision_id`). História is a read-only history surface: it never writes to `audit_events` nor to any module table.
- SQL-bearing class members:
  - `private const string RowColumns` — column projection for `HistoriaEntryRow` (occurred_at_utc, year, actor_user_id, actor_name_snapshot, module_id, action_code, entity_type, entity_id, entity_label_snapshot, result, reason, job_on_id, revision_id, before_summary::text, after_summary::text).
  - `BuildWhere(...)` — builds the `WHERE` clause + `DynamicParameters`: module visibility (`module_id = ANY(@VisibleModules)`, adding `admin` when `includeAdminWithAuditView`, else `__none__`); free-text `Query` with `ILIKE` over entity_label_snapshot / entity_id / entity_type / actor_name_snapshot / action_code; `EntityType`, `EntityId`, `ModuleId`, `ActionCode`, `Actor`, `Result` exact/filters; `FromUtc`/`ToUtc` range on `occurred_at_utc`.
  - `QueryAsync` — counts distinct `entity_type || '|' || entity_id`, pages group keys by `MAX(occurred_at_utc)` DESC, fetches all events of paged keys, assembles `HistoriaGroupRow` list, returns `HistoriaQueryResult`.
  - `QueryFlatAsync` — flat `SELECT {RowColumns} FROM audit_events ... ORDER BY occurred_at_utc DESC LIMIT/OFFSET`.
  - `private sealed record PagedGroupKey(string EntityType, string EntityId, DateTimeOffset LatestAtUtc)`.
- Infrastructure dependencies external to História (shared): `IDbConnectionFactory` (`BA.Dmo.Infrastructure.Persistence`), Dapper `Db.QueryAsync`.

## 9. Database Objects

História-specific DB objects: **0**. História creates no dedicated tables, indexes or triggers; it is a read-only consumer of the shared canonical audit table.

Classification: **CONFIRMED CURRENT** — the read-only history surface matches current source (only `audit_events` reads; no História-specific DB objects; no writes).

Shared/external DB dependency:

### audit_events (shared, introduced by N01)
- Definition: `database\migrations\N01_identity.sql` (table lines 98-119; indexes 121-132; trigger 134-138). The `audit_events` table is the module-independent append-only audit table (not a História-dedicated object). História only reads it.
- Columns used by `DapperHistoriaRepository` (from N01): `occurred_at_utc`, `year`, `actor_user_id`, `actor_name_snapshot`, `module_id`, `action_code`, `entity_type`, `entity_id`, `entity_label_snapshot`, `result`, `reason`, `job_on_id`, `revision_id`, `before_summary`, `after_summary`.
- Constraints (separate from counts): `ck_audit_events_year_positive` (year > 0), `ck_audit_events_result` (result IN ('succeeded','failed','denied','corrected')).
- Indexes (shared, not counted as História-specific): `ix_audit_events_year`, `ix_audit_events_module_action`, `ix_audit_events_actor`, `ix_audit_events_entity`, `ix_audit_events_occurred_at`, `ix_audit_events_job_on_id` (N01); `ix_audit_events_module_time` (N25 remediation, lines 280-281).
- Trigger (shared): `trg_audit_events_append_only` (BEFORE UPDATE OR DELETE, `EXECUTE FUNCTION ba_dmo_guard_append_only()`) — N01.
- RLS: `audit_events` is included in the RLS inventory in `database\migrations\N12_rls.sql` (lines 34, 107).

## 10. Migration Touchpoints

Distinct História-specific migration files: **0**. No migration introduces or alters a História-dedicated DB object, because História has no dedicated DB objects.

Shared/external migration references (navigation only):
- `database\migrations\N01_identity.sql` — introduces the shared `audit_events` table that História reads.
- `database\migrations\N12_rls.sql` — includes `audit_events` in the row-level-security inventory.
- `database\migrations\N25_remediation.sql` — adds the shared index `ix_audit_events_module_time` on `audit_events`.

## 11. Web / Routes

| Route | HTTP | Technical Entry Point | Authorization | File |
|---|---|---|---|---|
| `/historia` | GET | `BA.Dmo.Web.Pages.Historia.IndexModel.OnGetAsync` (Razor page) | `ModulePolicies.Historia` attribute + `HistoriaAuthorizationGate` | `src\BA.Dmo.Web\Pages\Historia\Index.cshtml` / `Index.cshtml.cs` |
| `/api/historia` | GET | `app.MapGet("/api/historia", ...)` → `HistoriaService.QueryAsync` | `.RequireAuthorization(ModulePolicies.Historia)` | `src\BA.Dmo.Web\Program.cs` (lines 1412-1425) |
| `/api/historia/events` | GET | `app.MapGet("/api/historia/events", ...)` → `HistoriaService.QueryFlatAsync` | `.RequireAuthorization(ModulePolicies.Historia)` | `src\BA.Dmo.Web\Program.cs` (lines 1428-1442) |

DI registrations (`src\BA.Dmo.Web\Program.cs`, lines 262-264):
- `builder.Services.AddScoped<IHistoriaRepository, DapperHistoriaRepository>();` (line 262)
- `builder.Services.AddScoped<HistoriaAuthorizationGate>();` (line 263)
- `builder.Services.AddScoped<HistoriaService>();` (line 264)

### Razor page details (`Index.cshtml` / `Index.cshtml.cs`)
- Page route: `@page "/historia"`, model `IndexModel`.
- Query-string params bound by `OnGetAsync(query, module, action, actor, result, from, to, pageSize = 20, page = 1)`.
- Exposed PageModel members: `Query`, `Module`, `Action`, `Actor`, `Result`, `FromUtc`, `ToUtc`, `PageSize`, `Histories` (`HistoriaQueryResult?`), `VisibleModuleIds` (`IReadOnlyCollection<string>`), `ErrorMessage`.
- Server-side logic: resolves scope via `_service.Authorization()` (populates `VisibleModuleIds`); builds `HistoriaFilter`; calls `_service.QueryAsync`; on failure sets `ErrorMessage`.

## 12. Static Assets

Dedicated História static asset files: **0**. There is no `historia.js` or `historia.css` in `wwwroot\scripts` or `wwwroot\styles`.

Shared CSS selectors (in `src\BA.Dmo.Web\wwwroot\styles\dmo-components.css`, lines 831-890): `.historia-group-list` (831), `.historia-group__head` (837), `.historia-group__entity` (845), `.historia-entry summary` (849), `.historia-entry[open] summary` (857), `.historia-entry__detail` (861), `.historia-entry__meta` (869), `.historia-entry__meta dt/dd` (876/884), `.historia-entry__snapshot` (890). The razor uses generic shared classes (`dmo-*`).

No dedicated JavaScript for História; the page is server-rendered Razor. Shared JS/CSS wiring (`dmo-foundation.css`, `dmo-components.css`, etc.) is referenced by the shared layout and is not História-specific.

## 13. Tests

| Test class | Kind | Direct target | Main method groups | Location |
|---|---|---|---|---|
| `HistoriaServiceTests` | Unit (xUnit) | `HistoriaService` | `QueryAsync_AuthorizesAndForwardsScopeToRepository`, `QueryAsync_WithAuditView_OrdersChronologicallyStableAndGroupsByEntity`, `QueryAsync_InvalidPageSize_IsValidationError`, `QueryAsync_WithoutHistoriaModule_IsForbidden` | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaServiceTests.cs` |
| `HistoriaAuthorizationGateTests` | Unit (xUnit) | `HistoriaAuthorizationGate` | `Require_WithHistoriaAndOrigins_ResolvesGrantedOriginsOnly`, `Require_WithAuditView_IncludesAdmin`, `Require_WithNoOriginModules_IsAuthorizedWithEmptyScope`, `Require_WithoutHistoriaModule_IsForbidden`, `Require_WithNoIdentity_IsForbidden` | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaAuthorizationGateTests.cs` |
| `HistoriaWebAuthorizationTests` | Integration (WebApplicationFactory) | Page `/historia` + authorization wiring | `Unauth_HistoriaPage_RedirectsToLogin`, `WithoutHistoriaModule_IsDenied`, `WithHistoria_OnlyGrantedOriginModulesReachTheProjection`, `WithHistoria_AdminEventsExcludedWithoutAuditView` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\HistoriaWebAuthorizationTests.cs` |

Note: other test files reference module-level `History`/`BqHistoryFilter` concepts (e.g. Boquilhas, ReparaçãoInterna, Armazém) that are not História test classes; they are out of this map's scope.

## 14. Test Doubles / Helpers

| File | Double / helper | Implements / role |
|---|---|---|
| `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaServiceTests.cs` | `FakeHistoriaRepository` | In-file fake of `IHistoriaRepository` (records `LastVisibleModules`, `LastIncludeAdmin`, returns configured `Result` or empty). |
| `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaAuthorizationGateTests.cs` | `HistoriaCurrentUser` | In-file fake `ICurrentUserAccessor` builder (`WithModules`, `WithModulesAndCapabilities`, `WithoutHistoriaModule`, `None`); nested `FakeUser`. |
| `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\HistoriaWebAuthorizationTests.cs` | `FakeHistoriaReadRepository` | In-file fake of `IHistoriaRepository` (serves only visible module groups; records scope). |
| `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\HistoriaWebAuthorizationTests.cs` | `HistoriaFixture` | In-file `WebApplicationFactory<Program>` (`IClassFixture`); replaces `IHistoriaRepository` and identity/auth/admin/jobon collaborators. Contains nested `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeAdminRepo`, `FakeJobOnRepo`. |

## 15. Direct História References

- `HistoriaService` → `HistoriaAuthorizationGate` (constructor)
- `HistoriaService` → `IHistoriaRepository` (constructor)
- `HistoriaService` → `HistoriaFilter`, `HistoriaQueryResult`, `HistoriaEntryRow`, `HistoriaScope`, `HistoriaModuleCatalog` (members)
- `HistoriaAuthorizationGate` → `HistoriaModuleCatalog` (ModuleId, OriginModuleIds)
- `HistoriaAuthorizationGate` → `ICurrentUserAccessor` (constructor)
- `IHistoriaRepository` → `HistoriaFilter`, `HistoriaQueryResult`, `HistoriaEntryRow` (method signatures)
- `DapperHistoriaRepository` → `IHistoriaRepository` (implements)
- `DapperHistoriaRepository` → `HistoriaFilter`, `HistoriaGroupRow`, `HistoriaEntryRow`, `HistoriaQueryResult` (members)
- `DapperHistoriaRepository` → `audit_events` (reads, shared table)
- `IndexModel` → `HistoriaService` (constructor), `HistoriaFilter`, `HistoriaQueryResult` (OnGetAsync)
- `IndexModel` → `ModulePolicies.Historia` (razor `[Authorize]`)
- `Program.cs` → `ModulePolicies.Historia`, `HistoriaService`, `HistoriaFilter`, `DapperHistoriaRepository` (registrations + endpoints)

## 16. External Technical References

| História Object | External Technical Reference | Reference Type |
|---|---|---|
| `HistoriaModuleCatalog.OriginModuleIds` | `CanonicalModuleCatalog.JobonModuleId / BoquilhasModuleId / PesoModuleId / PegamentosModuleId / FerramentasModuleId / ArmazemModuleId / ReparacaoInternaModuleId / ReparacaoExternaModuleId / TampoesModuleId` | shared catalog / static consumer |
| `HistoriaAuthorizationGate` | `CanonicalCapabilities.AuditView` (`AdminUserService.cs`) | shared catalog / static consumer |
| `HistoriaAuthorizationGate` | `CurrentUser.HasModule / HasCapability` (`Domain\Shared\Access\CurrentUser.cs`) | shared DB-agnostic domain dependency |
| `HistoriaAuthorizationGate`, `HistoriaService` | `Result<T, DomainError>`, `DomainError.Forbidden/Validation` (`Domain.Shared.Kernel`) | shared domain kernel (constructor/method dependency) |
| `HistoriaModuleCatalog.ModuleId` | `CanonicalModuleCatalog.HistoriaModuleId` | shared catalog / static consumer |
| `Historia.ConsultaPageId` | `CanonicalPageCatalog` (`HistoriaConsultaPageId`, page `/historia`) | shared web wiring / shared catalog |
| `ModulePolicies.Historia` page/endpoints | `ModuleAuthorizationHandler` / `ModuleRequirement` | shared web wiring |
| `DapperHistoriaRepository` | `IDbConnectionFactory` (`BA.Dmo.Infrastructure.Persistence`) | application port / query dependency |
| `DapperHistoriaRepository` | Dapper `Db.QueryAsync` / `DynamicParameters` | query/read dependency (external lib) |
| `DapperHistoriaRepository` | `audit_events` table (+ `admin` module events when `audit.view`) | shared DB dependency (read-only) |
| `IndexModel` | `PageModel`, `HttpContext` (Microsoft.AspNetCore.Mvc.RazorPages) | framework base type |

## 17. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `HistoriaService` | Application | `src\BA.Dmo.Application\Modules\Historia\HistoriaService.cs` |
| `HistoriaAuthorizationGate` / `HistoriaScope` | Application | `src\BA.Dmo.Application\Modules\Historia\HistoriaAuthorizationGate.cs` |
| `HistoriaModuleCatalog` | Application | `src\BA.Dmo.Application\Modules\Historia\HistoriaModuleCatalog.cs` |
| `HistoriaFilter` / `HistoriaEntryRow` / `HistoriaGroupRow` / `HistoriaQueryResult` | Application | `src\BA.Dmo.Application\Modules\Historia\HistoriaModels.cs` |
| `IHistoriaRepository` | Application (port) | `src\BA.Dmo.Application\Modules\Historia\IHistoriaRepository.cs` |
| `DapperHistoriaRepository` | Infrastructure | `src\BA.Dmo.Infrastructure\Access\DapperHistoriaRepository.cs` |
| `IndexModel` + `Index.cshtml` | Web | `src\BA.Dmo.Web\Pages\Historia\` |
| `/api/historia`, `/api/historia/events` endpoints | Web | `src\BA.Dmo.Web\Program.cs` |
| `ModulePolicies.Historia` | Web (Authorization) | `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs` |
| `audit_events` (shared, read-only) | Database | `database\migrations\N01_identity.sql` |
| `HistoriaServiceTests` / `FakeHistoriaRepository` | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaServiceTests.cs` |
| `HistoriaAuthorizationGateTests` / `HistoriaCurrentUser` | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaAuthorizationGateTests.cs` |
| `HistoriaWebAuthorizationTests` / `HistoriaFixture` / `FakeHistoriaReadRepository` | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\HistoriaWebAuthorizationTests.cs` |

## 18. Sources Verified

Reconciled IN PLACE at HEAD `8478308` (2026-08-27): História remains a read-only transversal surface over `audit_events`; no dedicated Domain module (folder absence re-confirmed); `DapperHistoriaRepository` reads EXACTLY ONE table (`audit_events`, no cross-module table JOINs); `Program.cs` line refs and DI registrations refreshed; `dmo-components.css` selector lines refreshed; test paths moved to `AI-CONTEXT\docs\tests\`.

Files inspected for this pass:
- `src\BA.Dmo.Application\Modules\Historia\HistoriaService.cs`
- `src\BA.Dmo.Application\Modules\Historia\IHistoriaRepository.cs`
- `src\BA.Dmo.Application\Modules\Historia\HistoriaModels.cs`
- `src\BA.Dmo.Application\Modules\Historia\HistoriaAuthorizationGate.cs`
- `src\BA.Dmo.Application\Modules\Historia\HistoriaModuleCatalog.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperHistoriaRepository.cs`
- `src\BA.Dmo.Web\Pages\Historia\Index.cshtml`
- `src\BA.Dmo.Web\Pages\Historia\Index.cshtml.cs`
- `src\BA.Dmo.Web\Program.cs`
- `src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`
- `src\BA.Dmo.Application\Shared\Access\CanonicalPageCatalog.cs`
- `src\BA.Dmo.Application\Modules\Admin\AdminUserService.cs` (`CanonicalCapabilities`)
- `src\BA.Dmo.Domain\Shared\Access\CurrentUser.cs`
- `src\BA.Dmo.Domain\Modules\` (folder listing — no `Historia` folder)
- `database\migrations\N01_identity.sql`
- `database\migrations\N12_rls.sql`
- `database\migrations\N25_remediation.sql`
- `src\BA.Dmo.Web\wwwroot\styles\dmo-components.css`
- `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaServiceTests.cs`
- `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Modules\Historia\HistoriaAuthorizationGateTests.cs`
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\HistoriaWebAuthorizationTests.cs`

## Counts

- Domain História files: **0**
- Application História files: **5**
- Infrastructure História files: **1**
- Shared infrastructure dependencies: **2** (`IDbConnectionFactory`, Dapper)
- Dedicated Web page files: **2**
- Dedicated static asset files: **0**
- Shared Web wiring files: **2** (`src\BA.Dmo.Web\Authorization\ModuleAuthorizationHandler.cs`, `src\BA.Dmo.Web\Program.cs`)
- Shared static asset files: **1** (`dmo-components.css` carries História selectors; page uses shared `dmo-*` classes)
- História-specific DB tables: **0**
- História-specific DB indexes: **0**
- História-specific DB triggers: **0**
- História-specific DB objects: **0** (0 tables + 0 indexes + 0 triggers)
- Shared / external DB dependencies: **1** (`audit_events` table + its shared indexes/trigger/constraints/RLS)
- Distinct História-specific migration files: **0**
- Test classes: **3**
- Dedicated test support files: **0** (all doubles are in-file)
- In-file test fixture files: **3** (`HistoriaServiceTests.cs`, `HistoriaAuthorizationGateTests.cs`, `HistoriaWebAuthorizationTests.cs`)
- Source-visible user surfaces: **1** (Shared)