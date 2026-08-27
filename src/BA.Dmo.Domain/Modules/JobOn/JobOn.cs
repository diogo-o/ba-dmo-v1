using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.JobOn;

/// <summary>
/// Job On aggregate root (N05, TD-18). Represents an operational production sheet.
/// Lifecycle states: rascunho → planeado → em_fabrico → fechado OR cancelado.
/// Revisions are immutable snapshots created on save (TD-18). Tool attribution via
/// job_on_component rows pins historical tool state for downstream consumers.
/// </summary>
public sealed class JobOn
{
    /// <summary>Primary key (created on insert).</summary>
    public Guid Id { get; private set; }

    /// <summary>For EF/Dapper mapping.</summary>
    internal void SetId(Guid id) => Id = id;

    /// <summary>Production code (e.g., 202603).</summary>
    public string ProductionCode { get; private set; } = null!;

    /// <summary>Machine/line code.</summary>
    public string MachineCode { get; private set; } = null!;

    /// <summary>Planned start date/time - single calendar source.</summary>
    public DateTimeOffset? PlannedStartAt { get; private set; }

    /// <summary>Planned end date/time - single calendar source.</summary>
    public DateTimeOffset? PlannedEndAt { get; private set; }

    /// <summary>Lifecycle state per N05 check constraint.</summary>
    public JobOnLifecycleState LifecycleState { get; private set; }

    /// <summary>Current revision link.
    public Guid? CurrentRevisionId { get; private set; }

    /// <summary>For Dapper mapping from database row.
    internal void FromRow(dynamic row)
    {
        Id = row.job_on_id;
        CurrentRevisionId = row.current_revision_id;
        CopiedFromJobOnId = row.copied_from_job_on_id;
        ArticleReferenceId = row.article_reference_id;
        CreatedAtUtc = row.created_at_utc;
        LifecycleState = JobOnLifecycleStateCodec.Parse((string)row.status);
        ClosedAtUtc = row.closed_at_utc;
        CancelledAtUtc = row.canceled_at_utc;
        CancelledBy = row.canceled_by;
        CancelReason = row.cancel_reason;
        ProductionFolder = row.production_folder;
    }

    /// <summary>Optional reference to original Job On if this is a duplication.</summary>
    public Guid? CopiedFromJobOnId { get; private set; }

    /// <summary>Optional article reference ID (logical link only).</summary>
    public Guid? ArticleReferenceId { get; private set; }

    /// <summary>
    /// Optional production folder identifier (N13). Links this Job On to a
    /// physical directory under the global main_documents_output_root setting.
    /// Used by downstream consumers (PDF, Pegamentos) to locate assets.
    /// </summary>
    public string? ProductionFolder { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Optional close timestamp when transitioning to fechado.</summary>
    public DateTime? ClosedAtUtc { get; private set; }

    /// <summary>Optional cancellation timestamp when transitioning to cancelado.</summary>
    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>Optional user who cancelled.</summary>
    public string? CancelledBy { get; private set; }

    /// <summary>Optional cancellation reason.</summary>
    public string? CancelReason { get; private set; }

    // Revisions collection loaded separately
    private readonly IReadOnlyList<JobOnRevision> _revisions;

    private JobOn()
    {
        _revisions = Array.Empty<JobOnRevision>();
    }

    public JobOn(
        string productionCode,
        string machineCode,
        DateTimeOffset? plannedStartAt,
        DateTimeOffset? plannedEndAt,
        IReadOnlyList<JobOnRevision> revisions)
    {
        if (string.IsNullOrWhiteSpace(productionCode))
            throw new ArgumentException("Production code must not be empty.", nameof(productionCode));
        if (string.IsNullOrWhiteSpace(machineCode))
            throw new ArgumentException("Machine code must not be empty.", nameof(machineCode));

        ProductionCode = productionCode.Trim();
        MachineCode = machineCode.Trim();
        PlannedStartAt = plannedStartAt;
        PlannedEndAt = plannedEndAt;
        _revisions = revisions ?? Array.Empty<JobOnRevision>();
        LifecycleState = JobOnLifecycleState.Rascunho;
    }

    public JobOnRevision? CurrentRevision => 
        _revisions.FirstOrDefault(r => r.JobOnRevisionId == CurrentRevisionId);

    public int RevisionCount => _revisions.Count;

    public IReadOnlyList<JobOnRevision> Revisions => new List<JobOnRevision>(_revisions).AsReadOnly();

    /// <summary>Create new revision with snapshot data (TD-18).</summary>
    public void SaveRevision(JobOnRevision revision)
    {
        CurrentRevisionId = revision.JobOnRevisionId;
    }

    /// <summary>Duplicate from another Job On instance.</summary>
    public static JobOn DuplicateFrom(
        JobOn source,
        string productionCode,
        string machineCode,
        DateTimeOffset? plannedStartAt,
        DateTimeOffset? plannedEndAt,
        IEnumerable<JobOnRevision> newRevisions)
    {
        var duplicated = new JobOn(
            productionCode,
            machineCode,
            plannedStartAt,
            plannedEndAt,
            newRevisions.ToList())
        {
            Id = source.Id, // Same aggregate root but different instance
            CreatedAtUtc = DateTime.UtcNow,
            CopiedFromJobOnId = source.Id
        };

        return duplicated;
    }

    /// <summary>Transition lifecycle state with validation (TD-27).</summary>
    public void TransitionTo(JobOnLifecycleState newState)
    {
        var fromState = LifecycleState;
        
        // Validate transitions
        switch (newState)
        {
            case JobOnLifecycleState.Rascunho:
                throw new Exception("A Job On cannot transition back to rascunho.");

            case JobOnLifecycleState.Planeado when fromState != JobOnLifecycleState.Rascunho:
                throw new Exception("Only rascunho can transition to planeado.");
            
            case JobOnLifecycleState.EmFabrico when fromState != JobOnLifecycleState.Planeado:
                throw new Exception("Only planeado can transition to em fabrico.");
            
            case JobOnLifecycleState.Fechado:
                throw new Exception("Use Close to transition to fechado.");

            case JobOnLifecycleState.Cancelado:
                throw new Exception("Use Cancel to transition to cancelado.");
        }

        LifecycleState = newState;
    }

    /// <summary>Close production (transition to fechado) with timestamp.</summary>
    public void Close(DateTime now)
    {
        if (LifecycleState != JobOnLifecycleState.EmFabrico)
            throw new Exception("Only em_fabrico can be closed.");
        
        ClosedAtUtc = now;
        LifecycleState = JobOnLifecycleState.Fechado;
    }

    /// <summary>Cancel production with reason.</summary>
    public void Cancel(string cancelReason, string actorId, DateTime now)
    {
        if (LifecycleState is not (JobOnLifecycleState.Rascunho or JobOnLifecycleState.Planeado))
            throw new Exception("Only rascunho/planeado can be cancelled.");
        
        CancelReason = cancelReason;
        CancelledBy = actorId;
        CancelledAtUtc = now;
        LifecycleState = JobOnLifecycleState.Cancelado;
    }

    /// <summary>Check if active (planeado or em fabrico).</summary>
    public bool IsActive => LifecycleState is JobOnLifecycleState.Planeado or JobOnLifecycleState.EmFabrico;
}
