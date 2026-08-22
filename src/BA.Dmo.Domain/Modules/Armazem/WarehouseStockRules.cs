using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Armazem;

/// <summary>
/// U-14 — Pure warehouse stock rules (GLM-ARM-04/08, GLM-DATA-04). Occupation is
/// 1:1 per position; <c>fora</c> is derived from active occupation facts; two
/// different references on the same position is a DATA-QUALITY WARNING, never a
/// silent normalization (owner decision E).
/// </summary>
public static class WarehouseStockRules
{
    /// <summary>Any active (not released) stock row means the position is occupied.</summary>
    public static bool IsPositionOccupied(IEnumerable<WarehouseStock> activeStocks) =>
        activeStocks.Any(s => s.IsActive);

    /// <summary>A tool is "fora" when it has NO active occupation row.</summary>
    public static bool IsFora(IEnumerable<WarehouseStock> stocks) =>
        !stocks.Any(s => s.IsActive);

    /// <summary>True when the candidate reference differs from an active occupant's reference.</summary>
    public static bool HasReferenceConflict(
        IEnumerable<WarehouseStock> activeStocks,
        Guid candidateToolLoteId,
        string candidateReference,
        Func<Guid, string?> referenceResolver)
    {
        foreach (var stock in activeStocks)
        {
            if (!stock.IsActive || stock.ToolId == candidateToolLoteId)
                continue;
            var occupantReference = referenceResolver(stock.ToolId);
            if (string.IsNullOrEmpty(occupantReference))
                continue;
            if (!string.Equals(occupantReference, candidateReference, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}