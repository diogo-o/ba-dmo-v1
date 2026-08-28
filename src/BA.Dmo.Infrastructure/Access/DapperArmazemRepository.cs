using System.Data;
using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Armazem;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-14 — Armazém Dapper persistence (N09, GLM-ARM-04). Implements IArmazemRepository.
/// Multi-table writes (stock + movement; Substituir release+occupy) run inside a
/// single DapperUnitOfWork transaction (GLM-DATA-05). Position is the source of
/// truth; <c>fora</c> is derived; release keeps the fact row (partial unique index).
/// </summary>
public sealed class DapperArmazemRepository : IArmazemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperArmazemRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    private static async Task<IDbConnection> Open(IDbConnectionFactory factory, CancellationToken ct)
        => await factory.OpenConnectionAsync(ct);

    private static async Task DisposeAsync(IDbConnection connection)
    {
        if (connection is IAsyncDisposable a) await a.DisposeAsync();
        else connection.Dispose();
    }

    // ---- Locations ---------------------------------------------------------

    public async Task<Guid> GetOrCreateLocationAsync(string code, string? kind, CancellationToken ct = default)
    {
        var existing = await GetLocationByCodeAsync(code, ct);
        if (existing is not null) return existing.WarehouseLocationId;

        return await DapperUnitOfWork.RunAsync(_connectionFactory, async (conn, tx, _) =>
        {
            const string insertSql = @"
INSERT INTO warehouse_locations (warehouse_location_id, code, kind)
VALUES (@Id, @Code, @Kind)
ON CONFLICT (code) DO NOTHING;";
            var id = Guid.NewGuid();
            await Db.ExecuteAsync(conn, insertSql, new
            {
                Id = id,
                Code = code,
                Kind = (object?)kind ?? DBNull.Value
            }, tx, ct);

            const string selectSql = @"
SELECT warehouse_location_id, code, kind FROM warehouse_locations WHERE code = @Code;";
            dynamic row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, selectSql, new { Code = code }, tx, ct);
            return row is null ? id : (Guid)row.warehouse_location_id;
        }, ct);
    }

    public async Task<WarehouseLocation?> GetLocationByCodeAsync(string code, CancellationToken ct = default)
    {
        const string sql = @"
SELECT warehouse_location_id, code, kind FROM warehouse_locations WHERE code = @Code;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Code = code }, cancellationToken: ct);
            return row is null ? null : MapLocation(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<WarehouseLocation?> GetLocationByIdAsync(Guid warehouseLocationId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT warehouse_location_id, code, kind FROM warehouse_locations WHERE warehouse_location_id = @Id;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { Id = warehouseLocationId }, cancellationToken: ct);
            return row is null ? null : MapLocation(row);
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Stock -------------------------------------------------------------

    public async Task<WarehouseStock?> GetActiveStockByLocationAsync(Guid warehouseLocationId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT warehouse_stock_id, warehouse_location_id, tool_lote_id,
       occupied_since_utc, occupied_by, released_at_utc, released_by
FROM warehouse_stock
WHERE warehouse_location_id = @LocationId AND released_at_utc IS NULL
ORDER BY occupied_since_utc ASC
LIMIT 1;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { LocationId = warehouseLocationId }, cancellationToken: ct);
            return row is null ? null : MapStock(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<WarehouseStock?> GetActiveStockByToolIdAsync(Guid toolId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT warehouse_stock_id, warehouse_location_id, tool_lote_id,
       occupied_since_utc, occupied_by, released_at_utc, released_by
FROM warehouse_stock
WHERE tool_lote_id = @ToolId AND released_at_utc IS NULL
ORDER BY occupied_since_utc ASC
LIMIT 1;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(conn, sql, new { ToolId = toolId }, cancellationToken: ct);
            return row is null ? null : MapStock(row);
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<WarehouseStock>> GetStockByLocationAsync(Guid warehouseLocationId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT warehouse_stock_id, warehouse_location_id, tool_lote_id,
       occupied_since_utc, occupied_by, released_at_utc, released_by
FROM warehouse_stock WHERE warehouse_location_id = @LocationId;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { LocationId = warehouseLocationId }, cancellationToken: ct);
            return rows.Select<dynamic, WarehouseStock>(r => MapStock(r)).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Writes ------------------------------------------------------------

    public async Task<Guid> RegisterEntradaAsync(
        WarehouseStock stock, WarehouseMovement movement, CancellationToken ct = default)
    {
        return await DapperUnitOfWork.RunAsync(_connectionFactory, async (conn, tx, _) =>
        {
            // Transaction-safe 1:1 occupation guard (TOCTOU fix): first lock the
            // location row itself so concurrent Entradas into the SAME location
            // serialize even when no active occupant exists yet. Without this,
            // two different tools entering a currently-empty position could both
            // see "no occupant" and both pass (no stock row exists to lock).
            // Locking the always-present warehouse_locations row closes that gap.
            const string lockLocationSql = @"
SELECT warehouse_location_id
FROM warehouse_locations
WHERE warehouse_location_id = @LocationId
FOR UPDATE;";
            await Db.QuerySingleOrDefaultAsync<dynamic>(
                conn, lockLocationSql, new { LocationId = stock.WarehouseLocationId }, tx, ct);

            // With the location serialized, check any active occupant of the
            // position. A different tool that occupies it must fail cleanly (no
            // overwrite); the exception rolls the transaction back
            // (DapperUnitOfWork rethrows).
            const string lockCheckSql = @"
SELECT warehouse_stock_id, tool_lote_id
FROM warehouse_stock
WHERE warehouse_location_id = @LocationId AND released_at_utc IS NULL
ORDER BY occupied_since_utc ASC
FOR UPDATE;";
            dynamic? active = await Db.QuerySingleOrDefaultAsync<dynamic>(
                conn, lockCheckSql, new { LocationId = stock.WarehouseLocationId }, tx, ct);
            if (active is not null)
            {
                var occupantToolId = (Guid)active.tool_lote_id;
                if (occupantToolId != stock.ToolId)
                    throw new ArmazemLocationOccupiedException(
                        "A posição já está ocupada por outra ferramenta.");
                throw new ArmazemLocationOccupiedException(
                    "A posição já contém esta ferramenta.");
            }

            const string stockSql = @"
INSERT INTO warehouse_stock
    (warehouse_stock_id, warehouse_location_id, tool_lote_id,
     occupied_since_utc, occupied_by)
VALUES
    (@Id, @LocationId, @ToolId, @OccupiedSinceUtc, @OccupiedBy);";
            await Db.ExecuteAsync(conn, stockSql, new
            {
                Id = stock.WarehouseStockId,
                LocationId = stock.WarehouseLocationId,
                ToolId = stock.ToolId,
                OccupiedSinceUtc = stock.OccupiedSinceUtc,
                OccupiedBy = (object?)stock.OccupiedBy ?? DBNull.Value
            }, tx, ct);

            await InsertMovementAsync(conn, tx, ToMovementWithStock(movement, stock.WarehouseStockId), ct);
            return stock.WarehouseStockId;
        }, ct);
    }

    public async Task RegisterSaidaAsync(
        Guid stockId, string? releasedBy, DateTimeOffset releasedAtUtc,
        WarehouseMovement movement, CancellationToken ct = default)
    {
        await DapperUnitOfWork.RunAsync(_connectionFactory, async (conn, tx, _) =>
        {
            const string releaseSql = @"
UPDATE warehouse_stock
SET released_at_utc = @ReleasedAtUtc, released_by = @ReleasedBy
WHERE warehouse_stock_id = @Id AND released_at_utc IS NULL;";
            var affected = await Db.ExecuteAsync(conn, releaseSql, new
            {
                Id = stockId,
                ReleasedAtUtc = releasedAtUtc,
                ReleasedBy = (object?)releasedBy ?? DBNull.Value
            }, tx, ct);
            ConcurrencyGuard.EnsureSingleRowUpdated(affected, "warehouse_stock (saída)");

            var mv = ToMovementWithStock(movement, stockId);
            await InsertMovementAsync(conn, tx, mv, ct);
            return true;
        }, ct);
    }

    public async Task CorrectLocationAsync(
        Guid? currentStockId,
        WarehouseStock? correctedStock,
        WarehouseMovement? outMovement,
        WarehouseMovement? inMovement,
        CancellationToken ct = default)
    {
        if (currentStockId is null && correctedStock is null)
            throw new ArgumentException("A location correction must release or occupy stock.");
        if ((currentStockId is null) != (outMovement is null))
            throw new ArgumentException("The correction out movement must match the released stock.");
        if ((correctedStock is null) != (inMovement is null))
            throw new ArgumentException("The correction in movement must match the corrected stock.");

        await DapperUnitOfWork.RunAsync(_connectionFactory, async (conn, tx, _) =>
        {
            if (correctedStock is not null)
            {
                const string lockLocationSql = @"
SELECT warehouse_location_id
FROM warehouse_locations
WHERE warehouse_location_id = @LocationId
FOR UPDATE;";
                await Db.QuerySingleOrDefaultAsync<dynamic>(
                    conn, lockLocationSql,
                    new { LocationId = correctedStock.WarehouseLocationId }, tx, ct);

                const string activeAtTargetSql = @"
SELECT warehouse_stock_id, tool_lote_id
FROM warehouse_stock
WHERE warehouse_location_id = @LocationId AND released_at_utc IS NULL
ORDER BY occupied_since_utc ASC
FOR UPDATE;";
                dynamic? activeAtTarget = await Db.QuerySingleOrDefaultAsync<dynamic>(
                    conn, activeAtTargetSql,
                    new { LocationId = correctedStock.WarehouseLocationId }, tx, ct);
                if (activeAtTarget is not null)
                    throw new ArmazemLocationOccupiedException(
                        "A posição encontrada já está ocupada por outra ferramenta.");
            }

            if (currentStockId is not null)
            {
                const string releaseSql = @"
UPDATE warehouse_stock
SET released_at_utc = @ReleasedAtUtc, released_by = @ReleasedBy
WHERE warehouse_stock_id = @Id AND released_at_utc IS NULL;";
                var affected = await Db.ExecuteAsync(conn, releaseSql, new
                {
                    Id = currentStockId.Value,
                    ReleasedAtUtc = outMovement!.OccurredAtUtc,
                    ReleasedBy = (object?)outMovement.ActorId ?? DBNull.Value
                }, tx, ct);
                ConcurrencyGuard.EnsureSingleRowUpdated(
                    affected, "warehouse_stock (correção de localização)");
                await InsertMovementAsync(
                    conn, tx, ToMovementWithStock(outMovement, currentStockId.Value), ct);
            }

            if (correctedStock is not null)
            {
                const string insertSql = @"
INSERT INTO warehouse_stock
    (warehouse_stock_id, warehouse_location_id, tool_lote_id,
     occupied_since_utc, occupied_by)
VALUES
    (@Id, @LocationId, @ToolId, @OccupiedSinceUtc, @OccupiedBy);";
                await Db.ExecuteAsync(conn, insertSql, new
                {
                    Id = correctedStock.WarehouseStockId,
                    LocationId = correctedStock.WarehouseLocationId,
                    ToolId = correctedStock.ToolId,
                    OccupiedSinceUtc = correctedStock.OccupiedSinceUtc,
                    OccupiedBy = (object?)correctedStock.OccupiedBy ?? DBNull.Value
                }, tx, ct);
                await InsertMovementAsync(
                    conn, tx, ToMovementWithStock(inMovement!, correctedStock.WarehouseStockId), ct);
            }

            return true;
        }, ct);
    }

    // ---- History -----------------------------------------------------------

    public async Task<IReadOnlyList<WarehouseMovement>> GetMovementHistoryAsync(Guid toolId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT m.warehouse_movement_id, m.warehouse_stock_id, m.direction, m.qty,
       m.destination, m.actor_id, m.occurred_at_utc
FROM warehouse_movements m
JOIN warehouse_stock s ON s.warehouse_stock_id = m.warehouse_stock_id
WHERE s.tool_lote_id = @ToolId
ORDER BY m.occurred_at_utc ASC;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new { ToolId = toolId }, cancellationToken: ct);
            return rows.Select<dynamic, WarehouseMovement>(MapMovement).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    public async Task<IReadOnlyList<WarehouseMovementFact>> ListMovementFactsAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int limit,
        CancellationToken ct = default)
    {
        const string sql = @"
SELECT s.tool_lote_id, l.code AS position_code,
       m.warehouse_movement_id, m.warehouse_stock_id, m.direction, m.qty,
       m.destination, m.actor_id, m.occurred_at_utc
FROM warehouse_movements m
JOIN warehouse_stock s ON s.warehouse_stock_id = m.warehouse_stock_id
LEFT JOIN warehouse_locations l ON l.warehouse_location_id = s.warehouse_location_id
WHERE (@FromUtc IS NULL OR m.occurred_at_utc >= @FromUtc)
  AND (@ToUtc IS NULL OR m.occurred_at_utc < @ToUtc)
ORDER BY m.occurred_at_utc DESC, m.warehouse_movement_id DESC
LIMIT @Limit;";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            var rows = await Db.QueryAsync<dynamic>(conn, sql, new
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Limit = limit
            }, cancellationToken: ct);
            return rows.Select<dynamic, WarehouseMovementFact>(MapMovementFact).ToList().AsReadOnly();
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- Audit -------------------------------------------------------------

    public async Task InsertAuditEventAsync(
        Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot,
        string actorId, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO audit_events (occurred_at_utc, year, actor_user_id, module_id, action_code,
                          entity_type, entity_id, result, before_summary, after_summary)
VALUES (now(), EXTRACT(YEAR FROM now()), @Actor, 'armazem', @Action,
        'armazem', @EntityId, 'succeeded', @Before::jsonb, @After::jsonb);";
        var conn = await Open(_connectionFactory, ct);
        try
        {
            await Db.ExecuteAsync(conn, sql, new
            {
                Actor = actorId,
                Action = eventType,
                EntityId = entityId?.ToString(),
                Before = AuditJson.Normalize(beforeSnapshot),
                After = AuditJson.Normalize(afterSnapshot)
            }, cancellationToken: ct);
        }
        finally { await DisposeAsync(conn); }
    }

    // ---- helpers -----------------------------------------------------------

    private static async Task InsertMovementAsync(
        IDbConnection conn, IDbTransaction tx, WarehouseMovement movement, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO warehouse_movements
    (warehouse_movement_id, warehouse_stock_id, direction, qty, destination, actor_id, occurred_at_utc)
VALUES
    (@Id, @StockId, @Direction, @Qty, @Destination, @ActorId, @OccurredAtUtc);";
        await Db.ExecuteAsync(conn, sql, new
        {
            Id = movement.WarehouseMovementId,
            StockId = (object?)movement.WarehouseStockId ?? DBNull.Value,
            Direction = WarehouseMovementDirectionCodec.ToStorage(movement.Direction),
            Qty = (object?)movement.Qty ?? DBNull.Value,
            Destination = (object?)movement.Destination ?? DBNull.Value,
            ActorId = (object?)movement.ActorId ?? DBNull.Value,
            OccurredAtUtc = movement.OccurredAtUtc
        }, tx, ct);
    }

    private static WarehouseMovement ToMovementWithStock(WarehouseMovement m, Guid? stockId) => new()
    {
        WarehouseMovementId = m.WarehouseMovementId,
        WarehouseStockId = stockId,
        Direction = m.Direction,
        Qty = m.Qty,
        Destination = m.Destination,
        ActorId = m.ActorId,
        OccurredAtUtc = m.OccurredAtUtc
    };

    private static WarehouseLocation MapLocation(dynamic row) => new()
    {
        WarehouseLocationId = row.warehouse_location_id,
        Code = row.code,
        Kind = row.kind as string
    };

    private static WarehouseStock MapStock(dynamic row) => new()
    {
        WarehouseStockId = row.warehouse_stock_id,
        WarehouseLocationId = row.warehouse_location_id,
        ToolId = row.tool_lote_id,
        OccupiedSinceUtc = row.occupied_since_utc,
        OccupiedBy = row.occupied_by as string,
        ReleasedAtUtc = row.released_at_utc as DateTimeOffset?,
        ReleasedBy = row.released_by as string
    };

    private static WarehouseMovement MapMovement(dynamic row) => new()
    {
        WarehouseMovementId = row.warehouse_movement_id,
        WarehouseStockId = row.warehouse_stock_id,
        Direction = WarehouseMovementDirectionCodec.FromStorage(row.direction),
        Qty = row.qty as decimal?,
        Destination = row.destination as string,
        ActorId = row.actor_id is null ? null : row.actor_id.ToString(),
        OccurredAtUtc = row.occurred_at_utc
    };

    private static WarehouseMovementFact MapMovementFact(dynamic row) => new(
        ToolId: row.tool_lote_id,
        PositionCode: row.position_code as string,
        Movement: MapMovement(row));
}
