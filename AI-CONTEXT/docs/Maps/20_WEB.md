# BA DMO — Web Technical Map

MAP ID: MAP-20
Status: COMPLETE (refreshed to HEAD 8478308 "Render one persistent Admin navigation")

> Refresh note: this revision was reconciled directly against current Web source
> (`src\BA.Dmo.Web\`) at HEAD 8478308 (branch `main`). Navigation changed in
> commits `1f91dfe..8478308` (persistent Admin tabs, single navigation layer in
> Admin, layout-rendered `_AdminNav` + `BA_DMO_ADMIN_NAV_RENDERED` request
> marker). Stale counts and route claims were corrected from source; findings are
> listed in §14 with evidence.

## Navigation Index

- [1. Scope](#1-scope)
- [2. Project / Folder Structure](#2-project--folder-structure)
- [3. Web Inventory](#3-web-inventory)
- [4. Razor Pages](#4-razor-pages)
- [5. PageModels](#5-pagemodels)
- [6. Routes / Endpoints](#6-routes--endpoints)
- [7. Authorization / Identity / Session Web Objects](#7-authorization--identity--session-web-objects)
- [8. Shared Shell / Navigation (three authorities)](#8-shared-shell--navigation)
- [9. Static Assets](#9-static-assets)
- [10. Module-Specific Web Areas](#10-module-specific-web-areas)
- [11. Direct Web References](#11-direct-web-references)
- [12. Target-to-Location Index](#12-target-to-location-index)
- [13. Sources Verified](#13-sources-verified)
- [14. Classification Findings (NEEDS REVIEW etc.)](#14-classification-findings)

## Counts

## 1. Scope

Pure transversal technical inventory/navigation of the **Web layer** (`src\BA.Dmo.Web\`). This map catalogues what Web source declares and where: Razor Pages, PageModels, API/endpoints, route declarations, the shared shell, navigation components, Web authorization handlers, Web identity/session support, Web services/composition wiring (`Program.cs`), static assets, JS/CSS ownership, exact source locations, and direct Web references.

Rules respected:

- It does **not** explain end-to-end user workflows.
- It does **not** duplicate Domain/Application/Infrastructure/Database detail (each mapped in its own transversal map: [19_APPLICATION.md](19_APPLICATION.md), [02_DATABASE.md](02_DATABASE.md), [04_DAPPER_INFRASTRUCTURE.md](04_DAPPER_INFRASTRUCTURE.md), [03_MIGRATIONS.md](03_MIGRATIONS.md)).
- It does **not** assign functional ownership beyond technical source structure; module-specific surfaces are classified by the source folder they live under, and shared design-system assets are classified as Shared.

`bin\` and `obj\` are build output and excluded. Only current source is mapped; no count is invented.

## 2. Project / Folder Structure

Project: `src\BA.Dmo.Web\BA.Dmo.Web.csproj` — `Microsoft.NET.Sdk.Web` Razor Pages application. It references **`BA.Dmo.Application`** and **`BA.Dmo.Infrastructure`**. It also ships `database\migrations\**\*.sql` as content (linked `database\migrations\...`, `CopyToOutputDirectory=PreserveNewest`) for the CLI migrate verb (GLM-ARCH-15; no separate CLI project — verbs are process-argument driven).

```
src\BA.Dmo.Web\
├─ Program.cs                     ← composition root: services, policies, DI, 125 API endpoints, CLI dispatch
├─ Authorization\                 (3) — AuthenticatedSession/Module/Capability handlers + policy constants
├─ Identity\                      (3) — SessionClaims, RequestCurrentUserAccessor, CurrentUserAuthorshipAccessor
├─ Shell\                         (1) — RequestShellService
├─ Cli\                           (4) — CliMode, CliModeResolver, MigrateCommand, BootstrapAdminCommand
├─ Pages\
│  ├─_ViewImports.cshtml, _ViewStart.cshtml
│  ├─ Index.cshtml / Index.cshtml.cs        (root redirect endpoint)
│  ├─ AccessDenied.cshtml / .cs             (shared auth safe-state)
│  ├─ NoAccess.cshtml / .cs                 (shared auth safe-state)
│  ├─ Shared\                               (4) _Layout, _Header, _Navigation, _AdminNav
│  ├─ Auth\                                 (2) Login, Logout
│  ├─ Admin\                                (17) Index + 4 areas (Users/Templates/Applications/Audit) + TemplateProfileStore.cs
│  ├─ JobOn\                                (Index page + JobOnLineColor.cs)
│  ├─ Peso\                                 (Index, Responsavel)
│  ├─ Pegamentos\                           (Index, Detail)
│  ├─ Ferramentas\                          (Index, Criar, Ficha + _ReferenceList partial)
│  ├─ Armazem\                              (Index)
│  ├─ ReparacaoExterna\                     (Index + _RepairListBuilder partial + ListBuilderModel.cs)
│  ├─ ReparacaoInterna\                     (Index)
│  ├─ Controlo\                             (Index)
│  ├─ Tampoes\                              (Index)
│  ├─ Historia\                             (Index)
│  ├─ Boquilhas\                            (Index)
│  └─ DesignLaboratorio\                    (Index)
└─ wwwroot\
   ├─ styles\                  (5 global dmo-*.css + 10 module CSS under styles\modules\)
   ├─ scripts\                 (12 JS files)
   └─ assets\                  (1 asset: ba-logo.png)
```

**Source-file counts (Web root, excluding `bin\`/`obj\`/`wwwroot` static binaries):** pure `.cs` (composition root + Authorization + Identity + Shell + Cli) = **12**; non-page Web classes = **3** (`Pages\JobOn\JobOnLineColor.cs`, `Pages\ReparacaoExterna\ReparacaoExternaListBuilderModel.cs`, `Pages\Admin\TemplateProfileStore.cs`); PageModel `.cshtml.cs` = **29**; `.cshtml` = **37** (incl. `_ViewImports`/`_ViewStart`). Total source `.cs` + `.cshtml` = **15 + 29 + 37 = 81**. `.json` = 3 (appsettings ×2 + `Properties\launchSettings.json`). Under `wwwroot\`: 15 `.css`, 12 `.js`, 1 `.png`. Full breakdowns in §3.

## 3. Web Inventory

### 3.1 Web source files by kind (exact)

| Kind | Count | Path (under `src\BA.Dmo.Web\`) |
|---|---|---|
| Composition root | 1 | `Program.cs` |
| Razor Pages (`.cshtml`) | 37 | `Pages\**\*.cshtml` (incl. `_ViewImports`, `_ViewStart`) |
| Razor PageModels / code-behind (`.cshtml.cs`) | 29 | `Pages\**\*.cshtml.cs` |
| Non-page Web classes | 3 | `Pages\JobOn\JobOnLineColor.cs`, `Pages\ReparacaoExterna\ReparacaoExternaListBuilderModel.cs`, `Pages\Admin\TemplateProfileStore.cs` |
| Authorization handlers | 3 | `Authorization\*Handler.cs` |
| Web identity/session | 3 | `Identity\` (SessionClaims, RequestCurrentUserAccessor, CurrentUserAuthorshipAccessor) |
| Shell service | 1 | `Shell\RequestShellService.cs` |
| CLI verbs | 4 | `Cli\` |
| Config/support | 3 | `.json` (settings: appsettings, appsettings.Development, launchSettings) |

### 3.2 Razor Pages by folder (source-grounded)

| Folder | `.cshtml` | `.cshtml.cs` (PageModels) | Route surface |
|---|---|---|---|
| `Pages\` (root) | Index, AccessDenied, NoAccess | Index.cs, AccessDenied.cs, NoAccess.cs | `/`, `/access-denied`, `/no-access` |
| `Pages\Shared\` | _Layout, _Header, _Navigation, _AdminNav | — (partials/layout) | shared shell |
| `Pages\Auth\` | Login, Logout | Login.cs, Logout.cs | `/login`, `/logout` |
| `Pages\Admin\` | 8 (Index; Users Create/Edit/Index; Templates Edit/Index; Applications Index; Audit Index) | 8 | `/admin`, `/admin/users`, `/admin/users/create`, `/admin/users/edit`, `/admin/templates`, `/admin/templates/edit`, `/admin/applications`, `/admin/audit` (+ `TemplateProfileStore.cs` non-page class) |
| `Pages\JobOn\` | Index | Index.cs | `/jobon` (+ `JobOnLineColor.cs`) |
| `Pages\Peso\` | Index, Responsavel | Index.cs, Responsavel.cs | `/peso`, `/peso/responsavel` |
| `Pages\Pegamentos\` | Index, Detail | Index.cs, Detail.cs | `/pegamentos`, `/pegamentos/{id:guid}` |
| `Pages\Ferramentas\` | Index, Criar, Ficha, _ReferenceList | Index.cs, Criar.cs, Ficha.cs | `/ferramentas`, `/ferramentas/criar`, `/ferramentas/{id:guid}` |
| `Pages\Armazem\` | Index | Index.cs | `/armazem` |
| `Pages\ReparacaoExterna\` | Index, _RepairListBuilder | Index.cs (+ `ReparacaoExternaListBuilderModel.cs`) | `/reparacao-externa` |
| `Pages\ReparacaoInterna\` | Index | Index.cs | `/reparacao-interna` |
| `Pages\Controlo\` | Index | Index.cs | `/controlo` |
| `Pages\Tampoes\` | Index | Index.cs | `/tampoes` |
| `Pages\Historia\` | Index | Index.cs | `/historia` |
| `Pages\Boquilhas\` | Index | Index.cs | `/boquilhas` |
| `Pages\DesignLaboratorio\` | Index | Index.cs | `/design-laboratorio` |

## 4. Razor Pages

Navigation-level inventory of the Razor page surface by ownership class:

| Class | Pages | Path |
|---|---|---|
| Shared shell | `_Layout`, `_Header`, `_Navigation`, `_AdminNav` partials | `Pages\Shared\` |
| Shared auth/shell safe-states | Index (root redirect), AccessDenied, NoAccess | `Pages\Index.*`, `Pages\AccessDenied.*`, `Pages\NoAccess.*` |
| Login pages (Login surface) | Login, Logout | `Pages\Auth\` |
| Design Laboratório page | Index | `Pages\DesignLaboratorio\` |
| Admin pages | Index + Users/Templates/Applications/Audit (8 pages) | `Pages\Admin\` |
| Module-specific canonical pages | JobOn, Peso(2), Pegamentos(2), Ferramentas(3 + partial), Armazem, ReparacaoExterna(+ partial), ReparacaoInterna, Controlo, Tampoes, Historia, Boquilhas | `Pages\<Module>\` |

Razor Page routes are declared via explicit `@page "<route>"` directives in each `.cshtml` (source-verified; map §6.1). Page authorization is declared as `@attribute [Microsoft.AspNetCore.Authorization.Authorize(Policy = …)]` **inside the `.cshtml` file** — not on the PageModel code-behind — plus a global fallback policy; see §6.1/§7.

Layout contract: `_ViewStart.cshtml` sets `Layout = "_Layout"` for the whole application (GLM-SHL-01). Five pages opt out with `Layout = null`: `Index.cshtml` (pure redirect), `AccessDenied.cshtml`, `NoAccess.cshtml` (safe states, GLM-SHL-06), `Auth\Login.cshtml`, `Auth\Logout.cshtml` (auth surface outside the shell, 05_SHL §5).

## 5. PageModels

`IndexModel : PageModel` style code-behind classes (`.cshtml.cs`), 29 total, one per PageModel. Representative surfaces and their constructor/service dependencies (full method detail per module map):

| Module | PageModels | Key injected services / fields exposed |
|---|---|---|
| Job On | `IndexModel` | `ICurrentUserAccessor`, `IJobOnRepository`, `JobOnService?`; `CanEdit/CanConfigure/CanConfirm` (capability flags), `CanViewControlo`/`CanViewRepairs` (module flags); `OnGetAsync` |
| Peso | `IndexModel`, `ResponsavelModel` | `ICurrentUserAccessor`, `PesoService`; `OnGet` redirects `/peso`↔`/peso/responsavel` by `peso.aprovar` capability; page-local gating flags |
| Pegamentos | `IndexModel`, `DetailModel` | `IndexModel(ICurrentUserAccessor)` (JS-driven surface over `/api/pegamentos/*`); `DetailModel` no ctor |
| Ferramentas | `IndexModel`, `CriarModel`, `FichaModel` (+ `_ReferenceList` partial) | `ICurrentUserAccessor`; `CanConfigure` (`ferramentas.configure`); `CriarModel` no ctor |
| Armazem | `IndexModel` | `ICurrentUserAccessor`; `CanCreateNewTool` (`ferramentas` module flag) |
| ReparacaoExterna | `IndexModel` (+ `ReparacaoExternaListBuilderModel` record) | `IndexModel` no ctor; partial view-model carries `Title` + `RepairType` only (behavior via `/api/reparacao-externa/*`) |
| ReparacaoInterna | `IndexModel` | `ICurrentUserAccessor`; `CanCorrigir` (`reparacao_interna.corrigir`) |
| Controlo | `IndexModel` | `ICurrentUserAccessor`; `CanEdit/CanSubmit/CanReview` (`controlo.*` capabilities) |
| Tampoes | `IndexModel` | no ctor (JS surface over `/api/tampoes/*`) |
| Historia | `IndexModel` | `HistoriaService`; history server-rendered query surface |
| Boquilhas | `IndexModel` | no ctor (JS surface over `/api/boquilhas/*`) |
| Admin | 8 PageModels (Index; Users Create/Edit/Index; Templates Edit/Index; Applications Index; Audit Index) | `AdminUserService`, `AdminTemplateService`, `AdminMirrorService`, `AdminAuditService` + `TemplateProfileStore` (constructed per page from `IDbConnectionFactory`, see below) |
| Auth | `LoginModel`, `LogoutModel` | `ISupabaseAuthAdapter`, `IdentityResolutionService`, `ILogger<LoginModel>?`; post-login redirect to `resolution.Value.FirstPage.Page.Route`; `/no-access` safe states |
| Shared auth/shell | `IndexModel` (`IdentityResolutionService`), `AccessDeniedModel` (`IdentityResolutionService`), `NoAccessModel` (no ctor) | root redirect `/` → FirstPage or `/no-access`; denied deep-link → FirstPage + `?acesso-negado=1` |
| Design Laboratório | `IndexModel` | no injected services |

`Pages\Admin\TemplateProfileStore.cs` (non-page Web class, introduced with N31): persistence helper for the **template-owned functional profile** (`access_template_profiles` table). `ListAsync`/`GetAsync`/`UpsertAsync` query via `IDbConnectionFactory`; on `DatabaseConnectionException` falls back to an in-memory dictionary (`tpl-admin`→Admin, `tpl-op`/`tpl-operator`→Operador / Controlador, `tpl-responsible`→Responsável) only for test hosts without a database — a reachable DB with a missing/invalid N31 still fails (cannot mask deployment drift). Consumed by `Admin\Users\Create`, `Admin\Users\Edit`, `Admin\Templates\Index`, `Admin\Templates\Edit` (each constructs it from `IDbConnectionFactory`).

## 6. Routes / Endpoints

### 6.1 Razor Page routes (source-grounded via `@page` + `@attribute [Authorize(Policy = …)]` in the `.cshtml`; fallback policy otherwise)

| Route | Kind | File | Authorization / metadata |
|---|---|---|---|
| `/` | Razor page | `Pages\Index.cshtml` | no `@attribute` → **fallback policy** `AuthenticatedSessionRequirement`; redirects to resolved FirstPage (`/jobon` for functional users, `/admin` for administrators) or `/no-access` |
| `/access-denied` | Razor page | `Pages\AccessDenied.cshtml` | `[AllowAnonymous]` (page `@attribute`); cookie `AccessDeniedPath`; redirects onward to FirstPage + `?acesso-negado=1` |
| `/no-access` | Razor page | `Pages\NoAccess.cshtml` | `[AllowAnonymous]` (page `@attribute` + code-behind) — shared auth safe-state |
| `/login`, `/logout` | Razor page | `Pages\Auth\Login.cshtml`, `Logout.cshtml` | `[AllowAnonymous]` (page + code-behind); `LoginPath`/`LogoutPath` |
| `/jobon` | Razor page | `Pages\JobOn\Index.cshtml` | `CapabilityPolicies.JobonView` |
| `/controlo` | Razor page | `Pages\Controlo\Index.cshtml` | `ModulePolicies.Controlo` (`BaDmo.Module.controlo` — Controlo is a top-level module **area** whose children are Peso + Pegamentos via `CanonicalModuleCatalog.AreaChildren`; see §14 finding) |
| `/peso`, `/peso/responsavel` | Razor page | `Pages\Peso\Index.cshtml`, `Responsavel.cshtml` | `ModulePolicies.Peso` (both); `peso.aprovar` capability gates approve/reject operations server-side via `PesoAuthorizationGate` and swaps the two pages via redirect |
| `/pegamentos`, `/pegamentos/{id:guid}` | Razor page | `Pages\Pegamentos\Index.cshtml`, `Detail.cshtml` | `ModulePolicies.Pegamentos` (both) |
| `/ferramentas`, `/ferramentas/criar`, `/ferramentas/{id:guid}` | Razor page | `Pages\Ferramentas\*.cshtml` | `ModulePolicies.Ferramentas` (all three) |
| `/armazem` | Razor page | `Pages\Armazem\Index.cshtml` | `ModulePolicies.Armazem` |
| `/reparacao-externa` | Razor page | `Pages\ReparacaoExterna\Index.cshtml` | `ModulePolicies.ReparacaoExterna` |
| `/reparacao-interna` | Razor page | `Pages\ReparacaoInterna\Index.cshtml` | `ModulePolicies.ReparacaoInterna` |
| `/tampoes` | Razor page | `Pages\Tampoes\Index.cshtml` | `ModulePolicies.Tampoes` |
| `/historia` | Razor page | `Pages\Historia\Index.cshtml` | `ModulePolicies.Historia` |
| `/boquilhas` | Razor page | `Pages\Boquilhas\Index.cshtml` | `ModulePolicies.Boquilhas` |
| `/design-laboratorio` | Razor page | `Pages\DesignLaboratorio\Index.cshtml` | no `@attribute` → **fallback** `AuthenticatedSessionRequirement` (no module/capability policy) |
| `/admin` | Razor page | `Pages\Admin\Index.cshtml` | `AdminPolicies.AdminGerir` |
| `/admin/users` | Razor page | `Pages\Admin\Users\Index.cshtml` | `AdminPolicies.AdminGerir`; handler POST `OnPostResetPasswordAsync(id)` |
| `/admin/users/create` | Razor page | `Pages\Admin\Users\Create.cshtml` | `AdminPolicies.AdminGerir`; `OnPostAsync` (email/password/displayName/**templateId**/active; legacy `templateIds[]` alias honored — first value only, never recreates a hybrid) |
| `/admin/users/edit` | Razor page | `Pages\Admin\Users\Edit.cshtml` | `AdminPolicies.AdminGerir`; `OnPostSaveAsync` (single template + functional profile + concurrency `version`), `OnPostResetPasswordAsync` |
| `/admin/templates` | Razor page | `Pages\Admin\Templates\Index.cshtml` | `AdminPolicies.AdminGerir` |
| `/admin/templates/edit` | Razor page | `Pages\Admin\Templates\Edit.cshtml` | `AdminPolicies.AdminGerir`; `OnPostAsync` (create/update: name + **exactly one functional profile** + canonical module-grant `lines`; Admin profile ⇒ admin module only, and vice-versa — validation inside the handler) |
| `/admin/applications` | Razor page | `Pages\Admin\Applications\Index.cshtml` | `AdminPolicies.AdminGerir`; `OnPostAsync(List<MirrorEntryInput>)` — catalog mirror display-order/active, known modules only (GLM-CAT-02) |
| `/admin/audit` | Razor page | `Pages\Admin\Audit\Index.cshtml` | `AdminPolicies.AuditView`; `OnGetAsync` filters (year/user/module/action/result/interval, pageSize 20/40/60), `OnPostExportAsync` → CSV file (audit.export re-checked in the use case; page policy remains AuditView) |

Denial flow: policy failure while authenticated → cookie `AccessDeniedPath` `/access-denied` → `AccessDeniedModel` resolves the first still-authorized page and redirects with the fixed `?acesso-negado=1` feedback flag (rendered by `_Layout`); unauthenticated → `LoginPath` `/login`. No redirects exist beyond these cookie-configured targets and the two in-page swap redirects (`/peso`↔`/peso/responsavel`).

### 6.2 API / JSON endpoints (`src\BA.Dmo.Web\Program.cs`)

**125** minimal-API endpoint mappings are declared in `Program.cs` (verified count: 66 `MapPost`, 51 `MapGet`, 6 `MapPut`, 2 `MapDelete`), and **all 125** are secured by exactly one `.RequireAuthorization(<policy>)` where `<policy>` is a canonical `ModulePolicies.*` or `CapabilityPolicies.*` for the owning module. No `/api/admin` JSON endpoints exist — Admin uses Razor page-handler POSTs only (§6.1). Enum-typed JSON binding is configured with `JsonStringEnumConverter` (`ConfigureHttpJsonOptions`).

| Route family (count) | Entry points (service methods) | Authorization policy |
|---|---|---|
| `/api/jobon/*` (7) | `JobOnService` image attach/replace/remove + current set/get; `IJobOnImageProvider.ResolveAsync` (image GET); `JobOnPdfService.GenerateAsync` (PDF) | `CapabilityPolicies.JobonEdit` (attach/replace/remove) / `.JobonView` (image GET, current GET/POST, document) |
| `/api/peso/*` (21) | `PesoService` control create/save/submit/calculate/approve/reject/reopen/delete, compare/decide, control detail, controls search, dates, settings get/save, document (PDF via `IPdfRenderer`), email prepare, references list/save, lote create, comparison create, day-approval | `ModulePolicies.Peso` (+ `CapabilityPolicies.PesoAprovar` for approve/reject/reopen/compare-decide/settings/day-approval via `PesoAuthorizationGate`) |
| `/api/pegamentos/*` (12) | `PegamentoService` context/revision/jobon/control detail, create, measurements, PUT update, close, history, search; `PegamentoPdfService.GenerateAsync` + document confirm | `ModulePolicies.Pegamentos` |
| `/api/ferramentas/*` (18) | `FerramentasService` references list/detail/create/edit, lotes list/duplicate/edit, pieces list/register, condition, rules list (lote), rule CRUD + toggle + rules/active, utilisation record/get (R003, at file end) | `ModulePolicies.Ferramentas` / `CapabilityPolicies.FerramentasConfigure` (rule POST/PUT/toggle/DELETE) |
| `/api/armazem/*` (6) | `ArmazemService` consulta, movimentos, entrada, saida, corrigir-localizacao, `{toolType}/historico` | `ModulePolicies.Armazem` |
| `/api/reparacao-externa/*` (16) | `ReparacaoExternaService` tools, create exit, list exits, exit detail, items add/remove, disponibilizar, recolha, retorno, historico, repairers list/create/update/deactivate, line-defaults get/upsert | `ModulePolicies.ReparacaoExterna` |
| `/api/reparacao-interna/*` (6) | `ReparacaoInternaService` line-cards, context, register, historico, detail, corrigir | `ModulePolicies.ReparacaoInterna` |
| `/api/controlo/*` (9) | `ControloSheetService` production, list, by-production, create, detail, items, submit, reopen, decide | `ModulePolicies.Controlo` (operations gated internally by `controlo.*` capabilities via `ControloSheetAuthorizationGate`) |
| `/api/tampoes/*` (14) | `TampaoService` consulta, configuração detalhe/maquinas/observacao/get, quantidade adicionar/remover, estado alterar, configuração alterar, movimentos, opcoes fields/values CRUD | `ModulePolicies.Tampoes` |
| `/api/historia*` (2) | `HistoriaService.QueryAsync` (grouped) / `QueryFlatAsync` (flat events) | `ModulePolicies.Historia` |
| `/api/boquilhas/*` (14) | `BoquilhasService` lotes create/list/detail, movements register/list, traces close/reopen, discrepancies list/resolve, repairers list/create/update, lines default; `ReparacaoInternaService.ListLineCardsAsync` (production-context sidepanel) | `ModulePolicies.Boquilhas` |

Route helpers declared in `Program.cs`: `ParseType` (Peso), `ParseRepairType`, `ParseRepairStatus` (RepExt), `ParseInternalToolType` (RepInt, CM/MF only — BQ deliberately not mapped), `ParseTampaoMovementType` (Tampões). Tampões/Boquilhas/História filters use explicit `page`/`pageSize` query parameters with server-side defaults (page≥1, pageSize 20).

### 6.3 No redirects beyond direct Web references

The only route-level redirect targets are cookie-configured (`LoginPath=/login`, `LogoutPath=/logout`, `AccessDeniedPath=/access-denied`), exposed as shared Web references (MAP-16/MAP-18 · [16_USERS_ACCESS.md](16_USERS_ACCESS.md), [18_LOGIN.md](18_LOGIN.md)).

## 7. Authorization / Identity / Session Web Objects

Location: `src\BA.Dmo.Web\Authorization\`, `src\BA.Dmo.Web\Identity\`.

| Object | Kind | Role | Path |
|---|---|---|---|
| `AuthenticatedSessionRequirement` / `AuthenticatedSessionHandler` | requirement + handler | verified authenticated session (fallback policy; requires `AuthUserIdClaimType` claim) | `Authorization\AuthenticatedSessionHandler.cs` |
| `ModuleRequirement` / `ModuleAuthorizationHandler` | requirement + handler | `user.HasModule(moduleId)` route guard | `Authorization\ModuleAuthorizationHandler.cs` |
| `CapabilityRequirement` / `CapabilityAuthorizationHandler` | requirement + handler | any-of `user.HasCapability` route guard | `Authorization\CapabilityAuthorizationHandler.cs` |
| `ModulePolicies` | static constants | `BaDmo.Module.{moduleId}` for each canonical module (incl. `Controlo = BaDmo.Module.controlo`) | `Authorization\ModuleAuthorizationHandler.cs` |
| `CapabilityPolicies` | static constants | `BaDmo.Capability.{capabilityId}` (JobOn view/edit/configure/confirmar, PesoAprovar, FerramentasConfigure, Controlo view/edit/submit/review) | `Authorization\ModuleAuthorizationHandler.cs` |
| `AdminPolicies` | static constants | `BaDmo.Admin.Gerir`, `BaDmo.Audit.View`, `BaDmo.Audit.Export` | `Authorization\CapabilityAuthorizationHandler.cs` |
| `SessionClaims` | static constants | `AuthenticationScheme="BaDmo.Session"`, `AuthUserIdClaimType="ba_dmo.auth_user_id"` | `Identity\SessionClaims.cs` |
| `RequestCurrentUserAccessor` | `ICurrentUserAccessor` | per-request `CurrentUser` (cookie auth-user-id → server-side resolution, memoized; null = fail-closed) | `Identity\RequestCurrentUserAccessor.cs` |
| `CurrentUserAuthorshipAccessor` | `IPersistenceAuthorshipAccessor` | per-request `PersistenceAuthorship` (resolved internal actor_id + `IClock` UTC; system ops → null actor) | `Identity\CurrentUserAuthorshipAccessor.cs` |

Cookie/DI wiring lives in `src\BA.Dmo.Web\Program.cs` (lines 82–135): `AddAuthentication(SessionClaims.AuthenticationScheme).AddCookie(...)` with `LoginPath=/login`, `LogoutPath=/logout`, `AccessDeniedPath=/access-denied`, `SlidingExpiration`, 8h `ExpireTimeSpan`, `HttpOnly`, `SameSite=Lax`, `SecurePolicy=SameAsRequest`; **fallback policy** = `AuthenticatedSessionRequirement` (applies to every endpoint/page without explicit metadata); 3 Admin capability policies; per-canonical-module/capability policies built from `CanonicalModuleCatalog.Instance`; handlers: `AuthenticatedSessionHandler` singleton, `CapabilityAuthorizationHandler` + `ModuleAuthorizationHandler` scoped. Web authorization does not invent role/email/template-based rules (GLM-ACC-03/04).

## 8. Shared Shell / Navigation

**Navigation is split across exactly three authorities. Describe, do NOT change.**

### 8.0 Shell frame objects

| Object | Role | Path |
|---|---|---|
| `Pages\Shared\_Layout.cshtml` | application frame (GLM-SHL-01/02): title; `adminScope = Request.Path.StartsWithSegments("/admin")` → body class `admin-scope` + narrower `.admin-work-area`, `ViewData["AdminScope"]` for `_Header`; stylesheet load order tokens → foundation → components → layout → utilities → **admin-layout.css**; shared `dmo-interactions.js` / `dmo-calendar.js`; renders `_Header`, then — **in admin scope only** — `<partial name="_AdminNav" />` at the top of `<main>`, then `RenderBody()`; renders the fixed `acesso-negado` feedback message | `Pages\Shared\_Layout.cshtml` |
| `Pages\Shared\_Header.cshtml` | global header: brand logo (`ba-logo.png`), page identity (admin scope shows "Portal DMO / Administração" instead of the page title), user block (name + profile_title, `data-user-profile-*`, logout link); renders `_Navigation` **only outside admin scope** (comment: "In Admin there is already a dedicated Admin tab strip inside the page. Do not render the global shell 'Administração' tab as a second menu.") | `Pages\Shared\_Header.cshtml` |
| `Pages\Shared\_Navigation.cshtml` | global module navigation, grants-derived (see 8.1) | `Pages\Shared\_Navigation.cshtml` |
| `Pages\Shared\_AdminNav.cshtml` | canonical Admin navigation, single-emission (see 8.2) | `Pages\Shared\_AdminNav.cshtml` |
| `RequestShellService` | per-request `ShellState` (`DisplayName`, `ProfileTitle`, navigation) over `IShellService` + `INavigationService`; null = fail-closed frame | `Shell\RequestShellService.cs` |

### 8.1 Authority 1 — Global shell navigation (module tabs)

`Pages\Shared\_Navigation.cshtml` injects `BA.Dmo.Application.Shared.Shell.IShellService` and consumes `Shell.Current.Navigation.LeftItems` + `Shell.Current.Navigation.AdminEntry` (right-aligned "Administração" `NavigationTab`). The file carries the **GLM-SHL-03** contract comment: *"Module navigation (GLM-SHL-03): tabs DERIVED from the resolved grants ∩ canonical catalog, canonical order. Controlo is one top-level entry; Peso/Pegamentos remain internal. Unauthorized entries do not exist."* Tabs are server-derived (`RequestShellService` → `IdentityResolutionService` + `INavigationService.Build(identity.Access, path)`), so the markup contains **no module-link inventory**; active state from `item.IsActive`; `data-testid="nav-item-{id}"`.

Rendered by `_Header` **only when NOT admin scope** (commit `9347b11` "Use one navigation layer in Admin" + `8478308`): in `/admin` the global shell nav is intentionally omitted so the shell's "Administração" tab never appears as a second menu next to the Admin tab strip.

### 8.2 Authority 2 — Admin-only navigation (persistent Admin tabs)

`Pages\Shared\_AdminNav.cshtml` is the canonical Admin navigation: one `<nav class="dmo-module-tabs admin-tabs" aria-label="Administração">` with four **hard-coded** tab links — Utilizadores (`/admin/users`), Templates (`/admin/templates`), Aplicações (`/admin/applications`), Auditoria (`/admin/audit`) — active tab derived only from the current route via `path.StartsWith(prefix)`. (Label "Modelos" was renamed to "Templates" in the nav rework.)

**Single-emission contract (commits `76004a0`, `8478308`):** the partial uses a request-scoped `Context.Items` marker `BA_DMO_ADMIN_NAV_RENDERED` — first invocation in the request renders the nav and sets the marker; every later invocation in the same request is suppressed (`shouldRender = !Context.Items.ContainsKey(renderKey)`). The file comment states the intent: *"The layout renders this once for the whole /admin scope. Older page-level partial calls are intentionally kept harmless during reconciliation: this request marker prevents a second menu from being emitted."*

Call sites (9 total, all source-verified):
- `_Layout.cshtml` line 46–49 — renders `_AdminNav` for **every** route under `/admin`, at the top of `<main>` (commit `8478308` "Render one persistent Admin navigation").
- All 8 Admin pages still call `<partial name="_AdminNav" />` themselves (`Admin\Index` L7; `Admin\Users\{Index,Edit,Create}` L7; `Admin\Templates\{Index,Edit}` L7; `Admin\Applications\Index` L7; `Admin\Audit\Index` L24) — **kept but suppressed by the marker** on normal requests.

The marker guarantees **at most one `<nav>` per request regardless of call-site execution order**; which call site emits it follows from Razor page/layout execution order (the page body is buffered and rendered by `RenderBody()` inside the layout; the map does not assert the winning call site — NOT a behavioural contract, see §14 ambiguity). Also removed in the rework: `Admin\Index.cshtml`'s old landing menu card with buttons to the four areas (commit `084604d` "Remove duplicate admin landing menu").

### 8.3 Authority 3 — Page-local module tab navigation (static, JS-switched)

Each interactive module page hard-codes its own workspace tab strip (`<nav class="dmo-module-tabs ...">`) with buttons switching in-page views via `data-view`/`data-tab` + the module JS — this is **view navigation, not route navigation**:

| Page | Tab strip (`class`) | Tabs |
|---|---|---|
| `JobOn\Index` | `jobon-tabs` | Planeamento / Job On / Histórico / Definições (Definições only when `CanConfigure`) |
| `Peso\Index` | `peso-tabs` | Novo controlo / Referências / Comparação / Histórico / Definições |
| `Peso\Responsavel` | `peso-tabs` | Aprovações / Definições |
| `Pegamentos\Index` | `pegamentos-tabs` | Históricos / Nova folha / Configuração |
| `Ferramentas\Index` | `ferramentas-tabs` | Contra moldes (CM) / Moldes finais (MF) |
| `Armazem\Index` | `armazem-tabs` | Registo / Consulta / Histórico |
| `ReparacaoExterna\Index` | `reparacao-externa-tabs` | Boquilhas / Contra moldes / Moldes finais / Envios / Histórico / Definições |
| `ReparacaoInterna\Index` | `reparacao-interna-tabs` | Registo / Histórico |
| `Controlo\Index` | `controlo-tabs` (`#workTabs`, rendered `hidden` until a production context is selected) | Resumo / Peso / Comparação / Pegamentos / Histórico |
| `Tampoes\Index` | `tampoes-tabs` | Registo / Consulta / Histórico / Linhas e Máquinas / Opções |
| `Boquilhas\Index` | `boquilhas-tabs` | Registo / Boquilhas / Histórico / Definições |

`Historia\Index` (server-rendered query) and `DesignLaboratorio\Index` (component catalogue) have no page-local tabs.

**Why three authorities:** the global shell owns cross-module navigation and must be dynamic/grants-derived (GLM-SHL-03 — module sets change per identity, so it can never live in markup); the Admin strip is a fixed canonical scope (four always-present tabs) but must render exactly once per request after the single-navigation-layer rework (marker); page-local strips belong to each module's own workspace anatomy and are JS view-switchers, independent of route authorization.

## 9. Static Assets

### 9.1 Shared design-system CSS (5 files) — `wwwroot\styles\dmo-*.css`

| File | Role |
|---|---|
| `dmo-tokens.css` | design tokens (`--dmo-*`, incl. `--dmo-line-b1..c3` line-color tokens consumed by `JobOnLineColor`) |
| `dmo-foundation.css` | base/foundation |
| `dmo-components.css` | canonical components (incl. module selectors such as `.boquilhas-*`, tab anatomy `.dmo-module-tabs`) |
| `dmo-layout.css` | layout/composition (incl. `.admin-nav` legacy selector) |
| `dmo-utilities.css` | utilities (incl. `.dmo-u-embedded-overlay`, used by `/design-laboratorio`) |

Served globally from `_Layout` (load order tokens → foundation → components → layout → utilities). Additionally `_Layout` loads `styles\modules\admin-layout.css` for every page (rules are scoped to `.admin-scope`/`.admin-work-area` so non-admin pages carry it inert).

### 9.2 Module-specific CSS (10 files) — `wwwroot\styles\modules\`

`admin-layout.css`, `armazem-layout.css`, `controlo-layout.css`, `ferramentas-layout.css`, `jobon-layout.css`, `pegamentos-layout.css`, `peso-layout.css`, `reparacao-externa-layout.css`, `reparacao-interna-layout.css`, `tampoes-layout.css`.

Wiring (each module page links its own file at the top of the page body): jobon, armazem, ferramentas (Index/Criar/Ficha), peso (Index/Responsavel), pegamentos (Index/Detail), reparacao-externa, reparacao-interna, tampoes. **`controlo-layout.css` is not referenced by any page — see §14 finding.** `admin-layout.css` is loaded from `_Layout` (always).

### 9.3 Shared + module JS (12 files) — `wwwroot\scripts\`

- **Shared:** `dmo-calendar.js`, `dmo-interactions.js` (both loaded by `_Layout`).
- **Module-specific (10):** `jobon.js`, `peso.js`, `pegamentos.js`, `ferramentas.js`, `armazem.js`, `reparacao-externa.js`, `reparacao-interna.js`, `controlo.js` (loaded by `Controlo\Index.cshtml` L107), `tampoes.js`, `boquilhas.js`.
- Server-rendered pages without module JS: Historia, DesignLaboratorio (latter has a small inline script, `Index.cshtml` L271), Auth and safe-state pages (standalone `<head>`, `Layout = null`; Login also loads `dmo-interactions.js` for the password-reveal contract).

### 9.4 Assets — `wwwroot\assets\`

`ba-logo.png` (shared brand asset, rendered by `_Header`).

## 10. Module-Specific Web Areas

Technical source ownership by folder (no functional-ownership claim beyond directory placement):

| Area | Razor pages (`Pages\`) | JS (`wwwroot\scripts\`) | Module CSS (`wwwroot\styles\modules\`) |
|---|---|---|---|
| Admin | `Admin\` (17 files: 8 pages × 2 + `TemplateProfileStore.cs`) | — (server-rendered Razor handlers) | `admin-layout.css` (loaded globally via `_Layout`) |
| Login | `Auth\` (4 files) | — (inline flow + shared `dmo-interactions.js`) | — |
| Design Laboratório | `DesignLaboratorio\` (2 files) | — (inline script, L271) | — |
| Job On | `JobOn\` (3 files) | `jobon.js` | `jobon-layout.css` |
| Peso | `Peso\` (4 files) | `peso.js` | `peso-layout.css` |
| Pegamentos | `Pegamentos\` (4 files) | `pegamentos.js` | `pegamentos-layout.css` |
| Ferramentas | `Ferramentas\` (7 files) | `ferramentas.js` | `ferramentas-layout.css` |
| Armazem | `Armazem\` (2 files) | `armazem.js` | `armazem-layout.css` |
| Reparação Externa | `ReparacaoExterna\` (5 files) | `reparacao-externa.js` | `reparacao-externa-layout.css` |
| Reparação Interna | `ReparacaoInterna\` (2 files) | `reparacao-interna.js` | `reparacao-interna-layout.css` |
| Controlo | `Controlo\` (2 files) | `controlo.js` | `controlo-layout.css` ⚠ **unreferenced** (§14) |
| Tampoes | `Tampoes\` (2 files) | `tampoes.js` | `tampoes-layout.css` |
| Historia | `Historia\` (2 files) | — (server-rendered) | — (shared `dmo-components.css` carries module selectors) |
| Boquilhas | `Boquilhas\` (2 files) | `boquilhas.js` | — (shared `dmo-components.css` carries `.boquilhas-*`) |

Admin, Login and Design Laboratório surfaces are classified by their source folders (Admin / Auth / DesignLaboratorio), not promoted to canonical modules. Shared shell pages (`_Layout`, `_Header`, `_Navigation`, `_AdminNav`) belong to the shared shell, not to any module.

## 11. Direct Web References

Mechanical, source-visible references inside Web (composition root → Application/Infrastructure; page → service/asset).

### 11.1 Program.cs composition references (source lines)

```
Program.cs L42-49   → CliModeResolver.Resolve(args) → MigrateCommand.Run() / BootstrapAdminCommand.Run() (CLI-only verbs)
Program.cs L56      → DataProtection ephemeral under "Testing" environment
Program.cs L60      → PersistenceMappings.Configure() (Dapper snake_case mapping)
Program.cs L64-67   → CatalogValidator.Validate(CanonicalModuleCatalog.Instance, CanonicalPageCatalog.Instance, AreaChildren)
Program.cs L69      → AddRazorPages()
Program.cs L74-75   → ConfigureHttpJsonOptions (JsonStringEnumConverter)
Program.cs L82-95   → AddAuthentication(SessionClaims.AuthenticationScheme).AddCookie(...) — Login/Logout/AccessDenied paths, 8h, HttpOnly, Lax
Program.cs L96-132  → FallbackPolicy=AuthenticatedSessionRequirement; AdminPolicies.AdminGerir/AuditView/AuditExport; per-canonical ModulePolicies+CapabilityPolicies
Program.cs L133-135 → IAuthorizationHandler registrations (Authenticated/handler singleton; Capability, Module scoped)
Program.cs L137-160 → IClock, LazyDbConnectionFactory(IDbConnectionFactory), DapperInternalUserRepository, AccessResolver, NavigationService(INavigationService), RequestShellService(IShellService), IdentityResolutionService, RequestCurrentUserAccessor, CurrentUserAuthorshipAccessor, SupabaseAuthAdapter (ISupabaseAuthAdapter)
Program.cs L167-190 → SupabaseAdminProvisioningAdapter (IAdminProvisioningAdapter, fail-closed), DapperAdminRepository, DapperModuleCatalogMirrorRepository, JobOn repos/gate/service, FileSystemJobOnImageProvider, JobOnPdfService/renderer, CanonicalModuleCatalog.Instance, Admin gating + AdminUser/Template/Mirror/Audit services, GrantNormalizer
Program.cs L193-275 → per-module Application services + Dapper ports + gates + PDF renderers (Peso, Pegamentos, Ferramentas, Armazém + IArmazemRepairMovementPort, RepExt, RepInt, Controlo, Tampões, História, Boquilhas)
Program.cs L281-285 → UseStaticFiles / UseRouting / UseAuthentication / UseAuthorization / MapRazorPages
Program.cs L287-1625 → 125 minimal-API endpoint groups → module Application services (full 1:1 mapping in MAP-19 §10)
Program.cs L1627-1631 → app.Run(); `public partial class Program;` (exposes entry point to the integration test project)
```

### 11.2 Page → service/asset references (representative)

```
Pages\JobOn\Index.cshtml        → CapabilityPolicies.JobonView; jobon.js; jobon-layout.css
Pages\Controlo\Index.cshtml     → ModulePolicies.Controlo; controlo.js (no module CSS — §14)
Pages\Admin\*\*.cshtml          → AdminPolicies.*; _AdminNav (suppressed page-level calls); admin-layout.css (via _Layout)
Pages\Auth\Login.cshtml         → [AllowAnonymous]; Layout=null; dmo-*.css (standalone head); dmo-interactions.js; ba-logo.png
Pages\DesignLaboratorio\Index   → fallback session policy; default layout (all dmo-*.css + dmo-calendar.js/dmo-interactions.js); inline script
Pages\Shared\_Layout.cshtml     → all dmo-*.css + admin-layout.css; dmo-calendar.js; dmo-interactions.js; _Header; _AdminNav (admin scope)
Pages\Shared\_Header.cshtml     → IShellService; _Navigation (non-admin scope); ba-logo.png
Pages\Shared\_Navigation.cshtml → IShellService; Navigation.LeftItems + AdminEntry
Pages\Shared\_AdminNav.cshtml   → Context.Items marker BA_DMO_ADMIN_NAV_RENDERED; route-derived active tab
```

### 11.3 Web-layer service composition

`Program.cs` is the composition root registering all Web/Application/Infrastructure services. It also hosts the CLI verbs (`CliMode`, `CliModeResolver`, `MigrateCommand`, `BootstrapAdminCommand`) used for migrate/bootstrap operations — CLI only, no HTTP migration endpoint (GLM-ARCH-15).

## 12. Target-to-Location Index

| Technical object | Location (`src\BA.Dmo.Web\`) |
|---|---|
| Composition root / API endpoints / policies / DI | `Program.cs` |
| AuthenticatedSession/Module/Capability handlers + policy constants | `Authorization\` |
| SessionClaims, RequestCurrentUserAccessor, CurrentUserAuthorshipAccessor | `Identity\` |
| RequestShellService | `Shell\RequestShellService.cs` |
| Shared shell partials | `Pages\Shared\_Layout/_Header/_Navigation/_AdminNav.cshtml` |
| Admin nav single-emission marker | `Pages\Shared\_AdminNav.cshtml` (`Context.Items["BA_DMO_ADMIN_NAV_RENDERED"]`) |
| Razor pages + PageModels | `Pages\**\*.cshtml` / `*.cshtml.cs` |
| Page `@page` route + `@attribute [Authorize]` declarations | `Pages\**\*.cshtml` |
| 125 minimal-API endpoints | `Program.cs` (L287–1625) |
| Non-page Web classes | `Pages\JobOn\JobOnLineColor.cs`, `Pages\ReparacaoExterna\ReparacaoExternaListBuilderModel.cs`, `Pages\Admin\TemplateProfileStore.cs` |
| Shared design-system CSS | `wwwroot\styles\dmo-*.css` |
| Module CSS | `wwwroot\styles\modules\*.css` (10 files; `controlo-layout.css` unreferenced — §14) |
| Shared JS | `wwwroot\scripts\dmo-calendar.js`, `dmo-interactions.js` |
| Module JS | `wwwroot\scripts\<module>.js` (10 files) |
| Brand asset | `wwwroot\assets\ba-logo.png` |
| CLI verbs | `Cli\` |

## 13. Sources Verified

All statements in this revision were verified against the following current files (HEAD `8478308`), source-only:

- `src\BA.Dmo.Web\BA.Dmo.Web.csproj` (references Application + Infrastructure; ships `database\migrations\**\*.sql` content).
- Full recursive listing of `src\BA.Dmo.Web\` (excluding `bin\`/`obj\`) — all `.cs`, `.cshtml`, `.css`, `.js`, `.json`, `.png` enumerated.
- `src\BA.Dmo.Web\Program.cs` (1631 lines, read completely): CLI dispatch L42–49; DI/policies L51–276; endpoint mappings L287–1625 (125 `Map*` + 125 `.RequireAuthorization`, verified by script); route helpers; `app.Run()` L1627; `public partial class Program;` L1631.
- `src\BA.Dmo.Web\Authorization\` (3 handlers + `ModulePolicies`/`CapabilityPolicies`/`AdminPolicies` constants).
- `src\BA.Dmo.Web\Identity\` (SessionClaims, RequestCurrentUserAccessor, CurrentUserAuthorshipAccessor).
- `src\BA.Dmo.Web\Shell\RequestShellService.cs`; `src\BA.Dmo.Web\Cli\` (4 files).
- `src\BA.Dmo.Web\Pages\` — all Razor pages/PageModels; `@page` route and `@attribute [Authorize(Policy = …)]` extracted per page (script-verified for all 30 page routes); Admin pages incl. `TemplateProfileStore.cs`; page-local tab strips enumerated.
- `src\BA.Dmo.Web\Pages\Shared\{_Layout,_Header,_Navigation,_AdminNav}.cshtml` (navigation authorities).
- `src\BA.Dmo.Web\wwwroot\` (5 root `dmo-*.css`, 10 module CSS, 12 JS, 1 `.png`; CSS/JS wiring per page verified; `controlo-layout.css` reference search across `src\BA.Dmo.Web`).
- `src\BA.Dmo.Application\Shared\Access\{CanonicalModuleCatalog,CanonicalPageCatalog,AccessResolver,NavigationService,CatalogValidator}.cs` (module ids, `AreaChildren`, capability ids, first-page resolution used by §6/§8).
- Git range `1f91dfe..8478308` on `src\BA.Dmo.Web\` (4 files changed: `_AdminNav.cshtml`, `_Header.cshtml`, `_Layout.cshtml`, `Admin\Index.cshtml`).
- Tests cross-referenced (they live under `AI-CONTEXT\docs\tests\`, not `tests\`): `BA.Dmo.IntegrationTests\Access\ShellRoutingTests.cs`, `AdminWebAuthorizationTests.cs`, `Design\ShellAndCalendarGuardTests.cs` — used only as evidence for the §14 drift finding, never as a source of truth.
- Cross-checked against [00_INDEX.md](00_INDEX.md), module maps [06_JOB_ON.md](06_JOB_ON.md) … [17_DESIGN_LABORATORIO.md](17_DESIGN_LABORATORIO.md), [15_ADMIN.md](15_ADMIN.md), [16_USERS_ACCESS.md](16_USERS_ACCESS.md), [18_LOGIN.md](18_LOGIN.md), [19_APPLICATION.md](19_APPLICATION.md), [05_TESTS.md](05_TESTS.md).
- No Design/SOT, AI-CONTEXT, or implementation behavior used as evidence; source-inspection only.

## 14. Classification Findings

Classification labels use the map's taxonomy. Findings are recorded here with evidence; this map proposes **no deletion** and makes **no change** to source.

1. **CONFIRMED CURRENT — navigation single layer + persistent Admin tabs (commits `1f91dfe..8478308`).** `_Layout` renders `<partial name="_AdminNav" />` for every /admin route; `_Header` skips the global shell `_Navigation` in admin scope; `Admin\Index.cshtml` landing menu removed; `_AdminNav` uses request marker `BA_DMO_ADMIN_NAV_RENDERED`. 8 page-level `_AdminNav` calls remain but are suppressed by the marker (at most one `<nav>` per request). Evidence: `git diff 1f91dfe..8478308 -- src/BA.Dmo.Web` (4 files), current content of the four partials (read fully).

2. **ORPHAN CANDIDATE — NEEDS AUDIT: `wwwroot\styles\modules\controlo-layout.css`.** The file exists (10 module CSS files) but **no page references it**: `Pages\Controlo\Index.cshtml` links no module stylesheet (only `controlo.js` at L107), and a repository-wide search for `controlo-layout` inside `src\BA.Dmo.Web` returns only the file itself and a comment in `dmo-layout.css` about the admin contract. Every other module CSS file is linked by its module page (source-verified, §9.2). The Controlo page therefore renders with shared `dmo-*` CSS only.

3. **NEEDS REVIEW — possible test drift in shell routing tests.** `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Access\ShellRoutingTests.cs` `Scenario7_AdminOnly_LandsOnAdmin_AndCannotOpenJobOn` (lines 182–185) asserts `AssertNav(adminHtml, present: ["admin"])` — i.e. expects `data-testid="nav-item-admin"` in the rendered `/admin` HTML. At HEAD, `_Header.cshtml` does **not** render `_Navigation` in admin scope (the `admin-tabs` strip has no `nav-item-*` data-testid), so no `nav-item-admin` should be emitted. The tests were last modified at `91e049f` (before `1f91dfe..8478308`, which touched only the four Web pages and no test file). If the suite is green, this assertion is trivially satisfied by something else or the suite is not executed — either way the assertion no longer documents the intended behavior.

4. **MIGRATION DRIFT IMPACT — NEEDS REVIEW (counts).** The previous map claimed **128** minimal-API endpoints with per-family counts (jobon 6, peso 24, pegamentos 13, armazem 5, reparacao-externa 18, tampoes 17, boquilhas 16) and an Armazém "substituir" entry. Current source, script-verified: **125** endpoints (66 POST / 51 GET / 6 PUT / 2 DELETE; jobon 7, peso 21, pegamentos 12, ferramentas 18, armazem 6 — consulta/movimentos/entrada/saida/corrigir-localizacao/historico, reparacao-externa 16, reparacao-interna 6, controlo 9, tampoes 14, historia 2, boquilhas 14). The old counts do not match any version verified here; they are replaced by the current verified inventory.

5. **SCHEMA/CONTRACT CLARIFICATION — NEEDS REVIEW (map correction): `/controlo` policy.** The previous map stated `/controlo` was `ModulePolicies.Peso` ("served under the pesos surface"). Current source: `Pages\Controlo\Index.cshtml` L3 declares `@attribute [Authorize(Policy = ModulePolicies.Controlo)]` (`BaDmo.Module.controlo`), and `/api/controlo/*` uses `ModulePolicies.Controlo`. `CanonicalModuleCatalog.AreaChildren = { controlo → [peso, pegamentos] }` — Controlo is the parent **area**; Peso/Pegamentos are its children (internal to the Controlo tab set, per the GLM-SHL-03 comment in `_Navigation.cshtml`). Corrected in §6.1/§6.2/§7.

6. **INTENTIONAL NORMALIZATION — `[Authorize]` attribute placement.** Page authorization is declared with `@attribute […Authorize(Policy = …)]` **inside the `.cshtml`** (all 22 protected pages), not on the `PageModel` code-behind (which carries only `[AllowAnonymous]` on Login/Logout/NoAccess). Pages without `@attribute` (`/`, `/design-laboratorio`) fall back to the `AuthenticatedSessionRequirement` fallback policy. Documented as-is; no change proposed.

7. **UNKNOWN / OWNER DECISION REQUIRED — `_AdminNav` winning call site.** The request marker guarantees at most one Admin `<nav>` per request, but which call site (layout vs page-level) actually emits it depends on Razor Pages page/layout execution order (the page body is rendered by `RenderBody()` inside the layout). The map deliberately records the **mechanism** (§8.2) and does not assert a winner; if a future change relies on the exact emitted position, verify at runtime (or add a rendering test pinning the order). No behavioral risk identified — exactly one menu renders either way.

8. **Minor observation (no classification):** the `admin-tabs` class on the Admin nav has no dedicated CSS selector in `wwwroot\styles\modules\admin-layout.css` or the `dmo-*.css` set (search verified); it inherits the canonical `dmo-module-tabs` anatomy from `dmo-components.css`.

## Counts

- Composition root: **1** (`Program.cs`)
- Razor `.cshtml` files: **37** (incl. `_ViewImports.cshtml`, `_ViewStart.cshtml`)
- PageModel `.cshtml.cs` files: **29**
- Non-page Web classes: **3** (`JobOnLineColor.cs`, `ReparacaoExternaListBuilderModel.cs`, `TemplateProfileStore.cs`)
- Pure `.cs` Web classes (Program + Authorization + Identity + Shell + Cli): **12**
- API / endpoint source (minimal-API route mappings in `Program.cs`): **125** (66 POST, 51 GET, 6 PUT, 2 DELETE) — all `.RequireAuthorization`
- Web authorization handler files: **3**
- Web identity/session files: **3**
- Shell service files: **1**
- CLI verb files: **4**
- Static CSS files: **15** (5 global `dmo-*.css` + 10 module CSS)
- Static JS files: **12** (2 shared + 10 module-specific)
- Static image/assets files: **1** (`ba-logo.png`)
- Razor pages by canonical-module ownership: JobOn 1, Peso 2, Pegamentos 2, Ferramentas 3 (+ `_ReferenceList` partial), Armazem 1, ReparacaoExterna 1 (+ `_RepairListBuilder` partial), ReparacaoInterna 1, Controlo 1, Tampoes 1, Historia 1, Boquilhas 1
- Admin pages: **17** files (8 pages × `.cshtml` + `.cshtml.cs` + `TemplateProfileStore.cs`)
- Login pages: **4** files (Login.cshtml/.cs, Logout.cshtml/.cs)
- Design Laboratório page: **2** files (Index.cshtml/.cs)
- Shared shell files: **4** (`_Layout`, `_Header`, `_Navigation`, `_AdminNav`)
- Navigation authorities: **3** (global shell `_Navigation` · Admin `_AdminNav` with `BA_DMO_ADMIN_NAV_RENDERED` marker · page-local `dmo-module-tabs` strips)
- Total relevant Web source files (`.cs` + `.cshtml`): **15 + 29 + 37 = 81** (source code, excluding static assets and config)