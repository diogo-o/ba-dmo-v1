using System.Data;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Modules.ReparacaoInterna;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Modules.ReparacaoInterna;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.ReparacaoInterna;

/// <summary>Fixed UTC clock for deterministic Reparação Interna service tests.</summary>
public sealed class ReparacaoInternaFixedClock(DateTimeOffset fixedUtcNow) : IClock
{
    public DateTimeOffset UtcNow => fixedUtcNow;
}

/// <summary>Fake canonical authorship accessor.</summary>
public sealed class ReparacaoInternaFakeAuthorship(string actorId = "repan-actor")
    : IPersistenceAuthorshipAccessor
{
    public PersistenceAuthorship Current { get; } =
        new(actorId, new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero));
}

/// <summary>In-memory fake of the shared unit-of-work factory (no DB).</summary>
public sealed class FakeReparacaoInternaUowFactory : IRepairUnitOfWorkFactory
{
    public Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDbUnitOfWork>(new FakeReparacaoInternaUnitOfWork());
}

/// <summary>No-op in-memory unit of work (confined to tests/*).</summary>
public sealed class FakeReparacaoInternaUnitOfWork : IDbUnitOfWork
{
    public IDbConnection Connection => null!;
    public IDbTransaction Transaction => null!;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Current-user accessor for the reparacao_interna module grant and the
/// reparacao_interna.corrigir capability. The <paramref name="grantCorrigir"/>
/// flag controls the capability.
/// </summary>
public sealed class ReparacaoInternaCurrentUser(
    string? actorId = "repan-actor", bool grantCorrigir = false)
    : ICurrentUserAccessor
{
    private readonly CurrentUser? _user = actorId is null ? null : new CurrentUser(
        Guid.NewGuid(), "Reparador de turno",
        new[] { ReparacaoInternaModuleCatalog.ModuleId },
        grantCorrigir ? new[] { ReparacaoInternaModuleCatalog.CorrigirCapabilityId } : Array.Empty<string>());

    public CurrentUser? Current => _user;

    public static ReparacaoInternaCurrentUser Authorized(bool corrigir = false) =>
        new("repan-actor", corrigir);

    public static ReparacaoInternaCurrentUser WithoutModule() => new(null);

    public static ReparacaoInternaCurrentUser WithoutCorrigir() => new("repan-actor", false);
}

/// <summary>
/// In-memory fake of <see cref="IReparacaoInternaRepository"/> (confined to tests/*).
/// Tracks records, repair events and audit events; supports atomically failing the
/// insert (via <see cref="FailInsert"/>) to assert save-failure preserves state.
/// </summary>
public sealed class FakeReparacaoInternaRepository : IReparacaoInternaRepository
{
    public List<InternalRepairRecord> Records { get; } = new();
    public List<(Guid? recordId, string? notes, string actor)> RepairEvents { get; } = new();
    public List<(string action, string entityId, string result, string actor)> AuditEvents { get; } = new();

    public bool FailInsert { get; set; }

    public Task<Guid> InsertAsync(IDbUnitOfWork uow, InternalRepairRecord record, CancellationToken ct = default)
    {
        if (FailInsert) throw new InvalidOperationException("simulated insert failure");
        Records.Add(record);
        return Task.FromResult(record.InternalRepairRecordId);
    }

    public Task<InternalRepairRecord?> GetByIdAsync(Guid recordId, CancellationToken ct = default)
        => Task.FromResult(Records.FirstOrDefault(r => r.InternalRepairRecordId == recordId));

    public Task<IReadOnlyList<InternalRepairRecord>> GetChainAsync(Guid rootRecordId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<InternalRepairRecord>>(
            Records
                .Where(r => r.InternalRepairRecordId == rootRecordId || r.CorrectionOfId == rootRecordId)
                .OrderBy(r => r.CreatedAtUtc)
                .ToList());

    public Task<IReadOnlyList<InternalRepairRecord>> ListAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? line, Guid? jobOnId,
        InternalRepairToolType? type, string? number, string? operatorId,
        bool onlyCorrected, CancellationToken ct = default)
    {
        // Latest valid version per chain root = the most recently inserted record
        // of each chain (correction supersedes its original).
        var seen = new HashSet<Guid>();
        var result = new List<InternalRepairRecord>();
        foreach (var record in Records.AsEnumerable().Reverse())
        {
            var rootId = record.CorrectionOfId ?? record.InternalRepairRecordId;
            if (!seen.Add(rootId)) continue;
            if (!Matches(record, from, to, line, jobOnId, type, number, operatorId, onlyCorrected))
                continue;
            result.Add(record);
        }
        return Task.FromResult<IReadOnlyList<InternalRepairRecord>>(result);
    }

    private static bool Matches(InternalRepairRecord record,
        DateTimeOffset? from, DateTimeOffset? to, string? line, Guid? jobOnId,
        InternalRepairToolType? type, string? number, string? operatorId, bool onlyCorrected) =>
        (from is null || record.OccurredAtUtc >= from) &&
        (to is null || record.OccurredAtUtc <= to) &&
        (line is null || record.Line == line) &&
        (jobOnId is null || record.JobOnId == jobOnId) &&
        (type is null || record.ToolType == type) &&
        (number is null || record.IndividualNumber == number) &&
        (operatorId is null || record.OperatorId == operatorId) &&
        (!onlyCorrected || record.IsCorrection);

    public Task InsertRepairEventAsync(
        IDbUnitOfWork uow, Guid? internalRepairRecordId, string? notes,
        string actorId, DateTimeOffset occurredAtUtc, CancellationToken ct = default)
    {
        RepairEvents.Add((internalRepairRecordId, notes, actorId));
        return Task.CompletedTask;
    }

    public Task InsertAuditEventAsync(
        IDbUnitOfWork uow, string actionCode, string entityType, string entityId,
        Guid? jobOnId, string result, string? beforeSummary, string? afterSummary,
        string actorId, DateTimeOffset occurredAtUtc, CancellationToken ct = default)
    {
        AuditEvents.Add((actionCode, entityId, result, actorId));
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory fake of <see cref="IJobOnActiveContextLookup"/>. Seeds per-line
/// resolutions so tests can drive Single / None / Ambiguous states deterministically.
/// </summary>
public sealed class FakeJobOnActiveContextLookup : IJobOnActiveContextLookup
{
    public Dictionary<string, InternalRepairContextResolution> ByLine { get; } = new();

    public Task<InternalRepairContextResolution> ResolveActiveAsync(
        string line, DateTimeOffset at, CancellationToken ct = default)
        => Task.FromResult(ByLine.TryGetValue(line, out var resolution)
            ? resolution
            : InternalRepairContextResolution.None());

    public void SeedSingle(string line, InternalRepairContext context) =>
        ByLine[line] = InternalRepairContextResolution.Single(context);

    public void SeedNone(string line) =>
        ByLine[line] = InternalRepairContextResolution.None();

    public void SeedAmbiguous(string line, params InternalRepairContextCandidate[] candidates) =>
        ByLine[line] = InternalRepairContextResolution.Ambiguous(candidates);

    public static InternalRepairContext Context(
        string line, Guid jobOnId, string reference = "REF-1", string production = "202608",
        IReadOnlyList<Guid>? cmLots = null, IReadOnlyList<Guid>? mfLots = null,
        IReadOnlyList<Guid>? bqLots = null, Guid? revisionId = null) =>
        new(jobOnId, revisionId ?? Guid.NewGuid(), line, production, reference, line,
            cmLots ?? new List<Guid>(), mfLots ?? new List<Guid>(), bqLots ?? new List<Guid>(),
            new DateTimeOffset(2026, 8, 18, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 18, 18, 0, 0, TimeSpan.Zero));
}

/// <summary>
/// In-memory fake of <see cref="IFerramentasPieceLookup"/> (Ferramentas-owned,
/// read-only). Seeds CM/MF pieces by type + number + parent lot.
/// </summary>
public sealed class FakeFerramentasPieceLookup : IFerramentasPieceLookup
{
    public List<FerramentasPieceHit> Pieces { get; } = new();

    public Task<IReadOnlyList<FerramentasPieceHit>> SearchAsync(
        FerramentasToolType type, string? reference, string? lot, string? number, CancellationToken ct = default)
    {
        var result = Pieces.Where(p =>
            p.Type == type &&
            (string.IsNullOrWhiteSpace(reference) || p.Reference.Contains(reference)) &&
            (string.IsNullOrWhiteSpace(lot) || p.Lot.Contains(lot)) &&
            (string.IsNullOrWhiteSpace(number) || p.Number.Contains(number))).ToList();
        return Task.FromResult<IReadOnlyList<FerramentasPieceHit>>(result);
    }

    public Task<FerramentasPieceHit?> ResolveAsync(Guid physicalPieceId, CancellationToken ct = default)
        => Task.FromResult(Pieces.FirstOrDefault(p => p.PhysicalPieceId == physicalPieceId));

    public FerramentasPieceHit Seed(
        string reference, string number, Guid toolLoteId,
        FerramentasToolType type = FerramentasToolType.CM, string? lot = null)
    {
        var hit = new FerramentasPieceHit(
            Guid.NewGuid(), toolLoteId, Guid.NewGuid(), type, reference, lot ?? "L-1", number, reference);
        Pieces.Add(hit);
        return hit;
    }
}