using System.Data;
using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Modules.ReparacaoExterna;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.ReparacaoExterna;

/// <summary>Fixed UTC clock for deterministic Reparação Externa service tests.</summary>
public sealed class ReparacaoExternaFixedClock(DateTimeOffset fixedUtcNow) : IClock
{
    public DateTimeOffset UtcNow => fixedUtcNow;
}

/// <summary>Fake canonical authorship accessor.</summary>
public sealed class ReparacaoExternaFakeAuthorship(string actorId = "repex-actor")
    : IPersistenceAuthorshipAccessor
{
    public PersistenceAuthorship Current { get; } =
        new(actorId, new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero));
}

/// <summary>Fake current-user accessor controlling the reparacao_externa module grant.</summary>
public sealed class ReparacaoExternaCurrentUser(string? actorId = "repex-actor")
    : ICurrentUserAccessor
{
    private readonly CurrentUser? _user =
        actorId is null ? null : new CurrentUser(
            Guid.NewGuid(), "Operador Reparação",
            new[] { ReparacaoExternaModuleCatalog.ModuleId }, Array.Empty<string>());

    public CurrentUser? Current => _user;

    public static ReparacaoExternaCurrentUser Authorized() => new("repex-actor");

    public static ReparacaoExternaCurrentUser WithoutModule() => new(null);
}

/// <summary>In-memory fake of the shared unit-of-work factory (no DB).</summary>
public sealed class FakeRepairUnitOfWorkFactory : IRepairUnitOfWorkFactory
{
    public Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDbUnitOfWork>(new FakeUnitOfWork());
}

/// <summary>No-op in-memory unit of work (confined to tests/*).</summary>
public sealed class FakeUnitOfWork : IDbUnitOfWork
{
    public IDbConnection Connection => null!;
    public IDbTransaction Transaction => null!;

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>In-memory fake of the Armazém repair movement port (Armazém-owned).</summary>
public sealed class FakeArmazemRepairMovementPort : IArmazemRepairMovementPort
{
    public List<(Guid repairExitId, Guid toolLoteId, string operatorId)> Pickups { get; } = new();
    public List<(Guid repairExitId, Guid toolLoteId, string position, string operatorId)> Returns { get; } = new();

    public bool FailOnPickup { get; set; }
    public bool FailOnReturn { get; set; }

    public Task<Result<bool, DomainError>> ConfirmPickupAsync(
        IDbUnitOfWork uow, Guid repairExitId, Guid toolLoteId, string actorId, DateTimeOffset outAtUtc, CancellationToken ct = default)
    {
        if (FailOnPickup)
            return Task.FromResult(Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "ARMZ_REPAIR_NOT_IN_WAREHOUSE", "Simulated: tool not in warehouse.")));
        Pickups.Add((repairExitId, toolLoteId, actorId));
        return Task.FromResult(Result<bool, DomainError>.Success(true));
    }

    public Task<Result<bool, DomainError>> ConfirmReturnAsync(
        IDbUnitOfWork uow, Guid repairExitId, Guid toolLoteId, string positionCode, string actorId, DateTimeOffset inAtUtc, CancellationToken ct = default)
    {
        if (FailOnReturn)
            return Task.FromResult(Result<bool, DomainError>.Failure(DomainError.DomainConflict(
                "ARMZ_REPAIR_POSITION_OCCUPIED", "Simulated: position occupied.")));
        Returns.Add((repairExitId, toolLoteId, positionCode, actorId));
        return Task.FromResult(Result<bool, DomainError>.Success(true));
    }
}

/// <summary>
/// Fake tool resolver (U-15-owned, over Ferramentas). Seeds CM/MF physical pieces by
/// reference/lote/number for the list-builder and pickup/return resolution.
/// </summary>
public sealed class FakeToolPieceResolver : IToolPieceResolver
{
    public List<RepairToolIdentity> Pieces { get; } = new();

    public Task<IReadOnlyList<RepairToolIdentity>> SearchAsync(
        Domain.Modules.ReparacaoExterna.RepairType type, string? reference, string? lot, string? number, CancellationToken ct = default)
    {
        var result = Pieces.Where(p =>
            p.Type == type &&
            (string.IsNullOrWhiteSpace(reference) || p.Reference.Contains(reference)) &&
            (string.IsNullOrWhiteSpace(lot) || p.Lot.Contains(lot)) &&
            (string.IsNullOrWhiteSpace(number) || p.Number.Contains(number))).ToList();
        return Task.FromResult<IReadOnlyList<RepairToolIdentity>>(result);
    }

    public Task<RepairToolIdentity?> ResolveAsync(Guid physicalPieceId, CancellationToken ct = default)
        => Task.FromResult(Pieces.FirstOrDefault(p => p.PhysicalPieceId == physicalPieceId));

    public RepairToolIdentity Seed(string reference, string lot, string number, Domain.Modules.ReparacaoExterna.RepairType type = Domain.Modules.ReparacaoExterna.RepairType.CM)
    {
        var piece = new RepairToolIdentity(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), type, reference, lot, number, reference);
        Pieces.Add(piece);
        return piece;
    }
}