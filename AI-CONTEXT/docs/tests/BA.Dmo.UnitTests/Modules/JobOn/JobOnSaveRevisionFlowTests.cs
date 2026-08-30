using System.Text.Json;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// "Guardar nova revisão" use-case tests (TD-18, modules/05 §4/§5.4).
///
/// Editing the folha and saving a NEW REVISION of the SAME Job On: the operation
/// requires <c>jobon.edit</c>, preserves the SAME <c>job_on_id</c> (never a new
/// Job On), creates a NEW immutable revision with an incremented revision number,
/// starts from the CURRENT revision (unchanged values carry forward, changed
/// values appear only in the new revision), persists the complete component graph
/// (components, fields, CAL rows, verification occurrences), advances
/// <c>current_revision_id</c>, leaves every previous revision byte/logically
/// unchanged and readable, records the audit fact atomically (before = previous
/// revision, after = new revision, same <c>job_on_id</c>, actor), enforces the
/// closed-revision change-reason rule server-side, and — on persistence failure —
/// leaves NO partial revision.
///
/// Verification occurrences follow the SAME-production rule the date-change flow
/// documents: they are copied WITH their current state into the new revision
/// (confirmed checks are never silently reset; regeneration-as-pendente is the
/// DUPLICATE rule, a new production) and the previous revision's rows are never
/// touched.
/// </summary>
public class JobOnSaveRevisionFlowTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeJobOnRepository _repository = new();
    private readonly SaveFlowTestIdentity _identity = new();
    private readonly FakeJobOnUserContextRepository _userContext = new();
    private readonly JobOnService _service;

    public JobOnSaveRevisionFlowTests()
    {
        var gate = new JobOnAuthorizationGate(_identity);
        _service = new JobOnService(
            gate, _repository, _userContext, new SaveFlowTestClock(
                new DateTimeOffset(2026, 8, 18, 9, 0, 0, TimeSpan.Zero)),
            new FakeFerramentasToolLookup(),
            articleImages: null);
        _identity.GrantResponsible();
    }

    [Fact]
    public async Task SaveEdit_ResponsibleWithEdit_SavesNewRevision_SameJobOn_NewId_Incremented_CurrentAdvanced()
    {
        // Tests #1, #3, #4, #5, #12 — Responsible + jobon.edit saves an edited revision
        // of an EXISTING Job On: the SAME job_on_id is preserved (never a new Job On),
        // a NEW revision id is created, the revision number increments and
        // current_revision_id advances to the new revision.
        var jobOnId = await CreateRascunhoAsync();
        var oldRevisionId = _repository.JobOns[jobOnId].CurrentRevisionId!.Value;

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas editadas", null, null, Array.Empty<JobOnComponent>()));

        Assert.True(result.IsSuccess);
        var newRevisionId = result.Value;
        Assert.NotEqual(oldRevisionId, newRevisionId);

        // SAME JobOnId — save never creates a new Job On.
        Assert.Single(_repository.JobOns);
        Assert.Equal(jobOnId, _repository.JobOns.Keys.Single());

        // Revision number increments (creation revision 1 → this is revision 2).
        var revision = Assert.Single(_repository.Revisions, r => r.JobOnRevisionId == newRevisionId);
        Assert.Equal(2, revision.RevisionNumber);
        Assert.Equal(jobOnId, revision.JobOnId);

        // current_revision_id advanced to the new revision (reload from persistence).
        Assert.Equal(newRevisionId, _repository.JobOns[jobOnId].CurrentRevisionId);
        var reloaded = (await _repository.GetByIdAsync(jobOnId))!;
        Assert.Equal(newRevisionId, reloaded.CurrentRevision!.JobOnRevisionId);
    }

    [Fact]
    public async Task SaveEdit_OperatorWithoutEdit_IsDenied_AndWritesNothing()
    {
        // Test #2 — an Operator with only jobon.view is denied the WRITE operation
        // server-side (gate, not just hidden UI), and nothing is persisted.
        var jobOnId = await CreateRascunhoAsync();
        var currentRevisionId = _repository.JobOns[jobOnId].CurrentRevisionId;
        var revisionCountBefore = _repository.Revisions.Count;
        _identity.GrantViewOnly();

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, Array.Empty<JobOnComponent>()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
        Assert.Equal(currentRevisionId, _repository.JobOns[jobOnId].CurrentRevisionId);
        Assert.DoesNotContain(_repository.AuditEvents, a => a.EventType == "jobon.guardar");
    }

    [Fact]
    public async Task SaveEdit_ChangedValuesInNewRevision_UnchangedValuesPreserved()
    {
        // Tests #7, #8 — the new revision starts from the CURRENT revision: changed
        // values (general notes + edited component values) appear ONLY in the new
        // revision, while unchanged values (reference, sections, drop count, typed
        // values, and the component graph rows the client submits untouched) carry
        // forward verbatim.
        var jobOnId = await CreateRascunhoAsync();
        var rich = await SeedRichCurrentRevision(jobOnId); // revision 2 with CM + field + CAL row + confirmed check

        // Edit: new general notes + a changed CM reference/lot + a changed field value.
        var componentCopy = rich.Components!.Single();
        var editedComponent = componentCopy with
        {
            ReferenceSnapshot = "CM 9999",
            LotSnapshot = "Lote 9",
            Fields = (componentCopy.Fields ?? []).Select(f => f with { ValueText = "2.5" }).ToList()
        };

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "notas da revisão 3", null, null, new[] { editedComponent }));

        Assert.True(result.IsSuccess);
        var revision = Assert.Single(_repository.Revisions, r => r.JobOnRevisionId == result.Value);
        Assert.Equal(3, revision.RevisionNumber);
        Assert.Equal("notas da revisão 3", revision.GeneralNotes);

        // Unchanged revision-owned values preserved from the current revision.
        Assert.Equal("{\"sec\":\"A\"}", revision.Sections);
        Assert.Equal(3.5m, revision.DropCount);
        Assert.Equal("{\"value\":\"tipo-B\"}", revision.TypeSnapshot);
        Assert.Equal("{\"value\":\"paragem-2\"}", revision.StopSnapshot);
        Assert.Equal(12.34m, revision.WeightSnapshot);
        Assert.Equal("{\"value\":\"NNPB\"}", revision.ProcessSnapshot);

        // Changed component values present in the NEW revision.
        var storedComponent = Assert.Single(revision.Components ?? Array.Empty<JobOnComponent>());
        Assert.Equal(ComponentFamily.MP_CM, storedComponent.Family);
        Assert.Equal("CM 9999", storedComponent.ReferenceSnapshot);
        Assert.Equal("Lote 9", storedComponent.LotSnapshot);
        var field = Assert.Single(storedComponent.Fields ?? Array.Empty<JobOnComponentField>());
        Assert.Equal("peso", field.FieldKey);
        Assert.Equal("2.5", field.ValueText);

        // The previous revision remains byte/logically unchanged and readable.
        var reloaded = (await _repository.GetByIdAsync(jobOnId))!;
        var previous = reloaded.Revisions.Single(r => r.JobOnRevisionId == rich.JobOnRevisionId);
        Assert.Equal("notas preservadas", previous.GeneralNotes);
        var previousComponent = Assert.Single(previous.Components ?? Array.Empty<JobOnComponent>());
        Assert.Equal("CM 5447", previousComponent.ReferenceSnapshot);
        Assert.Equal("Lote 3", previousComponent.LotSnapshot);
    }

    [Fact]
    public async Task SaveEdit_ComponentGraph_FieldsCalRowsVerifications_Persisted()
    {
        // Test #9 — the complete component graph (components + fields + CAL rows +
        // verifications) is persisted for the new revision, re-pinned to the NEW
        // revision id (R-002: all new child rows belong to the new revision).
        var jobOnId = await CreateRascunhoAsync();
        var componentId = Guid.NewGuid();
        var component = new JobOnComponent
        {
            JobOnComponentId = componentId,
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
        var revision = _repository.Revisions.Single(r => r.JobOnRevisionId == result.Value);

        // R-002: the stored component graph is pinned to the NEW revision id.
        var storedComponent = Assert.Single(
            _repository.Components, c => c.JobOnRevisionId == revision.JobOnRevisionId);
        Assert.Equal(componentId, storedComponent.JobOnComponentId);
        Assert.Contains(_repository.Fields, f => f.JobOnComponentId == componentId);
        Assert.Contains(_repository.Rows, r => r.JobOnComponentId == componentId);
        Assert.Contains(_repository.Verifications, v => v.JobOnComponentId == componentId);
    }

    [Fact]
    public async Task SaveEdit_CMAndMF_RemainDistinctTools()
    {
        // Test #10 — CM and MF are DIFFERENT tools: both components persist under the
        // new revision with their own identity; they are never merged.
        var jobOnId = await CreateRascunhoAsync();
        var cm = new JobOnComponent { Family = ComponentFamily.MP_CM, ReferenceSnapshot = "CM-1", LotSnapshot = "1" };
        var mf = new JobOnComponent { Family = ComponentFamily.MF, ReferenceSnapshot = "MF-7", LotSnapshot = "2" };

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, new[] { cm, mf }));

        Assert.True(result.IsSuccess);
        var revision = _repository.Revisions.Single(r => r.JobOnRevisionId == result.Value);
        var components = (revision.Components ?? Array.Empty<JobOnComponent>()).ToDictionary(c => c.Family);
        Assert.Equal(2, components.Count);
        Assert.Equal("CM-1", components[ComponentFamily.MP_CM].ReferenceSnapshot);
        Assert.Equal("MF-7", components[ComponentFamily.MF].ReferenceSnapshot);
        Assert.Equal("1", components[ComponentFamily.MP_CM].LotSnapshot);
        Assert.Equal("2", components[ComponentFamily.MF].LotSnapshot);
    }

    [Fact]
    public async Task SaveEdit_VerificationStatePreserved_OldRevisionUntouched()
    {
        // Test #11 — SAME production occurrence: verification occurrences are copied
        // WITH their current state (confirmed checks are never silently reset); the
        // previous revision's verified rows are never touched. The submitted graph
        // mirrors the real client round-trip: fresh ids everywhere, values carried.
        var jobOnId = await CreateRascunhoAsync();
        var rich = await SeedRichCurrentRevision(jobOnId); // CM + confirmed check

        var sourceComponent = rich.Components!.Single();
        var copiedComponentId = Guid.NewGuid();
        var copiedComponent = sourceComponent with
        {
            JobOnComponentId = copiedComponentId,
            Fields = (sourceComponent.Fields ?? Array.Empty<JobOnComponentField>())
                .Select(f => f with { JobOnComponentFieldId = Guid.NewGuid(), JobOnComponentId = copiedComponentId })
                .ToList(),
            Rows = (sourceComponent.Rows ?? Array.Empty<JobOnComponentRow>())
                .Select(r => r with { JobOnComponentRowId = Guid.NewGuid(), JobOnComponentId = copiedComponentId })
                .ToList(),
            Verifications = (sourceComponent.Verifications ?? Array.Empty<JobOnVerificationOccurrence>())
                .Select(v => v with { JobOnVerificationOccurrenceId = Guid.NewGuid(), JobOnComponentId = copiedComponentId })
                .ToList()
        };

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, new[] { copiedComponent }));

        Assert.True(result.IsSuccess);
        var revision = _repository.Revisions.Single(r => r.JobOnRevisionId == result.Value);
        var newComponent = Assert.Single(revision.Components ?? Array.Empty<JobOnComponent>());

        // The confirmation state carries into the new revision (fresh occurrence id,
        // same status/actor/timestamps — the same-production rule).
        var newVerification = Assert.Single(newComponent.Verifications ?? Array.Empty<JobOnVerificationOccurrence>());
        Assert.Equal("confirmada", newVerification.Status);
        Assert.Equal("actor-1", newVerification.CompletedBy);
        Assert.NotNull(newVerification.CompletedAtUtc);
        Assert.NotEqual(
            sourceComponent.Verifications!.Single().JobOnVerificationOccurrenceId,
            newVerification.JobOnVerificationOccurrenceId); // fresh id, never the old row

        // The OLD revision's occurrence row is untouched and still readable; the old
        // component graph row stays pinned to the OLD revision id.
        var oldVerification = _repository.Verifications
            .Single(v => v.JobOnComponentId == sourceComponent.JobOnComponentId);
        Assert.Equal("confirmada", oldVerification.Status);
        Assert.Equal("actor-1", oldVerification.CompletedBy);
        Assert.Equal(
            rich.JobOnRevisionId,
            _repository.Components.Single(c => c.JobOnComponentId == sourceComponent.JobOnComponentId)
                .JobOnRevisionId);
    }

    [Fact]
    public async Task SaveEdit_PersistenceFailure_LeavesNoPartialRevision()
    {
        // Test #13 — when the atomic persistence fails, NOTHING remains: no new
        // revision, no graph rows, the OLD current_revision_id stays and no audit
        // fact (mirror of the atomic create/duplicate/alter-date contract).
        var jobOnId = await CreateRascunhoAsync();
        var oldCurrent = _repository.JobOns[jobOnId].CurrentRevisionId;
        var revisionCountBefore = _repository.Revisions.Count;
        var componentsBefore = _repository.Components.Count;
        _repository.FailSaveRevisionGraph = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
                jobOnId, "Notas", null, null,
                new[] { new JobOnComponent { Family = ComponentFamily.MP_CM, ReferenceSnapshot = "CM-1" } })));

        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);
        Assert.Equal(componentsBefore, _repository.Components.Count);
        Assert.Equal(oldCurrent, _repository.JobOns[jobOnId].CurrentRevisionId);
        Assert.DoesNotContain(_repository.AuditEvents, a => a.EventType == "jobon.guardar");
    }

    [Fact]
    public async Task SaveEdit_ClosedWithoutChangeReason_IsRejected_ServerSide()
    {
        // Test #15 — the closed-revision change-reason rule is enforced SERVER-side
        // (never UI-only): a fechado Job On requires change_reason, and rejection
        // leaves zero writes.
        var jobOnId = await CreateClosedAsync();
        var revisionCountBefore = _repository.Revisions.Count;

        var withoutReason = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, Array.Empty<JobOnComponent>()));

        Assert.True(withoutReason.IsFailure);
        Assert.Equal(ErrorCategory.ValidationError, withoutReason.Error.Category);
        Assert.Equal("JOBON_CHANGE_REASON_REQUIRED", withoutReason.Error.Code);
        Assert.Equal(revisionCountBefore, _repository.Revisions.Count);

        var withReason = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", "Correção após fecho", null, Array.Empty<JobOnComponent>()));

        Assert.True(withReason.IsSuccess);
        Assert.Equal(revisionCountBefore + 1, _repository.Revisions.Count);
    }

    [Fact]
    public async Task SaveEdit_EmitsAuditEvent_IdentifiesSameJobOn_PreviousAndNewRevision_AndActor()
    {
        // Test #14 — the save event goes through the existing append-only audit path
        // and identifies: the SAME Job On, the previous revision (before) and the new
        // revision (after), with the actor.
        var jobOnId = await CreateRascunhoAsync();
        var oldRevisionId = _repository.JobOns[jobOnId].CurrentRevisionId!.Value;

        var result = await _service.SaveRevisionAsync(new SaveJobOnRevisionRequest(
            jobOnId, "Notas", null, null, Array.Empty<JobOnComponent>()));

        Assert.True(result.IsSuccess);
        var audit = Assert.Single(_repository.AuditEvents, a => a.EventType == "jobon.guardar");
        Assert.Equal(jobOnId, audit.JobId);
        Assert.Equal(result.Value, audit.RevisionId);
        Assert.Equal("aaaaaaaa-0000-0000-0000-000000000001", audit.ActorId);

        using var before = JsonDocument.Parse(audit.Before!);
        Assert.Equal(oldRevisionId, before.RootElement.GetProperty("revision_id").GetGuid());

        using var after = JsonDocument.Parse(audit.After!);
        Assert.Equal(result.Value, after.RootElement.GetProperty("revision_id").GetGuid());
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
    private async Task<JobOnRevision> SeedRichCurrentRevision(Guid jobOnId)
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
        return revision;
    }

    private sealed class SaveFlowTestIdentity : ICurrentUserAccessor
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

    private sealed class SaveFlowTestClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }
}