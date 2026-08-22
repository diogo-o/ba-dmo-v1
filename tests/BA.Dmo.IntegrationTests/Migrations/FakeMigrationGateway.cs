using BA.Dmo.Infrastructure.Persistence.Migrations;

namespace BA.Dmo.IntegrationTests.Migrations;

/// <summary>
/// In-memory test double for the migration gateway (confined to tests/*,
/// GLM-ARCH-18). It reproduces the transport contract faithfully:
/// whole-script execution, record-only-after-success, and explicit failure
/// propagation — without requiring any database (no live Supabase in U-02).
/// </summary>
internal sealed class FakeMigrationGateway : IMigrationScriptGateway
{
    private readonly Dictionary<string, AppliedMigration> _applied = new(StringComparer.Ordinal);

    public List<string> ExecutedScripts { get; } = [];

    public List<AppliedMigration> Records { get; } = [];

    public bool EnsureTrackingTableCalled { get; private set; }

    /// <summary>When set, ExecuteScriptAsync throws for scripts containing it.</summary>
    public string? FailOnScriptContaining { get; set; }

    /// <summary>Pre-existing schema_migrations rows (e.g. from a previous run).</summary>
    public void SeedApplied(AppliedMigration migration) => _applied[migration.Version] = migration;

    public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task EnsureTrackingTableAsync(CancellationToken cancellationToken = default)
    {
        EnsureTrackingTableCalled = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, AppliedMigration>> GetAppliedAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, AppliedMigration>>(
            new Dictionary<string, AppliedMigration>(_applied, StringComparer.Ordinal));

    public Task ExecuteScriptAsync(string wholeScript, CancellationToken cancellationToken = default)
    {
        if (FailOnScriptContaining is not null &&
            wholeScript.Contains(FailOnScriptContaining, StringComparison.Ordinal))
            throw new InvalidOperationException("Simulated database failure.");

        ExecutedScripts.Add(wholeScript);
        return Task.CompletedTask;
    }

    public Task RecordAppliedAsync(AppliedMigration migration, int executionTimeMs,
        CancellationToken cancellationToken = default)
    {
        _applied[migration.Version] = migration;
        Records.Add(migration);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
