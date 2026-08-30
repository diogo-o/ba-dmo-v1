using BA.Dmo.Domain.Modules.JobOn;

namespace BA.Dmo.Application.Modules.JobOn;

/// <summary>
/// Job On read/write port (N05). All CRUD operations go through this interface.
/// Implementation uses Dapper against job_on* tables (Infrastructure layer).
/// </summary>
public interface IJobOnRepository
{
    /// <summary>Create a new Job On (rascunho).</summary>
    Task<Guid> CreateAsync(Domain.Modules.JobOn.JobOn jobOn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically creates a new Job On (header) WITH its initial immutable
    /// revision (revision 1), advances <c>job_on.current_revision_id</c> and
    /// records the <c>jobon.criar</c> audit event — all in ONE database
    /// transaction. On any failure nothing persists: no header-only or partial
    /// Job On can remain. Returns the newly created <c>job_on</c> id.
    /// </summary>
    Task<Guid> CreateAtomicallyAsync(
        Domain.Modules.JobOn.JobOn jobOn,
        JobOnRevision initialRevision,
        string actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Get by primary key.</summary>
    Task<Domain.Modules.JobOn.JobOn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Get all active (planeado/em fabrico) for resolve(line, at).</summary>
    Task<IReadOnlyList<Domain.Modules.JobOn.JobOn>> GetActiveAsync(string machineCode, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>Get by production code.</summary>
    Task<Domain.Modules.JobOn.JobOn?> GetByProductionCodeAsync(string productionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically persists the complete Domain-owned lifecycle state and its
    /// transition audit event. Status and terminal timestamps are never written
    /// independently.
    /// </summary>
    Task TransitionLifecycleAsync(
        Domain.Modules.JobOn.JobOn jobOn,
        string actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Insert a new revision (TD-18 immutable snapshots).</summary>
    Task InsertRevisionAsync(JobOnRevision revision, CancellationToken cancellationToken = default);

    /// <summary>Get revisions for a Job On (ordered by number).</summary>
    Task<IReadOnlyList<Domain.Modules.JobOn.JobOnRevision>> GetRevisionsAsync(Guid jobOnId, CancellationToken cancellationToken = default);

    /// <summary>Insert component(s) for a revision.</summary>
    Task InsertComponentsAsync(IEnumerable<Domain.Modules.JobOn.JobOnComponent> components, CancellationToken cancellationToken = default);

    /// <summary>Insert field(s) for a component.</summary>
    Task InsertFieldsAsync(IEnumerable<Domain.Modules.JobOn.JobOnComponentField> fields, CancellationToken cancellationToken = default);

    /// <summary>Insert row(s) for a CAL component.</summary>
    Task InsertRowsAsync(IEnumerable<JobOnComponentRow> rows, CancellationToken cancellationToken = default);

    /// <summary>Insert verification occurrence(s).</summary>
    Task InsertVerificationsAsync(IEnumerable<Domain.Modules.JobOn.JobOnVerificationOccurrence> verifications, CancellationToken cancellationToken = default);

    /// <summary>Update verification status.</summary>
    Task UpdateVerificationStatusAsync(Guid occurrenceId, string status, string? completedBy, DateTime? completedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Get current revision_id for a Job On.</summary>
    Task<Guid?> GetCurrentRevisionIdAsync(Guid jobOnId, CancellationToken cancellationToken = default);

    /// <summary>Update current_revision_id link.</summary>
    Task UpdateCurrentRevisionAsync(Guid jobOnId, Guid revisionId, CancellationToken cancellationToken = default);

    /// <summary>Insert audit event (append-only).</summary>
    Task InsertAuditEventAsync(Guid jobId, Guid? revisionId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomic image mutation: inserts a new revision, updates current_revision_id,
    /// and records the audit event in ONE database transaction (TD-23).
    /// All three writes succeed or none do — no partial state.
    /// </summary>
    Task InsertImageMutationAsync(
        JobOnRevision newRevision,
        Guid jobOnId,
        string eventType,
        string? beforeImageAssetId,
        string? afterImageAssetId,
        string actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically persists a NEW immutable revision with its complete child graph
    /// (components + fields + CAL rows + verification occurrences), advances
    /// <c>job_on.current_revision_id</c>, and records the audit event — all in ONE
    /// database transaction. A current revision can never become partially persisted.
    /// </summary>
    Task SaveRevisionGraphAsync(
        JobOnRevision revision,
        string eventType,
        string actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically duplicates a Job On (header + copied revision graph + current-revision
    /// link + audit) in ONE database transaction. On failure nothing persists — no partially
    /// duplicated Job On remains. Returns the newly created <c>job_on</c> id.
    /// </summary>
    Task<Guid> DuplicateAtomicallyAsync(
        Domain.Modules.JobOn.JobOn newJobOn,
        JobOnRevision revision,
        Guid sourceJobOnId,
        string actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Get historical productions grouped by reference.</summary>
    Task<IReadOnlyList<HistoricalProductionSummary>> GetHistoricalProductionsAsync(string? referenceFilter, string? machineFilter, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Historical production summary for two-level history navigation (GLM-JOB-09).
/// </summary>
public sealed record HistoricalProductionSummary(
    Guid JobOnId,
    string ProductionCode,
    string ReferenceCode,
    string MachineCode,
    DateTimeOffset? PlannedStartAt,
    DateTimeOffset? PlannedEndAt,
    int CurrentRevisionNumber,
    int TotalRevisionCount,
    JobOnLifecycleState LifecycleState
);
