using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Domain.Modules.JobOn;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// In-memory fake of the Job On persistence port (confined to tests/*).
/// Tracks created ids, revisions, components, verifications and audit events
/// so use-case tests can assert persistence behavior without a live DB.
/// </summary>
public sealed class FakeJobOnRepository : IJobOnRepository
{
    public Dictionary<Guid, JobOnEntity> JobOns { get; } = new();

    public List<JobOnRevision> Revisions { get; } = [];

    public List<JobOnComponent> Components { get; } = [];

    public List<JobOnComponentField> Fields { get; } = [];

    public List<JobOnComponentRow> Rows { get; } = [];

    public List<JobOnVerificationOccurrence> Verifications { get; } = [];

    public List<(Guid JobId, Guid? RevisionId, string EventType, string? Before, string? After, string ActorId)> AuditEvents { get; } = [];

    public List<JobOnLifecycleState> LifecycleUpdates { get; } = [];

    public List<(Guid JobOnId, Guid RevisionId)> CurrentRevisionUpdates { get; } = [];

    public List<(Guid OccurrenceId, string Status, string? CompletedBy, DateTime? CompletedAt)> VerificationUpdates { get; } = [];

    /// <summary>When true, create/duplicate throw JobOnIdentityDuplicateException (audit JA-03 mapping test).</summary>
    public bool FailIdentityDuplicate { get; set; }

    /// <summary>When true, the atomic create throws a raw persistence failure (no-partial-write test).</summary>
    public bool FailCreateAtomically { get; set; }

    /// <summary>When true, the atomic duplicate throws a raw persistence failure (no-partial-write test).</summary>
    public bool FailDuplicateAtomically { get; set; }

    /// <summary>When true, the atomic alter-date throws a raw persistence failure (no-partial-write test).</summary>
    public bool FailAlterDatesAtomically { get; set; }

    public Task<Guid> CreateAsync(JobOnEntity jobOn, CancellationToken cancellationToken = default)
    {
        if (FailIdentityDuplicate)
            throw new JobOnIdentityDuplicateException(
                "Já existe um Job On não cancelado com esta produção e máquina.");
        var id = Guid.NewGuid();
        jobOn.SetId(id);
        JobOns[id] = jobOn;
        return Task.FromResult(id);
    }

    public Task<Guid> CreateAtomicallyAsync(
        JobOnEntity jobOn,
        JobOnRevision initialRevision,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (FailIdentityDuplicate)
            throw new JobOnIdentityDuplicateException(
                "Já existe um Job On não cancelado com esta produção e máquina.");
        if (FailCreateAtomically)
            throw new InvalidOperationException("persistence unavailable");

        var newId = Guid.NewGuid();
        jobOn.SetId(newId);
        JobOns[newId] = jobOn;

        // Mirror the real repository's re-pin rule (R-002): the initial revision
        // (and its children) belong to the newly created job_on id.
        var pinnedRevision = initialRevision with
        {
            JobOnId = newId,
            Components = (initialRevision.Components ?? Array.Empty<JobOnComponent>())
                .Select(c => c with { JobOnRevisionId = initialRevision.JobOnRevisionId })
                .ToList()
        };
        Revisions.Add(pinnedRevision);
        PersistRevisionGraph(pinnedRevision);
        // Mirror the real repository: current_revision_id is advanced on the
        // stored aggregate (the DB UPDATE target of the atomic create).
        jobOn.SaveRevision(pinnedRevision);
        CurrentRevisionUpdates.Add((newId, pinnedRevision.JobOnRevisionId));
        AuditEvents.Add((newId, pinnedRevision.JobOnRevisionId, "jobon.criar", null, null, actorId));

        return Task.FromResult(newId);
    }

    public Task<JobOnEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        JobOns.TryGetValue(id, out var stored);
        return Task.FromResult(stored is null ? null : Reconstruct(stored));
    }

    public Task<IReadOnlyList<JobOnEntity>> GetActiveAsync(
        string machineCode, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var rows = JobOns.Values
            .Where(j => j.MachineCode == machineCode && j.IsActive)
            .Select(Reconstruct)
            .ToList();
        return Task.FromResult<IReadOnlyList<JobOnEntity>>(rows);
    }

    public Task<JobOnEntity?> GetByProductionCodeAsync(string productionCode, CancellationToken cancellationToken = default)
    {
        var stored = JobOns.Values.FirstOrDefault(j => j.ProductionCode == productionCode);
        return Task.FromResult(stored is null ? null : Reconstruct(stored));
    }

    public Task TransitionLifecycleAsync(JobOnEntity jobOn, string actorId, CancellationToken cancellationToken = default)
    {
        LifecycleUpdates.Add(jobOn.LifecycleState);
        JobOns[jobOn.Id] = jobOn;
        AuditEvents.Add((
            jobOn.Id, null, "jobon.transicao", null,
            jobOn.LifecycleState.ToString(), actorId));
        return Task.CompletedTask;
    }

    public Task InsertRevisionAsync(JobOnRevision revision, CancellationToken cancellationToken = default)
    {
        Revisions.Add(revision);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<JobOnRevision>> GetRevisionsAsync(Guid jobOnId, CancellationToken cancellationToken = default)
    {
        var rows = Revisions.Where(r => r.JobOnId == jobOnId).OrderBy(r => r.RevisionNumber).ToList();
        return Task.FromResult<IReadOnlyList<JobOnRevision>>(rows);
    }

    public Task InsertComponentsAsync(IEnumerable<JobOnComponent> components, CancellationToken cancellationToken = default)
    {
        Components.AddRange(components);
        return Task.CompletedTask;
    }

    public Task InsertFieldsAsync(IEnumerable<JobOnComponentField> fields, CancellationToken cancellationToken = default)
    {
        Fields.AddRange(fields);
        return Task.CompletedTask;
    }

    public Task InsertRowsAsync(IEnumerable<JobOnComponentRow> rows, CancellationToken cancellationToken = default)
    {
        Rows.AddRange(rows);
        return Task.CompletedTask;
    }

    public Task InsertVerificationsAsync(IEnumerable<JobOnVerificationOccurrence> verifications, CancellationToken cancellationToken = default)
    {
        Verifications.AddRange(verifications);
        return Task.CompletedTask;
    }

    public Task UpdateVerificationStatusAsync(Guid occurrenceId, string status, string? completedBy, DateTime? completedAtUtc, CancellationToken cancellationToken = default)
    {
        VerificationUpdates.Add((occurrenceId, status, completedBy, completedAtUtc));
        return Task.CompletedTask;
    }

    public Task<Guid?> GetCurrentRevisionIdAsync(Guid jobOnId, CancellationToken cancellationToken = default)
    {
        JobOns.TryGetValue(jobOnId, out var jobOn);
        return Task.FromResult(jobOn?.CurrentRevisionId);
    }

    public Task UpdateCurrentRevisionAsync(Guid jobOnId, Guid revisionId, CancellationToken cancellationToken = default)
    {
        CurrentRevisionUpdates.Add((jobOnId, revisionId));
        // Mirror the real repository (UPDATE job_on SET current_revision_id):
        // the stored aggregate's current link advances too.
        if (JobOns.TryGetValue(jobOnId, out var jobOn))
        {
            var revision = Revisions.FirstOrDefault(r => r.JobOnRevisionId == revisionId);
            if (revision is not null) jobOn.SaveRevision(revision);
        }
        return Task.CompletedTask;
    }

    public Task InsertAuditEventAsync(Guid jobId, Guid? revisionId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken cancellationToken = default)
    {
        AuditEvents.Add((jobId, revisionId, eventType, beforeSnapshot, afterSnapshot, actorId));
        return Task.CompletedTask;
    }

    public Task InsertImageMutationAsync(
        JobOnRevision newRevision,
        Guid jobOnId,
        string eventType,
        string? beforeImageAssetId,
        string? afterImageAssetId,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        Revisions.Add(newRevision);
        CurrentRevisionUpdates.Add((jobOnId, newRevision.JobOnRevisionId));
        AuditEvents.Add((jobOnId, newRevision.JobOnRevisionId, eventType, beforeImageAssetId, afterImageAssetId, actorId));
        return Task.CompletedTask;
    }

    public Task SaveRevisionGraphAsync(
        JobOnRevision revision,
        string eventType,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        Revisions.Add(revision);
        PersistRevisionGraph(revision);
        CurrentRevisionUpdates.Add((revision.JobOnId, revision.JobOnRevisionId));
        // Mirror the real repository (current_revision_id advances atomically).
        if (JobOns.TryGetValue(revision.JobOnId, out var jobOn)) jobOn.SaveRevision(revision);
        AuditEvents.Add((revision.JobOnId, revision.JobOnRevisionId, eventType, null, null, actorId));
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
        if (FailAlterDatesAtomically)
            throw new InvalidOperationException("persistence unavailable");

        // Mirror the real repository: the header planned dates (single calendar
        // source), the new revision + children, the current-revision link advance
        // and the audit fact — all-or-nothing.
        if (JobOns.TryGetValue(jobOnId, out var jobOn))
        {
            jobOn.AlterDates(plannedStartAt, plannedEndAt);
        }
        Revisions.Add(newRevision);
        PersistRevisionGraph(newRevision);
        CurrentRevisionUpdates.Add((jobOnId, newRevision.JobOnRevisionId));
        if (JobOns.TryGetValue(jobOnId, out var stored)) stored.SaveRevision(newRevision);
        AuditEvents.Add((jobOnId, newRevision.JobOnRevisionId, eventType, beforeSnapshot, afterSnapshot, actorId));
        return Task.CompletedTask;
    }

    public Task<Guid> DuplicateAtomicallyAsync(
        JobOnEntity newJobOn,
        JobOnRevision revision,
        Guid sourceJobOnId,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (FailIdentityDuplicate)
            throw new JobOnIdentityDuplicateException(
                "Já existe um Job On não cancelado com esta produção e máquina.");
        if (FailDuplicateAtomically)
            throw new InvalidOperationException("persistence unavailable");
        var newId = Guid.NewGuid();

        // Construct the duplicated header via its public constructor, then hydrate the
        // readonly fields (copied_from, article_reference, timestamps, lifecycle) through
        // FromRow, mirroring the real repository — these fields have private setters and
        // cannot be assigned through an object initializer.
        dynamic row = new System.Dynamic.ExpandoObject();
        row.job_on_id = newId;
        row.current_revision_id = (Guid?)null;
        row.copied_from_job_on_id = sourceJobOnId;
        row.article_reference_id = newJobOn.ArticleReferenceId;
        row.created_at_utc = DateTime.UtcNow;
        row.status = JobOnLifecycleStateCodec.ToStorage(newJobOn.LifecycleState);
        row.closed_at_utc = newJobOn.ClosedAtUtc;
        row.canceled_at_utc = newJobOn.CancelledAtUtc;
        row.canceled_by = newJobOn.CancelledBy;
        row.cancel_reason = newJobOn.CancelReason;
        row.production_folder = newJobOn.ProductionFolder;

        var header = new JobOnEntity(
            newJobOn.ProductionCode,
            newJobOn.MachineCode,
            newJobOn.PlannedStartAt,
            newJobOn.PlannedEndAt,
            Array.Empty<JobOnRevision>());
        header.FromRow(row);
        JobOns[newId] = header;

        var pinnedRevision = revision with { JobOnId = newId };
        Revisions.Add(pinnedRevision);
        PersistRevisionGraph(pinnedRevision);
        CurrentRevisionUpdates.Add((newId, pinnedRevision.JobOnRevisionId));
        AuditEvents.Add((newId, null, "jobon.duplicar", null, sourceJobOnId.ToString(), actorId));

        return Task.FromResult(newId);
    }

    /// <summary>Persists a revision's component/field/CAL-row/verification graph (atomic fake).</summary>
    private void PersistRevisionGraph(JobOnRevision revision)
    {
        foreach (var component in revision.Components ?? Array.Empty<JobOnComponent>())
        {
            var storedComponent = component with { JobOnRevisionId = revision.JobOnRevisionId };
            Components.Add(storedComponent);
            Fields.AddRange(storedComponent.Fields ?? Array.Empty<JobOnComponentField>());
            Rows.AddRange(storedComponent.Rows ?? Array.Empty<JobOnComponentRow>());
            Verifications.AddRange(storedComponent.Verifications ?? Array.Empty<JobOnVerificationOccurrence>());
        }
    }

    public Task<IReadOnlyList<HistoricalProductionSummary>> GetHistoricalProductionsAsync(
        string? referenceFilter, string? machineFilter, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<HistoricalProductionSummary>>(Array.Empty<HistoricalProductionSummary>());
    }

    private JobOnEntity Reconstruct(JobOnEntity stored)
    {
        var revisions = Revisions
            .Where(r => r.JobOnId == stored.Id)
            .OrderBy(r => r.RevisionNumber)
            .ToList();
        var currentRevisionId = stored.CurrentRevisionId
            ?? revisions.OrderByDescending(r => r.RevisionNumber).FirstOrDefault()?.JobOnRevisionId;

        var jobOn = new JobOnEntity(
            stored.ProductionCode,
            stored.MachineCode,
            stored.PlannedStartAt,
            stored.PlannedEndAt,
            revisions.Select(HydrateRevision).ToList());

        dynamic row = new System.Dynamic.ExpandoObject();
        row.job_on_id = stored.Id;
        row.current_revision_id = currentRevisionId;
        row.copied_from_job_on_id = stored.CopiedFromJobOnId;
        row.article_reference_id = stored.ArticleReferenceId;
        row.created_at_utc = stored.CreatedAtUtc;
        row.status = JobOnLifecycleStateCodec.ToStorage(stored.LifecycleState);
        row.closed_at_utc = stored.ClosedAtUtc;
        row.canceled_at_utc = stored.CancelledAtUtc;
        row.canceled_by = stored.CancelledBy;
        row.cancel_reason = stored.CancelReason;
        row.production_folder = stored.ProductionFolder;
        jobOn.FromRow(row);
        return jobOn;
    }

    /// <summary>Re-attaches each revision's components (with fields/rows/verifications) and
    /// flattened verifications, mirroring the real repository's aggregate hydration.</summary>
    private JobOnRevision HydrateRevision(JobOnRevision revision)
    {
        var components = Components
            .Where(c => c.JobOnRevisionId == revision.JobOnRevisionId)
            .OrderBy(c => c.DisplayOrder)
            .Select(c =>
            {
                var verifications = Verifications
                    .Where(v => v.JobOnComponentId == c.JobOnComponentId)
                    .ToList();
                return c with
                {
                    Fields = Fields.Where(f => f.JobOnComponentId == c.JobOnComponentId).ToList(),
                    Rows = Rows.Where(r => r.JobOnComponentId == c.JobOnComponentId).ToList(),
                    Verifications = verifications
                };
            })
            .ToList();

        var allVerifications = components
            .SelectMany(c => c.Verifications ?? Array.Empty<JobOnVerificationOccurrence>())
            .ToList();

        return revision with
        {
            Components = components,
            Verifications = allVerifications
        };
    }
}
