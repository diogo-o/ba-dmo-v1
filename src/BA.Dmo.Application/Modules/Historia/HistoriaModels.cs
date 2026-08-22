namespace BA.Dmo.Application.Modules.Historia;

/// <summary>
/// U-18 — História transversal read models (modules/11, §3./§4, contract §13
/// History Entry). These are read-only projections over the canonical
/// append-only <c>audit_events</c> table. They preserve the module boundary
/// (<c>ModuleId</c>), the exact actor attribution recorded at execution time
/// (snapshot) and the UTC instant — and are never reinterpreted from current
/// mutable state.
/// </summary>

/// <summary>
/// Transversal filters of `/historia` (GLM-HIST-03): entity free text
/// (tool/reference/lot/production), module (origin), action type, actor,
/// result and period. Page sizes are the canonical 20/40/60.
/// </summary>
public sealed record HistoriaFilter(
    string? Query,
    string? EntityType,
    string? EntityId,
    string? ModuleId,
    string? ActionCode,
    string? Actor,
    string? Result,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Page,
    int PageSize)
{
    public static bool IsValidPageSize(int pageSize) =>
        HistoriaModuleCatalog.CanonicalPageSizes.Contains(pageSize);
}

/// <summary>One audit fact row surfaced by the História transversal view.</summary>
public sealed record HistoriaEntryRow(
    DateTimeOffset OccurredAtUtc,
    int Year,
    string? ActorUserId,
    string? ActorNameSnapshot,
    string ModuleId,
    string ActionCode,
    string EntityType,
    string EntityId,
    string? EntityLabelSnapshot,
    string Result,
    string? Reason,
    Guid? JobOnId,
    Guid? RevisionId,
    string? BeforeSummary,
    string? AfterSummary);

/// <summary>A History Entry row with a stable grouping key over entity + module.</summary>
public sealed record HistoriaGroupRow(
    string GroupKey,
    string EntityLabel,
    string ModuleId,
    string EntityType,
    string EntityId,
    IReadOnlyList<HistoriaEntryRow> Events)
{
    /// <summary>Latest event instant, used to order groups chronologically.</summary>
    public DateTimeOffset LatestAtUtc => Events.Count == 0
        ? DateTimeOffset.MinValue
        : Events.Max(e => e.OccurredAtUtc);
}

public sealed record HistoriaQueryResult(
    IReadOnlyList<HistoriaGroupRow> Groups,
    int TotalCount,
    int Page,
    int PageSize);