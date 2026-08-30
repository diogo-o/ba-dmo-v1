using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Application.Modules.Boquilhas;
using BA.Dmo.Application.Modules.Controlo;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Modules.Historia;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Application.Modules.Peso;
using BA.Dmo.Application.Modules.ReparacaoExterna;
using BA.Dmo.Application.Modules.ReparacaoInterna;
using BA.Dmo.Application.Modules.Tampoes;
using BA.Dmo.Application.Shared;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Application.Shared.Shell;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Domain.Modules.Boquilhas;
using BA.Dmo.Domain.Modules.Controlo;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Auth;
using BA.Dmo.Infrastructure.Identity;
using BA.Dmo.Infrastructure.Persistence;
using BA.Dmo.Web.Authorization;
using BA.Dmo.Web.Cli;
using BA.Dmo.Web.Identity;
using BA.Dmo.Web.Pages.JobOn;
using BA.Dmo.Web.Shell;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;

// BA DMO — single composition root (Plan-V3 GLM-ARCH-07).
//
// Operational CLI verbs are distinguished by process arguments; there is no separate CLI
// project (GLM-ARCH-15):
//   migrate            → dotnet BA.Dmo.Web.dll migrate
//   bootstrap-admin    → dotnet BA.Dmo.Web.dll bootstrap-admin
//   (omission)         → normal web startup
// CLI verbs are CLI ONLY: no HTTP migration endpoint, no hosted-service automation,
// no privileged action on normal production web startup.
var mode = CliModeResolver.Resolve(args);
switch (mode)
{
    case CliMode.Migrate:
        return MigrateCommand.Run();
    case CliMode.BootstrapAdmin:
        return BootstrapAdminCommand.Run();
}

var builder = WebApplication.CreateBuilder(args);

// Integration hosts must not depend on machine/user-profile key stores.
// Production keeps the framework's normal persistent data-protection behavior.
if (builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

// Persistence foundation (U-03): snake_case ↔ PascalCase mapping conventions
// for Dapper. CLI verbs exit above and never reach this point.
PersistenceMappings.Configure();

// Canonical catalog validation (U-04, GLM-ACC-03): an invalid canonical
// configuration fails explicitly at startup — it is never silently repaired.
CatalogValidator.Validate(
    CanonicalModuleCatalog.Instance,
    CanonicalPageCatalog.Instance,
    CanonicalModuleCatalog.AreaChildren);

builder.Services.AddRazorPages();

// Minimal-API JSON binding uses enum-by-name for the enum-typed request records
// (e.g. InternalRepairToolType CM|MF in /api/reparacao-interna/*), matching the
// stored text discriminators (GLM-ARCH-07).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Identity/authentication foundation (U-05, GLM-ACC-01): session cookie
// bridge carrying ONLY the Supabase auth user id; grants are resolved
// server-side per request and never stored in the cookie. The privileged
// provisioning adapter is intentionally NOT registered here — it exists
// only inside the bootstrap-admin CLI path (PV-07).
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddAuthentication(SessionClaims.AuthenticationScheme)
    .AddCookie(SessionClaims.AuthenticationScheme, options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(SessionClaims.AuthenticationScheme)
        .AddRequirements(new AuthenticatedSessionRequirement())
        .Build();

    // Administration policies (U-06): canonical capabilities only — never
    // role names, emails or template names (GLM-ACC-03/04).
    options.AddPolicy(AdminPolicies.AdminGerir, policy => policy
        .AddAuthenticationSchemes(SessionClaims.AuthenticationScheme)
        .AddRequirements(new CapabilityRequirement(CanonicalCapabilities.AdminGerir)));
    options.AddPolicy(AdminPolicies.AuditView, policy => policy
        .AddAuthenticationSchemes(SessionClaims.AuthenticationScheme)
        .AddRequirements(new CapabilityRequirement(CanonicalCapabilities.AuditView)));
    options.AddPolicy(AdminPolicies.AuditExport, policy => policy
        .AddAuthenticationSchemes(SessionClaims.AuthenticationScheme)
        .AddRequirements(new CapabilityRequirement(CanonicalCapabilities.AuditExport)));

    // Module/capability route guards (U-07, 05_SHL §5): one policy per
    // canonical catalog entry, built ONLY from the canonical catalog —
    // never from role names, emails or template names (GLM-ACC-03/04).
    foreach (var module in CanonicalModuleCatalog.Instance.Modules)
    {
        var moduleId = module.ModuleId;
        options.AddPolicy(ModulePolicies.Prefix + moduleId, policy => policy
            .AddAuthenticationSchemes(SessionClaims.AuthenticationScheme)
            .AddRequirements(new ModuleRequirement(moduleId)));
        foreach (var capability in module.Capabilities)
        {
            var capabilityId = capability.Id;
            options.AddPolicy(CapabilityPolicies.Prefix + capabilityId, policy => policy
                .AddAuthenticationSchemes(SessionClaims.AuthenticationScheme)
                .AddRequirements(new CapabilityRequirement(capabilityId)));
        }
    }
});
builder.Services.AddSingleton<IAuthorizationHandler, AuthenticatedSessionHandler>();
builder.Services.AddScoped<IAuthorizationHandler, CapabilityAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ModuleAuthorizationHandler>();

builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton<IDbConnectionFactory>(
    new LazyDbConnectionFactory(Environment.GetEnvironmentVariable));
builder.Services.AddSingleton<IInternalUserRepository, DapperInternalUserRepository>();
var accessResolver = new AccessResolver(
    CanonicalModuleCatalog.Instance,
    CanonicalPageCatalog.Instance,
    CanonicalModuleCatalog.AreaChildren);
builder.Services.AddSingleton(accessResolver);

// Shell navigation (U-07, GLM-SHL-01/03): navigation is DERIVED from the
// resolved grants — it never lives in markup. The per-request shell state
// (identity presentation + tabs) is resolved server-side, fail-closed.
builder.Services.AddSingleton<INavigationService>(new NavigationService(
    CanonicalPageCatalog.Instance, accessResolver, CanonicalModuleCatalog.Instance));
builder.Services.AddScoped<IShellService, RequestShellService>();

builder.Services.AddScoped<IdentityResolutionService>();
builder.Services.AddScoped<ICurrentUserAccessor, RequestCurrentUserAccessor>();
builder.Services.AddScoped<IPersistenceAuthorshipAccessor, CurrentUserAuthorshipAccessor>();
builder.Services.AddSingleton<ISupabaseAuthAdapter>(_ => new SupabaseAuthAdapter(
    new HttpClient(),
    SupabaseSettings.ResolveUrl(Environment.GetEnvironmentVariable),
    SupabaseSettings.ResolveAnonKey(Environment.GetEnvironmentVariable)));

// Administration module (U-06): Application services + persistence port.
// The privileged provisioning adapter is registered fail-closed: without the
// service-role environment configuration it rejects every operation, and it
// is only reachable through admin.gerir-gated use cases (TD-16) or the
// bootstrap-admin CLI — never exposed to the browser (PV-07).
builder.Services.AddSingleton<IAdminProvisioningAdapter>(_ =>
    new SupabaseAdminProvisioningAdapter(
        new HttpClient(),
        SupabaseSettings.ResolveUrl(Environment.GetEnvironmentVariable),
        SupabaseSettings.ResolveServiceRoleKey(Environment.GetEnvironmentVariable)));
builder.Services.AddSingleton<IAdminRepository, DapperAdminRepository>();
builder.Services.AddSingleton<IModuleCatalogMirrorRepository, DapperModuleCatalogMirrorRepository>();
builder.Services.AddScoped<IJobOnRepository, DapperJobOnRepository>();
builder.Services.AddScoped<IJobOnUserContextRepository, DapperJobOnUserContextRepository>();
builder.Services.AddScoped<IArticleReferenceImageRepository, DapperArticleReferenceImageRepository>();
builder.Services.AddScoped<JobOnAuthorizationGate>();
builder.Services.AddScoped<JobOnService>();
// Job On PDF generation (U-13): renderer + service + image provider abstraction.
builder.Services.AddScoped<IJobOnImageProvider, FileSystemJobOnImageProvider>();
builder.Services.AddScoped<JobOnPdfService>();
builder.Services.AddSingleton<IJobOnPdfRenderer, JobOnPdfRenderer>();
builder.Services.AddSingleton(CanonicalModuleCatalog.Instance);
builder.Services.AddScoped<AdminAuthorizationGate>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<AdminTemplateService>();
builder.Services.AddScoped<AdminMirrorService>();
builder.Services.AddScoped<AdminAuditService>();
builder.Services.AddScoped<GrantNormalizer>(_ =>
    new GrantNormalizer(CanonicalModuleCatalog.Instance));

// Peso module (U-10): Application services + persistence port + PDF renderer.
builder.Services.AddScoped<IPesoRepository, DapperPesoRepository>();
builder.Services.AddScoped<PesoAuthorizationGate>();
builder.Services.AddScoped<PesoService>();
builder.Services.AddSingleton<IPdfRenderer, PesoSingleFilePdfRenderer>();

// Pegamentos module (U-11): Application services + persistence port +
// Job On production context lookup + shared settings + PDF renderer.
builder.Services.AddScoped<IPegamentoRepository, DapperPegamentoRepository>();
builder.Services.AddScoped<IPegamentoUnitOfWorkFactory, DapperPegamentoUnitOfWorkFactory>();
builder.Services.AddScoped<IJobOnProductionContextLookup, DapperJobOnProductionContextLookup>();
builder.Services.AddScoped<PegamentoAuthorizationGate>();
builder.Services.AddScoped<PegamentoService>();
builder.Services.AddScoped<PegamentoPdfService>();
builder.Services.AddScoped<IJobOnProductionFolderResolver, DapperJobOnProductionFolderResolver>();
builder.Services.AddScoped<IAppSettingsReader, DapperAppSettingsReader>();
builder.Services.AddSingleton<IPegamentoPdfRenderer, PegamentoPdfRenderer>();

// Ferramentas module (U-12): Application services + persistence port +
// verification-rule lookup + cross-module identity lookup.
builder.Services.AddScoped<IFerramentasRepository, DapperFerramentasRepository>();
builder.Services.AddScoped<IFerramentasRuleLookup, DapperFerramentasRuleLookup>();
builder.Services.AddScoped<IFerramentasIdentityLookup, DapperFerramentasIdentityLookup>();
builder.Services.AddScoped<FerramentasAuthorizationGate>();
builder.Services.AddScoped<FerramentasService>();

// Armazém module (U-14): physical position/stock ownership. Tool identity is
// resolved only through Armazém's own IToolIdentityResolver (CM/MF/BQ adapter
// over Ferramentas' read-only canonical master lookup).
builder.Services.AddScoped<IArmazemRepository, DapperArmazemRepository>();
builder.Services.AddScoped<IToolIdentityResolver, FerramentasArmazemToolIdentityResolver>();
builder.Services.AddScoped<ArmazemAuthorizationGate>();
builder.Services.AddScoped<ArmazemService>();
builder.Services.AddScoped<IArmazemRepairMovementPort, DapperArmazemRepairMovementRepository>();

// Reparação Externa module (U-15): external CM/MF repair batches. BQ functional
// scope deferred to U-19 (owner decision A). Armazém physical movement is reached
// ONLY through the Armazém-owned IArmazemRepairMovementPort (owner decision B).
builder.Services.AddScoped<IFerramentasPieceLookup, DapperFerramentasPieceLookup>();
builder.Services.AddScoped<IToolPieceResolver, FerramentasRepairToolPieceResolver>();
builder.Services.AddScoped<IRepairUnitOfWorkFactory, DapperRepairUnitOfWorkFactory>();
builder.Services.AddScoped<IRepairRepository, DapperRepairRepository>();
builder.Services.AddScoped<ReparacaoExternaAuthorizationGate>();
builder.Services.AddScoped<ReparacaoExternaService>();

// Reparação Interna module (U-16): quick in-turn CM/MF repair records on the ACTIVE
// production context. Reads Job On context + Ferramentas piece identity read-only;
// never touches Armazém physical state or tool/master data.
builder.Services.AddScoped<IJobOnActiveContextLookup, DapperJobOnActiveContextLookup>();
builder.Services.AddScoped<IReparacaoInternaRepository, DapperReparacaoInternaRepository>();
builder.Services.AddScoped<ReparacaoInternaAuthorizationGate>();
builder.Services.AddScoped<ReparacaoInternaService>();

// Folha de Controlo (R010): production-level control summary sheet INSIDE the Controlo
// area. Anchored to job_on_id + exact job_on_revision_id; snapshots the components of that
// revision. Reads Job On context read-only; distinct from Peso/Pegamentos (no merge).
builder.Services.AddScoped<IControloProductionContextLookup, DapperControloProductionContextLookup>();
builder.Services.AddScoped<IControloSheetRepository, DapperControloSheetRepository>();
builder.Services.AddScoped<ControloSheetAuthorizationGate>();
builder.Services.AddScoped<ControloSheetService>();

// Tampões module (U-17): aggregate quantity control by technical configuration with
// the atomic state/configuração transforms run in ONE transaction (GLM-DATA-05).
builder.Services.AddScoped<ITampoesUnitOfWorkFactory, DapperTampoesUnitOfWorkFactory>();
builder.Services.AddScoped<ITampaoRepository, DapperTampaoRepository>();
builder.Services.AddScoped<TampaoAuthorizationGate>();
builder.Services.AddScoped<TampaoService>();

// História module (U-18): transversal READ-ONLY history view. It projects the
// canonical append-only audit_events table restricted by the TD-24 origin-module
// grants of the current identity; it never writes and has no own capabilities.
builder.Services.AddScoped<IHistoriaRepository, DapperHistoriaRepository>();
builder.Services.AddScoped<HistoriaAuthorizationGate>();
builder.Services.AddScoped<HistoriaService>();

// Boquilhas module (U-19): canonical daily, high-frequency, quantity-based
// operational flow (reference + lot) over the bq_* schema. Reads the canonical
// append-only fact tables; every write is transactional and emits its global
// audit_events fact. It never writes Ferramentas/Armazém/Reparação-Externa and
// consumes no live Job On lookup (immutable snapshots remain the default
// historical integration — U-19 D2). No capabilities (GLM-BQ-02).
builder.Services.AddScoped<IBoquilhasRepository, DapperBoquilhasRepository>();
builder.Services.AddScoped<IBoquilhasUnitOfWorkFactory, DapperBoquilhasUnitOfWorkFactory>();
builder.Services.AddScoped<BqAuthorizationGate>();
builder.Services.AddScoped<BoquilhasService>();

var app = builder.Build();

// Design system assets (U-08): the single global stylesheet set served to
// every page; no page-local design CSS exists (GLM-DSN-09).
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// Create a new Job On (rascunho). CREATE IS A WRITE: the route-level
// capability policy requires jobon.edit and the service gate re-checks the
// canonical capability server-side (fail closed). The service validates the
// minimum real production context (produção, referência, máquina, datas) and
// atomically persists the header + the initial revision — on success the
// client opens the newly created Folha Job On via /jobon?id={jobOnId}.
app.MapPost("/api/jobon", async (
    CreateJobOnRequest request,
    JobOnService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateAsync(request, cancellationToken);
    if (result.IsSuccess)
        return Results.Ok(new { jobOnId = result.Value });
    if (result.Error.Category == ErrorCategory.Forbidden)
        return Results.Forbid();
    return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.JobonEdit);

// Duplicate a Job On (modules/05 §6.2). DUPLICATE IS A WRITE: the route-level
// capability policy requires jobon.edit and the service gate re-checks the
// canonical capability server-side (fail closed). The body carries ONLY the NEW
// production/date context; the reference and tool setup come from the source
// revision. The service atomically persists the new header + the copied initial
// revision + the audit event; the source Job On is never modified. On success
// the client opens the newly created Folha Job On via /jobon?id={jobOnId}.
app.MapPost("/api/jobon/{jobOnId:guid}/duplicate", async (
    Guid jobOnId,
    DuplicateJobOnRequest request,
    JobOnService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.DuplicateAsync(
        request with { SourceJobOnId = jobOnId }, cancellationToken);
    if (result.IsSuccess)
        return Results.Ok(new { jobOnId = result.Value });
    if (result.Error.Category == ErrorCategory.Forbidden)
        return Results.Forbid();
    if (result.Error.Category == ErrorCategory.NotFound)
        return Results.NotFound(new { code = result.Error.Code, message = result.Error.Message });
    return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.JobonEdit);

// "Alterar data" — change the planned dates of an EXISTING Job On (modules/05).
// ALTER DATE IS A WRITE: the route-level capability policy requires jobon.edit and
// the service gate re-checks the canonical capability server-side (fail closed), so
// an Operator/Controller with only jobon.view is denied regardless of UI visibility.
// The body carries ONLY the new planned dates (and an optional change reason); the
// service creates a NEW immutable revision of the SAME job_on_id (next revision
// number, new dates snapshot, current setup preserved), advances the header planned
// dates (single calendar source) and current_revision_id, and records the audit
// event — all atomically. On success the client reopens the SAME Folha Job On via
// /jobon?id={jobOnId} rendering the new current revision.
app.MapPost("/api/jobon/{jobOnId:guid}/date", async (
    Guid jobOnId,
    AlterJobOnDatesRequest request,
    JobOnService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.AlterDatesAsync(
        request with { JobOnId = jobOnId }, cancellationToken);
    if (result.IsSuccess)
        return Results.Ok(new { jobOnId, revisionId = result.Value });
    if (result.Error.Category == ErrorCategory.Forbidden)
        return Results.Forbid();
    if (result.Error.Category == ErrorCategory.NotFound)
        return Results.NotFound(new { code = result.Error.Code, message = result.Error.Message });
    return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.JobonEdit);

// "Guardar nova revisão" — save an EDITED revision of an EXISTING Job On (TD-18).
// SAVE IS A WRITE: the route-level capability policy requires jobon.edit and the
// service gate re-checks the canonical capability server-side (fail closed), so an
// Operator/Controller with only jobon.view is denied regardless of UI visibility.
// The body carries ONLY revision-owned values (general notes + the complete edited
// component graph: components, fields, CAL rows, verifications); header-owned data
// (dates, production identity, machine/line) is neither accepted nor rewritten —
// dates keep their dedicated "Alterar data" flow. The service creates a NEW
// immutable revision of the SAME job_on_id (next revision number, header context
// and unchanged revision values preserved), advances current_revision_id, and
// records the audit event — all atomically; the previous revision is never
// modified. On success the client reopens the SAME Folha Job On via
// /jobon?id={jobOnId} rendering the new current revision.
app.MapPost("/api/jobon/{jobOnId:guid}/revision", async (
    Guid jobOnId,
    SaveJobOnRevisionRequest request,
    JobOnService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.SaveRevisionAsync(
        request with { JobOnId = jobOnId }, cancellationToken);
    if (result.IsSuccess)
        return Results.Ok(new { jobOnId, revisionId = result.Value });
    if (result.Error.Category == ErrorCategory.Forbidden)
        return Results.Forbid();
    if (result.Error.Category == ErrorCategory.NotFound)
        return Results.NotFound(new { code = result.Error.Code, message = result.Error.Message });
    return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.JobonEdit);

// "Alterar CM/MF/BQ associado" — tool selection options (Manual 10 §4/§8).
// READ-ONLY over the Ferramentas register: lists ONLY real registered tool
// lots (N04) of the requested type (CM/MF/BQ are distinct, never merged)
// whose registered allowed_lines include this Job On's machine/line — the
// identity tuple (tipo, referência, lote, máquina/linha) is enforced at the
// source, so the picker can never present or persist an invented combination.
// No Ferramentas/Armazém record is read or written except the register read.
// The picker is an edit surface: the route policy requires jobon.edit and the
// service gate re-checks it server-side (fail closed), so an operator with
// only jobon.view is denied regardless of UI visibility.
app.MapGet("/api/jobon/{jobOnId:guid}/tool-options", async (
    Guid jobOnId,
    string family,
    string? reference,
    string? lot,
    JobOnService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetToolSelectionOptionsAsync(
        jobOnId, family, reference, lot, cancellationToken);
    if (result.IsSuccess)
        return Results.Ok(new
        {
            jobOnId,
            machine = result.Value.Machine,
            family = result.Value.Family,
            items = result.Value.Items
        });
    if (result.Error.Category == ErrorCategory.Forbidden)
        return Results.Forbid();
    if (result.Error.Category == ErrorCategory.NotFound)
        return Results.NotFound(new { code = result.Error.Code, message = result.Error.Message });
    return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.JobonEdit);

// Job On image API endpoints: attach/replace/remove the master association
// owned by the current Article/Reference. These actions never create or change
// a Job On revision. The binary remains in the configured company image
// directory; only the validated file name is persisted.
app.MapPost("/api/jobon/{jobOnId:guid}/image/attach", async (
    Guid jobOnId,
    AttachImageRequest request,
    JobOnService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.AttachImageAsync(
        request with { JobOnId = jobOnId }, cancellationToken);
    return result.IsSuccess
        ? Results.Ok(new { reference = result.Value.ReferenceCode, imageAssetId = result.Value.ImageAssetId })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.JobonEdit);

app.MapPost("/api/jobon/{jobOnId:guid}/image/replace", async (
    Guid jobOnId,
    ReplaceImageRequest request,
    JobOnService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.ReplaceImageAsync(
        request with { JobOnId = jobOnId }, cancellationToken);
    return result.IsSuccess
        ? Results.Ok(new { reference = result.Value.ReferenceCode, imageAssetId = result.Value.ImageAssetId })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.JobonEdit);

app.MapPost("/api/jobon/{jobOnId:guid}/image/remove", async (
    Guid jobOnId,
    JobOnService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.RemoveImageAsync(
        new RemoveImageRequest(jobOnId), cancellationToken);
    return result.IsSuccess
        ? Results.Ok(new { reference = result.Value.ReferenceCode, removedImageAssetId = result.Value.ImageAssetId })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.JobonEdit);

app.MapGet("/api/jobon/{jobOnId:guid}/image", async (
    Guid jobOnId,
    IJobOnImageProvider provider,
    CancellationToken cancellationToken) =>
{
    var image = await provider.ResolveAsync(jobOnId, cancellationToken);
    return image is null
        ? Results.NotFound()
        : Results.File(image.Bytes, image.MimeType);
}).RequireAuthorization(CapabilityPolicies.JobonView);

// R011 — Universal Landing: record/read the Job On context THIS user explicitly
// opened. Requires only jobon.view (viewing planning is enough to open a folha).
// The service gate re-checks the current identity + capability server-side.
app.MapPost("/api/jobon/current", async (
    CurrentJobOnRequest request,
    JobOnService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.SetCurrentOpenAsync(request.JobOnId, cancellationToken);
    return result.IsSuccess
        ? Results.Ok(new { opened = request.JobOnId })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.JobonView);

app.MapGet("/api/jobon/current", async (
    JobOnService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetCurrentOpenAsync(cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : (result.Error.Code == "JOBON_CURRENT_NOT_FOUND"
            ? Results.NotFound(new { code = result.Error.Code, message = result.Error.Message })
            : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message }));
}).RequireAuthorization(CapabilityPolicies.JobonView);

// =============================================================
// PHASE 4 — Job On planning-only read (JOB ON PLANNING ISOLATION).
//
// GET /api/jobon/planning?date=YYYY-MM-DD[&month=YYYY-MM]
//
// Returns ONLY the data the Job On planning area needs for the selected
// date/month: the date display, the calendar month, the calendar marker
// data (record dates + deterministic line-color markers) and the day list.
//
// SAME PLANNING SOURCE + SEMANTICS as the landing page:
// IJobOnRepository.GetHistoricalProductionsAsync (month read, list filtered
// to the selected day) projected through JobOnPlanningProjection — the very
// code the page's OnGetAsync uses (one implementation).
//
// ISOLATION CONTRACT (PLANNING ISOLATION RULE): this endpoint is
// planning-only. It NEVER invokes ICurrentProductionContextLookup, the
// production rail projection or any current-production/rail reader —
// planning date state and the Current Production Context remain separate.
// The client consumes it by updating only the planning DOM (no full page
// reload, no shell re-creation, no rail re-fetch).
//
// date: strict yyyy-MM-dd; missing/invalid falls back to today (page rule).
// month: optional marker month for calendar navigation (strict yyyy-MM);
// missing/invalid falls back to the selected date's month. When it differs
// from the selected date's month the day list is read from the selected
// date's own month (the selected-date rule is preserved).
// =============================================================
app.MapGet("/api/jobon/planning", async (
    string? date, string? month, IJobOnRepository repository, CancellationToken cancellationToken) =>
{
    var selectedDate = JobOnPlanningProjection.ResolveSelectedDate(date);
    var markerMonthDate = JobOnPlanningProjection.ResolveMarkerMonth(month, selectedDate);

    var (markerFrom, markerTo) = JobOnPlanningProjection.MonthRange(markerMonthDate);
    var markerSummaries = await repository.GetHistoricalProductionsAsync(
        referenceFilter: null,
        machineFilter: null,
        from: markerFrom,
        to: markerTo,
        cancellationToken: cancellationToken);

    // Common path (day selection): marker month == selected date's month —
    // exactly the landing page's single month read. Calendar navigation to
    // another month adds one scoped read for the selected date's day list.
    IReadOnlyList<BA.Dmo.Application.Modules.JobOn.HistoricalProductionSummary> daySummaries = markerSummaries;
    if (new DateTime(markerMonthDate.Year, markerMonthDate.Month, 1)
        != new DateTime(selectedDate.Year, selectedDate.Month, 1))
    {
        var (dayFrom, dayTo) = JobOnPlanningProjection.MonthRange(selectedDate);
        daySummaries = await repository.GetHistoricalProductionsAsync(
            referenceFilter: null,
            machineFilter: null,
            from: dayFrom,
            to: dayTo,
            cancellationToken: cancellationToken);
    }

    return Results.Ok(JobOnPlanningProjection.Build(selectedDate, markerMonthDate, markerSummaries, daySummaries));
}).RequireAuthorization(CapabilityPolicies.JobonView);

// Job On document generation — returns 4-page PDF bytes (Ficha de Artigo x2,
// Job-On Moldes, Trabalho de Equipa). Requires jobon.view capability.
app.MapPost("/api/jobon/{jobOnId:guid}/document", async (
    Guid jobOnId,
    JobOnPdfService service,
    IJobOnPdfRenderer renderer,
    CancellationToken cancellationToken) =>
{
    var result = await service.GenerateAsync(renderer, jobOnId, cancellationToken);
    if (!result.IsSuccess)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });

    return Results.File(result.Value.PdfBytes, "application/pdf", result.Value.FileName);
}).RequireAuthorization(CapabilityPolicies.JobonView);

// =============================================================
// Peso module (U-10) endpoints. All gated server-side: base module
// for Operador create/edit/submit/generate; `peso.aprovar` for
// approve/reject/reopen/day-approvals/settings. The service re-checks
// capability through PesoAuthorizationGate on every call (GLM-ACC-04);
// hiding UI never substitutes server-side validation.
// =============================================================

static BA.Dmo.Domain.Modules.Peso.PesoRecordType? ParseType(string? value) => value?.Trim().ToLowerInvariant() switch
{
    "novo_controlo" => BA.Dmo.Domain.Modules.Peso.PesoRecordType.NovoControlo,
    "comparacao" => BA.Dmo.Domain.Modules.Peso.PesoRecordType.Comparacao,
    _ => null
};

app.MapPost("/api/peso/control", async (CreateControlRequest request, PesoService service, CancellationToken ct) =>
{
    var result = await service.CreateControlAsync(request, ct);
    return result.IsSuccess
        ? Results.Ok(new { controlId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Peso);

app.MapPost("/api/peso/{controlId:guid}/save", async (Guid controlId, SaveControlRequest request, PesoService service, CancellationToken ct) =>
{
    var result = await service.SaveControlAsync(request with { ControlId = controlId }, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Peso);

app.MapPost("/api/peso/{controlId:guid}/submit", async (Guid controlId, PesoService service, CancellationToken ct) =>
{
    var result = await service.SubmitControlAsync(new SubmitControlRequest(controlId), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Peso);

app.MapPost("/api/peso/{controlId:guid}/calculate", async (Guid controlId, PesoService service, CancellationToken ct) =>
{
    // The C# engine is the single source of calculation (GLM-PESO-05); this
    // endpoint returns the derived result for the live preview.
    var control = await service.GetControlForCalculationAsync(controlId, ct);
    return control.IsSuccess ? Results.Ok(control.Value)
        : Results.BadRequest(new { code = control.Error.Code, message = control.Error.Message });
}).RequireAuthorization(ModulePolicies.Peso);

app.MapPost("/api/peso/{controlId:guid}/approve", async (Guid controlId, PesoService service, CancellationToken ct) =>
{
    var result = await service.ApproveControlAsync(new ApproveControlRequest(controlId), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.PesoAprovar);

app.MapPost("/api/peso/{controlId:guid}/reject", async (Guid controlId, RejectControlRequest request, PesoService service, CancellationToken ct) =>
{
    var result = await service.RejectControlAsync(new RejectControlRequest(controlId, request.Justification), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.PesoAprovar);

app.MapPost("/api/peso/{controlId:guid}/reopen", async (Guid controlId, ReopenControlRequest request, PesoService service, CancellationToken ct) =>
{
    var result = await service.ReopenControlAsync(new ReopenControlRequest(controlId, request.Reason), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.PesoAprovar);

app.MapPost("/api/peso/{controlId:guid}/delete", async (Guid controlId, PesoService service, CancellationToken ct) =>
{
    var result = await service.DeleteControlAsync(new DeleteControlRequest(controlId), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Peso);

app.MapPost("/api/peso/{controlId:guid}/compare/decide", async (Guid controlId, ConfirmComparisonDecisionsRequest request, PesoService service, CancellationToken ct) =>
{
    var result = await service.ConfirmComparisonDecisionsAsync(
        new ConfirmComparisonDecisionsRequest(controlId, request.Justification, request.Decisions), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.PesoAprovar);

// Control detail for approval sheet / history view
app.MapGet("/api/peso/control/{controlId:guid}", async (Guid controlId, PesoService service, CancellationToken ct) =>
{
    var result = await service.GetControlDetailAsync(controlId, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(result.Value);
}).RequireAuthorization(ModulePolicies.Peso);

app.MapGet("/api/peso/controls", async (string? referenceId, string? status, string? type, DateTime? from, DateTime? to, PesoService service, CancellationToken ct) =>
{
    Guid? refGuid = Guid.TryParse(referenceId, out var g) ? g : null;
    var result = await service.SearchControlsAsync(new BA.Dmo.Application.Modules.Peso.ControlFilterRequest(refGuid, null, status, ParseType(type), from, to), ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Peso);

app.MapGet("/api/peso/dates", async (int year, int month, PesoService service, CancellationToken ct) =>
{
    var result = await service.GetRecordDatesAsync(year, month, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Peso);

app.MapPost("/api/peso/settings", async (SaveSettingsRequest request, PesoService service, CancellationToken ct) =>
{
    var result = await service.SaveSettingsAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.PesoAprovar);

// Generate document — returns PDF bytes + Content-Disposition header (GLM-PESO-09)
app.MapPost("/api/peso/{controlId:guid}/document", async (Guid controlId, PesoService service, IPdfRenderer renderer, CancellationToken ct) =>
{
    var result = await service.GenerateDocumentAsync(renderer, new GenerateDocumentRequest(controlId), ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    var doc = result.Value;
    return Results.File(doc.PdfBytes, "application/pdf", doc.FileName);
}).RequireAuthorization(ModulePolicies.Peso);

// Prepare email preview for approved control
app.MapPost("/api/peso/{controlId:guid}/email/prepare", async (Guid controlId, PesoService service, CancellationToken ct) =>
{
    var result = await service.PrepareEmailAsync(new PrepareEmailRequest(controlId), ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(result.Value);
}).RequireAuthorization(ModulePolicies.Peso);

// References list / create / update
app.MapGet("/api/peso/references", async (string? search, PesoService service, CancellationToken ct) =>
{
    var result = await service.ListReferencesAsync(search, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(result.Value);
}).RequireAuthorization(ModulePolicies.Peso);

app.MapPost("/api/peso/reference", async (SaveReferenceRequest request, PesoService service, CancellationToken ct) =>
{
    var result = await service.SaveReferenceAsync(request, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(new { id = result.Value });
}).RequireAuthorization(ModulePolicies.Peso);

// Create lot
app.MapPost("/api/peso/lote", async (CreateLoteRequest request, PesoService service, CancellationToken ct) =>
{
    var result = await service.CreateLoteAsync(request, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(new { id = result.Value });
}).RequireAuthorization(ModulePolicies.Peso);

// Save day approval
app.MapPost("/api/peso/day-approval", async (SaveDayApprovalRequest request, PesoService service, CancellationToken ct) =>
{
    var result = await service.SaveDayApprovalAsync(request, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(new { ok = true });
}).RequireAuthorization(CapabilityPolicies.PesoAprovar);

// Comparison creation
app.MapPost("/api/peso/comparison", async (CreateComparisonRequest request, PesoService service, CancellationToken ct) =>
{
    var result = await service.CreateComparisonAsync(request, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(new { id = result.Value });
}).RequireAuthorization(ModulePolicies.Peso);

// Settings read
app.MapGet("/api/peso/settings/{key}", async (string key, PesoService service, CancellationToken ct) =>
{
    var result = await service.GetSettingAsync(key, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    var val = result.Value ?? "";
    return Results.Ok(new { key, value = val });
}).RequireAuthorization(ModulePolicies.Peso);

// ============================================================================
// Pegamentos module (U-11) API endpoints.
// All endpoints require the pegamentos module policy (gated via
// PegamentoAuthorizationGate internally, fail-closed).
// ============================================================================

// Resolve exact historical production context for a revision (read-only).
app.MapGet("/api/pegamentos/context/{jobOnRevisionId:guid}", async (
    Guid jobOnRevisionId, PegamentoService service, CancellationToken ct) =>
{
    var result = await service.ResolveProductionContextAsync(jobOnRevisionId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Pegamentos);

// Pegamentos by exact revision (production/revision → Pegamentos).
app.MapGet("/api/pegamentos/revision/{jobOnRevisionId:guid}", async (
    Guid jobOnRevisionId, PegamentoService service, CancellationToken ct) =>
{
    var result = await service.ListByRevisionAsync(jobOnRevisionId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Pegamentos);

// Grouping list by Job On.
app.MapGet("/api/pegamentos/jobon/{jobOnId:guid}", async (
    Guid jobOnId, PegamentoService service, CancellationToken ct) =>
{
    var result = await service.ListByJobOnAsync(jobOnId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Pegamentos);

// Full control + measurements + historical context (Pegamento → production).
app.MapGet("/api/pegamentos/{controloId:guid}", async (
    Guid controloId, PegamentoService service, CancellationToken ct) =>
{
    var result = await service.GetControlDetailAsync(controloId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Pegamentos);

// Create control (resolves context from job_on_revision_id).
app.MapPost("/api/pegamentos", async (
    CreatePegamentoRequest request, PegamentoService service, CancellationToken ct) =>
{
    var result = await service.CreateControlAsync(request, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(new { id = result.Value });
}).RequireAuthorization(ModulePolicies.Pegamentos);

// Add measurement.
app.MapPost("/api/pegamentos/{controloId:guid}/measurements", async (
    Guid controloId, AddMeasurementRequest request, PegamentoService service, CancellationToken ct) =>
{
    // Ensure the route id matches the payload (single source of truth).
    if (controloId != request.ControloId)
        return Results.BadRequest(new { code = "PEGAMENTO_ID_MISMATCH", message = "O identificador da rota não corresponde ao corpo do pedido." });

    var result = await service.AddMeasurementAsync(request, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(new { id = result.Value });
}).RequireAuthorization(ModulePolicies.Pegamentos);

// Update control (tolerance, notes).
app.MapPut("/api/pegamentos/{controloId:guid}", async (
    Guid controloId, UpdatePegamentoRequest request, PegamentoService service, CancellationToken ct) =>
{
    if (controloId != request.ControloId)
        return Results.BadRequest(new { code = "PEGAMENTO_ID_MISMATCH", message = "O identificador da rota não corresponde ao corpo do pedido." });

    var result = await service.UpdateControlAsync(request, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(new { ok = true });
}).RequireAuthorization(ModulePolicies.Pegamentos);

// Close control.
app.MapPost("/api/pegamentos/{controloId:guid}/close", async (
    Guid controloId, PegamentoService service, CancellationToken ct) =>
{
    var result = await service.CloseControlAsync(new CloseControlRequest(controloId), ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(new { ok = true });
}).RequireAuthorization(ModulePolicies.Pegamentos);

// Measurement history / audit trail.
app.MapGet("/api/pegamentos/{controloId:guid}/history", async (
    Guid controloId, PegamentoService service, CancellationToken ct) =>
{
    var result = await service.GetHistoryAsync(controloId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Pegamentos);

// Search.
app.MapGet("/api/pegamentos/search", async (
    string? reference, string? productionCode, string? machine,
    DateTime? from, DateTime? to, PegamentoService service, CancellationToken ct) =>
{
    var result = await service.SearchAsync(
        new BA.Dmo.Application.Modules.Pegamentos.ControlFilterRequest(reference, productionCode, machine, from, to), ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Pegamentos);

// Generate PDF bytes + canonical filename (does NOT persist pegamento_documentos).
app.MapPost("/api/pegamentos/{controloId:guid}/document/generate", async (
    Guid controloId, PegamentoPdfService pdfService, IPegamentoPdfRenderer renderer, CancellationToken ct) =>
{
    var result = await pdfService.GenerateAsync(renderer, controloId, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.File(result.Value.PdfBytes, "application/pdf", result.Value.FileName);
}).RequireAuthorization(ModulePolicies.Pegamentos);

// Confirm final document save (persists pegamento_documentos with server-derived metadata).
app.MapPost("/api/pegamentos/{controloId:guid}/document/confirm", async (
    Guid controloId, PegamentoService service, CancellationToken ct) =>
{
    var result = await service.ConfirmDocumentSavedAsync(controloId, ct);
    if (result.IsFailure)
        return Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
    return Results.Ok(new { ok = true });
}).RequireAuthorization(ModulePolicies.Pegamentos);

// ============================================================================
// Ferramentas module (U-12) API endpoints.
// Module access requires the ferramentas module policy; verification-rule
// configuration requires ferramentas.configure (server-side gate too).
// ============================================================================

// Reference list (search) + detail.
app.MapGet("/api/ferramentas/references", async (
    string? reference, string? technicalName, string? lote, string? drawing,
    string? line, string? processo, string? ownerPlant,
    FerramentasService service, CancellationToken ct) =>
{
    var result = await service.ListReferencesAsync(new FerramentasSearchRequest(
        reference, technicalName, lote, drawing, line, processo, ownerPlant), ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

app.MapGet("/api/ferramentas/references/{referenceId:guid}", async (
    Guid referenceId, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.GetReferenceDetailAsync(referenceId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

// Create reference + first lot (atomic).
app.MapPost("/api/ferramentas/reference", async (
    CreateFerramentasRequest request, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.CreateReferenceWithFirstLoteAsync(request, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

// Edit master reference.
app.MapPut("/api/ferramentas/references/{referenceId:guid}", async (
    Guid referenceId, EditFerramentasRequest request, FerramentasService service, CancellationToken ct) =>
{
    if (referenceId != request.ReferenceId)
        return Results.BadRequest(new { code = "FERRAMENTAS_ID_MISMATCH", message = "Identificador da rota não corresponde ao corpo do pedido." });
    var result = await service.EditReferenceAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

// Lotes by reference.
app.MapGet("/api/ferramentas/references/{referenceId:guid}/lotes", async (
    Guid referenceId, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.ListLotesByReferenceAsync(referenceId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

// Duplicate lot (configuration only; master identity read-only).
app.MapPost("/api/ferramentas/lotes/{loteId:guid}/duplicate", async (
    Guid loteId, CreateLoteFromBaseRequest request, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.CreateLoteFromBaseAsync(request, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

// Edit lot.
app.MapPut("/api/ferramentas/lotes/{loteId:guid}", async (
    Guid loteId, EditLoteRequest request, FerramentasService service, CancellationToken ct) =>
{
    if (loteId != request.LoteId)
        return Results.BadRequest(new { code = "FERRAMENTAS_ID_MISMATCH", message = "Identificador da rota não corresponde ao corpo do pedido." });
    var result = await service.EditLoteAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

// Pieces per lote.
app.MapGet("/api/ferramentas/lotes/{loteId:guid}/pieces", async (
    Guid loteId, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.ListPiecesByLoteAsync(loteId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

app.MapPost("/api/ferramentas/lotes/{loteId:guid}/pieces", async (
    Guid loteId, RegisterPieceRequest request, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.RegisterPieceAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { id = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

// Condition change (explicit fact).
app.MapPost("/api/ferramentas/lotes/{loteId:guid}/condition", async (
    Guid loteId, SetConditionRequest request, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.SetConditionAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

// Verification rules (module + ferramentas.configure).
app.MapGet("/api/ferramentas/lotes/{loteId:guid}/rules", async (
    Guid loteId, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.ListCheckRulesByLoteAsync(loteId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

app.MapPost("/api/ferramentas/lotes/{loteId:guid}/rules", async (
    Guid loteId, CheckRuleRequest request, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.AddCheckRuleAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { id = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.FerramentasConfigure);

app.MapPut("/api/ferramentas/rules/{ruleId:guid}", async (
    Guid ruleId, CheckRuleRequest request, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.UpdateCheckRuleAsync(ruleId, request, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.FerramentasConfigure);

app.MapPost("/api/ferramentas/rules/{ruleId:guid}/toggle", async (
    Guid ruleId, ToggleRuleRequest request, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.ToggleCheckRuleAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.FerramentasConfigure);

app.MapDelete("/api/ferramentas/rules/{ruleId:guid}", async (
    Guid ruleId, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.DeleteCheckRuleAsync(ruleId, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(CapabilityPolicies.FerramentasConfigure);

// Active rules for the Job On materialization contract (read-only).
app.MapGet("/api/ferramentas/lotes/{loteId:guid}/rules/active", async (
    Guid loteId, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.ResolveActiveRulesAsync(loteId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

// Armazém (U-14): search / Entrada / Saída / história.
app.MapGet("/api/armazem/consulta", async (
    string? type, string? reference, string? lot, string? position,
    ArmazemService service, CancellationToken ct) =>
{
    var result = await service.ConsultarAsync(new ConsultarRequest(type, reference, lot, position), ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Armazem);

app.MapGet("/api/armazem/movimentos", async (
    DateTimeOffset? from, DateTimeOffset? to, int? limit,
    ArmazemService service, CancellationToken ct) =>
{
    var result = await service.ListMovimentosAsync(from, to, limit ?? 200, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Armazem);

app.MapPost("/api/armazem/entrada", async (
    RegistrarEntradaRequest request, ArmazemService service, CancellationToken ct) =>
{
    var result = await service.RegistrarEntradaAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { stockId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Armazem);

app.MapPost("/api/armazem/saida", async (
    RegistrarSaidaRequest request, ArmazemService service, CancellationToken ct) =>
{
    var result = await service.RegistrarSaidaAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Armazem);

app.MapPost("/api/armazem/corrigir-localizacao", async (
    CorrigirLocalizacaoRequest request, ArmazemService service, CancellationToken ct) =>
{
    var result = await service.CorrigirLocalizacaoAsync(request, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Armazem);

app.MapGet("/api/armazem/{toolType}/historico", async (
    string toolType, string? reference, string? lot,
    ArmazemService service, CancellationToken ct) =>
{
    var result = await service.HistoricoAsync(toolType, reference, lot, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Armazem);

// ============================================================================
// Reparação Externa module (U-15) API endpoints.
// All endpoints require the reparacao_externa module policy (gated via
// ReparacaoExternaAuthorizationGate internally, fail-closed). Physical
// warehouse movement is done through the Armazém-owned repair port — U-15 never
// writes warehouse tables directly (owner decisions B/C/D).
// ============================================================================

static BA.Dmo.Domain.Modules.ReparacaoExterna.RepairType? ParseRepairType(string? value) =>
    value?.Trim().ToUpperInvariant() switch
    {
        "CM" => BA.Dmo.Domain.Modules.ReparacaoExterna.RepairType.CM,
        "MF" => BA.Dmo.Domain.Modules.ReparacaoExterna.RepairType.MF,
        "BQ" => BA.Dmo.Domain.Modules.ReparacaoExterna.RepairType.BQ,
        _ => null
    };

static BA.Dmo.Domain.Modules.ReparacaoExterna.RepairExitStatus? ParseRepairStatus(string? value) =>
    value?.Trim().ToLowerInvariant() switch
    {
        "preparacao" => BA.Dmo.Domain.Modules.ReparacaoExterna.RepairExitStatus.Preparacao,
        "a_retirar" => BA.Dmo.Domain.Modules.ReparacaoExterna.RepairExitStatus.ARetirar,
        "enviado" => BA.Dmo.Domain.Modules.ReparacaoExterna.RepairExitStatus.Enviado,
        "retorno_parcial" => BA.Dmo.Domain.Modules.ReparacaoExterna.RepairExitStatus.RetornoParcial,
        "concluido" => BA.Dmo.Domain.Modules.ReparacaoExterna.RepairExitStatus.Concluido,
        "cancelado" => BA.Dmo.Domain.Modules.ReparacaoExterna.RepairExitStatus.Cancelado,
        _ => null
    };

// Tool-piece search for building a CM/MF repair list.
app.MapGet("/api/reparacao-externa/tools", async (
    string? type, string? reference, string? lot, string? number,
    ReparacaoExternaService service, CancellationToken ct) =>
{
    var repairType = ParseRepairType(type);
    if (repairType is null)
        return Results.BadRequest(new { code = "REPEXT_TYPE", message = "Tipo inválido (CM/MF)." });
    return Results.Ok(await service.SearchToolsAsync(repairType.Value, reference, lot, number, ct));
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

// Create a new CM/MF external repair exit list.
app.MapPost("/api/reparacao-externa", async (
    CreateExitRequest request, ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.CreateExitAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { exitId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

// List exits (Envios) with optional filters.
app.MapGet("/api/reparacao-externa", async (
    string? type, string? status, DateOnly? from, DateOnly? to,
    ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.ListExitsAsync(ParseRepairType(type), ParseRepairStatus(status), from, to, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

// Exit detail (double-click open).
app.MapGet("/api/reparacao-externa/{exitId:guid}", async (
    Guid exitId, ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.GetExitAsync(exitId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

// Add/remove items while the list is in preparation.
app.MapPost("/api/reparacao-externa/{exitId:guid}/items", async (
    Guid exitId, AddExitItemRequest request, ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.AddItemAsync(new AddExitItemRequest(exitId, request.PhysicalPieceId, request.Number), ct);
    return result.IsSuccess ? Results.Ok(new { itemId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

app.MapDelete("/api/reparacao-externa/{exitId:guid}/items/{itemId:guid}", async (
    Guid exitId, Guid itemId, ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.RemoveItemAsync(new RemoveExitItemRequest(exitId, itemId), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

// Disponibilizar (Preparação → A retirar).
app.MapPost("/api/reparacao-externa/{exitId:guid}/disponibilizar", async (
    Guid exitId, ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.DisponibilizarExitAsync(new DisponibilizarExitRequest(exitId), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

// Confirm physical pickup of an item (repair out + Armazém release, atomic).
app.MapPost("/api/reparacao-externa/items/{itemId:guid}/recolha", async (
    Guid itemId, ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.ConfirmPickupAsync(new ConfirmPickupRequest(itemId), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

// Confirm physical return of an item (repair in + Armazém re-occupation, atomic).
app.MapPost("/api/reparacao-externa/items/{itemId:guid}/retorno", async (
    Guid itemId, ConfirmReturnRequest request, ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.ConfirmReturnAsync(new ConfirmReturnRequest(itemId, request.PositionCode), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

// Histórico (transversal read).
app.MapGet("/api/reparacao-externa/historico", async (
    ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.GetHistoryAsync(ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

// Definições: repairers.
app.MapGet("/api/reparacao-externa/repairers", async (
    ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.ListRepairersAsync(ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

app.MapPost("/api/reparacao-externa/repairers", async (
    CreateRepairerRequest request, ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.CreateRepairerAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { id = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

app.MapPut("/api/reparacao-externa/repairers/{repairerId:guid}", async (
    Guid repairerId, UpdateRepairerRequest request, ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.UpdateRepairerAsync(new UpdateRepairerRequest(repairerId, request.Name, request.SupportedTypes), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

app.MapPost("/api/reparacao-externa/repairers/{repairerId:guid}/deactivate", async (
    Guid repairerId, ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.DeactivateRepairerAsync(new DeactivateRepairerRequest(repairerId), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

// Definições: line/type repairer defaults.
app.MapGet("/api/reparacao-externa/line-defaults", async (
    ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.ListLineDefaultsAsync(ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

app.MapPost("/api/reparacao-externa/line-defaults", async (
    UpsertLineDefaultRequest request, ReparacaoExternaService service, CancellationToken ct) =>
{
    var result = await service.UpsertLineDefaultAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoExterna);

// ============================================================================
// Reparação Interna module (U-16) API endpoints.
// All endpoints require the reparacao_interna module policy (gated via
// ReparacaoInternaAuthorizationGate internally, fail-closed). Corrections are
// gated by the reparacao_interna.corrigir capability. Context + tool identity are
// read-only; no Armazém / Ferramentas / Job On write ever happens here.
// ============================================================================

// Internal repair type parser: only CM/MF are recordable internal repair types.
// "BQ" is deliberately NOT mapped here — BQ is not accepted as an internal repair type
// (owner decision CM/MF-only); it may remain only as production/reference context elsewhere.
static BA.Dmo.Domain.Modules.ReparacaoInterna.InternalRepairToolType? ParseInternalToolType(string? value) =>
    value?.Trim().ToUpperInvariant() switch
    {
        "CM" => BA.Dmo.Domain.Modules.ReparacaoInterna.InternalRepairToolType.CM,
        "MF" => BA.Dmo.Domain.Modules.ReparacaoInterna.InternalRepairToolType.MF,
        _ => null
    };

// Line cards for the Registo tab line selector (B1–C3, active reference or none).
app.MapGet("/api/reparacao-interna/line-cards", async (
    ReparacaoInternaService service, CancellationToken ct) =>
{
    var result = await service.ListLineCardsAsync(ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoInterna);

// Resolve the active production context of a line at the current time.
app.MapGet("/api/reparacao-interna/context", async (
    string line, ReparacaoInternaService service, CancellationToken ct) =>
{
    var result = await service.ResolveLineContextAsync(line, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoInterna);

// Register internal repairs against the effective context (validate + persist atomically).
// R009: the request carries a list of numbers; each number persists as its own occurrence.
app.MapPost("/api/reparacao-interna", async (
    RegisterReparacaoRequest request, ReparacaoInternaService service, CancellationToken ct) =>
{
    if (!string.IsNullOrWhiteSpace(request.OverrideProduction)
        || !string.IsNullOrWhiteSpace(request.OverrideReference))
        return Results.BadRequest(new
        {
            code = "REPINT_CONTEXT_READ_ONLY",
            message = "O contexto de Produção/Referência é resolvido automaticamente e não é editável."
        });

    var result = await service.RegistrarReparacoesAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { recordIds = result.Value, count = result.Value.Count })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoInterna);

// History (filters + latest-valid per chain root).
app.MapGet("/api/reparacao-interna/historico", async (
    DateTimeOffset? from, DateTimeOffset? to, string? line, Guid? jobOnId,
    string? type, string? number, string? operatorId, bool? onlyCorrected,
    ReparacaoInternaService service, CancellationToken ct) =>
{
    var filter = new InternalRepairFilter(
        from, to, line, jobOnId, ParseInternalToolType(type), number, operatorId, onlyCorrected ?? false);
    var result = await service.ListHistoryAsync(filter, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoInterna);

// Detail (double-click open; includes the whole correction sequence).
app.MapGet("/api/reparacao-interna/{recordId:guid}", async (
    Guid recordId, ReparacaoInternaService service, CancellationToken ct) =>
{
    var result = await service.GetDetailAsync(recordId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoInterna);

// Correct/override an internal repair (capability reparacao_interna.corrigir;
// NEW row, GLM-DATA-07; R009: never modifies Job On).
app.MapPost("/api/reparacao-interna/{recordId:guid}/corrigir", async (
    Guid recordId, CorrigirReparacaoRequest request, ReparacaoInternaService service, CancellationToken ct) =>
{
    if (request.JobOnId is not null
        || request.JobOnRevisionId is not null
        || !string.IsNullOrWhiteSpace(request.ProductionCode)
        || !string.IsNullOrWhiteSpace(request.Reference)
        || request.LotId is not null)
        return Results.BadRequest(new
        {
            code = "REPINT_CONTEXT_READ_ONLY",
            message = "O contexto original é read-only; ao mudar de Linha, o contexto é recalculado automaticamente."
        });

    var result = await service.CorrigirReparacaoAsync(
        new CorrigirReparacaoRequest(recordId, request.Line, request.ToolType, request.IndividualNumber,
            request.JobOnId, request.JobOnRevisionId, request.ProductionCode, request.Reference,
            request.LotId, request.Reason), ct);
    return result.IsSuccess ? Results.Ok(new { recordId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.ReparacaoInterna);

// ============================================================================
// Folha de Controlo (R010) API endpoints.
// Production-level control summary sheet INSIDE the Controlo area. The surface
// uses the single Controlo top-level grant; operations are
// gated internally by the controlo.* capabilities (view/edit/submit/review).
// ============================================================================

// Create-or-load the Folha de Controlo for the already-selected production (job_on_id).
app.MapGet("/api/controlo/production", async (
    Guid jobOnId, ControloSheetService service, CancellationToken ct) =>
{
    var result = await service.GetForProductionAsync(jobOnId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Controlo);

// History list (free-mode consultation) of Folha de Controlo summaries.
app.MapGet("/api/controlo/list", async (
    DateTimeOffset? from, DateTimeOffset? to, string? machine, Guid? jobOn, string? status,
    ControloSheetService service, CancellationToken ct) =>
{
    var result = await service.ListSheetsAsync(from, to, machine, jobOn, status, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Controlo);

// Create-or-load by production code + machine (the context a selected Peso production row
// carries) — the user never re-searches the production.
app.MapGet("/api/controlo/by-production", async (
    string production, string? machine, ControloSheetService service, CancellationToken ct) =>
{
    var result = await service.GetForProductionByContextAsync(production, machine, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Controlo);

// Create a new draft Folha de Controlo for a production.
app.MapPost("/api/controlo", async (
    CreateControloSheetRequest request, ControloSheetService service, CancellationToken ct) =>
{
    var result = await service.CreateAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { sheetId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Controlo);

// Folha de Controlo detail (current items + full event history).
app.MapGet("/api/controlo/{sheetId:guid}", async (
    Guid sheetId, ControloSheetService service, CancellationToken ct) =>
{
    var result = await service.GetDetailAsync(sheetId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Controlo);

// Apply item controls (OK/NOK + observation + MCaliper link).
app.MapPost("/api/controlo/{sheetId:guid}/items", async (
    Guid sheetId, UpdateControloSheetItemsRequest request, ControloSheetService service, CancellationToken ct) =>
{
    var result = await service.UpdateItemsAsync(new UpdateControloSheetItemsRequest(
        sheetId, request.Edits ?? Array.Empty<ControloFolhaItemControlEdit>()), ct);
    return result.IsSuccess ? Results.Ok()
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Controlo);

// Submit/deliver the sheet.
app.MapPost("/api/controlo/{sheetId:guid}/submit", async (
    Guid sheetId, SubmitControloSheetRequest request, ControloSheetService service, CancellationToken ct) =>
{
    var result = await service.SubmitAsync(new SubmitControloSheetRequest(sheetId, request.Note), ct);
    return result.IsSuccess ? Results.Ok()
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Controlo);

// Reopen a submitted/decided sheet for editing (audit traced).
app.MapPost("/api/controlo/{sheetId:guid}/reopen", async (
    Guid sheetId, ReopenControloSheetRequest request, ControloSheetService service, CancellationToken ct) =>
{
    var result = await service.ReopenAsync(new ReopenControloSheetRequest(sheetId), ct);
    return result.IsSuccess ? Results.Ok()
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Controlo);

// Responsible/chief review decision (aprovado/rejeitado).
app.MapPost("/api/controlo/{sheetId:guid}/decide", async (
    Guid sheetId, DecideControloSheetRequest request, ControloSheetService service, CancellationToken ct) =>
{
    var result = await service.DecideAsync(new DecideControloSheetRequest(sheetId, request.Decision, request.Note), ct);
    return result.IsSuccess ? Results.Ok()
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Controlo);

// ============================================================================
// Tampões module (U-17) API endpoints.
// All endpoints require the tampoes module policy (gated via
// TampaoAuthorizationGate internally, fail-closed). Operator has FULL access
// (GLM-TP-02). The atomic state/configuração transforms update both saldos +
// movement + audit in ONE transaction server-side.
// ============================================================================

static BA.Dmo.Domain.Modules.Tampoes.TampaoMovementType? ParseTampaoMovementType(string? value) =>
    value?.Trim().ToLowerInvariant() switch
    {
        "adicionar" => BA.Dmo.Domain.Modules.Tampoes.TampaoMovementType.Adicionar,
        "remover" => BA.Dmo.Domain.Modules.Tampoes.TampaoMovementType.Remover,
        "alterar_estado" => BA.Dmo.Domain.Modules.Tampoes.TampaoMovementType.AlterarEstado,
        "alterar_configuracao" => BA.Dmo.Domain.Modules.Tampoes.TampaoMovementType.AlterarConfiguracao,
        _ => null
    };

// Consulta: configurations with balances (optional filter by configuration id).
app.MapGet("/api/tampoes/consulta", async (
    Guid? configurationId, string? machine, TampaoService service, CancellationToken ct) =>
{
    var result = await service.ConsultarAsync(new ConsultaFilter(configurationId, machine), ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

// R008 — record/detail sheet for one configuration (config + saldos + machines + notes + machine-event history).
app.MapGet("/api/tampoes/configuracao/{configurationId:guid}/detalhe", async (
    Guid configurationId, TampaoService service, CancellationToken ct) =>
{
    var result = await service.GetConfigurationDetailAsync(configurationId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

// R008 — set the machine set of a configuration (never duplicates the configuration).
app.MapPost("/api/tampoes/configuracao/{configurationId:guid}/maquinas", async (
    Guid configurationId, SetConfigurationMachinesRequest request, TampaoService service, CancellationToken ct) =>
{
    var result = await service.SetConfigurationMachinesAsync(
        new SetConfigurationMachinesRequest(configurationId, request.Machines), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

// R008 — append a comment/note (kept for history).
app.MapPost("/api/tampoes/configuracao/{configurationId:guid}/observacao", async (
    Guid configurationId, AddConfigurationNoteRequest request, TampaoService service, CancellationToken ct) =>
{
    var result = await service.AddConfigurationNoteAsync(
        new AddConfigurationNoteRequest(configurationId, request.Note), ct);
    return result.IsSuccess ? Results.Ok(new { noteId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

app.MapGet("/api/tampoes/configuracao/{configurationId:guid}", async (
    Guid configurationId, TampaoService service, CancellationToken ct) =>
{
    var result = await service.GetConfigurationAsync(configurationId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

// Registo: adicionar / remover a single balance.
app.MapPost("/api/tampoes/quantidade/adicionar", async (
    AdicionarQuantidadeRequest request, TampaoService service, CancellationToken ct) =>
{
    var result = await service.AdicionarQuantidadeAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { movementId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

app.MapPost("/api/tampoes/quantidade/remover", async (
    RemoverQuantidadeRequest request, TampaoService service, CancellationToken ct) =>
{
    var result = await service.RemoverQuantidadeAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { movementId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

// Alterar estado: single atomic transfer Enchidos ↔ Por encher.
app.MapPost("/api/tampoes/estado/alterar", async (
    AlterarEstadoRequest request, TampaoService service, CancellationToken ct) =>
{
    var result = await service.AlterarEstadoAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { movementId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

// Alterar configuração: atomic origin → destination (reuse/create, same transaction).
app.MapPost("/api/tampoes/configuracao/alterar", async (
    AlterarConfiguracaoRequest request, TampaoService service, CancellationToken ct) =>
{
    var result = await service.AlterarConfiguracaoAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { movementId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

// Histórico (movimentos imutáveis).
app.MapGet("/api/tampoes/movimentos", async (
    DateTimeOffset? from, DateTimeOffset? to, Guid? configurationId, string? type, string? operatorId,
    TampaoService service, CancellationToken ct) =>
{
    var result = await service.ListMovimentosAsync(from, to, configurationId,
        ParseTampaoMovementType(type), operatorId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

// Opções: fields & values (full Operator access).
app.MapGet("/api/tampoes/opcoes/fields", async (
    bool onlyActive, TampaoService service, CancellationToken ct) =>
{
    var result = await service.ListFieldDefsAsync(onlyActive, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

app.MapGet("/api/tampoes/opcoes/fields/{fieldDefId:guid}/values", async (
    Guid fieldDefId, bool onlyActive, TampaoService service, CancellationToken ct) =>
{
    var result = await service.ListFieldValuesAsync(fieldDefId, onlyActive, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

app.MapPost("/api/tampoes/opcoes/fields", async (
    CreateFieldDefRequest request, TampaoService service, CancellationToken ct) =>
{
    var result = await service.CreateFieldDefAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { fieldDefId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

app.MapPost("/api/tampoes/opcoes/values", async (
    CreateFieldValueRequest request, TampaoService service, CancellationToken ct) =>
{
    var result = await service.CreateFieldValueAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { fieldValueId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Tampoes);

// ============================================================================
// História module (U-18) API endpoints. READ-ONLY transversal history over the
// canonical append-only audit_events table, restricted by TD-24 origin-module
// grants (re-checked server-side by HistoriaAuthorizationGate, fail-closed).
// ============================================================================

// Grouped transversal history (entity groups ordered by latest event).
app.MapGet("/api/historia", async (
    string? query, string? module, string? action, string? actor,
    string? result, DateTime? from, DateTime? to,
    int pageSize, int page, HistoriaService service, CancellationToken ct) =>
{
    var filter = new HistoriaFilter(
        query, EntityType: null, EntityId: null, module, action, actor, result,
        from.HasValue ? new DateTimeOffset(from.Value, TimeSpan.Zero) : null,
        to.HasValue ? new DateTimeOffset(to.Value, TimeSpan.Zero) : null,
        page < 1 ? 1 : page, pageSize is > 0 ? pageSize : 20);
    var resultQuery = await service.QueryAsync(filter, ct);
    return resultQuery.IsSuccess ? Results.Ok(resultQuery.Value)
        : Results.BadRequest(new { code = resultQuery.Error.Code, message = resultQuery.Error.Message });
}).RequireAuthorization(ModulePolicies.Historia);

// Flat events for a single entity (expanded History Entry detail data).
app.MapGet("/api/historia/events", async (
    string entityType, string entityId, int pageSize, int page,
    HistoriaService service, CancellationToken ct) =>
{
    var filter = new HistoriaFilter(
        Query: null,
        EntityType: entityType,
        EntityId: entityId,
        ModuleId: null, ActionCode: null, Actor: null, Result: null,
        FromUtc: null, ToUtc: null,
        Page: page < 1 ? 1 : page, PageSize: pageSize is > 0 ? pageSize : 20);
    var rows = await service.QueryFlatAsync(filter, ct);
    return rows.IsSuccess ? Results.Ok(rows.Value)
        : Results.BadRequest(new { code = rows.Error.Code, message = rows.Error.Message });
}).RequireAuthorization(ModulePolicies.Historia);

// ============================================================================
// Boquilhas module (U-19) API. Canonical daily, high-frequency, quantity-based
// operational flow (reference + lot) over the bq_* schema. All endpoints require
// the boquilhas module policy; the service re-checks it server-side and attributes
// every write to the resolved actor. The 20→25 excess-return rule is preserved:
// a return larger than the expected repair balance is recorded in full and
// surfaces as a warning + open discrepancy — never a block (UD-08/UD-09).
// ============================================================================

// Sidepanel live production context — reuses R009 reparacao-interna projection
// so Boquilhas consumers see what Job On is producing on each line.
app.MapGet("/api/boquilhas/production-context", async (
    ReparacaoInternaService service, CancellationToken ct) =>
{
    var result = await service.ListLineCardsAsync(ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

// Create a lot + its first production trace + START + initial utilisation (atomic).
app.MapPost("/api/boquilhas/lotes", async (
    CreateBqLoteRequest request, BoquilhasService service, CancellationToken ct) =>
{
    var result = await service.CreateLoteWithTraceAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { lotId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

// List operational/closed-trace lots (Registo / Boquilhas tabs). Generic BQ
// scrap lifecycle is obsolete; historical rows stay stored but are not an
// active Boquilhas surface.
app.MapGet("/api/boquilhas/lotes", async (
    string? search, bool? onlyAvailable, string? lifecycle, int page, int pageSize,
    BoquilhasService service, CancellationToken ct) =>
{
    if (lifecycle is not null and not ("available" or "archived"))
        return Results.BadRequest(new
        {
            code = "BQ_LIFECYCLE_FILTER_INVALID",
            message = "O estado pedido não pertence ao fluxo ativo de Boquilhas."
        });

    BqLifecycleState? state = lifecycle switch
    {
        null => null,
        "available" => BqLifecycleState.Available,
        "archived" => BqLifecycleState.Archived,
        _ => null
    };
    var result = await service.ListLotesAsync(new BqLoteFilter(
        search, onlyAvailable, state, page < 1 ? 1 : page, pageSize is > 0 ? pageSize : 20), ct);
    return result.IsSuccess ? Results.Ok(result.Value
            .Where(lote => lote.LifecycleState != BqLifecycleState.Scrapped))
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

// Lot summary (resumo + active trace + saldo + utilisation).
app.MapGet("/api/boquilhas/lotes/{lotId:guid}", async (
    Guid lotId, BoquilhasService service, CancellationToken ct) =>
{
    var result = await service.GetLotSummaryAsync(lotId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

// Register a movement (Saída/Entrada/Não reparadas/Linha/Corrigir contagem).
app.MapPost("/api/boquilhas/movements", async (
    RegisterBqMovementRequest request, BoquilhasService service, CancellationToken ct) =>
{
    var result = await service.RegisterMovementAsync(request, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

// List movements (Registo lot list or Histórico aggregate by lot + search).
app.MapGet("/api/boquilhas/movements", async (
    Guid? lotId, string? search, string? type, Guid? repairerId, DateTime? from, DateTime? to,
    int page, int pageSize, BoquilhasService service, CancellationToken ct) =>
{
    var filter = new BqHistoryFilter(
        lotId, search,
        type is null ? null : BqMovementTypeCodec.FromStorage(type),
        repairerId,
        from.HasValue ? new DateTimeOffset(from.Value, TimeSpan.Zero) : null,
        to.HasValue ? new DateTimeOffset(to.Value, TimeSpan.Zero) : null,
        page < 1 ? 1 : page, pageSize is > 0 ? pageSize : 20);
    var result = await service.ListMovementsAsync(lotId, filter, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

// Close an active trace (immutable final snapshot of summary + current state).
app.MapPost("/api/boquilhas/traces/{traceId:guid}/close", async (
    Guid traceId, CloseBqTraceRequest request, BoquilhasService service, CancellationToken ct) =>
{
    var result = await service.CloseTraceAsync(request with { BqTraceId = traceId }, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

// Reopen the last closed trace (only when no other trace is active).
app.MapPost("/api/boquilhas/traces/{traceId:guid}/reopen", async (
    Guid traceId, ReopenBqTraceRequest request, BoquilhasService service, CancellationToken ct) =>
{
    var result = await service.ReopenTraceAsync(request with { BqTraceId = traceId }, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

// Discrepancies (return excess) + resolution.
app.MapGet("/api/boquilhas/discrepancies", async (
    Guid? lotId, BoquilhasService service, CancellationToken ct) =>
{
    var result = await service.ListDiscrepanciesAsync(lotId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

app.MapPost("/api/boquilhas/discrepancies/{discrepancyId:guid}/resolve", async (
    Guid discrepancyId, ResolveBqDiscrepancyRequest request, BoquilhasService service, CancellationToken ct) =>
{
    var result = await service.ResolveDiscrepancyAsync(new ResolveBqDiscrepancyRequest(
        discrepancyId, request.ResolutionNote), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

// Repairers (canonical vocabulary) + line defaults.
app.MapGet("/api/boquilhas/repairers", async (
    bool? onlyActive, string? type, BoquilhasService service, CancellationToken ct) =>
{
    var result = await service.ListRepairersAsync(onlyActive ?? true, type, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

app.MapPost("/api/boquilhas/repairers", async (
    CreateBqRepairerRequest request, BoquilhasService service, CancellationToken ct) =>
{
    var result = await service.CreateRepairerAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { repairerId = result.Value })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

app.MapPut("/api/boquilhas/repairers/{repairerId:guid}", async (
    Guid repairerId, UpdateBqRepairerRequest request, BoquilhasService service, CancellationToken ct) =>
{
    var result = await service.UpdateRepairerAsync(new UpdateBqRepairerRequest(
        repairerId, request.Name, request.Active), ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

app.MapPost("/api/boquilhas/lines", async (
    SetLineRepairerDefaultRequest request, BoquilhasService service, CancellationToken ct) =>
{
    var result = await service.SetLineRepairerDefaultAsync(request, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Boquilhas);

// ============================================================================
// Ferramentas utilisation (R003): append-only readings per tool_lote. % use is
// taken MANUALLY from SAP by the operator (recorded fact) — no auto formula.
// ============================================================================

app.MapPost("/api/ferramentas/lotes/{loteId:guid}/utilizacao", async (
    Guid loteId, RecordToolUtilisationRequest request, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.RecordUtilisationReadingAsync(
        request with { ToolLoteId = loteId }, ct);
    return result.IsSuccess ? Results.Ok(new { ok = true })
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

app.MapGet("/api/ferramentas/lotes/{loteId:guid}/utilizacao", async (
    Guid loteId, FerramentasService service, CancellationToken ct) =>
{
    var result = await service.GetUtilisationAsync(loteId, ct);
    return result.IsSuccess ? Results.Ok(result.Value)
        : Results.BadRequest(new { code = result.Error.Code, message = result.Error.Message });
}).RequireAuthorization(ModulePolicies.Ferramentas);

app.Run();
return 0;

// Exposes the generated entry point to the integration test project (tests/* only).
public partial class Program;
