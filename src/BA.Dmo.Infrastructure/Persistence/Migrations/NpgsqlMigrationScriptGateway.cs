using Npgsql;

namespace BA.Dmo.Infrastructure.Persistence.Migrations;

/// <summary>
/// Npgsql implementation of the migration gateway (Plan-V3 PV-04, GLM-DATA-12).
/// Each migration script is sent WHOLE to PostgreSQL as a single command
/// inside a transaction — the gateway performs no statement splitting, no
/// custom SQL parsing and no rewriting (semicolons are data, not boundaries).
/// </summary>
public sealed class NpgsqlMigrationScriptGateway : IMigrationScriptGateway
{
    private const string EnsureTrackingTableSql =
        """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            version           text        PRIMARY KEY,
            filename          text        NOT NULL,
            sha256            text        NOT NULL,
            applied_at        timestamptz NOT NULL DEFAULT now(),
            execution_time_ms integer     NULL
        );
        """;

    private readonly string _connectionString;
    private NpgsqlConnection? _connection;

    public NpgsqlMigrationScriptGateway(string connectionString)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        _connectionString = connectionString;
    }

    private NpgsqlConnection Connection =>
        _connection ?? throw new InvalidOperationException(
            "Migration gateway is not open. Call OpenAsync first.");

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        _connection ??= new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync(cancellationToken);
    }

    public async Task EnsureTrackingTableAsync(CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand(EnsureTrackingTableSql, Connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, AppliedMigration>> GetAppliedAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql =
            "SELECT version, filename, sha256, applied_at FROM schema_migrations;";

        var applied = new Dictionary<string, AppliedMigration>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand(sql, Connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var record = new AppliedMigration(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3));
            applied[record.Version] = record;
        }

        return applied;
    }

    public async Task ExecuteScriptAsync(string wholeScript, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(wholeScript);

        // One transaction per migration: success commits, failure rolls back and
        // propagates. The FULL script text is executed as one command — no
        // splitting, no parsing (GLM-DATA-12).
        await using var transaction = await Connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand(wholeScript, Connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task RecordAppliedAsync(AppliedMigration migration, int executionTimeMs,
        CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            INSERT INTO schema_migrations (version, filename, sha256, applied_at, execution_time_ms)
            VALUES (@version, @filename, @sha256, @applied_at, @execution_time_ms);
            """;

        await using var command = new NpgsqlCommand(sql, Connection);
        command.Parameters.AddWithValue("@version", migration.Version);
        command.Parameters.AddWithValue("@filename", migration.FileName);
        command.Parameters.AddWithValue("@sha256", migration.Sha256);
        command.Parameters.AddWithValue("@applied_at", migration.AppliedAtUtc);
        command.Parameters.AddWithValue("@execution_time_ms", executionTimeMs);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
