using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Domain.Modules.Armazem;

namespace BA.Dmo.UnitTests.Modules.Armazem;

/// <summary>
/// U-14 — In-memory fake of the Armazém persistence port (confined to tests/*).
/// Models occupation 1:1, release-keeps-fact, re-occupation and append-only
/// movements. Supports simulated atomic-write failure for fencing tests.
/// </summary>
public sealed class FakeArmazemRepository : IArmazemRepository
{
    public Dictionary<Guid, WarehouseLocation> Locations { get; } = new();
    public List<WarehouseStock> Stocks { get; } = new();
    public List<WarehouseMovement> Movements { get; } = new();
    public List<(Guid? entityId, string eventType, string? before, string? after, string actor)> AuditEvents { get; } = new();

    public int NextLocationSeq = 1;

    public bool FailAtomicWrite { get; set; }

    public Task<Guid> GetOrCreateLocationAsync(string code, string? kind, CancellationToken ct = default)
    {
        var existing = Locations.Values.FirstOrDefault(l => l.Code == code);
        if (existing is not null) return Task.FromResult(existing.WarehouseLocationId);
        var location = new WarehouseLocation { WarehouseLocationId = Guid.NewGuid(), Code = code, Kind = kind };
        Locations[location.WarehouseLocationId] = location;
        return Task.FromResult(location.WarehouseLocationId);
    }

    public Task<WarehouseLocation?> GetLocationByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Locations.Values.FirstOrDefault(l => l.Code == code));

    public Task<WarehouseLocation?> GetLocationByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Locations.GetValueOrDefault(id));

    public Task<WarehouseStock?> GetActiveStockByLocationAsync(Guid warehouseLocationId, CancellationToken ct = default)
        => Task.FromResult(Stocks.Where(s => s.WarehouseLocationId == warehouseLocationId && s.IsActive).OrderBy(s => s.OccupiedSinceUtc).FirstOrDefault());

    public Task<WarehouseStock?> GetActiveStockByToolIdAsync(Guid toolId, CancellationToken ct = default)
        => Task.FromResult(Stocks.Where(s => s.ToolId == toolId && s.IsActive).OrderBy(s => s.OccupiedSinceUtc).FirstOrDefault());

    public Task<IReadOnlyList<WarehouseStock>> GetActiveStocksAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WarehouseStock>>(Stocks.Where(s => s.IsActive).ToList());

    public Task<IReadOnlyList<WarehouseStock>> GetStockByLocationAsync(Guid warehouseLocationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WarehouseStock>>(Stocks.Where(s => s.WarehouseLocationId == warehouseLocationId).ToList());

    public Task<IReadOnlyList<WarehouseStock>> GetStockByToolIdAsync(Guid toolId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WarehouseStock>>(Stocks.Where(s => s.ToolId == toolId).ToList());

    public Task<Guid> RegisterEntradaAsync(WarehouseStock stock, WarehouseMovement movement, CancellationToken ct = default)
    {
        if (FailAtomicWrite) throw new InvalidOperationException("simulated atomic write failure");

        // Model the repository's atomic 1:1 occupation guard (TOCTOU fix): if an
        // active occupant already exists at the position, the Entrada conflicts.
        var active = Stocks.FirstOrDefault(s =>
            s.WarehouseLocationId == stock.WarehouseLocationId && s.IsActive);
        if (active is not null)
        {
            if (active.ToolId != stock.ToolId)
                throw new ArmazemLocationOccupiedException(
                    "A posição já está ocupada por outra ferramenta.");
            throw new ArmazemLocationOccupiedException(
                "A posição já contém esta ferramenta.");
        }

        Stocks.Add(stock);
        Movements.Add(ToMovementWithStock(movement, stock.WarehouseStockId));
        return Task.FromResult(stock.WarehouseStockId);
    }

    public Task RegisterSaidaAsync(Guid stockId, string? releasedBy, DateTimeOffset releasedAtUtc, WarehouseMovement movement, CancellationToken ct = default)
    {
        if (FailAtomicWrite) throw new InvalidOperationException("simulated atomic write failure");
        var stock = Stocks.FirstOrDefault(s => s.WarehouseStockId == stockId);
        if (stock is not null)
        {
            stock.ReleasedAtUtc = releasedAtUtc;
            stock.ReleasedBy = releasedBy;
        }
        Movements.Add(ToMovementWithStock(movement, stockId));
        return Task.CompletedTask;
    }

    public Task ReplaceOccupationAsync(Guid currentStockId, WarehouseStock newStock, WarehouseMovement outMovement, WarehouseMovement inMovement, CancellationToken ct = default)
    {
        if (FailAtomicWrite) throw new InvalidOperationException("simulated atomic write failure");
        var current = Stocks.FirstOrDefault(s => s.WarehouseStockId == currentStockId);
        if (current is not null)
        {
            current.ReleasedAtUtc = newStock.OccupiedSinceUtc;
            current.ReleasedBy = newStock.OccupiedBy;
        }
        Stocks.Add(newStock);
        Movements.Add(ToMovementWithStock(outMovement, currentStockId));
        Movements.Add(ToMovementWithStock(inMovement, newStock.WarehouseStockId));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WarehouseMovement>> GetMovementHistoryAsync(Guid toolId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WarehouseMovement>>(
            Movements.Where(m => m.WarehouseStockId.HasValue &&
                Stocks.Any(s => s.WarehouseStockId == m.WarehouseStockId && s.ToolId == toolId))
                .OrderBy(m => m.OccurredAtUtc).ToList());

    public Task InsertAuditEventAsync(Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken ct = default)
    {
        AuditEvents.Add((entityId, eventType, beforeSnapshot, afterSnapshot, actorId));
        return Task.CompletedTask;
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
}