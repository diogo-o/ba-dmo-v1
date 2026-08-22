using System.Data;
using BA.Dmo.Application.Modules.Boquilhas;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Boquilhas;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Boquilhas;

/// <summary>Fixed UTC clock for deterministic Boquilhas service tests.</summary>
public sealed class BqFixedClock(DateTimeOffset fixedUtcNow) : IClock
{
    public DateTimeOffset UtcNow => fixedUtcNow;
}

/// <summary>Fake canonical authorship accessor.</summary>
public sealed class BqFakeAuthorship(string actorId = "bq-actor")
    : IPersistenceAuthorshipAccessor
{
    public PersistenceAuthorship Current { get; } =
        new(actorId, new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero));
}

/// <summary>Fake current-user accessor controlling the boquilhas module grant.</summary>
public sealed class BqCurrentUser(string? actorId = "bq-actor")
    : ICurrentUserAccessor
{
    private readonly CurrentUser? _user = actorId is null ? null : new CurrentUser(
        Guid.NewGuid(), "Operador Boquilhas",
        new[] { BoquilhasModuleCatalog.ModuleId }, Array.Empty<string>());

    public CurrentUser? Current => _user;

    public static BqCurrentUser Authorized() => new("bq-actor");
    public static BqCurrentUser WithoutModule() => new(null);
}

/// <summary>No-op in-memory unit of work (confined to tests/*).</summary>
public sealed class FakeBqUnitOfWork : IDbUnitOfWork
{
    public IDbConnection Connection => null!;
    public IDbTransaction Transaction => null!;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class FakeBqUnitOfWorkFactory : IBoquilhasUnitOfWorkFactory
{
    public Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDbUnitOfWork>(new FakeBqUnitOfWork());
}

/// <summary>
/// In-memory fake of <see cref="IBoquilhasRepository"/> (confined to tests/*).
/// Tracks lots, traces, movements, discrepancies, repairers and line defaults.
/// </summary>
public sealed class FakeBoquilhasRepository : IBoquilhasRepository
{
    public List<BqLote> Lotes { get; } = new();
    public List<BqTrace> Traces { get; } = new();
    public List<BqMovement> Movements { get; } = new();
    public List<BqDiscrepancy> Discrepancies { get; } = new();
    public List<BqLifecycleEvent> LifecycleEvents { get; } = new();
    public List<BqUtilisationReading> Utilisation { get; } = new();
    public List<BqRepairer> Repairers { get; } = new();
    public List<BqLineRepairerDefault> LineDefaults { get; } = new();
    public List<(string action, string entityId, string result)> AuditEvents { get; } = new();

    public bool FailTransaction { get; set; }

    // ---- Lots ----------------------------------------------------------------
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

    public Task<int> CountLotesAsync(BqLoteFilter filter, CancellationToken ct = default)
        => Task.FromResult(Lotes.Count);

    public Task CreateLoteAsync(IDbUnitOfWork uow, BqLote lote, CancellationToken ct = default)
    {
        if (FailTransaction) throw new InvalidOperationException("simulated");
        Lotes.Add(lote);
        return Task.CompletedTask;
    }

    public Task UpdateLoteAsync(IDbUnitOfWork uow, BqLote lote, CancellationToken ct = default)
    {
        var existing = Lotes.FirstOrDefault(l => l.BqLoteId == lote.BqLoteId);
        if (existing is not null) { existing.Reference = lote.Reference; existing.BatchCode = lote.BatchCode; existing.AllowedLines = lote.AllowedLines; }
        return Task.CompletedTask;
    }

    public Task UpdateLifecycleStateAsync(IDbUnitOfWork uow, Guid bqLoteId, BqLifecycleState state, CancellationToken ct = default)
    {
        var lot = Lotes.FirstOrDefault(l => l.BqLoteId == bqLoteId);
        if (lot is not null) lot.LifecycleState = state;
        return Task.CompletedTask;
    }

    public Task InsertLifecycleEventAsync(IDbUnitOfWork uow, BqLifecycleEvent evt, CancellationToken ct = default)
    {
        if (FailTransaction) throw new InvalidOperationException("simulated");
        LifecycleEvents.Add(evt);
        return Task.CompletedTask;
    }

    // ---- Traces ----------------------------------------------------------------
    public Task<BqTrace?> GetTraceByIdAsync(Guid bqTraceId, CancellationToken ct = default)
        => Task.FromResult(Traces.FirstOrDefault(t => t.BqTraceId == bqTraceId));

    public Task<BqTrace?> GetActiveTraceForLoteAsync(Guid bqLoteId, CancellationToken ct = default)
        => Task.FromResult(Traces.FirstOrDefault(t => t.BqLoteId == bqLoteId && t.Status == BqTraceStatus.Active));

    public Task<BqTrace?> GetLastClosedOrActiveTraceAsync(Guid bqLoteId, CancellationToken ct = default)
        => Task.FromResult(Traces
            .Where(t => t.BqLoteId == bqLoteId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Cast<BqTrace?>().FirstOrDefault());

    public Task<BqTrace?> GetTraceForMovementAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default)
        => Task.FromResult(Traces.FirstOrDefault(t => t.BqTraceId == bqTraceId));

    public Task CreateTraceAsync(IDbUnitOfWork uow, BqTrace trace, CancellationToken ct = default)
    {
        if (FailTransaction) throw new InvalidOperationException("simulated");
        Traces.Add(trace);
        return Task.CompletedTask;
    }

    public Task CloseTraceAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default)
    {
        var t = Traces.FirstOrDefault(x => x.BqTraceId == bqTraceId);
        if (t is not null) t.Status = BqTraceStatus.Closed;
        return Task.CompletedTask;
    }

    public Task ReopenTraceAsync(IDbUnitOfWork uow, Guid bqTraceId, CancellationToken ct = default)
    {
        var t = Traces.FirstOrDefault(x => x.BqTraceId == bqTraceId);
        if (t is not null) t.Status = BqTraceStatus.Active;
        return Task.CompletedTask;
    }

    public Task AppendReopenHistoryAsync(IDbUnitOfWork uow, Guid bqTraceId, string actorId, DateTimeOffset atUtc, CancellationToken ct = default)
        => Task.CompletedTask;

    // ---- Movements ----------------------------------------------------------------
    public Task InsertMovementAsync(IDbUnitOfWork uow, BqMovement movement, CancellationToken ct = default)
    {
        if (FailTransaction) throw new InvalidOperationException("simulated");
        Movements.Add(movement);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BqMovement>> ListMovementsForTraceAsync(Guid bqTraceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BqMovement>>(Movements.Where(m => m.BqTraceId == bqTraceId)
            .OrderBy(m => m.OccurredAtUtc).ToList());

    public Task<IReadOnlyList<BqMovement>> ListMovementsByLoteAsync(Guid bqLoteId, BqHistoryFilter filter, CancellationToken ct = default)
        => ListMovementsAsync(filter with { BqLoteId = bqLoteId }, ct);

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

    public Task<int> CountMovementsAsync(BqHistoryFilter filter, CancellationToken ct = default)
        => Task.FromResult(Movements.Count);

    public Task VoidMovementAsync(IDbUnitOfWork uow, Guid bqTraceId, Guid bqMovementId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<IReadOnlySet<Guid>> ListVoidedMovementIdsAsync(Guid bqTraceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

    // ---- Utilisation ----------------------------------------------------------------
    public Task InsertUtilisationReadingAsync(IDbUnitOfWork uow, BqUtilisationReading reading, CancellationToken ct = default)
    {
        Utilisation.Add(reading);
        return Task.CompletedTask;
    }

    public Task<BqUtilisationReading?> GetUtilisationReadingAsync(Guid bqTraceId, BqUtilisationReadingKind kind, CancellationToken ct = default)
        => Task.FromResult(Utilisation.LastOrDefault(u => u.BqTraceId == bqTraceId && u.ReadingKind == kind));

    // ---- Discrepancies ----------------------------------------------------------------
    public Task<BqDiscrepancy?> GetOpenDiscrepancyForTraceAsync(Guid bqLoteId, Guid? bqTraceId, CancellationToken ct = default)
        => Task.FromResult(Discrepancies.LastOrDefault(d => d.BqLoteId == bqLoteId &&
            (bqTraceId is null || d.BqTraceId == bqTraceId) && d.Status == BqDiscrepancyStatus.Open));

    public Task InsertDiscrepancyAsync(IDbUnitOfWork uow, BqDiscrepancy discrepancy, CancellationToken ct = default)
    {
        if (FailTransaction) throw new InvalidOperationException("simulated");
        Discrepancies.Add(discrepancy);
        return Task.CompletedTask;
    }

    public Task UpdateDiscrepancyAsync(IDbUnitOfWork uow, BqDiscrepancy discrepancy, CancellationToken ct = default)
    {
        var d = Discrepancies.FirstOrDefault(x => x.BqDiscrepancyId == discrepancy.BqDiscrepancyId);
        if (d is not null) { d.Status = discrepancy.Status; d.ResolutionNote = discrepancy.ResolutionNote; }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BqDiscrepancy>> ListDiscrepanciesAsync(Guid? bqLoteId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BqDiscrepancy>>(Discrepancies
            .Where(d => bqLoteId is null || d.BqLoteId == bqLoteId).ToList());

    // ---- Repairers ----------------------------------------------------------------
    public Task<IReadOnlyList<BqRepairer>> ListRepairersAsync(bool onlyActive, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BqRepairer>>(Repairers.Where(r => !onlyActive || r.Active).ToList());

    public Task<BqRepairer?> GetRepairerByIdAsync(Guid repairerId, CancellationToken ct = default)
        => Task.FromResult(Repairers.FirstOrDefault(r => r.RepairerId == repairerId));

    public Task<Guid> CreateRepairerAsync(BqRepairer repairer, CancellationToken ct = default)
    {
        Repairers.Add(repairer);
        return Task.FromResult(repairer.RepairerId);
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
        var existing = LineDefaults.FirstOrDefault(d => d.Line == lineDefault.Line);
        if (existing is null) LineDefaults.Add(lineDefault);
        else existing.DefaultRepairerId = lineDefault.DefaultRepairerId;
        return Task.CompletedTask;
    }

    // ---- Audit ----------------------------------------------------------------
    public Task InsertAuditEventAsync(IDbUnitOfWork uow, string actionCode, string entityType, string entityId,
        string result, string? beforeSummary, string? afterSummary, string actorId,
        DateTimeOffset occurredAtUtc, CancellationToken ct = default)
    {
        AuditEvents.Add((actionCode, entityId, result));
        return Task.CompletedTask;
    }

    // ---- Test helpers -------------------------------------------------------------
    public BqLote SeedLote(string reference = "T194", string batch = "12", params string[] lines)
    {
        var lote = new BqLote
        {
            Reference = reference,
            BatchCode = batch,
            AllowedLines = lines.Length > 0 ? lines : new[] { "B1" },
            LifecycleState = BqLifecycleState.Available,
            CreatedAtUtc = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero)
        };
        Lotes.Add(lote);
        return lote;
    }

    public BqTrace SeedActiveTrace(BqLote lote, decimal initialQty, DateTimeOffset? at = null)
    {
        var startAt = at ?? new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero); // before any later movement
        var trace = new BqTrace
        {
            BqLoteId = lote.BqLoteId,
            Status = BqTraceStatus.Active,
            Purpose = BqTracePurpose.Production,
            StartLine = lote.AllowedLines.FirstOrDefault(),
            CreatedAtUtc = startAt,
            UpdatedAtUtc = startAt
        };
        Traces.Add(trace);
        Movements.Add(new BqMovement
        {
            BqTraceId = trace.BqTraceId,
            MovementType = BqMovementType.Inicio,
            Qty = initialQty,
            ActorId = "bq-actor",
            OccurredAtUtc = startAt
        });
        return trace;
    }

    public BqRepairer SeedRepairer(string name = "Reparador A") =>
        new() { Name = name, Active = true, CreatedAtUtc = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero) };
}