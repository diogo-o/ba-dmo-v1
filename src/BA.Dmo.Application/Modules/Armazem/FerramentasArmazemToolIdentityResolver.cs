using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.Armazem;
using BA.Dmo.Domain.Modules.Ferramentas;

namespace BA.Dmo.Application.Modules.Armazem;

/// <summary>
/// U-14 — Armazém CM/MF tool identity resolver. Adapts the Ferramentas-owned
/// <see cref="IFerramentasIdentityLookup"/> into the Armazém-owned
/// <see cref="WarehouseToolIdentity"/>. Accepts CM and MF only (owner decision C:
/// BQ/PU/CS rejected); read-only — never mutates Ferramentas.
/// </summary>
public sealed class FerramentasArmazemToolIdentityResolver : IToolIdentityResolver
{
    private readonly IFerramentasIdentityLookup _identityLookup;

    public FerramentasArmazemToolIdentityResolver(IFerramentasIdentityLookup identityLookup)
    {
        _identityLookup = identityLookup ?? throw new ArgumentNullException(nameof(identityLookup));
    }

    public async Task<IReadOnlyList<WarehouseToolIdentity>> SearchAsync(
        string type, string? reference, string? lot, CancellationToken ct = default)
    {
        if (!TryParseSupportedType(type, out var toolType))
            return Array.Empty<WarehouseToolIdentity>();

        var hits = await _identityLookup.SearchAsync(toolType, reference, lot, ct);
        return hits.Select(h => Map(h)).ToList().AsReadOnly();
    }

    public async Task<WarehouseToolIdentity?> ResolveAsync(Guid toolId, CancellationToken ct = default)
    {
        var hit = await _identityLookup.ResolveAsync(toolId, ct);
        return hit is null ? null : Map(hit);
    }

    private static bool TryParseSupportedType(string? type, out FerramentasToolType toolType)
    {
        toolType = default;
        if (string.IsNullOrWhiteSpace(type))
            return false;
        var normalized = type.Trim().ToUpperInvariant();
        switch (normalized)
        {
            case "CM":
                toolType = FerramentasToolType.CM;
                return true;
            case "MF":
                toolType = FerramentasToolType.MF;
                return true;
            default:
                // BQ / PU / CS are NOT supported by U-14 (owner decision C).
                return false;
        }
    }

    private static WarehouseToolIdentity Map(FerramentasIdentityHit hit) =>
        new(
            ToolId: hit.ToolLoteId,
            Domain: WarehouseToolDomain.Ferramentas,
            Type: ToDisplayType(hit.Type),
            Reference: hit.Reference,
            Lot: hit.Lot,
            TechnicalName: hit.TechnicalName);

    private static string ToDisplayType(FerramentasToolType type) => type switch
    {
        FerramentasToolType.CM => "CM",
        FerramentasToolType.MF => "MF",
        _ => type.ToString()
    };
}