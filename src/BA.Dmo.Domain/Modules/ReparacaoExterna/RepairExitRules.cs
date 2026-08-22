namespace BA.Dmo.Domain.Modules.ReparacaoExterna;

/// <summary>
/// Classification of external repair integrity rules (GLM-RE-06, GLM-RE-09;
/// GLM-CORE-02).
/// HARD BLOCKS (TECHNICAL INTEGRITY / CONFIRMED BUSINESS RULE): duplicate item in
/// an open exit; cycle integrity (return requires a matching open exit — never an
/// invented exit); repairer snapshot integrity.
/// WARNING ONLY (never blocks persistence): item with unknown location.
/// </summary>
public static class RepairExitRules
{
    /// <summary>True when the item has no known physical location (warning, not a hard block).</summary>
    public static bool HasUnknownLocation(string? positionCode) =>
        string.IsNullOrWhiteSpace(positionCode);

    /// <summary>Hard block: an item may not belong to more than one open exit.</summary>
    public const string DuplicateInOpenExitCode = "REPEXT_ITEM_IN_OPEN_EXIT";

    /// <summary>Hard block: a return requires a matching open exit.</summary>
    public const string ReturnWithoutExitCode = "REPEXT_RETURN_WITHOUT_EXIT";
}