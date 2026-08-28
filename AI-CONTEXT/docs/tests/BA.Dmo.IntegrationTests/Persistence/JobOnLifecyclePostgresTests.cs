using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL lifecycle, transaction rollback, and canceled-identity proofs
/// for JOBON-01. BA_DMO_TEST_DATABASE must identify an isolated test database.
/// Every row uses a unique jobon01 identifier; no existing row is selected or mutated.
/// </summary>
public sealed class JobOnLifecyclePostgresTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task TransitionLifecycleAsync_ActiveState_HasNoTerminalTimestamps()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedAsync();
        var repository = CreateRepository();
        var jobOn = await repository.GetByIdAsync(context.JobOnId);
        Assert.NotNull(jobOn);
        jobOn!.TransitionTo(JobOnLifecycleState.Planeado);

        await repository.TransitionLifecycleAsync(jobOn, context.ActorId);

        var row = await ReadLifecycleAsync(context.JobOnId);
        Assert.Equal("planeado", row.Status);
        Assert.Null(row.ClosedAtUtc);
        Assert.Null(row.CanceledAtUtc);
        Assert.Equal(1, await CountTransitionAuditsAsync(context.JobOnId));
    }

    [Fact]
    public async Task TransitionLifecycleAsync_ClosedState_PersistsClosedTimestamp()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedAsync();
        var repository = CreateRepository();
        var jobOn = await repository.GetByIdAsync(context.JobOnId);
        Assert.NotNull(jobOn);
        jobOn!.TransitionTo(JobOnLifecycleState.Planeado);
        jobOn.TransitionTo(JobOnLifecycleState.EmFabrico);
        var now = UtcNowAtPostgresPrecision();
        jobOn.Close(now);

        await repository.TransitionLifecycleAsync(jobOn, context.ActorId);

        var row = await ReadLifecycleAsync(context.JobOnId);
        Assert.Equal("fechado", row.Status);
        Assert.Equal(now, row.ClosedAtUtc);
        Assert.Null(row.CanceledAtUtc);
        Assert.Equal(1, await CountTransitionAuditsAsync(context.JobOnId));
    }

    [Fact]
    public async Task TransitionLifecycleAsync_CanceledState_PersistsCancellationFacts()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedAsync();
        var repository = CreateRepository();
        var jobOn = await repository.GetByIdAsync(context.JobOnId);
        Assert.NotNull(jobOn);
        var now = UtcNowAtPostgresPrecision();
        jobOn!.Cancel("JOBON-01 cancellation", context.ActorId, now);

        await repository.TransitionLifecycleAsync(jobOn, context.ActorId);

        var row = await ReadLifecycleAsync(context.JobOnId);
        Assert.Equal("cancelado", row.Status);
        Assert.Null(row.ClosedAtUtc);
        Assert.Equal(now, row.CanceledAtUtc);
        Assert.Equal(context.ActorId, row.CanceledBy);
        Assert.Equal("JOBON-01 cancellation", row.CancelReason);
        Assert.Equal(1, await CountTransitionAuditsAsync(context.JobOnId));
    }

    [Fact]
    public async Task TransitionLifecycleAsync_AuditFailure_RollsBackLifecycleUpdate()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedAsync();
        var repository = CreateRepository();
        var jobOn = await repository.GetByIdAsync(context.JobOnId);
        Assert.NotNull(jobOn);
        jobOn!.TransitionTo(JobOnLifecycleState.Planeado);
        jobOn.TransitionTo(JobOnLifecycleState.EmFabrico);
        jobOn.Close(DateTime.UtcNow);

        await Assert.ThrowsAsync<PostgresException>(() =>
            repository.TransitionLifecycleAsync(jobOn, "jobon01-missing-" + Guid.NewGuid().ToString("N")));

        var row = await ReadLifecycleAsync(context.JobOnId);
        Assert.Equal("rascunho", row.Status);
        Assert.Null(row.ClosedAtUtc);
        Assert.Null(row.CanceledAtUtc);
        Assert.Equal(0, await CountTransitionAuditsAsync(context.JobOnId));
    }

    [Fact]
    public async Task CanceledTimestamp_MakesExistingIdentityRuleReachable()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedAsync();
        var repository = CreateRepository();
        var original = await repository.GetByIdAsync(context.JobOnId);
        Assert.NotNull(original);
        original!.Cancel("identity reuse proof", context.ActorId, DateTime.UtcNow);
        await repository.TransitionLifecycleAsync(original, context.ActorId);

        var replacement = NewJobOn(context.ProductionCode, context.MachineCode);
        var replacementId = await repository.CreateAsync(replacement);
        Assert.NotEqual(Guid.Empty, replacementId);

        var duplicate = NewJobOn(context.ProductionCode, context.MachineCode);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => repository.CreateAsync(duplicate));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] JobOnLifecyclePostgresTests: BA_DMO_TEST_DATABASE not set — " +
            "real PostgreSQL lifecycle/rollback/identity assertions were not executed.");
        return true;
    }

    private static DapperJobOnRepository CreateRepository() =>
        new(new DbConnectionFactory(ConnectionString!));

    private static JobOnEntity NewJobOn(string productionCode, string machineCode) =>
        new(productionCode, machineCode, DateTimeOffset.UtcNow, null, Array.Empty<JobOnRevision>());

    private static DateTime UtcNowAtPostgresPrecision()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - (now.Ticks % 10), DateTimeKind.Utc);
    }

    private static async Task<TestContext> SeedAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var actorId = "jobon01-" + suffix;
        var templateId = "jobon01-tpl-" + suffix;
        var productionCode = "JOBON01-" + suffix;
        var machineCode = "JOBON01-M-" + suffix;

        await using (var connection = new NpgsqlConnection(ConnectionString!))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO access_templates (template_id, name, modules, active)
                VALUES (@TemplateId, @TemplateName, '["jobon"]'::jsonb, TRUE);
                INSERT INTO internal_users
                    (actor_id, auth_user_id, template_id, display_name, active)
                VALUES (@ActorId, @AuthUserId, @TemplateId, @DisplayName, TRUE);
                """, connection);
            command.Parameters.AddWithValue("TemplateId", templateId);
            command.Parameters.AddWithValue("TemplateName", "JOBON-01 " + suffix);
            command.Parameters.AddWithValue("ActorId", actorId);
            command.Parameters.AddWithValue("AuthUserId", Guid.NewGuid());
            command.Parameters.AddWithValue("DisplayName", "JOBON-01 " + suffix);
            await command.ExecuteNonQueryAsync();
        }

        var repository = CreateRepository();
        var jobOnId = await repository.CreateAsync(NewJobOn(productionCode, machineCode));
        return new TestContext(jobOnId, actorId, productionCode, machineCode);
    }

    private static async Task<LifecycleRow> ReadLifecycleAsync(Guid jobOnId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT status, closed_at_utc, canceled_at_utc, canceled_by, cancel_reason
            FROM job_on WHERE job_on_id = @Id;
            """, connection);
        command.Parameters.AddWithValue("Id", jobOnId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new LifecycleRow(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetDateTime(1),
            reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    private static async Task<int> CountTransitionAuditsAsync(Guid jobOnId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*) FROM job_on_audit_event
            WHERE job_on_id = @Id AND event_type = 'jobon.transicao';
            """, connection);
        command.Parameters.AddWithValue("Id", jobOnId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed record TestContext(
        Guid JobOnId, string ActorId, string ProductionCode, string MachineCode);

    private sealed record LifecycleRow(
        string Status, DateTime? ClosedAtUtc, DateTime? CanceledAtUtc,
        string? CanceledBy, string? CancelReason);
}
