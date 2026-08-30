using System.Net;
using System.Net.Http.Json;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BA.Dmo.IntegrationTests.JobOnAccess;

/// <summary>
/// NOTE: the namespace deliberately avoids "BA.Dmo.IntegrationTests.JobOn",
/// which would shadow the JobOn domain type for sibling test files under
/// BA.Dmo.IntegrationTests.* (jobon-landing tests, access tests).
///
/// PHASE 4 — Job On authorization/isolation (U-07 route + endpoint level).
/// Verifies the ACCESS RESOLVER derivation contract end-to-end through the
/// real pipeline:
///   - jobon.view is derived from Job On module presence (the operator/
///     control profile gets /jobon; the legacy capability arrays inside
///     ModulesJson are deliberately NOT authorization input);
///   - a user WITHOUT the Job On module fails Job On authorization with the
///     safe /access-denied deep-link state (never a data leak, never a loop);
///   - operation-level isolation is enforced server-side: the edit endpoints
///     (/api/jobon/{id}/image/replace) require jobon.edit, which only the
///     Responsible profile receives — 403 without it, admitted with it.
/// Mirrors the FerramentasWebApiTests authorization-guard proof style. All
/// collaborators are fakes — no live Supabase/DB.
/// </summary>
public class JobOnAuthorizationTests : IClassFixture<JobOnAuthorizationTests.AuthFixture>
{
    private static readonly Guid AuthUserId =
        Guid.Parse("88888888-1111-2222-3333-444444444444");

    private static readonly Guid SomeJobOnId =
        Guid.Parse("44444444-5555-6666-7777-888888888888");

    private readonly AuthFixture _fixture;

    public JobOnAuthorizationTests(AuthFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task JobOnModuleWithOperatorController_GrantsJobOnView()
    {
        // Operator / Controlador profile + Job On module grant. The resolver
        // derives jobon.view + jobon.confirmar from module presence; the
        // legacy capability arrays in ModulesJson are NOT authorization input.
        _fixture.Repository.User = _fixture.JobOnOperator();
        var client = await LoginAsync();

        var response = await client.GetAsync("/jobon");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-view=\"planeamento\"", html);

        // jobon.edit is NOT derived for the operator/control profile: the
        // privileged folha-edit surface stays hidden server-side.
        Assert.DoesNotContain("Editar folha", html);
        Assert.DoesNotContain("Criar Job On", html);
    }

    [Fact]
    public async Task WithoutJobOnModule_JobOnIsDenied_WithSafeAccessDeniedState()
    {
        // A functional user (Boquilhas module) without the Job On module has
        // no jobon.view, so the /jobon landing policy fails closed.
        _fixture.Repository.User = _fixture.JobOnlessUser();
        var client = await LoginAsync();

        var denied = await client.GetAsync("/jobon");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/access-denied", denied.Headers.Location!.PathAndQuery);

        // Deep-link rule (GLM-ACC-07 s10): the safe state resolves the user's
        // own authorized area and redirects with feedback — never a data
        // leak, never a redirect loop.
        var safe = await client.GetAsync("/access-denied");
        Assert.Equal(HttpStatusCode.Redirect, safe.StatusCode);
        Assert.Equal("/boquilhas?acesso-negado=1", safe.Headers.Location!.ToString());
    }

    [Fact]
    public async Task JobOnModuleWithoutEditCapability_EditEndpointIsForbidden()
    {
        // Operator / Controlador holds jobon.view but NOT jobon.edit: the
        // route-level capability policy must deny the edit endpoint with 403
        // (same proof as FerramentasWebApiTests capability guard).
        _fixture.Repository.User = _fixture.JobOnOperator();
        var client = await LoginAsync();

        var denied = await client.PostAsJsonAsync(
            $"/api/jobon/{SomeJobOnId}/image/replace",
            new { jobOnId = SomeJobOnId, imageAssetId = "nope/evil.png" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task ResponsibleProfileWithJobOnModule_EditEndpointIsAdmitted()
    {
        // The Responsible profile derives jobon.edit from the Job On module:
        // the route-level policy admits the call. The request then fails
        // service-level validation (the image asset id is rejected by
        // ArticleReferenceImageRules before any write) — 400, NOT 401/403,
        // proving the capability gate opened.
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/jobon/{SomeJobOnId}/image/replace",
            new { jobOnId = SomeJobOnId, imageAssetId = "nope/evil.png" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The Responsible /jobon surface exposes the edit capability.
        var html = await (await client.GetAsync("/jobon")).Content.ReadAsStringAsync();
        Assert.Contains("Editar folha", html);
        Assert.Contains("Criar Job On", html);
    }

    // ---- create flow (R011) ------------------------------------------------

    [Fact]
    public async Task OperatorWithoutEditCapability_CannotCreateJobOn()
    {
        // Test #2 — a user with only jobon.view fails closed on the WRITE
        // operation: the route-level jobon.edit policy denies POST /api/jobon
        // with 403 before any code runs.
        _fixture.Repository.User = _fixture.JobOnOperator();
        var client = await LoginAsync();

        var denied = await client.PostAsJsonAsync("/api/jobon", new
        {
            productionCode = "202699",
            machineCode = "B1",
            plannedStartAt = "2026-08-20",
            plannedEndAt = (string?)null,
            reference = "9262T288"
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task ResponsibleProfile_CreatesJobOn_AndOpensTheCreatedFolha()
    {
        // Tests #1, #4, #5 — Responsible + Job On module creates a REAL Job On
        // (header + initial revision) and the creation target resolves into the
        // newly created Folha Job On (/jobon?id={jobOnId}).
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        var created = await client.PostAsJsonAsync("/api/jobon", new
        {
            productionCode = "202620",
            machineCode = "C1",
            plannedStartAt = "2026-08-21",
            plannedEndAt = (string?)null,
            reference = "5447T173"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var payload = await created.Content.ReadFromJsonAsync<CreateJobOnResponse>();
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.JobOnId);

        // The redirect target opens the created Folha Job On (real projection).
        var folha = await client.GetAsync($"/jobon?id={payload.JobOnId}");
        Assert.Equal(HttpStatusCode.OK, folha.StatusCode);
        var html = await folha.Content.ReadAsStringAsync();
        Assert.Contains("data-initial-view=\"sheet\"", html); // folha opens, not planning
        Assert.Contains("meta name=\"jobon-id\" content=\"" + payload.JobOnId, html);
        Assert.Contains("5447T173", html); // the entered reference renders in the folha
        Assert.Contains("202620", html);   // the entered production renders in the folha
    }

    [Fact]
    public async Task ResponsibleProfile_CreateWithMissingReference_IsRejected()
    {
        // Test #3 — required creation data is validated server-side (400).
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        var rejected = await client.PostAsJsonAsync("/api/jobon", new
        {
            productionCode = "202620",
            machineCode = "B1",
            plannedStartAt = "2026-08-21",
            plannedEndAt = (string?)null,
            reference = "   "
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var body = await rejected.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("JOBON_INVALID", body?.Code);
    }

    // ---- duplicate flow (modules/05 §6.2) ----------------------------------

    [Fact]
    public async Task OperatorWithoutEditCapability_CannotDuplicateJobOn()
    {
        // A user with only jobon.view fails closed on the WRITE operation at the
        // route level: POST /api/jobon/{id}/duplicate requires jobon.edit and is
        // denied with 403 before any code runs.
        _fixture.Repository.User = _fixture.JobOnOperator();
        var client = await LoginAsync();

        var denied = await client.PostAsJsonAsync(
            $"/api/jobon/{SomeJobOnId}/duplicate",
            new { productionCode = "202699", machineCode = "B1" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task ResponsibleProfile_DuplicatesJobOn_AndOpensTheDuplicatedFolha()
    {
        // Tests #1, #3, #4, #6, #10 — Responsible + jobon.edit duplicates a REAL
        // Job On: the new production/date context is applied (new header + copied
        // initial revision), a NEW JobOnId is returned, the source Job On remains
        // untouched, and the success target opens the duplicated Folha Job On
        // (/jobon?id={newJobOnId}).
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        // 1. Create the source Job On through the real create flow.
        var created = await client.PostAsJsonAsync("/api/jobon", new
        {
            productionCode = "202608",
            machineCode = "B1",
            plannedStartAt = "2026-08-17",
            plannedEndAt = (string?)null,
            reference = "5447T173"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var creation = await created.Content.ReadFromJsonAsync<CreateJobOnResponse>();
        var sourceId = creation!.JobOnId;

        // 2. Duplicate it with the NEW production/date context.
        var duplicated = await client.PostAsJsonAsync(
            $"/api/jobon/{sourceId}/duplicate",
            new
            {
                productionCode = "202699",
                machineCode = "C1",
                plannedStartAt = "2026-08-24",
                plannedEndAt = "2026-08-25"
            });
        Assert.Equal(HttpStatusCode.OK, duplicated.StatusCode);
        var payload = await duplicated.Content.ReadFromJsonAsync<CreateJobOnResponse>();
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.JobOnId);
        Assert.NotEqual(sourceId, payload.JobOnId); // a NEW JobOnId, never the source's

        // 3. The success target opens the newly created Folha Job On.
        var newFolha = await client.GetAsync($"/jobon?id={payload.JobOnId}");
        Assert.Equal(HttpStatusCode.OK, newFolha.StatusCode);
        var newHtml = await newFolha.Content.ReadAsStringAsync();
        Assert.Contains("data-initial-view=\"sheet\"", newHtml);
        Assert.Contains("meta name=\"jobon-id\" content=\"" + payload.JobOnId, newHtml);
        Assert.Contains("202699", newHtml); // the new production renders in the folha
        Assert.Contains("5447T173", newHtml); // the source reference is reused

        // 4. The source Folha Job On remains untouched.
        var sourceFolha = await client.GetAsync($"/jobon?id={sourceId}");
        Assert.Equal(HttpStatusCode.OK, sourceFolha.StatusCode);
        var sourceHtml = await sourceFolha.Content.ReadAsStringAsync();
        Assert.Contains("meta name=\"jobon-id\" content=\"" + sourceId, sourceHtml);
        Assert.Contains("202608", sourceHtml);
    }

    [Fact]
    public async Task ResponsibleProfile_DuplicateUnknownSource_ReturnsCleanNotFound()
    {
        // An unknown source maps to the existing clean NotFound behavior, never
        // a raw 500 (the identity-conflict mapping itself is unit-proven:
        // Duplicate_IdentityDuplicate_Raw23505_MapsToCleanDomainConflict).
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        var denied = await client.PostAsJsonAsync(
            $"/api/jobon/{SomeJobOnId}/duplicate",
            new { productionCode = "202699", machineCode = "B1" });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); // unknown source: clean 404
    }

    // ---- alter-date flow ("Alterar data", modules/05) -----------------------

    [Fact]
    public async Task OperatorWithoutEditCapability_CannotAlterJobOnDate()
    {
        // Test #2 — an Operator/Controller with only jobon.view fails closed on the
        // WRITE operation at the route level: POST /api/jobon/{id}/date requires
        // jobon.edit and is denied with 403 before any code runs. Hiding the button
        // is never the only guard.
        _fixture.Repository.User = _fixture.JobOnOperator();
        var client = await LoginAsync();

        var denied = await client.PostAsJsonAsync(
            $"/api/jobon/{SomeJobOnId}/date",
            new { plannedStartAt = "2026-08-25", plannedEndAt = "2026-08-26" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task ResponsibleProfile_AltersDate_OnSameJobOn_AndReopensTheSameFolha()
    {
        // Tests #1, #3–#9, #12 — Responsible + jobon.edit alters the planned dates of
        // an EXISTING Job On: a NEW revision of the SAME job_on_id is created (never a
        // new Job On), the revision number increments, the previous revision stays
        // untouched, current_revision_id advances, and the success target reopens the
        // SAME folha (/jobon?id={sameJobOnId}) rendering the new dates.
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        // 1. Create the Job On through the real create flow (revision 1).
        var created = await client.PostAsJsonAsync("/api/jobon", new
        {
            productionCode = "202608",
            machineCode = "B1",
            plannedStartAt = "2026-08-17",
            plannedEndAt = (string?)null,
            reference = "5447T173"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var creation = await created.Content.ReadFromJsonAsync<CreateJobOnResponse>();
        var jobOnId = creation!.JobOnId;

        var before = await client.GetAsync($"/jobon?id={jobOnId}");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeHtml = await before.Content.ReadAsStringAsync();
        Assert.Contains("meta name=\"jobon-id\" content=\"" + jobOnId, beforeHtml);
        Assert.Contains("id=\"jobStartDate\" type=\"date\" value=\"2026-08-17\"", beforeHtml);
        Assert.Contains("202608", beforeHtml); // production unchanged on the same folha

        // 2. Alter the planned dates on the SAME Job On.
        var altered = await client.PostAsJsonAsync(
            $"/api/jobon/{jobOnId}/date",
            new { plannedStartAt = "2026-08-25", plannedEndAt = "2026-08-26" });
        Assert.Equal(HttpStatusCode.OK, altered.StatusCode);
        var payload = await altered.Content.ReadFromJsonAsync<AlterDateResponse>();
        Assert.NotNull(payload);
        Assert.Equal(jobOnId, payload!.JobOnId);      // SAME JobOnId — never a new Job On
        Assert.NotEqual(Guid.Empty, payload.RevisionId); // a NEW revision id

        var after = await client.GetAsync($"/jobon?id={jobOnId}");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var afterHtml = await after.Content.ReadAsStringAsync();
        // The SAME folha reopens rendering the new dates (new current revision).
        Assert.Contains("meta name=\"jobon-id\" content=\"" + jobOnId, afterHtml);
        Assert.Contains("meta name=\"jobon-revision-id\" content=\"" + payload.RevisionId, afterHtml);
        Assert.Contains("id=\"jobStartDate\" type=\"date\" value=\"2026-08-25\"", afterHtml);
        Assert.Contains("id=\"jobEndDate\" type=\"date\" value=\"2026-08-26\"", afterHtml);
        Assert.Contains("202608", afterHtml);   // production unchanged
        Assert.Contains("5447T173", afterHtml); // reference unchanged
    }

    // ---- edit / save-new-revision flow ("Guardar nova revisão", TD-18) ------

    [Fact]
    public async Task OperatorWithoutEditCapability_CannotSaveRevision()
    {
        // Test #2 — a user with only jobon.view fails closed on the WRITE operation
        // at the route level: POST /api/jobon/{id}/revision requires jobon.edit and
        // is denied with 403 before any code runs. Hiding the button is never the
        // only guard.
        _fixture.Repository.User = _fixture.JobOnOperator();
        var client = await LoginAsync();

        var denied = await client.PostAsJsonAsync(
            $"/api/jobon/{SomeJobOnId}/revision",
            new { generalNotes = "edit", changeReason = (string?)null, components = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task ResponsibleProfile_SavesNewRevision_SameJobOn_AndReopensTheSameFolha()
    {
        // Tests #1, #3–#9, #12, #17 — Responsible + jobon.edit saves an EDITED
        // revision of an EXISTING Job On: a NEW revision of the SAME job_on_id is
        // created (never a new Job On), the revision number increments, the previous
        // revision stays untouched, current_revision_id advances, and the success
        // target reopens the SAME folha (/jobon?id={sameJobOnId}) rendering the new
        // current revision with the edited values.
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        // 1. Create the Job On through the real create flow (revision 1).
        var created = await client.PostAsJsonAsync("/api/jobon", new
        {
            productionCode = "202608",
            machineCode = "B1",
            plannedStartAt = "2026-08-17",
            plannedEndAt = (string?)null,
            reference = "5447T173"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var creation = await created.Content.ReadFromJsonAsync<CreateJobOnResponse>();
        var jobOnId = creation!.JobOnId;

        var before = await client.GetAsync($"/jobon?id={jobOnId}");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeHtml = await before.Content.ReadAsStringAsync();
        Assert.Contains("meta name=\"jobon-id\" content=\"" + jobOnId, beforeHtml);
        Assert.Contains("meta name=\"jobon-revision-id\"", beforeHtml);
        Assert.DoesNotContain("Notas editadas na revisao 2", beforeHtml);

        // 2. Save an edited revision of the SAME Job On.
        var saved = await client.PostAsJsonAsync(
            $"/api/jobon/{jobOnId}/revision",
            new
            {
                jobOnId,
                generalNotes = "Notas editadas na revisao 2",
                changeReason = (string?)null,
                imageAssetId = (string?)null,
                components = Array.Empty<object>()
            });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var payload = await saved.Content.ReadFromJsonAsync<SaveRevisionResponse>();
        Assert.NotNull(payload);
        Assert.Equal(jobOnId, payload!.JobOnId);            // SAME JobOnId — never a new Job On
        Assert.NotEqual(Guid.Empty, payload.RevisionId);    // a NEW revision id

        // 3. The SAME folha reopens rendering the new current revision.
        var after = await client.GetAsync($"/jobon?id={jobOnId}");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var afterHtml = await after.Content.ReadAsStringAsync();
        Assert.Contains("meta name=\"jobon-id\" content=\"" + jobOnId, afterHtml);
        Assert.Contains("meta name=\"jobon-revision-id\" content=\"" + payload.RevisionId, afterHtml);
        Assert.Contains("Notas editadas na revisao 2", afterHtml); // edited value renders
        Assert.Contains("202608", afterHtml);   // production unchanged
        Assert.Contains("5447T173", afterHtml); // reference unchanged
    }

    // ---- "Alterar CM/MF/BQ associado" — tool-selection options (TD-18) ------

    [Fact]
    public async Task ToolOptions_WithoutEditCapability_DeniedWithSafeAccessDeniedState()
    {
        // GET /api/jobon/{id}/tool-options is an edit surface: the route policy
        // requires jobon.edit, so an operator with only jobon.view is denied
        // before any register read (hiding the button is never the only guard).
        // App GET denial contract: an authenticated denied user gets the safe
        // /access-denied deep-link state (redirect) — never data, never a loop.
        _fixture.Repository.User = _fixture.JobOnOperator();
        var client = await LoginAsync();

        var denied = await client.GetAsync(
            $"/api/jobon/{SomeJobOnId}/tool-options?family=CM");

        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/access-denied", denied.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task ToolOptions_Responsible_ReturnsOnlyRegisteredLotsForTheJobOnMachine()
    {
        // The options come ONLY from the real (fake) N04 register, filtered by
        // the Job On's machine: a CM lote registered for B2 is offered on a B2
        // Job On and never on a C3 one; CM and MF sharing reference "5447" are
        // DISTINCT tools (distinct reference/lot ids, never merged).
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        var b2 = await CreateJobOnAsync(client, "202631", "B2", "5447T173");
        var c3 = await CreateJobOnAsync(client, "202632", "C3", "5447T173");

        var cmB2 = await client.GetAsync($"/api/jobon/{b2}/tool-options?family=CM");
        Assert.Equal(HttpStatusCode.OK, cmB2.StatusCode);
        var cmB2Payload = await cmB2.Content.ReadFromJsonAsync<ToolOptionsResponse>();
        Assert.NotNull(cmB2Payload);
        Assert.Equal("B2", cmB2Payload!.Machine);
        Assert.Equal("CM", cmB2Payload.Family);
        // The CM 5447 lote registered for B2 is offered...
        Assert.Contains(cmB2Payload.Items, i => i.Lot == "1" && i.Reference == "5447");
        // ...and the C3-only CM lote is NOT offered on B2.
        Assert.DoesNotContain(cmB2Payload.Items, i => i.Lot == "3");

        var cmC3 = await client.GetAsync($"/api/jobon/{c3}/tool-options?family=CM");
        Assert.Equal(HttpStatusCode.OK, cmC3.StatusCode);
        var cmC3Payload = await cmC3.Content.ReadFromJsonAsync<ToolOptionsResponse>();
        Assert.NotNull(cmC3Payload);
        Assert.Contains(cmC3Payload!.Items, i => i.Lot == "3" && i.Reference == "5447");
        Assert.DoesNotContain(cmC3Payload.Items, i => i.Lot == "1");

        // Same reference code "5447" as MF — a DIFFERENT tool with its own ids.
        var mfB2 = await client.GetAsync($"/api/jobon/{b2}/tool-options?family=MF");
        Assert.Equal(HttpStatusCode.OK, mfB2.StatusCode);
        var mfB2Payload = await mfB2.Content.ReadFromJsonAsync<ToolOptionsResponse>();
        Assert.NotNull(mfB2Payload);
        var mfItem = Assert.Single(mfB2Payload!.Items, i => i.Reference == "5447");
        Assert.Equal("2", mfItem.Lot);
        var cmItem = Assert.Single(cmB2Payload.Items, i => i.Lot == "1");
        Assert.NotEqual(cmItem.LoteId, mfItem.LoteId);
        Assert.NotEqual(cmItem.ReferenceId, mfItem.ReferenceId);
    }

    [Fact]
    public async Task ToolOptions_InvalidFamilyOrUnknownJobOn_RejectedServerSide()
    {
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var client = await LoginAsync();

        // Unknown Job On → NotFound (no data leak).
        var missing = await client.GetAsync(
            $"/api/jobon/{Guid.NewGuid()}/tool-options?family=CM");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // Known Job On, invalid family (only CM/MF/BQ are tool families) → BadRequest.
        var jobOnId = await CreateJobOnAsync(client, "202608", "B2", "5447T173");
        var invalid = await client.GetAsync($"/api/jobon/{jobOnId}/tool-options?family=PU");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var body = await invalid.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("JOBON_TOOL_FAMILY_INVALID", body!.Code);
    }

    // ---- "Confirmar verificação" — verification occurrences (modules/05 §7) --

    [Fact]
    public async Task UserWithoutJobOnModule_ConfirmEndpointIsForbidden()
    {
        // A user without the Job On module holds no jobon.confirmar: the
        // route-level capability policy denies the WRITE before any service
        // code runs — hiding the checkbox is never the only guard.
        _fixture.Repository.User = _fixture.JobOnlessUser();
        var client = await LoginAsync();

        // Delta-based: the fixture is shared across the class, so assert the
        // denied call itself performs ZERO confirm writes (never an absolute).
        var mutationsBefore = _fixture.JobOnRepository.ConfirmMutationCount;
        var denied = await client.PostAsync(
            $"/api/jobon/{SomeJobOnId}/verifications/{Guid.NewGuid()}/confirm",
            new StringContent("null"));
        // App denial contract: an authenticated user WITHOUT the Job On module
        // holds no jobon.confirmar — the route-level capability policy denies
        // the WRITE in the safe /access-denied deep-link state (never a 2xx,
        // never a data leak, never a loop).
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/access-denied", denied.Headers.Location!.PathAndQuery);
        Assert.Equal(mutationsBefore, _fixture.JobOnRepository.ConfirmMutationCount);
    }

    [Fact]
    public async Task Operator_ConfirmsPendingVerification_ReloadShowsPersistedState_AndRepeatIsIdempotent()
    {
        // The REAL flow through the real pipeline:
        //   1. the Responsible saves a revision carrying a PENDING verification
        //      occurrence (the same graph the UI submits);
        //   2. the OPERATOR (jobon.view + jobon.confirmar, NO jobon.edit) confirms
        //      it through POST /api/jobon/{id}/verifications/{occurrenceId}/confirm —
        //      the actor is resolved from the authenticated session, the client
        //      sends nothing;
        //   3. reopening the SAME folha renders the persisted confirmed state
        //      (who/when + updated pending counter);
        //   4. a repeated confirm (double-click / duplicate POST) is idempotent —
        //      no second write, no duplicated confirmation state;
        //   5. after a new revision supersedes it, the stale occurrence is
        //      rejected with a clean 404 (previous revisions are never rewritten).
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var responsible = await LoginAsync();
        var jobOnId = await CreateJobOnAsync(responsible, "202608", "B1", "5447T173");

        // Delta baseline: the fixture is shared across the class, so every
        // confirm-write assertion below is relative to this test's start.
        var mutationsBefore = _fixture.JobOnRepository.ConfirmMutationCount;

        // Same transport the UI submits: the component graph is the current
        // revision's graph (the embedded #jobon-revision-graph), so components
        // carry the current revision id — the server re-pins them to the NEW
        // revision on save (R-002).
        var revisionPage = await responsible.GetAsync($"/jobon?id={jobOnId}");
        Assert.Equal(HttpStatusCode.OK, revisionPage.StatusCode);
        var revisionPageHtml = await revisionPage.Content.ReadAsStringAsync();
        var revisionIdMatch = System.Text.RegularExpressions.Regex.Match(
            revisionPageHtml, "name=\"jobon-revision-id\" content=\"([0-9a-fA-F-]{36})\"");
        Assert.True(revisionIdMatch.Success, "The folha must embed the current revision id.");
        var currentRevisionId = Guid.Parse(revisionIdMatch.Groups[1].Value);

        var componentId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var saved = await responsible.PostAsJsonAsync($"/api/jobon/{jobOnId}/revision", new
        {
            jobOnId,
            generalNotes = (string?)null,
            changeReason = (string?)null,
            imageAssetId = (string?)null,
            components = new object[]
            {
                new
                {
                    jobOnComponentId = componentId,
                    jobOnRevisionId = currentRevisionId,
                    family = "MP_CM",
                    referenceSnapshot = "5447",
                    lotSnapshot = "1",
                    technicalNameSnapshot = (string?)null,
                    plannedQuantity = (decimal?)null,
                    stockSnapshot = (decimal?)null,
                    usageSnapshot = (decimal?)null,
                    notes = (string?)null,
                    displayOrder = 0,
                    fields = Array.Empty<object>(),
                    rows = Array.Empty<object>(),
                    verifications = new object[]
                    {
                        new
                        {
                            jobOnVerificationOccurrenceId = occurrenceId,
                            jobOnComponentId = componentId,
                            sourceRuleId = (Guid?)null,
                            ruleTextSnapshot = "Verificar junta da boquilha",
                            status = "pendente",
                            completionSource = "manual_job_on",
                            completedBy = (string?)null,
                            completedAtUtc = (DateTime?)null,
                            createdAtUtc = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc),
                            updatedAtUtc = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc)
                        }
                    }
                }
            }
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        // The pending occurrence renders as an actionable check row.
        _fixture.Repository.User = _fixture.JobOnOperator();
        var operatorClient = await LoginAsync();
        var before = await operatorClient.GetAsync($"/jobon?id={jobOnId}");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeHtml = await before.Content.ReadAsStringAsync();
        Assert.Contains($"data-occurrence-id=\"{occurrenceId}\"", beforeHtml);
        Assert.Contains("1 pendentes", beforeHtml);
        Assert.Contains("Por confirmar", beforeHtml);

        // The OPERATOR confirms through the real endpoint (jobon.confirmar).
        var confirmed = await operatorClient.PostAsync(
            $"/api/jobon/{jobOnId}/verifications/{occurrenceId}/confirm",
            new StringContent("null"));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var confirmedBody = await confirmed.Content.ReadFromJsonAsync<ConfirmResponse>();
        Assert.Equal(jobOnId, confirmedBody?.JobOnId);
        Assert.Equal(occurrenceId, confirmedBody?.OccurrenceId);
        Assert.Equal("confirmada", confirmedBody?.Status);
        Assert.Equal(mutationsBefore + 1, _fixture.JobOnRepository.ConfirmMutationCount);

        // Reloading the SAME folha renders the persisted confirmed state: the row
        // is confirmed, the who/when render from the persisted confirmation, and
        // the pending counter is updated.
        var after = await operatorClient.GetAsync($"/jobon?id={jobOnId}");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var afterHtml = await after.Content.ReadAsStringAsync();
        Assert.Contains("check-row confirmed", afterHtml);
        Assert.Contains("Confirmada", afterHtml);
        Assert.Contains(AuthUserId.ToString(), afterHtml); // who — the persisted actor
        Assert.Contains("0 pendentes", afterHtml);

        // Exactly one module audit fact for this confirmation (existing audit path).
        Assert.Single(_fixture.JobOnRepository.AuditEvents,
            a => a.EventType == "jobon.verificacao.confirmar" && a.JobId == jobOnId);

        // A repeated confirm (double-click / duplicate POST) is idempotent: no
        // second write, no duplicated confirmation state, no duplicated audit.
        var repeated = await operatorClient.PostAsync(
            $"/api/jobon/{jobOnId}/verifications/{occurrenceId}/confirm",
            new StringContent("null"));
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(mutationsBefore + 1, _fixture.JobOnRepository.ConfirmMutationCount);
        Assert.Single(_fixture.JobOnRepository.AuditEvents,
            a => a.EventType == "jobon.verificacao.confirmar" && a.JobId == jobOnId);

        // A new revision supersedes it: the stale occurrence is rejected with a
        // clean 404 and the previous revision is never rewritten. Save back as the
        // Responsible (jobon.edit) — the fixture user was switched to the operator
        // for the confirm phase, and identity resolves from the shared user slot.
        _fixture.Repository.User = _fixture.JobOnResponsible();
        var again = await responsible.PostAsJsonAsync($"/api/jobon/{jobOnId}/revision", new
        {
            jobOnId,
            generalNotes = "Nova revisão",
            changeReason = (string?)null,
            imageAssetId = (string?)null,
            components = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        var stale = await operatorClient.PostAsync(
            $"/api/jobon/{jobOnId}/verifications/{occurrenceId}/confirm",
            new StringContent("null"));
        Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
        var staleBody = await stale.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("JOBON_VERIFICATION_NOT_FOUND", staleBody?.Code);
        Assert.Equal(mutationsBefore + 1, _fixture.JobOnRepository.ConfirmMutationCount);
    }

    private sealed record ConfirmResponse(Guid JobOnId, Guid OccurrenceId, string Status);

    private async Task<Guid> CreateJobOnAsync(
        HttpClient client, string production, string machine, string reference)
    {
        var created = await client.PostAsJsonAsync("/api/jobon", new
        {
            productionCode = production,
            machineCode = machine,
            plannedStartAt = "2026-08-17",
            plannedEndAt = (string?)null,
            reference
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var response = await created.Content.ReadFromJsonAsync<CreateJobOnResponse>();
        return response!.JobOnId;
    }

    private sealed record CreateJobOnResponse(Guid JobOnId);

    private sealed record AlterDateResponse(Guid JobOnId, Guid RevisionId);

    private sealed record SaveRevisionResponse(Guid JobOnId, Guid RevisionId);

    private sealed record ErrorBody(string Code, string Message);

    private sealed record ToolOptionsResponse(
        Guid JobOnId, string Machine, string Family, IReadOnlyList<ToolOption> Items);

    private sealed record ToolOption(
        Guid ReferenceId, Guid LoteId, string Type, string Reference, string Lot,
        IReadOnlyList<string> AllowedLines);

    private async Task<HttpClient> LoginAsync()
    {
        var client = _fixture.CreateTestClient();
        // Login round-trip: anti-forgery is disabled in this test host; the
        // fake adapter signs in the fixed auth user id for any credentials.
        var login = await client.PostAsync("/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["email"] = "jobon@ba-dmo.example",
                ["password"] = "correct"
            }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    /// <summary>
    /// Test host with fakes for the provider adapter, the identity repository
    /// (switchable per test) and the Job On repository. Matches the
    /// JobOnLandingTests fixture pattern; legacy capability arrays in
    /// ModulesJson are intentionally left empty so the AccessResolver
    /// derivation is what grants capabilities.
    /// </summary>
    public sealed class AuthFixture : WebApplicationFactory<Program>
    {
        public FakeIdentityRepository Repository { get; } = new();

        public MemoryJobOnRepository JobOnRepository { get; } = new();

        public InternalUserRecord JobOnOperator() => new(
            ActorId: "jobon-operator",
            AuthUserId: AuthUserId,
            DisplayName: "Operador Job On",
            ProfileTitle: FunctionalProfileNames.OperatorController,
            UserActive: true,
            TemplateId: "tpl-jobon-op",
            TemplateName: "Job On",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"jobon\",\"capabilities\":[]}]",
            FunctionalProfile: FunctionalProfileNames.OperatorController);

        public InternalUserRecord JobOnResponsible() => new(
            ActorId: "jobon-responsavel",
            AuthUserId: AuthUserId,
            DisplayName: "Responsável Job On",
            ProfileTitle: FunctionalProfileNames.Responsible,
            UserActive: true,
            TemplateId: "tpl-jobon-resp",
            TemplateName: "Job On",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"jobon\",\"capabilities\":[]}]",
            FunctionalProfile: FunctionalProfileNames.Responsible);

        /// <summary>Functional user with NO Job On module (Boquilhas only).</summary>
        public InternalUserRecord JobOnlessUser() => new(
            ActorId: "jobonless-actor",
            AuthUserId: AuthUserId,
            DisplayName: "Utilizador Boquilhas",
            ProfileTitle: FunctionalProfileNames.OperatorController,
            UserActive: true,
            TemplateId: "tpl-bq",
            TemplateName: "Boquilhas",
            TemplateActive: true,
            ModulesJson: "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]",
            FunctionalProfile: FunctionalProfileNames.OperatorController);

        public HttpClient CreateTestClient() => CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                ReplaceSingleton<ISupabaseAuthAdapter>(services, new FakeAuthAdapter());
                ReplaceSingleton<IInternalUserRepository>(services, Repository);
                ReplaceSingleton<IJobOnRepository>(services, JobOnRepository);
                ReplaceSingleton<IJobOnUserContextRepository>(services, new FakeJobOnUserContextRepository());
                ReplaceSingleton<IFerramentasIdentityLookup>(services, new FakeToolRegister());
                services.Configure<Microsoft.AspNetCore.Mvc.RazorPages.RazorPagesOptions>(
                    options => options.Conventions.ConfigureFilter(
                        new IgnoreAntiforgeryTokenAttribute()));
            });
        }

        private static void ReplaceSingleton<TService>(
            IServiceCollection services, TService implementation)
            where TService : class
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(TService)).ToList())
                services.Remove(descriptor);
            services.AddSingleton(implementation);
        }

        private sealed class FakeAuthAdapter : ISupabaseAuthAdapter
        {
            public Task<Result<AuthUser, DomainError>> SignInWithPasswordAsync(
                string email, string password, CancellationToken cancellationToken = default) =>
                Task.FromResult(Result<AuthUser, DomainError>.Success(new AuthUser(AuthUserId, email)));
        }

        /// <summary>
        /// In-memory Job On repository (R011 create-flow tests): atomically created
        /// Job Ons (header + initial revision) become readable through GetByIdAsync,
        /// so a successful create can open the newly created folha/redirect target.
        /// All other port members stay inert.
        /// </summary>
        public sealed class MemoryJobOnRepository : IJobOnRepository
        {
            private readonly Dictionary<Guid, Domain.Modules.JobOn.JobOn> _jobOns = new();
            private readonly List<JobOnRevision> _revisions = [];

            /// <summary>Module audit facts (jobon.criar / jobon.guardar / jobon.verificacao.confirmar …) for assertions.</summary>
            public List<(Guid JobId, Guid? RevisionId, string EventType, string? Before, string? After, string ActorId)> AuditEvents { get; } = [];

            /// <summary>Number of in-place confirmation writes that actually applied (idempotent repeats do not count).</summary>
            public int ConfirmMutationCount { get; private set; }

            public Task<Guid> CreateAsync(Domain.Modules.JobOn.JobOn jobOn, CancellationToken cancellationToken = default)
            {
                var id = Guid.NewGuid();
                SetId(jobOn, id);
                _jobOns[id] = jobOn;
                return Task.FromResult(id);
            }

            public Task<Guid> CreateAtomicallyAsync(
                Domain.Modules.JobOn.JobOn jobOn,
                JobOnRevision initialRevision,
                string actorId,
                CancellationToken cancellationToken = default)
            {
                var id = Guid.NewGuid();
                SetId(jobOn, id);
                _jobOns[id] = jobOn;
                var pinned = initialRevision with { JobOnId = id };
                _revisions.Add(pinned);
                _jobOns[id].SaveRevision(pinned);
                return Task.FromResult(id);
            }

            public Task<Domain.Modules.JobOn.JobOn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            {
                if (!_jobOns.TryGetValue(id, out var stored))
                    return Task.FromResult<Domain.Modules.JobOn.JobOn?>(null);
                // Mirror the real repository's aggregate hydration: each revision
                // exposes the flattened verification occurrences of its components
                // (the same collection the folha renders).
                var revisions = _revisions
                    .Where(r => r.JobOnId == id)
                    .OrderBy(r => r.RevisionNumber)
                    .Select(r => r.Components is null
                        ? r
                        : r with
                        {
                            Verifications = r.Components
                                .SelectMany(c => c.Verifications ?? Array.Empty<JobOnVerificationOccurrence>())
                                .ToList()
                        })
                    .ToList();
                var jobOn = new Domain.Modules.JobOn.JobOn(
                    stored.ProductionCode,
                    stored.MachineCode,
                    stored.PlannedStartAt,
                    stored.PlannedEndAt,
                    revisions);
                SetId(jobOn, id);
                foreach (var revision in revisions)
                    jobOn.SaveRevision(revision);
                return Task.FromResult<Domain.Modules.JobOn.JobOn?>(jobOn);
            }

            private static void SetId(Domain.Modules.JobOn.JobOn jobOn, Guid id)
            {
                typeof(Domain.Modules.JobOn.JobOn)
                    .GetMethod("SetId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(jobOn, new object[] { id });
            }

            public Task<IReadOnlyList<Domain.Modules.JobOn.JobOn>> GetActiveAsync(
                string machineCode, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<Domain.Modules.JobOn.JobOn>>(Array.Empty<Domain.Modules.JobOn.JobOn>());

            public Task<Domain.Modules.JobOn.JobOn?> GetByProductionCodeAsync(
                string productionCode, CancellationToken cancellationToken = default) =>
                Task.FromResult<Domain.Modules.JobOn.JobOn?>(null);

            public Task TransitionLifecycleAsync(
                Domain.Modules.JobOn.JobOn jobOn, string actorId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task InsertRevisionAsync(JobOnRevision revision, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task<IReadOnlyList<JobOnRevision>> GetRevisionsAsync(
                Guid jobOnId, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<JobOnRevision>>(
                    _revisions.Where(r => r.JobOnId == jobOnId).ToList());

            public Task InsertComponentsAsync(
                IEnumerable<JobOnComponent> components, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task InsertFieldsAsync(
                IEnumerable<JobOnComponentField> fields, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task InsertRowsAsync(
                IEnumerable<JobOnComponentRow> rows, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task InsertVerificationsAsync(
                IEnumerable<JobOnVerificationOccurrence> verifications, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task UpdateVerificationStatusAsync(
                Guid occurrenceId, string status, string? completedBy, DateTime? completedAtUtc, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            /// <summary>
            /// Mirrors the real repository's optimistic guard: the in-place
            /// confirmation only applies while the occurrence is still
            /// 'pendente'; the stored revision graph is updated so the SAME
            /// folha reopens rendering the persisted confirmed state.
            /// </summary>
            public Task<int> ConfirmVerificationOccurrenceAsync(
                Guid occurrenceId, string completedBy, DateTime completedAtUtc, CancellationToken cancellationToken = default)
            {
                for (var i = 0; i < _revisions.Count; i++)
                {
                    var revision = _revisions[i];
                    var component = (revision.Components ?? Array.Empty<JobOnComponent>())
                        .FirstOrDefault(c => (c.Verifications ?? Array.Empty<JobOnVerificationOccurrence>())
                            .Any(v => v.JobOnVerificationOccurrenceId == occurrenceId));
                    if (component?.Verifications is null)
                        continue;

                    var occurrence = component.Verifications
                        .Single(v => v.JobOnVerificationOccurrenceId == occurrenceId);
                    if (occurrence.Status != "pendente")
                        return Task.FromResult(0);

                    var updatedVerifications = component.Verifications
                        .Select(v => v.JobOnVerificationOccurrenceId == occurrenceId
                            ? v with
                            {
                                Status = "confirmada",
                                CompletedBy = completedBy,
                                CompletedAtUtc = completedAtUtc,
                                UpdatedAtUtc = completedAtUtc
                            }
                            : v)
                        .ToList();

                    var updatedComponents = (revision.Components ?? Array.Empty<JobOnComponent>())
                        .Select(c => c.JobOnComponentId == component.JobOnComponentId
                            ? c with { Verifications = updatedVerifications }
                            : c)
                        .ToList();

                    _revisions[i] = revision with { Components = updatedComponents };
                    ConfirmMutationCount++;
                    return Task.FromResult(1);
                }

                return Task.FromResult(0);
            }

            public Task<Guid?> GetCurrentRevisionIdAsync(Guid jobOnId, CancellationToken cancellationToken = default) =>
                Task.FromResult<Guid?>(_jobOns.TryGetValue(jobOnId, out var jobOn) ? jobOn.CurrentRevisionId : null);

            public Task UpdateCurrentRevisionAsync(Guid jobOnId, Guid revisionId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task InsertAuditEventAsync(
                Guid jobId, Guid? revisionId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken cancellationToken = default)
            {
                AuditEvents.Add((jobId, revisionId, eventType, beforeSnapshot, afterSnapshot, actorId));
                return Task.CompletedTask;
            }

            public Task InsertImageMutationAsync(
                JobOnRevision newRevision, Guid jobOnId, string eventType, string? beforeImageAssetId, string? afterImageAssetId, string actorId, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task SaveRevisionGraphAsync(
                JobOnRevision revision, string eventType, string actorId,
                string? beforeSnapshot = null, string? afterSnapshot = null,
                CancellationToken cancellationToken = default)
            {
                // Mirror the real repository: the new revision + the current-revision
                // link advance — readable through GetByIdAsync so the SAME folha reopens
                // rendering the new current revision after a successful save.
                if (_jobOns.TryGetValue(revision.JobOnId, out var stored))
                {
                    _revisions.Add(revision);
                    stored.SaveRevision(revision);
                }
                return Task.CompletedTask;
            }

            public Task AlterDatesAtomicallyAsync(
                Guid jobOnId,
                DateTimeOffset? plannedStartAt,
                DateTimeOffset? plannedEndAt,
                JobOnRevision newRevision,
                string eventType,
                string? beforeSnapshot,
                string? afterSnapshot,
                string actorId,
                CancellationToken cancellationToken = default)
            {
                // Mirror the real repository: header planned dates (single calendar
                // source) update + new revision + current-revision advance — readable
                // through GetByIdAsync so the same folha opens with the new dates.
                if (_jobOns.TryGetValue(jobOnId, out var stored))
                {
                    stored.AlterDates(plannedStartAt, plannedEndAt);
                    _revisions.Add(newRevision);
                    stored.SaveRevision(newRevision);
                }
                return Task.CompletedTask;
            }

            public Task<Guid> DuplicateAtomicallyAsync(
                Domain.Modules.JobOn.JobOn newJobOn, JobOnRevision revision, Guid sourceJobOnId, string actorId, CancellationToken cancellationToken = default)
            {
                // Mirror the real repository: a NEW job_on row (fresh DB id) with the
                // copied revision pinned to it and the current-revision link advanced —
                // readable through GetByIdAsync so the duplicated folha opens after a
                // successful duplicate. The header context comes from the service-built
                // duplicate (constructor-visible: production/machine/dates).
                var newId = Guid.NewGuid();
                var header = new Domain.Modules.JobOn.JobOn(
                    newJobOn.ProductionCode,
                    newJobOn.MachineCode,
                    newJobOn.PlannedStartAt,
                    newJobOn.PlannedEndAt,
                    Array.Empty<JobOnRevision>());
                SetId(header, newId);
                var pinned = revision with { JobOnId = newId };
                _revisions.Add(pinned);
                header.SaveRevision(pinned);
                _jobOns[newId] = header;
                return Task.FromResult(newId);
            }

            public Task<IReadOnlyList<HistoricalProductionSummary>> GetHistoricalProductionsAsync(
                string? referenceFilter, string? machineFilter, DateTime? from, DateTime? to, CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<HistoricalProductionSummary>>(
                    Array.Empty<HistoricalProductionSummary>());
        }

        /// <summary>
        /// In-memory N04 tool register for the tool-options endpoint proof
        /// (avoids a live DB): CM 5447 Lote 1 (B2) + Lote 3 (C3), MF 5447
        /// Lote 2 (B2) and BQ 5447 Lote 9 (C3) — the same reference code
        /// registered as three DISTINCT tools (different reference/lot ids).
        /// Read-only: the flow can never create Ferramentas records.
        /// </summary>
        private sealed class FakeToolRegister : IFerramentasIdentityLookup
        {
            public static readonly Guid CmRef = Guid.Parse("60000000-0000-4000-8000-000000000001");
            public static readonly Guid CmLote1 = Guid.Parse("60000000-0000-4000-8000-000000000011");
            public static readonly Guid CmLote3 = Guid.Parse("60000000-0000-4000-8000-000000000013");
            public static readonly Guid MfRef = Guid.Parse("60000000-0000-4000-8000-000000000002");
            public static readonly Guid MfLote2 = Guid.Parse("60000000-0000-4000-8000-000000000012");
            public static readonly Guid BqRef = Guid.Parse("60000000-0000-4000-8000-000000000003");
            public static readonly Guid BqLote9 = Guid.Parse("60000000-0000-4000-8000-000000000019");

            private static readonly FerramentasToolLoteOption[] Lots =
            {
                new(CmRef, CmLote1, FerramentasToolType.CM, "5447", "1", "Contra molde 5447", new[] { "B2" }),
                new(CmRef, CmLote3, FerramentasToolType.CM, "5447", "3", "Contra molde 5447", new[] { "C3" }),
                new(MfRef, MfLote2, FerramentasToolType.MF, "5447", "2", "Molde final 5447", new[] { "B2" }),
                new(BqRef, BqLote9, FerramentasToolType.BQ, "5447", "9", "Boquilha 5447", new[] { "C3" })
            };

            public Task<IReadOnlyList<FerramentasIdentityHit>> SearchAsync(
                FerramentasToolType type, string? reference, string? lot, CancellationToken ct = default) =>
                Task.FromResult<IReadOnlyList<FerramentasIdentityHit>>(
                    Lots.Where(l => l.Type == type)
                        .Select(l => new FerramentasIdentityHit(
                            l.ToolReferenceId, l.ToolLoteId, l.Type, l.Reference, l.Lot, l.TechnicalName))
                        .ToList());

            public Task<FerramentasIdentityHit?> ResolveAsync(Guid toolLoteId, CancellationToken ct = default)
            {
                var lot = Lots.FirstOrDefault(l => l.ToolLoteId == toolLoteId);
                return Task.FromResult(lot is null
                    ? null
                    : new FerramentasIdentityHit(
                        lot.ToolReferenceId, lot.ToolLoteId, lot.Type, lot.Reference, lot.Lot, lot.TechnicalName));
            }

            public Task<IReadOnlyList<FerramentasToolLoteOption>> SearchToolLoteOptionsAsync(
                FerramentasToolType type, string? reference, string? lot, string? line,
                CancellationToken ct = default)
            {
                var result = Lots
                    .Where(l => l.Type == type
                        && (string.IsNullOrWhiteSpace(reference) || l.Reference.Contains(reference))
                        && (string.IsNullOrWhiteSpace(lot) || l.Lot.Contains(lot))
                        && (line is null || l.AllowedLines.Contains(line)))
                    .ToList();
                return Task.FromResult<IReadOnlyList<FerramentasToolLoteOption>>(result);
            }

            public Task<FerramentasToolLoteOption?> ResolveToolLoteOptionAsync(Guid toolLoteId, CancellationToken ct = default) =>
                Task.FromResult(Lots.FirstOrDefault(l => l.ToolLoteId == toolLoteId));
        }

        /// <summary>R011 — in-memory per-user current-open Job On context (avoids a live DB).</summary>
        private sealed class FakeJobOnUserContextRepository : IJobOnUserContextRepository
        {
            public Task SetCurrentAsync(
                string actorId,
                Guid jobOnId,
                string productionCode,
                string reference,
                string machineCode,
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task<JobOnUserCurrent?> GetCurrentAsync(
                string actorId, CancellationToken cancellationToken = default) =>
                Task.FromResult<JobOnUserCurrent?>(null);
        }
    }

    public sealed class FakeIdentityRepository : IInternalUserRepository
    {
        public InternalUserRecord? User { get; set; }

        public Task<InternalUserRecord?> FindByAuthUserIdAsync(
            Guid authUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(User);

        public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task CreateBootstrapAdminAsync(
            BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}