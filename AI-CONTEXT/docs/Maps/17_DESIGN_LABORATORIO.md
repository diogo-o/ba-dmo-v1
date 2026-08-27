# BA DMO — Design Laboratório Technical Map

MAP ID: MAP-17
Status: COMPLETE

> Reconciliation note (HEAD 8478308): structure preserved; test inventory refreshed —
> the `Design` folder now holds **8** guard-class files (2 directly targeting the
> laboratory surface, 6 static structure/design guards for other modules), and the perceived
> "12 page entries" of the canonical page catalog are **13** (a second Peso page,
> `peso.responsavel`). Fixture-user claims updated from source. Migration range N01–N31.
>
> Cross-map links: [00_INDEX](00_INDEX.md) · [01_DOMAIN](01_DOMAIN.md) · [02_DATABASE](02_DATABASE.md) ·
> [03_MIGRATIONS](03_MIGRATIONS.md) · [04_DAPPER_INFRASTRUCTURE](04_DAPPER_INFRASTRUCTURE.md) ·
> [05_TESTS](05_TESTS.md) · [07_CONTROLO](07_CONTROLO.md) · [19_APPLICATION](19_APPLICATION.md) ·
> [20_WEB](20_WEB.md).

## Navigation Index

1. [Scope](#1-scope)
2. [Layer Summary](#2-layer-summary)
3. [Domain Objects](#3-domain-objects)
4. [Application Objects](#4-application-objects)
5. [Module / Page / Capability Identifiers](#5-module--page--capability-identifiers)
6. [Internal Technical Areas](#6-internal-technical-areas)
7. [Infrastructure Objects](#7-infrastructure-objects)
8. [Database Objects](#8-database-objects)
9. [Migration Touchpoints](#9-migration-touchpoints)
10. [User Surface](#10-user-surface)
11. [Web Pages / Routes](#11-web-pages--routes)
12. [Static Assets](#12-static-assets)
13. [Tests](#13-tests)
14. [Test Doubles / Helpers](#14-test-doubles--helpers)
15. [Direct Design Laboratório References](#15-direct-design-laboratório-references)
16. [External Technical References](#16-external-technical-references)
17. [Target-to-Layer Index](#17-target-to-layer-index)
18. [Sources Verified](#18-sources-verified)

## 1. Scope

This map is a pure technical inventory/navigation for the Design Laboratório **transversal / system surface** (`maps\17_DESIGN_LABORATORIO.md`, order 17). Design Laboratório is a technical design-system laboratory surface — it is NOT a canonical functional module. In current source, "Design Laboratório" resolves to a single Razor Pages design-system laboratory surface (`/design-laboratorio`) that renders the universal component/state catalog using the global `dmo-design-system` CSS. It is not a module route: the page declares that it exists nowhere in the canonical catalogs and grants nothing. Only current source is mapped; nothing is inferred or invented.

This scope covers:

- the dedicated Web page files under `src\BA.Dmo.Web\Pages\DesignLaboratorio\`;
- the session-gating it relies on (shared fallback authorization wiring in `Program.cs`);
- the shared design-system static assets it renders with;
- the tests whose direct target is the laboratory surface;
- the full `BA.Dmo.IntegrationTests\Design` folder inventory (all 8 guard classes), distinguishing the 2 classes that target `/design-laboratorio` from the 6 static structure/design guards that verify page/structure constraints of other modules (mapped here as shared/external, following the existing pattern);
- shared/external dependencies only where the laboratory page directly consumes them.

It does NOT absorb: the shared design-system CSS/JS as Design-laboratório-specific objects; the Admin/Login/shell surfaces; or any Design/Reference/SOT documentation. Design-Reference artifacts are excluded unless current technical source directly consumes them (none is consumed at runtime in scope).

## 2. Layer Summary

| Layer | Contents |
|---|---|
| Domain | **0** dedicated objects (no `Modules\DesignLaboratorio` folder; grep found no Design-laboratório Domain type) |
| Application | **0** dedicated objects (no `Modules\DesignLaboratorio` folder; no service/port/repository) |
| Infrastructure | **0** dedicated objects (no in-folder persistence/adapter; no DB involvement) |
| Database | **0** Design-laboratório-specific tables / indexes / triggers |
| Web | `Pages\DesignLaboratorio\Index.cshtml` + `Index.cshtml.cs` (route `/design-laboratorio`); gated by the shared fallback `AuthenticatedSessionRequirement` |
| Static | 0 dedicated files; renders with shared `wwwroot\styles\dmo-*.css` and `wwwroot\scripts\dmo-*.js` |
| Tests | `Design` integration folder: **8** guard classes. **2** directly target the laboratory surface (`DesignSystemGuardTests`, `ShellAndCalendarGuardTests`, 18 tests between them); **6** are static structure/design guards for other modules (`ArmazemBqGuardTests`, `ArmazemCorrectionGuardTests`, `ArmazemCreateGuardTests`, `ArmazemRecentMovementsGuardTests`, `PesoComparisonGuardTests`, `JobOnScriptSafetyGuardTests`, 20 tests) — shared/external for this module |

## 3. Domain Objects

No dedicated Design Laboratório Domain type found.

`src\BA.Dmo.Domain\Modules\` contains no `DesignLaboratorio` folder, and greps for `DesignLaboratorio`, `Laboratorio`, `Laboratório` across `src\BA.Dmo.Domain` returned no Domain match. No Design-laboratório-specific entity, aggregate root, record, value object, enum, state, identifier, domain service or validation helper exists in current Domain source.

## 4. Application Objects

No dedicated Design Laboratório Application type found.

`src\BA.Dmo.Application\Modules\` contains no `DesignLaboratorio` folder, and greps for `DesignLaboratorio`, `Laboratorio`, `Laboratório` across `src\BA.Dmo.Application` returned no match. No service, interface/port, repository, command, query, model/DTO, projection, validator, parser, document contract, result/error code or module constant exists for Design Laboratório in current Application source.

## 5. Module / Page / Capability Identifiers

Literal source values, verified from `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` and `CanonicalPageCatalog.cs` and `src\BA.Dmo.Application\Shared\Access\AccessResolver.cs`:

- `CanonicalModuleCatalog.Instance` declares **12** module entries. It declares **no** Design Laboratório module ID. There is no `design_laboratorio` module id const, display name, canonical order, initial route or capability.
- `CanonicalPageCatalog.Instance` declares **13** page entries (corrected from 12 — the count changed with the second Peso page `peso.responsavel`). It declares **no** Design-laboratório page ID. No `design-laboratorio` page is registered.
- `CanonicalModuleCatalog.AreaChildren` maps no Design-laboratório children (`AreaChildren` has a single key `controlo` → `[peso, pegamentos]`).
- `NavigationService` (`src\BA.Dmo.Application\Shared\Access\NavigationService.cs`) builds navigation from the canonical module/page catalogs only; it references no `design-laboratorio` route.
- `ModuleAuthorizationHandler` / `CapabilityAuthorizationHandler` (`src\BA.Dmo.Web\Authorization\`) declare no Design-laboratório module or capability policy.

Literal absence (not interpreted): the runtime access catalog does not contain a Design Laboratório entry; the laboratory page declares "it exists nowhere in the canonical catalogs and grants nothing." No forced entry is invented. The `/design-laboratorio` route exists only as a Razor Pages `@page` directive, not as a catalog route.

Counts: module IDs = **0** (Design-laboratório-specific; the canonical catalog has 12 but no entry for this module); capability IDs = **0**; page IDs = **0** (canonical page catalog: 13 entries, none for this module).

## 6. Internal Technical Areas

Internal Areas: **None**

Current source does not subdivide Design Laboratório into internal technical areas. The single laboratory surface presents multiple component families (buttons, fields, pills, alerts, table, pagination, menu, calendar, modal, sidebar, toast, skeleton, empty/error states, segmented control, tooltip, path-readonly, history-entry, form messages) as catalog examples on one page; these are rendered component examples, not separately mapped technical areas. No internal area is promoted to a canonical module.

## 7. Infrastructure Objects

No dedicated Design Laboratório Infrastructure object found.

`src\BA.Dmo.Infrastructure\` contains no Design-laboratório-specific repository, SQL class, adapter, settings, persistence mapping or HTTP client. The laboratory page performs no persistence, external-call or filesystem access.

Shared/external infrastructure dependencies (not counted as dedicated): the global shared persistence foundation under `src\BA.Dmo.Infrastructure\Persistence\` and the shared `/login` auth infrastructure are used by other pages; the Design-laboratório page itself declares none.

## 8. Database Objects

Design Laboratório-specific DB objects: **0**.

Greps for `design_laboratorio`, `design_lab`, `laboratorio` and `design_system` across `database\migrations\*.sql` and `database\consolidated_clean_install.sql` returned no Design-laboratório table. No table, index or trigger is created or altered for this module.

Counts: tables = **0**; indexes = **0**; triggers = **0**; DB objects = **0** (0 tables + 0 indexes + 0 triggers; constraints separated and also none).

Shared/external DB dependencies: None (the laboratory page reads no DB table; no shared DB dependency is consumed by this module).

## 9. Migration Touchpoints

Distinct Design Laboratório migration files: **0**.

None of the migrations under `database\migrations\` (N01–**N31**; 31 files, corrected from the previously stated N01–N26 — the inventory grew with N26–N31) directly creates or alters a Design-laboratório-specific DB object. No migration exists for this module.

## 10. User Surface

**User Surface: Shared.**

`src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml` is a single shared rendered surface with no profile-specific variant. It is session-gated by the global fallback `AuthenticatedSessionRequirement` (in `src\BA.Dmo.Web\Program.cs` lines 96–101, `options.FallbackPolicy = ... .AddRequirements(new AuthenticatedSessionRequirement())`), not by any module or capability grant. The integration tests confirm the page renders for any authenticated session regardless of module grants (updated fixture users, source-verified):

- `DesignSystemGuardTests.DesignFixture.ValidUser()` carries `ModulesJson: [{"moduleId":"jobon","capabilities":[]},{"moduleId":"boquilhas","capabilities":[]}]`, profile "Operador / Controlador", TemplateName "Design" — no `controlo` module, no design-related grant — `LaboratoryPage_RequiresASession_AndRendersTheCatalog` still reaches `/design-laboratorio` (200) after login (previously the map claimed "only a boquilhas capability"; the current fixture grants **jobon + boquilhas** modules with empty capability arrays).
- `ShellAndCalendarGuardTests.LabFixture.ValidUser()` carries `ModulesJson: [{"moduleId":"jobon","capabilities":[]}]` (previously the map claimed "an empty ModulesJson"; the current fixture grants the **jobon** module with no capabilities) — `LaboratoryPage_ConsumesTheCanonicalCalendar` reaches `/design-laboratorio` (200).

User Surface source-verified: YES.

## 11. Web Pages / Routes

### Dedicated page files (2)

| File | Role |
|---|---|
| `src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml` | Surface markup (289 lines); `@page "/design-laboratorio"`; renders the universal component/state catalog using only global `dmo-*` CSS classes; embeds a `<script>` block listening to the canonical `dmo:date-select` event and wiring the calendar clear button |
| `src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml.cs` | `IndexModel : PageModel`; single `OnGet()` with no injected services, no handler logic |

### Route (1)

- `GET /design-laboratorio` → `Pages\DesignLaboratorio\Index.cshtml` / `IndexModel`.

There are no `[Authorize]` attributes and no `RequireAuthorization` declarations on the page or route in `Program.cs`; the route is secured solely by the Razor Pages fallback authorization policy (`AuthenticatedSessionRequirement`) wired in `src\BA.Dmo.Web\Program.cs` (`options.FallbackPolicy = ... .AddRequirements(new AuthenticatedSessionRequirement())`; `app.MapRazorPages()`). The page is not registered in the `CanonicalPageCatalog`, so `NavigationService` does not link it; it is reached by direct URL only.

Dedicated Design Laboratório routes: **1**.

### Shared web wiring the page renders within (not Design-laboratório-specific)

- `src\BA.Dmo.Web\Program.cs` — fallback session policy (96–101) + `app.MapRazorPages()` (285); `UseStaticFiles` (281) serves the shared assets.
- `src\BA.Dmo.Web\Authorization\AuthenticatedSessionHandler.cs` — `AuthenticatedSessionRequirement` / `AuthenticatedSessionHandler`, the fallback session gate.
- `src\BA.Dmo.Web\Pages\Shared\_Layout.cshtml` — application frame; canonical stylesheet load order (tokens → foundation → components → layout → utilities, each `asp-append-version="true"`, plus `admin-layout.css` link) and the shared `dmo-interactions.js` / `dmo-calendar.js` deferred scripts served to every page including the laboratory (source-verified).

## 12. Static Assets

Dedicated Design Laboratório static assets: **0**.

All static assets under `src\BA.Dmo.Web\wwwroot\` are shared design-system assets served to every page:

- `wwwroot\styles\dmo-*.css` — `dmo-tokens.css`, `dmo-foundation.css`, `dmo-components.css`, `dmo-layout.css`, `dmo-utilities.css` (the global `dmo-design-system` set; canonical load order). `dmo-utilities.css` declares the `.dmo-u-embedded-overlay` selector whose comment states it is used only by `/design-laboratorio` (source-verified, line 41); the laboratory page consumes that class (Index.cshtml lines 182, 220, 265), but the file is a shared global stylesheet, so it is counted as a shared static asset, not a dedicated Design-laboratório file.
- `wwwroot\scripts\dmo-interactions.js`, `wwwroot\scripts\dmo-calendar.js` — the canonical interaction/calendar scripts served by `_Layout` and consumed by the laboratory page markup (calendar + `dmo:date-select`).
- `wwwroot\assets\ba-logo.png` — shared brand asset.

Shared static dependencies (counted): the 5 `dmo-*.css` files + 2 `dmo-*.js` scripts + the shared `ba-logo.png` brand asset are the shared static asset set the laboratory page renders with. No page-local CSS/JS file is dedicated to Design Laboratório.

## 13. Tests

Location: `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\` (tests re-based from the obsolete `tests\` prefix).

The `Design` folder contains **8** guard-class files (46 test methods). They fall into two groups.

### Group A — Test classes whose direct target is the Design Laboratório surface (`/design-laboratorio`):

| Test class | File | Test methods | Direct target |
|---|---|---|---|
| `DesignSystemGuardTests` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\DesignSystemGuardTests.cs` | 15 | `LaboratoryPage_RequiresASession_AndRendersTheCatalog` asserts `/design-laboratorio` redirects anonymous to `/login` and returns the catalog (component markers) for an authenticated session (fixture user: jobon + boquilhas modules); PLUS 14 static design-foundation guards: required token groups (`TokenFile_DefinesAllRequiredTokenGroups`), reduced motion (`ReducedMotion_IsImplemented`), exact semantic token values vs Design-Reference (`SemanticTokens_MatchTheDesignReferenceExactly` — incl. `--dmo-pill-approved-text #3f7765`), canonical `_Layout` load order exactly once (`Layout_WiresTheCanonicalLoadOrder_ExactlyOnce`), single design system / no legacy `site.css` (`SingleDesignSystem_NoCompetingLegacyCss` — pins the exact 5 `dmo-*.css` + 10 module-layout file set), tokens-only component layer (`SharedComponentLayer_ConsumesTokensOnly`), button state machine + typography (`ButtonStateMachine_FilledRestInvertedHover`, `Buttons_UseCanonicalTypographyAndCenteredLabels`), cross-page wiring examples (`Boquilhas_UsesCanonicalContextualSidebar`, `ReparacaoInterna_TypeChoice_PersistsAccessibleSelectedState`, `Logout_UsesTheCanonicalButtonAndStylesheets`, `ModuleTabs_UseOneSharedTypographyAndSizingContract`, `StylesheetLinks_AreFingerprintVersioned`, `Pages_ContainNoLocalDesignCss`) |
| `ShellAndCalendarGuardTests` | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\ShellAndCalendarGuardTests.cs` | 3 | `LaboratoryPage_ConsumesTheCanonicalCalendar` asserts the laboratory renders the canonical `data-dmo-calendar` markup and consumes `dmo:date-select` + both canonical scripts; PLUS 2 static guards: single canonical calendar implementation (CSS + exactly one `dmo-calendar.js`) and shell composition from the design system (`ShellComposition_UsesTheDesignSystem` — `_Header`/`_Navigation` anatomy; asserts navigation does NOT use `area.Children` because Peso/Pegamentos are internal) |

Test classes (Design-laboratório-specific): **2** (18 methods: 2 HTTP + 16 static foundation/shell guards).

### Group B — Static structure/design guards for OTHER modules (do NOT reference `/design-laboratorio`; shared/external for this module, following the existing `JobOnScriptSafetyGuardTests` classification pattern):

| Test class | File | Test methods | Target (module under guard) |
|---|---|---|---|
| `ArmazemBqGuardTests` | `ArmazemBqGuardTests.cs` | 1 | Armazém: `Pages\Armazem\Index.cshtml` type selectors expose `BQ` but not `PU`/`CS` |
| `ArmazemCorrectionGuardTests` | `ArmazemCorrectionGuardTests.cs` | 2 | Armazém: auditable location-correction card + `/api/armazem/corrigir-localizacao` + `ArmazemService.CorrectLocationAsync` |
| `ArmazemCreateGuardTests` | `ArmazemCreateGuardTests.cs` | 2 | Armazém/Ferramentas: two-owner create workflow (Ferramentas master first, Armazém Entrada second) in `armazem.js` |
| `ArmazemRecentMovementsGuardTests` | `ArmazemRecentMovementsGuardTests.cs` | 6 | Armazém: movement-backed recent/consulta/histórico surfaces, no demo rows (`9389T194`/`5447T173`), no L-prefix in visible lote, dormant "programadas" tab, compact/print CSS |
| `PesoComparisonGuardTests` | `PesoComparisonGuardTests.cs` | 3 | Peso (Controlo internal area): comparison UX contract in `Pages\Peso\Index.cshtml`/`Responsavel.cshtml`/`peso.js`/`PesoSingleFilePdfRenderer.cs`/`PesoService.cs` (pairing + explicit submit, glass-weight-only comparison, L-prefix reserved for filename) |
| `JobOnScriptSafetyGuardTests` | `JobOnScriptSafetyGuardTests.cs` | 6 | Job On script safety (`wwwroot\scripts\jobon.js` escaping of the operator-typed catalog label) + cross-module link guards on `controlo.js` (`params.get('jobOn')`/`params.get('section')`/`selectSection`) and `reparacao-interna.js` — it does NOT reference `/design-laboratorio` |

The 6 Group-B classes (20 methods) are classified here as **INTENTIONAL NORMALIZATION** — the `Design` folder acts as the repository-wide design/structure verification surface (U-08 contract checks), so other modules' static guard tests live alongside the laboratory tests; they are NOT Design-laboratório-specific artifacts and are not counted toward this module.

No unit-test classes target Design Laboratório in `AI-CONTEXT\docs\tests\BA.Dmo.UnitTests\` (grep returned no Design-laboratório match there).

## 14. Test Doubles / Helpers

### In-file test fixture files (2)

Files that embed their own test doubles (WebApplicationFactory fixtures + in-file fakes) whose direct target is the laboratory surface:

| File | Embedded doubles |
|---|---|
| `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\DesignSystemGuardTests.cs` | `DesignFixture : WebApplicationFactory<Program>` (`Repository` = `FakeIdentityRepository`, `ValidUser()` — jobon+boquilhas modules, `CreateTestClient()`, `ConfigureWebHost` with `ReplaceSingleton` of `ISupabaseAuthAdapter`/`IInternalUserRepository`/`IJobOnRepository` + `IgnoreAntiforgeryToken` convention); `FakeAuthAdapter : ISupabaseAuthAdapter`; `FakeJobOnRepository : IJobOnRepository`; `FakeIdentityRepository : IInternalUserRepository` |
| `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\ShellAndCalendarGuardTests.cs` | `LabFixture : WebApplicationFactory<Program>` (`Repository` = `FakeIdentityRepository`, `ValidUser()` — jobon module, `CreateTestClient()`, `ConfigureWebHost` with `ReplaceSingleton` of `ISupabaseAuthAdapter`/`IInternalUserRepository` + `IgnoreAntiforgeryToken` convention); `FakeAuthAdapter : ISupabaseAuthAdapter`; `FakeIdentityRepository : IInternalUserRepository` |

Dedicated test support files: **0** (no separate fake/fixture support file; doubles are embedded in-file).

In-file test fixture files: **2**.

## 15. Direct Design Laboratório References

One edge per source-proven relationship:

- `IndexModel` (`Index.cshtml.cs`) → `PageModel` (Razor Pages base; no injected services)
- `Index.cshtml` (`@page "/design-laboratorio"`) → `IndexModel` (`@model`)
- `/design-laboratorio` route → fallback `AuthenticatedSessionRequirement` (Program.cs fallback policy, no explicit `RequireAuthorization`)
- `Index.cshtml` markup → shared `dmo-*` CSS classes (rendered in `_Layout` load order; `dmo-u-embedded-overlay` in the global `dmo-utilities.css`)
- `Index.cshtml` inline `<script>` → `dmo:date-select` custom event (dispatched by `wwwroot\scripts\dmo-calendar.js`)
- `Index.cshtml` markup → `data-dmo-calendar` / `data-calendar-*` attributes (consumed by `wwwroot\scripts\dmo-calendar.js`)
- `DesignSystemGuardTests` → `/design-laboratorio` (http request) and shared `wwwroot\styles\dmo-*.css` + `_Layout.cshtml` source
- `ShellAndCalendarGuardTests` → `/design-laboratorio` (http request) and shared `wwwroot\scripts\dmo-calendar.js` / `dmo-interactions.js` / `_Layout.cshtml` / `_Header.cshtml` / `_Navigation.cshtml` source
- Group-B guards (shared/external, not module-specific): `ArmazemBqGuardTests`/`ArmazemCorrectionGuardTests`/`ArmazemCreateGuardTests`/`ArmazemRecentMovementsGuardTests` → `Pages\Armazem\*` + `wwwroot\scripts\armazem.js`; `PesoComparisonGuardTests` → `Pages\Peso\*` + `peso.js` + `PesoSingleFilePdfRenderer.cs` + `PesoService.cs`; `JobOnScriptSafetyGuardTests` → `wwwroot\scripts\jobon.js` / `controlo.js` / `reparacao-interna.js`

## 16. External Technical References

| Design Laboratório Object | External Technical Reference | Reference Type |
|---|---|---|
| `/design-laboratorio` (Razor page) | `Program.cs` fallback `AuthenticatedSessionRequirement` | web authorization wiring |
| `/design-laboratorio` (Razor page) | `Program.cs` `app.MapRazorPages()` | shared web wiring |
| `Index.cshtml` renders within | `Pages\Shared\_Layout.cshtml` (frame + stylesheet load order + shared scripts) | shared web dependency |
| `Index.cshtml` component classes | shared `wwwroot\styles\dmo-*.css` (5 files; canonical load order pinned by `DesignSystemGuardTests.Layout_WiresTheCanonicalLoadOrder_ExactlyOnce`) | shared static dependency |
| `Index.cshtml` inline script / calendar | shared `wwwroot\scripts\dmo-calendar.js`, `dmo-interactions.js` | shared static dependency |
| `Index.cshtml` `.dmo-u-embedded-overlay` | shared `wwwroot\styles\dmo-utilities.css` (selector comment: "used only by /design-laboratorio") | shared static dependency |
| Session gate (fallback policy) | `AuthenticatedSessionHandler` / `AuthenticatedSessionRequirement` | shared web authorization dependency |
| Canonical catalogs absence | `CanonicalModuleCatalog.Instance` (12 entries, none for this module); `CanonicalPageCatalog.Instance` (13 entries, none for this module) | literal absence (no catalog entry invented) |
| `DesignSystemGuardTests` | shared `ISupabaseAuthAdapter`, `IInternalUserRepository`, `IJobOnRepository` (fakes) | test constructor/port dependency |
| `ShellAndCalendarGuardTests` | shared `ISupabaseAuthAdapter`, `IInternalUserRepository` (fakes) | test constructor/port dependency |
| `JobOnScriptSafetyGuardTests` (same Design folder) | `wwwroot\scripts\jobon.js` + `controlo.js` + `reparacao-interna.js` | module consumers (shared/external, not counted) |
| `ArmazemBqGuardTests` / `ArmazemCorrectionGuardTests` / `ArmazemCreateGuardTests` / `ArmazemRecentMovementsGuardTests` (same Design folder) | `Pages\Armazem\*`, `wwwroot\scripts\armazem.js`, `Program.cs`, `ArmazemService.cs` | module consumers (shared/external, not counted) |
| `PesoComparisonGuardTests` (same Design folder) | `Pages\Peso\*`, `wwwroot\scripts\peso.js`, `PesoSingleFilePdfRenderer.cs`, `PesoService.cs` | module consumer — Controlo internal area Peso (shared/external, not counted) |

## 17. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `IndexModel` (`OnGet`) | Web Pages | `src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml.cs` |
| `Index.cshtml` (surface, `@page "/design-laboratorio"`) | Web Pages | `src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml` |
| `/design-laboratorio` fallback session gate | Web | `src\BA.Dmo.Web\Program.cs` (FallbackPolicy 96–101 + `MapRazorPages` 285) |
| `AuthenticatedSessionRequirement` / `AuthenticatedSessionHandler` | Web Authn | `src\BA.Dmo.Web\Authorization\AuthenticatedSessionHandler.cs` |
| `_Layout.cshtml` (frame + design-system load order) | Web Shell | `src\BA.Dmo.Web\Pages\Shared\_Layout.cshtml` |
| `dmo-tokens/foundation/components/layout/utilities.css` | Web shared static | `src\BA.Dmo.Web\wwwroot\styles\dmo-*.css` |
| `dmo-calendar.js`, `dmo-interactions.js` | Web shared static | `src\BA.Dmo.Web\wwwroot\scripts\` |
| `DesignSystemGuardTests`, `ShellAndCalendarGuardTests` (laboratory-direct) | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\` |
| `ArmazemBqGuardTests`, `ArmazemCorrectionGuardTests`, `ArmazemCreateGuardTests`, `ArmazemRecentMovementsGuardTests`, `PesoComparisonGuardTests`, `JobOnScriptSafetyGuardTests` (static guards, shared/external) | Tests | `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\` |
| Domain / Application / Infrastructure dedicated objects | — | none in current source |

## 18. Sources Verified

- `maps\00_INDEX.md` (binding contract; Design Laboratório row, surface order 17, COMPLETE; transversal/system-surface classification).
- `src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml`, `Index.cshtml.cs` (read completely).
- `src\BA.Dmo.Web\Program.cs` (fallback authorization policy lines 96–101, `AddPolicy` loop 105–131, Peso/Pegamentos DI 192–207, Controlo DI 244–250, `UseStaticFiles` 279–281, `MapRazorPages` 285).
- `src\BA.Dmo.Web\Authorization\AuthenticatedSessionHandler.cs` (referenced requirement/handler pair; map text carries no unverified claim).
- `src\BA.Dmo.Web\Pages\Shared\_Layout.cshtml` (read completely: 5 `dmo-*.css` + `admin-layout.css` links; `dmo-interactions.js`/`dmo-calendar.js` deferred scripts).
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`, `AccessResolver.cs` (read completely; 12 module entries, 13 page entries, no design entry).
- `src\BA.Dmo.Web\wwwroot\styles\dmo-utilities.css` (line 41 comment on `.dmo-u-embedded-overlay`); page usage of `dmo-u-embedded-overlay` at Index.cshtml lines 182/220/265.
- `AI-CONTEXT\docs\tests\BA.Dmo.IntegrationTests\Design\DesignSystemGuardTests.cs` (574 lines, read completely), `ShellAndCalendarGuardTests.cs` (read completely), `ArmazemBqGuardTests.cs`, `ArmazemCorrectionGuardTests.cs`, `ArmazemCreateGuardTests.cs`, `ArmazemRecentMovementsGuardTests.cs`, `PesoComparisonGuardTests.cs`, `JobOnScriptSafetyGuardTests.cs` (all read completely).
- `database\migrations\*.sql` and `database\consolidated_clean_install.sql` (grepped for design/laboratorio; no match; full migration inventory N01–N31).
- `src\BA.Dmo.Infrastructure\Persistence\Migrations\` (directory inspected; no Design-laboratório object).
- `src\BA.Dmo.Domain\Modules\` and `src\BA.Dmo.Application\Modules\` (directory listing; no `DesignLaboratorio` folder).
- Global source greps for `DesignLaboratorio|DesignLaboratório|Laboratório|Laboratorio|Laboratory|design-laboratorio` across `src\`, `AI-CONTEXT\docs\tests\`, `database\`.

## Counts

- Domain Design Laboratório files: **0**
- Application Design Laboratório files: **0**
- Infrastructure Design Laboratório files: **0**
- Shared / external infrastructure dependencies: **0** (the page depends on no dedicated/shared Infrastructure-layer object; the fallback session handler is a Web authorization dependency)
- Dedicated Web page files: **2**
- Dedicated API / endpoint files: **0**
- Dedicated routes: **1** (`/design-laboratorio`)
- Dedicated static asset files: **0**
- Shared web wiring files: **3** (Program.cs, AuthenticatedSessionHandler.cs, `_Layout.cshtml`)
- Shared static asset files: **8** (5 `dmo-*.css` + 2 `dmo-*.js` + 1 `ba-logo.png`)
- Design-laboratório-specific DB tables: **0**
- Design-laboratório-specific DB indexes: **0**
- Design-laboratório-specific DB triggers: **0**
- Design-laboratório-specific DB objects: **0** (0 tables + 0 indexes + 0 triggers)
- Shared / external DB dependencies: **None**
- Distinct Design-laboratório migration files: **0**
- Module IDs (Design-laboratório-specific): **0**
- Capability IDs (Design-laboratório-specific): **0**
- Page IDs (Design-laboratório-specific): **0**
- Canonical page catalog entries: **13** (none for this module; corrected from 12 — `peso.responsavel` added)
- Design-folder test classes: **8** (2 laboratory-direct + 6 static guards, shared/external)
- Test classes (Design-laboratório-specific): **2** — `DesignSystemGuardTests` (15 methods), `ShellAndCalendarGuardTests` (3 methods)
- Static structure/design guard classes (shared/external): **6** — `ArmazemBqGuardTests` (1), `ArmazemCorrectionGuardTests` (2), `ArmazemCreateGuardTests` (2), `ArmazemRecentMovementsGuardTests` (6), `PesoComparisonGuardTests` (3), `JobOnScriptSafetyGuardTests` (6)
- Dedicated test support files: **0**
- In-file test fixture files: **2**
- Source-visible user surfaces: **1** (Shared)
- Internal technical areas: **0** (None)