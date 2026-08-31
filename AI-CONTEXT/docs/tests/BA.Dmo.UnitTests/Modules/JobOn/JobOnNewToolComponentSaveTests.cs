using System.Text.Json;
using System.Text.Json.Serialization;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// Brand-new tool-component save tests — the "Alterar CM/MF/BQ associado" edit
/// flow where a family with NO stored component gains its association through an
/// explicit picker selection, and "Guardar nova revisão" saves it.
///
/// The client builds the new-revision graph from the embedded CURRENT revision:
/// existing components round-trip under FRESH ids still pinned to the CURRENT
/// (previous) revision id, and a brand-new component is created client-side
/// under a fresh id pinned to the SAME current revision id — the submitted graph
/// never carries a null/nonexistent revision id, so the request always binds to
/// the non-nullable <c>Guid</c> contract. The server stays AUTHORITATIVE: the
/// service creates the NEW revision id and the repository re-pins the whole
/// graph (including the brand-new component) to it at persistence (R-002). The
/// previous revision is never modified and no Ferramentas record is created
/// (the register is read-only).
/// </summary>
public class JobOnNewToolComponentSaveTests
{
    private const string Line = "LINHA-1";
    private const string OtherLine = "LINHA-9";

    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    // Registered N04 lots (one per distinct type) for this Job On's line.
    private static readonly Guid CmReferenceId =
        Guid.Parse("11111111-0000-4000-8000-000000000001");
    private static readonly Guid CmLoteId =
        Guid.Parse("22222222-0000-4000-8000-000000000001");
    private static readonly Guid MfReferenceId =
        Guid.Parse("11111111-0000-4000-8000-000000000002");
    private static readonly Guid MfLoteId =
        Guid.Parse("22222222-0000-4000-8000-000000000002");
    private static readonly Guid BqReferenceId =
        Guid.Parse("11111111-0000-4000-8000-000000000003");
    private static readonly Guid BqLoteId =
        Guid.Parse("22222222-0000-4000-8000-000000000003");
    private static readonly Guid WrongLineReferenceId =
        Guid.Parse("11111111-0000-4000-8000-000000000009");
    private static readonly Guid WrongLineLoteId =
        Guid.Parse("22222222-0000-4000-8000-000000000009");

    private readonly FakeJobOnRepository _repository = new();
    private readonly FakeFerramentasToolLookup _tools = new();
    private readonly NewToolSaveIdentity _identity = new();
    private readonly JobOnService _service;

    public JobOnNewToolComponentSaveTests()
    {
        _tools.Register(CmReferenceId, CmLoteId, FerramentasToolType.CM,
            "CM-5447", "Lote-3", "Contra-molde 5447", Line);
        _tools.Register(MfReferenceId, MfLoteId, FerramentasToolType.MF,
            "MF-8812", "Lote-7", "Molde final 8812", Line);
        _tools.Register(BqReferenceId, BqLoteId, FerramentasToolType.BQ,
            "BQ-2205", "Lote-1", "Boquilha 2205", Line);
        // Same (referência, lote) registered for ANOTHER line — a different tool
        // identity: the wrong line for this Job On.
        _tools.Register(WrongLineReferenceId, WrongLineLoteId,
            FerramentasToolType.CM, "CM-5447", "Lote-3",
            "Contra-molde 5447", OtherLine);

        var gate = new JobOnAuthorizationGate(_identity);
        _service = new JobOnService(
            gate, _repository, new FakeJobOnUserContextRepository(),
            new FixedClock(new DateTimeOffset(2026, 8, 18, 9, 0, 0, TimeSpan.Zero)),
            _tools,
            articleImages: null);
        _identity.GrantResponsible();
    }

    // ---- brand-new CM / MF / BQ component through the real save use case ----

    [Fact]
    public async Task Save_NewCmComponent_AddedInEditMode_SavesNewRevision_RePinnedToNewRevision()
        => await SaveBrandNewComponentAsync(
            ComponentFamily.MP_CM, CmReferenceId, CmLoteId,
            expectedReference: "CM-5447", expectedLot: "Lote-3",
            expectedTechnicalName: "Contra-molde 5447");

    [Fact]
    public async Task Save_NewMfComponent_AddedInEditMode_SavesNewRevision_RePinnedToNewRevision()
        => await SaveBrandNewComponentAsync(
            ComponentFamily.MF, MfReferenceId, MfLoteId,
            expectedReference: "MF-8812", expectedLot: "Lote-7",
            expectedTechnicalName: "Molde final 8812");

    [Fact]
    public async Task Save_NewBqComponent_AddedInEditMode_SavesNewRevision_RePinnedToNewRevision()
        => await SaveBrandNewComponentAsync(
            ComponentFamily.BQ, BqReferenceId, BqLoteId,
            expectedReference: "BQ-2205", expectedLot: "Lote-1",
            expectedTechnicalName: "Boquilha 2205");

    /// <summary>
    /// Full brand-new-component save assertions (tests #1–#7): add a brand-new
    /// component of the given family in edit mode (fresh client id, pinned to
    /// the CURRENT revision id — exactly the shape the fixed client submits) and
    /// save. The request binds, the SAME <c>job_on_id</c> is preserved (never a
    /// new Job On), a NEW revision id is created with an incremented number, the
    /// new component persists pinned to the NEW revision id (repository re-pin,
    /// R-002) with its register-backed identity, and the previous revision stays
    /// unchanged.
    /// </summary>
    private async Task SaveBrandNewComponentAsync(
        ComponentFamily family,
        Guid referenceId,
        Guid loteId,
        string expectedReference,
        string expectedLot,
        string expectedTechnicalName)
    {
        var jobOnId = await CreateRascunhoAsync();
        var oldRevisionId = _repository.JobOns[jobOnId].CurrentRevisionId!.Value;

        var componentId = Guid.NewGuid();
        var newComponent = NewToolComponent(
            componentId, oldRevisionId, family, referenceId, loteId,
            expectedReference, expectedLot, expectedTechnicalName);

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, new[] { newComponent }));

        // The save succeeds (no JSON-binding failure before the service ran).
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        var newRevisionId = result.Value;
        Assert.NotEqual(oldRevisionId, newRevisionId);

        // SAME JobOnId — save never creates a new Job On.
        Assert.Single(_repository.JobOns);
        Assert.Equal(jobOnId, _repository.JobOns.Keys.Single());

        // A NEW revision was created: incremented number, current advanced.
        var revision = Assert.Single(
            _repository.Revisions, r => r.JobOnRevisionId == newRevisionId);
        Assert.Equal(2, revision.RevisionNumber);
        Assert.Equal(jobOnId, revision.JobOnId);
        Assert.Equal(newRevisionId, _repository.JobOns[jobOnId].CurrentRevisionId);

        // R-002: the NEW component persists pinned to the NEW revision id —
        // never to the previous one the client submitted.
        var stored = Assert.Single(
            _repository.Components, c => c.JobOnComponentId == componentId);
        Assert.Equal(newRevisionId, stored.JobOnRevisionId);
        Assert.NotEqual(oldRevisionId, stored.JobOnRevisionId);

        // The register-backed identity tuple is preserved through the save.
        Assert.Equal(family, stored.Family);
        Assert.Equal(referenceId, stored.SourceToolId);
        Assert.Equal(loteId, stored.SourceLotId);
        Assert.Equal(expectedReference, stored.ReferenceSnapshot);
        Assert.Equal(expectedLot, stored.LotSnapshot);
        Assert.Equal(expectedTechnicalName, stored.TechnicalNameSnapshot);

        // Reload round-trip: the new current revision renders the component.
        var reloaded = (await _repository.GetByIdAsync(jobOnId))!;
        var reloadedComponent = reloaded.CurrentRevision!.Components!.Single();
        Assert.Equal(componentId, reloadedComponent.JobOnComponentId);
        Assert.Equal(expectedReference, reloadedComponent.ReferenceSnapshot);
        Assert.Equal(expectedLot, reloadedComponent.LotSnapshot);

        // The previous revision (the rascunho creation revision) is unchanged:
        // still revision 1, still no components, still readable.
        var previous = reloaded.Revisions.Single(r => r.JobOnRevisionId == oldRevisionId);
        Assert.Equal(1, previous.RevisionNumber);
        Assert.DoesNotContain(_repository.Components,
            c => c.JobOnRevisionId == oldRevisionId);
    }

    [Fact]
    public async Task Save_NewComponent_AndExistingComponent_BothPersist_UnderNewRevision()
    {
        // Test #9 — a family with a stored component round-trips under fresh ids
        // (values carried) AND a brand-new family is added in the same save;
        // BOTH persist under the new revision id.
        var jobOnId = await CreateRascunhoAsync();
        var seeded = await SeedCurrentRevisionWithCmAsync(jobOnId);
        var currentRevisionId = seeded.JobOnRevisionId;

        // Existing CM: new client id, SAME current-revision pin, values carried.
        var seededComponent = seeded.Components!.Single();
        var existingCopyId = Guid.NewGuid();
        var existingCopy = seededComponent with
        {
            JobOnComponentId = existingCopyId,
            JobOnRevisionId = currentRevisionId,
            Fields = (seededComponent.Fields ?? Array.Empty<JobOnComponentField>())
                .Select(f => f with
                {
                    JobOnComponentFieldId = Guid.NewGuid(),
                    JobOnComponentId = existingCopyId
                }).ToList()
        };

        // Brand-new MF: fresh id, same current-revision pin (client-side creation).
        var newMfId = Guid.NewGuid();
        var newMf = NewToolComponent(
            newMfId, currentRevisionId, ComponentFamily.MF,
            MfReferenceId, MfLoteId, "MF-8812", "Lote-7", "Molde final 8812");

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, new[] { existingCopy, newMf }));

        Assert.True(result.IsSuccess);
        var newRevisionId = result.Value;
        Assert.NotEqual(currentRevisionId, newRevisionId);

        // BOTH components persist, re-pinned to the NEW revision id.
        var stored = _repository.Components
            .Where(c => c.JobOnRevisionId == newRevisionId)
            .ToList();
        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, c => c.JobOnComponentId == existingCopyId
            && c.Family == ComponentFamily.MP_CM
            && c.ReferenceSnapshot == "CM 5447"
            && c.LotSnapshot == "Lote 3");
        Assert.Contains(stored, c => c.JobOnComponentId == newMfId
            && c.Family == ComponentFamily.MF
            && c.ReferenceSnapshot == "MF-8812"
            && c.SourceToolId == MfReferenceId
            && c.SourceLotId == MfLoteId);
        Assert.Contains(_repository.Fields, f => f.JobOnComponentId == existingCopyId);
    }

    [Fact]
    public async Task Save_NewComponent_PreviousRevision_Unchanged()
    {
        // Test #8 — saving a brand-new component never modifies the previous
        // revision: its component stays pinned to the OLD revision id with the
        // original values, and the new revision is the only new write.
        var jobOnId = await CreateRascunhoAsync();
        var seeded = await SeedCurrentRevisionWithCmAsync(jobOnId);
        var oldComponent = seeded.Components!.Single();

        var newBqId = Guid.NewGuid();
        var newBq = NewToolComponent(
            newBqId, seeded.JobOnRevisionId, ComponentFamily.BQ,
            BqReferenceId, BqLoteId, "BQ-2205", "Lote-1", "Boquilha 2205");

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, new[] { newBq }));

        Assert.True(result.IsSuccess);

        var reloaded = (await _repository.GetByIdAsync(jobOnId))!;
        var previous = reloaded.Revisions.Single(r => r.JobOnRevisionId == seeded.JobOnRevisionId);
        var previousComponent = Assert.Single(previous.Components ?? Array.Empty<JobOnComponent>());
        Assert.Equal(oldComponent.JobOnComponentId, previousComponent.JobOnComponentId);
        Assert.Equal("CM 5447", previousComponent.ReferenceSnapshot);
        Assert.Equal("Lote 3", previousComponent.LotSnapshot);
        Assert.Equal(
            seeded.JobOnRevisionId,
            _repository.Components.Single(c => c.JobOnComponentId == oldComponent.JobOnComponentId)
                .JobOnRevisionId);
    }

    [Fact]
    public async Task Save_NewComponent_InvalidToolAssociation_Rejected_ByExistingServerValidation_ZeroWrites()
    {
        // Test #10 — the EXISTING server validation still rejects invalid
        // associations before any write: a nonexistent registered lot and a lot
        // registered for another line both fail, leaving zero writes.
        var jobOnId = await CreateRascunhoAsync();
        var currentRevisionId = _repository.JobOns[jobOnId].CurrentRevisionId!.Value;
        var revisionCountBefore = _repository.Revisions.Count;
        var componentsBefore = _repository.Components.Count;

        // (a) Nonexistent lote id (never in the register).
        var ghost = NewToolComponent(
            Guid.NewGuid(), currentRevisionId, ComponentFamily.MP_CM,
            Guid.NewGuid(), Guid.NewGuid(), "CX-0000", "Lote-X", null);
        var ghostResult = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, new[] { ghost }));
        Assert.True(ghostResult.IsFailure);
        Assert.Equal("JOBON_TOOL_NOT_FOUND", ghostResult.Error.Code);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
        Assert.Equal(componentsBefore, _repository.Components.Count);

        // (b) Real lot registered for ANOTHER line (wrong identity tuple).
        var wrongLine = NewToolComponent(
            Guid.NewGuid(), currentRevisionId, ComponentFamily.MP_CM,
            WrongLineReferenceId, WrongLineLoteId, "CM-5447", "Lote-3", null);
        var wrongLineResult = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, new[] { wrongLine }));
        Assert.True(wrongLineResult.IsFailure);
        Assert.Equal("JOBON_TOOL_LINE_NOT_ALLOWED", wrongLineResult.Error.Code);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
        Assert.Equal(componentsBefore, _repository.Components.Count);
        Assert.Equal(currentRevisionId, _repository.JobOns[jobOnId].CurrentRevisionId);

        // The register stays untouched: no Ferramentas record is created.
        Assert.Equal(4, _tools.Lots.Count);
    }

    // ---- transport contract: the JSON request must bind ----

    [Fact]
    public void RequestDto_BrandNewComponentPinnedToCurrentRevision_Binds()
    {
        // Test #4 — the EXACT wire shape the fixed client submits (camelCase,
        // enum strings, nulls preserved; a brand-new component pinned to the
        // CURRENT revision id) binds to the request DTO through the app's HTTP
        // JSON options (Web defaults + JsonStringEnumConverter, per
        // ConfigureHttpJsonOptions in Program.cs).
        const string json = """
        {
          "jobOnId": "00000000-0000-0000-0000-000000000000",
          "generalNotes": "Notas",
          "changeReason": null,
          "imageAssetId": null,
          "components": [
            {
              "jobOnComponentId": "00000000-0000-0000-0000-000000000001",
              "jobOnRevisionId": "00000000-0000-0000-0000-000000000002",
              "family": "MP_CM",
              "sourceToolId": "11111111-0000-4000-8000-000000000001",
              "sourceLotId": "22222222-0000-4000-8000-000000000001",
              "referenceSnapshot": "CM-5447",
              "lotSnapshot": "Lote-3",
              "technicalNameSnapshot": "Contra-molde 5447",
              "plannedQuantity": null,
              "stockSnapshot": null,
              "usageSnapshot": null,
              "notes": null,
              "displayOrder": 0,
              "fields": [],
              "rows": [],
              "verifications": []
            }
          ]
        }
        """;

        var request = JsonSerializer.Deserialize<SaveJobOnRevisionRequest>(
            json, HttpJsonOptions);
        Assert.NotNull(request);
        var component = Assert.Single(request!.Components);
        Assert.Equal(ComponentFamily.MP_CM, component.Family);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"),
            component.JobOnRevisionId);
        Assert.Equal(CmReferenceId, component.SourceToolId);
        Assert.Equal(CmLoteId, component.SourceLotId);
        Assert.Empty(component.Fields!);
    }

    [Fact]
    public void RequestDto_NullRevisionId_FailsJsonBinding_TheOriginalBug()
    {
        // Root-cause pin: the PRE-fix client payload (`jobOnRevisionId: null`
        // for a brand-new component) cannot bind to the non-nullable Guid
        // contract — the request failed JSON model binding before the service
        // (and its repository re-pin) ever ran. The contract is preserved: null
        // is rejected; the client now always sends a valid revision id.
        const string json = """
        {
          "jobOnId": "00000000-0000-0000-0000-000000000000",
          "generalNotes": null,
          "changeReason": null,
          "imageAssetId": null,
          "components": [
            {
              "jobOnComponentId": "00000000-0000-0000-0000-000000000001",
              "jobOnRevisionId": null,
              "family": "BQ",
              "sourceToolId": "11111111-0000-4000-8000-000000000003",
              "sourceLotId": "22222222-0000-4000-8000-000000000003",
              "referenceSnapshot": "BQ-2205",
              "lotSnapshot": "Lote-1",
              "technicalNameSnapshot": "Boquilha 2205",
              "displayOrder": 0,
              "fields": [],
              "rows": [],
              "verifications": []
            }
          ]
        }
        """;

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<SaveJobOnRevisionRequest>(json, HttpJsonOptions));
    }

    // ---- helpers ------------------------------------------------------------

    /// <summary>The app's HTTP JSON options (Program.cs ConfigureHttpJsonOptions).</summary>
    private static JsonSerializerOptions HttpJsonOptions { get; } =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

    private async Task<Guid> CreateRascunhoAsync()
    {
        var result = await _service.CreateAsync(new CreateJobOnRequest(
            "202608", Line, Start, null, "9262T288"));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    /// <summary>
    /// A brand-new client-side tool component in EXACTLY the shape the fixed
    /// client submits: fresh client id, pinned to the CURRENT (previous)
    /// revision id, register-backed identity tuple, empty fields/rows/
    /// verifications.
    /// </summary>
    private static JobOnComponent NewToolComponent(
        Guid componentId,
        Guid currentRevisionId,
        ComponentFamily family,
        Guid referenceId,
        Guid loteId,
        string reference,
        string lot,
        string? technicalName) => new()
    {
        JobOnComponentId = componentId,
        JobOnRevisionId = currentRevisionId,
        Family = family,
        SourceToolId = referenceId,
        SourceLotId = loteId,
        ReferenceSnapshot = reference,
        LotSnapshot = lot,
        TechnicalNameSnapshot = technicalName,
        PlannedQuantity = null,
        StockSnapshot = null,
        UsageSnapshot = null,
        Notes = null,
        DisplayOrder = 0,
        Fields = Array.Empty<JobOnComponentField>(),
        Rows = Array.Empty<JobOnComponentRow>(),
        Verifications = Array.Empty<JobOnVerificationOccurrence>()
    };

    /// <summary>
    /// Seeds the current revision (revision 2) with a real stored CM component
    /// (field "peso") — the state the edit flow starts from.
    /// </summary>
    private async Task<JobOnRevision> SeedCurrentRevisionWithCmAsync(Guid jobOnId)
    {
        var componentId = Guid.NewGuid();
        var component = new JobOnComponent
        {
            JobOnComponentId = componentId,
            JobOnRevisionId = Guid.NewGuid(),
            Family = ComponentFamily.MP_CM,
            ReferenceSnapshot = "CM 5447",
            LotSnapshot = "Lote 3",
            Fields = new[]
            {
                new JobOnComponentField
                {
                    JobOnComponentFieldId = Guid.NewGuid(),
                    JobOnComponentId = componentId,
                    FieldKey = "peso",
                    ValueText = "1.0",
                    DisplayOrder = 0
                }
            }
        };
        var revision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = jobOnId,
            RevisionNumber = 2,
            ProductionSnapshot = "{\"production_code\":\"202608\"}",
            ReferenceSnapshot = "{\"article_reference\":\"9262T288\"}",
            MachineSnapshot = $"{{\"machine_code\":\"{Line}\"}}",
            DatesSnapshot = "{\"start_at\":\"2026-08-17T08:00:00Z\",\"end_at\":null}",
            Sections = "{}",
            GeneralNotes = "notas da revisão 2",
            SavedBy = "actor-new-tool",
            SavedAtUtc = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc),
            Components = new[] { component }
        };
        await _repository.SaveRevisionGraphAsync(revision, "jobon.guardar", "actor-new-tool");
        return (await _repository.GetByIdAsync(jobOnId))!.CurrentRevision!;
    }

    private sealed class NewToolSaveIdentity : ICurrentUserAccessor
    {
        public CurrentUser? User { get; set; }

        public CurrentUser? Current => User;

        public void GrantResponsible() => User = new CurrentUser(
            Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            "Responsável Técnico",
            new[] { "jobon" },
            new[] { "jobon.view", "jobon.edit", "jobon.configure", "jobon.confirmar" });
    }

    private sealed class FixedClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }
}
