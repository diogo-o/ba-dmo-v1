using System.Data;
using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Application.Shared;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Pegamentos;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Pegamentos;

/// <summary>No-op in-memory unit of work for the Pegamentos flows (confined to tests/*).</summary>
public sealed class FakePegamentoUnitOfWork : IDbUnitOfWork
{
    public IDbConnection Connection => null!;
    public IDbTransaction Transaction => null!;
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>In-memory fake of the Pegamentos unit-of-work factory (no DB).</summary>
public sealed class FakePegamentoUnitOfWorkFactory : IPegamentoUnitOfWorkFactory
{
    public Task<IDbUnitOfWork> BeginAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDbUnitOfWork>(new FakePegamentoUnitOfWork());
}

/// <summary>Fixed global output root settings reader.</summary>
public sealed class FakeSettings(string? outputRoot) : IAppSettingsReader
{
    public Task<string?> GetOutputRootAsync(CancellationToken ct = default)
        => Task.FromResult(outputRoot);
}

/// <summary>Fixed UTC clock for deterministic service tests.</summary>
public sealed class FixedClock(DateTimeOffset fixedUtcNow) : IClock
{
    public DateTimeOffset UtcNow => fixedUtcNow;
}

/// <summary>Fake authorship accessor with a fixed canonical actor_id.</summary>
public sealed class FakeAuthorshipAccessor(string? actorId) : IPersistenceAuthorshipAccessor
{
    public PersistenceAuthorship Current { get; } =
        new(actorId, new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero));

    public static FakeAuthorshipAccessor Authorized(string actorId = "peg-actor") => new(actorId);

    public static FakeAuthorshipAccessor Anonymous() => new(null);
}

/// <summary>
/// In-memory fake of the Job On production context lookup. Maps a revision id
/// to a resolved historical context; returns null when not configured (or when
/// configured as incomplete).
/// </summary>
public sealed class FakeJobOnProductionContextLookup : IJobOnProductionContextLookup
{
    public Dictionary<Guid, PegamentoProductionContext?> ContextByRevision { get; } = new();

    public Task<PegamentoProductionContext?> ResolveAsync(Guid jobOnRevisionId, CancellationToken ct = default)
        => Task.FromResult(ContextByRevision.GetValueOrDefault(jobOnRevisionId));
}

/// <summary>Captures the last rendered data/payload for assertion.</summary>
public sealed class FakePegamentoPdfRenderer(Func<PegamentoPdfData, byte[]> render) : IPegamentoPdfRenderer
{
    public PegamentoPdfData? LastData { get; private set; }

    public byte[] RenderPegamento(PegamentoPdfData data)
    {
        LastData = data;
        return render(data);
    }

    public static FakePegamentoPdfRenderer NonEmpty() =>
        new(data => new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF"
}

/// <summary>Builds a complete historical Pegamento production context.</summary>
public static class PegamentoContextBuilder
{
    public static PegamentoProductionContext Complete(
        Guid jobOnId,
        Guid revisionId,
        string reference = "5447T173",
        string production = "202601",
        string machine = "B1")
    {
        return new PegamentoProductionContext(
            JobOnId: jobOnId,
            JobOnRevisionId: revisionId,
            ProductionCode: production,
            MachineCode: machine,
            Reference: reference,
            CmSnapshot: new PegamentoToolSnapshot(PegamentoComponentKey.CM, "5447", "4"),
            BqSnapshot: new PegamentoToolSnapshot(PegamentoComponentKey.BQ, "T173", "4"),
            MfSnapshot: new PegamentoToolSnapshot(PegamentoComponentKey.MF, "MF-1", null),
            CmNominal: 52.00m,
            BqNominal: 38.50m,
            MfNominal: 60.00m);
    }
}