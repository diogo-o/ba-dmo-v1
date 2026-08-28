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

    public Task<IReadOnlyList<WarehouseStock>> GetStockByLocationAsync(Guid warehouseLocationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WarehouseStock>>(Stocks.Where(s => s.WarehouseLocationId == warehouseLocationId).ToList());

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

    public Task CorrectLocationAsync(
        Guid? currentStockId,
        WarehouseStock? correctedStock,
        WarehouseMovement? outMovement,
        WarehouseMovement? inMovement,
        CancellationToken ct = default)
    {
        if (FailAtomicWrite) throw new InvalidOperationException("simulated atomic write failure");
        if (currentStockId is null && correctedStock is null)
            throw new ArgumentException("A location correction must release or occupy stock.");

        var current = currentStockId is null
            ? null
            : Stocks.FirstOrDefault(s => s.WarehouseStockId == currentStockId && s.IsActive);
        if (currentStockId is not null && current is null)
            throw new InvalidOperationException("warehouse_stock (correção de localização) was changed concurrently.");

        if (correctedStock is not null && Stocks.Any(s =>
                s.WarehouseLocationId == correctedStock.WarehouseLocationId && s.IsActive))
            throw new ArmazemLocationOccupiedException(
                "A posição encontrada já está ocupada por outra ferramenta.");

        if (current is not null)
        {
            current.ReleasedAtUtc = outMovement!.OccurredAtUtc;
            current.ReleasedBy = outMovement.ActorId;
            Movements.Add(ToMovementWithStock(outMovement, current.WarehouseStockId));
        }

        if (correctedStock is not null)
        {
            Stocks.Add(correctedStock);
            Movements.Add(ToMovementWithStock(inMovement!, correctedStock.WarehouseStockId));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WarehouseMovement>> GetMovementHistoryAsync(Guid toolId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WarehouseMovement>>(
            Movements.Where(m => m.WarehouseStockId.HasValue &&
                Stocks.Any(s => s.WarehouseStockId == m.WarehouseStockId && s.ToolId == toolId))
                .OrderBy(m => m.OccurredAtUtc).ToList());

    public Task<IReadOnlyList<WarehouseMovementFact>> ListMovementFactsAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int limit,
        CancellationToken ct = default)
    {
        var facts = Movements
            .Where(m => (!fromUtc.HasValue || m.OccurredAtUtc >= fromUtc.Value) &&
                        (!toUtc.HasValue || m.OccurredAtUtc < toUtc.Value))
            .Select(m =>
            {
                var stock = Stocks.FirstOrDefault(s => s.WarehouseStockId == m.WarehouseStockId);
                if (stock is null) return null;
                var position = Locations.GetValueOrDefault(stock.WarehouseLocationId)?.Code;
                return new WarehouseMovementFact(stock.ToolId, position, m);
            })
            .Where(f => f is not null)
            .Cast<WarehouseMovementFact>()
            .OrderByDescending(f => f.Movement.OccurredAtUtc)
            .ThenByDescending(f => f.Movement.WarehouseMovementId)
            .Take(limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<WarehouseMovementFact>>(facts);
    }

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
