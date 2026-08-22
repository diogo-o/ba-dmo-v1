namespace BA.Dmo.Infrastructure.Persistence.Migrations;

/// <summary>
/// Port between the migration runner and the database (Plan-V3 PV-04,
/// GLM-DATA-12). The runner owns the policy; the gateway owns the transport:
/// - the tracking table (schema_migrations) is ensured before any run;
/// - every migration script is executed WHOLE — the gateway never splits,
///   parses or rewrites the script (no split(';'), no custom SQL parser,
///   no EF Core Migrations);
/// - a migration is recorded only AFTER successful execution.
/// </summary>
public interface IMigrationScriptGateway : IAsyncDisposable
{
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates schema_migrations when absent (idempotent).</summary>
    Task EnsureTrackingTableAsync(CancellationToken cancellationToken = default);

    /// <summary>All applied migrations, keyed by version.</summary>
    Task<IReadOnlyDictionary<string, AppliedMigration>> GetAppliedAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the ENTIRE script to PostgreSQL and executes it atomically.
    /// Any failure rolls back and propagates; nothing is recorded.
    /// </summary>
    Task ExecuteScriptAsync(string wholeScript, CancellationToken cancellationToken = default);

    /// <summary>Records a successfully executed migration.</summary>
    Task RecordAppliedAsync(AppliedMigration migration, int executionTimeMs,
        CancellationToken cancellationToken = default);
}
