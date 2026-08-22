using System.Text.RegularExpressions;

namespace BA.Dmo.Domain.Modules.Armazem;

/// <summary>
/// U-14 — A physical position/location in the warehouse (N09
/// <c>warehouse_locations</c>). The code is exactly 4 digits (owner decision).
/// </summary>
public sealed class WarehouseLocation
{
    public Guid WarehouseLocationId { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;

    public string? Kind { get; set; }

    private static readonly Regex PositionCodeRegex =
        new($"(?:{ArmazemModuleCatalog.PositionCodePattern})", RegexOptions.Compiled);

    /// <summary>Validates a position code is exactly four digits.</summary>
    public static bool IsValidPositionCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && PositionCodeRegex.IsMatch(code.Trim());

    public static string NormalizePositionCode(string? code) => code?.Trim() ?? string.Empty;
}