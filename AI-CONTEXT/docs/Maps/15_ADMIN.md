# BA DMO — Admin Technical Map

MAP ID: MAP-15
Status: COMPLETE (reconciled at HEAD 8478308 "Render one persistent Admin navigation")

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

This map inventories the Admin module (administração do portal). Admin exposes one canonical module (`admin`) with a single landing page (`/admin`) plus four sub-areas rendered as ONE persistent tab strip: Utilizadores (`/admin/users`), Templates (`/admin/templates`), Aplicações (`/admin/applications`) and Auditoria (`/admin/audit`). The tab strip is rendered by the shared layout for the whole `/admin` scope once (single-render marker), with legacy page-level `<partial name="_AdminNav" />` calls kept harmless (commits 1f91dfe "Fix admin navigation as persistent tabs" … 8478308 "Render one persistent Admin navigation").

All operations run through Razor PageModels (no separate `/api/admin` endpoints), re-authorize server-side via `AdminAuthorizationGate`, and operate on the CURRENT single-template access model: Aplicações (canonical catalog) → Template (title/function + ONE functional profile + module grants) → User (ONE reusable template). Template-owned functional profiles and the single-assignment junction are schema objects of N27/N31 (see [03_MIGRATIONS.md](03_MIGRATIONS.md)); the Admin read/write surface now includes `access_templates`, `internal_users`, `internal_user_access_templates`, `access_template_profiles`, `audit_events` and `module_catalog_mirror` (profiles are written by the Web-layer `TemplateProfileStore`, not by `DapperAdminRepository`).

The map covers only what exists in source: objects, locations, members, routes, capability declarations, DB references, migrations, tests and direct/external references. It does not explain end-to-end flow (see [16_USERS_ACCESS.md](16_USERS_ACCESS.md)) and does not absorb the Login surface ([18_LOGIN.md](18_LOGIN.md)).

## 2. Layer Summary

| Layer | Dedicated Admin objects | Location |
|---|---|---|
| Domain | 0 | — |
| Application | 7 | `src\BA.Dmo.Application\Modules\Admin\` |
| Infrastructure | 2 | `src\BA.Dmo.Infrastructure\Access\DapperAdminRepository.cs`, `src\BA.Dmo.Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs` |
| Database | 0 dedicated (reads/writes shared access/audit/mirror/profile/junction tables) | — |
| Migrations | 0 dedicated (N27/N31 are shared access-model migrations; Admin consumes them) | — |
| Web pages | 17 | `src\BA.Dmo.Web\Pages\Admin\` (8 pages × `.cshtml`+`.cshtml.cs` = 16, plus `TemplateProfileStore.cs`) |
| Web endpoints | 0 JSON (Razor page handlers only) | `src\BA.Dmo.Web\Program.cs` (policies + DI) |
| Static assets | 1 dedicated CSS | `src\BA.Dmo.Web\wwwroot\styles\modules\admin-layout.css` |
| Tests | 10 classes + doubles | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Admin\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\`, `Cli\`, `Identity\` |

### 2.1 Layer Coverage

| Layer | Present | Primary locations |
|---|---|---|
| Domain | NO | — (no dedicated Admin Domain object) |
| Application | YES | `src\BA.Dmo.Application\Modules\Admin\` |
| Infrastructure | YES | `src\BA.Dmo.Infrastructure\Access\DapperAdminRepository.cs`, `src\BA.Dmo.Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs` |
| Web | YES | `src\BA.Dmo.Web\Pages\Admin\` (incl. `TemplateProfileStore.cs`); `src\BA.Dmo.Web\Program.cs`; `Authorization\CapabilityAuthorizationHandler.cs` |
| Database | NO | — (reads/writes shared `internal_users`, `access_templates`, `internal_user_access_templates`, `access_template_profiles`, `audit_events`, `module_catalog_mirror`; no Admin-specific DB object) |
| Tests | YES | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Admin\`, `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\`, `Cli\`, `Identity\` |

This is technical navigation only; it does not explain workflow. `Present = NO` is a valid, source-verified value.

## 3. Domain Objects

No dedicated Admin Domain object exists. The Domain project (`src\BA.Dmo.Domain\Modules\`) contains no `Admin` folder. Admin consumes only shared Domain kernel/access types — including `FunctionalProfile` / `FunctionalProfileNames` (`src\BA.Dmo.Domain\Shared\Access\FunctionalProfile.cs`, the three-profile enum Admin / Operador / Controlador / Responsável) — see section 18.

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
  - `Task<Result<AdminUserRow, DomainError>> CreateUserAsync(CreateAdminUserRequest, CancellationToken)` — creates the Auth account via `_provisioning.EnsureAuthUserAsync`, then the internal user + its single junction row; validates the chosen template against the requested profile (`ValidateProfileTemplatesAsync`, `ADMIN_PROFILE_TEMPLATE_MISMATCH`); uses `templateIds[0]` as the primary `internal_users.template_id`.
  - `Task<Result<AdminUserRow, DomainError>> UpdateUserAsync(UpdateAdminUserRequest, CancellationToken)`.
  - `Task<Result<AdminUserRow, DomainError>> ChangeTemplateAsync(ChangeUserTemplateRequest, CancellationToken)` — legacy single-template entry that delegates to `ChangeTemplatesAsync` with a one-element list.
  - `Task<Result<AdminUserRow, DomainError>> ChangeTemplatesAsync(ChangeUserTemplatesRequest, CancellationToken)` — replace-assignment via `_repository.ReplaceUserAccessTemplatesAsync`; self-lockout guarded.
  - `Task<Result<AdminUserRow, DomainError>> SetActiveAsync(SetUserActiveRequest, CancellationToken)` — guarded by self-lockout.
  - `Task<Result<AdminUserRow, DomainError>> SaveUserAsync(string actorId, string displayName, string? profileTitle, IReadOnlyList<string> templateIds, bool active, DateTimeOffset expectedUpdatedAt, CancellationToken)` — composite save (update → template-replace → activation) used by the Edit page with a single-element template list.
  - `Task<Result<bool, DomainError>> RequestPasswordResetAsync(string targetActorId, CancellationToken)` — via `_provisioning.RequestPasswordResetAsync`.
- Private helpers: `ValidateProfileTemplatesAsync` (profile↔template module-set consistency, incl. "Admin profile may only use Administration templates" / "operational profiles may not receive Administration templates"), `IsValidEmail`, `AuditAsync`.
- Constants: `PasswordPolicyMinLength = 8`; `SchemaMigrationUnavailableCode = "SCHEMA_MIGRATION_REQUIRED"`.
- Error codes produced: `ADMIN_USER_INVALID`, `ADMIN_USER_INVALID_EMAIL`, `ADMIN_USER_WEAK_PASSWORD`, `ADMIN_USER_PROFILE_INVALID`, `ADMIN_TEMPLATE_INVALID`, `ADMIN_PROFILE_TEMPLATE_MISMATCH`, `ADMIN_USER_ALREADY_REGISTERED`, `ADMIN_SELF_LOCKOUT`, `ADMIN_CONCURRENCY_CONFLICT`, `INTERNAL_USER_NOT_FOUND`, `ADMIN_USER_NO_AUTH_ACCOUNT`, `SCHEMA_MIGRATION_REQUIRED`, plus `ADMIN_FORBIDDEN` (gate).
- REMOVED vs previous map revision: `SaveUserWithModulesAsync` and `SaveUserModulesAsync` no longer exist in source; there is no per-user module-grant editor anymore (see LEGACY items in section 5/10). `JsonOptions` is no longer in this file (it lives in `AdminTemplateService`).

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
- Grant validation (`ValidateGrants`, strict, current model): templates assign MODULES ONLY — any submitted capability array (>0 capabilities) rejects the write with `ACCESS_TEMPLATE_GRANTS_INVALID` ("os templates atribuem apenas módulos; o perfil determina as capacidades"); grants are normalized via `GrantNormalizer` and any discarded (unknown/non-assignable/duplicate) entry rejects the whole write; the canonical `modules` JSON is persisted to `access_templates.modules`.
- The template's functional profile is NOT part of `AdminTemplateService` — it is persisted by the Web-layer `TemplateProfileStore` (section 12) after the service write.
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
  - `Task<Result<IReadOnlyList<MirrorDisplayEntry>, DomainError>> SaveDisplayAsync(IReadOnlyList<MirrorEntryInput>, CancellationToken)` — rejects module ids outside the canonical catalog; audits `mirror_update`.
- Error codes produced: `CATALOG_MIRROR_INVALID`, plus `ADMIN_FORBIDDEN` (gate).

### AdminModels
- File: `AdminModels.cs`
- Records (current shape):
  - `AdminUserRow` — now a mutable class-style record with parameterless ctor (Dapper property materialization) and members `ActorId`, `AuthUserId`, `DisplayName`, `ProfileTitle`, `TemplateId` (compatibility pointer), `Active`, `UpdatedAtUtc`, `AuthEmail`, `ModulesOverrideJson` (dormant, see section 10), `TemplateIds` (string[] — junction template list from `internal_user_access_templates`, populated by a subquery in `DapperAdminRepository.UserColumns`); computed `AssignedTemplateIds` falls back to `[TemplateId]`.
  - `AdminTemplateRow(string TemplateId, string Name, string ModulesJson, bool Active, DateTimeOffset UpdatedAtUtc)`.
  - `AuditQueryFilter(...)` with `static int[] CanonicalPageSizes = [20, 40, 60]` and `static bool IsValidPageSize(int)`.
  - `AuditEventRow(...)`, `AuditQueryResult(...)`, `AuditEntry(...)` (as before, with optional `BeforeSummary`/`AfterSummary`).
  - `CreateAdminUserRequest(string Email, string Password, string DisplayName, string? ProfileTitle, string TemplateId, bool Active = true, IReadOnlyList<string>? TemplateIds = null)` with computed `AssignedTemplateIds` (`TemplateIds` else `[TemplateId]`).
  - `UpdateAdminUserRequest(string ActorId, string DisplayName, string? ProfileTitle, DateTimeOffset ExpectedUpdatedAt)`.
  - `ChangeUserTemplateRequest(string ActorId, string TemplateId, DateTimeOffset ExpectedUpdatedAt)` (legacy single).
  - `ChangeUserTemplatesRequest(string ActorId, IReadOnlyList<string> TemplateIds, DateTimeOffset ExpectedUpdatedAt)` (current replace-assignment).
  - `SetUserActiveRequest(string ActorId, bool Active, DateTimeOffset ExpectedUpdatedAt)`.
  - `TemplateGrantInput(string ModuleId, IReadOnlyList<string> Capabilities)`.
  - `CreateTemplateRequest(string TemplateId, string Name, IReadOnlyList<TemplateGrantInput> Grants)`.
  - `UpdateTemplateRequest(string TemplateId, string Name, IReadOnlyList<TemplateGrantInput> Grants, bool Active, DateTimeOffset ExpectedUpdatedAt)`.
  - `MirrorEntryInput(string ModuleId, int DisplayOrder, bool Active)`.

## 5. Application Contracts / Ports

### IAdminRepository (Admin-specific port)
- File: `src\BA.Dmo.Application\Modules\Admin\IAdminRepository.cs`
- Type: `public interface IAdminRepository`
- Methods (current):
  - Internal users: `ListUsersAsync`, `GetUserAsync`, `AuthUserIdAlreadyRegisteredAsync`, `CreateInternalUserAsync`, `UpdateUserAsync`, `ChangeUserTemplateAsync` (legacy single — delegates), `ReplaceUserAccessTemplatesAsync` (current replace-assignment, keeps `internal_users.template_id` in sync as compatibility key), `SetUserActiveAsync`, `SetUserModulesOverrideAsync` (dormant — no caller in `src\`; see section 10).
  - Self-lockout support: `CountActiveAdminsAsync(string? excludeActorId, CancellationToken)`.
  - Access templates: `ListTemplatesAsync`, `GetTemplateAsync`, `CreateTemplateAsync`, `UpdateTemplateAsync`.
  - Audit: `InsertAuditEventAsync(AuditEntry, CancellationToken)`, `QueryAuditAsync(AuditQueryFilter, CancellationToken)`.
- Implementation (registering): `DapperAdminRepository` (see section 9).
- Documents concurrency via `updated_at`; guarded writes validated in the same transaction.

**LEGACY CANDIDATE — NEEDS AUDIT:** `IAdminRepository.ChangeUserTemplateAsync` and `DapperAdminRepository.ChangeUserTemplateAsync` (lines 235–242 of `DapperAdminRepository.cs`) remain as single-template delegation shims to `ReplaceUserAccessTemplatesAsync`; no caller in `src\` uses the port method directly (the service's `ChangeTemplateAsync` calls the plural path). `SetUserModulesOverrideAsync` (port + Dapper implementation) has no caller — the N26 override is dormant (N27 nulls the column; `IdentityResolutionService` ignores it).

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
| `IAdminRepository` | List/Get/Create/Update users, template CRUD, junction replace-assignment, admins count, audit insert/query | `DapperAdminRepository` | shared `internal_users`, `access_templates`, `internal_user_access_templates`, `audit_events` (shared DB) | `Application\Modules\Admin\IAdminRepository.cs` → `Infrastructure\Access\DapperAdminRepository.cs` |
| `IAdminProvisioningAdapter` (shared port) | `EnsureAuthUserAsync`, `EnsureAuthUserWithStatusAsync`, `RequestPasswordResetAsync`, `GetUserEmailsAsync` | `SupabaseAdminProvisioningAdapter` | Supabase Auth provider (service-role) | `Shared\Identity\SupabaseAuthPorts.cs` → `Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs` |
| `IModuleCatalogMirrorRepository` (shared port) | `GetAllAsync`, `UpsertAllAsync` | `DapperModuleCatalogMirrorRepository` | shared `module_catalog_mirror` table | `Shared\Access\IModuleCatalogMirrorRepository.cs` → `Infrastructure\Access\DapperModuleCatalogMirrorRepository.cs` |

## 6. Authorization / Capabilities / Catalog

- Module id: `admin` (`CanonicalModuleCatalog.AdminModuleId`, `CanonicalCapabilities.AdminModuleId`).
- Canonical module entry: `new ModuleDefinition(AdminModuleId, "Administração", ModuleKind.Module, 99, "/admin", new[] { Capability(admin.gerir), Capability(audit.view), Capability(audit.export) })` in `CanonicalModuleCatalog.Build()` (`src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`).
- Page id: `admin.gestao` (`CanonicalPageCatalog.AdminGestaoPageId`), page entry `new PageDefinition(AdminGestaoPageId, AdminModuleId, "/admin", requiredCapabilityId: AdminGerirCapabilityId, displayOrder: 99)` (not landing) in `CanonicalPageCatalog.Build()` (`src\BA.Dmo.Application\Shared\Access\CanonicalPageCatalog.cs`).
- Capability ids declared in Admin source (`CanonicalCapabilities` in `AdminUserService.cs`) AND in `CanonicalModuleCatalog`: `admin.gerir`, `audit.view`, `audit.export`. In the current model these capabilities are DERIVED at resolution from the Admin functional profile × admin module (`AccessResolver.ProjectProfileCapabilities` in `src\BA.Dmo.Application\Shared\Access\AccessResolver.cs`) — stored capability arrays in template JSON are no longer authorization input.
- Web policies (`AdminPolicies` in `src\BA.Dmo.Web\Authorization\CapabilityAuthorizationHandler.cs`): `AdminGerir = "BaDmo.Admin.Gerir"`, `AuditView = "BaDmo.Audit.View"`, `AuditExport = "BaDmo.Audit.Export"`.
- Policy enforcement:
  - `options.AddPolicy(AdminPolicies.AdminGerir, ...)`, `.AuditView`, `.AuditExport` built on `CapabilityRequirement(CanonicalCapabilities.*)` — `Program.cs` lines 105–113.
  - Razor pages carry `@attribute [Authorize(Policy = ...)]` (see section 12).
  - Server-side re-check: `AdminAuthorizationGate.Require(...)` inside every Admin service method.

| Capability | Declared In | Policy / Check | Technical Consumers |
|---|---|---|---|
| `admin.gerir` | `CanonicalCapabilities.AdminGerir` (`AdminUserService.cs`); `CanonicalModuleCatalog.AdminGerirCapabilityId` | `AdminPolicies.AdminGerir` (`CapabilityAuthorizationHandler.cs`) + `AdminAuthorizationGate.Require(AdminGerir)` | `/admin`, `/admin/users`, `/admin/users/create`, `/admin/users/edit`, `/admin/templates`, `/admin/templates/edit`, `/admin/applications` pages; `AdminUserService`, `AdminTemplateService`, `AdminMirrorService` methods |
| `audit.view` | `CanonicalCapabilities.AuditView` (`AdminUserService.cs`); `CanonicalModuleCatalog.AuditViewCapabilityId` | `AdminPolicies.AuditView` + `AdminAuthorizationGate.Require(AuditView)` | `/admin/audit` page; `AdminAuditService.QueryAsync`; referenced by `HistoriaAuthorizationGate` (História MAP-14) |
| `audit.export` | `CanonicalCapabilities.AuditExport` (`AdminUserService.cs`); `CanonicalModuleCatalog.AuditExportCapabilityId` | `AdminPolicies.AuditExport` + `AdminAuthorizationGate.Require(AuditExport)` | `/admin/audit` Export handler; `AdminAuditService.ExportAsync` |

## 7. User Surface

**Admin.** Source exposes a single Admin landing page (`/admin`, `IndexModel` with no body model) plus the Users/Templates/Applications/Audit sub-pages. There is no Operador/Responsável/User-variant of the Admin pages — the Admin module is one surface. The `audit.view` / `audit.export` capabilities gate the Admin Audit page and its export control, but they are capability-conditioned controls on the same Admin surface, not separate user surfaces. In the current model the Admin functional profile (`access_template_profiles.functional_profile = 'Admin'`) is the ONLY profile that resolves to the `admin` module; `AccessResolver` fails closed on any hybrid (see [16_USERS_ACCESS.md](16_USERS_ACCESS.md) §8).

## 8. Admin Technical Areas

Source-grounded sub-areas contained inside the single Admin User Surface (not canonical modules). All four appear in ONE persistent tab strip rendered by `_AdminNav.cshtml` (section 12):

- **Utilizadores** — pages `/admin/users`, `/admin/users/create`, `/admin/users/edit`; `AdminUserService`; the editor assigns EXACTLY ONE reusable template (single `<select name="templateId">`); the template supplies title/function + functional profile + modules; the per-user module-override editor NO LONGER EXISTS (the previous `SaveUserWithModulesAsync` flow was removed).
- **Templates** — pages `/admin/templates`, `/admin/templates/edit`; `AdminTemplateService` (module-only grant validation) + Web-layer `TemplateProfileStore` (functional profile on `access_template_profiles`); one profile per template enforced by `ck_access_template_profiles_functional_profile` and the N31 trigger.
- **Aplicações** — page `/admin/applications`; `AdminMirrorService` over the shared `module_catalog_mirror` (display/order only, never authorization).
- **Auditoria** — page `/admin/audit`; `AdminAuditService` over the shared `audit_events`.

These are subsections of 15_ADMIN.md; they are not separate maps and do not appear as canonical modules in the INDEX.

## 9. Infrastructure Objects

### DapperAdminRepository (Admin-specific)
- File: `src\BA.Dmo.Infrastructure\Access\DapperAdminRepository.cs`
- Type: `public sealed class DapperAdminRepository : IAdminRepository`
- Constructor dependency: `IDbConnectionFactory _connectionFactory`.
- SQL-bearing members:
  - `const string UserColumns` — projection over `internal_users` (actor_id, auth_user_id, display_name, profile_title, template_id, active, updated_at_utc, `NULL::text AS AuthEmail`, `modules_override::text`) PLUS an `ARRAY(SELECT ut.template_id FROM internal_user_access_templates ut WHERE ut.actor_id = u.actor_id ORDER BY ut.template_id) AS TemplateIds` subquery (junction list).
  - `const string TemplateColumns` — projection over `access_templates`.
  - `const string AdminGrantPatternJson = "[{\"moduleId\":\"admin\"}]"`.
  - `const string UndefinedColumnSqlState = "42703"` — maps to `SchemaMigrationRequiredException` (N26 not applied).
  - User methods: `ListUsersAsync` / `GetUserAsync` (SELECT with `UserColumns`), `AuthUserIdAlreadyRegisteredAsync`, `CreateInternalUserAsync` (CTE: INSERT `internal_users` `ON CONFLICT (actor_id) DO NOTHING`, then INSERT one `internal_user_access_templates` junction row from the inserted actor — single assignment), `UpdateUserAsync` (guarded UPDATE `WHERE updated_at_utc = @ExpectedUpdatedAt`), `ChangeUserTemplateAsync` (legacy delegate → `ReplaceUserAccessTemplatesAsync` with one id), `ReplaceUserAccessTemplatesAsync` (transaction: UPDATE `internal_users.template_id = ids[0]` guarded, DELETE all junction rows, INSERT FROM `unnest(@TemplateIds::text[])`, then self-lockout count — one effective row), `SetUserActiveAsync` (via `GuardedUserWriteAsync`), `SetUserModulesOverrideAsync` (guarded UPDATE of `modules_override`; DORMANT — no caller), `CountActiveAdminsAsync`.
  - Template methods: `ListTemplatesAsync` / `GetTemplateAsync` (SELECT over `access_templates`), `CreateTemplateAsync` (INSERT), `UpdateTemplateAsync` (guarded UPDATE in `DapperUnitOfWork` + self-lockout count).
  - Audit methods: `InsertAuditEventAsync` (INSERT into `audit_events` with `BeforeSummary`/`AfterSummary` `::jsonb` NULL), `QueryAuditAsync` (COUNT + SELECT over `audit_events` with filter `WHERE` and page LIMIT/OFFSET).
  - `GuardedUserWriteAsync` — applies the write then `CountActiveAdminsOnAsync`; rolls back (returns false) when zero surviving active admins (`LockoutViolationException`).
  - `CountActiveAdminsOnAsync` — CURRENT self-lockout SQL joins `internal_users u` → `internal_user_access_templates ut` → `access_templates t`, counts `DISTINCT u.actor_id` where `u.active`, `u.profile_title = 'Admin'`, `t.active`, `t.modules @> '[{"moduleId":"admin"}]'::jsonb` (profile-driven + junction-based; not the old capability-array match).
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
- `IDbConnectionFactory` (`BA.Dmo.Infrastructure.Persistence`) — injected into `DapperAdminRepository`, `DapperModuleCatalogMirrorRepository`, and the Web-layer `TemplateProfileStore`.
- Dapper (`Db.QueryAsync`/`ExecuteAsync`/`QuerySingleOrDefaultAsync`), `DynamicParameters`, Npgsql (`PostgresException`).
- `DapperUnitOfWork` + `ConcurrencyGuard` (`BA.Dmo.Application.Shared.Persistence`) — used by `DapperAdminRepository`.
- `SchemaMigrationRequiredException` (`BA.Dmo.Application.Shared.Persistence`) — mapped from SQLSTATE 42703 by `DapperAdminRepository`.
- `SupabaseSettings` (`BA.Dmo.Infrastructure\Auth`) — resolves provider URL/keys for adapter registration in `Program.cs`.

## 10. Database Objects

### Admin-specific DB objects

Admin-specific DB objects: **0**. Admin creates no dedicated table, index or trigger.

### Shared / external DB dependencies

Admin reads/writes the following shared **Access / audit** tables (all classed Shared in [02_DATABASE.md](02_DATABASE.md); not Admin-dedicated):

| Table | Source | Admin access | Shared indexes / constraints / trigger (from DATABASE/MIGRATIONS maps) |
|---|---|---|---|
| `internal_users` | `N01_identity.sql` (N25 `auth_user_id` NOT NULL + UNIQUE; N26 `modules_override` dormant; N27 `profile_title` NOT NULL + `ck_internal_users_functional_profile`) | SELECT projection, INSERT, guarded UPDATE (display, profile, template, active), `modules_override` (dormant) | `ix_internal_users_auth_user_id`, `ix_internal_users_active`, `ix_internal_users_template_id`; `uq_internal_users_auth_user` |
| `internal_user_access_templates` | `N27_access_convergence.sql` (junction; N31 collapses to one row per actor) | SELECT (UserColumns subquery, self-lockout join), INSERT + DELETE (replace-assignment, create) | PK (actor_id, template_id); `ix_internal_user_access_templates_template`; `ux_internal_user_access_templates_actor` UNIQUE (actor_id) — single assignment (N31); RLS + `internal_user_access_templates_app_access` policy |
| `access_templates` | `N01_identity.sql` | SELECT, INSERT, guarded UPDATE | `ix_access_templates_active`; N31 AFTER INSERT trigger `trg_access_templates_ensure_profile` |
| `access_template_profiles` | `N31_template_profiles_single_assignment.sql` | SELECT/upsert ONLY via Web-layer `TemplateProfileStore` (NOT via `DapperAdminRepository`) | PK `template_id` FK `access_templates` ON DELETE CASCADE; `ck_access_template_profiles_functional_profile` (Admin / Operador / Controlador / Responsável); RLS + `access_template_profiles_app_access` policy |
| `audit_events` | `N01_identity.sql` | INSERT (audit), SELECT query/export; constraint `ck_audit_events_result` value `succeeded` used by Admin inserts | indexes/trigger shared (see [02_DATABASE.md](02_DATABASE.md)) |
| `module_catalog_mirror` | `N02_catalog.sql` | UPSERT/SELECT (catalog mirror) | `ix_module_catalog_mirror_order` |

Constraints referenced by Admin write paths: `internal_users.template_id → access_templates` FK (N01), `internal_user_access_templates` FKs (N27), `access_template_profiles.template_id → access_templates ON DELETE CASCADE` (N31), `access_templates.active` index, `audit_events.result` CHECK (`succeeded`/`failed`/`denied`/`corrected`). The self-lockout read (`CountActiveAdminsAsync`) joins `internal_users` + `internal_user_access_templates` + `access_templates` filtering `u.profile_title = 'Admin'` AND `t.modules @> AdminGrantPatternJson`.

**LEGACY CANDIDATE — NEEDS AUDIT:** `internal_users.modules_override` (N26) is still projected (`u.modules_override::text AS ModulesOverrideJson` in `DapperAdminRepository.UserColumns`, and in `DapperInternalUserRepository.FindByAuthUserIdSql`) and still writable via `IAdminRepository.SetUserModulesOverrideAsync` / `DapperAdminRepository.SetUserModulesOverrideAsync` (lines 335–366), but N27 sets the column to NULL for all rows and `IdentityResolutionService` never reads it (test `ModulesOverride_IsDormant_AndDoesNotReplaceTemplateModules` in `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Identity\IdentityResolutionServiceTests.cs`). The Admin UI offers no per-user module override anymore.

## 11. Migration Touchpoints

Distinct Admin-specific migration files: **0**. No migration introduces an Admin-dedicated DB object.

Shared/external migration references (navigation only) relevant to Admin:

| Migration | Object(s) | Technical Change | Classification |
|---|---|---|---|
| `N01_identity.sql` | `internal_users`, `access_templates`, `audit_events` | shared identity/access/audit tables used by Admin | shared/external |
| `N02_catalog.sql` | `module_catalog_mirror` | catalog mirror table used by Admin Applications area | shared/external |
| `N25_remediation.sql` | `internal_users.auth_user_id`, `audit_events` index | `uq_internal_users_auth_user`; `ix_audit_events_module_time` | shared/external |
| `N26_user_modules_override.sql` | `internal_users.modules_override` | `ALTER TABLE internal_users ADD COLUMN IF NOT EXISTS modules_override jsonb` | shared table; per-user override now DORMANT (N27) — legacy |
| `N27_access_convergence.sql` | `internal_user_access_templates` (+ `internal_users.profile_title` NOT NULL + CHECK, `modules_override` NULLed, legacy-override-* compatibility templates) | junction table used by Admin create/replace-assignment + self-lockout join | shared access-model |
| `N31_template_profiles_single_assignment.sql` | `access_template_profiles` + function `ba_dmo_ensure_access_template_profile` + trigger `trg_access_templates_ensure_profile` + `ux_internal_user_access_templates_actor` | template-owned functional profile; one effective template per user | shared access-model |

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

### Admin navigation (_AdminNav.cshtml) — in-layout vs page-level

- `src\BA.Dmo.Web\Pages\Shared\_AdminNav.cshtml` renders the persistent four-tab strip (Utilizadores / Templates / Aplicações / Auditoria) with an `IsActive(prefix)` path check and a SINGLE-RENDER MARKER: `const string renderKey = "BA_DMO_ADMIN_NAV_RENDERED"` set in `Context.Items`; only the first renderer emits the `<nav>`.
- The shared layout `src\BA.Dmo.Web\Pages\Shared\_Layout.cshtml` (line 48) renders `<partial name="_AdminNav" />` inside `<main class="app-work-area@(adminScope ? " admin-work-area" : "")">` whenever `adminScope` (`Request.Path.StartsWithSegments("/admin")`, line 27). It also sets `ViewData["AdminScope"]` and the `admin-scope` body class.
- Every Admin page still contains a `<partial name="_AdminNav" />` (verified in `Index.cshtml`, `Users\Index/Create/Edit.cshtml`, `Templates\Index/Edit.cshtml`, `Applications\Index.cshtml`, `Audit\Index.cshtml`) — these are the legacy page-level calls kept harmless by the marker.
- `src\BA.Dmo.Web\Pages\Shared\_Header.cshtml` renders the global module-tab partial `<partial name="_Navigation" />` ONLY when `!adminScope` (lines 33–38), so the shell's right-aligned "Administração" tab is never duplicated inside Admin; in admin scope the header shows the "Portal DMO / Administração" brand block.
- The shell `_Navigation.cshtml` (`Pages\Shared\_Navigation.cshtml`) renders `shell.Navigation.LeftItems` + the right-aligned `AdminEntry` — both derived server-side by `NavigationService` from the resolved grants (see [16_USERS_ACCESS.md](16_USERS_ACCESS.md) §8).

### User editor: single-template behavior (verified in source)

- `Users\Edit.cshtml`: single `<select id="templateId" name="templateId">`; helper text "Apenas um template pode estar associado. Ao mudar o template, o perfil e os módulos mudam em conjunto."; the "Perfil atual" field is read-only — the profile is supplied by the template (`TemplateProfileStore`), not edited per user.
- `Users\Edit.cshtml.cs` `OnPostSaveAsync`: resolves the profile for the selected `templateId` from `TemplateProfileStore.ListAsync` (missing profile → ModelState error "Selecione um template com perfil funcional configurado."), then calls `AdminUserService.SaveUserAsync(id, displayName, profile, [templateId], active, expectedVersion, ct)` — ONE template id.
- `Users\Create.cshtml.cs` `OnPostAsync`: single `templateId`; accepts a legacy `templateIds` POST alias but only its FIRST value is considered ("It can never recreate a hybrid" — commit 7c9944f); then `CreateUserAsync(new CreateAdminUserRequest(email, password, displayName, profile, selectedTemplateId, active, [selectedTemplateId]))`.
- Add sanity: created/edited users always carry exactly one junction row (`CreateInternalUserAsync` CTE, `ReplaceUserAccessTemplatesAsync` delete+insert) enforced at DB level by `ux_internal_user_access_templates_actor` (N31).

### TemplateProfileStore.cs (Admin Web helper)
- File: `src\BA.Dmo.Web\Pages\Admin\TemplateProfileStore.cs` (namespace `BA.Dmo.Web.Pages.Admin`).
- Role: persistence helper for the N31 template-owned functional profile, injected with `IDbConnectionFactory` and constructed by the Admin page models (`Users\Create/Edit.cshtml.cs`, `Templates\Index/Edit.cshtml.cs`).
- Methods: `ListAsync` (SELECT `template_id, functional_profile FROM access_template_profiles ORDER BY template_id`), `GetAsync` (single row), `UpsertAsync` (INSERT ... ON CONFLICT (template_id) DO UPDATE `functional_profile`, then `UPDATE internal_users SET profile_title = @FunctionalProfile WHERE template_id = @TemplateId AND profile_title IS DISTINCT FROM @FunctionalProfile` — keeps the compatibility column in sync, mirroring the N31 backfill rule).
- Fail-safe: catches `DatabaseConnectionException` and falls back to a small in-memory dictionary (`tpl-admin`/`tpl-op`/`tpl-operator`/`tpl-responsible`) ONLY for hosts with no DB connection (isolated web test hosts); a reachable database with a missing/invalid N31 still fails (never masks deployment drift).

DI registrations and policies (`src\BA.Dmo.Web\Program.cs`) — current line numbers:
- Policies: lines 105–113 (`AdminPolicies.AdminGerir` / `AuditView` / `AuditExport`).
- `AddSingleton<IAdminProvisioningAdapter>(_ => new SupabaseAdminProvisioningAdapter(...))` — line 167.
- `AddSingleton<IAdminRepository, DapperAdminRepository>()` — line 172.
- `AddSingleton<IModuleCatalogMirrorRepository, DapperModuleCatalogMirrorRepository>()` — line 173.
- `AddScoped<AdminAuthorizationGate>()`, `AddScoped<AdminUserService>()`, `AddScoped<AdminTemplateService>()`, `AddScoped<AdminMirrorService>()`, `AddScoped<AdminAuditService>()` — lines 184–188; `AddScoped<GrantNormalizer>(...)` — lines 189–190.
- CI invocation: `CliMode.BootstrapAdmin` → `BootstrapAdminCommand.Run()` — lines 46–48.

## 13. Audit Technical Surface

- Route: `/admin/audit` (GET) and POST handler `Export` (Razor page handler, not a separate API endpoint).
- Query service: `AdminAuditService.QueryAsync(AuditQueryFilter)`.
- Export service: `AdminAuditService.ExportAsync(AuditQueryFilter)` — emits CSV text (columns `occurred_at_utc;year;actor_user_id;actor_name;module_id;action_code;entity_type;entity_id;entity_label;result;reason`), `PageSize = 0` for unlimited read.
- Capability checks: `AdminPolicies.AuditView` (page + `QueryAsync`), `AdminPolicies.AuditExport` (export handler + `ExportAsync`).
- DTO/filter types: `AuditQueryFilter`, `AuditEventRow`, `AuditQueryResult`, `AuditEntry` (all in `AdminModels.cs`).
- Repository: `DapperAdminRepository.QueryAuditAsync` / `InsertAuditEventAsync` over the shared `audit_events` table.
- Output type/format: `text/csv; charset=utf-8`; filename `auditoria-{Year ?? "tudo"}.csv` (literal in `Audit\Index.cshtml.cs` line 112).
- Shared dependency: `audit_events` table (N01); export filter year/user/module/action/result/date-interval.

## 14. Static Assets

### Dedicated Admin static asset
| Asset file | Path | Principal selectors / wiring |
|---|---|---|
| `admin-layout.css` | `src\BA.Dmo.Web\wwwroot\styles\modules\admin-layout.css` | `body.admin-scope`, `.admin-work-area`, `.admin-app-list`, `.admin-app-row`, `.admin-app-name`, `.admin-app-state`, `.admin-availability-note`, admin tabs (`._admin-tabs`/`.admin-tabs` compatibility selectors for the persistent tab strip), audit export form inline. |

### Shared static assets carrying Admin selectors
| Asset file | Path | Admin relevance |
|---|---|---|
| `dmo-layout.css` | `src\BA.Dmo.Web\wwwroot\styles\dmo-layout.css` | `.admin-nav`, admin composition/filter-grid, width tokens. |
| `dmo-components.css` | `src\BA.Dmo.Web\wwwroot\styles\dmo-components.css` | row actions / table-card chrome referencing admin patterns. |
| `dmo-tokens.css` | `src\BA.Dmo.Web\wwwroot\styles\dmo-tokens.css` | admin tokens `--dmo-admin-header-height`, `--dmo-admin-nav-height`, `--dmo-admin-main-max-width`. |
| `dmo-interactions.js` | `src\BA.Dmo.Web\wwwroot\scripts\dmo-interactions.js` | shared selection pattern extended for the admin form. |

No dedicated Admin JavaScript file exists; Admin forms are server-rendered Razor.

## 15. Tests

Test files live under `AI-CONTEXT\docs\tests\` (there is no `tests\` directory at the repository root).

| Test class | Kind | Direct target | Main method groups | Location |
|---|---|---|---|---|
| `AdminUserServiceTests` | Unit (xUnit) | `AdminUserService` | capability gate fail-closed, create-user provisioning/validation/duplicate (single template `tpl-active`), partial failure + idempotent retry, update/concurrency, self-lockout, template change, schema-migration guard, password reset, list email enrichment | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Admin\AdminUserServiceTests.cs` |
| `AdminTemplateServiceTests` | Unit (xUnit) | `AdminTemplateService` | create/update canonical JSON persistence, module-only grant rejection (capability input invalid), invalid-grant rejection, duplicate id, self-lockout, concurrency, capability denial | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Admin\AdminTemplateServiceTests.cs` |
| `AdminAuditAndMirrorTests` | Unit (xUnit) | `AdminAuditService`, `AdminMirrorService` | audit.view/audit.export separation, canonical pagination, factual export no-secrets, mirror unknown-module rejection, mirror audit | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Admin\AdminAuditAndMirrorTests.cs` |
| `AdminWebAuthorizationTests` | Integration (WebApplicationFactory) | Admin pages + authorization wiring | unauthenticated redirect, forged POST denial, admin landing (login → /admin), jobon excluded for admin, admin profile derives audit.view from admin module, audit page requires audit.view | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\AdminWebAuthorizationTests.cs` |
| `AdminFormAntiforgeryTests` | Integration (WebApplicationFactory) | Admin forms antiforgery pipeline | token render, tokenless POST 400 + no write, token create, cross-session token reject, anonymous redirect, operator policy denial | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\AdminFormAntiforgeryTests.cs` |
| `AdminUserListResetTests` | Integration (WebApplicationFactory) | Admin user-list reset + edit reset | shared service path, audit + provisioning, unknown user error, edit-page path | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\AdminUserListResetTests.cs` |
| `AdminSecurityGuardTests` | Integration (guard) | Admin services/pages security contracts | privileged provisioning not page-reachable, pages authorize via capability policy only, Admin services depend on ports only | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\AdminSecurityGuardTests.cs` |
| `DapperAdminRepositoryProjectionTests` | Integration (projection) | `DapperAdminRepository.UserColumns` | `ListUsersAsync` materializes `AdminUserRow` with `AuthEmail == null` pre-enrichment via real Dapper + in-memory reader | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\DapperAdminRepositoryProjectionTests.cs` |
| `SupabaseAdminProvisioningAdapterTests` | Integration (adapter) | `SupabaseAdminProvisioningAdapter` | paginated email lookup, create/idempotent-conflict, password reset, service-role isolation | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Identity\SupabaseAdminProvisioningAdapterTests.cs` |
| `BootstrapAdminCliTests` | Integration (CLI) | `BootstrapAdminCommand` | no-config fail, partial-config listing, missing DB config | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Cli\BootstrapAdminCliTests.cs` |

Note: shared-catalog / shared-identity test files that Admin depends on but whose direct target is not Admin are handled by the catalog/identity maps: `ModuleCatalogMirrorSynchronizerTests` (Unit, Shared\Access) targets the mirror synchronizer consumed by `AdminMirrorService`; `BootstrapAdminServiceTests` (Unit, Shared\Identity) targets the shared bootstrap service. They are out of Admin's test scope.

## 16. Test Doubles / Helpers

| File | Double / helper | Implements / role |
|---|---|---|
| `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Admin\FakeAdminRepository.cs` | `FakeAdminRepository` | Dedicated in-memory fake of `IAdminRepository` (Users/Templates/Audits/Writes, self-lockout/concurrency/schema-migration simulation), shared by the Unit `Shared\Admin` tests. |
| `AdminUserServiceTests.cs` | `FakeProvisioning`, `FakeCurrentUserAccessor`, `FixedClock` | In-file fakes of `IAdminProvisioningAdapter`, `ICurrentUserAccessor`, `IClock`. |
| `AdminTemplateServiceTests.cs` | `FakeCurrentUserAccessor`, `FixedClock` | In-file fakes. |
| `AdminAuditAndMirrorTests.cs` | `FakeMirrorRepository`, `FakeCurrentUserAccessor`, `FixedClock` | In-file fakes of `IModuleCatalogMirrorRepository`, `ICurrentUserAccessor`, `IClock`. |
| `AdminWebAuthorizationTests.cs` | `AdminFixture` (`WebApplicationFactory<Program>`), `FakeAdminWritesRepository`, `FakeAuthAdapter`, `FakeIdentityRepository`, `FakeMirrorRepository` | In-file `WebApplicationFactory` fixture replacing auth/identity/Admin/mirror collaborators. |
| `AdminFormAntiforgeryTests.cs` | `AfFixture` (`WebApplicationFactory<Program>`), `FakeAdminRepository`, `FakeMirrorRepository`, `FakeProvisioningAdapter`, `FakeAuthAdapter`, `FakeIdentityRepository` | In-file `WebApplicationFactory` fixture with antiforgery enforced. |
| `AdminUserListResetTests.cs` | `ResetFixture` (`WebApplicationFactory<Program>`), `RecordingProvisioningAdapter`, `RecordingAdminRepository`, `FakeAuthAdapter`, `FakeIdentityRepository`, `NoopMirror` | In-file `WebApplicationFactory` fixture recording resets/audits. |
| `DapperAdminRepositoryProjectionTests.cs` | `DataReaderDbConnection`, `DataReaderDbCommand`, `NoParameterCollection`, `FixedReaderConnectionFactory` | In-file ADO.NET/connection doubles exercising the real Dapper projection. |

In-file test fixture files count: **7** (`AdminUserServiceTests.cs`, `AdminTemplateServiceTests.cs`, `AdminAuditAndMirrorTests.cs`, `AdminWebAuthorizationTests.cs`, `AdminFormAntiforgeryTests.cs`, `AdminUserListResetTests.cs`, `DapperAdminRepositoryProjectionTests.cs`).

## 17. Direct Admin References

- `AdminUserService` → `AdminAuthorizationGate` (constructor)
- `AdminUserService` → `IAdminRepository` (constructor)
- `AdminUserService` → `IAdminProvisioningAdapter` (constructor)
- `AdminUserService` → `CanonicalCapabilities` (`AdminGerir`, `AdminModuleId`, `AuditView`) — same file declaration
- `AdminUserService` → `GrantNormalizer`-independent validation: `AccessTemplateGrantsParser` (template modules parse), `CanonicalModuleCatalog` (module validation), `FunctionalProfileNames` (profile parse) — `ValidateProfileTemplatesAsync`
- `AdminUserService` → `AdminModels` types (`AdminUserRow`, `Create/Update/Change/Set` requests, `AuditEntry`)
- `AdminTemplateService` → `AdminAuthorizationGate`, `IAdminRepository`, `GrantNormalizer`, `IClock` (constructors)
- `AdminTemplateService` → `AdminModels` types (`AdminTemplateRow`, `Create/UpdateTemplateRequest`, `TemplateGrantInput`, `AuditEntry`)
- `AdminAuditService` → `AdminAuthorizationGate`, `IAdminRepository` (constructors)
- `AdminAuditService` → `AdminModels` types (`AuditQueryFilter`, `AuditQueryResult`, `AuditEventRow`)
- `AdminMirrorService` → `AdminAuthorizationGate`, `ModuleCatalog`, `IModuleCatalogMirrorRepository`, `IAdminRepository`, `IClock` (constructors)
- `AdminMirrorService` → `ModuleCatalogMirrorSynchronizer` (constructed internally), `MirrorDisplayEntry`, `AuditEntry`
- `IAdminRepository` → `AdminModels` types (method signatures)
- `DapperAdminRepository` → `IAdminRepository` (implements)
- `DapperAdminRepository` → `internal_users`, `access_templates`, `internal_user_access_templates`, `audit_events` (reads/writes, shared tables)
- `TemplateProfileStore` → `access_template_profiles` (SELECT/upsert) + `internal_users.profile_title` sync (via `IDbConnectionFactory`, Web layer)
- `SupabaseAdminProvisioningAdapter` → `IAdminProvisioningAdapter` (implements)
- `CanonicalCapabilities` → `CanonicalModuleCatalog` ids (admin.gerir / audit.view / audit.export)
- Admin page models → `AdminUserService` / `AdminTemplateService` / `AdminAuditService` / `AdminMirrorService` / `TemplateProfileStore` (constructors)
- Admin pages → `AdminPolicies.*` (razor `[Authorize]` attributes)
- `Program.cs` → `AdminPolicies.*`, `CanonicalCapabilities.*`, Admin services, `DapperAdminRepository`, `SupabaseAdminProvisioningAdapter` (policies + DI)

## 18. External Technical References

| Admin Object | External Technical Reference | Reference Type |
|---|---|---|
| `AdminUserService`, `AdminTemplateService`, `AdminMirrorService`, `AdminAuditService`, `AdminAuthorizationGate` | `Result<T, DomainError>`, `DomainError.Forbidden/Validation/NotFound/DomainConflict/ConcurrencyConflict/BackendUnavailable` (`Domain.Shared.Kernel`) | shared domain kernel (method/constructor dependency) |
| `AdminAuthorizationGate` | `ICurrentUserAccessor`, `CurrentUser.HasCapability` (`Domain\Shared\Access\CurrentUser.cs`) | shared identity dependency |
| `AdminUserService`, `AdminTemplateService`, `AdminMirrorService` | `GrantNormalizer`, `ModuleCatalog`, `CanonicalModuleCatalog`, `CanonicalPageCatalog` (`Application\Shared\Access`) | shared access catalog / static consumer |
| `AdminUserService` profile/template validation | `AccessTemplateGrantsParser`, `FunctionalProfileNames` (`Application\Shared\Identity` + `Domain\Shared\Access\FunctionalProfile.cs`) | shared template/identity consumer |
| `AdminMirrorService` | `IModuleCatalogMirrorRepository`, `ModuleCatalogMirrorSynchronizer`, `ModuleCatalogMirrorRow`, `MirrorDisplayEntry` (`Application\Shared\Access`) | shared catalog / port dependency |
| `AdminUserService` | `IAdminProvisioningAdapter` (`Application\Shared\Identity\SupabaseAuthPorts.cs`) | shared privileged-provisioning port (Admin consumer) |
| `BootstrapAdminService` / `BootstrapAdminCommand` | `IAdminProvisioningAdapter`, `IInternalUserRepository` | shared identity bootstrap dependency (uses Admin provisioning port) |
| `TemplateProfileStore` | `IDbConnectionFactory` (`Infrastructure\Persistence`), `Db` (Dapper), `DatabaseConnectionException` | shared persistence dependency (Web-layer helper) |
| `CanonicalCapabilities.AuditView` | `HistoriaAuthorizationGate` (História MAP-14 reads `audit.view`) | shared capability consumed by another module |
| `AdminPolicies.*` | `CapabilityAuthorizationHandler` / `CapabilityRequirement` | shared web authorization wiring |
| Admin pages | `PageModel`, `HttpContext`, antiforgery (Microsoft.AspNetCore.Mvc.RazorPages) | framework base types |
| `DapperAdminRepository` | `IDbConnectionFactory`, `DapperUnitOfWork`, `ConcurrencyGuard`, `SchemaMigrationRequiredException` (`Application\Shared\Persistence` + `Infrastructure\Persistence`) | application port / query dependency |
| `DapperAdminRepository` | Dapper `Db.QueryAsync`/`ExecuteAsync` / `DynamicParameters`, Npgsql `PostgresException` | query/read dependency (external lib) |
| `DapperAdminRepository` | `internal_users`, `access_templates`, `internal_user_access_templates`, `audit_events` tables | shared DB dependency (read/write) |
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
| Admin Index + 4-area pages (8 PageModels + 8 .cshtml) + `TemplateProfileStore.cs` | Web | `src\BA.Dmo.Web\Pages\Admin\` |
| `_AdminNav.cshtml` (persistent tabs, single-render marker) | Web (shared partial) | `src\BA.Dmo.Web\Pages\Shared\_AdminNav.cshtml` |
| `AdminPolicies.*` | Web (Authorization) | `src\BA.Dmo.Web\Authorization\CapabilityAuthorizationHandler.cs` |
| Admin policies + DI registration | Web | `src\BA.Dmo.Web\Program.cs` |
| `admin-layout.css` | Web (static) | `src\BA.Dmo.Web\wwwroot\styles\modules\admin-layout.css` |
| `internal_users`, `access_templates`, `audit_events` (shared, read/write) | Database | `database\migrations\N01_identity.sql` (+N25/N26/N27) |
| `internal_user_access_templates` (shared, read/write) | Database | `database\migrations\N27_access_convergence.sql` (+N31 single assignment) |
| `access_template_profiles` (shared, read/write via Web helper) | Database | `database\migrations\N31_template_profiles_single_assignment.sql` |
| `module_catalog_mirror` (shared, read/write) | Database | `database\migrations\N02_catalog.sql` |
| Admin service tests / `FakeAdminRepository` | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Admin\` |
| Admin integration tests / fixtures | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\`, `Cli\`, `Identity\` |

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
- `src\BA.Dmo.Application\Shared\Access\AccessResolver.cs` (profile-derived capabilities)
- `src\BA.Dmo.Application\Shared\Access\ModuleCatalogMirrorSynchronizer.cs`
- `src\BA.Dmo.Application\Shared\Access\IModuleCatalogMirrorRepository.cs`
- `src\BA.Dmo.Application\Shared\Access\GrantNormalizer.cs`
- `src\BA.Dmo.Application\Shared\Identity\SupabaseAuthPorts.cs`
- `src\BA.Dmo.Application\Shared\Identity\AccessTemplateGrantsParser.cs`
- `src\BA.Dmo.Application\Shared\Identity\BootstrapAdminService.cs`
- `src\BA.Dmo.Domain\Shared\Access\FunctionalProfile.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperAdminRepository.cs`
- `src\BA.Dmo.Infrastructure\Access\DapperModuleCatalogMirrorRepository.cs`
- `src\BA.Dmo.Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs`
- `src\BA.Dmo.Web\Program.cs`
- `src\BA.Dmo.Web\Authorization\CapabilityAuthorizationHandler.cs`
- `src\BA.Dmo.Web\Pages\Shared\_Layout.cshtml`, `_Header.cshtml`, `_Navigation.cshtml`, `_AdminNav.cshtml`
- `src\BA.Dmo.Web\Pages\Admin\` (Index, Applications/Index, Audit/Index, Templates/Index+Edit, Users/Index+Create+Edit — .cshtml + .cshtml.cs) + `TemplateProfileStore.cs`
- `src\BA.Dmo.Web\wwwroot\styles\modules\admin-layout.css`
- `database\migrations\N01_identity.sql`, `N02_catalog.sql`, `N25_remediation.sql`, `N26_user_modules_override.sql`, `N27_access_convergence.sql`, `N31_template_profiles_single_assignment.sql`
- [00_INDEX.md](00_INDEX.md), [01_DOMAIN.md](01_DOMAIN.md), [02_DATABASE.md](02_DATABASE.md), [03_MIGRATIONS.md](03_MIGRATIONS.md), [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md), [05_TESTS.md](05_TESTS.md), [19_APPLICATION.md](19_APPLICATION.md), [20_WEB.md](20_WEB.md) (cross-map navigation)
- `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Admin\AdminUserServiceTests.cs`, `AdminTemplateServiceTests.cs`, `AdminAuditAndMirrorTests.cs`, `FakeAdminRepository.cs`
- `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Identity\IdentityResolutionServiceTests.cs` (single-template enforcement), `AccessResolverTests.cs`, `NavigationServiceTests.cs`
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\AdminWebAuthorizationTests.cs`, `AdminFormAntiforgeryTests.cs`, `AdminUserListResetTests.cs`, `AdminSecurityGuardTests.cs`, `DapperAdminRepositoryProjectionTests.cs`
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Cli\BootstrapAdminCliTests.cs`
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Identity\SupabaseAdminProvisioningAdapterTests.cs`

## Counts

- Domain Admin files: **0**
- Application Admin files: **7**
- Infrastructure Admin files: **2**
- Shared infrastructure dependencies: **7** (`IDbConnectionFactory`, Dapper, Npgsql, `DapperUnitOfWork`/`ConcurrencyGuard`, `SchemaMigrationRequiredException`, `SupabaseSettings`, `DapperModuleCatalogMirrorRepository` — shared infra consumed by Admin)
- Dedicated Web page files: **17** (8 pages × `.cshtml` + `.cshtml.cs` = 16, plus `TemplateProfileStore.cs`)
- Dedicated static asset files: **1** (`admin-layout.css`)
- Shared Web wiring files: **2** (`src\BA.Dmo.Web\Program.cs`, `src\BA.Dmo.Web\Authorization\CapabilityAuthorizationHandler.cs`) + **2 navigation partials** (`_Layout.cshtml`, `_AdminNav.cshtml`)
- Shared static asset files: **4** (`dmo-interactions.js`, `dmo-components.css`, `dmo-layout.css`, `dmo-tokens.css`) with Admin selectors
- Shared Application catalog files: **8** (`CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`, `GrantNormalizer.cs`, `ModuleCatalogMirrorSynchronizer.cs`, `IModuleCatalogMirrorRepository.cs`, `AccessTemplateGrantsParser.cs`, `SupabaseAuthPorts.cs`, `BootstrapAdminService.cs`)
- Admin-specific DB tables: **0**
- Admin-specific DB indexes: **0**
- Admin-specific DB triggers: **0**
- Admin-specific DB objects: **0** (0 tables + 0 indexes + 0 triggers)
- Shared / external DB dependencies: **6** (`internal_users`, `access_templates`, `internal_user_access_templates`, `access_template_profiles`, `audit_events`, `module_catalog_mirror`, each with shared indexes/constraints/trigger)
- Distinct Admin-specific migration files: **0** (N27/N31 are shared access-model migrations consumed by Admin)
- Capabilities: **3**
- Capability IDs: `admin.gerir`, `audit.view`, `audit.export`
- Authorization / Catalog: **MAPPED** (profile-derived capabilities; module-only template grants)
- Admin technical areas: **Utilizadores · Templates · Aplicações · Auditoria** (one persistent tab strip)
- Web / Routes: **MAPPED** (Razor page handlers; no `/api/admin` endpoints)
- Audit surface: **MAPPED** (page + query service + CSV export)
- Static assets: **MAPPED**
- Test classes: **10**
- Dedicated test support files: **1** (`FakeAdminRepository.cs`)
- In-file test fixture files: **7**
- Source-visible user surfaces: **1** (Admin)