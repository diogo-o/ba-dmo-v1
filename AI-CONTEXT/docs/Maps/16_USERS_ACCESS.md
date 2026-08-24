# BA DMO — Users / Access Technical Map

MAP ID: MAP-16
Status: COMPLETE

## Navigation Index

1. [Scope](#1-scope)
2. [Layer Summary](#2-layer-summary)
3. [Domain Shared Access Objects](#3-domain-shared-access-objects)
4. [Application Shared Access Objects](#4-application-shared-access-objects)
5. [Application Shared Identity Objects](#5-application-shared-identity-objects)
6. [Access Template / Grant Model](#6-access-template--grant-model)
7. [Canonical Module / Capability / Page Catalogs](#7-canonical-module--capability--page-catalogs)
8. [Access Resolution Objects](#8-access-resolution-objects)
9. [Authorization Primitives](#9-authorization-primitives)
10. [Current User / Request Context](#10-current-user--request-context)
11. [Infrastructure Objects](#11-infrastructure-objects)
12. [Database Objects](#12-database-objects)
13. [Migration Touchpoints](#13-migration-touchpoints)
14. [User Surface](#14-user-surface)
15. [Web / Routes](#15-web--routes)
16. [Static Assets](#16-static-assets)
17. [Tests](#17-tests)
18. [Test Doubles / Helpers](#18-test-doubles--helpers)
19. [Direct Users / Access References](#19-direct-users--access-references)
20. [External Technical References](#20-external-technical-references)
21. [Target-to-Layer Index](#21-target-to-layer-index)
22. [Sources Verified](#22-sources-verified)

## 1. Scope

This map is a pure technical inventory/navigation for the shared Users / Access architecture: the reusable identity/access machinery shared across modules, the shell and the Administration. It does NOT absorb the Admin UI/pages/services/gates (mapped in `15_ADMIN.md`, MAP-15) nor the Login surface/request handling (mapped in `18_LOGIN.md`, MAP-18). Only current source is mapped; nothing is inferred or invented.

Shared users/access scope covers:

- current-user models/accessors;
- internal-user repositories;
- access template / grant model;
- module/page/capability catalogs;
- access resolution;
- authorization primitives;
- identity/auth ports;
- auth-provider adapters;
- module overrides;
- bootstrap identity components;
- persistence/authorship access;
- related DB objects, migrations and tests.

## 2. Layer Summary

| Layer | Contents |
|---|---|
| Domain | `CurrentUser`, `Capability`, `ModuleCatalog`, `ModuleDefinition`, `ModuleKind`, `ICurrentUserAccessor`, `JobonModuleCatalog` |
| Application — Access | Catalog, grant normalizer, access resolver, page catalog, navigation, mirror synchronizer |
| Application — Identity | Internal-user port, auth ports, bootstrap service, identity resolution, template grants parser |
| Infrastructure | `DapperInternalUserRepository`, `SupabaseAuthAdapter`, `SupabaseSettings`, `DapperModuleCatalogMirrorRepository` |
| Web | Session claims, request current-user accessor, authorship accessor, authorization handlers, composition-root policy wiring |
| Database | `internal_users`, `access_templates`, `module_catalog_mirror` |

## 3. Domain Shared Access Objects

Location: `src\BA.Dmo.Domain\Shared\Access\`

| Type | Kind | Members | Key methods | Path |
|---|---|---|---|---|
| `CurrentUser` | sealed record | `InternalUserId` (Guid), `DisplayName`, `Modules` (IReadOnlySet<string>), `Capabilities` (IReadOnlySet<string>) | `HasModule(moduleId)`, `HasCapability(capabilityId)`, `Normalize(values)` | `CurrentUser.cs` |
| `Capability` | sealed record | `Id`, `ModuleSegment` | ctor validates `{moduleId}.{ação}` grammar | `Capability.cs` |
| `ModuleCatalog` | sealed class | `Modules` (ordered), `Count`, `Empty` | `ContainsModule`, `TryGetModule`, `IsCapabilityKnown` | `ModuleCatalog.cs` |
| `ModuleDefinition` | sealed record | `ModuleId`, `DisplayName`, `Kind`, `CanonicalOrder`, `InitialRoute`, `Capabilities` | ctor validates id/route/order | `ModuleDefinition.cs` |
| `ModuleKind` | enum | `Module`, `FunctionalArea` | — | `ModuleKind.cs` |
| `ICurrentUserAccessor` | interface | `Current` (CurrentUser?) | — | `ICurrentUserAccessor.cs` |
| `JobonModuleCatalog` | static class | const `JobonModuleId="jobon"`, capability IDs `jobon.view/edit/configure/confirmar`, field families `Family*` | — | `JobonModuleCatalog.cs` |

Direct Domain references: `CurrentUser` is referenced by `ICurrentUserAccessor`; `ModuleCatalog`/`ModuleDefinition`/`Capability`/`ModuleKind` are referenced by the Application `CanonicalModuleCatalog`; `JobonModuleCatalog` declares the Job On canonical capability constants consumed by `CanonicalModuleCatalog`.

Share/no-absorb note: `Commands/Capability` guarded per-module capabilities live on each `ModuleDefinition`; only the shared catalog machinery is mapped here.

## 4. Application Shared Access Objects

Location: `src\BA.Dmo.Application\Shared\Access\`

| Type | File | Constants/IDs | Principal members | Relevant methods | Direct dependencies |
|---|---|---|---|---|---|
| `CanonicalModuleCatalog` | `CanonicalModuleCatalog.cs` | 12 canonical module ids; capability ids incl. `jobon.view`, `peso.aprovar`, `admin.gerir`, `audit.view/export`, `controlo.*`; `AreaChildren` (controlo → peso, pegamentos) | `Instance`, `AreaChildren`, `Descriptions` | `Build()` | `ModuleCatalog`, `ModuleDefinition`, `Capability`, `ModuleKind`, `JobonModuleCatalog` |
| `CanonicalPageCatalog` | `CanonicalPageCatalog.cs` | 12 canonical page ids | `Instance` | `Build()` | `PageCatalog`, `PageDefinition`, `CanonicalModuleCatalog` |
| `PageCatalog` | `PageCatalog.cs` | — | `Pages`, `Count`, `LandingPage` | `TryGetById`, `TryGetByRoute` | `PageDefinition` |
| `PageDefinition` | `PageCatalog.cs` | route grammar `Regex` | `PageId`, `ModuleId`, `Route`, `RequiredCapabilityId`, `DisplayOrder`, `IsActive`, `IsLanding` | ctor, static `IsValidRoute` | `CanonicalModuleCatalog` |
| `AccessResolver` | `AccessResolver.cs` | `FirstPageOutcome` enum, `FirstPageResolution`, `EffectiveAccess` | ctor (catalog, pages, areaChildren) | `Resolve`, `IsPageAccessible`, `AccessiblePages`, `ResolveFirstPage`, `ResolveAreaFirstPage` | `ModuleCatalog`, `PageCatalog`, `GrantNormalizer`, `CanonicalModuleCatalog`, `CanonicalPageCatalog` |
| `GrantNormalizer` | `GrantNormalizer.cs` | `NormalizationResult` record | ctor (catalog) | `Normalize(grants)` | `ModuleCatalog`, `ModuleGrant` |
| `CatalogValidator` | `CatalogValidator.cs` | `CatalogValidationException` | — | `Validate` | `ModuleCatalog`, `PageCatalog`, `PageDefinition` |
| `NavigationService` | `NavigationService.cs` | `NavigationItem/Tab/Area`, `ShellNavigation`, `INavigationService` | ctor (pages, resolver, catalog) | `Build` | `EffectiveAccess`, `PageCatalog`, `AccessResolver`, `ModuleCatalog`, `CanonicalPageCatalog` |
| `IModuleCatalogMirrorRepository` | `IModuleCatalogMirrorRepository.cs` | `ModuleCatalogMirrorRow` record | — | `GetAllAsync`, `UpsertAllAsync` | persistence port (no DB types) |
| `ModuleCatalogMirrorSynchronizer` | `ModuleCatalogMirrorSynchronizer.cs` | `MirrorDisplayEntry`, `MirrorValidationReport` | — | `BuildSyncRows`, `ValidateMirrorRows`, `MergeForDisplay` | `ModuleCatalog` |

The Access template model (`AccessTemplateDefinition`, `ModuleGrant`) is mapped in section 6.

## 5. Application Shared Identity Objects

Location: `src\BA.Dmo.Application\Shared\Identity\`

| Type | File | Members / methods | Direct dependencies |
|---|---|---|---|
| `InternalUserRecord` | `IInternalUserRepository.cs` | `ActorId`, `AuthUserId`, `DisplayName`, `ProfileTitle`, `UserActive`, `TemplateId`, `TemplateName`, `TemplateActive`, `ModulesJson`, `ModulesOverrideJson` | — |
| `IInternalUserRepository` | `IInternalUserRepository.cs` | `FindByAuthUserIdAsync`, `AdminExistsAsync`, `CreateBootstrapAdminAsync` | `BootstrapAdminCreation` |
| `BootstrapAdminCreation` | `IInternalUserRepository.cs` | `ActorId`, `AuthUserId`, `DisplayName`, `TemplateId`, `TemplateName`, `ModulesJson`, `CreatedAtUtc` | — |
| `AuthUser` | `SupabaseAuthPorts.cs` | `AuthUserId`, `Email` | — |
| `EnsuredAuthUser` | `SupabaseAuthPorts.cs` | `AuthUserId`, `Email`, `AccountPreExisted` | — |
| `ISupabaseAuthAdapter` | `SupabaseAuthPorts.cs` | `SignInWithPasswordAsync` → `Result<AuthUser, DomainError>` | `AuthUser`, `DomainError` |
| `IAdminProvisioningAdapter` | `SupabaseAuthPorts.cs` | `EnsureAuthUserAsync`, `EnsureAuthUserWithStatusAsync`, `RequestPasswordResetAsync`, `GetUserEmailsAsync` | `AuthUser`, `EnsuredAuthUser`, `DomainError` |
| `AccessTemplateGrantsParser` | `AccessTemplateGrantsParser.cs` | static `Parse(modulesJson)` → `Result<IReadOnlyList<ModuleGrant>, DomainError>`; `ModulesEntry` private | `ModuleGrant`, `DomainError`, System.Text.Json |
| `AmbiguousIdentityException` | `AmbiguousIdentityException.cs` | `AuthUserId` | data-integrity typed exception |
| `IdentityResolutionService` | `IdentityResolutionService.cs` | `ResolvedIdentity` record; `ResolveAsync(authUserId)` (request-memoized) | `IInternalUserRepository`, `AccessResolver`, `CurrentUser`, `AccessTemplateGrantsParser`, `AccessTemplateDefinition` |
| `BootstrapAdminService` | `BootstrapAdminService.cs` | const `BootstrapTemplateId="tpl-bootstrap-admin"`, `BootstrapTemplateName`, `BootstrapModulesJson`; `BootstrapAdminOptions`, `BootstrapAdminOutcome` enum; `RunAsync` | `IAdminProvisioningAdapter`, `IInternalUserRepository`, `IClock`, `DomainError` |

Identity error codes visible in source: `INTERNAL_USER_INACTIVE`, `ACCESS_TEMPLATE_INACTIVE`, `IDENTITY_AMBIGUOUS`, `IDENTITY_RESOLUTION_UNAVAILABLE`, `ACCESS_TEMPLATE_MODULES_INVALID`.

Shared Identity is kept separate from Admin-specific services (`AdminUserService`, `AdminTemplateService`, `AdminMirrorService`, `AdminAuditService`), which are mapped in `15_ADMIN.md`.

## 6. Access Template / Grant Model

Location: `src\BA.Dmo.Application\Shared\Access\AccessTemplateDefinition.cs`, `src\BA.Dmo.Application\Shared\Identity\AccessTemplateGrantsParser.cs`, `src\BA.Dmo.Application\Shared\Access\GrantNormalizer.cs`

**Template storage model** (`access_templates.modules` jsonb, N01): a JSON array `[{ moduleId, capabilities: [] }]`.

| Object | Literal behavior |
|---|---|
| `ModuleGrant` | record `ModuleId` + `Capabilities` (IReadOnlyList<string>). Presence of module grants entry; capabilities grant operations. |
| `AccessTemplateDefinition` | record `TemplateId`, `Name`, `Active`, `Grants`, `PreferredFirstPageId` (read-only field, never consulted by resolution). |
| `AccessTemplateGrantsParser.Parse` | deserializes `modules` jsonb → `List<ModuleGrant>`; structural JSON defects fail as `ACCESS_TEMPLATE_MODULES_INVALID`; blank module ids are skipped. `ModulesEntry` DTO has `ModuleId` and `Capabilities`. |
| `GrantNormalizer.Normalize` | discards unknown/functional-area modules, duplicate module entries, capabilities that do not belong to the granted module per the catalog, blank capabilities; reports discarded entries. Returns `NormalizationResult(Grants, DiscardedEntries)`. |
| Module override | `internal_users.modules_override` jsonb (N26). When non-null, `IdentityResolutionService` substitutes it for the template `ModulesJson`; a non-null invalid value denies resolution (same `ACCESS_TEMPLATE_INACTIVE` path). |

No per-user override structure beyond `modules_override` exists in current source; the template path is the fallback when the override is null.

## 7. Canonical Module / Capability / Page Catalogs

### Canonical Module Catalog (12 entries)

| Module ID | Display | Kind | CanonicalOrder | Route | Capabilities |
|---|---|---|---|---|---|
| `jobon` | Job On | Module | 5 | /jobon | view, edit, configure, confirmar |
| `boquilhas` | Boquilhas | Module | 10 | /boquilhas | — |
| `controlo` | Controlo | FunctionalArea | 20 | /controlo | view, edit, submit, review |
| `peso` | Peso | Module | 21 | /peso | aprovar |
| `pegamentos` | Pegamentos | Module | 22 | /pegamentos | — |
| `ferramentas` | Ferramentas | Module | 40 | /ferramentas | configure |
| `armazem` | Armazém | Module | 50 | /armazem | — |
| `reparacao_interna` | Reparação Interna | Module | 60 | /reparacao-interna | corrigir |
| `reparacao_externa` | Reparação Externa | Module | 70 | /reparacao-externa | — |
| `tampoes` | Tampões | Module | 80 | /tampoes | — |
| `historia` | História | Module | 90 | /historia | — |
| `admin` | Administração | Module | 99 | /admin | gerir, audit.view, audit.export |

Counts: **12 module entries**; area children: `controlo → [peso, pegamentos]`.

### Canonical Capability IDs (14)

`jobon.view`, `jobon.edit`, `jobon.configure`, `jobon.confirmar`, `controlo.view`, `controlo.edit`, `controlo.submit`, `controlo.review`, `peso.aprovar`, `ferramentas.configure`, `reparacao_interna.corrigir`, `admin.gerir`, `audit.view`, `audit.export`.

### Canonical Page Catalog (12 pages)

| Page ID | Module | Route | RequiredCapability | DisplayOrder | Active / Landing |
|---|---|---|---|---|---|
| `jobon.folha` | jobon | /jobon | jobon.view | 5 | isLanding=true |
| `boquilhas.registo` | boquilhas | /boquilhas | null | 10 | active |
| `peso.operador` | peso | /peso | null | 21 | active |
| `peso.responsavel` | peso | /peso/responsavel | peso.aprovar | 21 | active |
| `pegamentos.folha` | pegamentos | /pegamentos | null | 22 | active |
| `ferramentas.lista` | ferramentas | /ferramentas | null | 40 | active |
| `armazem.mapa` | armazem | /armazem | null | 50 | active |
| `reparacao_interna.registo` | reparacao_interna | /reparacao-interna | null | 60 | active |
| `reparacao_externa.listas` | reparacao_externa | /reparacao-externa | null | 70 | active |
| `tampoes.quantidades` | tampoes | /tampoes | null | 80 | active |
| `historia.consulta` | historia | /historia | null | 90 | active |
| `admin.gestao` | admin | /admin | admin.gerir | 99 | active |

All catalog entries are built with `isActive: true` (source default). `CanonicalModuleCatalog.Instance` and `CanonicalPageCatalog.Instance` are composition-time singletons validated by `CatalogValidator.Validate` in `Program.cs`.

## 8. Access Resolution Objects

Location: `src\BA.Dmo.Application\Shared\Access\AccessResolver.cs`, `src\BA.Dmo.Application\Shared\Identity\IdentityResolutionService.cs`

| Object | Inputs | Outputs | Principal methods | Path |
|---|---|---|---|---|
| `GrantNormalizer` | `IEnumerable<ModuleGrant>` | `NormalizationResult` | `Normalize` | `GrantNormalizer.cs` |
| `AccessResolver` | `AccessTemplateDefinition` | `EffectiveAccess` | `Resolve` | `AccessResolver.cs` |
| `EffectiveAccess` | — | `NavigationModules`, `VisibleAreaChildren`, `HasModule`, `HasCapability`, `AuthorizedModuleIds`, `GrantedCapabilityIds`, `IsEmpty` | — | `AccessResolver.cs` |
| `AccessResolver.IsPageAccessible` | `EffectiveAccess`, `PageDefinition` | bool | — | `AccessResolver.cs` |
| `AccessResolver.AccessiblePages` | `EffectiveAccess` | `IReadOnlyList<PageDefinition>` | — | `AccessResolver.cs` |
| `AccessResolver.ResolveFirstPage` | `EffectiveAccess` | `FirstPageResolution(Outcome, Page?)` | — | `AccessResolver.cs` |
| `AccessResolver.ResolveAreaFirstPage` | `EffectiveAccess`, areaId | `PageDefinition?` | — | `AccessResolver.cs` |
| `IdentityResolutionService` | `Guid authUserId` | `Result<ResolvedIdentity, DomainError>` | `ResolveAsync` | `IdentityResolutionService.cs` |
| `ResolvedIdentity` | — | `CurrentUser`, `ActorId`, `ProfileTitle`, `EffectiveAccess Access`, `FirstPageResolution FirstPage` | — | `IdentityResolutionService.cs` |

`FirstPageOutcome` enum: `Landing`, `FallbackCanonicalOrder`, `NoAccess`. Inactive templates resolve to an empty `EffectiveAccess` (no grants). Job On `jobon.view` is added for active non-admin templates by the resolver; templates holding `admin` do not receive it.

## 9. Authorization Primitives

Location: `src\BA.Dmo.Web\Authorization\`

| Object | Kind | Members | Path |
|---|---|---|---|
| `AuthenticatedSessionRequirement` | requirement | — | `AuthenticatedSessionHandler.cs` |
| `AuthenticatedSessionHandler` | `AuthorizationHandler<AuthenticatedSessionRequirement>` | checks authenticated identity + `SessionClaims.AuthUserIdClaimType` claim | `AuthenticatedSessionHandler.cs` |
| `ModuleRequirement` | requirement | `ModuleId` | `ModuleAuthorizationHandler.cs` |
| `ModuleAuthorizationHandler` | `AuthorizationHandler<ModuleRequirement>` | checks `ICurrentUserAccessor.Current.HasModule` | `ModuleAuthorizationHandler.cs` |
| `CapabilityRequirement` | requirement | `AnyOfCapabilityIds` | `CapabilityAuthorizationHandler.cs` |
| `CapabilityAuthorizationHandler` | `AuthorizationHandler<CapabilityRequirement>` | checks `ICurrentUserAccessor.Current.HasCapability` | `CapabilityAuthorizationHandler.cs` |
| `ModulePolicies` | static class | const names `BaDmo.Module.{moduleId}` for each canonical module | `ModuleAuthorizationHandler.cs` |
| `CapabilityPolicies` | static class | const names `BaDmo.Capability.{capabilityId}` | `ModuleAuthorizationHandler.cs` |
| `AdminPolicies` | static class | `BaDmo.Admin.Gerir`, `BaDmo.Audit.View`, `BaDmo.Audit.Export` (named Admin policies) | `CapabilityAuthorizationHandler.cs` |

Policy registration in `src\BA.Dmo.Web\Program.cs`: fallback policy → `AuthenticatedSessionRequirement`; one policy per catalog module (via `ModulePolicies` + `ModuleRequirement`), one per catalog capability (via `CapabilityPolicies` + `CapabilityRequirement`), plus Admin policies from `AdminPolicies`. `AuthenticatedSessionHandler` registered singleton; `CapabilityAuthorizationHandler` and `ModuleAuthorizationHandler` registered scoped. `AdminPolicies` names are declared here but their gating requirements use shared capability ids from `CanonicalCapabilities` (Admin module) — see External Technical References.

## 10. Current User / Request Context

Location: `src\BA.Dmo.Web\Identity\`, `src\BA.Dmo.Web\Shell\`, `src\BA.Dmo.Domain\Shared\Access\`

| Object | Kind | Members | Direct references | Path |
|---|---|---|---|---|
| `ICurrentUserAccessor` | interface | `Current` | — | Domain `Source\ICurrentUserAccessor.cs` |
| `RequestCurrentUserAccessor` | `ICurrentUserAccessor` | `Current` (cached per request) | `IdentityResolutionService`, `IHttpContextAccessor`, `SessionClaims` | `RequestCurrentUserAccessor.cs` |
| `CurrentUserAuthorshipAccessor` | `IPersistenceAuthorshipAccessor` | `Current` → `PersistenceAuthorship` | `ICurrentUserAccessor`, `IdentityResolutionService`, `IHttpContextAccessor`, `IClock`, `SessionClaims` | `CurrentUserAuthorshipAccessor.cs` |
| `PersistenceAuthorship` | record | `ActorId`, `NowUtc` | — | Application `Shared\Persistence\PersistenceAuthorship.cs` |
| `IPersistenceAuthorshipAccessor` | interface | `Current` | — | Application `Shared\Persistence\PersistenceAuthorship.cs` |
| `SessionClaims` | static class | `AuthenticationScheme="BaDmo.Session"`, `AuthUserIdClaimType="ba_dmo.auth_user_id"` | — | `SessionClaims.cs` |
| `IShellService` | interface | `Current` (ShellState?) | — | Application `Shared\Shell\IShellService.cs` |
| `RequestShellService` | `IShellService` | `Current` (cached per request) | `IdentityResolutionService`, `INavigationService`, `IHttpContextAccessor`, `SessionClaims` | `RequestShellService.cs` |
| `ShellState` | record | `DisplayName`, `ProfileTitle`, `ShellNavigation Navigation` | — | Application `Shared\Shell\IShellService.cs` |

The current user is the `CurrentUser` projection (section 3); actor/display-name resolution returns `ResolvedIdentity.ActorId` used as the persistence authorship `actor_id`.

## 11. Infrastructure Objects

### A. Users/Access-specific shared Infrastructure

| Object | Kind | Implements | Target table(s) | Path |
|---|---|---|---|---|
| `DapperInternalUserRepository` | sealed class | `IInternalUserRepository` | `internal_users`, `access_templates`, `audit_events` (bootstrap audit write) | `src\BA.Dmo.Infrastructure\Identity\DapperInternalUserRepository.cs` |
| `SupabaseAuthAdapter` | sealed class | `ISupabaseAuthAdapter` | Supabase Auth REST (anon) | `src\BA.Dmo.Infrastructure\Auth\SupabaseAuthAdapter.cs` |
| `SupabaseSettings` | static class | — (configuration) | env vars `BA_DMO_SUPABASE_*` | `src\BA.Dmo.Infrastructure\Auth\SupabaseSettings.cs` |
| `DapperModuleCatalogMirrorRepository` | sealed class | `IModuleCatalogMirrorRepository` | `module_catalog_mirror` | `src\BA.Dmo.Infrastructure\Access\DapperModuleCatalogMirrorRepository.cs` |

### B. External provider/framework dependencies (constructor/support)

- Dapper/Npgsql persistence foundation (`Db`, `DbConnectionFactory`, `IDbConnectionFactory`, `DatabaseConnectionSettings`, `DapperUnitOfWork`, `PersistenceMappings`) under `src\BA.Dmo.Infrastructure\Persistence\` — shared infra consumed by Users/Access repositories.
- `HttpClient` for Supabase auth REST calls.

### C. Admin-scoped adapters (external technical reference, not absorbed)

- `SupabaseAdminProvisioningAdapter` (`src\BA.Dmo.Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs`) implements the shared `IAdminProvisioningAdapter` port (defined in Application Shared Identity), but its operational scope is privileged provisioning (bootstrap-admin CLI + admin.gerir-gated user create/password-reset). It is classified as an external technical reference, not counted in the Users/Access-specific Infrastructure set.

## 12. Database Objects

Users/Access-specific tables and indexes (source: `database\migrations\N01_identity.sql`, `N02_catalog.sql`, `N25_remediation.sql` §1.1, `N26_user_modules_override.sql`); reflected identically in `database\consolidated_clean_install.sql`.

### Tables (3)

**`access_templates`**
- PK: `template_id` (text).
- Columns: `name` text NOT NULL, `modules` jsonb NOT NULL DEFAULT `'[]'`, `active` boolean NOT NULL DEFAULT TRUE, `created_at_utc` timestamptz, `created_by` text, `updated_at_utc` timestamptz.

**`internal_users`**
- PK: `actor_id` (text).
- FK: `template_id` text REFERENCES `access_templates (template_id)` NOT NULL.
- UNIQUE: `uq_internal_users_auth_user` UNIQUE (`auth_user_id`) — added by N25.
- Columns: `auth_user_id` uuid NOT NULL (logical Supabase Auth link, no FK to `auth.users`), `display_name` text NOT NULL, `profile_title` text NULL, `active` boolean NOT NULL DEFAULT TRUE, `created_at_utc`, `updated_at_utc`, `modules_override` jsonb NULL (added by N26).

**`module_catalog_mirror`**
- PK: `module_id` (text).
- Columns: `display_name` text NOT NULL, `display_order` integer NOT NULL, `active` boolean NOT NULL DEFAULT TRUE, `synced_at_utc` timestamptz.

### Indexes (5)

| Index | Table | Columns |
|---|---|---|
| `ix_access_templates_active` | access_templates | (active) |
| `ix_internal_users_auth_user_id` | internal_users | (auth_user_id) |
| `ix_internal_users_active` | internal_users | (active) |
| `ix_internal_users_template_id` | internal_users | (template_id) |
| `ix_module_catalog_mirror_order` | module_catalog_mirror | (display_order) |

### Triggers (0)

No Users/Access-specific trigger exists. `trg_audit_events_append_only` (N01) applies to `audit_events`, a shared/global object, not a Users/Access table.

### Constraints (separate)

- PKs: `access_templates.template_id`, `internal_users.actor_id`, `module_catalog_mirror.module_id`.
- FK: `internal_users.template_id → access_templates.template_id`.
- UNIQUE: `uq_internal_users_auth_user (auth_user_id)`.

### Shared / external DB dependencies (not counted)

- `audit_events` — shared global append-only audit table; written by `DapperInternalUserRepository` bootstrap and consumed by Admin/História. Not Users/Access-specific.
- `auth.users` — external Supabase Auth table; referenced logically by `internal_users.auth_user_id` (no local FK). Not present in this repository's schema.

Counts: DB objects = 3 tables + 5 indexes + 0 triggers = **8** (constraints separated).

## 13. Migration Touchpoints

Distinct migrations that directly introduce/change Users/Access-specific DB objects:

| Migration | Users/Access scope |
|---|---|
| `N01_identity.sql` | creates `access_templates` + `ix_access_templates_active`; creates `internal_users` + indexes; roles `ba_dmo_app`/`ba_dmo_migrate`; shared `ba_dmo_guard_append_only()` function. |
| `N02_catalog.sql` | creates `module_catalog_mirror` + `ix_module_catalog_mirror_order`. |
| `N25_remediation.sql` | §1.1: `internal_users.auth_user_id` set NOT NULL + `uq_internal_users_auth_user` UNIQUE. (Remainder of N25 touches job_on/bq/pegamento/repair/peso/audit — not Users/Access-specific.) |
| `N26_user_modules_override.sql` | adds `internal_users.modules_override` jsonb column. |

`N12_rls.sql` enables RLS + `ba_dmo_app_access` policy on `internal_users`, `access_templates`, `audit_events`, `module_catalog_mirror` as part of the global security contract covering all tables; it is counted as shared RLS coverage (not a distinct Users/Access DDL migration).

Distinct Users/Access migration files: **4** (N01, N02, N25 §1.1, N26).

## 14. User Surface

**User Surface: None / No dedicated rendered surface.**

There is no dedicated rendered page/route/UI for "Users / Access" in current source (`src\BA.Dmo.Web\Pages\` has no `Users` or `Access` folder). The shared surfaces present are shell/auth surfaces — `AccessDenied`, `NoAccess`, and the `Index` landing — which are shared shell/auth, not a Users/Access surface. The Admin pages under `Pages\Admin\Users` belong to the Admin module and are not reused as a Users/Access surface. The Login surface under `Pages\Auth` is separate (18_LOGIN.md). User Surface source-verified: YES.

## 15. Web / Routes

Dedicated Users / Access routes: **0**.

Shared authorization/web wiring present in `src\BA.Dmo.Web\Program.cs`:

- Session cookie authentication scheme `BaDmo.Session` (`SessionClaims.AuthenticationScheme`) with cookie options (login/logout/access-denied paths, 8h expiry, HttpOnly/SameSite=Lax).
- Fallback authorization policy requiring `AuthenticatedSessionRequirement`.
- Policy registration per canonical module/capability and Admin policies.
- `<ICurrentUserAccessor>` scoped `RequestCurrentUserAccessor`; `IPersistenceAuthorshipAccessor` scoped `CurrentUserAuthorshipAccessor`; `IdentityResolutionService` scoped; `INavigationService` singleton (`NavigationService`); `IShellService` scoped (`RequestShellService`); `ISupabaseAuthAdapter` singleton (`SupabaseAuthAdapter`); `IAdminProvisioningAdapter` singleton (`SupabaseAdminProvisioningAdapter`); `IModuleCatalogMirrorRepository` singleton (`DapperModuleCatalogMirrorRepository`); `AccessResolver` singleton; `IClock` singleton (`SystemClock`).

The `/login`, `/logout`, `/access-denied`, `/no-access` and `/` routes are shell/auth surfaces belonging to LOGIN/shell units; `/admin/*` belongs to 15_ADMIN.md. No Users/Access route is registered.

## 16. Static Assets

Dedicated Users/Access static assets: **0**.

The global stylesheet set (`wwwroot\styles\dmo-*.css`) and shared scripts under `wwwroot\scripts\` are shared shell/assets for all modules. No static asset carries an access-condition selector specific to Users/Access (grep for capability/access-condition selectors returned no Users/Access match).

## 17. Tests

### Unit tests — Shared Access (10)

`tests\BA.Dmo.UnitTests\Shared\Access\`: `AccessResolverTests.cs`, `CanonicalModuleCatalogTests.cs`, `CanonicalPageCatalogTests.cs`, `CapabilityAndModuleDefinitionTests.cs`, `CatalogValidatorTests.cs`, `CurrentUserTests.cs`, `GrantNormalizerTests.cs`, `ModuleCatalogMirrorSynchronizerTests.cs`, `ModuleCatalogTests.cs`, `NavigationServiceTests.cs`.

### Unit tests — Shared Identity (3)

`tests\BA.Dmo.UnitTests\Shared\Identity\`: `AccessTemplateGrantsParserTests.cs`, `IdentityResolutionServiceTests.cs`, `BootstrapAdminServiceTests.cs`.

### Integration tests — shared Access/Identity (7)

| Class | File | Target |
|---|---|---|
| `CatalogCompositionGuardTests` | `tests\BA.Dmo.IntegrationTests\Access\CatalogCompositionGuardTests.cs` | canonical catalog composition |
| `ShellRoutingTests` | `tests\BA.Dmo.IntegrationTests\Access\ShellRoutingTests.cs` | shell routing / shared access navigation |
| `IdentitySecurityGuardTests` | `tests\BA.Dmo.IntegrationTests\Identity\IdentitySecurityGuardTests.cs` | session/identity security contract |
| `IdentityAmbiguityLandingTests` | `tests\BA.Dmo.IntegrationTests\Identity\IdentityAmbiguityLandingTests.cs` | ambiguous identity landing |
| `WebAuthSessionTests` | `tests\BA.Dmo.IntegrationTests\Identity\WebAuthSessionTests.cs` | session authentication |
| `SupabaseAuthAdapterTests` | `tests\BA.Dmo.IntegrationTests\Identity\SupabaseAuthAdapterTests.cs` | shared auth adapter |
| `SupabaseAdminProvisioningAdapterTests` | `tests\BA.Dmo.IntegrationTests\Identity\SupabaseAdminProvisioningAdapterTests.cs` | shared provisioning port impl |

Not absorbed (direct target is Admin or a functional module, not shared Access/Identity): `AdminSecurityGuardTests`, `AdminFormAntiforgeryTests`, `AdminWebAuthorizationTests`, `DapperAdminRepositoryProjectionTests`, `AdminUserListResetTests` (Admin); `HistoriaWebAuthorizationTests`, `BoquilhasWebAuthorizationTests` (module consumers). Login page tests (if any) not counted.

Test classes directly targeting shared Access/Identity: **20** (10 + 3 + 7).

## 18. Test Doubles / Helpers

### Dedicated test support files (1)

- `tests\BA.Dmo.IntegrationTests\Identity\FakeHttpMessageHandler.cs` — shared HTTP message handler fake for Supabase auth adapter tests.

(`tests\BA.Dmo.IntegrationTests\Access\FakeBoquilhasWebRepository.cs` targets the Boquilhas module and is not shared Access/Identity support.)

### In-file fixture files (5)

Files containing embedded fakes/doubles whose direct target is shared Access/Identity:

| File | Embedded doubles |
|---|---|
| `tests\BA.Dmo.UnitTests\Shared\Identity\IdentityResolutionServiceTests.cs` | `FakeInternalUserRepository` |
| `tests\BA.Dmo.UnitTests\Shared\Identity\BootstrapAdminServiceTests.cs` | `FakeProvisioningAdapter`, `FakeInternalUserRepository` |
| `tests\BA.Dmo.IntegrationTests\Access\ShellRoutingTests.cs` | `FakeAuthAdapter`, `FakeIdentityRepository`, module fakes |
| `tests\BA.Dmo.IntegrationTests\Identity\WebAuthSessionTests.cs` | `FakeAuthAdapter`, `FakeIdentityRepository` |
| `tests\BA.Dmo.IntegrationTests\Identity\IdentityAmbiguityLandingTests.cs` | `FakeAuthAdapter`, `FakeIdentityRepository` |

In-file fixture count counts files: **5**.

## 19. Direct Users / Access References

One edge per source-proven relationship:

- `AccessResolver` → `ModuleCatalog`, `PageCatalog`, `GrantNormalizer`
- `GrantNormalizer` → `ModuleCatalog`
- `AccessResolver` → `CanonicalModuleCatalog` (ids), `CanonicalPageCatalog` (ids)
- `IdentityResolutionService` → `IInternalUserRepository`
- `IdentityResolutionService` → `AccessResolver`, `AccessTemplateGrantsParser`, `AccessTemplateDefinition`, `CurrentUser`
- `CurrentUserAuthorshipAccessor` → `ICurrentUserAccessor`, `IdentityResolutionService`, `IPersistenceAuthorshipAccessor`
- `RequestCurrentUserAccessor` → `ICurrentUserAccessor`, `IdentityResolutionService`, `SessionClaims`
- `RequestShellService` → `IdentityResolutionService`, `INavigationService`, `IShellService`
- `NavigationService` → `AccessResolver`, `PageCatalog`, `ModuleCatalog`
- `ModuleAuthorizationHandler` → `ICurrentUserAccessor`, `ModuleRequirement`
- `CapabilityAuthorizationHandler` → `ICurrentUserAccessor`, `CapabilityRequirement`
- `ModuleCatalogMirrorSynchronizer` → `ModuleCatalog`
- `DapperInternalUserRepository` → `internal_users`, `access_templates`, `audit_events`
- `DapperModuleCatalogMirrorRepository` → `module_catalog_mirror`
- `BootstrapAdminService` → `IAdminProvisioningAdapter`, `IInternalUserRepository`
- `BootstrapAdminCommand` → `BootstrapAdminService`, `SupabaseAdminProvisioningAdapter`, `DapperInternalUserRepository`
- `CanonicalModuleCatalog` → `JobonModuleCatalog` (jobon capability constants)

## 20. External Technical References

| Users / Access Object | External Technical Reference | Reference Type |
|---|---|---|
| `AccessResolver` / `EffectiveAccess` | `NavigationService` (shell) | shared catalog consumer |
| `CanonicalModuleCatalog` / `CanonicalPageCatalog` | `Program.cs` policy registration | web authorization wiring |
| `CanonicalCapabilities` (Admin module) | `Program.cs` admin policies; `AdminAuditService`; `AdminMirrorService`; `AdminTemplateService`; `AdminUserService` | Admin consumer |
| `IAdminProvisioningAdapter` | `SupabaseAdminProvisioningAdapter` | application port (privileged provisioning) |
| `IAdminProvisioningAdapter` / `SupabaseAdminProvisioningAdapter` | `AdminUserService` (admin user create/password-reset); `BootstrapAdminCommand` | admin consumer + constructor dependency |
| `ICurrentUserAccessor` | `AdminAuthorizationGate`, `JobOnAuthorizationGate`, module gates | constructor dependency |
| `IPersistenceAuthorshipAccessor` | `CurrentUserAuthorshipAccessor` | application port |
| `GrantNormalizer` | `AdminTemplateService` (Admin template write) | Admin consumer |
| `AdminPolicies`/`CapabilityPolicies`/`ModulePolicies` | `Program.cs` | web authorization wiring |
| `internal_users.auth_user_id` | Supabase `auth.users` | external auth identity reference (no local FK) |
| `audit_events` | `DapperInternalUserRepository` bootstrap write; AdminAuditService; História | shared DB dependency |
| `ISupabaseAuthAdapter` | `<Login>` sign-in use (see 18_LOGIN.md) | Login consumer |
| Dapper/Npgsql `IDbConnectionFactory`, `Db`, `DbConnectionFactory`, `DatabaseConnectionSettings`, `DapperUnitOfWork`, `PersistenceMappings` | `DapperInternalUserRepository`, `DapperModuleCatalogMirrorRepository` | framework dependency |
| `JobonModuleCatalog` | `CanonicalModuleCatalog` | shared catalog consumer |
| `CanonicalModuleCatalog.AreaChildren` | `CatalogValidator`, `AccessResolver`, `NavigationService` | shared catalog consumer |

## 21. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `CurrentUser`, `Capability`, `ModuleCatalog`, `ModuleDefinition`, `ModuleKind`, `JobonModuleCatalog` | Domain | `src\BA.Dmo.Domain\Shared\Access\` |
| `ICurrentUserAccessor` | Domain | `src\BA.Dmo.Domain\Shared\Access\ICurrentUserAccessor.cs` |
| `DomainError`, `IClock`, `SystemClock`, `Result` | Domain Kernel | `src\BA.Dmo.Domain\Shared\Kernel\` |
| `CanonicalModuleCatalog`, `CanonicalPageCatalog`, `PageCatalog`, `PageDefinition` | Application Access | `src\BA.Dmo.Application\Shared\Access\` |
| `AccessResolver`, `EffectiveAccess`, `FirstPageResolution`, `FirstPageOutcome` | Application Access | `src\BA.Dmo.Application\Shared\Access\AccessResolver.cs` |
| `GrantNormalizer`, `NormalizationResult` | Application Access | `src\BA.Dmo.Application\Shared\Access\GrantNormalizer.cs` |
| `CatalogValidator`, `CatalogValidationException` | Application Access | `src\BA.Dmo.Application\Shared\Access\CatalogValidator.cs` |
| `AccessTemplateDefinition`, `ModuleGrant` | Application Access | `src\BA.Dmo.Application\Shared\Access\AccessTemplateDefinition.cs` |
| `NavigationService`, `INavigationService`, `ShellNavigation`, `NavigationItem/Tab/Area` | Application Access | `src\BA.Dmo.Application\Shared\Access\NavigationService.cs` |
| `IModuleCatalogMirrorRepository`, `ModuleCatalogMirrorRow` | Application Access | `src\BA.Dmo.Application\Shared\Access\IModuleCatalogMirrorRepository.cs` |
| `ModuleCatalogMirrorSynchronizer`, `MirrorDisplayEntry`, `MirrorValidationReport` | Application Access | `src\BA.Dmo.Application\Shared\Access\ModuleCatalogMirrorSynchronizer.cs` |
| `IInternalUserRepository`, `InternalUserRecord`, `BootstrapAdminCreation` | Application Identity | `src\BA.Dmo.Application\Shared\Identity\IInternalUserRepository.cs` |
| `AuthUser`, `EnsuredAuthUser`, `ISupabaseAuthAdapter`, `IAdminProvisioningAdapter` | Application Identity | `src\BA.Dmo.Application\Shared\Identity\SupabaseAuthPorts.cs` |
| `AccessTemplateGrantsParser` | Application Identity | `src\BA.Dmo.Application\Shared\Identity\AccessTemplateGrantsParser.cs` |
| `IdentityResolutionService`, `ResolvedIdentity` | Application Identity | `src\BA.Dmo.Application\Shared\Identity\IdentityResolutionService.cs` |
| `BootstrapAdminService`, `BootstrapAdminOptions`, `BootstrapAdminOutcome` | Application Identity | `src\BA.Dmo.Application\Shared\Identity\BootstrapAdminService.cs` |
| `AmbiguousIdentityException` | Application Identity | `src\BA.Dmo.Application\Shared\Identity\AmbiguousIdentityException.cs` |
| `PersistenceAuthorship`, `IPersistenceAuthorshipAccessor` | Application Persistence | `src\BA.Dmo.Application\Shared\Persistence\PersistenceAuthorship.cs` |
| `IShellService`, `ShellState` | Application Shell | `src\BA.Dmo.Application\Shared\Shell\IShellService.cs` |
| `DapperInternalUserRepository` | Infrastructure Identity | `src\BA.Dmo.Infrastructure\Identity\DapperInternalUserRepository.cs` |
| `DapperModuleCatalogMirrorRepository` | Infrastructure Access | `src\BA.Dmo.Infrastructure\Access\DapperModuleCatalogMirrorRepository.cs` |
| `SupabaseAuthAdapter`, `SupabaseSettings` | Infrastructure Auth | `src\BA.Dmo.Infrastructure\Auth\` |
| `SupabaseAdminProvisioningAdapter` | Infrastructure Auth (external/admin-scoped) | `src\BA.Dmo.Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs` |
| `Db`, `DbConnectionFactory`, `IDbConnectionFactory`, `DatabaseConnectionSettings`, `DapperUnitOfWork`, `PersistenceMappings` | Infrastructure Persistence | `src\BA.Dmo.Infrastructure\Persistence\` |
| `SessionClaims`, `RequestCurrentUserAccessor`, `CurrentUserAuthorshipAccessor` | Web Identity | `src\BA.Dmo.Web\Identity\` |
| `AuthenticatedSessionHandler`, `ModuleAuthorizationHandler`, `CapabilityAuthorizationHandler`, `ModulePolicies`, `CapabilityPolicies`, `AdminPolicies` | Web Authn/Authz | `src\BA.Dmo.Web\Authorization\` |
| `RequestShellService` | Web Shell | `src\BA.Dmo.Web\Shell\RequestShellService.cs` |
| Composition root / policy + DI wiring | Web | `src\BA.Dmo.Web\Program.cs` |
| `BootstrapAdminCommand`, `CliMode`, `CliModeResolver` | Web CLI | `src\BA.Dmo.Web\Cli\` |

## 22. Sources Verified

- `src\BA.Dmo.Domain\Shared\Access\` (CurrentUser, Capability, ModuleCatalog, ModuleDefinition, ModuleKind, ICurrentUserAccessor, JobonModuleCatalog)
- `src\BA.Dmo.Domain\Shared\Kernel\` (DomainError)
- `src\BA.Dmo.Application\Shared\Access\` (All 10 files)
- `src\BA.Dmo.Application\Shared\Identity\` (All 6 files)
- `src\BA.Dmo.Application\Shared\Persistence\PersistenceAuthorship.cs`
- `src\BA.Dmo.Application\Shared\Shell\IShellService.cs`
- `src\BA.Dmo.Infrastructure\Identity\DapperInternalUserRepository.cs`
- `src\BA.Dmo.Infrastructure\Auth\` (SupabaseAuthAdapter, SupabaseSettings, SupabaseAdminProvisioningAdapter)
- `src\BA.Dmo.Infrastructure\Access\DapperModuleCatalogMirrorRepository.cs`
- `src\BA.Dmo.Web\Program.cs`
- `src\BA.Dmo.Web\Authorization\` (3 files)
- `src\BA.Dmo.Web\Identity\` (3 files)
- `src\BA.Dmo.Web\Shell\RequestShellService.cs`
- `src\BA.Dmo.Web\Cli\BootstrapAdminCommand.cs`
- `src\BA.Dmo.Web\Pages\` (confirmed no dedicated Users/Access surface; shared shell/auth pages present)
- `database\migrations\N01_identity.sql`, `N02_catalog.sql`, `N12_rls.sql`, `N25_remediation.sql`, `N26_user_modules_override.sql`
- `database\consolidated_clean_install.sql`
- `src\BA.Dmo.Web\wwwroot\` (styles/scripts scanned for access-condition selectors)
- `tests\BA.Dmo.UnitTests\Shared\Access\` (10 files), `tests\BA.Dmo.UnitTests\Shared\Identity\` (3 files)
- `tests\BA.Dmo.IntegrationTests\Access\` (classification), `tests\BA.Dmo.IntegrationTests\Identity\` (all files)

## Counts

- Domain shared Access files: **7**
- Application shared Access files: **10**
- Application shared Identity files: **6**
- Infrastructure Users/Access files: **4**
- Shared / external infrastructure dependencies: **2** (SupabaseAdminProvisioningAdapter external; Dapper/Npgsql persistence foundation)
- Dedicated Web page files: **0**
- Dedicated Users/Access routes: **0**
- Dedicated static asset files: **0**
- Shared web wiring files: **8** (Program.cs + 3 Authorization + 3 Identity + RequestShellService)
- Shared static asset files: **0**
- Users/Access-specific DB tables: **3**
- Users/Access-specific DB indexes: **5**
- Users/Access-specific DB triggers: **0**
- Users/Access-specific DB objects: **8** (3 tables + 5 indexes + 0 triggers)
- Shared / external DB dependencies: **2** (shared local `audit_events`; external Supabase `auth.users` — no local FK)
- Distinct Users/Access migration files: **4**
- Canonical module IDs: **12**
- Canonical capability IDs: **14**
- Canonical page IDs: **12**
- Test classes: **20**
- Dedicated test support files: **1**
- In-file test fixture files: **5**
- Source-visible user surfaces: **0**