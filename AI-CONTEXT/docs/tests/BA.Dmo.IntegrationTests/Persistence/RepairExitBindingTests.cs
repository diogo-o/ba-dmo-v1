using System.Text.Json;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL binding proofs for the external-repair exit persistence
/// (REPAIR-01 / GLM-RE): the repairer snapshot must persist as jsonb
/// (Dapper would otherwise fail with 42804 "jsonb but expression is of type
/// text") and list filters must accept DateOnly range parameters (Dapper
/// cannot bind System.DateOnly members without conversion). The disposable
/// database is supplied through BA_DMO_TEST_DATABASE, following
/// RepairAtomicityTests.
/// </summary>
public sealed class RepairExitBindingTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task CreateExit_WithRepairerSnapshot_RoundTripsJsonbAndMapsSnapshot()
    {
        if (SkipIfNoDatabase()) return;
        var (actorId, templateId, repairerId) = await SeedActorAndRepairerAsync();
        var exit = RepairExit.Create(
            RepairType.MF,
            new RepairerSnapshot(repairerId, "REPAIR BINDING SNAP", Active: true),
            new DateOnly(2026, 9, 15),
            new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero),
            actorId);
        Assert.True(exit.IsSuccess);

        try
        {
            var repository = new DapperRepairRepository(new DbConnectionFactory(ConnectionString!));
            var snapshotJson = JsonSerializer.Serialize(
                new RepairerSnapshot(repairerId, "REPAIR BINDING SNAP", Active: true));

            // The jsonb binding regression: without a ::jsonb cast the insert
            // fails with 42804 (jsonb vs text). The repository call returns the
            // exit id only after a successful insert+commit.
            var exitId = await repository.CreateExitAsync(
                exit.Value, new RepairerSnapshot(repairerId, "REPAIR BINDING SNAP", Active: true), snapshotJson);

            var stored = await ReadSnapshotJsonAsync(exitId);
            // Postgres jsonb normalizes whitespace/key order on storage; compare
            // semantic content, not the raw representation.
            using (var expected = JsonDocument.Parse(snapshotJson))
            using (var actual = JsonDocument.Parse(stored!))
            {
                Assert.Equal(
                    expected.RootElement.GetProperty("RepairerId").GetGuid(),
                    actual.RootElement.GetProperty("RepairerId").GetGuid());
                Assert.Equal(
                    expected.RootElement.GetProperty("Name").GetString(),
                    actual.RootElement.GetProperty("Name").GetString());
            }

            var hydrated = await repository.GetExitByIdAsync(exitId);
            Assert.NotNull(hydrated);
            Assert.Equal(RepairExitStatus.Preparacao, hydrated!.Status);
            Assert.Equal("REPAIR BINDING SNAP", hydrated.RepairerSnapshot?.Name);
            Assert.Equal(new DateOnly(2026, 9, 15), hydrated.PlannedDate);

            // The DateOnly filter regression: ListExitsAsync must accept the
            // range parameters without throwing NotSupportedException.
            var listed = await repository.ListExitsAsync(
                RepairType.MF, RepairExitStatus.Preparacao,
                new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1));
            Assert.Contains(listed, e => e.RepairExitId == exitId);
            var outside = await repository.ListExitsAsync(
                RepairType.MF, null, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1));
            Assert.DoesNotContain(outside, e => e.RepairExitId == exitId);
        }
        finally
        {
            await CleanupAsync(actorId, templateId, repairerId);
        }
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] RepairExitBindingTests: BA_DMO_TEST_DATABASE not set - " +
            "real PostgreSQL binding assertions were not executed.");
        return true;
    }

    private static async Task<(string ActorId, string TemplateId, Guid RepairerId)> SeedActorAndRepairerAsync()
    {
        var actorId = "repair-binding-" + Guid.NewGuid().ToString("N")[..8];
        var templateId = "repair-binding-tpl-" + Guid.NewGuid().ToString("N")[..8];
        var repairerId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO access_templates (template_id, name, modules, active)
            VALUES (@TemplateId, 'Repair binding test', '["reparacao_externa"]'::jsonb, TRUE);
            INSERT INTO internal_users
                (actor_id, auth_user_id, template_id, display_name, active)
            VALUES
                (@ActorId, @AuthUserId, @TemplateId, 'Repair Binding Test', TRUE);
            INSERT INTO repairers (repairer_id, name, active)
            VALUES (@RepairerId, 'REPAIR BINDING SNAP', TRUE);
            """,
            new NpgsqlParameter("TemplateId", templateId),
            new NpgsqlParameter("ActorId", actorId),
            new NpgsqlParameter("AuthUserId", Guid.NewGuid()),
            new NpgsqlParameter("RepairerId", repairerId));
        return (actorId, templateId, repairerId);
    }

    private static async Task<string?> ReadSnapshotJsonAsync(Guid exitId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT repairer_snapshot::text FROM repair_exits WHERE repair_exit_id = @Id;
            """, connection);
        command.Parameters.AddWithValue("Id", exitId);
        var value = await command.ExecuteScalarAsync();
        return value as string;
    }

    private static async Task CleanupAsync(string actorId, string templateId, Guid repairerId)
    {
        await ExecuteAsync(
            """
            DELETE FROM repair_exit_items
            WHERE repair_exit_id IN (SELECT repair_exit_id FROM repair_exits WHERE created_by = @ActorId);
            DELETE FROM repair_exits WHERE created_by = @ActorId;
            DELETE FROM repairers WHERE repairer_id = @RepairerId;
            DELETE FROM internal_users WHERE actor_id = @ActorId;
            DELETE FROM access_templates WHERE template_id = @TemplateId;
            """,
            new NpgsqlParameter("ActorId", actorId),
            new NpgsqlParameter("TemplateId", templateId),
            new NpgsqlParameter("RepairerId", repairerId));
    }

    private static async Task ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }
}