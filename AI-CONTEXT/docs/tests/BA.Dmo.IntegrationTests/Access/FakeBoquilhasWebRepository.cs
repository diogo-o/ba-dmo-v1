using BA.Dmo.Application.Modules.Boquilhas;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Boquilhas;

namespace BA.Dmo.IntegrationTests.Access;

/// <summary>
/// U-19 — In-memory fake of <see cref="IBoquilhasRepository"/> for the
/// Boquilhas Web integration fixture (confined to tests/*). Implements the same
/// contract as the unit-test fake; supports create/lot/movement/discrepancy/
/// trace flows. Reset() restores empty state per test.
/// </summary>
public sealed class FakeBoquilhasWebRepository : IBoquilhasRepository
{
    public List<BqLote> Lotes { get; } = new();
    public List<BqTrace> Traces { get; } = new();
    public List<BqMovement> Movements { get; } = new();
    public List<BqDiscrepancy> Discrepancies { get; } = new();
    public List<BqLifecycleEvent> LifecycleEvents { get; } = new();
    public List<BqUtilisationReading> Utilisation { get; } = new();
    public List<BqRepairer> Repairers { get; } = new();
    public List<BqLineRepairerDefault> LineDefaults { get; } = new();

    public void Reset()
    {
        Lotes.Clear(); Traces.Clear(); Movements.Clear(); Discrepancies.Clear();
        LifecycleEvents.Clear(); Utilisation.Clear(); Repairers.Clear(); LineDefaults.Clear();
    }

    public Task<BqLote?> GetLoteByIdAsync(Guid bqLoteId, CancellationToken ct = default)
        => Task.FromResult(Lotes.FirstOrDefault(l => l.BqLoteId == bqLoteId));

    public Task<BqLote?> GetLoteByReferenceBatchAsync(string reference, string batchCode, CancellationToken ct = default)
        => Task.FromResult(Lotes.FirstOrDefault(l => l.Reference == reference && l.BatchCode == batchCode));

    public Task<IReadOnlyList<BqLote>> ListLotesAsync(BqLoteFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BqLote>>(Lotes.Where(l =>
            (filter.Search is null ||
             l.Reference.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) ||
             l.BatchCode.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)) &&
            (filter.OnlyAvailable != true || l.LifecycleState == BqLifecycleState.Available) &&
            (filter.LifecycleState is null || l.LifecycleState == filter.LifecycleState)).ToList());

    public Task<int> CountLotesAsync(BqLoteFilter filter, CancellationToken ct = default) => Task.FromResult(Lotes.Count);

    public Task CreateLoteAsync(IDbUnitOfWork uow, BqLote lote, CancellationToken ct = default)
    {
        Lotes.Add(lote); return Task.CompletedTask;
    }

    public Task UpdateLoteAsync(IDbUnitOfWork uow, BqLote lote, CancellationToken ct = default)
    {
        var e = Lotes.FirstOrDefault(l => l.BqLoteId == lote.BqLoteId);
        if (e is not null) { e.Reference = lote.Reference; e.BatchCode = lote.BatchCode; e.AllowedLines = lote.AllowedLines; }
        return Task.CompletedTask;
    }

    public Task UpdateLifecycleStateAsync(IDbUnitOfWork uow, Guid bqLoteId, BqLifecycleState state, CancellationToken ct = default)
    {
        var l = Lotes.FirstOrDefault(x => x.BqLoteId == bqLoteId);
        if (l is not null) l.LifecycleState = state; return Task.CompletedTask;
    }

    public Task InsertLifecycleEventAsync(IDbUnitOfWork uow, BqLifecycleEvent evt, CancellationToken ct = default)
    {
        LifecycleEvents.Add(evt); return Task.CompletedTask;
    }

    public Task<BqTrace?> GetTraceByIdAsync(Guid bqTraceId, CancellationToken ct = default)
        => Task.FromResult(Traces.FirstOrDefault(t => t.BqTraceId == bqTraceId));

    public Task<BqTrace?> GetActiveTraceForLoteAsync(Guid bqLoteId, CancellationToken ct = default)
        => Task.FromResult(Traces.FirstOrDefault(t => t.BqLoteId == bqLoteId && t.Status == BqTraceStatus.Active));

    public Task<BqTrace?> GetLastClosedOrActiveTraceAsync(Guid bqLoteId, CancellationToken ct = default)
        => Task.FromResult(Traces.Where(t => t.BqLoteId == bqLoteId).OrderByDescending(t => t.CreatedAtUtc).Cast<BqTrace?>().FirstOrDefault());

    public Task<BqTrace?> GetTraceForMovementAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default)
        => Task.FromResult(Traces.FirstOrDefault(t => t.BqTraceId == bqTraceId));

    public Task CreateTraceAsync(IDbUnitOfWork uow, BqTrace trace, CancellationToken ct = default)
    {
        Traces.Add(trace); return Task.CompletedTask;
    }

    public Task CloseTraceAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default)
    {
        var t = Traces.FirstOrDefault(x => x.BqTraceId == bqTraceId);
        if (t is not null) t.Status = BqTraceStatus.Closed; return Task.CompletedTask;
    }

    public Task ReopenTraceAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default)
    {
        var t = Traces.FirstOrDefault(x => x.BqTraceId == bqTraceId);
        if (t is not null) t.Status = BqTraceStatus.Active; return Task.CompletedTask;
    }

    public Task AppendReopenHistoryAsync(IDbUnitOfWork uow, Guid bqTraceId, string actorId, DateTimeOffset atUtc, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task InsertMovementAsync(IDbUnitOfWork uow, BqMovement movement, CancellationToken ct = default)
    {
        Movements.Add(movement); return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BqMovement>> ListMovementsForTraceAsync(Guid bqTraceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BqMovement>>(Movements.Where(m => m.BqTraceId == bqTraceId).OrderBy(m => m.OccurredAtUtc).ToList());

    public Task<IReadOnlyList<BqMovement>> ListMovementsByLoteAsync(Guid bqLoteId, BqHistoryFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BqMovement>>(Movements.Where(m =>
            Traces.Any(t => t.BqTraceId == m.BqTraceId && t.BqLoteId == bqLoteId)).ToList());

    public Task<IReadOnlyList<BqMovement>> ListMovementsAsync(BqHistoryFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BqMovement>>(Movements
            .Where(m => filter.BqLoteId is null || Traces.Any(t => t.BqTraceId == m.BqTraceId && t.BqLoteId == filter.BqLoteId))
            .Where(m => filter.MovementType is null || m.MovementType == filter.MovementType)
            .Where(m => filter.RepairerId is null || m.RepairerId == filter.RepairerId)
            .Where(m => filter.From is null || m.OccurredAtUtc >= filter.From)
            .Where(m => filter.To is null || m.OccurredAtUtc <= filter.To)
            .Where(m => filter.Search is null
                || Lotes.Any(l => l.BqLoteId == Traces.FirstOrDefault(t => t.BqTraceId == m.BqTraceId)?.BqLoteId
                    && (l.Reference.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)
                        || l.BatchCode.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)))
                || (m.Line?.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(m => m.OccurredAtUtc)
            .ToList());

    public Task<int> CountMovementsAsync(BqHistoryFilter filter, CancellationToken ct = default) => Task.FromResult(Movements.Count);

    public Task VoidMovementAsync(IDbUnitOfWork uow, Guid bqTraceId, Guid bqMovementId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlySet<Guid>> ListVoidedMovementIdsAsync(Guid bqTraceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

    public Task InsertUtilisationReadingAsync(IDbUnitOfWork uow, BqUtilisationReading reading, CancellationToken ct = default)
    {
        Utilisation.Add(reading); return Task.CompletedTask;
    }

    public Task<BqUtilisationReading?> GetUtilisationReadingAsync(Guid bqTraceId, BqUtilisationReadingKind kind, CancellationToken ct = default)
        => Task.FromResult(Utilisation.LastOrDefault(u => u.BqTraceId == bqTraceId && u.ReadingKind == kind));

    public Task<BqDiscrepancy?> GetOpenDiscrepancyForTraceAsync(Guid bqLoteId, Guid? bqTraceId, CancellationToken ct = default) => Task.FromResult<BqDiscrepancy?>(null);

    public Task InsertDiscrepancyAsync(IDbUnitOfWork uow, BqDiscrepancy discrepancy, CancellationToken ct = default)
    {
        Discrepancies.Add(discrepancy); return Task.CompletedTask;
    }

    public Task UpdateDiscrepancyAsync(IDbUnitOfWork uow, BqDiscrepancy discrepancy, CancellationToken ct = default)
    {
        var d = Discrepancies.FirstOrDefault(x => x.BqDiscrepancyId == discrepancy.BqDiscrepancyId);
        if (d is not null) { d.Status = discrepancy.Status; d.ResolutionNote = discrepancy.ResolutionNote; }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BqDiscrepancy>> ListDiscrepanciesAsync(Guid? bqLoteId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BqDiscrepancy>>(Discrepancies.Where(d => bqLoteId is null || d.BqLoteId == bqLoteId).ToList());

    public Task<IReadOnlyList<BqRepairer>> ListRepairersAsync(bool onlyActive, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BqRepairer>>(Repairers.Where(r => !onlyActive || r.Active).ToList());

    public Task<BqRepairer?> GetRepairerByIdAsync(Guid repairerId, CancellationToken ct = default)
        => Task.FromResult(Repairers.FirstOrDefault(r => r.RepairerId == repairerId));

    public Task<Guid> CreateRepairerAsync(BqRepairer repairer, CancellationToken ct = default)
    {
        Repairers.Add(repairer); return Task.FromResult(repairer.RepairerId);
    }

    public Task UpdateRepairerAsync(BqRepairer repairer, CancellationToken ct = default)
    {
        var r = Repairers.FirstOrDefault(x => x.RepairerId == repairer.RepairerId);
        if (r is not null) { r.Name = repairer.Name; r.Active = repairer.Active; }
        return Task.CompletedTask;
    }

    public Task<BqLineRepairerDefault?> GetLineRepairerDefaultAsync(string line, CancellationToken ct = default)
        => Task.FromResult(LineDefaults.FirstOrDefault(d => d.Line == line));

    public Task SetLineRepairerDefaultAsync(BqLineRepairerDefault lineDefault, CancellationToken ct = default)
    {
        var e = LineDefaults.FirstOrDefault(d => d.Line == lineDefault.Line);
        if (e is null) LineDefaults.Add(lineDefault);
        else e.DefaultRepairerId = lineDefault.DefaultRepairerId;
        return Task.CompletedTask;
    }

    public Task InsertAuditEventAsync(IDbUnitOfWork uow, string actionCode, string entityType, string entityId,
        string result, string? beforeSummary, string? afterSummary, string actorId,
        DateTimeOffset occurredAtUtc, CancellationToken ct = default)
        => Task.CompletedTask;
}