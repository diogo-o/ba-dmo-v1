using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Ferramentas;

/// <summary>Fixed UTC clock for deterministic service tests.</summary>
public sealed class FixedClock(DateTimeOffset fixedUtcNow) : IClock
{
    public DateTimeOffset UtcNow => fixedUtcNow;
}

/// <summary>Fake canonical authorship accessor.</summary>
public sealed class FakeAuthorshipAccessor(string? actorId) : IPersistenceAuthorshipAccessor
{
    public PersistenceAuthorship Current { get; } =
        new(actorId, new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero));
}

/// <summary>Fake current-user accessor controlling module + capability grants.</summary>
public sealed class FakeCurrentUser(string? actorId = "ferr-actor", bool canConfigure = false)
    : ICurrentUserAccessor
{
    private readonly CurrentUser? _user =
        actorId is null ? null : new CurrentUser(
            Guid.NewGuid(),
            "Utilizador Ferramentas",
            new[] { FerramentasModuleCatalog.ModuleId },
            canConfigure ? new[] { CanonicalModuleCatalog.FerramentasConfigureCapabilityId } : Array.Empty<string>());

    public CurrentUser? Current => _user;

    public static FakeCurrentUser Authorized() => new("ferr-actor", canConfigure: false);

    public static FakeCurrentUser Configurator() => new("ferr-actor", canConfigure: true);

    public static FakeCurrentUser WithoutModule() => new(null);
}

/// <summary>Fake rule lookup returning configured Job On verification rules.</summary>
public sealed class FakeRuleLookup : IFerramentasRuleLookup
{
    public IReadOnlyList<Domain.Modules.JobOn.VerificationRule> Rules { get; set; } = Array.Empty<Domain.Modules.JobOn.VerificationRule>();

    public Task<IReadOnlyList<Domain.Modules.JobOn.VerificationRule>> ResolveActiveRulesAsync(Guid toolLoteId, CancellationToken ct = default)
        => Task.FromResult(Rules);
}