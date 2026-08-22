using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Infrastructure.Persistence.Migrations;

/// <summary>
/// Outcome of one migration run.
/// </summary>
public sealed record MigrationRunReport(
    IReadOnlyList<MigrationFile> Applied,
    IReadOnlyList<MigrationFile> Skipped)
{
    public bool NothingToDo => Applied.Count == 0;
}

/// <summary>
/// Custom Npgsql full-script migration runner (Plan-V3 PV-04, GLM-DATA-12).
/// For every discovered migration, in canonical order:
///   1. read the ENTIRE .sql file;
///   2. compute SHA-256 over its raw content;
///   3. compare with schema_migrations:
///      - never applied            → execute whole, record only after success;
///      - applied, same checksum   → skip safely;
///      - applied, other checksum  → FAIL explicitly (no silent continuation);
///   4. a failed script is never recorded and stops the run: no later
///      migration executes (09_TEST §10.2).
/// The runner contains no SQL parsing and never splits scripts.
/// </summary>
public sealed class MigrationRunner
{
    private readonly IMigrationScriptGateway _gateway;
    private readonly IClock _clock;

    public MigrationRunner(IMigrationScriptGateway gateway, IClock clock)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<MigrationRunReport> RunAsync(
        IReadOnlyList<MigrationFile> migrations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migrations);

        await _gateway.EnsureTrackingTableAsync(cancellationToken);
        var applied = await _gateway.GetAppliedAsync(cancellationToken);

        var newlyApplied = new List<MigrationFile>();
        var skipped = new List<MigrationFile>();

        foreach (var migration in migrations)
        {
            var sha256 = MigrationChecksum.ComputeSha256File(migration.FullPath);

            if (applied.TryGetValue(migration.Version, out var recorded))
            {
                if (string.Equals(recorded.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
                {
                    skipped.Add(migration);
                    continue;
                }

                throw new MigrationChecksumMismatchException(
                    migration.Version, migration.FileName, recorded.Sha256, sha256);
            }

            // Whole-script execution: the file content is sent exactly as-is.
            var wholeScript = File.ReadAllText(migration.FullPath);
            var startedAt = _clock.UtcNow;
            try
            {
                await _gateway.ExecuteScriptAsync(wholeScript, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new MigrationExecutionException(
                    migration.Version, migration.FileName, ex);
            }

            var executionTimeMs = (int)Math.Max(
                0, (_clock.UtcNow - startedAt).TotalMilliseconds);

            // Record ONLY after successful execution (GLM-DATA-12.6).
            var record = new AppliedMigration(
                migration.Version, migration.FileName, sha256, _clock.UtcNow);
            await _gateway.RecordAppliedAsync(record, executionTimeMs, cancellationToken);

            applied = new Dictionary<string, AppliedMigration>(applied)
            {
                [migration.Version] = record
            };
            newlyApplied.Add(migration);
        }

        return new MigrationRunReport(newlyApplied, skipped);
    }
}
