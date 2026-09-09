using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL regression proof for the Job On revision snapshot round
/// trip (DapperJobOnRepository). Typed revision values (type/stop/process)
/// are plain text in the domain and are persisted by the repository as JSON
/// documents { value: "…" } because the snapshot columns are jsonb — a bare
/// string would be rejected by the ::jsonb cast (22P02). The mapper unwraps
/// them back to the plain string the domain compares (e.g. Peso process
/// NNPB/PS). Valid-but-other JSON shapes pass through unchanged.
///
/// BA_DMO_TEST_DATABASE must identify an isolated, already-migrated test
/// database. Every row uses a unique suffix.
/// </summary>
public sealed class JobOnRevisionSnapshotPostgresTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task SaveRevisionGraph_RoundTripsPlainTextTypedSnapshots()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedJobOnAsync();
        var repository = new DapperJobOnRepository(CreateFactory("jobon-snapshot-write-" + context.Suffix));
        var revisionId = Guid.NewGuid();
        var revision = new JobOnRevision
        {
            JobOnRevisionId = revisionId,
            JobOnId = context.JobOnId,
            RevisionNumber = 1,
            ProductionSnapshot = System.Text.Json.JsonSerializer.Serialize(new { production_code = "202610" }),
            ReferenceSnapshot = System.Text.Json.JsonSerializer.Serialize(new { article_reference = "5810T188" }),
            MachineSnapshot = System.Text.Json.JsonSerializer.Serialize(new { machine_code = "B2" }),
            DatesSnapshot = System.Text.Json.JsonSerializer.Serialize(new { start_at = (DateTimeOffset?)null, end_at = (DateTimeOffset?)null }),
            Sections = "{}",
            TypeSnapshot = "Tipo A",
            StopSnapshot = "Automática",
            ProcessSnapshot = "NNPB",
            GeneralNotes = null,
            ChangeReason = null,
            SavedBy = context.ActorId,
            SavedAtUtc = DateTime.UtcNow
        };

        await repository.SaveRevisionGraphAsync(
            revision, "jobon.guardar", context.ActorId, "{}", "{}");

        var jobOn = await repository.GetByIdAsync(context.JobOnId);
        Assert.NotNull(jobOn);
        Assert.NotNull(jobOn.CurrentRevision);
        Assert.Equal(revisionId, jobOn.CurrentRevision!.JobOnRevisionId);
        Assert.Equal("Tipo A", jobOn.CurrentRevision.TypeSnapshot);
        Assert.Equal("Automática", jobOn.CurrentRevision.StopSnapshot);
        Assert.Equal("NNPB", jobOn.CurrentRevision.ProcessSnapshot);
    }

    [Fact]
    public async Task GetByIdAsync_UnwrapsValueObjectSnapshots()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync(wrapped: true);
        var repository = new DapperJobOnRepository(CreateFactory("jobon-snapshot-value-" + context.Suffix));

        var jobOn = await repository.GetByIdAsync(context.JobOnId);

        Assert.NotNull(jobOn);
        Assert.NotNull(jobOn.CurrentRevision);
        Assert.Equal("NNPB", jobOn.CurrentRevision!.ProcessSnapshot);
        Assert.Equal("Sopro NV", jobOn.CurrentRevision.TypeSnapshot);
        Assert.Equal("Automática", jobOn.CurrentRevision.StopSnapshot);
    }

    [Fact]
    public async Task GetByIdAsync_LegacyPlainTextSnapshots_PassThroughUnchanged()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync(wrapped: false);
        var repository = new DapperJobOnRepository(CreateFactory("jobon-snapshot-legacy-" + context.Suffix));

        var jobOn = await repository.GetByIdAsync(context.JobOnId);

        Assert.NotNull(jobOn);
        Assert.NotNull(jobOn.CurrentRevision);
        Assert.Equal("\"LegacyText\"", jobOn.CurrentRevision!.ProcessSnapshot);
        Assert.Equal("\"NNPB\"", jobOn.CurrentRevision.TypeSnapshot);
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] JobOnRevisionSnapshotPostgresTests: BA_DMO_TEST_DATABASE not set — " +
            "real PostgreSQL revision-snapshot assertions were not executed.");
        return true;
    }

    private static DbConnectionFactory CreateFactory(string applicationName)
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString!)
        {
            ApplicationName = applicationName
        };
        return new DbConnectionFactory(builder.ConnectionString);
    }

    private static async Task<TestContext> SeedJobOnAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var jobOnId = Guid.NewGuid();
        var productionCode = "2026" + Guid.NewGuid().ToString("N")[..6];

        await ExecuteAsync(
            """
            INSERT INTO access_templates (template_id, name)
            VALUES ('tpl-' || @Suffix, 'Snapshot test ' || @Suffix);

            INSERT INTO internal_users
                (actor_id, auth_user_id, template_id, display_name, active)
            VALUES
                ('snap-actor-' || @Suffix, @AuthUserId, 'tpl-' || @Suffix,
                 'Snapshot test ' || @Suffix, TRUE);

            INSERT INTO job_on
                (job_on_id, production_code, machine_code, status)
            VALUES
                (@JobOnId, @ProductionCode, 'B2', 'rascunho');
            """,
            new NpgsqlParameter("JobOnId", jobOnId),
            new NpgsqlParameter("ProductionCode", productionCode),
            new NpgsqlParameter("AuthUserId", Guid.NewGuid()),
            new NpgsqlParameter("Suffix", suffix));

        return new TestContext(suffix, jobOnId, "snap-actor-" + suffix);
    }

    private static async Task<TestContext> SeedContextAsync(bool wrapped)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var jobOnId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var productionCode = "2026" + Guid.NewGuid().ToString("N")[..6];
        var typeJson = wrapped ? "'{\"value\":\"Sopro NV\"}'::jsonb" : "'\"NNPB\"'::jsonb";
        var stopJson = wrapped ? "'{\"value\":\"Automática\"}'::jsonb" : "NULL";
        var processJson = wrapped ? "'{\"value\":\"NNPB\"}'::jsonb" : "'\"LegacyText\"'::jsonb";

        await ExecuteAsync(
            $"""
            INSERT INTO job_on
                (job_on_id, production_code, machine_code, status)
            VALUES
                (@JobOnId, @ProductionCode, 'B2', 'rascunho');

            INSERT INTO job_on_revision
                (job_on_revision_id, job_on_id, revision_number, sections,
                 type_snapshot, stop_snapshot, process_snapshot)
            VALUES
                (@RevisionId, @JobOnId, 1, @Sections::jsonb,
                 {typeJson}, {stopJson}, {processJson});

            UPDATE job_on SET current_revision_id = @RevisionId
            WHERE job_on_id = @JobOnId;
            """,
            new NpgsqlParameter("JobOnId", jobOnId),
            new NpgsqlParameter("RevisionId", revisionId),
            new NpgsqlParameter("ProductionCode", productionCode),
            new NpgsqlParameter("Sections", "{}"));

        return new TestContext(suffix, jobOnId, "snap-read-actor-" + suffix);
    }

    private static async Task ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record TestContext(string Suffix, Guid JobOnId, string ActorId);
}