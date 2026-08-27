using System.Data;
using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Armazem;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-15 — Armazém-owned Dapper implementation of the repair-movement port
/// (IArmazemRepairMovementPort; owner decision B). Armazém remains the SOLE owner
/// of <c>warehouse_stock</c> and <c>warehouse_movements</c>: this class writes
/// those tables, preserving occupancy 1:1 and append-only movements, and records
/// the <c>repair_exit_id</c> on the movement for provenance. It participates in the
/// caller-provided <see cref="IDbUnitOfWork"/> so the repair-cycle write + the
/// physical movement commit/roll back together (owner decision C). Physical state
/// changes only on explicit confirmation (owner decision D) — nothing is inferred.
/// </summary>
public sealed class DapperArmazemRepairMovementRepository : IArmazemRepairMovementPort
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperArmazemRepairMovementRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Result<bool, DomainError>> ConfirmPickupAsync(
        IDbUnitOfWork uow, Guid repairExitId, Guid toolLoteId, string actorId, DateTimeOffset outAtUtc, CancellationToken ct = default)
    {
        // Only the currently active occupation is released (occupancy 1:1, GLM-ARM-04).
        const string findActive = @"
SELECT warehouse_stock_id, tool_lote_id
FROM warehouse_stock
WHERE tool_lote_id = @ToolLoteId AND released_at_utc IS NULL
ORDER BY occupied_since_utc ASC LIMIT 1;";
        dynamic? active = await Db.QuerySingleOrDefaultAsync<dynamic>(
            uow.Connection, findActive, new { ToolLoteId = toolLoteId }, uow.Transaction, ct);

        if (active is null)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "ARMZ_REPAIR_NOT_IN_WAREHOUSE",
                "A ferramenta não está registada como presente no Armazém para ser libertada."));

        var stockId = (Guid)active.warehouse_stock_id;

        const string release = @"
UPDATE warehouse_stock
SET released_at_utc = @ReleasedAtUtc, released_by = @ReleasedBy
WHERE warehouse_stock_id = @Id AND released_at_utc IS NULL
RETURNING warehouse_stock_id;";
        var released = await Db.QuerySingleOrDefaultAsync<Guid?>(
            uow.Connection, release, new { Id = stockId, ReleasedAtUtc = outAtUtc, ReleasedBy = actorId }, uow.Transaction, ct);

        if (released is null)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "ARMZ_REPAIR_ALREADY_RELEASED",
                "A posição desta ferramenta já foi libertada."));

        await InsertMovementAsync(uow, stockId, "out", repairExitId, actorId, outAtUtc, ct);
        return Result<bool, DomainError>.Success(true);
    }

    public async Task<Result<bool, DomainError>> ConfirmReturnAsync(
        IDbUnitOfWork uow, Guid repairExitId, Guid toolLoteId, string positionCode, string actorId, DateTimeOffset inAtUtc, CancellationToken ct = default)
    {
        if (!WarehouseLocation.IsValidPositionCode(positionCode))
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "ARMZ_REPAIR_POSITION_CODE", "A posição de retorno deve ter exatamente 4 dígitos."));

        var locationId = await GetOrCreateLocationAsync(uow, WarehouseLocation.NormalizePositionCode(positionCode), "tool", ct);

        // Serialize on the stable location row before checking active stock. An
        // empty position has no warehouse_stock row to lock, so locking only the
        // occupant would leave a TOCTOU gap for concurrent returns.
        const string lockLocation = @"
SELECT warehouse_location_id
FROM warehouse_locations
WHERE warehouse_location_id = @LocationId
FOR UPDATE;";
        await Db.QuerySingleOrDefaultAsync<Guid?>(
            uow.Connection, lockLocation, new { LocationId = locationId }, uow.Transaction, ct);

        // Occupancy 1:1 — after the location lock is acquired, this read observes
        // any occupation committed by a return that held the lock immediately
        // before this transaction.
        const string occupant = @"
SELECT warehouse_stock_id, tool_lote_id
FROM warehouse_stock
WHERE warehouse_location_id = @LocationId AND released_at_utc IS NULL
ORDER BY occupied_since_utc ASC
LIMIT 1
FOR UPDATE;";
        dynamic? existing = await Db.QuerySingleOrDefaultAsync<dynamic>(
            uow.Connection, occupant, new { LocationId = locationId }, uow.Transaction, ct);

        if (existing is not null)
            return Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "ARMZ_REPAIR_POSITION_OCCUPIED",
                (Guid)existing.tool_lote_id == toolLoteId
                    ? "A posição de retorno já contém esta ferramenta."
                    : "A posição de retorno já está ocupada por outra ferramenta."));

        var stockId = Guid.NewGuid();
        const string occupy = @"
INSERT INTO warehouse_stock
    (warehouse_stock_id, warehouse_location_id, tool_lote_id, occupied_since_utc, occupied_by)
VALUES
    (@Id, @LocationId, @ToolLoteId, @OccupiedSinceUtc, @OccupiedBy);";
        await Db.ExecuteAsync(uow.Connection, occupy, new
        {
            Id = stockId,
            LocationId = locationId,
            ToolLoteId = toolLoteId,
            OccupiedSinceUtc = inAtUtc,
            OccupiedBy = (object?)actorId ?? DBNull.Value
        }, uow.Transaction, ct);

        await InsertMovementAsync(uow, stockId, "in", repairExitId, actorId!, inAtUtc, ct);
        return Result<bool, DomainError>.Success(true);
    }

    private async Task InsertMovementAsync(
        IDbUnitOfWork uow, Guid stockId, string direction, Guid repairExitId, string actorId, DateTimeOffset occurredAtUtc, CancellationToken ct)
    {
        const string sql = @"
INSERT INTO warehouse_movements
    (warehouse_movement_id, warehouse_stock_id, direction, qty, destination, repair_exit_id, actor_id, occurred_at_utc)
VALUES
    (@Id, @StockId, @Direction, @Qty, @Destination, @RepairExitId, @ActorId, @OccurredAtUtc);";
        await Db.ExecuteAsync(uow.Connection, sql, new
        {
            Id = Guid.NewGuid(),
            StockId = stockId,
            Direction = direction,
            Qty = DBNull.Value,
            Destination = (object?)"reparacao_externa" ?? DBNull.Value,
            RepairExitId = repairExitId,
            ActorId = (object?)actorId ?? DBNull.Value,
            OccurredAtUtc = occurredAtUtc
        }, uow.Transaction, ct);
    }

    private async Task<Guid> GetOrCreateLocationAsync(
        IDbUnitOfWork uow, string code, string? kind, CancellationToken ct)
    {
        const string select = "SELECT warehouse_location_id FROM warehouse_locations WHERE code = @Code;";
        var id = await Db.QuerySingleOrDefaultAsync<Guid?>(
            uow.Connection, select, new { Code = code }, uow.Transaction, ct);
        if (id.HasValue) return id.Value;

        const string insert = @"
INSERT INTO warehouse_locations (warehouse_location_id, code, kind)
VALUES (@Id, @Code, @Kind)
ON CONFLICT (code) DO NOTHING;";
        var newId = Guid.NewGuid();
        await Db.ExecuteAsync(uow.Connection, insert, new { Id = newId, Code = code, Kind = (object?)kind ?? DBNull.Value }, uow.Transaction, ct);

        const string reselect = "SELECT warehouse_location_id FROM warehouse_locations WHERE code = @Code;";
        var confirmed = await Db.QuerySingleOrDefaultAsync<Guid?>(
            uow.Connection, reselect, new { Code = code }, uow.Transaction, ct);
        return confirmed ?? newId;
    }
}
