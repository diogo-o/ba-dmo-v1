using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// Dapper/Npgsql implementation of the catalog mirror port (table created by
/// U-02 N02_catalog.sql). Explicit parameterized SQL with enumerated columns;
/// writes run inside ONE unit of work (GLM-DATA-05). The mirror is display
/// data for Administration — this repository never influences authorization.
/// </summary>
public sealed class DapperModuleCatalogMirrorRepository : IModuleCatalogMirrorRepository
{
    private const string SelectAllSql =
        """
        SELECT module_id, display_name, display_order, active, synced_at_utc
        FROM module_catalog_mirror
        ORDER BY display_order, module_id;
        """;

    private const string UpsertSql =
        """
        INSERT INTO module_catalog_mirror (module_id, display_name, display_order, active, synced_at_utc)
        VALUES (@ModuleId, @DisplayName, @DisplayOrder, @Active, @SyncedAtUtc)
        ON CONFLICT (module_id) DO UPDATE
        SET display_name = @DisplayName,
            display_order = @DisplayOrder,
            active = @Active,
            synced_at_utc = @SyncedAtUtc;
        """;

    private const string DeleteSql =
        "DELETE FROM module_catalog_mirror WHERE module_id = @ModuleId;";

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperModuleCatalogMirrorRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<ModuleCatalogMirrorRow>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            return await Db.QueryAsync<ModuleCatalogMirrorRow>(
                connection, SelectAllSql, cancellationToken: cancellationToken);
        }
        finally
        {
            if (connection is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                connection.Dispose();
        }
    }

    public async Task UpsertAllAsync(
        IReadOnlyList<ModuleCatalogMirrorRow> rows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        // Atomic replacement inside ONE transaction: remove rows that are no
        // longer in the canonical set, then upsert the current set.
        await DapperUnitOfWork.RunAsync<int>(_connectionFactory, async (connection, transaction, ct) =>
        {
            var canonicalIds = rows.Select(r => r.ModuleId).ToHashSet(StringComparer.Ordinal);

            var existing = await Db.QueryAsync<ModuleCatalogMirrorRow>(
                connection, SelectAllSql, transaction: transaction, cancellationToken: ct);
            foreach (var row in existing.Where(r => !canonicalIds.Contains(r.ModuleId)))
            {
                await Db.ExecuteAsync(
                    connection, DeleteSql, new { row.ModuleId },
                    transaction: transaction, cancellationToken: ct);
            }

            foreach (var row in rows)
            {
                await Db.ExecuteAsync(
                    connection, UpsertSql, row,
                    transaction: transaction, cancellationToken: ct);
            }

            return rows.Count;
        }, cancellationToken);
    }
}
