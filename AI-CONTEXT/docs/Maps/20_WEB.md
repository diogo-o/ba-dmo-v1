# BA DMO — Web Technical Map

MAP ID: MAP-20
Status: COMPLETE

## Navigation Index

- [1. Scope](#1-scope)
- [2. Project / Folder Structure](#2-project--folder-structure)
- [3. Web Inventory](#3-web-inventory)
- [4. Razor Pages](#4-razor-pages)
- [5. PageModels](#5-pagemodels)
- [6. Routes / Endpoints](#6-routes--endpoints)
- [7. Authorization / Identity / Session Web Objects](#7-authorization--identity--session-web-objects)
- [8. Shared Shell / Navigation](#8-shared-shell--navigation)
- [9. Static Assets](#9-static-assets)
- [10. Module-Specific Web Areas](#10-module-specific-web-areas)
- [11. Direct Web References](#11-direct-web-references)
- [12. Target-to-Location Index](#12-target-to-location-index)
- [13. Sources Verified](#13-sources-verified)

## Counts

## 1. Scope

Pure transversal technical inventory/navigation of the **Web layer** (`src\BA.Dmo.Web\`). This map catalogues what Web source declares and where: Razor Pages, PageModels, API/endpoints, route declarations, the shared shell, navigation components, Web authorization handlers, Web identity/session support, Web services/composition wiring (`Program.cs`), static assets, JS/CSS ownership, exact source locations, and direct Web references.

Rules respected:

- It does **not** explain end-to-end user workflows.
- It does **not** duplicate Domain/Application/Infrastructure/Database detail (each mapped in its own transversal map).
- It does **not** assign functional ownership beyond technical source structure; module-specific surfaces are classified by the source folder they live under, and shared design-system assets are classified as Shared.

`bin\` and `obj\` are build output and excluded. Only current source is mapped; no count is invented.

## 2. Project / Folder Structure

Project: `src\BA.Dmo.Web\BA.Dmo.Web.csproj` — `Microsoft.NET.Sdk.Web` Razor Pages application. It references **`BA.Dmo.Application`** and **`BA.Dmo.Infrastructure`**. It also ships `database\migrations\**\*.sql` as content for the CLI migrate verbs.

```
src\BA.Dmo.Web\
├─ Program.cs                     ← composition root: services, policies, DI, API endpoints
├─ Authorization\                 (3) — AuthenticatedSession/Module/Capability handlers + policies
├─ Identity\                      (3) — SessionClaims, RequestCurrentUserAccessor, CurrentUserAuthorshipAccessor
├─ Shell\                         (1) — RequestShellService
├─ Cli\                           (4) — CliMode, CliModeResolver, MigrateCommand, BootstrapAdminCommand
├─ Pages\
│  ├─_ViewImports.cshtml, _ViewStart.cshtml
│  ├─ Index.cshtml / Index.cshtml.cs        (root redirect endpoint)
│  ├─ AccessDenied.cshtml / .cs             (shared auth safe-state)
│  ├─ NoAccess.cshtml / .cs                 (shared auth safe-state)
│  ├─ Shared\                               (4) _Layout, _Header, _Navigation, _AdminNav
│  ├─ Auth\                                 (4) Login, Logout
│  ├─ Admin\                                (16) Index + 4 areas (Users/Templates/Applications/Audit)
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

**Source-file counts (Web root, excluding `bin\`/`obj\`/`wwwroot` static binaries):** `.cs` = 43, `.cshtml` = 37 (incl. `_ViewImports`/`_ViewStart`), `.json` = 3 (config/launchSettings). Under `wwwroot\`: 15 `.css`, 12 `.js`, 1 `.png`. Full breakdowns in §3.

## 3. Web Inventory

### 3.1 Web source files by kind (exact)

| Kind | Count | Path (under `src\BA.Dmo.Web\`) |
|---|---|---|
| Composition root | 1 | `Program.cs` |
| Razor Pages (`.cshtml`) | 37 | `Pages\**\*.cshtml` (incl. `_ViewImports`, `_ViewStart`) |
| Razor PageModels / code-behind (`.cshtml.cs`) | 29 | `Pages\**\*.cshtml.cs` |
| Non-page Web classes | 2 | `Pages\JobOn\JobOnLineColor.cs`, `Pages\ReparacaoExterna\ReparacaoExternaListBuilderModel.cs` |
| Authorization handlers | 3 | `Authorization\*Handler.cs` |
| Web identity/session | 3 | `Identity\` (SessionClaims, RequestCurrentUserAccessor, CurrentUserAuthorshipAccessor) |
| Shell service | 1 | `Shell\RequestShellService.cs` |
| CLI verbs | 4 | `Cli\` |
| Config/support | 3 | `.json` (settings) |

### 3.2 Razor Pages by folder (source-grounded)

| Folder | `.cshtml` | `.cshtml.cs` (PageModels) | Route surface |
|---|---|---|---|
| `Pages\` (root) | Index, AccessDenied, NoAccess | Index.cs, AccessDenied.cs, NoAccess.cs | `/`, `/access-denied`, `/no-access` |
| `Pages\Shared\` | _Layout, _Header, _Navigation, _AdminNav | — (partials/layout) | shared shell |
| `Pages\Auth\` | Login, Logout | Login.cs, Logout.cs | `/login`, `/logout` |
| `Pages\Admin\` | 8 (Index, Users Create/Edit/Index, Templates Edit/Index, Applications Index, Audit Index) | 8 | `/admin`, `/admin/users/*`, `/admin/templates/*`, `/admin/applications`, `/admin/audit` |
| `Pages\JobOn\` | Index | Index.cs | `/jobon` (+ `JobOnLineColor.cs`) |
| `Pages\Peso\` | Index, Responsavel | Index.cs, Responsavel.cs | `/peso`, `/peso/responsavel` |
| `Pages\Pegamentos\` | Index, Detail | Index.cs, Detail.cs | `/pegamentos`, `/pegamentos/{id}` |
| `Pages\Ferramentas\` | Index, Criar, Ficha, _ReferenceList | Index.cs, Criar.cs, Ficha.cs | `/ferramentas`, `/ferramentas/criar`, `/ferramentas/{id}` |
| `Pages\Armazem\` | Index | Index.cs | `/armazem` |
| `Pages\ReparacaoExterna\` | Index, _RepairListBuilder | Index.cs (+ ListBuilderModel.cs) | `/reparacao-externa` |
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
| Module-specific canonical pages | JobOn, Peso(2), Pegamentos(2), Ferramentas(4), Armazem, ReparacaoExterna, ReparacaoInterna, Controlo, Tampoes, Historia, Boquilhas | `Pages\<Module>\` |

Razor Page routes are declared via `@page` directives; authorization is attached per page via `[Authorize(Policy = …)]` attributes (see §6–§7).

## 5. PageModels

`IndexModel : PageModel` style code-behind classes (`.cshtml.cs`), 29 total, one per PageModel. Representative surfaces and their constructor/service dependencies (full method detail per module map):

| Module | PageModels | Key injected services / fields exposed |
|---|---|---|
| Job On | `IndexModel` | `ICurrentUserAccessor`, `IJobOnRepository`, `JobOnService?`; `OnGetAsync` |
| Peso | `IndexModel`, `ResponsavelModel` | `ICurrentUserAccessor` (+ service routes via JS) |
| Pegamentos | `IndexModel`, `DetailModel` | PageModel surfaces over `PegamentoService` |
| Ferramentas | `IndexModel`, `CriarModel`, `FichaModel` (+ `FerramentasListModel` partial model) | `ICurrentUserAccessor`, `CanConfigure` |
| Armazem | `IndexModel` | `ICurrentUserAccessor` |
| ReparacaoExterna | `IndexModel` (+ `ReparacaoExternaListBuilderModel`) | PageModel |
| ReparacaoInterna | `IndexModel` | `CanCorrigir` capability flag |
| Controlo | `IndexModel` | `ICurrentUserAccessor`; `CanEdit/CanSubmit/CanReview` |
| Tampoes | `IndexModel` | PageModel |
| Historia | `IndexModel` | `HistoriaService`; `VisibleModuleIds`, `Histories` |
| Boquilhas | `IndexModel` | PageModel |
| Admin | 8 PageModels (Index, Users Create/Edit/Index, Templates Edit/Index, Applications Index, Audit Index) | Admin services (`AdminUserService`/`AdminTemplateService`/`AdminAuditService`/`AdminMirrorService`) |
| Auth | `LoginModel`, `LogoutModel` | `ISupabaseAuthAdapter`, `IdentityResolutionService` |
| Shared auth/shell | `IndexModel`, `AccessDeniedModel`, `NoAccessModel` | shell/redirect logic |
| Design Laboratório | `IndexModel` | no injected services |

## 6. Routes / Endpoints

### 6.1 Razor Page routes (source-grounded via `@page` + `[Authorize(Policy = …)]`)

| Route | Kind | File | Authorization / metadata |
|---|---|---|---|
| `/` | Razor page | `Pages\Index.cshtml` | shared shell redirect (authenticated) |
| `/access-denied` | Razor page | `Pages\AccessDenied.cshtml` | cookie `AccessDeniedPath` (shared) |
| `/no-access` | Razor page | `Pages\NoAccess.cshtml` | shared auth safe-state |
| `/login`, `/logout` | Razor page | `Pages\Auth\Login.cshtml`, `Logout.cshtml` | `[AllowAnonymous]` (Login/Logout) |
| `/jobon` | Razor page | `Pages\JobOn\Index.cshtml` | `CapabilityPolicies.JobonView` |
| `/peso`, `/peso/responsavel` | Razor page | `Pages\Peso\Index.cshtml`, `Responsavel.cshtml` | `ModulePolicies.Peso` (Responsavel page is also `ModulePolicies.Peso`; the `peso.aprovar` capability gates approve/reject operations server-side via `PesoAuthorizationGate`) |
| `/pegamentos`, `/pegamentos/{id}` | Razor page | `Pages\Pegamentos\Index.cshtml`, `Detail.cshtml` | `ModulePolicies.Pegamentos` |
| `/ferramentas`, `/ferramentas/criar`, `/ferramentas/{id:guid}` | Razor page | `Pages\Ferramentas\*.cshtml` | `ModulePolicies.Ferramentas` |
| `/armazem` | Razor page | `Pages\Armazem\Index.cshtml` | `ModulePolicies.Armazem` |
| `/reparacao-externa` | Razor page | `Pages\ReparacaoExterna\Index.cshtml` | `ModulePolicies.ReparacaoExterna` |
| `/reparacao-interna` | Razor page | `Pages\ReparacaoInterna\Index.cshtml` | `ModulePolicies.ReparacaoInterna` |
| `/controlo` | Razor page | `Pages\Controlo\Index.cshtml` | `ModulePolicies.Peso` (Controlo page is served under the pesos surface) |
| `/tampoes` | Razor page | `Pages\Tampoes\Index.cshtml` | `ModulePolicies.Tampoes` |
| `/historia` | Razor page | `Pages\Historia\Index.cshtml` | `ModulePolicies.Historia` |
| `/boquilhas` | Razor page | `Pages\Boquilhas\Index.cshtml` | `ModulePolicies.Boquilhas` |
| `/design-laboratorio` | Razor page | `Pages\DesignLaboratorio\Index.cshtml` | fallback `AuthenticatedSessionRequirement` (no module/capability policy) |
| `/admin`, `/admin/users/*`, `/admin/templates/*`, `/admin/applications`, `/admin/audit` | Razor pages | `Pages\Admin\**` | `AdminPolicies.AdminGerir` / `AuditView` / `AuditExport` |

### 6.2 API / JSON endpoints (`src\BA.Dmo.Web\Program.cs`)

**128** minimal-API endpoint mappings are declared in `Program.cs`. Each is secured by one `.RequireAuthorization(<policy>)` where `<policy>` is a canonical `ModulePolicies.*` or `CapabilityPolicies.*` for the owning module. Listed by route family (verb, technical entry point, authorization policy — exact line numbers in `Program.cs`):

| Route family | Entry points (service methods) | Authorization policy |
|---|---|---|
| `/api/jobon/*` (6) | `JobOnService` image attach/replace/remove, current set/get; `JobOnPdfService.GenerateAsync` | `CapabilityPolicies.JobonEdit` / `.JobonView` |
| `/api/peso/*` (24) | `PesoService` control/lote/reference/settings/comparison/document/email/day-approval | `ModulePolicies.Peso` (+ `peso.aprovar` for approve/reject/reopen via `PesoAuthorizationGate`) |
| `/api/pegamentos/*` (13) | `PegamentoService` context/revision/measurements/close/history/search/document | `ModulePolicies.Pegamentos` |
| `/api/ferramentas/*` (18) | `FerramentasService` references/lotes/pieces/rules/utilisation | `ModulePolicies.Ferramentas` / `CapabilityPolicies.FerramentasConfigure` (rule writes) |
| `/api/armazem/*` (5) | `ArmazemService` consulta/entrada/saida/substituir/historico | `ModulePolicies.Armazem` |
| `/api/reparacao-externa/*` (18) | `ReparacaoExternaService` tools/exits/items/disponibilizar/recolha/retorno/historico/repairers/line-defaults | `ModulePolicies.ReparacaoExterna` |
| `/api/reparacao-interna/*` (6) | `ReparacaoInternaService` line-cards/context/register/historico/detail/corrigir | `ModulePolicies.ReparacaoInterna` |
| `/api/controlo/*` (9) | `ControloSheetService` production/list/by-production/create/items/submit/reopen/decide | `ModulePolicies.Peso` (+ `controlo.*` via `ControloSheetAuthorizationGate`) |
| `/api/tampoes/*` (17) | `TampaoService` consulta/configuração/quantidade/estado/planos/movimentos/opcoes | `ModulePolicies.Tampoes` |
| `/api/historia*` (2) | `HistoriaService.QueryAsync` / `QueryFlatAsync` | `ModulePolicies.Historia` |
| `/api/boquilhas/*` (16) | `BoquilhasService` / `ReparacaoInternaService.ListLineCardsAsync` (production-context) | `ModulePolicies.Boquilhas` |

Route helpers declared in `Program.cs`: `ParseType` (Peso), `ParseRepairType`, `ParseRepairStatus`, `ParseInternalToolType`, `ParseTampaoBalance`, `ParseTampaoMovementType`. Admin uses no `/api/admin` JSON endpoints (Razor page-handler POSTs only).

### 6.3 No redirects beyond direct Web references

The only route-level redirect targets are cookie-configured (`LoginPath=/login`, `LogoutPath=/logout`, `AccessDeniedPath=/access-denied`), exposed as shared Web references (MAP-16/MAP-18).

## 7. Authorization / Identity / Session Web Objects

Location: `src\BA.Dmo.Web\Authorization\`, `src\BA.Dmo.Web\Identity\`.

| Object | Kind | Role | Path |
|---|---|---|---|
| `AuthenticatedSessionRequirement` / `AuthenticatedSessionHandler` | requirement + handler | verified authenticated session (fallback policy) | `Authorization\AuthenticatedSessionHandler.cs` |
| `ModuleRequirement` / `ModuleAuthorizationHandler` | requirement + handler | `user.HasModule(moduleId)` route guard | `Authorization\ModuleAuthorizationHandler.cs` |
| `CapabilityRequirement` / `CapabilityAuthorizationHandler` | requirement + handler | `user.HasCapability` route guard | `Authorization\CapabilityAuthorizationHandler.cs` |
| `ModulePolicies` | static constants | `BaDmo.Module.{moduleId}` for each canonical module | `Authorization\ModuleAuthorizationHandler.cs` |
| `CapabilityPolicies` | static constants | `BaDmo.Capability.{capabilityId}` | `Authorization\ModuleAuthorizationHandler.cs` |
| `AdminPolicies` | static constants | `BaDmo.Admin.Gerir`, `BaDmo.Audit.View`, `BaDmo.Audit.Export` | `Authorization\CapabilityAuthorizationHandler.cs` |
| `SessionClaims` | static constants | `AuthenticationScheme="BaDmo.Session"`, `AuthUserIdClaimType` | `Identity\SessionClaims.cs` |
| `RequestCurrentUserAccessor` | `ICurrentUserAccessor` | per-request `CurrentUser` | `Identity\RequestCurrentUserAccessor.cs` |
| `CurrentUserAuthorshipAccessor` | `IPersistenceAuthorshipAccessor` | per-request actor + timestamp | `Identity\CurrentUserAuthorshipAccessor.cs` |

Cookie/DI wiring lives in `src\BA.Dmo.Web\Program.cs`: fallback session policy + 3 Admin capability policies + per-canonical-module/capability policies (built from `CanonicalModuleCatalog.Instance`); cookie options (`LoginPath`/`LogoutPath`/`AccessDeniedPath`, 8h expiry, HttpOnly, SameSite=Lax). Web authorization does not invent role/email/template-based rules.

## 8. Shared Shell / Navigation

| Object | Role | Path |
|---|---|---|
| `Pages\Shared\_Layout.cshtml` | application frame; stylesheet load order (tokens → foundation → components → layout → utilities); shared `dmo-interactions.js` / `dmo-calendar.js` | `Pages\Shared\_Layout.cshtml` |
| `Pages\Shared\_Header.cshtml` | shared header | `Pages\Shared\_Header.cshtml` |
| `Pages\Shared\_Navigation.cshtml` | generic module navigation (derived from `IShellService` / `NavigationService`) | `Pages\Shared\_Navigation.cshtml` |
| `Pages\Shared\_AdminNav.cshtml` | Admin area navigation | `Pages\Shared\_AdminNav.cshtml` |
| `RequestShellService` | per-request `ShellState` (`DisplayName`, `ProfileTitle`, navigation) over `IShellService` | `Shell\RequestShellService.cs` |

Navigation is derived from the canonical catalogs via Application `NavigationService`/`AccessResolver`; shared markup contains no module-link inventory itself. `_Layout.cshtml` serves the global `dmo-*` design-system set to every page.

## 9. Static Assets

### 9.1 Shared design-system CSS (5 files) — `wwwroot\styles\dmo-*.css`

| File | Role |
|---|---|
| `dmo-tokens.css` | design tokens (`--dmo-*`) |
| `dmo-foundation.css` | base/foundation |
| `dmo-components.css` | canonical components (incl. module-specific selectors, e.g. `.boquilhas-*`) |
| `dmo-layout.css` | layout/composition (incl. `.admin-nav`) |
| `dmo-utilities.css` | utilities (incl. `.dmo-u-embedded-overlay`, used by `/design-laboratorio`) |

### 9.2 Module-specific CSS (10 files) — `wwwroot\styles\modules\`

`admin-layout.css`, `armazem-layout.css`, `controlo-layout.css`, `ferramentas-layout.css`, `jobon-layout.css`, `pegamentos-layout.css`, `peso-layout.css`, `reparacao-externa-layout.css`, `reparacao-interna-layout.css`, `tampoes-layout.css`.

### 9.3 Shared + module JS (12 files) — `wwwroot\scripts\`

- **Shared:** `dmo-calendar.js`, `dmo-interactions.js`.
- **Module-specific:** `jobon.js`, `peso.js`, `pegamentos.js`, `ferramentas.js`, `armazem.js`, `reparacao-externa.js`, `reparacao-interna.js`, `controlo.js`, `tampoes.js`, `boquilhas.js`.

### 9.4 Assets — `wwwroot\assets\`

`ba-logo.png` (shared brand asset).

## 10. Module-Specific Web Areas

Technical source ownership by folder (no functional-ownership claim beyond directory placement):

| Area | Razor pages (`Pages\`) | JS (`wwwroot\scripts\`) | Module CSS (`wwwroot\styles\modules\`) |
|---|---|---|---|
| Admin | `Admin\` (16 files) | — (server-rendered Razor) | `admin-layout.css` |
| Login | `Auth\` (4 files) | — (inline script + shared `dmo-interactions.js`) | — |
| Design Laboratório | `DesignLaboratorio\` (2 files) | — (inline script) | — |
| Job On | `JobOn\` | `jobon.js` | `jobon-layout.css` |
| Peso | `Peso\` | `peso.js` | `peso-layout.css` |
| Pegamentos | `Pegamentos\` | `pegamentos.js` | `pegamentos-layout.css` |
| Ferramentas | `Ferramentas\` | `ferramentas.js` | `ferramentas-layout.css` |
| Armazem | `Armazem\` | `armazem.js` | `armazem-layout.css` |
| Reparação Externa | `ReparacaoExterna\` | `reparacao-externa.js` | `reparacao-externa-layout.css` |
| Reparação Interna | `ReparacaoInterna\` | `reparacao-interna.js` | `reparacao-interna-layout.css` |
| Controlo | `Controlo\` | `controlo.js` | `controlo-layout.css` |
| Tampoes | `Tampoes\` | `tampoes.js` | `tampoes-layout.css` |
| Historia | `Historia\` | — (server-rendered) | — (shared `dmo-components.css` carries `.historia-*`) |
| Boquilhas | `Boquilhas\` | `boquilhas.js` | — (shared `dmo-components.css`/`dmo-tokens.css` carry `.boquilhas-*`) |

Admin pages and Design Laboratório/Login pages are classified by their source folders (Admin / Auth / DesignLaboratorio), not promoted to canonical modules. Shared shell pages (`_Layout`, `_Header`, `_Navigation`, `_AdminNav`) belong to the shared shell, not to any module.

## 11. Direct Web References

Mechanical, source-visible references inside Web (composition root → Application/Infrastructure; page → service/asset).

### 11.1 Program.cs composition references

```
Program.cs → CanonicalModuleCatalog.Instance, CanonicalPageCatalog.Instance (policies + catalog)
Program.cs → AccessResolver, NavigationService, IdentityResolutionService, RequestCurrentUserAccessor
Program.cs → ISupabaseAuthAdapter → SupabaseAuthAdapter
Program.cs → IAdminProvisioningAdapter → SupabaseAdminProvisioningAdapter
Program.cs → Application ports → Infrastructure implementations (full 1:1 mapping in MAP-19 §10)
Program.cs → /api/* endpoint groups → each module's Application service
Program.cs → Razor Pages fallback policy (AuthenticatedSessionRequirement), MapRazorPages
```

### 11.2 Page → service/asset references (representative)

```
Pages\JobOn\Index.cshtml        → CapabilityPolicies.JobonView; jobon.js; jobon-layout.css
Pages\Controlo\Index.cshtml     → ModulePolicies.Peso; controlo.js; controlo-layout.css
Pages\Admin\*\*.cshtml          → AdminPolicies.*; admin-layout.css
Pages\Auth\Login.cshtml         → [AllowAnonymous]; dmo-*.css; dmo-interactions.js; ba-logo.png
Pages\DesignLaboratorio\Index   → fallback session; dmo-*.css; dmo-calendar.js
Pages\Shared\_Layout.cshtml     → all dmo-*.css; dmo-calendar.js; dmo-interactions.js
```

### 11.3 Web-layer service composition

`Program.cs` is the composition root registering all Web/Application/Infrastructure services. It also hosts the CLI verbs (`CliMode`, `MigrateCommand`, `BootstrapAdminCommand`) used for migrate/bootstrap operations.

## 12. Target-to-Location Index

| Technical object | Location (`src\BA.Dmo.Web\`) |
|---|---|
| Composition root / API endpoints / policies / DI | `Program.cs` |
| AuthenticatedSession/Module/Capability handlers + policies | `Authorization\` |
| SessionClaims, RequestCurrentUserAccessor, CurrentUserAuthorshipAccessor | `Identity\` |
| RequestShellService | `Shell\RequestShellService.cs` |
| Shared shell partials | `Pages\Shared\_Layout/_Header/_Navigation/_AdminNav.cshtml` |
| Razor pages + PageModels | `Pages\**\*.cshtml` / `*.cshtml.cs` |
| Module/shell route `@page` declarations | `Pages\**\*.cshtml` |
| 128 minimal-API endpoints | `Program.cs` |
| Shared design-system CSS | `wwwroot\styles\dmo-*.css` |
| Module CSS | `wwwroot\styles\modules\*.css` |
| Shared JS | `wwwroot\scripts\dmo-calendar.js`, `dmo-interactions.js` |
| Module JS | `wwwroot\scripts\<module>.js` |
| Brand asset | `wwwroot\assets\ba-logo.png` |
| CLI verbs | `Cli\` |

## 13. Sources Verified

- `src\BA.Dmo.Web\BA.Dmo.Web.csproj` (references Application + Infrastructure; ships migrations content).
- Full recursive listing of `src\BA.Dmo.Web\` (excluding `bin\`/`obj\`) — all `.cs`, `.cshtml`, `.css`, `.js`, `.json`, `.png` enumerated.
- `src\BA.Dmo.Web\Program.cs` (services/policies lines 90–130; DI 131–269; `app = builder.Build()` 271; `app.MapRazorPages()` 279; 128 minimal-API endpoint mappings with `.RequireAuthorization`; route helpers).
- `src\BA.Dmo.Web\Authorization\` (3 handlers + policy constants).
- `src\BA.Dmo.Web\Identity\` (SessionClaims, RequestCurrentUserAccessor, CurrentUserAuthorshipAccessor).
- `src\BA.Dmo.Web\Shell\RequestShellService.cs`; `src\BA.Dmo.Web\Cli\` (4 files).
- `src\BA.Dmo.Web\Pages\` (all Razor pages/PageModels; `@page` routes and `[Authorize(Policy = …)]` attributes).
- `src\BA.Dmo.Web\wwwroot\` (5 root `dmo-*.css`, 10 module CSS, 12 JS, 1 `.png`).
- Cross-checked against `maps\06..18_*.md` (each module map inventories its Web/route/static detail) and `maps\19_APPLICATION.md` (App-layer port→Infrastructure mapping).
- No Design/SOT, AI-CONTEXT, or implementation behavior used as evidence; source-inspection only.

## Counts

- Composition root: **1** (`Program.cs`)
- Razor `.cshtml` files: **37** (incl. `_ViewImports.cshtml`, `_ViewStart.cshtml`)
- PageModel `.cshtml.cs` files: **29**
- Non-page Web classes: **2** (`JobOnLineColor.cs`, `ReparacaoExternaListBuilderModel.cs`)
- API / endpoint source (minimal-API route mappings in `Program.cs`): **128**
- Web authorization handler files: **3**
- Web identity/session files: **3**
- Shell service files: **1**
- CLI verb files: **4**
- Shared web service/composition files: **1** (`Program.cs`)
- Static CSS files: **15** (5 global `dmo-*.css` + 10 module CSS)
- Static JS files: **12** (2 shared + 10 module-specific)
- Static image/assets files: **1** (`ba-logo.png`)
- Razor pages by canonical-module ownership: JobOn 1, Peso 2, Pegamentos 2, Ferramentas 4, Armazem 1, ReparacaoExterna 2, ReparacaoInterna 1, Controlo 1, Tampoes 1, Historia 1, Boquilhas 1 (+ `_ReferenceList`/`_RepairListBuilder` partials)
- Admin pages: **16** files (8 pages × `.cshtml` + `.cshtml.cs`)
- Login pages: **4** files (Login.cshtml/.cs, Logout.cshtml/.cs)
- Design Laboratório page: **2** files (Index.cshtml/.cs)
- Shared shell files: **4** (`_Layout`, `_Header`, `_Navigation`, `_AdminNav`)
- Total relevant Web source files (`.cs` + `.cshtml`): **43 + 37 = 80** (source code, excluding static assets and config)