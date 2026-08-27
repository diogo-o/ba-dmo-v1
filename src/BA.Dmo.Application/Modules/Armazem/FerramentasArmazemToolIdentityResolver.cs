using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.Armazem;
using BA.Dmo.Domain.Modules.Ferramentas;

namespace BA.Dmo.Application.Modules.Armazem;

/// <summary>
/// U-14 — Armazém CM/MF/BQ tool identity resolver. Adapts the Ferramentas-owned
/// <see cref="IFerramentasIdentityLookup"/> into the Armazém-owned
/// <see cref="WarehouseToolIdentity"/>. Accepts the warehouse-supported CM, MF and
/// BQ types; rejects PU/CS; read-only — never mutates Ferramentas.
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
            case "BQ":
                toolType = FerramentasToolType.BQ;
                return true;
            default:
                // PU / CS are Job On production configuration, not Armazém stock.
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
        FerramentasToolType.BQ => "BQ",
        _ => type.ToString()
    };
}
