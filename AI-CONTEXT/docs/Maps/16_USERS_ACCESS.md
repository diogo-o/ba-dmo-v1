# BA DMO — Users / Access Technical Map

MAP ID: MAP-16
Status: COMPLETE (reconciled at HEAD 8478308 "Render one persistent Admin navigation")

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

This map is a pure technical inventory/navigation for the shared Users / Access architecture: the reusable identity/access machinery shared across modules, the shell and the Administration. It does NOT absorb the Admin UI/pages/services/gates (mapped in [15_ADMIN.md](15_ADMIN.md), MAP-15) nor the Login surface/request handling (mapped in [18_LOGIN.md](18_LOGIN.md), MAP-18). Only current source is mapped; nothing is inferred or invented.

The CURRENT (post-N27/N31) shared users/access model is:

- **Aplicações** (canonical module catalog, code-first) → **Template** (title/function + ONE functional profile + module grants) → **User** (ONE reusable template).
- A template assigns modules only; the functional profile (Admin / Operador / Controlador / Responsável) is template-owned (`access_template_profiles`, N31) and DERIVES the capability set at resolution (`AccessResolver.ProjectProfileCapabilities`). Stored capability arrays in `access_templates.modules` are no longer authorization input.
- Template assignments are single: `internal_user_access_templates` junction (N27) carries exactly one row per actor (`ux_internal_user_access_templates_actor`, N31), and `IdentityResolutionService` fails closed (`ACCESS_TEMPLATE_AMBIGUOUS`) when more than one ACTIVE template is associated.
- The N26 per-user module override (`internal_users.modules_override`) is DORMANT: N27 resets it to NULL for all rows and the resolver never reads it.

Shared users/access scope covers:

- current-user models/accessors;
- internal-user repositories;
- access template / functional-profile / grant model;
- module/page/capability catalogs;
- access resolution;
- authorization primitives;
- identity/auth ports;
- auth-provider adapters;
- bootstrap identity components;
- persistence/authorship access;
- related DB objects, migrations and tests.

## 2. Layer Summary

| Layer | Contents |
|---|---|
| Domain | `CurrentUser`, `Capability`, `ModuleCatalog`, `ModuleDefinition`, `ModuleKind`, `ICurrentUserAccessor`, `JobonModuleCatalog`, `FunctionalProfile` (+`FunctionalProfileNames`) |
| Application — Access | Catalog, grant normalizer, access resolver (profile-driven), page catalog, navigation, mirror synchronizer |
| Application — Identity | Internal-user port (junction-aware), auth ports, bootstrap service, identity resolution (single-template), template grants parser |
| Infrastructure | `DapperInternalUserRepository` (junction join), `SupabaseAuthAdapter`, `SupabaseSettings`, `DapperModuleCatalogMirrorRepository` |
| Web | Session claims, request current-user accessor, authorship accessor, authorization handlers, shell service, composition-root policy wiring |
| Database | `internal_users`, `access_templates`, `internal_user_access_templates`, `access_template_profiles`, `module_catalog_mirror` (+ shared `app_settings`, `audit_events`) |

## 3. Domain Shared Access Objects

Location: `src\BA.Dmo.Domain\Shared\Access\` (8 files)

| Type | Kind | Members | Key methods | Path |
|---|---|---|---|---|
| `CurrentUser` | sealed record | `InternalUserId` (Guid), `DisplayName`, `Modules` (IReadOnlySet<string>), `Capabilities` (IReadOnlySet<string>) | `HasModule(moduleId)`, `HasCapability(capabilityId)`, `Normalize(values)` | `CurrentUser.cs` |
| `Capability` | sealed record | `Id`, `ModuleSegment` | ctor validates `{moduleId}.{ação}` grammar | `Capability.cs` |
| `ModuleCatalog` | sealed class | `Modules` (ordered), `Count`, `Empty` | `ContainsModule`, `TryGetModule`, `IsCapabilityKnown` | `ModuleCatalog.cs` |
| `ModuleDefinition` | sealed record | `ModuleId`, `DisplayName`, `Kind`, `CanonicalOrder`, `InitialRoute`, `Capabilities`, `IsAssignable` | ctor validates id/route/order; `isAssignable` controls template assignability | `ModuleDefinition.cs` |
| `ModuleKind` | enum | `Module`, `FunctionalArea` | — (NOTE: no catalog entry currently uses `FunctionalArea`; Controlo is declared `Module` — LEGACY CANDIDATE — NEEDS AUDIT) | `ModuleKind.cs` |
| `ICurrentUserAccessor` | interface | `Current` (CurrentUser?) | — | `ICurrentUserAccessor.cs` |
| `JobonModuleCatalog` | static class | const `JobonModuleId="jobon"`, capability IDs `jobon.view/edit/configure/confirmar`, field families `Family*` | — | `JobonModuleCatalog.cs` |
| `FunctionalProfile` / `FunctionalProfileNames` | enum + static class | `Admin`, `OperatorController`, `Responsible`; display names "Admin", "Operador / Controlador", "Responsável" | `TryParse`, `DisplayName()` | `FunctionalProfile.cs` |

Direct Domain references: `CurrentUser` is referenced by `ICurrentUserAccessor`; `ModuleCatalog`/`ModuleDefinition`/`Capability`/`ModuleKind` are referenced by the Application `CanonicalModuleCatalog`; `JobonModuleCatalog` declares the Job On canonical capability constants consumed by `CanonicalModuleCatalog`; `FunctionalProfile` is consumed by `AccessResolver` (Application) and `AdminUserService`.

Share/no-absorb note: `Commands/Capability` guarded per-module capabilities live on each `ModuleDefinition`; only the shared catalog machinery is mapped here.

## 4. Application Shared Access Objects

Location: `src\BA.Dmo.Application\Shared\Access\` (10 files)

| Type | File | Constants/IDs | Principal members | Relevant methods | Direct dependencies |
|---|---|---|---|---|---|
| `CanonicalModuleCatalog` | `CanonicalModuleCatalog.cs` | 12 canonical module ids; 14 capability ids incl. `jobon.*` (4), `controlo.view/edit/submit/review`, `peso.aprovar`, `ferramentas.configure`, `reparacao_interna.corrigir`, `admin.gerir`, `audit.view/export`; `AreaChildren` (controlo → peso, pegamentos); `Descriptions` (Aplicações cards) | `Instance`, `AreaChildren`, `Descriptions` | `Build()` | `ModuleCatalog`, `ModuleDefinition`, `Capability`, `ModuleKind`, `JobonModuleCatalog` |
| `CanonicalPageCatalog` | `CanonicalPageCatalog.cs` | 13 canonical page ids (added `controlo.resumo` — /controlo, controlo.view) | `Instance` | `Build()` | `PageCatalog`, `PageDefinition`, `CanonicalModuleCatalog` |
| `PageCatalog` | `PageCatalog.cs` | — | `Pages`, `Count`, `LandingPage` | `TryGetById`, `TryGetByRoute` | `PageDefinition` |
| `PageDefinition` | `PageCatalog.cs` | route grammar `Regex` | `PageId`, `ModuleId`, `Route`, `RequiredCapabilityId`, `DisplayOrder`, `IsActive`, `IsLanding` | ctor, static `IsValidRoute` | `CanonicalModuleCatalog` |
| `AccessResolver` | `AccessResolver.cs` | `FirstPageOutcome` enum, `FirstPageResolution`, `EffectiveAccess` | ctor (catalog, pages, areaChildren) | `Resolve(templates, profile)`, `IsPageAccessible`, `AccessiblePages`, `ResolveFirstPage`, `ResolveAreaFirstPage`, private `ProjectProfileCapabilities` | `ModuleCatalog`, `PageCatalog`, `GrantNormalizer`, `CanonicalModuleCatalog`, `CanonicalPageCatalog`, `FunctionalProfile` |
| `GrantNormalizer` | `GrantNormalizer.cs` | `NormalizationResult` record | ctor (catalog) | `Normalize(grants)` | `ModuleCatalog`, `ModuleGrant` |
| `CatalogValidator` | `CatalogValidator.cs` | `CatalogValidationException` | — | `Validate` | `ModuleCatalog`, `PageCatalog`, `PageDefinition` |
| `NavigationService` | `NavigationService.cs` | `NavigationItem/Tab/Area`, `ShellNavigation`, `INavigationService` | ctor (pages, resolver, catalog) | `Build` | `EffectiveAccess`, `PageCatalog`, `AccessResolver`, `ModuleCatalog`, `CanonicalPageCatalog` |
| `IModuleCatalogMirrorRepository` | `IModuleCatalogMirrorRepository.cs` | `ModuleCatalogMirrorRow` record | — | `GetAllAsync`, `UpsertAllAsync` | persistence port (no DB types) |
| `ModuleCatalogMirrorSynchronizer` | `ModuleCatalogMirrorSynchronizer.cs` | `MirrorDisplayEntry`, `MirrorValidationReport` | — | `BuildSyncRows`, `ValidateMirrorRows`, `MergeForDisplay` | `ModuleCatalog` |

The Access template model (`AccessTemplateDefinition`, `ModuleGrant`) is mapped in section 6.

## 5. Application Shared Identity Objects

Location: `src\BA.Dmo.Application\Shared\Identity\` (6 files)

| Type | File | Members / methods | Direct dependencies |
|---|---|---|---|
| `InternalUserRecord` | `IInternalUserRepository.cs` | `ActorId`, `AuthUserId`, `DisplayName`, `ProfileTitle`, `UserActive`, `TemplateId`, `TemplateName`, `TemplateActive`, `ModulesJson`, `ModulesOverrideJson` (dormant), `AccessTemplates` (IReadOnlyList<InternalUserAccessTemplateRecord>?) | — |
| `InternalUserAccessTemplateRecord` | `IInternalUserRepository.cs` | `TemplateId`, `TemplateName`, `TemplateActive`, `ModulesJson` — one N27 junction entry | — |
| `IInternalUserRepository` | `IInternalUserRepository.cs` | `FindByAuthUserIdAsync`, `AdminExistsAsync`, `CreateBootstrapAdminAsync` | `BootstrapAdminCreation` |
| `BootstrapAdminCreation` | `IInternalUserRepository.cs` | `ActorId`, `AuthUserId`, `DisplayName`, `TemplateId`, `TemplateName`, `ModulesJson`, `CreatedAtUtc` | — |
| `AuthUser` | `SupabaseAuthPorts.cs` | `AuthUserId`, `Email` | — |
| `EnsuredAuthUser` | `SupabaseAuthPorts.cs` | `AuthUserId`, `Email`, `AccountPreExisted` | — |
| `ISupabaseAuthAdapter` | `SupabaseAuthPorts.cs` | `SignInWithPasswordAsync` → `Result<AuthUser, DomainError>` | `AuthUser`, `DomainError` |
| `IAdminProvisioningAdapter` | `SupabaseAuthPorts.cs` | `EnsureAuthUserAsync`, `EnsureAuthUserWithStatusAsync`, `RequestPasswordResetAsync`, `GetUserEmailsAsync` | `AuthUser`, `EnsuredAuthUser`, `DomainError` |
| `AccessTemplateGrantsParser` | `AccessTemplateGrantsParser.cs` | static `Parse(modulesJson)` → `Result<IReadOnlyList<ModuleGrant>, DomainError>`; `ModulesEntry` private | `ModuleGrant`, `DomainError`, System.Text.Json |
| `AmbiguousIdentityException` | `AmbiguousIdentityException.cs` | `AuthUserId` | data-integrity typed exception |
| `IdentityResolutionService` | `IdentityResolutionService.cs` | `ResolvedIdentity` record; `ResolveAsync(authUserId)` (request-memoized) | `IInternalUserRepository`, `AccessResolver`, `CurrentUser`, `AccessTemplateGrantsParser`, `AccessTemplateDefinition`, `FunctionalProfileNames` |
| `BootstrapAdminService` | `BootstrapAdminService.cs` | const `BootstrapTemplateId="tpl-bootstrap-admin"`, `BootstrapTemplateName`, `BootstrapModulesJson`; `BootstrapAdminOptions`, `BootstrapAdminOutcome` enum; `RunAsync` | `IAdminProvisioningAdapter`, `IInternalUserRepository`, `IClock`, `DomainError` |

Identity error codes visible in source: `INTERNAL_USER_INACTIVE`, `ACCESS_TEMPLATE_INACTIVE`, `ACCESS_TEMPLATE_AMBIGUOUS` (more than one ACTIVE template associated), `FUNCTIONAL_PROFILE_INVALID` (profile_title not one of the three), `IDENTITY_AMBIGUOUS` (more than one internal_users row per auth_user_id), `IDENTITY_RESOLUTION_UNAVAILABLE`, `ACCESS_TEMPLATE_MODULES_INVALID`.

Shared Identity is kept separate from Admin-specific services (`AdminUserService`, `AdminTemplateService`, `AdminMirrorService`, `AdminAuditService`), which are mapped in [15_ADMIN.md](15_ADMIN.md).

## 6. Access Template / Grant Model

Location: `src\BA.Dmo.Application\Shared\Access\AccessTemplateDefinition.cs`, `src\BA.Dmo.Application\Shared\Identity\AccessTemplateGrantsParser.cs`, `src\BA.Dmo.Application\Shared\Access\GrantNormalizer.cs`, `src\BA.Dmo.Domain\Shared\Access\FunctionalProfile.cs`

**Current storage model (N01 + N27 + N31):**
- `access_templates` carries `template_id`, `name` (title/function), `modules` jsonb `[{ moduleId, capabilities: [] }]` (module-only entries after N27), `active`.
- `access_template_profiles` (N31) carries the ONE functional profile per template: `functional_profile` constrained to `('Admin', 'Operador / Controlador', 'Responsável')`; PK `template_id` FK `access_templates` ON DELETE CASCADE.
- `internal_user_access_templates` (N27) carries the single template assignment per user (`ux_internal_user_access_templates_actor` UNIQUE (actor_id) after N31).

| Object | Literal behavior |
|---|---|
| `ModuleGrant` | record `ModuleId` + `Capabilities` (IReadOnlyList<string>). Presence of the module grants ENTRY; capabilities are LEGACY data — capability OWNERSHIP for authorization is now derived from the functional profile (`AccessResolver.ProjectProfileCapabilities`). |
| `AccessTemplateDefinition` | record `TemplateId`, `Name`, `Active`, `Grants`, `PreferredFirstPageId` (read-only field, never consulted by resolution — 05_SHL §4). |
| `FunctionalProfile` / `FunctionalProfileNames` | the three and only three profiles; `TryParse`/`DisplayName` used by `IdentityResolutionService` (resolution) and `AdminUserService`/`AdminTemplateService` (Admin validation). |
| `AccessTemplateGrantsParser.Parse` | deserializes `modules` jsonb → `List<ModuleGrant>`; structural JSON defects fail as `ACCESS_TEMPLATE_MODULES_INVALID`; blank module ids are skipped. `ModulesEntry` DTO has `ModuleId` and `Capabilities`. |
| `GrantNormalizer.Normalize` | discards unknown modules, NON-ASSIGNABLE modules (`peso`, `pegamentos`, `historia`), duplicate module entries, capabilities that do not belong to the granted module per the catalog, blank capabilities; reports discarded entries. Returns `NormalizationResult(Grants, DiscardedEntries)`. |
| Template-owned profile | `access_template_profiles.functional_profile` (N31). On INSERT of an `access_templates` row the AFTER INSERT trigger `trg_access_templates_ensure_profile` → function `ba_dmo_ensure_access_template_profile()` inserts `'Admin'` when `modules @> '[{"moduleId":"admin"}]'` else `'Operador / Controlador'` (`ON CONFLICT (template_id) DO NOTHING`). N31 also backfilled existing templates (preferring the unanimous `internal_users.profile_title`). `TemplateProfileStore` (Web layer, MAP-15) reads/upserts the profile and re-syncs `internal_users.profile_title`. |
| Module override (N26) | `internal_users.modules_override` jsonb — **DORMANT**: N27 sets the column to NULL for every row (and preserves any legacy override as a `legacy-override-<md5(actor_id)[..24]>` compatibility template); `IdentityResolutionService` never reads it (verified; unit test `ModulesOverride_IsDormant_AndDoesNotReplaceTemplateModules`). The port method `IAdminRepository.SetUserModulesOverrideAsync` remains implemented but has no caller — LEGACY CANDIDATE — NEEDS AUDIT. |
| Single template assignment | N31 deleted any junction row whose `template_id <> internal_users.template_id`, re-inserted one row per user, and added the UNIQUE index on `(actor_id)`. Runtime enforcement: `IdentityResolutionService` resolves `AccessTemplates`, filters ACTIVE templates, and if the count != 1 fails closed with `ACCESS_TEMPLATE_AMBIGUOUS` ("O utilizador tem mais do que um template ativo associado…"). |

## 7. Canonical Module / Capability / Page Catalogs

### Canonical Module Catalog (12 entries)

Counts: **12 module entries**; area children: `controlo → [peso, pegamentos]` (children are `isAssignable: false` technical children); `historia`, `peso`, `pegamentos` are `isAssignable: false` (História derived at resolution; Peso/Pegamentos internal to Controlo); Controlo is declared `ModuleKind.Module` (NOT `FunctionalArea` — the enum value is currently unused, LEGACY CANDIDATE — NEEDS AUDIT).

| Module ID | Display | Kind | CanonicalOrder | Route | Capabilities | Assignable |
|---|---|---|---|---|---|---|
| `jobon` | Job On | Module | 5 | /jobon | view, edit, configure, confirmar | yes |
| `boquilhas` | Boquilhas | Module | 10 | /boquilhas | — | yes |
| `controlo` | Controlo | Module | 20 | /controlo | view, edit, submit, review | yes (single top-level grant) |
| `peso` | Peso | Module | 21 | /peso | aprovar | NO (derived from controlo) |
| `pegamentos` | Pegamentos | Module | 22 | /pegamentos | — | NO (derived from controlo) |
| `ferramentas` | Ferramentas | Module | 40 | /ferramentas | configure | yes |
| `armazem` | Armazém | Module | 50 | /armazem | — | yes |
| `reparacao_interna` | Reparação Interna | Module | 60 | /reparacao-interna | corrigir | yes |
| `reparacao_externa` | Reparação Externa | Module | 70 | /reparacao-externa | — | yes |
| `tampoes` | Tampões | Module | 80 | /tampoes | — | yes |
| `historia` | História | Module | 90 | /historia | — | NO (transversal, derived) |
| `admin` | Administração | Module | 99 | /admin | gerir, audit.view, audit.export | yes |

### Canonical Capability IDs (14, declared on module definitions)

`jobon.view`, `jobon.edit`, `jobon.configure`, `jobon.confirmar` (jobon); `controlo.view`, `controlo.edit`, `controlo.submit`, `controlo.review` (controlo); `peso.aprovar` (peso); `ferramentas.configure` (ferramentas); `reparacao_interna.corrigir` (reparacao_interna); `admin.gerir`, `audit.view`, `audit.export` (admin).

**IMPORTANT:** these catalog declarations define capability OWNERSHIP and the per-module policy names, but at RUNTIME the granted capability SET is derived by `AccessResolver.ProjectProfileCapabilities(profile, modules)` — stored template capability arrays are not authorization input (verified: unit test `InvalidGrantEntries_AreDiscarded_NotSilentlyRepaired` in `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Identity\IdentityResolutionServiceTests.cs`; integration test `AdminProfile_DerivesAuditAccess_FromAdminModule` in `AdminWebAuthorizationTests.cs`).

### Canonical Page Catalog (13 pages)

| Page ID | Module | Route | RequiredCapability | DisplayOrder | Active / Landing |
|---|---|---|---|---|---|
| `jobon.folha` | jobon | /jobon | jobon.view | 5 | isLanding=true |
| `boquilhas.registo` | boquilhas | /boquilhas | null | 10 | active |
| `controlo.resumo` | controlo | /controlo | controlo.view | 20 | active (NEW page id) |
| `peso.operador` | peso | /peso | null | 21 | active (exclusive: hidden from peso.aprovar holders) |
| `peso.responsavel` | peso | /peso/responsavel | peso.aprovar | 21 | active |
| `pegamentos.folha` | pegamentos | /pegamentos | null | 22 | active |
| `ferramentas.lista` | ferramentas | /ferramentas | null | 40 | active |
| `armazem.mapa` | armazem | /armazem | null | 50 | active |
| `reparacao_interna.registo` | reparacao_interna | /reparacao-interna | null | 60 | active |
| `reparacao_externa.listas` | reparacao_externa | /reparacao-externa | null | 70 | active |
| `tampoes.quantidades` | tampoes | /tampoes | null | 80 | active |
| `historia.consulta` | historia | /historia | null | 90 | active |
| `admin.gestao` | admin | /admin | admin.gerir | 99 | active |

All catalog entries are built with `isActive: true` (source default). `CanonicalModuleCatalog.Instance` and `CanonicalPageCatalog.Instance` are composition-time singletons validated by `CatalogValidator.Validate` in `Program.cs` (lines 64–67).

### Catalog-location note (POTENTIAL OVERLAP — NEEDS AUDIT)

Module-id/capability constants are declared in two different layers: `JobonModuleCatalog` lives in the **Domain** (`src\BA.Dmo.Domain\Shared\Access\JobonModuleCatalog.cs`, jobon ids + field families) while `HistoriaModuleCatalog` lives in the **Application** (`src\BA.Dmo.Application\Modules\Historia\HistoriaModuleCatalog.cs`, `ModuleId = "historia"` + `OriginModuleIds` array re-declaring catalog module ids). The `historia` id and the origin module ids duplicate `CanonicalModuleCatalog` constants (Application Shared\Access) without referencing them. Location is inconsistent across layers — current state described; classification: **POTENTIAL OVERLAP — NEEDS AUDIT** (not addressed by the access-model rework).

`ModuleKind.FunctionalArea` (Domain `src\BA.Dmo.Domain\Shared\Access\ModuleKind.cs`) is declared but no `CanonicalModuleCatalog` entry uses it — Controlo is built as `ModuleKind.Module`. Classification: **LEGACY CANDIDATE — NEEDS AUDIT**.

## 8. Access Resolution Objects

Location: `src\BA.Dmo.Application\Shared\Access\AccessResolver.cs`, `src\BA.Dmo.Application\Shared\Identity\IdentityResolutionService.cs`

| Object | Inputs | Outputs | Principal methods | Path |
|---|---|---|---|---|
| `GrantNormalizer` | `IEnumerable<ModuleGrant>` | `NormalizationResult` | `Normalize` | `GrantNormalizer.cs` |
| `AccessResolver` | `AccessTemplateDefinition` + `FunctionalProfile` | `EffectiveAccess` | `Resolve`, `ProjectProfileCapabilities` (private) | `AccessResolver.cs` |
| `EffectiveAccess` | — | `NavigationModules`, `VisibleAreaChildren`, `HasModule`, `HasCapability`, `AuthorizedModuleIds`, `GrantedCapabilityIds`, `IsEmpty` | — | `AccessResolver.cs` |
| `AccessResolver.IsPageAccessible` | `EffectiveAccess`, `PageDefinition` | bool (incl. Peso exclusivity: `peso.operador` hidden from `peso.aprovar` holders) | — | `AccessResolver.cs` |
| `AccessResolver.AccessiblePages` | `EffectiveAccess` | `IReadOnlyList<PageDefinition>` | — | `AccessResolver.cs` |
| `AccessResolver.ResolveFirstPage` | `EffectiveAccess` | `FirstPageResolution(Outcome, Page?)` | — | `AccessResolver.cs` |
| `AccessResolver.ResolveAreaFirstPage` | `EffectiveAccess`, areaId | `PageDefinition?` | — | `AccessResolver.cs` |
| `IdentityResolutionService` | `Guid authUserId` | `Result<ResolvedIdentity, DomainError>` | `ResolveAsync` | `IdentityResolutionService.cs` |
| `ResolvedIdentity` | — | `CurrentUser`, `ActorId`, `ProfileTitle` (= active template NAME), `EffectiveAccess Access`, `FirstPageResolution FirstPage` | — | `IdentityResolutionService.cs` |

**Resolution pipeline (current, verified in source):**

1. Cookie carries ONLY `ba_dmo.auth_user_id` (`SessionClaims`); `RequestCurrentUserAccessor` / `RequestShellService` / `LoginModel` call `IdentityResolutionService.ResolveAsync(authUserId)` (request-memoized).
2. `DapperInternalUserRepository.FindByAuthUserIdAsync` runs `internal_users u LEFT JOIN internal_user_access_templates ut ON ut.actor_id = u.actor_id LEFT JOIN access_templates t ON t.template_id = ut.template_id WHERE u.auth_user_id = @AuthUserId`; >1 distinct `actor_id` → `AmbiguousIdentityException` → `IDENTITY_AMBIGUOUS` (Unauthorized, plain /no-access).
3. No record or inactive user → `INTERNAL_USER_INACTIVE`. Inactive template → `ACCESS_TEMPLATE_INACTIVE`.
4. `associatedTemplates` = `record.AccessTemplates` (junction list); when empty it falls back to a single record built from the compatibility columns (`template_id`, `template_name`, `template_active`, `modules_json`) so a missing junction row cannot be confused with "no template".
5. ACTIVE template count != 1 → **`ACCESS_TEMPLATE_AMBIGUOUS`** fail-closed (single-template model; resolve([template], profile) is never fed a merged Admin+Operador surface).
6. `profile_title` parsed via `FunctionalProfileNames.TryParse` → invalid → `FUNCTIONAL_PROFILE_INVALID`.
7. `AccessResolver.Resolve(template, profile)`:
   - normalizes the template grants (modules only);
   - Admin profile: drops every module except `admin`; non-Admin: drops `admin`;
   - `controlo` grant expands to `peso` + `pegamentos` (technical entries keep routes working);
   - `historia` is added for non-Admin profiles with at least one module (derived transversal surface, never assignable);
   - `ProjectProfileCapabilities(profile, modules)` derives capabilities: Admin → `admin.gerir` + `audit.view` + `audit.export`; jobon → `jobon.view` + `jobon.confirmar` (+ `jobon.edit` + `jobon.configure` for Responsável); ferramentas → `ferramentas.configure` for Responsável; controlo → `controlo.view` + (`controlo.edit`+`controlo.submit` for Operador/Controlador | `controlo.review`+`peso.aprovar` for Responsável);
   - defense-in-depth intersection with the catalog; navigationModules = `ModuleKind.Module` entries.
8. `ResolveFirstPage`: Job On landing when accessible (functional users); otherwise first accessible page in canonical order → for an Admin template the outcome is `FallbackCanonicalOrder` → `/admin` (admin has no jobon.view by owner decision); no accessible page → `NoAccess`.

`FirstPageOutcome` enum: `Landing`, `FallbackCanonicalOrder`, `NoAccess`. Inactive templates resolve to an empty `EffectiveAccess` (no grants).

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
| `ModulePolicies` | static class | const names `BaDmo.Module.{moduleId}` for the 11 functional catalog modules | `ModuleAuthorizationHandler.cs` |
| `CapabilityPolicies` | static class | const names `BaDmo.Capability.{capabilityId}` | `ModuleAuthorizationHandler.cs` |
| `AdminPolicies` | static class | `BaDmo.Admin.Gerir`, `BaDmo.Audit.View`, `BaDmo.Audit.Export` (named Admin policies) | `CapabilityAuthorizationHandler.cs` |

Policy registration in `src\BA.Dmo.Web\Program.cs` (current line numbers): fallback policy → `AuthenticatedSessionRequirement` (lines 98–101); Admin policies (105–113); one policy per catalog module via `ModulePolicies` + `ModuleRequirement` and one per catalog capability via `CapabilityPolicies` + `CapabilityRequirement` (loop over `CanonicalModuleCatalog.Instance.Modules`, lines 118–131). `AuthenticatedSessionHandler` registered singleton (133); `CapabilityAuthorizationHandler` and `ModuleAuthorizationHandler` registered scoped (134–135). `AdminPolicies` names are declared here but their gating requirements use shared capability ids from `CanonicalCapabilities` — see External Technical References.

**Direct URL authorization:** every Razor page is protected by the fallback policy (session required); module pages add `[Authorize(Policy = ModulePolicies.X)]` / capability policies (e.g. `/admin/*` uses `AdminPolicies.*`); the authorization handlers resolve the per-request identity through `RequestCurrentUserAccessor` (server-side resolution; grants never come from the client). Deep-link denial is handled by `AccessDeniedModel` (redirect to first authorized page with `?acesso-negado=1`; see [18_LOGIN.md](18_LOGIN.md) §9).

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

The current user is the `CurrentUser` projection (section 3); actor/display-name resolution returns `ResolvedIdentity.ActorId` used as the persistence authorship `actor_id`. Shell navigation is derived (`NavigationService.Build`) into `ShellNavigation(LeftItems, AdminEntry)`: left tabs from `access.NavigationModules` in canonical order (excluding `admin` — right-aligned `AdminEntry` — and excluding area-child ids so Peso/Pegamentos never render as global tabs); `AdminEntry` exists only when `admin.gestao` is accessible (`admin.gerir`). `_Header.cshtml` renders `_Navigation.cshtml` only outside the `/admin` scope; inside `/admin` the persistent `_AdminNav.cshtml` tab strip is used instead ([15_ADMIN.md](15_ADMIN.md) §12).

## 11. Infrastructure Objects

### A. Users/Access-specific shared Infrastructure

| Object | Kind | Implements | Target table(s) | Path |
|---|---|---|---|---|
| `DapperInternalUserRepository` | sealed class | `IInternalUserRepository` | `internal_users` + `internal_user_access_templates` + `access_templates` (find join); `access_templates` + `internal_users` + junction + `audit_events` (bootstrap write) | `src\BA.Dmo.Infrastructure\Identity\DapperInternalUserRepository.cs` |
| `SupabaseAuthAdapter` | sealed class | `ISupabaseAuthAdapter` | Supabase Auth REST (anon) `POST /auth/v1/token?grant_type=password` | `src\BA.Dmo.Infrastructure\Auth\SupabaseAuthAdapter.cs` |
| `SupabaseSettings` | static class | — (configuration) | env vars `BA_DMO_SUPABASE_*` | `src\BA.Dmo.Infrastructure\Auth\SupabaseSettings.cs` |
| `DapperModuleCatalogMirrorRepository` | sealed class | `IModuleCatalogMirrorRepository` | `module_catalog_mirror` | `src\BA.Dmo.Infrastructure\Access\DapperModuleCatalogMirrorRepository.cs` |

`DapperInternalUserRepository` specifics (verified SQL):
- `FindByAuthUserIdSql` (lines 16–32) LEFT JOINs the N27 junction; multi-actor duplicate rows raise `AmbiguousIdentityException` (HI-2, explicit count check); returns `InternalUserRecord.AccessTemplates` from the joined rows.
- `AdminExistsSql` (lines 34–45) joins `internal_users u` → junction → `access_templates t`, requiring `u.active`, `u.profile_title = 'Admin'`, `t.active`, `t.modules @> '[{"moduleId":"admin"}]'::jsonb` — junction + profile driven (bootstrap idempotency, GLM-ACC-13).
- `CreateBootstrapAdminAsync` writes template + internal_user + ONE junction row (`InsertUserTemplateSql`, `ON CONFLICT (actor_id, template_id) DO NOTHING`) + audit `bootstrap_admin` in one `DapperUnitOfWork` (lines 47–85, 166–211).

### B. External provider/framework dependencies (constructor/support)

- Dapper/Npgsql persistence foundation (`Db`, `DbConnectionFactory`, `IDbConnectionFactory`, `DatabaseConnectionSettings`, `DapperUnitOfWork`, `PersistenceMappings`) under `src\BA.Dmo.Infrastructure\Persistence\` — shared infra consumed by Users/Access repositories.
- `HttpClient` for Supabase auth REST calls.

### C. Admin-scoped adapters (external technical reference, not absorbed)

- `SupabaseAdminProvisioningAdapter` (`src\BA.Dmo.Infrastructure\Auth\SupabaseAdminProvisioningAdapter.cs`) implements the shared `IAdminProvisioningAdapter` port (defined in Application Shared Identity), but its operational scope is privileged provisioning (bootstrap-admin CLI + admin.gerir-gated user create/password-reset). It is classified as an external technical reference, not counted in the Users/Access-specific Infrastructure set.

## 12. Database Objects

Users/Access-specific tables and indexes (source: `database\migrations\N01_identity.sql`, `N02_catalog.sql`, `N25_remediation.sql` §1.1, `N26_user_modules_override.sql`, `N27_access_convergence.sql`, `N31_template_profiles_single_assignment.sql`); reflected identically in `database\consolidated_clean_install.sql` and the migration family in [03_MIGRATIONS.md](03_MIGRATIONS.md).

### Tables (5)

**`access_templates`** (N01)
- PK: `template_id` (text).
- Columns: `name` text NOT NULL, `modules` jsonb NOT NULL DEFAULT `'[]'` (module-only entries after N27), `active` boolean NOT NULL DEFAULT TRUE, `created_at_utc` timestamptz, `created_by` text, `updated_at_utc` timestamptz.
- N31 AFTER INSERT trigger: `trg_access_templates_ensure_profile` → `ba_dmo_ensure_access_template_profile()` (initial profile row).

**`internal_users`** (N01 + N25 + N26 + N27)
- PK: `actor_id` (text).
- FK: `template_id` text REFERENCES `access_templates (template_id)` NOT NULL (compatibility/authority pointer; N31 keeps it in sync with the single junction row).
- UNIQUE: `uq_internal_users_auth_user` UNIQUE (`auth_user_id`) — added by N25; `auth_user_id` set NOT NULL by N25.
- Columns: `auth_user_id` uuid NOT NULL (logical Supabase Auth link, no FK to `auth.users`), `display_name` text NOT NULL, `profile_title` text NOT NULL + `ck_internal_users_functional_profile` CHECK (`'Admin','Operador / Controlador','Responsável'`) — NOT NULL + closed CHECK by N27, `active` boolean NOT NULL DEFAULT TRUE, `created_at_utc`, `updated_at_utc`, `modules_override` jsonb NULL (N26 — DORMANT, set NULL for all rows by N27).

**`internal_user_access_templates`** (N27 + N31)
- PK: (`actor_id`, `template_id`); FKs to `internal_users (actor_id)` and `access_templates (template_id)`.
- Columns: `assigned_at_utc` timestamptz NOT NULL DEFAULT now(), `assigned_by` text NULL.
- Indexes: `ix_internal_user_access_templates_template` (template_id, actor_id) — N27; `ux_internal_user_access_templates_actor` UNIQUE (actor_id) — N31 (SINGLE template assignment at DB level).
- RLS enabled + policy `internal_user_access_templates_app_access FOR ALL TO ba_dmo_app` (N27); anon/authenticated revoked.

**`access_template_profiles`** (N31)
- PK: `template_id` text REFERENCES `access_templates (template_id)` ON DELETE CASCADE.
- Columns: `functional_profile` text NOT NULL, `updated_at_utc` timestamptz NOT NULL DEFAULT now().
- Constraint: `ck_access_template_profiles_functional_profile` CHECK (`functional_profile IN ('Admin','Operador / Controlador','Responsável')`).
- RLS enabled + policy `access_template_profiles_app_access FOR ALL TO ba_dmo_app` (N31); anon/authenticated revoked.

**`module_catalog_mirror`** (N02)
- PK: `module_id` (text).
- Columns: `display_name` text NOT NULL, `display_order` integer NOT NULL, `active` boolean NOT NULL DEFAULT TRUE, `synced_at_utc` timestamptz.

### Indexes (6)

| Index | Table | Columns |
|---|---|---|
| `ix_access_templates_active` | access_templates | (active) |
| `ix_internal_users_auth_user_id` | internal_users | (auth_user_id) |
| `ix_internal_users_active` | internal_users | (active) |
| `ix_internal_users_template_id` | internal_users | (template_id) |
| `ix_module_catalog_mirror_order` | module_catalog_mirror | (display_order) |
| `ix_internal_user_access_templates_template` | internal_user_access_templates | (template_id, actor_id) |

Plus partial/unique indexes: `uq_internal_users_auth_user` (UNIQUE constraint, N25) and `ux_internal_user_access_templates_actor` (UNIQUE index, N31).

### Triggers (1 access-specific + 1 shared)

- `trg_access_templates_ensure_profile` (N31) — AFTER INSERT ON `access_templates` FOR EACH ROW → `ba_dmo_ensure_access_template_profile()` (creates the template's `access_template_profiles` row deterministically, `ON CONFLICT DO NOTHING`).
- `trg_audit_events_append_only` (N01) applies to `audit_events` — shared/global object, not a Users/Access table.

### Constraints (separate)

- PKs: `access_templates.template_id`, `internal_users.actor_id`, `internal_user_access_templates (actor_id, template_id)`, `access_template_profiles.template_id`, `module_catalog_mirror.module_id`.
- FKs: `internal_users.template_id → access_templates`; junction `actor_id → internal_users` + `template_id → access_templates`; `access_template_profiles.template_id → access_templates ON DELETE CASCADE`.
- UNIQUE: `uq_internal_users_auth_user (auth_user_id)` (N25); `ux_internal_user_access_templates_actor` (N31) — single assignment.
- CHECK: `ck_internal_users_functional_profile` (N27), `ck_access_template_profiles_functional_profile` (N31).

### Shared / external DB dependencies (not counted)

- `audit_events` — shared global append-only audit table; written by `DapperInternalUserRepository` bootstrap and consumed by Admin/História. Not Users/Access-specific.
- `app_settings` (N11) — shared key/value settings (`setting_key` PK, `setting_value` jsonb, `updated_at_utc`, `updated_by` FK `internal_users (actor_id)`); read via `DapperAppSettingsReader` (Pegamentos settings) — see [02_DATABASE.md](02_DATABASE.md), not an access table.
- `auth.users` — external Supabase Auth table; referenced logically by `internal_users.auth_user_id` (no local FK). Not present in this repository's schema.

Counts: DB objects = 5 tables + 6 indexes + 1 access trigger = **12** (constraints separated).

## 13. Migration Touchpoints

Distinct migrations that directly introduce/change Users/Access-specific DB objects:

| Migration | Users/Access scope |
|---|---|
| `N01_identity.sql` | creates `access_templates` + `ix_access_templates_active`; creates `internal_users` + indexes; `audit_events` (shared); roles `ba_dmo_app`/`ba_dmo_migrate`; shared `ba_dmo_guard_append_only()` function. |
| `N02_catalog.sql` | creates `module_catalog_mirror` + `ix_module_catalog_mirror_order`. |
| `N11_partilhado.sql` | creates `app_settings` (shared; referenced by `updated_by` FK). |
| `N12_rls.sql` | RLS + `ba_dmo_app_access` policy on `internal_users`, `access_templates`, `audit_events`, `module_catalog_mirror`, `app_settings` (global security contract). |
| `N25_remediation.sql` | §1.1: `internal_users.auth_user_id` set NOT NULL + `uq_internal_users_auth_user` UNIQUE. (Remainder of N25 touches job_on/bq/pegamento/repair/peso/audit — not Users/Access-specific.) |
| `N26_user_modules_override.sql` | adds `internal_users.modules_override` jsonb column — DORMANT since N27 (legacy path; no resolver read). |
| `N27_access_convergence.sql` | creates `internal_user_access_templates` (junction) + `ix_internal_user_access_templates_template`; infers and enforces `profile_title` NOT NULL + closed CHECK; creates `legacy-override-*` compatibility templates from former overrides; converts template grants to module-only; sets `modules_override = NULL`; RLS + junction policy. |
| `N31_template_profiles_single_assignment.sql` | creates `access_template_profiles` + `ck_access_template_profiles_functional_profile`; function `ba_dmo_ensure_access_template_profile` + trigger `trg_access_templates_ensure_profile`; profile backfill from user data; collapses multi-template assignments to one row per user + `ux_internal_user_access_templates_actor`; syncs `internal_users.profile_title`; RLS + policy. |

`N29_jobon_reference_images.sql` / `N30_jobon_reference_image_updated_by_index.sql` touch Job On reference images only — no Users/Access RLS role. (The RLS for the N27/N31 tables is self-contained in those migrations, not in N25/N29.)

Distinct Users/Access migration files: **7** (N01, N02, N25 §1.1, N26, N27, N31 + N12 shared RLS coverage; N11 `app_settings` shared/external).

## 14. User Surface

**User Surface: None / No dedicated rendered surface.**

There is no dedicated rendered page/route/UI for "Users / Access" in current source (`src\BA.Dmo.Web\Pages\` has no `Users` or `Access` folder). The shared surfaces present are shell/auth surfaces — `AccessDenied`, `NoAccess`, and the `Index` landing — plus the derived shell navigation (`_Navigation.cshtml`), which are shared shell/auth, not a Users/Access surface. The Admin pages under `Pages\Admin\Users` belong to the Admin module and are not reused as a Users/Access surface. The Login surface under `Pages\Auth` is separate ([18_LOGIN.md](18_LOGIN.md)). User Surface source-verified: YES.

## 15. Web / Routes

Dedicated Users / Access routes: **0**.

Shared authorization/web wiring present in `src\BA.Dmo.Web\Program.cs` (current line numbers):

- Session cookie authentication scheme `BaDmo.Session` (`SessionClaims.AuthenticationScheme`) with cookie options (lines 83–95): `LoginPath = "/login"`, `LogoutPath = "/logout"`, `AccessDeniedPath = "/access-denied"`, `SlidingExpiration = true`, `ExpireTimeSpan = TimeSpan.FromHours(8)`, `Cookie.HttpOnly = true`, `Cookie.SameSite = SameSiteMode.Lax`, `Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest`.
- Fallback authorization policy requiring `AuthenticatedSessionRequirement` (lines 98–101); Admin policies (105–113); per-module/per-capability policies from the canonical catalog (118–131).
- DI (current lines): `<ICurrentUserAccessor>` scoped `RequestCurrentUserAccessor` (155); `IPersistenceAuthorshipAccessor` scoped `CurrentUserAuthorshipAccessor` (156); `IdentityResolutionService` scoped (154); `INavigationService` singleton (`NavigationService`, 150–151); `IShellService` scoped (`RequestShellService`, 152); `ISupabaseAuthAdapter` singleton (`SupabaseAuthAdapter`, 157–160); `IAdminProvisioningAdapter` singleton (`SupabaseAdminProvisioningAdapter`, 167–171); `IModuleCatalogMirrorRepository` singleton (`DapperModuleCatalogMirrorRepository`, 173); `AccessResolver` singleton (141–145); `IInternalUserRepository` singleton (`DapperInternalUserRepository`, 140); `IClock` singleton (137); `GrantNormalizer` scoped (189–190); `ModuleCatalog` singleton (`CanonicalModuleCatalog.Instance`, 183).

The `/login`, `/logout`, `/access-denied`, `/no-access` and `/` routes are shell/auth surfaces belonging to LOGIN/shell units; `/admin/*` belongs to [15_ADMIN.md](15_ADMIN.md). No Users/Access route is registered.

## 16. Static Assets

Dedicated Users/Access static assets: **0**.

The global stylesheet set (`wwwroot\styles\dmo-*.css`) and shared scripts under `wwwroot\scripts\` are shared shell/assets for all modules. No static asset carries an access-condition selector specific to Users/Access (grep for capability/access-condition selectors returned no Users/Access match).

## 17. Tests

Test files live under `AI-CONTEXT\docs\tests\` (there is no `tests\` directory at the repository root).

### Unit tests — Shared Access (10)

`AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Access\`: `AccessResolverTests.cs`, `CanonicalModuleCatalogTests.cs`, `CanonicalPageCatalogTests.cs`, `CapabilityAndModuleDefinitionTests.cs`, `CatalogValidatorTests.cs`, `CurrentUserTests.cs`, `GrantNormalizerTests.cs`, `ModuleCatalogMirrorSynchronizerTests.cs`, `ModuleCatalogTests.cs`, `NavigationServiceTests.cs`.

### Unit tests — Shared Identity (3)

`AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Identity\`: `AccessTemplateGrantsParserTests.cs`, `IdentityResolutionServiceTests.cs`, `BootstrapAdminServiceTests.cs`.

`IdentityResolutionServiceTests.cs` (current single-template model): valid active user+template resolves; landing Job On; inactive user/template denied; malformed grants fail closed; **multiple assigned ACTIVE templates fail closed as `ACCESS_TEMPLATE_AMBIGUOUS`**; unknown profile → `FUNCTIONAL_PROFILE_INVALID`; invalid grant entries discarded (profile-derived capabilities, not stored arrays); Admin grants land on `/admin` without jobon.view; **ModulesOverride dormant**; template names never grant; repository failure → BackendUnavailable; `AmbiguousIdentityException` → `IDENTITY_AMBIGUOUS` (Unauthorized, not BackendUnavailable).

### Integration tests — shared Access/Identity (7)

| Class | File | Target |
|---|---|---|
| `CatalogCompositionGuardTests` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\CatalogCompositionGuardTests.cs` | canonical catalog composition |
| `ShellRoutingTests` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\ShellRoutingTests.cs` | shell routing / shared access navigation |
| `IdentitySecurityGuardTests` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Identity\IdentitySecurityGuardTests.cs` | session/identity security contract |
| `IdentityAmbiguityLandingTests` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Identity\IdentityAmbiguityLandingTests.cs` | ambiguous identity landing |
| `WebAuthSessionTests` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Identity\WebAuthSessionTests.cs` | session authentication |
| `SupabaseAuthAdapterTests` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Identity\SupabaseAuthAdapterTests.cs` | shared auth adapter |
| `SupabaseAdminProvisioningAdapterTests` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Identity\SupabaseAdminProvisioningAdapterTests.cs` | shared provisioning port impl |

Not absorbed (direct target is Admin or a functional module, not shared Access/Identity): `AdminSecurityGuardTests`, `AdminFormAntiforgeryTests`, `AdminWebAuthorizationTests`, `DapperAdminRepositoryProjectionTests`, `AdminUserListResetTests` (Admin); `HistoriaWebAuthorizationTests`, `BoquilhasWebAuthorizationTests` (module consumers). Login page tests (if any) not counted.

Test classes directly targeting shared Access/Identity: **20** (10 + 3 + 7).

## 18. Test Doubles / Helpers

### Dedicated test support files (1)

- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Identity\FakeHttpMessageHandler.cs` — shared HTTP message handler fake for Supabase auth adapter tests.

(`AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\FakeBoquilhasWebRepository.cs` targets the Boquilhas module and is not shared Access/Identity support.)

### In-file fixture files (5)

Files containing embedded fakes/doubles whose direct target is shared Access/Identity:

| File | Embedded doubles |
|---|---|
| `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Identity\IdentityResolutionServiceTests.cs` | `FakeInternalUserRepository` |
| `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Identity\BootstrapAdminServiceTests.cs` | `FakeProvisioningAdapter`, `FakeInternalUserRepository` |
| `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\ShellRoutingTests.cs` | `FakeAuthAdapter`, `FakeIdentityRepository`, module fakes |
| `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Identity\WebAuthSessionTests.cs` | `FakeAuthAdapter`, `FakeIdentityRepository` |
| `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Identity\IdentityAmbiguityLandingTests.cs` | `FakeAuthAdapter`, `FakeIdentityRepository` |

In-file fixture count counts files: **5**.

## 19. Direct Users / Access References

One edge per source-proven relationship:

- `AccessResolver` → `ModuleCatalog`, `PageCatalog`, `GrantNormalizer`
- `AccessResolver` → `CanonicalModuleCatalog` (ids), `CanonicalPageCatalog` (ids), `FunctionalProfile` (capability projection)
- `GrantNormalizer` → `ModuleCatalog`
- `IdentityResolutionService` → `IInternalUserRepository` (junction-aware record `AccessTemplates`)
- `IdentityResolutionService` → `AccessResolver`, `AccessTemplateGrantsParser`, `AccessTemplateDefinition`, `CurrentUser`, `FunctionalProfileNames`
- `CurrentUserAuthorshipAccessor` → `ICurrentUserAccessor`, `IdentityResolutionService`, `IPersistenceAuthorshipAccessor`
- `RequestCurrentUserAccessor` → `ICurrentUserAccessor`, `IdentityResolutionService`, `SessionClaims`
- `RequestShellService` → `IdentityResolutionService`, `INavigationService`, `IShellService`
- `NavigationService` → `AccessResolver`, `PageCatalog`, `ModuleCatalog`
- `ModuleAuthorizationHandler` → `ICurrentUserAccessor`, `ModuleRequirement`
- `CapabilityAuthorizationHandler` → `ICurrentUserAccessor`, `CapabilityRequirement`
- `ModuleCatalogMirrorSynchronizer` → `ModuleCatalog`
- `DapperInternalUserRepository` → `internal_users`, `internal_user_access_templates`, `access_templates`, `audit_events`
- `DapperModuleCatalogMirrorRepository` → `module_catalog_mirror`
- `TemplateProfileStore` (Web, MAP-15) → `access_template_profiles`, `internal_users` (profile sync)
- `AdminTemplateService`/`AdminUserService` → `FunctionalProfileNames` (profile validation)
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
| `ISupabaseAuthAdapter` | `<Login>` sign-in use (see [18_LOGIN.md](18_LOGIN.md)) | Login consumer |
| Dapper/Npgsql `IDbConnectionFactory`, `Db`, `DbConnectionFactory`, `DatabaseConnectionSettings`, `DapperUnitOfWork`, `PersistenceMappings` | `DapperInternalUserRepository`, `DapperModuleCatalogMirrorRepository` | framework dependency |
| `JobonModuleCatalog` | `CanonicalModuleCatalog` | shared catalog consumer |
| `CanonicalModuleCatalog.AreaChildren` | `CatalogValidator`, `AccessResolver`, `NavigationService` | shared catalog consumer |

## 21. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `CurrentUser`, `Capability`, `ModuleCatalog`, `ModuleDefinition`, `ModuleKind`, `JobonModuleCatalog`, `FunctionalProfile`/`FunctionalProfileNames` | Domain | `src\BA.Dmo.Domain\Shared\Access\` |
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
| `IInternalUserRepository`, `InternalUserRecord`, `InternalUserAccessTemplateRecord`, `BootstrapAdminCreation` | Application Identity | `src\BA.Dmo.Application\Shared\Identity\IInternalUserRepository.cs` |
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
| `TemplateProfileStore` (N31 profile read/upsert) | Web Admin | `src\BA.Dmo.Web\Pages\Admin\TemplateProfileStore.cs` |
| Composition root / policy + DI wiring | Web | `src\BA.Dmo.Web\Program.cs` |
| `BootstrapAdminCommand`, `CliMode`, `CliModeResolver` | Web CLI | `src\BA.Dmo.Web\Cli\` |
| `access_templates`, `internal_users`, `internal_user_access_templates`, `access_template_profiles`, `module_catalog_mirror` | Database | `database\migrations\N01/N02/N25/N26/N27/N31*.sql` |

## 22. Sources Verified

- `src\BA.Dmo.Domain\Shared\Access\` (CurrentUser, Capability, ModuleCatalog, ModuleDefinition, ModuleKind, ICurrentUserAccessor, JobonModuleCatalog, FunctionalProfile)
- `src\BA.Dmo.Domain\Shared\Kernel\` (DomainError)
- `src\BA.Dmo.Application\Shared\Access\` (All 10 files)
- `src\BA.Dmo.Application\Shared\Identity\` (All 6 files)
- `src\BA.Dmo.Application\Shared\Persistence\PersistenceAuthorship.cs`
- `src\BA.Dmo.Application\Shared\Shell\IShellService.cs`
- `src\BA.Dmo.Application\Modules\Admin\` (AdminUserService.cs — profile/template validation; AdminTemplateService.cs — module-only grants; AdminModels.cs)
- `src\BA.Dmo.Application\Modules\Historia\HistoriaModuleCatalog.cs` (module catalog location note)
- `src\BA.Dmo.Infrastructure\Identity\DapperInternalUserRepository.cs` (junction join SQL)
- `src\BA.Dmo.Infrastructure\Auth\` (SupabaseAuthAdapter, SupabaseSettings, SupabaseAdminProvisioningAdapter)
- `src\BA.Dmo.Infrastructure\Access\DapperModuleCatalogMirrorRepository.cs`
- `src\BA.Dmo.Web\Program.cs`
- `src\BA.Dmo.Web\Authorization\` (3 files)
- `src\BA.Dmo.Web\Identity\` (3 files)
- `src\BA.Dmo.Web\Shell\RequestShellService.cs`
- `src\BA.Dmo.Web\Pages\Shared\_Layout.cshtml`, `_Header.cshtml`, `_Navigation.cshtml`, `_AdminNav.cshtml`
- `src\BA.Dmo.Web\Pages\Admin\TemplateProfileStore.cs`
- `src\BA.Dmo.Web\Cli\BootstrapAdminCommand.cs`
- `src\BA.Dmo.Web\Pages\` (confirmed no dedicated Users/Access surface; shared shell/auth pages present)
- `database\migrations\N01_identity.sql`, `N02_catalog.sql`, `N11_partilhado.sql`, `N12_rls.sql`, `N25_remediation.sql`, `N26_user_modules_override.sql`, `N27_access_convergence.sql`, `N31_template_profiles_single_assignment.sql`
- `database\consolidated_clean_install.sql`
- `src\BA.Dmo.Web\wwwroot\` (styles/scripts scanned for access-condition selectors)
- `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Access\` (10 files), `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\Shared\Identity\` (3 files)
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\` (classification), `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Identity\` (all files)
- [00_INDEX.md](00_INDEX.md), [01_DOMAIN.md](01_DOMAIN.md), [02_DATABASE.md](02_DATABASE.md), [03_MIGRATIONS.md](03_MIGRATIONS.md), [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md), [05_TESTS.md](05_TESTS.md), [19_APPLICATION.md](19_APPLICATION.md), [20_WEB.md](20_WEB.md) (cross-map navigation)

## Counts

- Domain shared Access files: **8** (added `FunctionalProfile.cs`)
- Application shared Access files: **10**
- Application shared Identity files: **6**
- Infrastructure Users/Access files: **4**
- Shared / external infrastructure dependencies: **2** (SupabaseAdminProvisioningAdapter external; Dapper/Npgsql persistence foundation)
- Dedicated Web page files: **0**
- Dedicated Users/Access routes: **0**
- Dedicated static asset files: **0**
- Shared web wiring files: **8** (Program.cs + 3 Authorization + 3 Identity + RequestShellService)
- Shared static asset files: **0**
- Users/Access-specific DB tables: **5** (`internal_users`, `access_templates`, `internal_user_access_templates`, `access_template_profiles`, `module_catalog_mirror`)
- Users/Access-specific DB indexes: **6**
- Users/Access-specific DB triggers: **1** (`trg_access_templates_ensure_profile`; shared `trg_audit_events_append_only` excluded)
- Users/Access-specific DB objects: **12** (5 tables + 6 indexes + 1 trigger)
- Shared / external DB dependencies: **3** (shared local `audit_events` + `app_settings`; external Supabase `auth.users` — no local FK)
- Distinct Users/Access migration files: **7** (N01, N02, N25 §1.1, N26, N27, N31 + N12 shared RLS; N11 `app_settings` shared/external)
- Canonical module IDs: **12**
- Canonical capability IDs: **14**
- Canonical page IDs: **13** (added `controlo.resumo`)
- Test classes: **20**
- Dedicated test support files: **1**
- In-file test fixture files: **5**
- Source-visible user surfaces: **0**