using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Infrastructure.Persistence.Migrations;

namespace BA.Dmo.IntegrationTests.Migrations;

/// <summary>
/// U-02 runner behavior tests (Plan-V3 PV-04, GLM-DATA-12, 09_TEST §10.1–10.2):
/// unapplied execution, record-only-after-success, same-checksum skip,
/// checksum-mismatch failure, failed SQL not recorded, no continuation after
/// failure, whole-script execution without any statement splitting.
/// No database is required: the gateway double reproduces the transport contract.
/// </summary>
public sealed class MigrationRunnerTests : IDisposable
{
    private const string MultiStatementScript =
        "-- two statements and a DO block, full of semicolons\n" +
        "CREATE TABLE a (id int);\n" +
        "INSERT INTO a VALUES (1);\n" +
        "DO $$ BEGIN NULL; END $$;\n";

    private readonly string _directory = Directory.CreateTempSubdirectory("ba_dmo_runner_").FullName;
    private readonly FakeMigrationGateway _gateway = new();
    private readonly MigrationRunner _runner;

    public MigrationRunnerTests()
    {
        _runner = new MigrationRunner(_gateway, new FixedClock(
            new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.Zero)));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private MigrationFile WriteMigration(string fileName, string content)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, content);
        return new MigrationFile(fileName[..3], fileName, path);
    }

    [Fact]
    public async Task UnappliedMigration_IsExecutedWhole_AndRecordedAfterSuccess()
    {
        var migration = WriteMigration("N01_a.sql", MultiStatementScript);

        var report = await _runner.RunAsync([migration]);

        // Whole script, exactly once, byte-for-byte (no splitting, no rewriting).
        var executed = Assert.Single(_gateway.ExecutedScripts);
        Assert.Equal(MultiStatementScript, executed);

        // Record only after success, with the file's SHA-256.
        var record = Assert.Single(report.Applied);
        Assert.Equal("N01", record.Version);
        var stored = Assert.Single(_gateway.Records);
        Assert.Equal("N01_a.sql", stored.FileName);
        Assert.Equal(MigrationChecksum.ComputeSha256File(migration.FullPath), stored.Sha256);
        Assert.True(_gateway.EnsureTrackingTableCalled);
        Assert.Empty(report.Skipped);
    }

    [Fact]
    public async Task AppliedMigrationWithSameChecksum_IsSkipped_NotReExecuted()
    {
        var migration = WriteMigration("N01_a.sql", MultiStatementScript);
        _gateway.SeedApplied(new AppliedMigration(
            "N01", "N01_a.sql", MigrationChecksum.ComputeSha256File(migration.FullPath),
            DateTimeOffset.UtcNow));

        var report = await _runner.RunAsync([migration]);

        Assert.Empty(_gateway.ExecutedScripts);
        Assert.Empty(report.Applied);
        Assert.Empty(_gateway.Records);
        var skipped = Assert.Single(report.Skipped);
        Assert.Equal("N01_a.sql", skipped.FileName);
    }

    [Fact]
    public async Task AppliedMigrationWithDifferentChecksum_FailsExplicitly()
    {
        var migration = WriteMigration("N01_a.sql", MultiStatementScript);
        _gateway.SeedApplied(new AppliedMigration(
            "N01", "N01_a.sql", new string('0', 64), DateTimeOffset.UtcNow));

        var ex = await Assert.ThrowsAsync<MigrationChecksumMismatchException>(
            () => _runner.RunAsync([migration]));

        Assert.Equal("N01", ex.Version);
        Assert.Equal("N01_a.sql", ex.FileName);
        Assert.Empty(_gateway.ExecutedScripts);   // never re-applied
        Assert.Empty(_gateway.Records);           // never re-recorded
    }

    [Fact]
    public async Task FailedScript_IsNotRecorded_AndStopsTheRun()
    {
        var first = WriteMigration("N01_a.sql", MultiStatementScript);
        var second = WriteMigration("N02_b.sql", "CREATE TABLE broken (;");
        var third = WriteMigration("N03_c.sql", "CREATE TABLE c (id int);");
        _gateway.FailOnScriptContaining = "broken";

        var ex = await Assert.ThrowsAsync<MigrationExecutionException>(
            () => _runner.RunAsync([first, second, third]));

        Assert.Equal("N02", ex.Version);
        // N01 applied before the failure; the failed N02 is NOT recorded and
        // N03 never executes (no later migration after a failure).
        Assert.Single(_gateway.ExecutedScripts);
        Assert.Equal([first.FileName], _gateway.Records.Select(r => r.FileName).ToArray());
        Assert.Equal("N02_b.sql", ex.FileName);
    }

    [Fact]
    public async Task Migrations_ExecuteInCanonicalOrder_AndAllRecorded()
    {
        var third = WriteMigration("N03_c.sql", "CREATE TABLE c (id int);");
        var first = WriteMigration("N01_a.sql", "CREATE TABLE a (id int);");
        var second = WriteMigration("N02_b.sql", "CREATE TABLE b (id int);");

        var report = await _runner.RunAsync([first, second, third]);

        Assert.Equal(
            ["CREATE TABLE a (id int);", "CREATE TABLE b (id int);", "CREATE TABLE c (id int);"],
            _gateway.ExecutedScripts);
        Assert.Equal(
            ["N01_a.sql", "N02_b.sql", "N03_c.sql"],
            report.Applied.Select(m => m.FileName).ToArray());
        Assert.Equal(3, _gateway.Records.Count);
    }

    [Fact]
    public async Task EmptyFamily_SucceedsWithNothingToDo()
    {
        var report = await _runner.RunAsync([]);

        Assert.True(report.NothingToDo);
        Assert.Empty(_gateway.ExecutedScripts);
        Assert.True(_gateway.EnsureTrackingTableCalled);
    }

    [Fact]
    public async Task ScriptsWithSemicolonsInsideStrings_AreNeverSplit()
    {
        // A script whose literal data contains semicolons must arrive intact:
        // proof that the runner performs no statement splitting/parsing.
        var script =
            "INSERT INTO notes (text) VALUES ('a;b;c');\n" +
            "INSERT INTO notes (text) VALUES ('x;y');\n";
        var migration = WriteMigration("N01_a.sql", script);

        await _runner.RunAsync([migration]);

        var executed = Assert.Single(_gateway.ExecutedScripts);
        Assert.Equal(script, executed);
    }

    private sealed class FixedClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }
}
