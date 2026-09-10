using System.Text.Json;
using BA.Dmo.Application.Modules.ReparacaoExterna;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;
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
            // All-null filters: the parameters arrive untyped (42P08 without the
            // ::date casts) - the list/histórico surfaces call this shape.
            var allNull = await repository.ListExitsAsync(null, null, null, null);
            Assert.Contains(allNull, e => e.RepairExitId == exitId);
        }
        finally
        {
            await CleanupAsync(actorId, templateId, repairerId);
        }
    }

    [Fact]
    public async Task PickupAndReturn_Facts_RoundTripOutAndInTimestamps()
    {
        if (SkipIfNoDatabase()) return;
        var (actorId, templateId, repairerId) = await SeedActorAndRepairerAsync();
        var pieceId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var reference = "CM-BIND-" + Guid.NewGuid().ToString("N")[..8];
        await ExecuteAsync(
            """
            INSERT INTO tool_references (tool_reference_id, tool_type, ref_code)
            VALUES (@ReferenceId, 'CM', @Reference);
            INSERT INTO tool_lotes (tool_lote_id, tool_reference_id, lote)
            VALUES (@LotId, @ReferenceId, 'BIND-LOT');
            INSERT INTO physical_pieces (physical_piece_id, tool_lote_id, sequence, number)
            VALUES (@PieceId, @LotId, 1, 'BIND-N');
            """,
            new NpgsqlParameter("ReferenceId", referenceId),
            new NpgsqlParameter("Reference", reference),
            new NpgsqlParameter("LotId", lotId),
            new NpgsqlParameter("PieceId", pieceId));

        try
        {
            var factory = new DbConnectionFactory(ConnectionString!);
            var repository = new DapperRepairRepository(factory);

            var exit = RepairExit.Create(RepairType.CM, null, null, new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero), actorId);
            Assert.True(exit.IsSuccess);

            Guid exitId;
            Guid itemId = Guid.Empty;
            await using (var uow = await new DapperRepairUnitOfWorkFactory(factory).BeginAsync())
            {
                exitId = await repository.CreateExitAsync(uow, exit.Value, null, null);
                var itemResult = RepairExitItem.CreateCmMf(exitId, pieceId, "BIND-N", RepairType.CM);
                Assert.True(itemResult.IsSuccess);
                itemId = itemResult.Value.RepairExitItemId;
                var created = await repository.AddItemAsync(uow, itemResult.Value);
                // Confirm pickup + return in the same unit of work, mirroring the service.
                itemResult.Value.ConfirmPickedOut(new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero), actorId);
                await repository.ConfirmItemPickedAsync(uow, itemResult.Value, default);
                itemResult.Value.ConfirmReturned(new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero), actorId);
                await repository.ConfirmItemReturnedAsync(uow, itemResult.Value, default);
                await uow.CommitAsync();
            }

            var items = await repository.GetExitItemsAsync(exitId);
            var item = Assert.Single(items, i => i.RepairExitItemId == itemId);
            // The timestamptz readback regression: `as DateTimeOffset?` returned
            // null for Npgsql's DateTime values; the facts must surface verbatim.
            Assert.NotNull(item.OutAtUtc);
            Assert.NotNull(item.InAtUtc);
            Assert.Equal(new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero), item.OutAtUtc!.Value.ToUniversalTime());
            Assert.Equal(new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero), item.InAtUtc!.Value.ToUniversalTime());
            Assert.Equal(actorId, item.OutOperatorId);
            Assert.Equal(actorId, item.InOperatorId);
            Assert.Equal("devolvido", item.Status);
        }
        finally
        {
            await ExecuteAsync(
                """
                DELETE FROM repair_exit_items
                WHERE repair_exit_id IN (SELECT repair_exit_id FROM repair_exits WHERE created_by = @ActorId);
                DELETE FROM repair_exits WHERE created_by = @ActorId;
                DELETE FROM physical_pieces WHERE physical_piece_id = @PieceId;
                DELETE FROM tool_lotes WHERE tool_lote_id = @LotId;
                DELETE FROM tool_references WHERE tool_reference_id = @ReferenceId;
                DELETE FROM repairers WHERE repairer_id = @RepairerId;
                DELETE FROM internal_users WHERE actor_id = @ActorId;
                DELETE FROM access_templates WHERE template_id = @TemplateId;
                """,
                new NpgsqlParameter("ActorId", actorId),
                new NpgsqlParameter("TemplateId", templateId),
                new NpgsqlParameter("RepairerId", repairerId),
                new NpgsqlParameter("PieceId", pieceId),
                new NpgsqlParameter("LotId", lotId),
                new NpgsqlParameter("ReferenceId", referenceId));
        }
    }

    [Fact]
    public async Task ReturnClosesTheCycle_ThroughTheRealService()
    {
        if (SkipIfNoDatabase()) return;
        var (actorId, templateId, repairerId) = await SeedActorAndRepairerAsync();
        var pieceId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var reference = "CM-CYCLE-" + Guid.NewGuid().ToString("N")[..8];
        // Random distinctive 4-digit positions so re-runs never collide with
        // the residue left by the append-only movement rows of earlier runs.
        var startPosition = (1000 + (Guid.NewGuid().GetHashCode() % 9000 + 9000) % 9000).ToString();
        var returnPosition = (1000 + (Guid.NewGuid().GetHashCode() % 9000 + 9000) % 9000).ToString();
        if (startPosition == returnPosition) returnPosition = (1000 + (int.Parse(returnPosition) + 7) % 9000).ToString();
        await ExecuteAsync(
            """
            INSERT INTO tool_references (tool_reference_id, tool_type, ref_code)
            VALUES (@ReferenceId, 'CM', @Reference);
            INSERT INTO tool_lotes (tool_lote_id, tool_reference_id, lote)
            VALUES (@LotId, @ReferenceId, 'CYCLE-LOT');
            INSERT INTO physical_pieces (physical_piece_id, tool_lote_id, sequence, number)
            VALUES (@PieceId, @LotId, 1, 'CYCLE-N');
            INSERT INTO warehouse_locations (warehouse_location_id, code, kind)
            VALUES (@LocationId, @StartPosition, 'tool');
            INSERT INTO warehouse_stock (warehouse_stock_id, warehouse_location_id, tool_lote_id, occupied_since_utc, occupied_by)
            VALUES (@StockId, @LocationId, @LotId, now(), @ActorId);
            """,
            new NpgsqlParameter("ReferenceId", referenceId),
            new NpgsqlParameter("Reference", reference),
            new NpgsqlParameter("LotId", lotId),
            new NpgsqlParameter("PieceId", pieceId),
            new NpgsqlParameter("LocationId", Guid.NewGuid()),
            new NpgsqlParameter("StockId", Guid.NewGuid()),
            new NpgsqlParameter("ActorId", actorId),
            new NpgsqlParameter("StartPosition", startPosition));

        try
        {
            var factory = new DbConnectionFactory(ConnectionString!);
            var repository = new DapperRepairRepository(factory);
            var piece = new RepairToolIdentity(pieceId, lotId, referenceId, RepairType.CM, reference, "CYCLE-LOT", "CYCLE-N", null);
            var service = new ReparacaoExternaService(
                repository,
                new FixedToolResolver(piece),
                new DapperArmazemRepairMovementRepository(factory),
                new DapperRepairUnitOfWorkFactory(factory),
                new ReparacaoExternaAuthorizationGate(
                    new FixedCurrentUser(actorId), new FixedAuthorship(actorId)),
                new FixedClock(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero)));

            var created = await service.CreateExitAsync(new CreateExitRequest(
                RepairType.CM, repairerId, new DateOnly(2026, 9, 20),
                [new NewExitItemRequest(pieceId, "CYCLE-N")], null));
            Assert.True(created.IsSuccess);
            var exitId = created.Value;

            var disponibilizado = await service.DisponibilizarExitAsync(new DisponibilizarExitRequest(exitId));
            Assert.True(disponibilizado.IsSuccess);

            var items = await repository.GetExitItemsAsync(exitId);
            var item = Assert.Single(items);
            var picked = await service.ConfirmPickupAsync(new ConfirmPickupRequest(item.RepairExitItemId));
            Assert.True(picked.IsSuccess);

            var returned = await service.ConfirmReturnAsync(
                new ConfirmReturnRequest(item.RepairExitItemId, returnPosition));
            Assert.True(returned.IsSuccess, returned.IsFailure ? returned.Error.Message : string.Empty);

            // The coordinated return regression: the status machine must observe
            // the just-persisted in fact (same unit of work) and close the cycle.
            var exit = await repository.GetExitByIdAsync(exitId);
            Assert.NotNull(exit);
            Assert.Equal(RepairExitStatus.Concluido, exit!.Status);
            Assert.Equal("concluido", await ReadExitStatusAsync(exitId));
        }
        finally
        {
            // Best-effort: repair_events/warehouse_movements are append-only and
            // still reference the seeded rows, so part of the cleanup
            // legitimately fails (residue lives in the disposable test DB only).
            try
            {
                await ExecuteAsync(
                    """
                    DELETE FROM repair_exit_items
                    WHERE repair_exit_id IN (SELECT repair_exit_id FROM repair_exits WHERE created_by = @ActorId);
                    DELETE FROM repair_exits WHERE created_by = @ActorId;
                    DELETE FROM physical_pieces WHERE physical_piece_id = @PieceId;
                    DELETE FROM tool_lotes WHERE tool_lote_id = @LotId;
                    DELETE FROM tool_references WHERE tool_reference_id = @ReferenceId;
                    DELETE FROM repairers WHERE repairer_id = @RepairerId;
                    DELETE FROM internal_users WHERE actor_id = @ActorId;
                    DELETE FROM access_templates WHERE template_id = @TemplateId;
                    """,
                    new NpgsqlParameter("ActorId", actorId),
                    new NpgsqlParameter("TemplateId", templateId),
                    new NpgsqlParameter("RepairerId", repairerId),
                    new NpgsqlParameter("PieceId", pieceId),
                    new NpgsqlParameter("LotId", lotId),
                    new NpgsqlParameter("ReferenceId", referenceId));
            }
            catch (NpgsqlException)
            {
                // Append-only guards block part of the cleanup by design.
            }
        }
    }

    private static async Task<string?> ReadExitStatusAsync(Guid exitId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT status FROM repair_exits WHERE repair_exit_id = @Id;", connection);
        command.Parameters.AddWithValue("Id", exitId);
        return await command.ExecuteScalarAsync() as string;
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

    private sealed class FixedToolResolver(RepairToolIdentity piece) : IToolPieceResolver
    {
        public Task<IReadOnlyList<RepairToolIdentity>> SearchAsync(RepairType type, string? reference, string? lot, string? number, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepairToolIdentity>>([piece]);
        public Task<RepairToolIdentity?> ResolveAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(id == piece.PhysicalPieceId ? piece : null);
    }

    private sealed class FixedCurrentUser(string actorId) : ICurrentUserAccessor
    {
        public CurrentUser? Current { get; } = new(
            Guid.NewGuid(), actorId, [ReparacaoExternaModuleCatalog.ModuleId], []);
    }

    private sealed class FixedAuthorship(string actorId) : IPersistenceAuthorshipAccessor
    {
        public PersistenceAuthorship Current { get; } = new(actorId, DateTimeOffset.UtcNow);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}