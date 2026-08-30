using System.Text.Json;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// "Alterar data" use-case tests (modules/05, TD-18). The operation changes the
/// planned dates of an EXISTING Job On by creating a NEW immutable revision of the
/// SAME <c>job_on_id</c> — never a new Job On. It requires <c>jobon.edit</c>, keeps
/// the complete current setup (reference, sections, drop count, notes, typed values,
/// components, fields, CAL rows and verification state), advances
/// <c>current_revision_id</c>, leaves every previous revision untouched and readable,
/// and records the audit fact atomically with the revision.
/// </summary>
public class JobOnAlterDateTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeJobOnRepository _repository = new();
    private readonly AlterDateTestIdentity _identity = new();
    private readonly FakeJobOnUserContextRepository _userContext = new();
    private readonly JobOnService _service;

    public JobOnAlterDateTests()
    {
        var gate = new JobOnAuthorizationGate(_identity);
        _service = new JobOnService(
            gate, _repository, _userContext, new AlterDateTestClock(
                new DateTimeOffset(2026, 8, 18, 9, 0, 0, TimeSpan.Zero)),
            new FakeFerramentasToolLookup(),
            articleImages: null);
        _identity.GrantResponsible();
    }

    [Fact]
    public async Task AlterDates_WithEditCapability_NewRevisionOfSameJobOn_NewDates()
    {
        // Tests #1, #3, #4, #5, #7, #9 — Responsible + jobon.edit alters the dates of
        // an EXISTING Job On: the SAME job_on_id is preserved, a NEW revision id is
        // created, the revision number increments, the new revision carries the new
        // dates snapshot, current_revision_id advances and the header dates (single
        // calendar source) update — atomically, with the audit fact.
        var jobOnId = await CreateRascunhoAsync();
        var oldRevisionId = _repository.JobOns[jobOnId].CurrentRevisionId!.Value;
        var newStart = Start.AddDays(7);
        var newEnd = Start.AddDays(9);

        var result = await _service.AlterDatesAsync(new AlterJobOnDatesRequest(
            jobOnId, newStart, newEnd));

        Assert.True(result.IsSuccess);
        var newRevisionId = result.Value;
        Assert.NotEqual(oldRevisionId, newRevisionId);

        // SAME JobOnId — the operation never creates a new Job On.
        Assert.Single(_repository.JobOns);
        Assert.Equal(jobOnId, _repository.JobOns.Keys.Single());

        // Revision number increments (creation revision 1 → this is revision 2).
        var revision = Assert.Single(_repository.Revisions, r => r.JobOnRevisionId == newRevisionId);
        Assert.Equal(2, revision.RevisionNumber);
        Assert.Equal(jobOnId, revision.JobOnId);

        // New revision records the NEW dates.
        using var dates = JsonDocument.Parse(revision.DatesSnapshot!);
        Assert.Equal(newStart, dates.RootElement.GetProperty("start_at").GetDateTimeOffset());
        Assert.Equal(newEnd, dates.RootElement.GetProperty("end_at").GetDateTimeOffset());

        // current_revision_id advanced to the new revision; header dates updated.
        Assert.Equal(newRevisionId, _repository.JobOns[jobOnId].CurrentRevisionId);
        Assert.Equal(newStart, _repository.JobOns[jobOnId].PlannedStartAt);
        Assert.Equal(newEnd, _repository.JobOns[jobOnId].PlannedEndAt);
        Assert.Contains(_repository.CurrentRevisionUpdates, u => u.RevisionId == newRevisionId);

        // The new revision is the current revision of the reloaded aggregate.
        var reloaded = (await _repository.GetByIdAsync(jobOnId))!;
        Assert.Equal(newRevisionId, reloaded.CurrentRevision!.JobOnRevisionId);
    }

    [Fact]
    public async Task AlterDates_PreservesCurrentSetup_OnlyDateContextChanges()
    {
        // Test #8 — the new revision starts from the current revision and preserves the
        // complete setup (reference snapshot, sections, drop count, notes, typed values,
        // components, fields, CAL rows and verification occurrences); ONLY the date
        // context changes.
        var jobOnId = await CreateRascunhoAsync();
        await SeedRichCurrentRevision(jobOnId);

        var newStart = Start.AddDays(3);
        var result = await _service.AlterDatesAsync(new AlterJobOnDatesRequest(
            jobOnId, newStart, newStart.AddHours(8)));

        Assert.True(result.IsSuccess);
        var revision = Assert.Single(_repository.Revisions, r => r.JobOnRevisionId == result.Value);
        Assert.Equal(3, revision.RevisionNumber);

        using var dates = JsonDocument.Parse(revision.DatesSnapshot!);
        Assert.Equal(newStart, dates.RootElement.GetProperty("start_at").GetDateTimeOffset());

        // Production/reference/machine identity unchanged.
        using var prod = JsonDocument.Parse(revision.ProductionSnapshot!);
        Assert.Equal("202608", prod.RootElement.GetProperty("production_code").GetString());
        using var reference = JsonDocument.Parse(revision.ReferenceSnapshot!);
        Assert.Equal("9262T288", reference.RootElement.GetProperty("article_reference").GetString());
        using var machine = JsonDocument.Parse(revision.MachineSnapshot!);
        Assert.Equal("LINHA-1", machine.RootElement.GetProperty("machine_code").GetString());

        // Revision-owned values preserved from the current revision.
        Assert.Equal("{\"sec\":\"A\"}", revision.Sections);
        Assert.Equal(3.5m, revision.DropCount);
        Assert.Equal("notas preservadas", revision.GeneralNotes);
        Assert.Equal("{\"value\":\"tipo-B\"}", revision.TypeSnapshot);
        Assert.Equal("{\"value\":\"paragem-2\"}", revision.StopSnapshot);
        Assert.Equal(12.34m, revision.WeightSnapshot);
        Assert.Equal("{\"value\":\"NNPB\"}", revision.ProcessSnapshot);

        // The complete component graph is copied: CM component with a field, a CAL row
        // and a confirmed verification preserved (new ids, same state).
        var component = Assert.Single(revision.Components ?? Array.Empty<JobOnComponent>());
        Assert.Equal(ComponentFamily.MP_CM, component.Family);
        Assert.Equal("CM 5447", component.ReferenceSnapshot);
        Assert.Equal("Lote 3", component.LotSnapshot);

        var field = Assert.Single(component.Fields ?? Array.Empty<JobOnComponentField>());
        Assert.Equal("peso", field.FieldKey);
        Assert.Equal("1.0", field.ValueText);

        var row = Assert.Single(component.Rows ?? Array.Empty<JobOnComponentRow>());
        Assert.Equal("CAL1", row.ElementLabel);

        // Same production occurrence: the confirmation state is NOT silently reset
        // (regeneration-as-pendente is the DUPLICATE rule, a new production).
        var verification = Assert.Single(component.Verifications ?? Array.Empty<JobOnVerificationOccurrence>());
        Assert.Equal("confirmada", verification.Status);
        Assert.Equal("actor-1", verification.CompletedBy);
        Assert.NotNull(verification.CompletedAtUtc);
    }

    [Fact]
    public async Task AlterDates_PreviousRevisionRemainsUnchanged_AndReadable()
    {
        // Test #6 — previous revisions are immutable and remain readable: their
        // snapshots and component graphs (including verified occurrences) never change.
        var jobOnId = await CreateRascunhoAsync();
        var richRevisionId = await SeedRichCurrentRevision(jobOnId);

        var result = await _service.AlterDatesAsync(new AlterJobOnDatesRequest(
            jobOnId, Start.AddDays(4), null));

        Assert.True(result.IsSuccess);

        var reloaded = (await _repository.GetByIdAsync(jobOnId))!;
        var previous = reloaded.Revisions.Single(r => r.JobOnRevisionId == richRevisionId);
        using var previousDates = JsonDocument.Parse(previous.DatesSnapshot!);
        Assert.Equal(Start, previousDates.RootElement.GetProperty("start_at").GetDateTimeOffset());
        Assert.Equal("{\"sec\":\"A\"}", previous.Sections);
        Assert.Equal("notas preservadas", previous.GeneralNotes);

        var previousComponent = Assert.Single(previous.Components ?? Array.Empty<JobOnComponent>());
        var previousVerification = Assert.Single(previousComponent.Verifications ?? Array.Empty<JobOnVerificationOccurrence>());
        Assert.Equal("confirmada", previousVerification.Status); // history never mutated

        // The repository-level verification rows of the old revision are untouched.
        var oldOccurrenceId = _repository.Verifications
            .Single(v => v.JobOnComponentId == previousComponent.JobOnComponentId)
            .JobOnVerificationOccurrenceId;
        Assert.Equal("confirmada", _repository.Verifications
            .Single(v => v.JobOnVerificationOccurrenceId == oldOccurrenceId).Status);
    }

    [Fact]
    public async Task AlterDates_WithoutEditCapability_IsDenied_AndWritesNothing()
    {
        // Test #2 — an Operator/Controller with only jobon.view is denied the WRITE
        // operation server-side (gate, not just hidden UI), and nothing is persisted.
        var jobOnId = await CreateRascunhoAsync();
        var currentRevisionId = _repository.JobOns[jobOnId].CurrentRevisionId;
        var revisionCountBefore = _repository.Revisions.Count;
        _identity.GrantViewOnly();

        var result = await _service.AlterDatesAsync(new AlterJobOnDatesRequest(
            jobOnId, Start.AddDays(1), null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
        Assert.Equal(currentRevisionId, _repository.JobOns[jobOnId].CurrentRevisionId);
        Assert.Equal(Start, _repository.JobOns[jobOnId].PlannedStartAt); // header unchanged
        Assert.DoesNotContain(_repository.AuditEvents, a => a.EventType == "jobon.alterar.data");
    }

    [Fact]
    public async Task AlterDates_PersistenceFailure_LeavesNoPartialRevision_AndOldCurrentRemains()
    {
        // Test #10 — when the atomic persistence fails, NOTHING remains: no new
        // revision, header dates unchanged, the OLD current_revision_id stays, and no
        // audit fact (mirror of the atomic create/duplicate contract).
        var jobOnId = await CreateRascunhoAsync();
        var oldCurrent = _repository.JobOns[jobOnId].CurrentRevisionId;
        var revisionCountBefore = _repository.Revisions.Count;
        _repository.FailAlterDatesAtomically = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AlterDatesAsync(new AlterJobOnDatesRequest(
                jobOnId, Start.AddDays(2), Start.AddDays(3))));

        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
        Assert.Equal(oldCurrent, _repository.JobOns[jobOnId].CurrentRevisionId);
        Assert.Equal(Start, _repository.JobOns[jobOnId].PlannedStartAt); // header untouched
        Assert.Null(_repository.JobOns[jobOnId].PlannedEndAt);
        Assert.DoesNotContain(_repository.AuditEvents, a => a.EventType == "jobon.alterar.data");
    }

    [Fact]
    public async Task AlterDates_EmitsAuditEvent_ThroughExistingAuditPath()
    {
        // Test #11 — the date change emits the audit event through the existing
        // append-only path, identifying the SAME Job On, the previous revision/state
        // (before) and the new revision/dates (after), with the actor.
        var jobOnId = await CreateRascunhoAsync();
        var oldRevisionId = _repository.JobOns[jobOnId].CurrentRevisionId!.Value;
        var newStart = Start.AddDays(5);
        var newEnd = Start.AddDays(6);

        var result = await _service.AlterDatesAsync(new AlterJobOnDatesRequest(
            jobOnId, newStart, newEnd));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(
            _repository.AuditEvents, a => a.EventType == "jobon.alterar.data");
        Assert.Equal(jobOnId, audit.JobId);
        Assert.Equal(result.Value, audit.RevisionId);
        Assert.Equal("aaaaaaaa-0000-0000-0000-000000000001", audit.ActorId);

        using var before = JsonDocument.Parse(audit.Before!);
        Assert.Equal(oldRevisionId, before.RootElement.GetProperty("revision_id").GetGuid());
        Assert.Equal(Start, before.RootElement.GetProperty("start_at").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("end_at").ValueKind);

        using var after = JsonDocument.Parse(audit.After!);
        Assert.Equal(result.Value, after.RootElement.GetProperty("revision_id").GetGuid());
        Assert.Equal(newStart, after.RootElement.GetProperty("start_at").GetDateTimeOffset());
        Assert.Equal(newEnd, after.RootElement.GetProperty("end_at").GetDateTimeOffset());
    }

    [Fact]
    public async Task AlterDates_OnClosedWithoutChangeReason_IsRejected()
    {
        // Same established rule as any edit of a closed revision: fechado requires a
        // change_reason (modules/05 §4/§5.4).
        var jobOnId = await CreateClosedAsync();
        var revisionCountBefore = _repository.Revisions.Count;

        var result = await _service.AlterDatesAsync(new AlterJobOnDatesRequest(
            jobOnId, Start.AddDays(1), null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.ValidationError, result.Error.Category);
        Assert.Equal("JOBON_CHANGE_REASON_REQUIRED", result.Error.Code);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
    }

    [Fact]
    public async Task AlterDates_OnClosedWithChangeReason_IsAllowed()
    {
        var jobOnId = await CreateClosedAsync();

        var result = await _service.AlterDatesAsync(new AlterJobOnDatesRequest(
            jobOnId, Start.AddDays(1), null, "Correção de datas após fecho"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _repository.Revisions.Count); // creation revision 1 + this alter
    }

    [Fact]
    public async Task AlterDates_WithStartAfterEnd_IsRejected()
    {
        var jobOnId = await CreateRascunhoAsync();

        var result = await _service.AlterDatesAsync(new AlterJobOnDatesRequest(
            jobOnId, Start.AddDays(2), Start.AddDays(1)));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.ValidationError, result.Error.Category);
        Assert.Equal("JOBON_INVALID_DATES", result.Error.Code);
        Assert.DoesNotContain(_repository.AuditEvents, a => a.EventType == "jobon.alterar.data");
    }

    [Fact]
    public async Task AlterDates_JobOnNotFound_ReturnsNotFound()
    {
        var result = await _service.AlterDatesAsync(new AlterJobOnDatesRequest(
            Guid.NewGuid(), Start.AddDays(1), null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, result.Error.Category);
        Assert.Equal("JOBON_NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public async Task AlterDates_IncrementsFromCurrentRevisionNumber()
    {
        // The new revision number always increments from the CURRENT revision, even
        // when several revisions already exist.
        var jobOnId = await CreateRascunhoAsync();
        await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, Array.Empty<JobOnComponent>())); // → revision 2

        var result = await _service.AlterDatesAsync(new AlterJobOnDatesRequest(
            jobOnId, Start.AddDays(3), null));

        Assert.True(result.IsSuccess);
        var revision = _repository.Revisions.Single(r => r.JobOnRevisionId == result.Value);
        Assert.Equal(3, revision.RevisionNumber);
        Assert.Equal(2, _repository.Revisions.Count(r => r.JobOnId == jobOnId && r.RevisionNumber < 3));
    }

    // ---- helpers ------------------------------------------------------------

    private async Task<Guid> CreateRascunhoAsync()
    {
        var result = await _service.CreateAsync(new CreateJobOnRequest("202608", "LINHA-1", Start, null, "9262T288"));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private async Task<Guid> CreateClosedAsync()
    {
        var id = await CreateRascunhoAsync();
        await _service.TransitionAsync(new TransitionJobOnRequest(id, JobOnLifecycleState.Planeado));
        await _service.TransitionAsync(new TransitionJobOnRequest(id, JobOnLifecycleState.EmFabrico));
        await _service.TransitionAsync(new TransitionJobOnRequest(id, JobOnLifecycleState.Fechado));
        return id;
    }

    /// <summary>
    /// Seeds a rich current revision (revision 2) with the complete setup: reference,
    /// sections, drop count, notes, typed values and a CM component carrying a field, a
    /// CAL row and a CONFIRMED verification.
    /// </summary>
    private async Task<Guid> SeedRichCurrentRevision(Guid jobOnId)
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
                    Status = "confirmada",
                    CompletedBy = "actor-1",
                    CompletedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    CreatedAtUtc = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc)
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
            MachineSnapshot = "{\"machine_code\":\"LINHA-1\"}",
            DatesSnapshot = "{\"start_at\":\"2026-08-17T08:00:00Z\",\"end_at\":null}",
            Sections = "{\"sec\":\"A\"}",
            DropCount = 3.5m,
            TypeSnapshot = "{\"value\":\"tipo-B\"}",
            StopSnapshot = "{\"value\":\"paragem-2\"}",
            WeightSnapshot = 12.34m,
            ProcessSnapshot = "{\"value\":\"NNPB\"}",
            GeneralNotes = "notas preservadas",
            SavedBy = "actor-1",
            SavedAtUtc = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc),
            Components = new[] { component }
        };
        await _repository.SaveRevisionGraphAsync(revision, "jobon.guardar", "actor-1");
        return revision.JobOnRevisionId;
    }

    private sealed class AlterDateTestIdentity : ICurrentUserAccessor
    {
        public CurrentUser? User { get; set; }

        public CurrentUser? Current => User;

        public void GrantResponsible() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            "Responsável Técnico",
            new[] { "jobon" },
            new[] { "jobon.view", "jobon.edit", "jobon.configure", "jobon.confirmar" });

        public void GrantViewOnly() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
            "Operador",
            new[] { "jobon" },
            new[] { "jobon.view" });
    }

    private sealed class AlterDateTestClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }
}