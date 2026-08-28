using System.Diagnostics;
using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL success, conflict, duplicate, and row-lock contention proofs
/// for ARMAZEM-01. BA_DMO_TEST_DATABASE must identify an isolated test database.
/// Every row uses a unique armazem01 identifier; no existing business row is
/// selected or mutated.
/// </summary>
public sealed class ArmazemReturnPostgresTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task ConfirmReturnAsync_FreeLocation_CommitsStockAndMovement()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync();
        var factory = CreateFactory("armazem01-success-" + Guid.NewGuid().ToString("N"));
        var repository = new DapperArmazemRepairMovementRepository(factory);

        await using (var uow = await DapperUnitOfWork.BeginAsync(factory))
        {
            var result = await repository.ConfirmReturnAsync(
                uow, context.FirstExitId, context.FirstLotId, context.PositionCode,
                context.ActorId, DateTimeOffset.UtcNow);

            Assert.True(result.IsSuccess);
            await uow.CommitAsync();
        }

        Assert.Equal(1, await CountActiveOccupantsAsync(context.LocationId));
        Assert.Equal(1, await CountMovementsAsync(context.FirstExitId));
    }

    [Fact]
    public async Task ConfirmReturnAsync_DifferentLotOccupied_ReturnsConflictWithoutPartialState()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync();
        var originalStockId = await SeedActiveStockAsync(context, context.FirstLotId);
        var factory = CreateFactory("armazem01-occupied-" + Guid.NewGuid().ToString("N"));
        var repository = new DapperArmazemRepairMovementRepository(factory);

        await using (var uow = await DapperUnitOfWork.BeginAsync(factory))
        {
            var result = await repository.ConfirmReturnAsync(
                uow, context.SecondExitId, context.SecondLotId, context.PositionCode,
                context.ActorId, DateTimeOffset.UtcNow);

            Assert.True(result.IsFailure);
            Assert.Equal("ARMZ_REPAIR_POSITION_OCCUPIED", result.Error.Code);
        }

        Assert.Equal(originalStockId, await ReadOnlyActiveStockIdAsync(context.LocationId));
        Assert.Equal(1, await CountActiveOccupantsAsync(context.LocationId));
        Assert.Equal(0, await CountMovementsAsync(context.SecondExitId));
    }

    [Fact]
    public async Task ConfirmReturnAsync_SameLotDuplicate_ReturnsControlledConflictWithout23505()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync();
        var originalStockId = await SeedActiveStockAsync(context, context.FirstLotId);
        var factory = CreateFactory("armazem01-duplicate-" + Guid.NewGuid().ToString("N"));
        var repository = new DapperArmazemRepairMovementRepository(factory);

        await using (var uow = await DapperUnitOfWork.BeginAsync(factory))
        {
            var exception = await Record.ExceptionAsync(async () =>
            {
                var result = await repository.ConfirmReturnAsync(
                    uow, context.FirstExitId, context.FirstLotId, context.PositionCode,
                    context.ActorId, DateTimeOffset.UtcNow);

                Assert.True(result.IsFailure);
                Assert.Equal("ARMZ_REPAIR_POSITION_OCCUPIED", result.Error.Code);
            });

            Assert.Null(exception);
        }

        Assert.Equal(originalStockId, await ReadOnlyActiveStockIdAsync(context.LocationId));
        Assert.Equal(1, await CountActiveOccupantsAsync(context.LocationId));
        Assert.Equal(0, await CountMovementsAsync(context.FirstExitId));
    }

    [Fact]
    public async Task ConfirmReturnAsync_ConcurrentDifferentLots_SerializesAndAllowsOneOccupant()
    {
        if (SkipIfNoDatabase()) return;
        var context = await SeedContextAsync();
        var firstApplication = "armazem01-winner-" + Guid.NewGuid().ToString("N");
        var secondApplication = "armazem01-contender-" + Guid.NewGuid().ToString("N");
        var firstFactory = CreateFactory(firstApplication);
        var secondFactory = CreateFactory(secondApplication);
        var firstRepository = new DapperArmazemRepairMovementRepository(firstFactory);
        var secondRepository = new DapperArmazemRepairMovementRepository(secondFactory);

        await using var firstUow = await DapperUnitOfWork.BeginAsync(firstFactory);
        await using var secondUow = await DapperUnitOfWork.BeginAsync(secondFactory);
        var firstCommitted = false;

        try
        {
            var first = await firstRepository.ConfirmReturnAsync(
                firstUow, context.FirstExitId, context.FirstLotId, context.PositionCode,
                context.ActorId, DateTimeOffset.UtcNow);
            Assert.True(first.IsSuccess);

            var secondTask = secondRepository.ConfirmReturnAsync(
                secondUow, context.SecondExitId, context.SecondLotId, context.PositionCode,
                context.ActorId, DateTimeOffset.UtcNow);

            await WaitForPostgresLockAsync(secondApplication);
            await firstUow.CommitAsync();
            firstCommitted = true;

            var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(second.IsFailure);
            Assert.Equal("ARMZ_REPAIR_POSITION_OCCUPIED", second.Error.Code);
        }
        finally
        {
            if (!firstCommitted) await firstUow.RollbackAsync();
            await secondUow.RollbackAsync();
        }

        Assert.Equal(1, await CountActiveOccupantsAsync(context.LocationId));
        Assert.Equal(1, await CountMovementsAsync(context.FirstExitId));
        Assert.Equal(0, await CountMovementsAsync(context.SecondExitId));
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] ArmazemReturnPostgresTests: BA_DMO_TEST_DATABASE not set — " +
            "real PostgreSQL return/concurrency assertions were not executed.");
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

    private static async Task<TestContext> SeedContextAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var actorId = "armazem01-" + suffix;
        var templateId = "armazem01-tpl-" + suffix;
        var referenceId = Guid.NewGuid();
        var firstLotId = Guid.NewGuid();
        var secondLotId = Guid.NewGuid();
        var firstExitId = Guid.NewGuid();
        var secondExitId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var positionCode = await ReservePositionCodeAsync(locationId);

        await ExecuteAsync(
            """
            INSERT INTO access_templates (template_id, name, modules, active)
            VALUES (@TemplateId, @TemplateName, '["armazem","reparacao_externa"]'::jsonb, TRUE);
            INSERT INTO internal_users
                (actor_id, auth_user_id, template_id, display_name, active)
            VALUES (@ActorId, @AuthUserId, @TemplateId, @DisplayName, TRUE);
            INSERT INTO tool_references
                (tool_reference_id, tool_type, ref_code, created_by)
            VALUES (@ReferenceId, 'CM', @Reference, @ActorId);
            INSERT INTO tool_lotes
                (tool_lote_id, tool_reference_id, lote, created_by)
            VALUES
                (@FirstLotId, @ReferenceId, @FirstLot, @ActorId),
                (@SecondLotId, @ReferenceId, @SecondLot, @ActorId);
            INSERT INTO repair_exits
                (repair_exit_id, repair_type, status, created_by)
            VALUES
                (@FirstExitId, 'CM', 'enviado', @ActorId),
                (@SecondExitId, 'CM', 'enviado', @ActorId);
            UPDATE warehouse_locations SET created_by = @ActorId
            WHERE warehouse_location_id = @LocationId;
            """,
            new NpgsqlParameter("TemplateId", templateId),
            new NpgsqlParameter("TemplateName", "ARMAZEM-01 " + suffix),
            new NpgsqlParameter("ActorId", actorId),
            new NpgsqlParameter("AuthUserId", Guid.NewGuid()),
            new NpgsqlParameter("DisplayName", "ARMAZEM-01 " + suffix),
            new NpgsqlParameter("ReferenceId", referenceId),
            new NpgsqlParameter("Reference", "CM-R01-" + suffix[..10]),
            new NpgsqlParameter("FirstLotId", firstLotId),
            new NpgsqlParameter("SecondLotId", secondLotId),
            new NpgsqlParameter("FirstLot", "R01-A-" + suffix[..10]),
            new NpgsqlParameter("SecondLot", "R01-B-" + suffix[..10]),
            new NpgsqlParameter("FirstExitId", firstExitId),
            new NpgsqlParameter("SecondExitId", secondExitId),
            new NpgsqlParameter("LocationId", locationId));

        return new TestContext(
            actorId, firstLotId, secondLotId, firstExitId, secondExitId,
            locationId, positionCode);
    }

    private static async Task<string> ReservePositionCodeAsync(Guid locationId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var code = Random.Shared.Next(1000, 10000).ToString();
            await using var connection = new NpgsqlConnection(ConnectionString!);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO warehouse_locations (warehouse_location_id, code, kind)
                VALUES (@Id, @Code, 'tool')
                ON CONFLICT (code) DO NOTHING
                RETURNING code;
                """, connection);
            command.Parameters.AddWithValue("Id", locationId);
            command.Parameters.AddWithValue("Code", code);
            var inserted = await command.ExecuteScalarAsync();
            if (inserted is string reserved) return reserved;
        }

        throw new InvalidOperationException("Could not reserve a synthetic ARMAZEM-01 position.");
    }

    private static async Task<Guid> SeedActiveStockAsync(TestContext context, Guid lotId)
    {
        var stockId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO warehouse_stock
                (warehouse_stock_id, warehouse_location_id, tool_lote_id,
                 occupied_since_utc, occupied_by)
            VALUES (@StockId, @LocationId, @LotId, now(), @ActorId);
            """,
            new NpgsqlParameter("StockId", stockId),
            new NpgsqlParameter("LocationId", context.LocationId),
            new NpgsqlParameter("LotId", lotId),
            new NpgsqlParameter("ActorId", context.ActorId));
        return stockId;
    }

    private static async Task WaitForPostgresLockAsync(string applicationName)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            await using var connection = new NpgsqlConnection(ConnectionString!);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_stat_activity
                    WHERE application_name = @ApplicationName
                      AND wait_event_type = 'Lock');
                """, connection);
            command.Parameters.AddWithValue("ApplicationName", applicationName);
            if ((bool)(await command.ExecuteScalarAsync())!) return;
            await Task.Delay(25);
        }

        throw new TimeoutException(
            "The second PostgreSQL transaction did not report a real lock wait.");
    }

    private static async Task<Guid> ReadOnlyActiveStockIdAsync(Guid locationId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT warehouse_stock_id FROM warehouse_stock
            WHERE warehouse_location_id = @LocationId AND released_at_utc IS NULL;
            """, connection);
        command.Parameters.AddWithValue("LocationId", locationId);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> CountActiveOccupantsAsync(Guid locationId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)::int FROM warehouse_stock
            WHERE warehouse_location_id = @LocationId AND released_at_utc IS NULL;
            """, connection);
        command.Parameters.AddWithValue("LocationId", locationId);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<int> CountMovementsAsync(Guid exitId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*)::int FROM warehouse_movements WHERE repair_exit_id = @ExitId;",
            connection);
        command.Parameters.AddWithValue("ExitId", exitId);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString!);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record TestContext(
        string ActorId,
        Guid FirstLotId,
        Guid SecondLotId,
        Guid FirstExitId,
        Guid SecondExitId,
        Guid LocationId,
        string PositionCode);
}
