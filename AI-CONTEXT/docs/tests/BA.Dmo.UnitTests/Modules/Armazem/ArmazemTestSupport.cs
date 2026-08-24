using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Armazem;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Armazem;

/// <summary>Fixed UTC clock for deterministic Armazém service tests.</summary>
public sealed class ArmazemFixedClock(DateTimeOffset fixedUtcNow) : IClock
{
    public DateTimeOffset UtcNow => fixedUtcNow;
}

/// <summary>Fake canonical authorship accessor.</summary>
public sealed class ArmazemFakeAuthorship(string? actorId)
    : IPersistenceAuthorshipAccessor
{
    public PersistenceAuthorship Current { get; } =
        new(actorId, new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero));
}

/// <summary>Fake current-user accessor controlling the armazem module grant.</summary>
public sealed class ArmazemCurrentUser(string? actorId = "arm-actor")
    : ICurrentUserAccessor
{
    private readonly CurrentUser? _user =
        actorId is null ? null : new CurrentUser(
            Guid.NewGuid(), "Operador Armazém", new[] { ArmazemModuleCatalog.ModuleId }, Array.Empty<string>());

    public CurrentUser? Current => _user;

    public static ArmazemCurrentUser Authorized() => new("arm-actor");

    public static ArmazemCurrentUser WithoutModule() => new(null);
}

/// <summary>Fake Ferramentas identity lookup (Ferramentas-owned port) used to
/// exercise the resolver adapter (confined to tests/*).</summary>
public sealed class FakeFerramentasIdentityLookup : IFerramentasIdentityLookup
{
    public List<FerramentasIdentityHit> Hits { get; } = new();

    public Task<IReadOnlyList<FerramentasIdentityHit>> SearchAsync(
        Domain.Modules.Ferramentas.FerramentasToolType type,
        string? reference, string? lot, CancellationToken ct = default)
    {
        var result = Hits.Where(h =>
            h.Type == type &&
            (string.IsNullOrWhiteSpace(reference) || h.Reference.Contains(reference)) &&
            (string.IsNullOrWhiteSpace(lot) || h.Lot.Contains(lot))).ToList();
        return Task.FromResult<IReadOnlyList<FerramentasIdentityHit>>(result);
    }

    public Task<FerramentasIdentityHit?> ResolveAsync(Guid toolLoteId, CancellationToken ct = default)
        => Task.FromResult(Hits.FirstOrDefault(h => h.ToolLoteId == toolLoteId));
}