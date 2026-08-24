using System.Text.Json;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// U-13 Job On use-case tests (modules/05 §2/§4/§6/§7, TD-18/TD-27).
/// High-value coverage: capability gate on every operation, create/duplicate/
/// save-revision/transition/resolve/confirm-verification, revision
/// immutability, closed-revision change_reason, and audit facts. All
/// collaborators are fakes — no live DB.
/// </summary>
public class JobOnServiceTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeJobOnRepository _repository = new();
    private readonly FakeCurrentUserAccessor _identity = new();
    private readonly FakeJobOnUserContextRepository _userContext = new();
    private readonly JobOnService _service;

    public JobOnServiceTests()
    {
        var gate = new JobOnAuthorizationGate(_identity);
        _service = new JobOnService(
            gate, _repository, _userContext, new FixedClock(
                new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero)));
        _identity.GrantJobOn();
    }

    // ---- authorization gate (fail closed, capability only) ----------------

    [Fact]
    public async Task Create_WithoutEditCapability_IsDenied_AndWritesNothing()
    {
        _identity.GrantNone();

        var result = await _service.CreateAsync(new CreateJobOnRequest("202608", "LINHA-1", Start, null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
        Assert.Empty(_repository.JobOns);
        Assert.Empty(_repository.AuditEvents);
    }

    [Fact]
    public async Task ConfirmVerification_WithoutConfirmarCapability_IsDenied()
    {
        _identity.GrantViewOnly();

        var result = await _service.ConfirmVerificationAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
        Assert.Empty(_repository.VerificationUpdates);
    }

    // ---- create -----------------------------------------------------------

    [Fact]
    public async Task Create_WithEditCapability_CreatesRascunho_AndAudits()
    {
        var result = await _service.CreateAsync(new CreateJobOnRequest("202608", "LINHA-1", Start, null));

        Assert.True(result.IsSuccess);
        var jobOn = Assert.Single(_repository.JobOns.Values);
        Assert.Equal(JobOnLifecycleState.Rascunho, jobOn.LifecycleState);
        Assert.Equal("202608", jobOn.ProductionCode);
        Assert.Equal("LINHA-1", jobOn.MachineCode);
        Assert.Contains(_repository.AuditEvents, a => a.EventType == "jobon.criar");
    }

    [Theory]
    [InlineData("", "LINHA-1")]
    [InlineData("202608", "")]
    [InlineData("   ", "LINHA-1")]
    public async Task Create_WithMissingProductionOrMachine_IsRejected(string production, string machine)
    {
        var result = await _service.CreateAsync(new CreateJobOnRequest(production, machine, Start, null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.ValidationError, result.Error.Category);
        Assert.Empty(_repository.JobOns);
    }

    // ---- duplication (modules/05 §6.2) -----------------------------------

    [Fact]
    public async Task Duplicate_CopiesSnapshot_NewDates_AndAudits()
    {
        var sourceId = await SeedPlaneadoWithRevision();
        var seededVerificationCount = _repository.Verifications.Count;

        var result = await _service.DuplicateAsync(new DuplicateJobOnRequest(
            sourceId, "202620", "LINHA-1", Start.AddDays(3), Start.AddDays(3).AddHours(8)));

        Assert.True(result.IsSuccess);
        var duplicated = _repository.JobOns[result.Value];
        Assert.Equal("202620", duplicated.ProductionCode);
        Assert.Equal(Start.AddDays(3), duplicated.PlannedStartAt);
        Assert.Equal(sourceId, duplicated.CopiedFromJobOnId);
        // Origin immutable.
        Assert.Equal("202608", _repository.JobOns[sourceId].ProductionCode);
        // New revision copied with regenerated pendente occurrences (never copied checks).
        Assert.Contains(_repository.AuditEvents, a => a.EventType == "jobon.duplicar");
        Assert.Equal(2, _repository.Revisions.Count);
        var newVerifications = _repository.Verifications.Skip(seededVerificationCount).ToList();
        Assert.NotEmpty(newVerifications);
        Assert.All(newVerifications, v => Assert.Equal("pendente", v.Status));
    }

    [Fact]
    public async Task Duplicate_SourceNotFound_ReturnsNotFound()
    {
        var result = await _service.DuplicateAsync(new DuplicateJobOnRequest(
            Guid.NewGuid(), "202620", "LINHA-1", Start, null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, result.Error.Category);
        Assert.Empty(_repository.JobOns);
    }

    // ---- save revision (TD-18) -------------------------------------------

    [Fact]
    public async Task SaveRevision_InsertsNewRevision_AndUpdatesCurrent()
    {
        var jobOnId = await SeedRascunho();

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, Array.Empty<JobOnComponent>()));

        Assert.True(result.IsSuccess);
        var revision = Assert.Single(_repository.Revisions);
        Assert.Equal(1, revision.RevisionNumber);
        Assert.Equal("Notas", revision.GeneralNotes);
        Assert.Contains(_repository.CurrentRevisionUpdates, u => u.RevisionId == revision.JobOnRevisionId);
        Assert.Contains(_repository.AuditEvents, a => a.EventType == "jobon.guardar");
    }

    [Fact]
    public async Task SaveRevision_OnClosedWithoutChangeReason_IsRejected()
    {
        var jobOnId = await SeedClosed();

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, Array.Empty<JobOnComponent>()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.ValidationError, result.Error.Category);
        Assert.Equal("JOBON_CHANGE_REASON_REQUIRED", result.Error.Code);
        Assert.Empty(_repository.Revisions);
    }

    [Fact]
    public async Task SaveRevision_OnClosedWithChangeReason_IsAllowed()
    {
        var jobOnId = await SeedClosed();

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", "Correção pós-fecho", null, Array.Empty<JobOnComponent>()));

        Assert.True(result.IsSuccess);
        Assert.Single(_repository.Revisions);
    }

    // ---- snapshot completeness (owner D2 contract, TD-18) -----------------

    [Fact]
    public async Task SaveRevision_ConstructsCompleteSnapshot_FromHeaderAndContext()
    {
        var jobOnId = await SeedRascunho();

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas gerais", null, "dir-imagens/artigo", Array.Empty<JobOnComponent>()));

        Assert.True(result.IsSuccess);
        var revision = Assert.Single(_repository.Revisions);

        // production/machine/dates come from the Job On header/context.
        using var prod = JsonDocument.Parse(revision.ProductionSnapshot!);
        Assert.Equal("202608", prod.RootElement.GetProperty("production_code").GetString());

        using var machine = JsonDocument.Parse(revision.MachineSnapshot!);
        Assert.Equal("LINHA-1", machine.RootElement.GetProperty("machine_code").GetString());

        using var dates = JsonDocument.Parse(revision.DatesSnapshot!);
        Assert.Equal(Start, dates.RootElement.GetProperty("start_at").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, dates.RootElement.GetProperty("end_at").ValueKind);

        // Reference and typed values are absent on the first save (no prior
        // revision): preserved as null, never invented.
        Assert.Null(revision.ReferenceSnapshot);
        Assert.Null(revision.TypeSnapshot);
        Assert.Null(revision.StopSnapshot);
        Assert.Null(revision.WeightSnapshot);
        Assert.Null(revision.ProcessSnapshot);
    }

    [Fact]
    public async Task SaveRevision_CarriesReferenceAndTypedSnapshots_FromCurrentRevision()
    {
        var jobOnId = await SeedRascunho();

        // Seed a prior revision with readable reference + typed snapshots.
        var prior = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = jobOnId,
            RevisionNumber = 1,
            ProductionSnapshot = "{\"production_code\":\"202608\"}",
            ReferenceSnapshot = "{\"article_reference\":\"9262T288\"}",
            MachineSnapshot = "{\"machine_code\":\"LINHA-1\"}",
            DatesSnapshot = "{\"start_at\":\"2026-08-17T08:00:00Z\",\"end_at\":null}",
            TypeSnapshot = "{\"value\":\"tipo-A\"}",
            StopSnapshot = "{\"value\":\"paragem-1\"}",
            WeightSnapshot = 12.34m,
            ProcessSnapshot = "{\"value\":\"NNPB\"}",
            Sections = "{}",
            SavedBy = "actor-1",
            SavedAtUtc = DateTime.UtcNow
        };
        await _repository.InsertRevisionAsync(prior);
        await _repository.UpdateCurrentRevisionAsync(jobOnId, prior.JobOnRevisionId);

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas gerais", null, null, Array.Empty<JobOnComponent>()));

        Assert.True(result.IsSuccess);
        var revision = _repository.Revisions.Single(r => r.RevisionNumber == 2);

        // Reference and revision-owned typed values carried from the current
        // revision exactly (no re-encoding / no double wrap).
        Assert.Equal(prior.ReferenceSnapshot, revision.ReferenceSnapshot);
        using var refDoc = JsonDocument.Parse(revision.ReferenceSnapshot!);
        Assert.Equal("9262T288", refDoc.RootElement.GetProperty("article_reference").GetString());

        Assert.Equal(prior.TypeSnapshot, revision.TypeSnapshot);
        Assert.Equal(prior.StopSnapshot, revision.StopSnapshot);
        Assert.Equal(prior.ProcessSnapshot, revision.ProcessSnapshot);
        Assert.Equal(prior.WeightSnapshot, revision.WeightSnapshot);
    }

    [Fact]
    public async Task SaveRevision_DoesNotSubstituteInternalId_AsReadableReference()
    {
        // On a first save there is no prior readable reference; even though the
        // header may only hold an internal article_reference_id (UUID), the
        // snapshot must NOT use that UUID — it is preserved as null.
        var jobOnId = await SeedRascunho();

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, Array.Empty<JobOnComponent>()));

        Assert.True(result.IsSuccess);
        var revision = Assert.Single(_repository.Revisions);
        Assert.Null(revision.ReferenceSnapshot);
    }

    [Fact]
    public async Task SaveRevision_CarriesHeaderContextSnapshots_FromCurrentRevisionOntoNext()
    {
        var jobOnId = await SeedRascunho();
        _repository.JobOns[jobOnId].TransitionTo(JobOnLifecycleState.Planeado);

        var first = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas 1", null, null, Array.Empty<JobOnComponent>()));

        // Datas are still produced from the header on every save; the second save
        // must not clobber the complete header-derived snapshot.
        var second = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas 2", null, null, Array.Empty<JobOnComponent>()));

        Assert.True(second.IsSuccess);
        var rev2 = _repository.Revisions.Single(r => r.RevisionNumber == 2);
        using var prod = JsonDocument.Parse(rev2.ProductionSnapshot!);
        Assert.Equal("202608", prod.RootElement.GetProperty("production_code").GetString());
    }

    // ---- lifecycle transition (TD-27) ------------------------------------

    [Fact]
    public async Task Transition_Valid_UpdatesState_AndAudits()
    {
        var jobOnId = await SeedRascunho();

        var result = await _service.TransitionAsync(new TransitionJobOnRequest(jobOnId, JobOnLifecycleState.Planeado));

        Assert.True(result.IsSuccess);
        Assert.Equal(JobOnLifecycleState.Planeado, result.Value);
        Assert.Contains(_repository.LifecycleUpdates, s => s == JobOnLifecycleState.Planeado);
        Assert.Contains(_repository.AuditEvents, a => a.EventType == "jobon.transicao");
    }

    [Fact]
    public async Task Transition_Invalid_ReturnsDomainConflict()
    {
        var jobOnId = await SeedRascunho();

        var result = await _service.TransitionAsync(new TransitionJobOnRequest(jobOnId, JobOnLifecycleState.Fechado));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.DomainConflict, result.Error.Category);
        Assert.Empty(_repository.LifecycleUpdates);
    }

    // ---- resolve (TD-27) --------------------------------------------------

    [Fact]
    public async Task Resolve_ReturnsResolution_ForActiveCandidates()
    {
        var jobOnId = await SeedPlaneadoWithRevision();
        _repository.JobOns[jobOnId].TransitionTo(JobOnLifecycleState.Planeado);

        var result = await _service.ResolveAsync("LINHA-1", Start.AddHours(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(JobOnResolutionKind.Single, result.Value.Kind);
    }

    [Fact]
    public async Task Resolve_WithoutViewCapability_IsDenied()
    {
        _identity.GrantNone();

        var result = await _service.ResolveAsync("LINHA-1", Start);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    // ---- confirm verification (modules/05 §7) ----------------------------

    [Fact]
    public async Task ConfirmVerification_WithConfirmarCapability_UpdatesStatus()
    {
        var occurrenceId = Guid.NewGuid();

        var result = await _service.ConfirmVerificationAsync(occurrenceId);

        Assert.True(result.IsSuccess);
        var update = Assert.Single(_repository.VerificationUpdates);
        Assert.Equal(occurrenceId, update.OccurrenceId);
        Assert.Equal("confirmada", update.Status);
        Assert.NotNull(update.CompletedBy);
        Assert.NotNull(update.CompletedAt);
    }

    // ---- helpers ----------------------------------------------------------

    private async Task<Guid> SeedRascunho()
    {
        var result = await _service.CreateAsync(new CreateJobOnRequest("202608", "LINHA-1", Start, null));
        return result.Value;
    }

    private async Task<Guid> CreateRascunhoAsync()
    {
        return await SeedRascunho();
    }

    private async Task<Guid> SeedPlaneadoWithRevision()
    {
        var id = await SeedRascunho();
        var componentId = Guid.NewGuid();
        var component = new JobOnComponent
        {
            JobOnComponentId = componentId,
            JobOnRevisionId = Guid.NewGuid(),
            Family = ComponentFamily.MP_CM,
            ReferenceSnapshot = "CM 5447",
            LotSnapshot = "Lote 3",
            Verifications = new[]
            {
                new JobOnVerificationOccurrence
                {
                    JobOnVerificationOccurrenceId = Guid.NewGuid(),
                    JobOnComponentId = componentId,
                    Status = "confirmada",
                    CompletedBy = "actor-1",
                    CompletedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            }
        };
        await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            id, "Notas", null, null, new[] { component }));
        return id;
    }

    private async Task<Guid> SeedClosed()
    {
        var id = await SeedRascunho();
        await _service.TransitionAsync(new TransitionJobOnRequest(id, JobOnLifecycleState.Planeado));
        await _service.TransitionAsync(new TransitionJobOnRequest(id, JobOnLifecycleState.EmFabrico));
        await _service.TransitionAsync(new TransitionJobOnRequest(id, JobOnLifecycleState.Fechado));
        return id;
    }

    // ---- image acceptance (TD-23, modules/05 §5.7) -------------------------

    [Fact]
    public async Task AttachImage_CreatesNewRevision_WithImageAssetId()
    {
        var id = await CreateRascunhoAsync();
        // Create an initial revision so there is a current revision to attach to.
        var initialRevision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = id,
            RevisionNumber = 1,
            SavedBy = "test",
            SavedAtUtc = DateTime.UtcNow
        };
        await _repository.InsertRevisionAsync(initialRevision);
        await _repository.UpdateCurrentRevisionAsync(id, initialRevision.JobOnRevisionId);

        var result = await _service.AttachImageAsync(
            new AttachImageRequest(id, "dir-imagens/artigo-9262T288"));

        Assert.True(result.IsSuccess);
        var newRevisionId = result.Value;
        Assert.NotEqual(initialRevision.JobOnRevisionId, newRevisionId);

        // Verify the new revision has the image_asset_id.
        var newRevision = _repository.Revisions.First(r => r.JobOnRevisionId == newRevisionId);
        Assert.Equal("dir-imagens/artigo-9262T288", newRevision.ImageAssetId);

        // Verify audit event was recorded.
        var audit = _repository.AuditEvents.FirstOrDefault(e => e.EventType == "jobon.imagem.anexar");
        Assert.NotEqual(default, audit);
        Assert.Equal(id, audit.JobId);
        Assert.Equal(newRevisionId, audit.RevisionId);
        Assert.Null(audit.Before); // No previous image.
        Assert.Equal("dir-imagens/artigo-9262T288", audit.After);
    }

    [Fact]
    public async Task AttachImage_WithoutEditCapability_IsDenied()
    {
        _identity.GrantNone();
        var id = Guid.NewGuid();

        var result = await _service.AttachImageAsync(
            new AttachImageRequest(id, "dir-imagens/test"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
        Assert.Empty(_repository.AuditEvents);
    }

    [Fact]
    public async Task AttachImage_WithEmptyImageAssetId_IsRejected()
    {
        var id = await CreateRascunhoAsync();
        var initialRevision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = id,
            RevisionNumber = 1,
            SavedBy = "test",
            SavedAtUtc = DateTime.UtcNow
        };
        await _repository.InsertRevisionAsync(initialRevision);
        await _repository.UpdateCurrentRevisionAsync(id, initialRevision.JobOnRevisionId);

        var result = await _service.AttachImageAsync(
            new AttachImageRequest(id, ""));

        Assert.True(result.IsFailure);
        Assert.Equal("JOBON_IMAGE_INVALID", result.Error.Code);
    }

    [Fact]
    public async Task AttachImage_WithoutCurrentRevision_IsRejected()
    {
        var id = await CreateRascunhoAsync();
        // No revision exists yet.

        var result = await _service.AttachImageAsync(
            new AttachImageRequest(id, "dir-imagens/test"));

        Assert.True(result.IsFailure);
        Assert.Equal("JOBON_NO_REVISION", result.Error.Code);
    }

    [Fact]
    public async Task ReplaceImage_CreatesNewRevision_WithNewImageAssetId()
    {
        var id = await CreateRascunhoAsync();
        var initialRevision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = id,
            RevisionNumber = 1,
            ImageAssetId = "dir-imagens/old",
            SavedBy = "test",
            SavedAtUtc = DateTime.UtcNow
        };
        await _repository.InsertRevisionAsync(initialRevision);
        await _repository.UpdateCurrentRevisionAsync(id, initialRevision.JobOnRevisionId);

        var result = await _service.ReplaceImageAsync(
            new ReplaceImageRequest(id, "dir-imagens/new"));

        Assert.True(result.IsSuccess);
        var newRevisionId = result.Value;
        Assert.NotEqual(initialRevision.JobOnRevisionId, newRevisionId);

        var newRevision = _repository.Revisions.First(r => r.JobOnRevisionId == newRevisionId);
        Assert.Equal("dir-imagens/new", newRevision.ImageAssetId);

        // Verify audit event with before/after.
        var audit = _repository.AuditEvents.FirstOrDefault(e => e.EventType == "jobon.imagem.substituir");
        Assert.NotEqual(default, audit);
        Assert.Equal("dir-imagens/old", audit.Before);
        Assert.Equal("dir-imagens/new", audit.After);
    }

    [Fact]
    public async Task ReplaceImage_WithoutExistingImage_IsRejected()
    {
        var id = await CreateRascunhoAsync();
        var initialRevision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = id,
            RevisionNumber = 1,
            ImageAssetId = null, // No image to replace.
            SavedBy = "test",
            SavedAtUtc = DateTime.UtcNow
        };
        await _repository.InsertRevisionAsync(initialRevision);
        await _repository.UpdateCurrentRevisionAsync(id, initialRevision.JobOnRevisionId);

        var result = await _service.ReplaceImageAsync(
            new ReplaceImageRequest(id, "dir-imagens/new"));

        Assert.True(result.IsFailure);
        Assert.Equal("JOBON_NO_IMAGE", result.Error.Code);
    }

    [Fact]
    public async Task RemoveImage_CreatesNewRevision_WithNullImageAssetId()
    {
        var id = await CreateRascunhoAsync();
        var initialRevision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = id,
            RevisionNumber = 1,
            ImageAssetId = "dir-imagens/to-remove",
            SavedBy = "test",
            SavedAtUtc = DateTime.UtcNow
        };
        await _repository.InsertRevisionAsync(initialRevision);
        await _repository.UpdateCurrentRevisionAsync(id, initialRevision.JobOnRevisionId);

        var result = await _service.RemoveImageAsync(new RemoveImageRequest(id));

        Assert.True(result.IsSuccess);
        var newRevisionId = result.Value;
        Assert.NotEqual(initialRevision.JobOnRevisionId, newRevisionId);

        var newRevision = _repository.Revisions.First(r => r.JobOnRevisionId == newRevisionId);
        Assert.Null(newRevision.ImageAssetId);

        // Verify audit event with before/after.
        var audit = _repository.AuditEvents.FirstOrDefault(e => e.EventType == "jobon.imagem.remover");
        Assert.NotEqual(default, audit);
        Assert.Equal("dir-imagens/to-remove", audit.Before);
        Assert.Null(audit.After);
    }

    [Fact]
    public async Task RemoveImage_WithoutExistingImage_IsRejected()
    {
        var id = await CreateRascunhoAsync();
        var initialRevision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = id,
            RevisionNumber = 1,
            ImageAssetId = null, // No image to remove.
            SavedBy = "test",
            SavedAtUtc = DateTime.UtcNow
        };
        await _repository.InsertRevisionAsync(initialRevision);
        await _repository.UpdateCurrentRevisionAsync(id, initialRevision.JobOnRevisionId);

        var result = await _service.RemoveImageAsync(new RemoveImageRequest(id));

        Assert.True(result.IsFailure);
        Assert.Equal("JOBON_NO_IMAGE", result.Error.Code);
    }

    [Fact]
    public async Task RemoveImage_WithoutEditCapability_IsDenied()
    {
        _identity.GrantNone();
        var id = Guid.NewGuid();

        var result = await _service.RemoveImageAsync(new RemoveImageRequest(id));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    [Fact]
    public async Task DuplicateJobOn_PreservesSameImageAssetId()
    {
        // Seed a source Job On with a revision that has an image.
        var sourceId = await CreateRascunhoAsync();
        var sourceRevision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = sourceId,
            RevisionNumber = 1,
            ImageAssetId = "dir-imagens/artigo-9262T288",
            ReferenceSnapshot = "9262T288",
            SavedBy = "test",
            SavedAtUtc = DateTime.UtcNow
        };
        await _repository.InsertRevisionAsync(sourceRevision);
        await _repository.UpdateCurrentRevisionAsync(sourceId, sourceRevision.JobOnRevisionId);

        // Duplicate the Job On.
        var result = await _service.DuplicateAsync(new DuplicateJobOnRequest(
            sourceId, "202620", "LINHA-1", Start.AddDays(3), Start.AddDays(3).AddHours(8)));

        Assert.True(result.IsSuccess);
        var duplicatedId = result.Value;

        // Verify the duplicated revision has the same ImageAssetId.
        var duplicatedRevisions = _repository.Revisions
            .Where(r => r.JobOnId == duplicatedId)
            .OrderBy(r => r.RevisionNumber)
            .ToList();
        Assert.Single(duplicatedRevisions);
        Assert.Equal("dir-imagens/artigo-9262T288", duplicatedRevisions[0].ImageAssetId);

        // Verify the duplicated revision has the same article/reference.
        Assert.Equal("9262T288", duplicatedRevisions[0].ReferenceSnapshot);

        // Verify the source revision is unchanged.
        var sourceRevisions = _repository.Revisions
            .Where(r => r.JobOnId == sourceId)
            .ToList();
        Assert.Single(sourceRevisions);
        Assert.Equal("dir-imagens/artigo-9262T288", sourceRevisions[0].ImageAssetId);
        Assert.Equal("9262T288", sourceRevisions[0].ReferenceSnapshot);
    }

    [Fact]
    public async Task Duplicate_CopiesFullComponentGraph_WithRePinnedIds_AndRegeneratedVerifications()
    {
        // Source carries a component with a field, a CAL row and an occurrence cheked as
        // "confirmada" — the complete graph duplication contract (R-002): components,
        // fields and CAL rows are copied verbatim under NEW ids pinned to the new revision;
        // verification occurrences are regenerated as pendente (never copied with checks).
        var sourceId = await CreateRascunhoAsync();

        var sourceComponentId = Guid.NewGuid();
        var sourceComponent = new JobOnComponent
        {
            JobOnComponentId = sourceComponentId,
            JobOnRevisionId = Guid.NewGuid(),
            Family = ComponentFamily.CAL,
            ReferenceSnapshot = "REF-CAL",
            DisplayOrder = 0,
            Fields = new[]
            {
                new JobOnComponentField
                {
                    JobOnComponentFieldId = Guid.NewGuid(),
                    JobOnComponentId = sourceComponentId,
                    FieldKey = "pressao",
                    ValueType = "decimal",
                    ValueDecimal = 12.5m,
                    DisplayOrder = 0
                }
            },
            Rows = new[]
            {
                new JobOnComponentRow
                {
                    JobOnComponentRowId = Guid.NewGuid(),
                    JobOnComponentId = sourceComponentId,
                    ElementLabel = "E1",
                    ValueText = "v1",
                    MachineQuantity = 2m,
                    DisplayOrder = 0
                }
            },
            Verifications = new[]
            {
                new JobOnVerificationOccurrence
                {
                    JobOnVerificationOccurrenceId = Guid.NewGuid(),
                    JobOnComponentId = sourceComponentId,
                    RuleTextSnapshot = "Verificar pressão",
                    Status = "confirmada",
                    CompletedBy = "actor-1",
                    CompletedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            }
        };
        var sourceRevision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = sourceId,
            RevisionNumber = 1,
            ReferenceSnapshot = "REF-CAL",
            GeneralNotes = "orig",
            Components = new[] { sourceComponent },
            SavedBy = "test",
            SavedAtUtc = DateTime.UtcNow
        };
        await _repository.SaveRevisionGraphAsync(sourceRevision, "jobon.guardar", "test");

        var result = await _service.DuplicateAsync(new DuplicateJobOnRequest(
            sourceId, "202699", "LINHA-9", Start.AddDays(9), Start.AddDays(9).AddHours(8)));

        Assert.True(result.IsSuccess);
        var duplicatedId = result.Value;

        // Header pinned to the new job on; source unchanged.
        Assert.Equal(duplicatedId, _repository.JobOns[duplicatedId].Id);
        Assert.Equal(sourceId, _repository.JobOns[sourceId].Id);
        Assert.Equal("202608", _repository.JobOns[sourceId].ProductionCode); // source unchanged

        // Exactly one new revision belongs to the duplicate.
        var dupRevisions = _repository.Revisions.Where(r => r.JobOnId == duplicatedId).ToList();
        Assert.Single(dupRevisions);
        var dupRevision = dupRevisions[0];

        // Component copied (new id), re-pinned to the new revision, fields/rows copied verbatim.
        var dupComponent = Assert.Single(dupRevision.Components ?? Array.Empty<JobOnComponent>());
        Assert.NotEqual(sourceComponentId, dupComponent.JobOnComponentId);
        Assert.Equal(dupRevision.JobOnRevisionId, dupComponent.JobOnRevisionId);
        Assert.Equal("REF-CAL", dupComponent.ReferenceSnapshot);
        Assert.Equal(ComponentFamily.CAL, dupComponent.Family);

        var dupField = Assert.Single(dupComponent.Fields ?? Array.Empty<JobOnComponentField>());
        Assert.Equal(dupComponent.JobOnComponentId, dupField.JobOnComponentId);
        Assert.Equal("pressao", dupField.FieldKey);
        Assert.Equal(12.5m, dupField.ValueDecimal);

        var dupRow = Assert.Single(dupComponent.Rows ?? Array.Empty<JobOnComponentRow>());
        Assert.Equal(dupComponent.JobOnComponentId, dupRow.JobOnComponentId);
        Assert.Equal("E1", dupRow.ElementLabel);

        // Verification regenerated as pendente (never copied with checks).
        var dupVerification = Assert.Single(dupComponent.Verifications ?? Array.Empty<JobOnVerificationOccurrence>());
        Assert.Equal("pendente", dupVerification.Status);
        Assert.Null(dupVerification.CompletedBy);
        Assert.Null(dupVerification.CompletedAtUtc);

        // Source component graph remains untouched (reload from persistence path).
        var reloadedSource = await _repository.GetByIdAsync(sourceId);
        Assert.NotNull(reloadedSource);
        var srcComponent = Assert.Single(reloadedSource!.CurrentRevision!.Components ?? Array.Empty<JobOnComponent>());
        Assert.Equal(sourceComponentId, srcComponent.JobOnComponentId);
        var srcVerification = Assert.Single(srcComponent.Verifications ?? Array.Empty<JobOnVerificationOccurrence>());
        Assert.Equal("confirmada", srcVerification.Status);
        Assert.Equal("actor-1", srcVerification.CompletedBy); // source check preserved
    }

    [Fact]
    public async Task SaveRevision_PersistsCompleteComponentGraph_AndAdvancesCurrent()
    {
        // SaveRevisionGraphAsync persists revision + components + fields + CAL rows +
        // verifications + advances current_revision_id + audit atomically. This mirrors the
        // real repository contract exercised through the fake (R-003): a saved revision's full
        // child graph must be stored, not just its header/snapshot.
        var jobOnId = await CreateRascunhoAsync();

        var componentId = Guid.NewGuid();
        var component = new JobOnComponent
        {
            JobOnComponentId = componentId,
            JobOnRevisionId = Guid.NewGuid(),
            Family = ComponentFamily.MP_CM,
            ReferenceSnapshot = "CM 5447",
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
            },
            Rows = new[]
            {
                new JobOnComponentRow
                {
                    JobOnComponentRowId = Guid.NewGuid(),
                    JobOnComponentId = componentId,
                    ElementLabel = "CAL1",
                    DisplayOrder = 0
                }
            },
            Verifications = new[]
            {
                new JobOnVerificationOccurrence
                {
                    JobOnVerificationOccurrenceId = Guid.NewGuid(),
                    JobOnComponentId = componentId,
                    RuleTextSnapshot = "Compatível",
                    Status = "pendente",
                    CreatedAtUtc = DateTime.UtcNow
                }
            }
        };

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, new[] { component }));

        Assert.True(result.IsSuccess);
        var revision = Assert.Single(_repository.Revisions);
        Assert.Contains(_repository.CurrentRevisionUpdates, u => u.RevisionId == revision.JobOnRevisionId);

        // The full child graph was persisted for the saved revision.
        var storedComponent = Assert.Single(_repository.Components.Where(c => c.JobOnRevisionId == revision.JobOnRevisionId));
        Assert.Equal(componentId, storedComponent.JobOnComponentId);
        Assert.NotEmpty(_repository.Fields.Where(f => f.JobOnComponentId == componentId));
        Assert.NotEmpty(_repository.Rows.Where(r => r.JobOnComponentId == componentId));
        Assert.NotEmpty(_repository.Verifications.Where(v => v.JobOnComponentId == componentId));
        Assert.Contains(_repository.AuditEvents, a => a.EventType == "jobon.guardar");
    }

    [Fact]
    public async Task ImageActions_PreserveRevisionImmutability()
    {
        var id = await CreateRascunhoAsync();
        var initialRevision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = id,
            RevisionNumber = 1,
            ImageAssetId = "dir-imagens/original",
            GeneralNotes = "Notas originais",
            SavedBy = "test",
            SavedAtUtc = DateTime.UtcNow
        };
        await _repository.InsertRevisionAsync(initialRevision);
        await _repository.UpdateCurrentRevisionAsync(id, initialRevision.JobOnRevisionId);

        // Attach (replace) image.
        await _service.ReplaceImageAsync(new ReplaceImageRequest(id, "dir-imagens/updated"));

        // Verify the original revision is untouched.
        var original = _repository.Revisions.First(r => r.JobOnRevisionId == initialRevision.JobOnRevisionId);
        Assert.Equal("dir-imagens/original", original.ImageAssetId);
        Assert.Equal("Notas originais", original.GeneralNotes);
        Assert.Equal(1, original.RevisionNumber);

        // Verify a new revision was created.
        var newRev = _repository.Revisions.First(r => r.JobOnRevisionId != initialRevision.JobOnRevisionId);
        Assert.Equal("dir-imagens/updated", newRev.ImageAssetId);
        Assert.Equal("Notas originais", newRev.GeneralNotes); // Preserved from original.
        Assert.Equal(2, newRev.RevisionNumber);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser? User { get; set; }

        public CurrentUser? Current => User;

        public void GrantJobOn() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            "Responsável Técnico",
            new[] { "jobon" },
            new[] { "jobon.view", "jobon.edit", "jobon.configure", "jobon.confirmar" });

        public void GrantViewOnly() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
            "Operador",
            new[] { "jobon" },
            new[] { "jobon.view" });

        public void GrantNone() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"),
            "Sem Acesso",
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private sealed class FixedClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }
}
