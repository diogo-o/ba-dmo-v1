using BA.Dmo.Application.Modules.ReparacaoExterna;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.ReparacaoExterna;

namespace BA.Dmo.UnitTests.Modules.ReparacaoExterna;

/// <summary>
/// In-memory fake of the Reparação persistence port (confined to tests/*). Models
/// exits, items, repairers, line defaults and the duplicate-in-open-exit rule.
/// The coordinated pickup/return writes record the shared unit of work to let the
/// tests assert the repair and Armazém writes participated in ONE transaction
/// (owner decision C). Supports simulated atomic failure on item write. Copy of
/// the real signature used by the RepairExitStatusMachine.
/// </summary>
public sealed class FakeRepairRepository : IRepairRepository
{
    public List<RepairExit> Exits { get; } = new();
    public List<RepairExitItem> Items { get; } = new();
    public List<Repairer> Repairers { get; } = new();
    public List<LineRepairerDefault> LineDefaults { get; } = new();
    public Dictionary<Guid, HashSet<string>> RepairerTypes { get; } = new();
    public List<(Guid? entityId, string eventType, string? before, string? after, string actor)> AuditEvents { get; } = new();
    public List<(string kind, Guid exitItemId)> CoordinatedWrites { get; } = new();

    public bool FailItemWrite { get; set; }

    // ---- Exits --------------------------------------------------------------

    public Task<Guid> CreateExitAsync(RepairExit exit, RepairerSnapshot? snap, string? snapshotJson, CancellationToken ct = default)
    {
        Exits.Add(exit);
        return Task.FromResult(exit.RepairExitId);
    }

    public Task<RepairExit?> GetExitByIdAsync(Guid repairExitId, CancellationToken ct = default)
        => Task.FromResult(Exits.FirstOrDefault(e => e.RepairExitId == repairExitId));

    public Task<IReadOnlyList<RepairExitItem>> GetExitItemsAsync(Guid repairExitId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RepairExitItem>>(Items.Where(i => i.RepairExitId == repairExitId).Select(Clone).ToList());

    public Task<IReadOnlyList<RepairExit>> ListExitsAsync(
        RepairType? type, RepairExitStatus? status, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var result = Exits.Where(e =>
            (type is null || e.RepairType == type) &&
            (status is null || e.Status == status) &&
            (from is null || !e.PlannedDate.HasValue || e.PlannedDate.Value >= from) &&
            (to is null || !e.PlannedDate.HasValue || e.PlannedDate.Value <= to)).ToList();
        return Task.FromResult<IReadOnlyList<RepairExit>>(result);
    }

    public Task<bool> ExistsItemInOpenExitAsync(Guid physicalPieceId, CancellationToken ct = default)
    {
        var exists = Items.Any(i => i.PhysicalPieceId == physicalPieceId && !i.IsReturned);
        return Task.FromResult(exists);
    }

    // ---- Items --------------------------------------------------------------

    public Task<Guid> AddItemAsync(RepairExitItem item, CancellationToken ct = default)
    {
        if (FailItemWrite) throw new InvalidOperationException("simulated item write failure");
        Items.Add(item);
        return Task.FromResult(item.RepairExitItemId);
    }

    public Task<RepairExitItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default)
        => Task.FromResult(Clone(Items.FirstOrDefault(i => i.RepairExitItemId == itemId)));

    public Task DeleteItemAsync(Guid itemId, CancellationToken ct = default)
    {
        Items.RemoveAll(i => i.RepairExitItemId == itemId);
        return Task.CompletedTask;
    }

    // ---- Coordinated writes (shared unit of work) -----------------------------

    public Task ConfirmItemPickedAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default)
    {
        CoordinatedWrites.Add(("pickup", item.RepairExitItemId));
        var stored = Items.FirstOrDefault(i => i.RepairExitItemId == item.RepairExitItemId);
        if (stored is not null)
        {
            stored.Picked = item.Picked;
            stored.OutAtUtc = item.OutAtUtc;
            stored.OutOperatorId = item.OutOperatorId;
            stored.Status = item.Status;
        }
        return Task.CompletedTask;
    }

    public Task ConfirmItemReturnedAsync(IDbUnitOfWork uow, RepairExitItem item, CancellationToken ct = default)
    {
        CoordinatedWrites.Add(("return", item.RepairExitItemId));
        var stored = Items.FirstOrDefault(i => i.RepairExitItemId == item.RepairExitItemId);
        if (stored is not null)
        {
            stored.InAtUtc = item.InAtUtc;
            stored.InOperatorId = item.InOperatorId;
            stored.Status = item.Status;
        }
        return Task.CompletedTask;
    }

    public Task UpdateExitStatusAsync(IDbUnitOfWork uow, Guid repairExitId, string statusStorage, CancellationToken ct = default)
    {
        var exit = Exits.FirstOrDefault(e => e.RepairExitId == repairExitId);
        if (exit is not null) exit.Status = RepairExitStatusCodec.FromStorage(statusStorage);
        return Task.CompletedTask;
    }

    public Task InsertRepairEventAsync(IDbUnitOfWork uow, Guid repairExitItemId, string? notes, string actorId, DateTimeOffset occurredAtUtc, CancellationToken ct = default)
    {
        CoordinatedWrites.Add(("event", repairExitItemId));
        return Task.CompletedTask;
    }

    // ---- Repairers / line defaults --------------------------------------------

    public Task<Guid> CreateRepairerAsync(Repairer repairer, CancellationToken ct = default)
    {
        Repairers.Add(repairer);
        return Task.FromResult(repairer.RepairerId);
    }

    public Task UpdateRepairerAsync(Repairer repairer, CancellationToken ct = default)
    {
        var existing = Repairers.FirstOrDefault(r => r.RepairerId == repairer.RepairerId);
        if (existing is not null) existing.Name = repairer.Name;
        return Task.CompletedTask;
    }

    public Task DeactivateRepairerAsync(Guid repairerId, CancellationToken ct = default)
    {
        var r = Repairers.FirstOrDefault(x => x.RepairerId == repairerId);
        if (r is not null) r.Active = false;
        return Task.CompletedTask;
    }

    public Task<Repairer?> GetRepairerByIdAsync(Guid repairerId, CancellationToken ct = default)
        => Task.FromResult(Repairers.FirstOrDefault(r => r.RepairerId == repairerId));

    public Task<IReadOnlyList<Repairer>> ListRepairersAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Repairer>>(Repairers.ToList());

    public Task UpsertLineDefaultAsync(LineRepairerDefault lineDefault, CancellationToken ct = default)
    {
        LineDefaults.RemoveAll(d => d.Line == lineDefault.Line && d.ToolType == lineDefault.ToolType);
        LineDefaults.Add(lineDefault);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LineRepairerDefault>> ListLineDefaultsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LineRepairerDefault>>(LineDefaults.ToList());

    public Task SetRepairerRepairTypesAsync(Guid repairerId, IEnumerable<string> repairTypes, CancellationToken ct = default)
    {
        RepairerTypes[repairerId] = new HashSet<string>(repairTypes, StringComparer.Ordinal);
        var repairer = Repairers.FirstOrDefault(r => r.RepairerId == repairerId);
        if (repairer is not null) repairer.SupportedTypes = RepairerTypes[repairerId];
        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<string>> ListRepairerRepairTypesAsync(Guid repairerId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlySet<string>>(
            RepairerTypes.TryGetValue(repairerId, out var s) ? s : new HashSet<string>(StringComparer.Ordinal));

    // ---- Audit --------------------------------------------------------------

    public Task InsertAuditEventAsync(Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot, string actorId, CancellationToken ct = default)
    {
        AuditEvents.Add((entityId, eventType, beforeSnapshot, afterSnapshot, actorId));
        return Task.CompletedTask;
    }

    private static RepairExitItem Clone(RepairExitItem? item)
    {
        if (item is null) return null!;
        return new RepairExitItem
        {
            RepairExitItemId = item.RepairExitItemId,
            RepairExitId = item.RepairExitId,
            BqLoteId = item.BqLoteId,
            PhysicalPieceId = item.PhysicalPieceId,
            Qty = item.Qty,
            IndividualNumber = item.IndividualNumber,
            Picked = item.Picked,
            OutAtUtc = item.OutAtUtc,
            OutOperatorId = item.OutOperatorId,
            InAtUtc = item.InAtUtc,
            InOperatorId = item.InOperatorId,
            Status = item.Status
        };
    }
}