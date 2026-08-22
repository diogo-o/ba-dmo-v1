using System.Text;
using BA.Dmo.Application.Modules.Historia;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-18 — Dapper/Npgsql implementation of the História transversal read port.
/// It projects READ-ONLY from the canonical append-only <c>audit_events</c>
/// table (N01), applying the TD-24 origin-module visibility resolved by the
/// authorization gate. It never writes to any module's domain data and never
/// creates a universal business-history table (BT-03).
///
/// Grouping paging is stable: the group keys are paged first (ordered by each
/// entity's latest event, newest first), then every event of the paged groups
/// is fetched so each expanded group is complete.
/// </summary>
public sealed class DapperHistoriaRepository : IHistoriaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperHistoriaRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    private const string RowColumns =
        """
        occurred_at_utc        AS OccurredAtUtc,
        year                   AS Year,
        actor_user_id          AS ActorUserId,
        actor_name_snapshot    AS ActorNameSnapshot,
        module_id              AS ModuleId,
        action_code            AS ActionCode,
        entity_type            AS EntityType,
        entity_id              AS EntityId,
        entity_label_snapshot  AS EntityLabelSnapshot,
        result                 AS Result,
        reason                 AS Reason,
        job_on_id              AS JobOnId,
        revision_id            AS RevisionId,
        before_summary::text   AS BeforeSummary,
        after_summary::text    AS AfterSummary
        """;

    public async Task<HistoriaQueryResult> QueryAsync(
        HistoriaFilter filter,
        IReadOnlyCollection<string> visibleModuleIds,
        bool includeAdminWithAuditView,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            // ---- Build the base WHERE (module scope + predicates).
            var (whereClause, parameters) = BuildWhere(filter, visibleModuleIds, includeAdminWithAuditView);

            // ---- Page the GROUP KEYS first (stable over entities).
            var countSql =
                $"SELECT COUNT(DISTINCT entity_type || '|' || entity_id) FROM audit_events {whereClause};";
            var total = await Db.QuerySingleOrDefaultAsync<int>(connection, countSql, parameters,
                cancellationToken: cancellationToken);

            var groupKeySql =
                $"""
                SELECT entity_type     AS EntityType,
                       entity_id       AS EntityId,
                       MAX(occurred_at_utc) AS LatestAtUtc
                FROM audit_events
                {whereClause}
                GROUP BY entity_type, entity_id
                ORDER BY LatestAtUtc DESC
                LIMIT @PageSize OFFSET @Offset;
                """;
            parameters.Add("PageSize", filter.PageSize);
            parameters.Add("Offset", (filter.Page - 1) * filter.PageSize);

            var pagedKeys = await Db.QueryAsync<PagedGroupKey>(
                connection, groupKeySql, parameters, cancellationToken: cancellationToken);

            if (!pagedKeys.Any())
                return new HistoriaQueryResult(Array.Empty<HistoriaGroupRow>(), total, filter.Page, filter.PageSize);

            // ---- Fetch ALL events of the paged group keys (complete groups).
            var eventsForGroups = await Db.QueryAsync<HistoriaEntryRow>(
                connection,
                """
                SELECT occurred_at_utc        AS OccurredAtUtc,
                       year                   AS Year,
                       actor_user_id          AS ActorUserId,
                       actor_name_snapshot    AS ActorNameSnapshot,
                       module_id              AS ModuleId,
                       action_code            AS ActionCode,
                       entity_type            AS EntityType,
                       entity_id              AS EntityId,
                       entity_label_snapshot  AS EntityLabelSnapshot,
                       result                 AS Result,
                       reason                 AS Reason,
                       job_on_id              AS JobOnId,
                       revision_id            AS RevisionId,
                       before_summary::text   AS BeforeSummary,
                       after_summary::text    AS AfterSummary
                FROM audit_events
                WHERE entity_type || '|' || entity_id = ANY(@GroupKeys)
                ORDER BY occurred_at_utc DESC;
                """,
                new { GroupKeys = pagedKeys.Select(k => $"{k.EntityType}|{k.EntityId}").ToArray() },
                cancellationToken: cancellationToken);

            var groups = new List<HistoriaGroupRow>(pagedKeys.Count());
            foreach (var key in pagedKeys)
            {
                var events = eventsForGroups
                    .Where(e => e.EntityType == key.EntityType && e.EntityId == key.EntityId)
                    .ToList();
                if (events.Count == 0)
                    continue;
                var first = events[0];
                groups.Add(new HistoriaGroupRow(
                    $"{key.EntityType}|{key.EntityId}",
                    first.EntityLabelSnapshot ?? first.EntityId,
                    first.ModuleId,
                    key.EntityType,
                    key.EntityId,
                    events));
            }

            return new HistoriaQueryResult(groups, total, filter.Page, filter.PageSize);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public async Task<IReadOnlyList<HistoriaEntryRow>> QueryFlatAsync(
        HistoriaFilter filter,
        IReadOnlyCollection<string> visibleModuleIds,
        bool includeAdminWithAuditView,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            var (whereClause, parameters) = BuildWhere(filter, visibleModuleIds, includeAdminWithAuditView);

            var sql = new StringBuilder(
                $"""
                SELECT {RowColumns}
                FROM audit_events
                {whereClause}
                ORDER BY occurred_at_utc DESC
                LIMIT @Limit OFFSET @Offset;
                """);
            parameters.Add("Limit", filter.PageSize);
            parameters.Add("Offset", (filter.Page - 1) * filter.PageSize);

            var rows = await Db.QueryAsync<HistoriaEntryRow>(
                connection, sql.ToString(), parameters, cancellationToken: cancellationToken);
            return rows.ToList();
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    private static (string Where, DynamicParameters Parameters) BuildWhere(
        HistoriaFilter filter,
        IReadOnlyCollection<string> visibleModuleIds,
        bool includeAdminWithAuditView)
    {
        var where = new StringBuilder("WHERE TRUE");
        var parameters = new DynamicParameters();

        // TD-24 origin-module visibility: only modules the identity is granted,
        // plus the admin module when the identity holds audit.view.
        var visible = new List<string>(visibleModuleIds);
        if (includeAdminWithAuditView)
            visible.Add("admin");
        if (visible.Count == 0)
            visible.Add("__none__");
        where.Append(" AND module_id = ANY(@VisibleModules)");
        parameters.Add("VisibleModules", visible.Distinct(StringComparer.Ordinal).ToArray());

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var pattern = $"%{filter.Query.Trim()}%";
            where.Append(
                """
                 AND (entity_label_snapshot ILIKE @Query
                      OR entity_id ILIKE @Query
                      OR entity_type ILIKE @Query
                      OR actor_name_snapshot ILIKE @Query
                      OR action_code ILIKE @Query)
                """);
            parameters.Add("Query", pattern);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            where.Append(" AND entity_type = @EntityType");
            parameters.Add("EntityType", filter.EntityType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            where.Append(" AND entity_id = @EntityId");
            parameters.Add("EntityId", filter.EntityId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.ModuleId))
        {
            where.Append(" AND module_id = @ModuleId");
            parameters.Add("ModuleId", filter.ModuleId);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActionCode))
        {
            where.Append(" AND action_code ILIKE @ActionCode");
            parameters.Add("ActionCode", $"%{filter.ActionCode.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.Actor))
        {
            where.Append(" AND (actor_user_id ILIKE @Actor OR actor_name_snapshot ILIKE @Actor)");
            parameters.Add("Actor", $"%{filter.Actor.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.Result))
        {
            where.Append(" AND result = @Result");
            parameters.Add("Result", filter.Result);
        }

        if (filter.FromUtc is not null)
        {
            where.Append(" AND occurred_at_utc >= @FromUtc");
            parameters.Add("FromUtc", filter.FromUtc);
        }

        if (filter.ToUtc is not null)
        {
            where.Append(" AND occurred_at_utc <= @ToUtc");
            parameters.Add("ToUtc", filter.ToUtc);
        }

        return (where.ToString(), parameters);
    }

    private sealed record PagedGroupKey(
        string EntityType,
        string EntityId,
        DateTimeOffset LatestAtUtc);

    private static async Task DisposeAsync(System.Data.IDbConnection connection)
    {
        if (connection is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            connection.Dispose();
    }
}