using BA.Dmo.Infrastructure.Access;
using BA.Dmo.Infrastructure.Persistence;
using Npgsql;

namespace BA.Dmo.IntegrationTests.Persistence;

/// <summary>
/// Real-PostgreSQL binding proof for the ARMAZEM-01 readback regression:
/// <c>warehouse_stock.released_at_utc</c> is a timestamptz that Npgsql surfaces as
/// DateTime on dynamic rows, so the old <c>as DateTimeOffset?</c> mapper cast
/// silently returned null. The explicit conversion must surface the stored
/// instant (and keep the null path null for active occupations). The disposable
/// database is supplied through BA_DMO_TEST_DATABASE, following
/// RepairExitBindingTests.
/// </summary>
public sealed class ArmazemReleasedAtUtcBindingTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("BA_DMO_TEST_DATABASE");

    [Fact]
    public async Task GetStockByLocationAsync_SurfacesReleasedAtUtcAndKeepsActiveNull()
    {
        if (SkipIfNoDatabase()) return;
        var actorId = "armazem-ts-" + Guid.NewGuid().ToString("N")[..8];
        var templateId = "armazem-ts-tpl-" + Guid.NewGuid().ToString("N")[..8];
        var referenceId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var releasedLotId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var stockId = Guid.NewGuid();
        var releasedStockId = Guid.NewGuid();
        var reference = "CM-TS-" + Guid.NewGuid().ToString("N")[..6];
        var position = (1000 + Guid.NewGuid().GetHashCode() % 9000 + 9000) % 9000;

        await SeedAsync(actorId, templateId, referenceId, lotId, releasedLotId, locationId,
            stockId, releasedStockId, reference, position.ToString());

        try
        {
            var repository = new DapperArmazemRepository(new DbConnectionFactory(ConnectionString!));
            var releasedAt = new DateTimeOffset(2026, 9, 11, 14, 30, 0, TimeSpan.Zero);

            var stock = await repository.GetStockByLocationAsync(locationId);

            // The timestamptz readback regression: `as DateTimeOffset?` returned
            // null for the released row; the explicit conversion must surface it.
            var released = Assert.Single(stock, s => s.WarehouseStockId == releasedStockId);
            Assert.NotNull(released.ReleasedAtUtc);
            Assert.Equal(releasedAt.ToUniversalTime(), released.ReleasedAtUtc!.Value.ToUniversalTime());
            Assert.Equal(actorId, released.ReleasedBy);
            Assert.Equal(releasedLotId, released.ToolId);

            // The null path must stay null: an active occupation has no release.
            var active = Assert.Single(stock, s => s.WarehouseStockId == stockId);
            Assert.Null(active.ReleasedAtUtc);
            Assert.Null(active.ReleasedBy);
            Assert.True(active.IsActive);

            // The active-only reader must not surface the released fact.
            var activeOnly = await repository.GetActiveStockByLocationAsync(locationId);
            Assert.NotNull(activeOnly);
            Assert.Equal(stockId, activeOnly!.WarehouseStockId);
        }
        finally
        {
            await CleanupAsync(actorId, templateId, referenceId, lotId, releasedLotId, locationId, stockId, releasedStockId);
        }
    }

    private static bool SkipIfNoDatabase()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return false;
        Console.WriteLine(
            "[SKIP] ArmazemReleasedAtUtcBindingTests: BA_DMO_TEST_DATABASE not set - " +
            "real PostgreSQL binding assertions were not executed.");
        return true;
    }

    private static async Task SeedAsync(
        string actorId, string templateId, Guid referenceId, Guid lotId, Guid releasedLotId,
        Guid locationId, Guid stockId, Guid releasedStockId, string reference, string position)
    {
        await ExecuteAsync(
            """
            INSERT INTO access_templates (template_id, name, modules, active)
            VALUES (@TemplateId, 'Armazem ts binding test', '["armazem"]'::jsonb, TRUE);
            INSERT INTO internal_users
                (actor_id, auth_user_id, template_id, display_name, active)
            VALUES
                (@ActorId, @AuthUserId, @TemplateId, 'Armazem Ts Binding Test', TRUE);
            INSERT INTO tool_references (tool_reference_id, tool_type, ref_code)
            VALUES (@ReferenceId, 'CM', @Reference);
            INSERT INTO tool_lotes (tool_lote_id, tool_reference_id, lote)
            VALUES (@LotId, @ReferenceId, 'TS-LOT-ACTIVE'), (@ReleasedLotId, @ReferenceId, 'TS-LOT-RELEASED');
            INSERT INTO warehouse_locations (warehouse_location_id, code, kind)
            VALUES (@LocationId, @Position, 'tool');
            INSERT INTO warehouse_stock
                (warehouse_stock_id, warehouse_location_id, tool_lote_id, occupied_since_utc, occupied_by,
                 released_at_utc, released_by)
            VALUES
                (@StockId, @LocationId, @LotId, now(), @ActorId, NULL, NULL),
                (@ReleasedStockId, @LocationId, @ReleasedLotId, now(), @ActorId,
                 '2026-09-11T14:30:00Z'::timestamptz, @ActorId);
            """,
            new NpgsqlParameter("TemplateId", templateId),
            new NpgsqlParameter("ActorId", actorId),
            new NpgsqlParameter("AuthUserId", Guid.NewGuid()),
            new NpgsqlParameter("ReferenceId", referenceId),
            new NpgsqlParameter("Reference", reference),
            new NpgsqlParameter("LotId", lotId),
            new NpgsqlParameter("ReleasedLotId", releasedLotId),
            new NpgsqlParameter("LocationId", locationId),
            new NpgsqlParameter("Position", position),
            new NpgsqlParameter("StockId", stockId),
            new NpgsqlParameter("ReleasedStockId", releasedStockId));
    }

    private static async Task CleanupAsync(
        string actorId, string templateId, Guid referenceId, Guid lotId, Guid releasedLotId,
        Guid locationId, Guid stockId, Guid releasedStockId)
    {
        await ExecuteAsync(
            """
            DELETE FROM warehouse_stock
            WHERE warehouse_stock_id IN (@StockId, @ReleasedStockId);
            DELETE FROM warehouse_locations WHERE warehouse_location_id = @LocationId;
            DELETE FROM tool_lotes
            WHERE tool_lote_id IN (@LotId, @ReleasedLotId);
            DELETE FROM tool_references WHERE tool_reference_id = @ReferenceId;
            DELETE FROM internal_users WHERE actor_id = @ActorId;
            DELETE FROM access_templates WHERE template_id = @TemplateId;
            """,
            new NpgsqlParameter("ActorId", actorId),
            new NpgsqlParameter("TemplateId", templateId),
            new NpgsqlParameter("ReferenceId", referenceId),
            new NpgsqlParameter("LotId", lotId),
            new NpgsqlParameter("ReleasedLotId", releasedLotId),
            new NpgsqlParameter("LocationId", locationId),
            new NpgsqlParameter("StockId", stockId),
            new NpgsqlParameter("ReleasedStockId", releasedStockId));
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