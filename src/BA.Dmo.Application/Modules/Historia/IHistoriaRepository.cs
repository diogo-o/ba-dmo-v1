namespace BA.Dmo.Application.Modules.Historia;

/// <summary>
/// U-18 — Persistence port of the História transversal read (modules/11,
/// TD-19/GLM-HIST-04). READ-ONLY: it only projects rows from the canonical
/// append-only <c>audit_events</c> table; it never writes to any module's
/// domain data and never creates a universal business-history table (BT-03).
///
/// <c>visibleModuleIds</c> is the TD-24 resolution result produced by the
/// authorization gate (origin modules the current identity is granted), so the
/// repository itself only ever returns events the user may see.
/// </summary>
public interface IHistoriaRepository
{
    /// <summary>
    /// Paged transversal query grouped by entity. Ordering is stable:
    /// groups are ordered by their latest event instant, newest first;
    /// events inside a group are chronological, newest first.
    /// </summary>
    Task<HistoriaQueryResult> QueryAsync(
        HistoriaFilter filter,
        IReadOnlyCollection<string> visibleModuleIds,
        bool includeAdminWithAuditView,
        CancellationToken cancellationToken = default);

    /// <summary>Paged flat query (no grouping) used by the detail/JSON path.</summary>
    Task<IReadOnlyList<HistoriaEntryRow>> QueryFlatAsync(
        HistoriaFilter filter,
        IReadOnlyCollection<string> visibleModuleIds,
        bool includeAdminWithAuditView,
        CancellationToken cancellationToken = default);
}