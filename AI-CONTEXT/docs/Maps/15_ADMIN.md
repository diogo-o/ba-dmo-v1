# BA DMO — Admin Technical Map

MAP ID: MAP-15
Status: COMPLETE

## Navigation Index

- [1. Scope](#1-scope)
- [2. Layer Summary](#2-layer-summary)
- [3. Domain Objects](#3-domain-objects)
- [4. Application Objects](#4-application-objects)
- [5. Application Contracts / Ports](#5-application-contracts--ports)
- [6. Authorization / Capabilities / Catalog](#6-authorization--capabilities--catalog)
- [7. User Surface](#7-user-surface)
- [8. Admin Technical Areas](#8-admin-technical-areas)
- [9. Infrastructure Objects](#9-infrastructure-objects)
- [10. Database Objects](#10-database-objects)
- [11. Migration Touchpoints](#11-migration-touchpoints)
- [12. Web / Routes](#12-web--routes)
- [13. Audit Technical Surface](#13-audit-technical-surface)
- [14. Static Assets](#14-static-assets)
- [15. Tests](#15-tests)
- [16. Test Doubles / Helpers](#16-test-doubles--helpers)
- [17. Direct Admin References](#17-direct-admin-references)
- [18. External Technical References](#18-external-technical-references)
- [19. Target-to-Layer Index](#19-target-to-layer-index)
- [20. Sources Verified](#20-sources-verified)
- [Counts](#counts)

## 1. Scope

This map inventories the Admin module (administração do portal). Admin exposes one canonical module (`admin`) with a single landing page (`/admin`) and four sub-areas: Users (`/admin/users`), Templates (`/admin/templates`), Applications (`/admin/applications`) and Audit (`/admin/audit`). All operations run through Razor PageModels (no separate `/api/admin` endpoints) and re-authorize server-side via `AdminAuthorizationGate`. The technical source lives in the Application, Infrastructure and Web layers; there is no Admin-specific Domain folder and no Admin-specific database object — Admin reads/writes the shared `internal_users`, `access_templates`, `audit_events` and `module_catalog_mirror` tables.

The map covers only what exists in source: objects, locations, members, routes, capability declarations, DB references, migrations, tests and direct/external references. It does not explain end-to-end flow and does not absorb the separate Users / Access transversal / system surface (MAP-16), which is not a canonical functional module.

## 2. Layer Summary

| Layer | Dedicated Admin objects | Location |
|---|---|---|
| Domain | 0 | — |
| Application | 7 | `src\BA.Dmo.Application\Modules\Admin\` |
| Infrastructure | 2 | `src\BA.Dmo.Infrastructure\Access\DapperAdminRepository.cs`, `src\BA.Dmo.Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs` |
| Database | 0 (reads/writes shared access/audit/mirror tables) | — |
| Migrations | 0 dedicated | — |
| Web pages | 16 | `src\BA.Dmo.Web\Pages\Admin\` |
| Web endpoints | 0 JSON (Razor page handlers only) | `src\BA.Dmo.Web\Program.cs` (policies + DI) |
| Static assets | 1 dedicated CSS | `src\BA.Dmo.Web\wwwroot\styles\modules\admin-layout.css` |
| Tests | 10 classes + doubles | `tests\BA.Dmo.UnitTests\Shared\Admin\`, `tests\BA.Dmo.IntegrationTests\Access\`, `tests\BA.Dmo.IntegrationTests\Cli\`, `tests\BA.Dmo.IntegrationTests\Identity\` |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | NO | — (no dedicated Admin Domain object) |
| Application | YES | `src\BA.Dmo.Application\Modules\Admin\` |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\DapperAdminRepository.cs`, `src\BA.Dmo.Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs` |
| Web | YES | `src\BA.Dmo.Web\Pages\Admin\`; `src\BA.Dmo.Web\Program.cs`; `Authorization\CapabilityAuthorizationHandler.cs` |
| Database | NO | — (reads/writes shared `internal_users`, `access_templates`, `audit_events`, `module_catalog_mirror`; no Admin-specific DB object) |
| Tests | YES | `tests\BA.Dmo.UnitTests\Shared\Admin\`, `tests\BA.Dmo.IntegrationTests\Access\`, `Cli\`, `Identity\` |

This is technical navigation only; it does not explain workflow. `Present = NO` is a valid, source-verified value.

## 3. Domain Objects

No dedicated Admin Domain object exists. The Domain project (`src\BA.Dmo.Domain\Modules\`) contains no `Admin` folder. Admin consumes only shared Domain kernel/access types (see section 18).

## 4. Application Objects

All under `src\BA.Dmo.Application\Modules\Admin\` (7 files).

### AdminAuthorizationGate
- File: `AdminAuthorizationGate.cs`
- Type: `public sealed class AdminAuthorizationGate`
- Constructor dependency: `ICurrentUserAccessor _currentUserAccessor`
- Public method: `Result<AdminExecutor, DomainError> Require(params string[] anyOfCapabilityIds)`
- Behavior: returns `DomainError.Forbidden("ADMIN_FORBIDDEN", ...)` when no resolved identity or when none of the requested capability ids is granted; otherwise returns `AdminExecutor(InternalUserId, DisplayName)`.
- Records in file: `public sealed record AdminExecutor(string ActorId, string DisplayName)`.

### AdminUserService
- File: `AdminUserService.cs`
- Type: `public sealed class AdminUserService`
- Constructor dependencies: `AdminAuthorizationGate`, `IAdminRepository`, `IAdminProvisioningAdapter`, `IClock`.
- Public methods (each first calls `_gate.Require(CanonicalCapabilities.AdminGerir)`):
  - `Task<Result<IReadOnlyList<AdminUserRow>, DomainError>> ListAsync(string? search, CancellationToken)` — enriches `AuthEmail` via batched `_provisioning.GetUserEmailsAsync`.
  - `Task<Result<AdminUserRow, DomainError>> GetAsync(string actorId, CancellationToken)`.
  - `Task<Result<AdminUserRow, DomainError>> CreateUserAsync(CreateAdminUserRequest, CancellationToken)` — creates the Auth account via `_provisioning.EnsureAuthUserAsync`, then the internal user.
  - `Task<Result<AdminUserRow, DomainError>> UpdateUserAsync(UpdateAdminUserRequest, CancellationToken)`.
  - `Task<Result<AdminUserRow, DomainError>> ChangeTemplateAsync(ChangeUserTemplateRequest, CancellationToken)` — guarded by self-lockout.
  - `Task<Result<AdminUserRow, DomainError>> SetActiveAsync(SetUserActiveRequest, CancellationToken)` — guarded by self-lockout.
  - `Task<Result<AdminUserRow, DomainError>> SaveUserAsync(...)` — composite save (update → template → activation).
  - `Task<Result<AdminUserRow, DomainError>> SaveUserWithModulesAsync(...)` — composite save + per-user module override.
  - `Task<Result<AdminUserRow, DomainError>> SaveUserModulesAsync(string actorId, IReadOnlyList<TemplateGrantInput>, DateTimeOffset, CancellationToken)` — validates grants via shared `GrantNormalizer`, applies Job On guard, self-lockout guard.
  - `Task<Result<bool, DomainError>> RequestPasswordResetAsync(string targetActorId, CancellationToken)` — via `_provisioning.RequestPasswordResetAsync`.
- Private helpers: `ValidateGrants`, `IsValidEmail`, `AuditAsync`, `JsonOptions` (`JsonNamingPolicy.CamelCase`).
- Constants: `PasswordPolicyMinLength = 8`; `SchemaMigrationUnavailableCode = "SCHEMA_MIGRATION_REQUIRED"`.
- Error codes produced: `ADMIN_USER_INVALID`, `ADMIN_USER_INVALID_EMAIL`, `ADMIN_USER_WEAK_PASSWORD`, `ADMIN_TEMPLATE_INVALID`, `ADMIN_USER_ALREADY_REGISTERED`, `ADMIN_USER_JOON_DENIED`, `ADMIN_SELF_LOCKOUT`, `ADMIN_CONCURRENCY_CONFLICT`, `INTERNAL_USER_NOT_FOUND`, `ADMIN_USER_NO_AUTH_ACCOUNT`, `ACCESS_TEMPLATE_GRANTS_INVALID`, `SCHEMA_MIGRATION_REQUIRED`, plus `ADMIN_FORBIDDEN` (gate).

### CanonicalCapabilities (declared inside `AdminUserService.cs`)
- Static class with capability constants physically declared in the Admin file:
  - `public const string AdminModuleId = "admin"`
  - `public const string AdminGerir = "admin.gerir"`
  - `public const string AuditView = "audit.view"`
  - `public const string AuditExport = "audit.export"`

### AdminTemplateService
- File: `AdminTemplateService.cs`
- Type: `public sealed class AdminTemplateService`
- Constructor dependencies: `AdminAuthorizationGate`, `IAdminRepository`, `GrantNormalizer`, `IClock`.
- Public methods (each first calls `_gate.Require(CanonicalCapabilities.AdminGerir)`):
  - `Task<IReadOnlyList<AdminTemplateRow>> ListAsync(CancellationToken)` (returns empty list on gate failure).
  - `Task<Result<AdminTemplateRow, DomainError>> GetAsync(string templateId, CancellationToken)`.
  - `Task<Result<AdminTemplateRow, DomainError>> CreateAsync(CreateTemplateRequest, CancellationToken)`.
  - `Task<Result<AdminTemplateRow, DomainError>> UpdateAsync(UpdateTemplateRequest, CancellationToken)` — guarded by self-lockout.
- Private helpers: `ValidateGrants` (canonical grant validation), `AuditAsync`, `JsonOptions`.
- Error codes produced: `ACCESS_TEMPLATE_INVALID`, `ACCESS_TEMPLATE_EXISTS`, `ACCESS_TEMPLATE_NOT_FOUND`, `ACCESS_TEMPLATE_GRANTS_INVALID`, `ADMIN_SELF_LOCKOUT`, `ADMIN_CONCURRENCY_CONFLICT`.

### AdminAuditService
- File: `AdminAuditService.cs`
- Type: `public sealed class AdminAuditService`
- Constructor dependencies: `AdminAuthorizationGate`, `IAdminRepository`.
- Public methods:
  - `Task<Result<AuditQueryResult, DomainError>> QueryAsync(AuditQueryFilter, CancellationToken)` — first calls `_gate.Require(CanonicalCapabilities.AuditView)`; validates page size `20/40/60` and page >= 1.
  - `Task<Result<string, DomainError>> ExportAsync(AuditQueryFilter, CancellationToken)` — first calls `_gate.Require(CanonicalCapabilities.AuditExport)`; queries unlimited (`PageSize = 0`) and returns CSV text.
- Error codes produced: `AUDIT_PAGE_SIZE_INVALID`, `AUDIT_PAGE_INVALID`, plus `ADMIN_FORBIDDEN` (gate).

### AdminMirrorService
- File: `AdminMirrorService.cs`
- Type: `public sealed class AdminMirrorService`
- Constructor dependencies: `AdminAuthorizationGate`, `ModuleCatalog`, `IModuleCatalogMirrorRepository`, `IAdminRepository`, `IClock`; builds an internal `ModuleCatalogMirrorSynchronizer`.
- Public methods (each first calls `_gate.Require(CanonicalCapabilities.AdminGerir)`):
  - `Task<Result<IReadOnlyList<MirrorDisplayEntry>, DomainError>> GetDisplayAsync(CancellationToken)`.
  - `Task<Result<IReadOnlyList<MirrorDisplayEntry>, DomainError>> SaveDisplayAsync(IReadOnlyList<MirrorEntryInput>, CancellationToken)` — rejects module ids outside the canonical catalog.
- Error codes produced: `CATALOG_MIRROR_INVALID`, plus `ADMIN_FORBIDDEN` (gate).

### AdminModels
- File: `AdminModels.cs`
- Records:
  - `AdminUserRow(string ActorId, Guid? AuthUserId, string DisplayName, string? ProfileTitle, string TemplateId, bool Active, DateTimeOffset UpdatedAtUtc, string? AuthEmail = null, string? ModulesOverrideJson = null)`
  - `AdminTemplateRow(string TemplateId, string Name, string ModulesJson, bool Active, DateTimeOffset UpdatedAtUtc)`
  - `AuditQueryFilter(int? Year, string? ActorUserId, string? ModuleId, string? ActionCode, string? Result, DateTimeOffset? FromUtc, DateTimeOffset? ToUtc, int Page, int PageSize)` with `static int[] CanonicalPageSizes = [20, 40, 60]` and `static bool IsValidPageSize(int)`.
  - `AuditEventRow(DateTimeOffset OccurredAtUtc, int Year, string? ActorUserId, string? ActorNameSnapshot, string ModuleId, string ActionCode, string EntityType, string EntityId, string? EntityLabelSnapshot, string Result, string? Reason)`
  - `AuditQueryResult(IReadOnlyList<AuditEventRow> Rows, int TotalCount, int Page, int PageSize)`
  - `AuditEntry(DateTimeOffset OccurredAtUtc, string? ActorUserId, string? ActorNameSnapshot, string ModuleId, string ActionCode, string EntityType, string EntityId, string? EntityLabelSnapshot, string Result, string? Reason, string? BeforeSummary = null, string? AfterSummary = null)`
  - `CreateAdminUserRequest(string Email, string Password, string DisplayName, string? ProfileTitle, string TemplateId, bool Active = true)`
  - `UpdateAdminUserRequest(string ActorId, string DisplayName, string? ProfileTitle, DateTimeOffset ExpectedUpdatedAt)`
  - `ChangeUserTemplateRequest(string ActorId, string TemplateId, DateTimeOffset ExpectedUpdatedAt)`
  - `SetUserActiveRequest(string ActorId, bool Active, DateTimeOffset ExpectedUpdatedAt)`
  - `TemplateGrantInput(string ModuleId, IReadOnlyList<string> Capabilities)`
  - `CreateTemplateRequest(string TemplateId, string Name, IReadOnlyList<TemplateGrantInput> Grants)`
  - `UpdateTemplateRequest(string TemplateId, string Name, IReadOnlyList<TemplateGrantInput> Grants, bool Active, DateTimeOffset ExpectedUpdatedAt)`
  - `MirrorEntryInput(string ModuleId, int DisplayOrder, bool Active)`

## 5. Application Contracts / Ports

### IAdminRepository (Admin-specific port)
- File: `src\BA.Dmo.Application\Modules\Admin\IAdminRepository.cs`
- Type: `public interface IAdminRepository`
- Methods:
  - Internal users: `ListUsersAsync`, `GetUserAsync`, `AuthUserIdAlreadyRegisteredAsync`, `CreateInternalUserAsync`, `UpdateUserAsync`, `ChangeUserTemplateAsync`, `SetUserActiveAsync`, `SetUserModulesOverrideAsync`.
  - Self-lockout support: `CountActiveAdminsAsync(string? excludeActorId, CancellationToken)`.
  - Access templates: `ListTemplatesAsync`, `GetTemplateAsync`, `CreateTemplateAsync`, `UpdateTemplateAsync`.
  - Audit: `InsertAuditEventAsync(AuditEntry, CancellationToken)`, `QueryAuditAsync(AuditQueryFilter, CancellationToken)`.
- Implementation (registering): `DapperAdminRepository` (see section 9).
- Documents concurrency via `updated_at`; guarded writes validated in the same transaction.

### IAdminProvisioningAdapter (shared privileged-provisioning port)
- Declared in `src\BA.Dmo.Application\Shared\Identity\SupabaseAuthPorts.cs` (shared Identity, not Admin-specific).
- Methods: `EnsureAuthUserAsync`, `EnsureAuthUserWithStatusAsync`, `RequestPasswordResetAsync`, `GetUserEmailsAsync`.
- Implementation (registering): `SupabaseAdminProvisioningAdapter` (see section 9). Admin-scope consumers: `AdminUserService`, and the `bootstrap-admin` CLI path.

### IModuleCatalogMirrorRepository (shared catalog-mirror port)
- Declared in `src\BA.Dmo.Application\Shared\Access\IModuleCatalogMirrorRepository.cs` (shared Access).
- Methods: `GetAllAsync`, `UpsertAllAsync`.
- Implementation (registering): `DapperModuleCatalogMirrorRepository` (shared infra, see MAP-04). Admin-scope consumer: `AdminMirrorService`.

| Port | Principal methods | Implementation(s) | External dependency | Location |
|---|---|---|---|---|
| `IAdminRepository` | List/Get/Create/Update users, template CRUD, module override, admins count, audit insert/query | `DapperAdminRepository` | shared `internal_users`, `access_templates`, `audit_events` (shared DB) | `Application\Modules\Admin\IAdminRepository.cs` → `Infrastructure\Access\DapperAdminRepository.cs` |
| `IAdminProvisioningAdapter` (shared port) | `EnsureAuthUserAsync`, `EnsureAuthUserWithStatusAsync`, `RequestPasswordResetAsync`, `GetUserEmailsAsync` | `SupabaseAdminProvisioningAdapter` | Supabase Auth provider (service-role) | `Shared\Identity\SupabaseAuthPorts.cs` → `Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs` |
| `IModuleCatalogMirrorRepository` (shared port) | `GetAllAsync`, `UpsertAllAsync` | `DapperModuleCatalogMirrorRepository` | shared `module_catalog_mirror` table | `Shared\Access\IModuleCatalogMirrorRepository.cs` → `Infrastructure\Access\DapperModuleCatalogMirrorRepository.cs` |

## 6. Authorization / Capabilities / Catalog

- Module id: `admin` (`CanonicalModuleCatalog.AdminModuleId`, `CanonicalCapabilities.AdminModuleId`).
- Canonical module entry: `new ModuleDefinition(AdminModuleId, "Administração", ModuleKind.Module, 99, "/admin", new[] { Capability(admin.gerir), Capability(audit.view), Capability(audit.export) })` in `CanonicalModuleCatalog.Build()` (`src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`).
- Page id: `admin.gestao` (`CanonicalPageCatalog.AdminGestaoPageId`), page entry `new PageDefinition(AdminGestaoPageId, AdminModuleId, "/admin", requiredCapabilityId: AdminGerirCapabilityId, displayOrder: 99)` (not landing) in `CanonicalPageCatalog.Build()` (`src\BA.Dmo.Application\Shared\Access\CanonicalPageCatalog.cs`).
- Capability ids declared in Admin source (`CanonicalCapabilities` in `AdminUserService.cs`) AND in `CanonicalModuleCatalog`: `admin.gerir`, `audit.view`, `audit.export`.
- Web policies (`AdminPolicies` in `src\BA.Dmo.Web\Authorization\CapabilityAuthorizationHandler.cs`): `AdminGerir = "BaDmo.Admin.Gerir"`, `AuditView = "BaDmo.Audit.View"`, `AuditExport = "BaDmo.Audit.Export"`.
- Policy enforcement:
  - `options.AddPolicy(AdminPolicies.AdminGerir, ...)`, `.AuditView`, `.AuditExport` built on `CapabilityRequirement(CanonicalCapabilities.*)` — `Program.cs` lines 99–107.
  - Razor pages carry `@attribute [Authorize(Policy = ...)]` (see section 12).
  - Server-side re-check: `AdminAuthorizationGate.Require(...)` inside every Admin service method.

| Capability | Declared In | Policy / Check | Technical Consumers |
|---|---|---|---|
| `admin.gerir` | `CanonicalCapabilities.AdminGerir` (`AdminUserService.cs`); `CanonicalModuleCatalog.AdminGerirCapabilityId` | `AdminPolicies.AdminGerir` (`CapabilityAuthorizationHandler.cs`) + `AdminAuthorizationGate.Require(AdminGerir)` | `/admin`, `/admin/users`, `/admin/users/create`, `/admin/users/edit`, `/admin/templates`, `/admin/templates/edit`, `/admin/applications` pages; `AdminUserService`, `AdminTemplateService`, `AdminMirrorService` methods |
| `audit.view` | `CanonicalCapabilities.AuditView` (`AdminUserService.cs`); `CanonicalModuleCatalog.AuditViewCapabilityId` | `AdminPolicies.AuditView` + `AdminAuthorizationGate.Require(AuditView)` | `/admin/audit` page; `AdminAuditService.QueryAsync`; referenced by `HistoriaAuthorizationGate` (História MAP-14) |
| `audit.export` | `CanonicalCapabilities.AuditExport` (`AdminUserService.cs`); `CanonicalModuleCatalog.AuditExportCapabilityId` | `AdminPolicies.AuditExport` + `AdminAuthorizationGate.Require(AuditExport)` | `/admin/audit` Export handler; `AdminAuditService.ExportAsync` |

## 7. User Surface

**Admin.** Source exposes a single Admin landing page (`/admin`, `IndexModel` with no body model) plus the Users/Templates/Applications/Audit sub-pages. There is no Operador/Responsável/User-variant of the Admin pages — the Admin module is one surface. The `audit.view` / `audit.export` capabilities gate the Admin Audit page and its export control, but they are capability-conditioned controls on the same Admin surface, not separate user surfaces.

## 8. Admin Technical Areas

Source-grounded sub-areas contained inside the single Admin User Surface (not canonical modules):

- **Users** — pages `/admin/users`, `/admin/users/create`, `/admin/users/edit`; `AdminUserService`; per-user module override editor.
- **Templates** — pages `/admin/templates`, `/admin/templates/edit`; `AdminTemplateService`; canonical grant validation.
- **Applications** — page `/admin/applications`; `AdminMirrorService` over the shared `module_catalog_mirror`.
- **Audit** — page `/admin/audit`; `AdminAuditService` over the shared `audit_events`.

These are subsections of 15_ADMIN.md; they are not separate maps and do not appear as canonical modules in the INDEX.

## 9. Infrastructure Objects

### DapperAdminRepository (Admin-specific)
- File: `src\BA.Dmo.Infrastructure\Access\DapperAdminRepository.cs`
- Type: `public sealed class DapperAdminRepository : IAdminRepository`
- Constructor dependency: `IDbConnectionFactory _connectionFactory`.
- SQL-bearing members:
  - `const string UserColumns` — projection over `internal_users` (actor_id, auth_user_id, display_name, profile_title, template_id, active, updated_at_utc, `NULL::text AS AuthEmail`, `modules_override::text`).
  - `const string TemplateColumns` — projection over `access_templates`.
  - `const string AdminGrantPatternJson = "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\"]}]"`.
  - `const string UndefinedColumnSqlState = "42703"` — maps to `SchemaMigrationRequiredException` (N26 not applied).
  - User methods: `ListUsersAsync` / `GetUserAsync` (SELECT over `internal_users`), `AuthUserIdAlreadyRegisteredAsync`, `CreateInternalUserAsync` (INSERT, `ON CONFLICT (actor_id) DO NOTHING`), `UpdateUserAsync` (guarded UPDATE `WHERE updated_at_utc = @ExpectedUpdatedAt`), `ChangeUserTemplateAsync` / `SetUserActiveAsync` (via `GuardedUserWriteAsync`), `SetUserModulesOverrideAsync` (guarded UPDATE of `modules_override`), `CountActiveAdminsAsync`.
  - Template methods: `ListTemplatesAsync` / `GetTemplateAsync` (SELECT over `access_templates`), `CreateTemplateAsync` (INSERT), `UpdateTemplateAsync` (guarded UPDATE in `DapperUnitOfWork`).
  - Audit methods: `InsertAuditEventAsync` (INSERT into `audit_events` with `BeforeSummary`/`AfterSummary` `::jsonb` NULL), `QueryAuditAsync` (COUNT + SELECT over `audit_events` with filter `WHERE` and page LIMIT/OFFSET).
  - `GuardedUserWriteAsync` — applies the write then `CountActiveAdminsOnAsync`; rolls back (returns false) when zero surviving active admins (`LockoutViolationException`).
  - `LockoutViolationException` private sealed marker.

### SupabaseAdminProvisioningAdapter (Admin-specific provisioning adapter)
- File: `src\BA.Dmo.Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs`
- Type: `public sealed class SupabaseAdminProvisioningAdapter : IAdminProvisioningAdapter`
- Constructor dependency: `HttpClient`, `string? supabaseUrl`, `string? serviceRoleKey`.
- Methods: `EnsureAuthUserAsync`, `EnsureAuthUserWithStatusAsync` (via `EnsureAuthUserInternalAsync`, idempotent), `RequestPasswordResetAsync`, `GetUserEmailsAsync` (paginated).
- Calls Supabase Auth privileged endpoints: `POST /auth/v1/admin/users`, `GET /auth/v1/admin/users/{id}`, `POST /auth/v1/admin/generate_link`, `GET /auth/v1/admin/users?page=&per_page=`, using the service-role bearer key.
- Error codes produced: `PROVISIONING_CONFIGURATION_MISSING`, `PROVISIONING_CONFLICT`, `PROVISIONING_FAILED`, `AUTH_PROVIDER_UNAVAILABLE`, `BOOTSTRAP_CONFIGURATION_MISSING`.

### DapperModuleCatalogMirrorRepository (shared infra, consumed by Admin)
- File: `src\BA.Dmo.Infrastructure\Access\DapperModuleCatalogMirrorRepository.cs`
- Type: `public sealed class DapperModuleCatalogMirrorRepository : IModuleCatalogMirrorRepository` (shared port).
- Used by `AdminMirrorService`; reads/writes `module_catalog_mirror`. Counted as a shared Infrastructure dependency of Admin (see MAP-04).

### Shared Infrastructure dependencies of Admin (external to Admin object files)
- `IDbConnectionFactory` (`BA.Dmo.Infrastructure.Persistence`) — injected into `DapperAdminRepository`, `DapperModuleCatalogMirrorRepository`.
- Dapper (`Db.QueryAsync`/`ExecuteAsync`/`QuerySingleOrDefaultAsync`), `DynamicParameters`, Npgsql (`PostgresException`).
- `DapperUnitOfWork` + `ConcurrencyGuard` (`BA.Dmo.Application.Shared.Persistence`) — used by `DapperAdminRepository`.
- `SchemaMigrationRequiredException` (`BA.Dmo.Application.Shared.Persistence`) — mapped from SQLSTATE 42703 by `DapperAdminRepository`.
- `SupabaseSettings` (`BA.Dmo.Infrastructure\Auth`) — resolves provider URL/keys for adapter registration in `Program.cs`.

## 10. Database Objects

### Admin-specific DB objects

Admin-specific DB objects: **0**. Admin creates no dedicated table, index or trigger.

### Shared / external DB dependencies

Admin reads/writes the following shared **Access / audit** tables (all classed Shared in `02_DATABASE.md`; not Admin-dedicated):

| Table | Source | Admin access | Shared indexes / constraints / trigger (from DATABASE/MIGRATIONS maps) |
|---|---|---|---|
| `internal_users` | `N01_identity.sql` (N25 `auth_user_id` NOT NULL + UNIQUE; N26 `modules_override`) | SELECT projection, INSERT, guarded UPDATE (display, template, active, `modules_override`) | `ix_internal_users_auth_user_id`, `ix_internal_users_active`, `ix_internal_users_template_id`; `uq_internal_users_auth_user` |
| `access_templates` | `N01_identity.sql` | SELECT, INSERT, guarded UPDATE | `ix_access_templates_active` |
| `audit_events` | `N01_identity.sql` | INSERT (audit), SELECT query/export; constraint `ck_audit_events_result` value `succeeded` used by Admin inserts | indexes/trigger shared (see 02_DATABASE.md) |
| `module_catalog_mirror` | `N02_catalog.sql` | UPSERT/SELECT (catalog mirror) | `ix_module_catalog_mirror_order` |

Constraints referenced by Admin write paths (separate from counts): `internal_users.template_id → access_templates` FK (N01), `access_templates.active` index, `audit_events.result` CHECK (`succeeded`/`failed`/`denied`/`corrected`), `internal_users.modules_override` jsonb (N26). The self-lockout read (`CountActiveAdminsAsync`) joins `internal_users` + `access_templates` filtering `t.modules @> AdminGrantPatternJson`.

## 11. Migration Touchpoints

Distinct Admin-specific migration files: **0**. No migration introduces an Admin-dedicated DB object.

Shared/external migration references (navigation only) relevant to Admin:

| Migration | Object(s) | Technical Change | Classification |
|---|---|---|---|
| `N01_identity.sql` | `internal_users`, `access_templates`, `audit_events` | shared identity/access/audit tables used by Admin | shared/external |
| `N02_catalog.sql` | `module_catalog_mirror` | catalog mirror table used by Admin Applications area | shared/external |
| `N25_remediation.sql` | `internal_users.auth_user_id`, `audit_events` index | `uq_internal_users_auth_user`; `ix_audit_events_module_time` | shared/external |
| `N26_user_modules_override.sql` | `internal_users.modules_override` | `ALTER TABLE internal_users ADD COLUMN IF NOT EXISTS modules_override jsonb` | shared table; supports the Admin per-user module override feature |

## 12. Web / Routes

Admin exposes Razor-pages only; there are no `/api/admin` JSON endpoints (Admin mutations are Razor POST page-handlers).

| Route | HTTP | Entry Point | Policy / Capability | File |
|---|---|---|---|---|
| `/admin` | GET | `Admin.IndexModel.OnGet` (Razor page) | `AdminPolicies.AdminGerir` + gate | `Pages\Admin\Index.cshtml` / `Index.cshtml.cs` |
| `/admin/users` | GET | `Admin.Users.IndexModel.OnGetAsync` | `AdminPolicies.AdminGerir` + gate | `Pages\Admin\Users\Index.cshtml` / `Index.cshtml.cs` |
| `/admin/users` | POST (handler `ResetPassword`) | `Admin.Users.IndexModel.OnPostResetPasswordAsync` | `AdminPolicies.AdminGerir` + service gate | `Pages\Admin\Users\Index.cshtml.cs` |
| `/admin/users/create` | GET/POST | `Admin.Users.CreateModel.OnGetAsync` / `OnPostAsync` | `AdminPolicies.AdminGerir` + service gate | `Pages\Admin\Users\Create.cshtml` / `Create.cshtml.cs` |
| `/admin/users/edit` | GET | `Admin.Users.EditModel.OnGetAsync(string id)` | `AdminPolicies.AdminGerir` + service gate | `Pages\Admin\Users\Edit.cshtml` / `Edit.cshtml.cs` |
| `/admin/users/edit` | POST (handler `Save`) | `EditModel.OnPostSaveAsync` | `AdminPolicies.AdminGerir` + service gate | `Pages\Admin\Users\Edit.cshtml.cs` |
| `/admin/users/edit` | POST (handler `ResetPassword`) | `EditModel.OnPostResetPasswordAsync` | `AdminPolicies.AdminGerir` + service gate | `Pages\Admin\Users\Edit.cshtml.cs` |
| `/admin/templates` | GET | `Admin.Templates.IndexModel.OnGetAsync` | `AdminPolicies.AdminGerir` + service gate | `Pages\Admin\Templates\Index.cshtml` / `Index.cshtml.cs` |
| `/admin/templates/edit` | GET/POST | `Admin.Templates.EditModel.OnGetAsync(string? id)` / `OnPostAsync` | `AdminPolicies.AdminGerir` + service gate | `Pages\Admin\Templates\Edit.cshtml` / `Edit.cshtml.cs` |
| `/admin/applications` | GET/POST | `Admin.Applications.IndexModel.OnGetAsync` / `OnPostAsync(List<MirrorEntryInput>)` | `AdminPolicies.AdminGerir` + service gate | `Pages\Admin\Applications\Index.cshtml` / `Index.cshtml.cs` |
| `/admin/audit` | GET | `Admin.Audit.IndexModel.OnGetAsync` | `AdminPolicies.AuditView` + service gate | `Pages\Admin\Audit\Index.cshtml` / `Index.cshtml.cs` |
| `/admin/audit` | POST (handler `Export`) | `IndexModel.OnPostExportAsync` → returns `File(..., "text/csv", $"auditoria-{Year}.csv")` | `AdminPolicies.AuditExport` + `AdminAuditService.ExportAsync` gate | `Pages\Admin\Audit\Index.cshtml.cs` |

DI registrations and policies (`src\BA.Dmo.Web\Program.cs`):
- Policies: lines 99–107 (`AdminPolicies.AdminGerir` / `AuditView` / `AuditExport`).
- `AddSingleton<IAdminProvisioningAdapter>(_ => new SupabaseAdminProvisioningAdapter(...))` — line 161.
- `AddSingleton<IAdminRepository, DapperAdminRepository>()` — line 166.
- `AddSingleton<IModuleCatalogMirrorRepository, DapperModuleCatalogMirrorRepository>()` — line 167.
- `AddScoped<AdminAuthorizationGate>()`, `AddScoped<AdminUserService>()`, `AddScoped<AdminTemplateService>()`, `AddScoped<AdminMirrorService>()`, `AddScoped<AdminAuditService>()` — lines 177–181.
- CI invocation: `CliMode.BootstrapAdmin` → `BootstrapAdminCommand.Run()` — lines 46–47, 75.

## 13. Audit Technical Surface

- Route: `/admin/audit` (GET) and POST handler `Export` (Razor page handler, not a separate API endpoint).
- Query service: `AdminAuditService.QueryAsync(AuditQueryFilter)`.
- Export service: `AdminAuditService.ExportAsync(AuditQueryFilter)` — emits CSV text (columns `occurred_at_utc;year;actor_user_id;actor_name;module_id;action_code;entity_type;entity_id;entity_label;result;reason`), `PageSize = 0` for unlimited read.
- Capability checks: `AdminPolicies.AuditView` (page + `QueryAsync`), `AdminPolicies.AuditExport` (export handler + `ExportAsync`).
- DTO/filter types: `AuditQueryFilter`, `AuditEventRow`, `AuditQueryResult`, `AuditEntry` (all in `AdminModels.cs`).
- Repository: `DapperAdminRepository.QueryAuditAsync` / `InsertAuditEventAsync` over the shared `audit_events` table.
- Output type/format: `text/csv`; filename `auditoria-{Year ?? "tudo"}.csv` (literal in `Audit\Index.cshtml.cs` lines 94–97).
- Shared dependency: `audit_events` table (N01); export filter year/user/module/action/result/date-interval.

## 14. Static Assets

### Dedicated Admin static asset
| Asset file | Path | Principal selectors / wiring |
|---|---|---|
| `admin-layout.css` | `src\BA.Dmo.Web\wwwroot\styles\modules\admin-layout.css` | `body.admin-scope`, `.admin-work-area`, `.admin-app-list`, `.admin-app-row`, `.admin-app-name`, `.admin-app-state`, `.admin-availability-note`, `.admin-nav` (in shared layout), audit export form inline. |

### Shared static assets carrying Admin selectors
| Asset file | Path | Admin relevance |
|---|---|---|
| `dmo-layout.css` | `src\BA.Dmo.Web\wwwroot\styles\dmo-layout.css` | `.admin-nav`, admin composition/filter-grid, width tokens. |
| `dmo-components.css` | `src\BA.Dmo.Web\wwwroot\styles\dmo-components.css` | row actions / table-card chrome referencing admin patterns. |
| `dmo-tokens.css` | `src\BA.Dmo.Web\wwwroot\styles\dmo-tokens.css` | admin tokens `--dmo-admin-header-height`, `--dmo-admin-nav-height`, `--dmo-admin-main-max-width`. |
| `dmo-interactions.js` | `src\BA.Dmo.Web\wwwroot\scripts\dmo-interactions.js` | shared selection pattern extended for the admin form. |

No dedicated Admin JavaScript file exists; Admin forms are server-rendered Razor.

## 15. Tests

| Test class | Kind | Direct target | Main method groups | Location |
|---|---|---|---|---|
| `AdminUserServiceTests` | Unit (xUnit) | `AdminUserService` | capability gate fail-closed, create-user provisioning/validation/duplicate, partial failure + idempotent retry, update/concurrency, self-lockout, per-user module override guard, schema-migration guard, password reset, list email enrichment | `tests\BA.Dmo.UnitTests\Shared\Admin\AdminUserServiceTests.cs` |
| `AdminTemplateServiceTests` | Unit (xUnit) | `AdminTemplateService` | create/update canonical JSON persistence, invalid-grant rejection, duplicate id, self-lockout, concurrency, capability denial | `tests\BA.Dmo.UnitTests\Shared\Admin\AdminTemplateServiceTests.cs` |
| `AdminAuditAndMirrorTests` | Unit (xUnit) | `AdminAuditService`, `AdminMirrorService` | audit.view/audit.export separation, canonical pagination, factual export no-secrets, mirror unknown-module rejection, mirror audit | `tests\BA.Dmo.UnitTests\Shared\Admin\AdminAuditAndMirrorTests.cs` |
| `AdminWebAuthorizationTests` | Integration (WebApplicationFactory) | Admin pages + authorization wiring | unauthenticated redirect, forged POST denial, admin landing, admin.gerir-only login, jobon excluded, audit page requires audit.view | `tests\BA.Dmo.IntegrationTests\Access\AdminWebAuthorizationTests.cs` |
| `AdminFormAntiforgeryTests` | Integration (WebApplicationFactory) | Admin forms antiforgery pipeline | token render, tokenless POST 400 + no write, token create, cross-session token reject, anonymous redirect, operator policy denial | `tests\BA.Dmo.IntegrationTests\Access\AdminFormAntiforgeryTests.cs` |
| `AdminUserListResetTests` | Integration (WebApplicationFactory) | Admin user-list reset + edit reset | shared service path, audit + provisioning, unknown user error, edit-page path | `tests\BA.Dmo.IntegrationTests\Access\AdminUserListResetTests.cs` |
| `AdminSecurityGuardTests` | Integration (guard) | Admin services/pages security contracts | privileged provisioning not page-reachable, pages authorize via capability policy only, Admin services depend on ports only | `tests\BA.Dmo.IntegrationTests\Access\AdminSecurityGuardTests.cs` |
| `DapperAdminRepositoryProjectionTests` | Integration (projection) | `DapperAdminRepository.UserColumns` | `ListUsersAsync` materializes `AdminUserRow` with `AuthEmail == null` pre-enrichment via real Dapper + in-memory reader | `tests\BA.Dmo.IntegrationTests\Access\DapperAdminRepositoryProjectionTests.cs` |
| `SupabaseAdminProvisioningAdapterTests` | Integration (adapter) | `SupabaseAdminProvisioningAdapter` | paginated email lookup, create/idempotent-conflict, password reset, service-role isolation | `tests\BA.Dmo.IntegrationTests\Identity\SupabaseAdminProvisioningAdapterTests.cs` |
| `BootstrapAdminCliTests` | Integration (CLI) | `BootstrapAdminCommand` | no-config fail, partial-config listing, missing DB config | `tests\BA.Dmo.IntegrationTests\Cli\BootstrapAdminCliTests.cs` |

Note: shared-catalog / shared-identity test files that Admin depends on but whose direct target is not Admin are handled by the catalog/identity maps: `ModuleCatalogMirrorSynchronizerTests` (Unit, Shared\Access) targets the mirror synchronizer consumed by `AdminMirrorService`; `BootstrapAdminServiceTests` (Unit, Shared\Identity) targets the shared bootstrap service. They are out of Admin's test scope.

## 16. Test Doubles / Helpers

| File | Double / helper | Implements / role |
|---|---|---|
| `tests\BA.Dmo.UnitTests\Shared\Admin\FakeAdminRepository.cs` | `FakeAdminRepository` | Dedicated in-memory fake of `IAdminRepository` (Users/Templates/Audits/Writes, self-lockout/concurrency/schema-migration simulation), shared by the Unit `Shared\Admin` tests. |
| `AdminUserServiceTests.cs` | `FakeProvisioning`, `FakeCurrentUserAccessor`, `FixedClock` | In-file fakes of `IAdminProvisioningAdapter`, `ICurrentUserAccessor`, `IClock`. |
| `AdminTemplateServiceTests.cs` | `FakeCurrentUserAccessor`, `FixedClock` | In-file fakes. |
| `AdminAuditAndMirrorTests.cs` | `FakeMirrorRepository`, `FakeCurrentUserAccessor`, `FixedClock` | In-file fakes of `IModuleCatalogMirrorRepository`, `ICurrentUserAccessor`, `IClock`. |
| `AdminWebAuthorizationTests.cs` | `AdminFixture` (`WebApplicationFactory<Program>`), `FakeAdminWritesRepository`, `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeMirrorRepository` | In-file `WebApplicationFactory` fixture replacing auth/identity/Admin/mirror collaborators. |
| `AdminFormAntiforgeryTests.cs` | `AfFixture` (`WebApplicationFactory<Program>`), `FakeAdminRepository`, `FakeMirrorRepository`, `FakeProvisioningAdapter`, `FakeAuthAdapter`, `FakeIdentityRepository` | In-file `WebApplicationFactory` fixture with antiforgery enforced. |
| `AdminUserListResetTests.cs` | `ResetFixture` (`WebApplicationFactory<Program>`), `RecordingProvisioningAdapter`, `RecordingAdminRepository`, `FakeAuthAdapter`, `FakeIdentityRepository`, `NoopMirror` | In-file `WebApplicationFactory` fixture recording resets/audits. |
| `DapperAdminRepositoryProjectionTests.cs` | `DataReaderDbConnection`, `DataReaderDbCommand`, `NoParameterCollection`, `FixedReaderConnectionFactory` | In-file ADO.NET/connection doubles exercising the real Dapper projection. |

`In-file test fixture files` count: **7** (`AdminUserServiceTests.cs`, `AdminTemplateServiceTests.cs`, `AdminAuditAndMirrorTests.cs`, `AdminWebAuthorizationTests.cs`, `AdminFormAntiforgeryTests.cs`, `AdminUserListResetTests.cs`, `DapperAdminRepositoryProjectionTests.cs`).

## 17. Direct Admin References

- `AdminUserService` → `AdminAuthorizationGate` (constructor)
- `AdminUserService` → `IAdminRepository` (constructor)
- `AdminUserService` → `IAdminProvisioningAdapter` (constructor)
- `AdminUserService` → `CanonicalCapabilities` (`AdminGerir`, `AdminModuleId`, `AuditView`) — same file declaration
- `AdminUserService` → `GrantNormalizer`, `CanonicalModuleCatalog` (module validation)
- `AdminUserService` → `AdminModels` types (`AdminUserRow`, `Create/Update/Change/Set` requests, `TemplateGrantInput`, `AuditEntry`)
- `AdminTemplateService` → `AdminAuthorizationGate`, `IAdminRepository`, `GrantNormalizer`, `IClock` (constructors)
- `AdminTemplateService` → `AdminModels` types (`AdminTemplateRow`, `Create/UpdateTemplateRequest`, `TemplateGrantInput`, `AuditEntry`)
- `AdminAuditService` → `AdminAuthorizationGate`, `IAdminRepository` (constructors)
- `AdminAuditService` → `AdminModels` types (`AuditQueryFilter`, `AuditQueryResult`, `AuditEventRow`)
- `AdminMirrorService` → `AdminAuthorizationGate`, `ModuleCatalog`, `IModuleCatalogMirrorRepository`, `IAdminRepository`, `IClock` (constructors)
- `AdminMirrorService` → `ModuleCatalogMirrorSynchronizer` (constructed internally), `MirrorDisplayEntry`, `AuditEntry`
- `IAdminRepository` → `AdminModels` types (method signatures)
- `DapperAdminRepository` → `IAdminRepository` (implements)
- `DapperAdminRepository` → `internal_users`, `access_templates`, `audit_events` (reads/writes, shared tables)
- `SupabaseAdminProvisioningAdapter` → `IAdminProvisioningAdapter` (implements)
- `CanonicalCapabilities` → `CanonicalModuleCatalog` ids (admin.gerir / audit.view / audit.export)
- Admin page models → `AdminUserService` / `AdminTemplateService` / `AdminAuditService` / `AdminMirrorService` (constructors)
- Admin pages → `AdminPolicies.*` (razor `[Authorize]` attributes)
- `Program.cs` → `AdminPolicies.*`, `CanonicalCapabilities.*`, Admin services, `DapperAdminRepository`, `SupabaseAdminProvisioningAdapter` (policies + DI)

## 18. External Technical References

| Admin Object | External Technical Reference | Reference Type |
|---|---|---|
| `AdminUserService`, `AdminTemplateService`, `AdminMirrorService`, `AdminAuditService`, `AdminAuthorizationGate` | `Result<T, DomainError>`, `DomainError.Forbidden/Validation/NotFound/DomainConflict/ConcurrencyConflict/BackendUnavailable` (`Domain.Shared.Kernel`) | shared domain kernel (method/constructor dependency) |
| `AdminAuthorizationGate` | `ICurrentUserAccessor`, `CurrentUser.HasCapability` (`Domain\Shared\Access\CurrentUser.cs`) | shared identity dependency |
| `AdminUserService`, `AdminTemplateService`, `AdminMirrorService` | `GrantNormalizer`, `ModuleCatalog`, `CanonicalModuleCatalog`, `CanonicalPageCatalog` (`Application\Shared\Access`) | shared access catalog / static consumer |
| `AdminUserService`, `AdminTemplateService` grant parsing | `AccessTemplateGrantsParser` (`Application\Shared\Identity`) | shared template resolution consumer |
| `AdminMirrorService` | `IModuleCatalogMirrorRepository`, `ModuleCatalogMirrorSynchronizer`, `ModuleCatalogMirrorRow`, `MirrorDisplayEntry` (`Application\Shared\Access`) | shared catalog / port dependency |
| `AdminUserService` | `IAdminProvisioningAdapter` (`Application\Shared\Identity\SupabaseAuthPorts.cs`) | shared privileged-provisioning port (Admin consumer) |
| `BootstrapAdminService` / `BootstrapAdminCommand` | `IAdminProvisioningAdapter`, `IInternalUserRepository` | shared identity bootstrap dependency (uses Admin provisioning port) |
| `CanonicalCapabilities.AuditView` | `HistoriaAuthorizationGate` (História MAP-14 reads `audit.view`) | shared capability consumed by another module |
| `AdminPolicies.*` | `CapabilityAuthorizationHandler` / `CapabilityRequirement` | shared web authorization wiring |
| Admin pages | `PageModel`, `HttpContext`, antiforgery (Microsoft.AspNetCore.Mvc.RazorPages) | framework base types |
| `DapperAdminRepository` | `IDbConnectionFactory`, `DapperUnitOfWork`, `ConcurrencyGuard`, `SchemaMigrationRequiredException` (`Application\Shared\Persistence` + `Infrastructure\Persistence`) | application port / query dependency |
| `DapperAdminRepository` | Dapper `Db.QueryAsync`/`ExecuteAsync` / `DynamicParameters`, Npgsql `PostgresException` | query/read dependency (external lib) |
| `DapperAdminRepository` | `internal_users`, `access_templates`, `audit_events` tables | shared DB dependency (read/write) |
| `DapperModuleCatalogMirrorRepository` | `IModuleCatalogMirrorRepository` port, `module_catalog_mirror` table | shared DB/catalog dependency (Admin consumer) |
| `SupabaseAdminProvisioningAdapter` | Supabase Auth admin API (service-role), SupabaseSettings | auth provider adapter |
| `AdminUserService` | `IClock` (`SystemClock.Instance`) | framework/shared clock dependency |

## 19. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `AdminAuthorizationGate` / `AdminExecutor` | Application | `src\BA.Dmo.Application\Modules\Admin\AdminAuthorizationGate.cs` |
| `AdminUserService` | Application | `src\BA.Dmo.Application\Modules\Admin\AdminUserService.cs` |
| `CanonicalCapabilities` | Application | `src\BA.Dmo.Application\Modules\Admin\AdminUserService.cs` |
| `AdminTemplateService` | Application | `src\BA.Dmo.Application\Modules\Admin\AdminTemplateService.cs` |
| `AdminAuditService` | Application | `src\BA.Dmo.Application\Modules\Admin\AdminAuditService.cs` |
| `AdminMirrorService` | Application | `src\BA.Dmo.Application\Modules\Admin\AdminMirrorService.cs` |
| `AdminModels` (rows/filters/requests) | Application | `src\BA.Dmo.Application\Modules\Admin\AdminModels.cs` |
| `IAdminRepository` | Application (port) | `src\BA.Dmo.Application\Modules\Admin\IAdminRepository.cs` |
| `DapperAdminRepository` | Infrastructure | `src\BA.Dmo.Infrastructure\Access\DapperAdminRepository.cs` |
| `SupabaseAdminProvisioningAdapter` | Infrastructure | `src\BA.Dmo.Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs` |
| `DapperModuleCatalogMirrorRepository` | Infrastructure (shared, consumed by Admin) | `src\BA.Dmo.Infrastructure\Access\DapperModuleCatalogMirrorRepository.cs` |
| Admin Index + 4-area pages (8 PageModels + 8 .cshtml) | Web | `src\BA.Dmo.Web\Pages\Admin\` |
| `AdminPolicies.*` | Web (Authorization) | `src\BA.Dmo.Web\Authorization\CapabilityAuthorizationHandler.cs` |
| Admin policies + DI registration | Web | `src\BA.Dmo.Web\Program.cs` |
| `admin-layout.css` | Web (static) | `src\BA.Dmo.Web\wwwroot\styles\modules\admin-layout.css` |
| `internal_users`, `access_templates`, `audit_events` (shared, read/write) | Database | `database\migrations\N01_identity.sql` (+N25/N26) |
| `module_catalog_mirror` (shared, read/write) | Database | `database\migrations\N02_catalog.sql` |
| Admin service tests / `FakeAdminRepository` | Tests | `tests\BA.Dmo.UnitTests\Shared\Admin\` |
| Admin integration tests / fixtures | Tests | `tests\BA.Dmo.IntegrationTests\Access\`, `Cli\`, `Identity\` |

## 20. Sources Verified

- `src\BA.Dmo.Application\Modules\Admin\AdminUserService.cs`
- `src\BA.Dmo.Application\Modules\Admin\AdminTemplateService.cs`
- `src\BA.Dmo.Application\Modules\Admin\AdminAuditService.cs`
- `src\BA.Dmo.Application\Modules\Admin\AdminMirrorService.cs`
- `src\BA.Dmo.Application\Modules\Admin\AdminAuthorizationGate.cs`
- `src\BA.Dmo.Application\Modules\Admin\AdminModels.cs`
- `src\BA.Dmo.Application\Modules\Admin\IAdminRepository.cs`
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`
- `src\BA.Dmo.Application\Shared\Access\CanonicalPageCatalog.cs`
- `src\BA.Dmo.Application\Shared\Access\ModuleCatalogMirrorSynchronizer.cs`
- `src\BA.Dmo.Application\Shared\Access\IModuleCatalogMirrorRepository.cs`
- `src\BA.Dmo.Application\Shared\Identity\SupabaseAuthPorts.cs`
- `src\BA.Dmo.Application\Shared\Identity\BootstrapAdminService.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperAdminRepository.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperModuleCatalogMirrorRepository.cs`
- `src\BA.Dmo.Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs`
- `src\BA.Dmo.Web\Program.cs`
- `src\BA.Dmo.Web\Authorization\CapabilityAuthorizationHandler.cs`
- `src\BA.Dmo.Web\Pages\Admin\` (Index, Applications/Index, Audit/Index, Templates/Index+Edit, Users/Index+Create+Edit — .cshtml + .cshtml.cs)
- `src\BA.Dmo.Web\wwwroot\styles\modules\admin-layout.css`
- `src\BA.Dmo.Web\wwwroot\scripts\dmo-interactions.js`
- `src\BA.Dmo.Web\wwwroot\styles\dmo-components.css`, `dmo-layout.css`, `dmo-tokens.css`
- `database\migrations\N01_identity.sql`, `N02_catalog.sql`, `N25_remediation.sql`, `N26_user_modules_override.sql`
- `maps\02_DATABASE.md`, `maps\03_MIGRATIONS.md`
- `tests\BA.Dmo.UnitTests\Shared\Admin\AdminUserServiceTests.cs`, `AdminTemplateServiceTests.cs`, `AdminAuditAndMirrorTests.cs`, `FakeAdminRepository.cs`
- `tests\BA.Dmo.IntegrationTests\Access\AdminWebAuthorizationTests.cs`, `AdminFormAntiforgeryTests.cs`, `AdminUserListResetTests.cs`, `AdminSecurityGuardTests.cs`, `DapperAdminRepositoryProjectionTests.cs`
- `tests\BA.Dmo.IntegrationTests\Cli\BootstrapAdminCliTests.cs`
- `tests\BA.Dmo.IntegrationTests\Identity\SupabaseAdminProvisioningAdapterTests.cs`

## Counts

- Domain Admin files: **0**
- Application Admin files: **7**
- Infrastructure Admin files: **2**
- Shared infrastructure dependencies: **7** (`IDbConnectionFactory`, Dapper, Npgsql, `DapperUnitOfWork`/`ConcurrencyGuard`, `SchemaMigrationRequiredException`, `SupabaseSettings`, `DapperModuleCatalogMirrorRepository` — shared infra consumed by Admin)
- Dedicated Web page files: **16** (8 pages × `.cshtml` + `.cshtml.cs`)
- Dedicated static asset files: **1** (`admin-layout.css`)
- Shared Web wiring files: **2** (`src\BA.Dmo.Web\Program.cs`, `src\BA.Dmo.Web\Authorization\CapabilityAuthorizationHandler.cs`)
- Shared static asset files: **4** (`dmo-interactions.js`, `dmo-components.css`, `dmo-layout.css`, `dmo-tokens.css`) with Admin selectors
- Shared Application catalog files: **8** (`CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`, `GrantNormalizer.cs`, `ModuleCatalogMirrorSynchronizer.cs`, `IModuleCatalogMirrorRepository.cs`, `AccessTemplateGrantsParser.cs`, `SupabaseAuthPorts.cs`, `BootstrapAdminService.cs`)
- Admin-specific DB tables: **0**
- Admin-specific DB indexes: **0**
- Admin-specific DB triggers: **0**
- Admin-specific DB objects: **0** (0 tables + 0 indexes + 0 triggers)
- Shared / external DB dependencies: **4** (`internal_users`, `access_templates`, `audit_events`, `module_catalog_mirror`, each with shared indexes/constraints/trigger)
- Distinct Admin-specific migration files: **0**
- Capabilities: **3**
- Capability IDs: `admin.gerir`, `audit.view`, `audit.export`
- Authorization / Catalog: **MAPPED**
- Admin technical areas: **Users · Templates · Applications · Audit**
- Web / Routes: **MAPPED** (Razor page handlers; no `/api/admin` endpoints)
- Audit surface: **MAPPED** (page + query service + CSV export)
- Static assets: **MAPPED**
- Test classes: **10**
- Dedicated test support files: **1** (`FakeAdminRepository.cs`)
- In-file test fixture files: **7**
- Source-visible user surfaces: **1** (Admin)