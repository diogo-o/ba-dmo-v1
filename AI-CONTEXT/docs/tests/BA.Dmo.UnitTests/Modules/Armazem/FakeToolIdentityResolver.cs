using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Domain.Modules.Armazem;

namespace BA.Dmo.UnitTests.Modules.Armazem;

/// <summary>
/// U-14 — In-memory fake of the Armazém tool identity resolver (confined to
/// tests/*). Returns preset CM/MF/BQ identities and records query count.
/// </summary>
public sealed class FakeToolIdentityResolver : IToolIdentityResolver
{
    public List<WarehouseToolIdentity> Identities { get; } = new();
    public int Searches { get; private set; }

    public Task<IReadOnlyList<WarehouseToolIdentity>> SearchAsync(string type, string? reference, string? lot, CancellationToken ct = default)
    {
        Searches++;
        var result = Identities.Where(i =>
            i.Type == type &&
            (string.IsNullOrWhiteSpace(reference) || i.Reference.Equals(reference, StringComparison.Ordinal)) &&
            (string.IsNullOrWhiteSpace(lot) || i.Lot.Equals(lot, StringComparison.Ordinal))).ToList();
        return Task.FromResult<IReadOnlyList<WarehouseToolIdentity>>(result);
    }

    public Task<WarehouseToolIdentity?> ResolveAsync(Guid toolId, CancellationToken ct = default)
        => Task.FromResult(Identities.FirstOrDefault(i => i.ToolId == toolId));
}
