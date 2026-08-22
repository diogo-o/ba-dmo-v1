using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.ReparacaoExterna;

namespace BA.Dmo.Application.Modules.ReparacaoExterna;

/// <summary>
/// U-15 — Reparação External CM/MF tool-piece resolver. Adapts the Ferramentas-owned
/// <see cref="IFerramentasPieceLookup"/> (read-only) into the U-15-owned
/// <see cref="RepairToolIdentity"/>. Accepts CM and MF only (BQ reserved for U-19).
/// Read-only — never mutates Ferramentas.
/// </summary>
public sealed class FerramentasRepairToolPieceResolver : IToolPieceResolver
{
    private readonly IFerramentasPieceLookup _pieceLookup;

    public FerramentasRepairToolPieceResolver(IFerramentasPieceLookup pieceLookup)
    {
        _pieceLookup = pieceLookup ?? throw new ArgumentNullException(nameof(pieceLookup));
    }

    public async Task<IReadOnlyList<RepairToolIdentity>> SearchAsync(
        RepairType type, string? reference, string? lot, string? number, CancellationToken ct = default)
    {
        if (!TryMap(type, out var toolType))
            return Array.Empty<RepairToolIdentity>();

        var hits = await _pieceLookup.SearchAsync(toolType.Value, reference, lot, number, ct);
        return hits.Select(Map).ToList().AsReadOnly();
    }

    public async Task<RepairToolIdentity?> ResolveAsync(Guid physicalPieceId, CancellationToken ct = default)
    {
        var hit = await _pieceLookup.ResolveAsync(physicalPieceId, ct);
        return hit is null ? null : Map(hit);
    }

    private static bool TryMap(RepairType type, out FerramentasToolType? toolType)
    {
        switch (type)
        {
            case RepairType.CM:
                toolType = FerramentasToolType.CM;
                return true;
            case RepairType.MF:
                toolType = FerramentasToolType.MF;
                return true;
            default:
                toolType = null;
                return false;
        }
    }

    private static RepairToolIdentity Map(FerramentasPieceHit hit) => new(
        PhysicalPieceId: hit.PhysicalPieceId,
        ToolLoteId: hit.ToolLoteId,
        ToolReferenceId: hit.ToolReferenceId,
        Type: hit.Type == FerramentasToolType.CM ? RepairType.CM : RepairType.MF,
        Reference: hit.Reference,
        Lot: hit.Lot,
        Number: hit.Number,
        TechnicalName: hit.TechnicalName);
}