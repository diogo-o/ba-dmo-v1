namespace BA.Dmo.Infrastructure.Persistence.Migrations;

/// <summary>
/// Errors of the migration subsystem. All of them are explicit and diagnostic:
/// a failed migration never continues silently (Plan-V3 PV-04, 09_TEST §10.2).
/// </summary>
public sealed class MigrationDiscoveryException(string message) : Exception(message);

/// <summary>
/// A migration version/filename is already recorded in schema_migrations with a
/// DIFFERENT SHA-256. The run fails explicitly; nothing is re-applied silently.
/// </summary>
public sealed class MigrationChecksumMismatchException(
    string version,
    string fileName,
    string recordedSha256,
    string currentSha256)
    : Exception(
        $"Migration '{fileName}' (version '{version}') was already applied with SHA-256 " +
        $"'{recordedSha256}' but the current file has SHA-256 '{currentSha256}'. " +
        "Applied migrations are immutable (forward-only family).")
{
    public string Version { get; } = version;
    public string FileName { get; } = fileName;
    public string RecordedSha256 { get; } = recordedSha256;
    public string CurrentSha256 { get; } = currentSha256;
}

/// <summary>
/// A migration script failed while executing. The failed script is NOT recorded
/// in schema_migrations and no later migration runs (09_TEST §10.1/§10.2).
/// </summary>
public sealed class MigrationExecutionException(
    string version,
    string fileName,
    Exception innerException)
    : Exception(
        $"Migration '{fileName}' (version '{version}') failed to execute. " +
        "It was not recorded and no later migration was applied.",
        innerException)
{
    public string Version { get; } = version;
    public string FileName { get; } = fileName;
}
