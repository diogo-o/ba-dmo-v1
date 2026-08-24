# BA DMO — Design Laboratório Technical Map

MAP ID: MAP-17
Status: COMPLETE

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
| Tests | 2 integration test classes directly targeting the laboratory surface; 3rd Design-folder test targets `jobon.js` (shared/external) |

## 3. Domain Objects

No dedicated Design Laboratório Domain type found.

`src\BA.Dmo.Domain\Modules\` contains no `DesignLaboratorio` folder, and greps for `DesignLaboratorio`, `Laboratorio`, `Laboratório` across `src\BA.Dmo.Domain` returned no Domain match. No Design-laboratório-specific entity, aggregate root, record, value object, enum, state, identifier, domain service or validation helper exists in current Domain source.

## 4. Application Objects

No dedicated Design Laboratório Application type found.

`src\BA.Dmo.Application\Modules\` contains no `DesignLaboratorio` folder, and greps for `DesignLaboratorio`, `Laboratorio`, `Laboratório` across `src\BA.Dmo.Application` returned no match. No service, interface/port, repository, command, query, model/DTO, projection, validator, parser, document contract, result/error code or module constant exists for Design Laboratório in current Application source.

## 5. Module / Page / Capability Identifiers

Literal source values, verified from `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs` and `CanonicalPageCatalog.cs` and `src\BA.Dmo.Application\Shared\Access\AccessResolver.cs`:

- `CanonicalModuleCatalog.Instance` declares **12** module entries. It declares **no** Design Laboratório module ID. There is no `design_laboratorio` module id const, display name, canonical order, initial route or capability.
- `CanonicalPageCatalog.Instance` declares **12** page entries. It declares **no** Design-laboratório page ID. No `design-laboratorio` page is registered.
- `CanonicalModuleCatalog.AreaChildren` maps no Design-laboratório children.
- `NavigationService` (`src\BA.Dmo.Application\Shared\Access\NavigationService.cs`) builds navigation from the canonical module/page catalogs only; it references no `design-laboratorio` route.
- `ModuleAuthorizationHandler` / `CapabilityAuthorizationHandler` (`src\BA.Dmo.Web\Authorization\`) declare no Design-laboratório module or capability policy.

Literal absence (not interpreted): the runtime access catalog does not contain a Design Laboratório entry; the laboratory page declares "it exists nowhere in the canonical catalogs and grants nothing." No forced entry is invented. The `/design-laboratorio` route exists only as a Razor Pages `@page` directive, not as a catalog route.

Counts: module IDs = **0** (Design-laboratório-specific; the canonical catalog has 12 but no entry for this module); capability IDs = **0**; page IDs = **0**.

## 6. Internal Technical Areas

Internal Areas: **None**

Current source does not subdivide Design Laboratório into internal technical areas. The single laboratory surface presents multiple component families (buttons, fields, pills, alerts, table, pagination, menu, calendar, modal, sidebar, toast) as catalog examples on one page; these are rendered component examples, not separately mapped technical areas. No internal area is promoted to a canonical module.

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

None of the migrations under `database\migrations\` (N01–N26) directly creates or alters a Design-laboratório-specific DB object. No migration exists for this module.

## 10. User Surface

**User Surface: Shared.**

`src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml` is a single shared rendered surface with no profile-specific variant. It is session-gated by the global fallback `AuthenticatedSessionRequirement` (in `src\BA.Dmo.Web\Program.cs`), not by any module or capability grant. The integration tests confirm the page renders for any authenticated session regardless of module grants: `DesignSystemGuardTests`'s fixture user carries only a `boquilhas` capability, and `ShellAndCalendarGuardTests`'s fixture user carries an empty `ModulesJson` — both still reach `/design-laboratorio` after login. User Surface source-verified: YES.

## 11. Web Pages / Routes

### Dedicated page files (2)

| File | Role |
|---|---|
| `src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml` | Surface markup; `@page "/design-laboratorio"`; renders the universal component/state catalog using only global `dmo-*` CSS classes; embeds a `<script>` block listening to the canonical `dmo:date-select` event |
| `src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml.cs` | `IndexModel : PageModel`; single `OnGet()` with no injected services, no handler logic |

### Route (1)

- `GET /design-laboratorio` → `Pages\DesignLaboratorio\Index.cshtml` / `IndexModel`.

There are no `[Authorize]` attributes and no `RequireAuthorization` declarations on the page or route in `Program.cs`; the route is secured solely by the Razor Pages fallback authorization policy (`AuthenticatedSessionRequirement`) wired in `src\BA.Dmo.Web\Program.cs` (`options.FallbackPolicy = ... .AddRequirements(new AuthenticatedSessionRequirement())`; `app.MapRazorPages()`). The page is not registered in the `CanonicalPageCatalog`, so `NavigationService` does not link it; it is reached by direct URL only.

Dedicated Design Laboratório routes: **1**.

### Shared web wiring the page renders within (not Design-laboratório-specific)

- `src\BA.Dmo.Web\Program.cs` — fallback session policy + `app.MapRazorPages()`; `UseStaticFiles` serves the shared assets.
- `src\BA.Dmo.Web\Authorization\AuthenticatedSessionHandler.cs` — `AuthenticatedSessionRequirement` / `AuthenticatedSessionHandler`, the fallback session gate.
- `src\BA.Dmo.Web\Pages\Shared\_Layout.cshtml` — application frame; canonical stylesheet load order (tokens → foundation → components → layout → utilities) and the shared `dmo-interactions.js` / `dmo-calendar.js` scripts served to every page including the laboratory.

## 12. Static Assets

Dedicated Design Laboratório static assets: **0**.

All static assets under `src\BA.Dmo.Web\wwwroot\` are shared design-system assets served to every page:

- `wwwroot\styles\dmo-*.css` — `dmo-tokens.css`, `dmo-foundation.css`, `dmo-components.css`, `dmo-layout.css`, `dmo-utilities.css` (the global `dmo-design-system` set; canonical load order). `dmo-utilities.css` declares the `.dmo-u-embedded-overlay` selector whose comment states it is used only by `/design-laboratorio`, but the file is a shared global stylesheet, so it is counted as a shared static asset, not a dedicated Design-laboratório file.
- `wwwroot\scripts\dmo-interactions.js`, `wwwroot\scripts\dmo-calendar.js` — the canonical interaction/calendar scripts served by `_Layout` and consumed by the laboratory page markup.
- `wwwroot\assets\ba-logo.png` — shared brand asset.

Shared static dependencies (counted): the 5 `dmo-*.css` files + 2 `dmo-*.js` scripts + the shared `ba-logo.png` brand asset are the shared static asset set the laboratory page renders with. No page-local CSS/JS file is dedicated to Design Laboratório.

## 13. Tests

Location: `tests\BA.Dmo.IntegrationTests\Design\`.

Test classes whose direct target is the Design Laboratório surface (`/design-laboratorio`):

| Test class | File | Direct target |
|---|---|---|
| `DesignSystemGuardTests` | `tests\BA.Dmo.IntegrationTests\Design\DesignSystemGuardTests.cs` | `LaboratoryPage_RequiresASession_AndRendersTheCatalog` asserts `/design-laboratorio` redirects anonymous to `/login` and returns the catalog (component markers) for an authenticated session; plus design-foundation token/load-order/single-system guards against the shared design-system source |
| `ShellAndCalendarGuardTests` | `tests\BA.Dmo.IntegrationTests\Design\ShellAndCalendarGuardTests.cs` | `LaboratoryPage_ConsumesTheCanonicalCalendar` asserts the laboratory renders the canonical `data-dmo-calendar` markup and consumes `dmo:date-select`; plus shell/calendar single-implementation guards |

`JobOnScriptSafetyGuardTests` (`tests\BA.Dmo.IntegrationTests\Design\JobOnScriptSafetyGuardTests.cs`) sits in the same `Design` folder but its direct target is the Job On module script (`wwwroot\scripts\jobon.js`); it does not reference `/design-laboratorio`. It is classified as shared/external for this module and is NOT counted as a Design-laboratório-specific test class.

Test classes (Design-laboratório-specific): **2**.

No unit-test classes target Design Laboratório in `tests\BA.Dmo.UnitTests\` (grep returned no Design-laboratório match there).

## 14. Test Doubles / Helpers

### In-file test fixture files (2)

Files that embed their own test doubles (WebApplicationFactory fixtures + in-file fakes) whose direct target is the laboratory surface:

| File | Embedded doubles |
|---|---|
| `tests\BA.Dmo.IntegrationTests\Design\DesignSystemGuardTests.cs` | `DesignFixture : WebApplicationFactory<Program>` (`Repository`, `ValidUser()`, `CreateTestClient()`, `ConfigureWebHost` with `ReplaceSingleton`); `FakeAuthAdapter : ISupabaseAuthAdapter`; `FakeJobOnRepository : IJobOnRepository`; `FakeIdentityRepository : IInternalUserRepository` |
| `tests\BA.Dmo.IntegrationTests\Design\ShellAndCalendarGuardTests.cs` | `LabFixture : WebApplicationFactory<Program>` (`Repository`, `ValidUser()`, `CreateTestClient()`, `ConfigureWebHost` with `ReplaceSingleton`); `FakeAuthAdapter : ISupabaseAuthAdapter`; `FakeIdentityRepository : IInternalUserRepository` |

Dedicated test support files: **0** (no separate fake/fixture support file; doubles are embedded in-file).

In-file test fixture files: **2**.

## 15. Direct Design Laboratório References

One edge per source-proven relationship:

- `IndexModel` (`Index.cshtml.cs`) → `PageModel` (Razor Pages base; no injected services)
- `Index.cshtml` (`@page "/design-laboratorio"`) → `IndexModel` (`@model`)
- `/design-laboratorio` route → fallback `AuthenticatedSessionRequirement` (Program.cs fallback policy, no explicit `RequireAuthorization`)
- `Index.cshtml` markup → shared `dmo-*` CSS classes (rendered in `_Layout` load order)
- `Index.cshtml` inline `<script>` → `dmo:date-select` custom event (dispatched by `wwwroot\scripts\dmo-calendar.js`)
- `Index.cshtml` markup → `data-dmo-calendar` / `data-calendar-*` attributes (consumed by `wwwroot\scripts\dmo-calendar.js`)
- `DesignSystemGuardTests` → `/design-laboratorio` (http request) and shared `wwwroot\styles\dmo-*.css` source
- `ShellAndCalendarGuardTests` → `/design-laboratorio` (http request) and shared `wwwroot\scripts\dmo-calendar.js` / `dmo-interactions.js` source

## 16. External Technical References

| Design Laboratório Object | External Technical Reference | Reference Type |
|---|---|---|
| `/design-laboratorio` (Razor page) | `Program.cs` fallback `AuthenticatedSessionRequirement` | web authorization wiring |
| `/design-laboratorio` (Razor page) | `Program.cs` `app.MapRazorPages()` | shared web wiring |
| `Index.cshtml` renders within | `Pages\Shared\_Layout.cshtml` (frame + stylesheet load order + shared scripts) | shared web dependency |
| `Index.cshtml` component classes | shared `wwwroot\styles\dmo-*.css` (5 files) | shared static dependency |
| `Index.cshtml` inline script / calendar | shared `wwwroot\scripts\dmo-calendar.js`, `dmo-interactions.js` | shared static dependency |
| Session gate (fallback policy) | `AuthenticatedSessionHandler` / `AuthenticatedSessionRequirement` | shared web authorization dependency |
| Canonical catalogs absence | `CanonicalModuleCatalog.Instance` (12 entries, none for this module) | literal absence (no catalog entry invented) |
| `DesignSystemGuardTests` / `ShellAndCalendarGuardTests` | shared `ISupabaseAuthAdapter`, `IInternalUserRepository`, `IJobOnRepository` (fakes) | test constructor/port dependency |
| `JobOnScriptSafetyGuardTests` (same Design folder) | `wwwroot\scripts\jobon.js` | module consumer (shared/external, not counted) |

## 17. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `IndexModel` (`OnGet`) | Web Pages | `src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml.cs` |
| `Index.cshtml` (surface, `@page "/design-laboratorio"`) | Web Pages | `src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml` |
| `/design-laboratorio` fallback session gate | Web | `src\BA.Dmo.Web\Program.cs` (FallbackPolicy + `MapRazorPages`) |
| `AuthenticatedSessionRequirement` / `AuthenticatedSessionHandler` | Web Authn | `src\BA.Dmo.Web\Authorization\AuthenticatedSessionHandler.cs` |
| `_Layout.cshtml` (frame + design-system load order) | Web Shell | `src\BA.Dmo.Web\Pages\Shared\_Layout.cshtml` |
| `dmo-tokens/foundation/components/layout/utilities.css` | Web shared static | `src\BA.Dmo.Web\wwwroot\styles\dmo-*.css` |
| `dmo-calendar.js`, `dmo-interactions.js` | Web shared static | `src\BA.Dmo.Web\wwwroot\scripts\` |
| `DesignSystemGuardTests`, `ShellAndCalendarGuardTests` | Tests | `tests\BA.Dmo.IntegrationTests\Design\` |
| Domain / Application / Infrastructure dedicated objects | — | none in current source |

## 18. Sources Verified

- `maps\00_INDEX.md` (binding contract; Design Laboratório row, surface order 17, COMPLETE)
- `src\BA.Dmo.Web\Pages\DesignLaboratorio\Index.cshtml`, `Index.cshtml.cs`
- `src\BA.Dmo.Web\Program.cs` (fallback authorization policy, `MapRazorPages`, static files)
- `src\BA.Dmo.Web\Authorization\AuthenticatedSessionHandler.cs`
- `src\BA.Dmo.Web\Pages\Shared\_Layout.cshtml`
- `src\BA.Dmo.Web\Shell\RequestShellService.cs`
- `src\BA.Dmo.Application\Shared\Access\CanonicalModuleCatalog.cs`, `CanonicalPageCatalog.cs`, `NavigationService.cs`, `AccessResolver.cs`
- `src\BA.Dmo.Web\wwwroot\styles\dmo-utilities.css` (and directory scan of `styles\dmo-*.css`, `styles\modules\`, `scripts\`, `assets\`)
- `tests\BA.Dmo.IntegrationTests\Design\DesignSystemGuardTests.cs`, `ShellAndCalendarGuardTests.cs`, `JobOnScriptSafetyGuardTests.cs`
- `database\migrations\*.sql` and `database\consolidated_clean_install.sql` (grepped for design/laboratorio; no match)
- `src\BA.Dmo.Infrastructure\Persistence\Migrations\` (directory inspected; no Design-laboratório object)
- `src\BA.Dmo.Domain\Modules\` and `src\BA.Dmo.Application\Modules\` (directory listing; no `DesignLaboratorio` folder)
- Global source greps for `DesignLaboratorio|DesignLaboratório|Laboratório|Laboratorio|Laboratory|design-laboratorio` across `src\`, `tests\`, `database\`

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
- Test classes: **2**
- Dedicated test support files: **0**
- In-file test fixture files: **2**
- Source-visible user surfaces: **1** (Shared)
- Internal technical areas: **0** (None)