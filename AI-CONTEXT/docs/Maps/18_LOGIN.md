# BA DMO — Login Technical Map

MAP ID: MAP-18
Status: COMPLETE

## Navigation Index

1. [Scope](#1-scope)
2. [Layer Summary](#2-layer-summary)
3. [Domain Objects](#3-domain-objects)
4. [Application Objects](#4-application-objects)
5. [Infrastructure Objects](#5-infrastructure-objects)
6. [Database Objects](#6-database-objects)
7. [Migration Touchpoints](#7-migration-touchpoints)
8. [User Surface](#8-user-surface)
9. [Web Pages / Routes](#9-web-pages--routes)
10. [Login Form / Handlers](#10-login-form--handlers)
11. [Session / Cookie Wiring](#11-session--cookie-wiring)
12. [Static Assets](#12-static-assets)
13. [Tests](#13-tests)
14. [Test Doubles / Helpers](#14-test-doubles--helpers)
15. [Direct Login References](#15-direct-login-references)
16. [External Technical References](#16-external-technical-references)
17. [Target-to-Layer Index](#17-target-to-layer-index)
18. [Sources Verified](#18-sources-verified)

## 1. Scope

This map is a pure technical inventory/navigation for the Login **transversal / system surface** (order 18). Login is the application authentication surface — it is NOT a canonical functional module. In current source Login resolves to the dedicated application-authentication Razor Pages surface under `src\BA.Dmo.Web\Pages\Auth\`: the `/login` sign-in page and the `/logout` sign-out page. Login declares no dedicated Domain, Application, Infrastructure, Database or migration objects; it consumes only shared Identity/Access/Session machinery and the shared auth provider adapter.

Scope covered:

- the dedicated Login/Logout Razor page files, routes and PageModels;
- Login form/handler mechanics local to the page;
- direct session/cookie references Login makes to the shared web session infrastructure (`SessionClaims`, `HttpContext.SignInAsync`/`SignOutAsync`, `Program.cs` cookie options);
- auth/session application ports consumed directly by Login (`ISupabaseAuthAdapter`, `IdentityResolutionService`);
- the shared auth-provider adapter runtime reference (`SupabaseAuthAdapter`);
- the shared shell/auth routes Login redirects to (`/no-access`, `/login`, and indirectly `/access-denied`);
- the shared static assets the Login page renders with.

It does NOT absorb the shared Users / Access architecture (MAP-16), the Admin module (MAP-15), or the Design Laboratório surface (MAP-17). Shared Users / Access objects are referenced only where Login directly consumes them.

## 2. Layer Summary

| Layer | Contents |
|---|---|
| Domain | **0** dedicated objects (no `Modules\Login` folder; no Login Domain type) |
| Application | **0** dedicated objects (no `Modules\Login` folder; consumes shared Identity ports) |
| Infrastructure | **0** dedicated objects (auth adapter is shared, MAP-16) |
| Database | **0** Login-specific tables / indexes / triggers |
| Web | `Pages\Auth\Login.cshtml` + `Login.cshtml.cs` (route `/login`), `Pages\Auth\Logout.cshtml` + `Logout.cshtml.cs` (route `/logout`) |
| Static | 0 dedicated files; renders with shared `wwwroot\styles\dmo-*.css`, `assets\ba-logo.png`, `scripts\dmo-interactions.js` |
| Tests | 3 integration test classes whose direct target is the Login auth surface |

## 3. Domain Objects

No dedicated Login Domain type found.

`src\BA.Dmo.Domain\Modules\` contains no `Login` folder, and greps for `Login`/`login`/`LoginModel` across `src\BA.Dmo.Domain` returned no Login-specific domain type. No Login entity, record, value object, enum, state, identifier, error or validation helper exists in current Domain source. The shared `CurrentUser`, `DomainError`, `Result<T, DomainError>` and `ErrorCategory` used around the Login path are shared Domain kernel/access objects (mapped in MAP-16), not Login-specific.

## 4. Application Objects

No dedicated Login Application type found.

`src\BA.Dmo.Application\Modules\` contains no `Login` folder. No Login service, command/query, request/result model, validator, application abstraction, error/result mapping or landing/redirect abstraction exists. Login directly consumes the following shared Application Identity/Access ports (all mapped in MAP-16 as shared; referenced here only as Login direct dependencies):

| Shared Application object | File | What Login consumes |
|---|---|---|
| `ISupabaseAuthAdapter.SignInWithPasswordAsync` → `Result<AuthUser, DomainError>` | `Shared\Identity\SupabaseAuthPorts.cs` | credential verification in `LoginModel.OnPostAsync` |
| `AuthUser` (record `AuthUserId`, `Email`) | `Shared\Identity\SupabaseAuthPorts.cs` | `signIn.Value.AuthUserId` read for the session claim |
| `IdentityResolutionService.ResolveAsync(Guid)` → `Result<ResolvedIdentity, DomainError>` | `Shared\Identity\IdentityResolutionService.cs` | post-login destination resolution in `LoginModel.OnPostAsync` |
| `ResolvedIdentity.FirstPage` (`FirstPageResolution`) | `Shared\Identity\IdentityResolutionService.cs` | `resolution.Value.FirstPage.Page` navigation in `LoginModel.OnPostAsync` |
| `FirstPageResolution` / `FirstPageOutcome` / `AccessResolver.ResolveFirstPage` | `Shared\Access\AccessResolver.cs` | produces `FirstPage.Page` (route) consumed by Login |
| `DomainError`, `ErrorCategory` (`.BackendUnavailable`) | Domain `Shared\Kernel` | error-branch selection in `LoginModel.OnPostAsync` |

Dedicated Application objects = **0**; the ports above are mapped as shared dependencies.

## 5. Infrastructure Objects

No dedicated Login Infrastructure object found.

Login's runtime auth provider implementation is the shared `SupabaseAuthAdapter` (`src\BA.Dmo.Infrastructure\Auth\SupabaseAuthAdapter.cs`), already classified as a shared Users / Access Infrastructure object in MAP-16. Login does not define its own adapter, settings, HttpClient or endpoint configuration.

### Shared / external infrastructure reference (consumed by Login runtime)

| Object | Kind | Path | Classification |
|---|---|---|---|
| `SupabaseAuthAdapter` | sealed class, implements `ISupabaseAuthAdapter` | `src\BA.Dmo.Infrastructure\Auth\SupabaseAuthAdapter.cs` | shared Infrastructure (MAP-16), registered singleton in `Program.cs` for the Login path |
| `SupabaseSettings` | static config class (`BA_DMO_SUPABASE_URL`, `BA_DMO_SUPABASE_ANON_KEY`) | `src\BA.Dmo.Infrastructure\Auth\SupabaseSettings.cs` | shared Infrastructure config; `Program.cs` uses `ResolveUrl`/`ResolveAnonKey` when constructing the registered `SupabaseAuthAdapter` |

`SupabaseAuthAdapter` calls the Supabase Auth REST anon endpoint `POST /auth/v1/token?grant_type=password`; it is the shared provider adapter, not Login-specific. Dedicated Infrastructure Login files = **0**.

## 6. Database Objects

Login-specific DB objects: **0**.

Greps for `login` across `database\migrations\*.sql` and `database\consolidated_clean_install.sql` returned no Login table. No table, index, trigger or constraint is created or altered for Login.

Shared / external DB references relevant for navigation only (not Login-dedicated):

| Reference | Source | Why relevant to Login |
|---|---|---|
| `internal_users` (with `auth_user_id`) | `N01_identity.sql` (+ N25) | the internal identity resolved after credential acceptance via `IdentityResolutionService` |
| `access_templates` | `N01_identity.sql` | grant source in `IdentityResolutionService` resolution |
| Supabase `auth.users` | external (Supabase Auth) | external provider identity; no local FK; linked logically by `internal_users.auth_user_id` |

None of these is a Login-specific DB object.

## 7. Migration Touchpoints

Distinct Login migration files: **0**.

None of the migrations under `database\migrations\` (N01–N26) directly creates or alters a Login-specific DB object. N01/N25/N26 create/alter shared identity tables (`internal_users`, `access_templates`) that participate in post-auth identity resolution, but they are shared Users / Access objects (MAP-16/MAP-03), not Login-specific.

## 8. User Surface

**User Surface: Shared.**

`src\BA.Dmo.Web\Pages\Auth\Login.cshtml` is a single shared rendered sign-in surface with no profile-specific variant. The source contains no separate admin/operator/responsável/authenticated-anonymous Login rendering; `Login.cshtml` implements one form (`data-dmo-login`) used by every session. Authentication state and the apply of the global fallback session gate are not user-surface subdivisions. The `Logout.cshtml` page is likewise a single shared surface. User Surface source-verified: YES.

## 9. Web Pages / Routes

### Dedicated Login page files (4)

| File | Role |
|---|---|
| `src\BA.Dmo.Web\Pages\Auth\Login.cshtml` | Sign-in surface; `@page "/login"`; `[AllowAnonymous]`; `Layout = null` (outside the application shell); renders the identity panel + login form |
| `src\BA.Dmo.Web\Pages\Auth\Login.cshtml.cs` | `LoginModel : PageModel`; `[AllowAnonymous]`; `OnGet()`, `OnPostAsync(email, password)` |
| `src\BA.Dmo.Web\Pages\Auth\Logout.cshtml` | Sign-out surface; `@page "/logout"`; `[AllowAnonymous]`; `Layout = null`; POST-only confirmation form |
| `src\BA.Dmo.Web\Pages\Auth\Logout.cshtml.cs` | `LogoutModel : PageModel`; `[AllowAnonymous]`; `OnGet()`, `OnPostAsync()` |

### Route classification

| Route | Files | Classification | Relevant handlers |
|---|---|---|---|
| `/login` | `Pages\Auth\Login.cshtml` / `Login.cshtml.cs` | **Dedicated Login** | `LoginModel.OnGet`, `OnPostAsync` |
| `/logout` | `Pages\Auth\Logout.cshtml` / `Logout.cshtml.cs` | **Dedicated Login** | `LogoutModel.OnGet`, `OnPostAsync` |
| `/access-denied` | `Pages\AccessDenied.cshtml` / `AccessDenied.cshtml.cs` | **shared auth/shell** (safe state) | `AccessDeniedModel.OnGetAsync` |
| `/no-access` | `Pages\NoAccess.cshtml` / `NoAccess.cshtml.cs` | **shared auth/shell** (safe state) | `NoAccessModel.OnGet` |
| `/` | `Pages\Index.cshtml` / `Index.cshtml.cs` | **shared auth/shell** (redirect endpoint) | `IndexModel.OnGetAsync` |

Dedicated Login routes: **2** (`/login`, `/logout`).

`/no-access` is a direct `LoginModel` redirect target (`/no-access` and `/no-access?indisponivel=1`). `/access-denied` is the shared cookie `AccessDeniedPath` configured in `Program.cs`. `/` is a shared shell/root redirect endpoint inspected for boundary/context and is not counted as a route directly referenced by Login-specific code. `/login` remains the dedicated Login route used as the Logout redirect target.

Dedicated API / endpoint files: **0**. There is no `/api/login` route; Login is Razor page only.

## 10. Login Form / Handlers

### Login.cshtml form structure (`@page "/login"`)

- Bound view property: `Model.Email` (`[BindProperty] string? Email`) re-rendered in `name="email"` input on failed POST. Password is intentionally not bound — it is only the handler argument `password`, never stored or rendered.
- Form fields: `input[name=email]` (`type=email`, `autocomplete=username`), `input[name=password]` (`type=password`, `autocomplete=current-password`, `password-wrap` with `data-dmo-password-toggle="password"` toggling button), submit button `data-dmo-login-submit`.
- `@Html.AntiForgeryToken()` present.
- Error slot: `<div class="dmo-form-message" role="alert">@Model.ErrorMessage</div>` rendered only when `Model.ErrorMessage` is non-blank.
- Embedded inline `<script>` (DOMContentLoaded) disables the submit trigger and shows "A entrar…" on submit; not a dedicated static file.
- `data-dmo-login` marker consumed by the shared `dmo-interactions.js`.

### LoginModel `OnGet()`
- No logic; renders the sign-in form.

### LoginModel `OnPostAsync(string email, string password)` — local mechanics
- If `email`/`password` blank → sets `ErrorMessage = "Credenciais inválidas."`, returns `Page()`.
- Calls `_authAdapter.SignInWithPasswordAsync(email, password, HttpContext.RequestAborted)`.
- On `signIn.IsFailure`: logs the provider reason server-side; sets `ErrorMessage` to "Autenticação temporariamente indisponível…" when `signIn.Error.Category == ErrorCategory.BackendUnavailable`, otherwise "Credenciais inválidas."; returns `Page()`.
- On success (`signIn.Value.AuthUserId`): constructs `new ClaimsIdentity([new Claim(SessionClaims.AuthUserIdClaimType, signIn.Value.AuthUserId.ToString())], SessionClaims.AuthenticationScheme)`; calls `HttpContext.SignInAsync(SessionClaims.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true, AllowRefresh = true })`.
- Calls `_resolutionService.ResolveAsync(signIn.Value.AuthUserId, HttpContext.RequestAborted)`.
- If `resolution.IsSuccess` and `resolution.Value.FirstPage.Page is not null` → `return Redirect(resolution.Value.FirstPage.Page.Route)`.
- If `resolution.IsFailure`:
  - logs the resolution error server-side;
  - if `resolution.Error.Category == ErrorCategory.BackendUnavailable` → `return Redirect("/no-access?indisponivel=1")`.
- Fall-through → `return Redirect("/no-access")`.

### LogoutModel `OnGet()` / `OnPostAsync()`
- `OnGet` renders the confirmation form.
- `OnPostAsync`: `await HttpContext.SignOutAsync(SessionClaims.AuthenticationScheme)`; returns `Redirect("/login")`.

## 11. Session / Cookie Wiring

### A. Direct Login references to shared session infrastructure

- `LoginModel.OnPostAsync` → `SessionClaims.AuthenticationScheme` ("BaDmo.Session") and `SessionClaims.AuthUserIdClaimType` ("ba_dmo.auth_user_id") when building the identity.
- `LoginModel.OnPostAsync` → `HttpContext.SignInAsync(...)` creating the session cookie.
- `LogoutModel.OnPostAsync` → `HttpContext.SignOutAsync(SessionClaims.AuthenticationScheme)` clearing the cookie.

### B. Shared `Program.cs` / `SessionClaims` wiring (source-grounded, shared)

`SessionClaims` (`src\BA.Dmo.Web\Identity\SessionClaims.cs`):
- `AuthenticationScheme = "BaDmo.Session"`
- `AuthUserIdClaimType = "ba_dmo.auth_user_id"`

`Program.cs` cookie authentication options (`AddCookie(SessionClaims.AuthenticationScheme, ...)`, lines 77–89):
- `LoginPath = "/login"`
- `LogoutPath = "/logout"`
- `AccessDeniedPath = "/access-denied"`
- `SlidingExpiration = true`
- `ExpireTimeSpan = TimeSpan.FromHours(8)`
- `Cookie.HttpOnly = true`
- `Cookie.SameSite = SameSiteMode.Lax`
- `Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest`

Related shared wiring used around the Login path: the fallback authorization policy requiring `AuthenticatedSessionRequirement` (`Program.cs` lines 90–95); `ISupabaseAuthAdapter` registered singleton as `new SupabaseAuthAdapter(new HttpClient(), ...)` with `SupabaseSettings.ResolveUrl`/`ResolveAnonKey` (`Program.cs` lines 151–154); `IdentityResolutionService` registered scoped (line 148). These are shared composition-root wiring (MAP-16), referenced here as the session/auth foundation Login runs within.

## 12. Static Assets

Dedicated Login static asset files: **0**. The Login page embeds its submit-state script directly in `Login.cshtml`; the password reveal is driven by the shared `dmo-interactions.js` (`data-dmo-password-toggle`), not a page-specific file.

### Shared static dependencies consumed by Login

| Asset | Path | Login use |
|---|---|---|
| `dmo-tokens.css` | `wwwroot\styles\dmo-tokens.css` | rendered in Login `<head>` |
| `dmo-foundation.css` | `wwwroot\styles\dmo-foundation.css` | rendered in Login `<head>` |
| `dmo-components.css` | `wwwroot\styles\dmo-components.css` | rendered in Login `<head>` |
| `dmo-layout.css` | `wwwroot\styles\dmo-layout.css` | rendered in Login `<head>` |
| `dmo-utilities.css` | `wwwroot\styles\dmo-utilities.css` | rendered in Login `<head>` |
| `ba-logo.png` | `wwwroot\assets\ba-logo.png` | `img.brand-logo` in the identity panel |
| `dmo-interactions.js` | `wwwroot\scripts\dmo-interactions.js` | shared password-toggle behavior |

Shared static asset files: **7**. All are shared design-system (MAP-17) / brand assets; none is Login-specific.

## 13. Tests

Test classes whose direct target is the Login authentication surface (login/logout page handling, session cookie, post-login landing, sign-in call-site):

| Test class | File | Direct target |
|---|---|---|
| `WebAuthSessionTests` | `tests\BA.Dmo.IntegrationTests\Identity\WebAuthSessionTests.cs` | `/login` GET public, POST sign-in → `/jobon`/`/admin` landing with session cookie, failed login stays on form + preserves email + no password render, provider-outage message, `/no-access` (plain + `?indisponivel=1`) post-login, `/logout` → `/login`, ReturnUrl non-use |
| `IdentityAmbiguityLandingTests` | `tests\BA.Dmo.IntegrationTests\Identity\IdentityAmbiguityLandingTests.cs` | `/login` POST landing on plain `/no-access` (never `?indisponivel=1`) for an ambiguous identity; real repository failure → `?indisponivel=1` |
| `NoDebugBypassGuardTests` | `tests\BA.Dmo.IntegrationTests\Security\NoDebugBypassGuardTests.cs` | source/composition guard; asserts exactly one `.SignInAsync(` call site, pinned to `Pages\Auth\Login.cshtml.cs`, and no `#if DEBUG` auth path (scans `Program.cs`, `Pages\Auth\*.cs`, `Cli\*.cs`) |

Test classes (direct Login target): **3**.

Note: `WebAuthSessionTests` and `IdentityAmbiguityLandingTests` are also counted in `maps\16_USERS_ACCESS.md` (MAP-16) as shared session/identity contract tests by their session-identity classification. For 18_LOGIN.md they are counted by direct `/login` page target; the reference-type classification is per-map and does not double-count a Login dedicated object.

Other integration tests (`DesignSystemGuardTests`, `ShellAndCalendarGuardTests`, `AdminWebAuthorizationTests`, `AdminFormAntiforgeryTests`, `AdminUserListResetTests`, module `*WebApiTests`, `ShellRoutingTests`, `JobOnLandingTests`) use `/login` as a fixture log-in helper, but their direct target is another module or the shared shell/design surface — not Login. No Login-specific Unit test exists in `tests\BA.Dmo.UnitTests\`.

## 14. Test Doubles / Helpers

### Dedicated test support files

Dedicated test support files: **0**. No separate fake/fixture file is dedicated to Login; the doubles used by the Login-targeting tests are embedded in-file.

### In-file test fixture files (2)

| File | Embedded doubles / fixtures |
|---|---|
| `tests\BA.Dmo.IntegrationTests\Identity\WebAuthSessionTests.cs` | `AuthTestFixture : WebApplicationFactory<Program>`; in-file `FakeAuthAdapter : ISupabaseAuthAdapter`; `FakeIdentityRepository : IInternalUserRepository` |
| `tests\BA.Dmo.IntegrationTests\Identity\IdentityAmbiguityLandingTests.cs` | `AmbiguityFixture : WebApplicationFactory<Program>`; in-file `FakeAuthAdapter : ISupabaseAuthAdapter`; `FakeIdentityRepository : IInternalUserRepository` |

`NoDebugBypassGuardTests` uses no WebApplicationFactory fixture (source-inspection guard) and is not an in-file fixture file.

In-file test fixture files: **2**.

## 15. Direct Login References

One edge per source-proven relationship:

- `/login` → `LoginModel` (`Login.cshtml` `@page "/login"` + `@model`)
- `Login.cshtml` → `LoginModel` (`@model`), shared `dmo-*.css` (5), `ba-logo.png`, `dmo-interactions.js`
- `/logout` → `LogoutModel` (`Logout.cshtml` `@page "/logout"` + `@model`)
- `LoginModel` → `ISupabaseAuthAdapter` (constructor)
- `LoginModel` → `IdentityResolutionService` (constructor)
- `LoginModel` → `ILogger<LoginModel>` (constructor, optional)
- `LoginModel.OnPostAsync` → `_authAdapter.SignInWithPasswordAsync`
- `LoginModel.OnPostAsync` → `AuthUser.AuthUserId` (`signIn.Value.AuthUserId`)
- `LoginModel.OnPostAsync` → `SessionClaims.AuthUserIdClaimType`, `SessionClaims.AuthenticationScheme`
- `LoginModel.OnPostAsync` → `HttpContext.SignInAsync`
- `LoginModel.OnPostAsync` → `_resolutionService.ResolveAsync`
- `LoginModel.OnPostAsync` → `ResolvedIdentity.FirstPage.Page.Route` (redirect)
- `LoginModel.OnPostAsync` → `Redirect("/no-access")`, `Redirect("/no-access?indisponivel=1")`
- `LogoutModel.OnPostAsync` → `HttpContext.SignOutAsync(SessionClaims.AuthenticationScheme)`
- `LogoutModel.OnPostAsync` → `Redirect("/login")`
- `Program.cs` → LoginPath `/login`, LogoutPath `/logout`, AccessDeniedPath `/access-denied`
- `Program.cs` → `ISupabaseAuthAdapter` singleton registered as `SupabaseAuthAdapter`
- `Program.cs` → `ISupabaseAuthAdapter` → `SupabaseSettings.ResolveUrl`/`ResolveAnonKey`
- `Program.cs` → `IdentityResolutionService` scoped
- `NoAccessModel.OnGet` → reads `?indisponivel` query (shared safe-state consumed by Login redirect)

## 16. External Technical References

| Login Object | External Technical Reference | Reference Type |
|---|---|---|
| `LoginModel` | `ISupabaseAuthAdapter` (`Shared\Identity\SupabaseAuthPorts.cs`) | shared application identity dependency |
| `LoginModel` | `SupabaseAuthAdapter` (`Infrastructure\Auth\SupabaseAuthAdapter.cs`) | shared infrastructure implementation (runtime for the port) |
| `LoginModel` | `SupabaseSettings` (`BA_DMO_SUPABASE_URL` / `BA_DMO_SUPABASE_ANON_KEY`) | shared infrastructure configuration |
| `LoginModel` | `IdentityResolutionService` / `ResolvedIdentity` / `FirstPageResolution` (`Shared\Identity`, `Shared\Access`) | shared identity dependency |
| `LoginModel` | `AuthUser` / `DomainError` / `ErrorCategory` (`Shared\Identity`, Domain `Shared\Kernel`) | shared domain dependency |
| `LoginModel` | `SessionClaims` (`Web\Identity\SessionClaims.cs`) | shared web identity dependency |
| `LoginModel` / `LogoutModel` | `HttpContext.SignInAsync` / `SignOutAsync`, `ClaimsIdentity`/`ClaimsPrincipal` (Microsoft.AspNetCore) | framework auth dependency |
| `Program.cs` (Login path) | cookie options: `LoginPath`/`LogoutPath`/`AccessDeniedPath`, `BaDmo.Session`, 8h expiry, HttpOnly, SameSite=Lax, SecurePolicy | shared web session wiring |
| `/login` / `/logout` pages | `Program.cs` fallback `AuthenticatedSessionRequirement`; both endpoints declare `[AllowAnonymous]` | shared authorization configuration present but not applied to these endpoints |
| `LoginModel.OnPostAsync` redirect | `/no-access`, `/no-access?indisponivel=1` (`NoAccessModel`) | shell route reference |
| `LogoutModel.OnPostAsync` redirect | `/login` | Login-dedicated route reference |
| `Login.cshtml` styles | shared `wwwroot\styles\dmo-*.css` (5 files) | shared static dependency |
| `Login.cshtml` brand logo | shared `wwwroot\assets\ba-logo.png` | shared static dependency |
| `Login.cshtml` password toggle | shared `wwwroot\scripts\dmo-interactions.js` | shared static dependency |
| `ISupabaseAuthAdapter` sign-in | Supabase Auth REST `POST /auth/v1/token?grant_type=password` | external provider dependency |

## 17. Target-to-Layer Index

| Technical Object | Layer | Location |
|---|---|---|
| `LoginModel` (`OnGet`, `OnPostAsync`) | Web Pages | `src\BA.Dmo.Web\Pages\Auth\Login.cshtml.cs` |
| `Login.cshtml` (`@page "/login"`, `@attribute [AllowAnonymous]`) | Web Pages | `src\BA.Dmo.Web\Pages\Auth\Login.cshtml` |
| `LogoutModel` (`OnGet`, `OnPostAsync`) | Web Pages | `src\BA.Dmo.Web\Pages\Auth\Logout.cshtml.cs` |
| `Logout.cshtml` (`@page "/logout"`, `@attribute [AllowAnonymous]`) | Web Pages | `src\BA.Dmo.Web\Pages\Auth\Logout.cshtml` |
| `ISupabaseAuthAdapter` / `AuthUser` | Application Shared Identity (port) | `src\BA.Dmo.Application\Shared\Identity\SupabaseAuthPorts.cs` |
| `IdentityResolutionService` / `ResolvedIdentity` | Application Shared Identity | `src\BA.Dmo.Application\Shared\Identity\IdentityResolutionService.cs` |
| `FirstPageResolution` / `AccessResolver.ResolveFirstPage` | Application Shared Access | `src\BA.Dmo.Application\Shared\Access\AccessResolver.cs` |
| `SessionClaims` | Web Identity | `src\BA.Dmo.Web\Identity\SessionClaims.cs` |
| Cookie auth options (`/login`, `/logout`, `/access-denied`, 8h, HttpOnly, SameSite=Lax) | Web composition root | `src\BA.Dmo.Web\Program.cs` (lines 77–89) |
| `ISupabaseAuthAdapter` singleton / `IdentityResolutionService` scoped | Web composition root | `src\BA.Dmo.Web\Program.cs` (lines 148, 151–154) |
| `SupabaseAuthAdapter` | Infrastructure Auth (shared) | `src\BA.Dmo.Infrastructure\Auth\SupabaseAuthAdapter.cs` |
| `SupabaseSettings` | Infrastructure Auth (shared) | `src\BA.Dmo.Infrastructure\Auth\SupabaseSettings.cs` |
| Shared auth/shell pages (`/access-denied`, `/no-access`, `/`) | Web Pages (shared) | `src\BA.Dmo.Web\Pages\AccessDenied.*`, `NoAccess.*`, `Index.*` |
| Shared `dmo-*.css` (5), `ba-logo.png`, `dmo-interactions.js` | Web shared static | `src\BA.Dmo.Web\wwwroot\` |
| `WebAuthSessionTests`, `IdentityAmbiguityLandingTests` | Tests | `tests\BA.Dmo.IntegrationTests\Identity\` |
| `NoDebugBypassGuardTests` | Tests | `tests\BA.Dmo.IntegrationTests\Security\` |
| Domain / Application / Infrastructure dedicated Login objects | — | none in current source |

## 18. Sources Verified

- `maps\00_INDEX.md` (binding contract; Login row, surface order 18, COMPLETE; User Surface Shared)
- `maps\16_USERS_ACCESS.md` (MAP-16 boundary)
- `maps\15_ADMIN.md` (MAP-15 boundary)
- `maps\17_DESIGN_LABORATORIO.md` (MAP-17 boundary)
- `src\BA.Dmo.Web\Pages\Auth\Login.cshtml`, `Login.cshtml.cs`, `Logout.cshtml`, `Logout.cshtml.cs`
- `src\BA.Dmo.Web\Pages\Index.cshtml`, `Index.cshtml.cs`, `AccessDenied.cshtml`, `AccessDenied.cshtml.cs`, `NoAccess.cshtml`, `NoAccess.cshtml.cs`
- `src\BA.Dmo.Web\Identity\SessionClaims.cs`
- `src\BA.Dmo.Web\Program.cs` (cookie options, session/policy, `ISupabaseAuthAdapter`/`IdentityResolutionService` registration; module endpoints scanned, none for login)
- `src\BA.Dmo.Web\Pages\Shared\_Layout.cshtml` (shared static set)
- `src\BA.Dmo.Application\Shared\Identity\SupabaseAuthPorts.cs`, `IdentityResolutionService.cs`
- `src\BA.Dmo.Application\Shared\Access\AccessResolver.cs` (`FirstPageResolution`/`FirstPageOutcome`)
- `src\BA.Dmo.Infrastructure\Auth\SupabaseAuthAdapter.cs`, `SupabaseSettings.cs`
- `src\BA.Dmo.Domain\Modules\` and `src\BA.Dmo.Application\Modules\` (directory listing; no `Login` folder)
- Global source greps for `Login|login|LoginModel|/login|/logout|SignInAsync` across `src\`, `tests\`, `database\`
- `database\migrations\*.sql` and `database\consolidated_clean_install.sql` (grepped for `login`; no match)
- `src\BA.Dmo.Web\wwwroot\` (styles/scripts/assets directory scan; no login-specific static file)
- `tests\BA.Dmo.IntegrationTests\Identity\WebAuthSessionTests.cs`, `IdentityAmbiguityLandingTests.cs`
- `tests\BA.Dmo.IntegrationTests\Security\NoDebugBypassGuardTests.cs`
- `tests\BA.Dmo.UnitTests\` (grepped for `Login|/login`; no Login-targeting unit test)

## Counts

- Domain Login files: **0**
- Application Login files: **0**
- Infrastructure Login files: **0**
- Shared / external infrastructure dependencies: **2** (`SupabaseAuthAdapter`, `SupabaseSettings` — shared infra consumed by Login runtime)
- Dedicated Web page files: **4** (`Login.cshtml`, `Login.cshtml.cs`, `Logout.cshtml`, `Logout.cshtml.cs`)
- Dedicated API / endpoint files: **0**
- Dedicated Login routes: **2** (`/login`, `/logout`)
- Shared auth/shell routes referenced: **2** (`/no-access`, `/access-denied`)
- Dedicated static asset files: **0**
- Shared static asset files: **7** (5 `dmo-*.css` + 1 `ba-logo.png` + 1 `dmo-interactions.js`)
- Login-specific DB tables: **0**
- Login-specific DB indexes: **0**
- Login-specific DB triggers: **0**
- Login-specific DB objects: **0** (0 tables + 0 indexes + 0 triggers)
- Shared / external DB dependencies: **3** (navigational: `internal_users`, `access_templates`, external Supabase `auth.users`)
- Distinct Login migration files: **0**
- Module IDs: **0** (Login is not a canonical catalog module)
- Capability IDs: **0**
- Page IDs: **0** (Login is not a canonical catalog page)
- Test classes: **3** (`WebAuthSessionTests`, `IdentityAmbiguityLandingTests`, `NoDebugBypassGuardTests`)
- Dedicated test support files: **0**
- In-file test fixture files: **2**
- Source-visible user surfaces: **1** (Shared)