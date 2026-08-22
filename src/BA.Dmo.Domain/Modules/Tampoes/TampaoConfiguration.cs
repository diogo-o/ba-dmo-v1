namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// U-17 — A Tampões technical configuration (N10 <c>tampao_configurations</c>;
/// GLM-TP-04, TAMPOES_DESIGN_BRIEF §2/§6). A configuration is a STABLE combination
/// of characteristic values with its own id; <c>values_json</c> is UNIQUE so an
/// existing destination configuration is REUSED by id rather than duplicated.
/// Values are keyed by field name (e.g. "Diâmetro", "Profundidade/Calote") and are
/// normalized numbers. Deactivating/obsolescence never rewrites history.
/// </summary>
public sealed class TampaoConfiguration
{
    public Guid TampaoConfigurationId { get; set; } = Guid.NewGuid();

    /// <summary>Ordered characteristic values (field name → normalized value).</summary>
    public IReadOnlyDictionary<string, decimal> Values { get; set; } =
        new SortedDictionary<string, decimal>(StringComparer.Ordinal);

    public bool Active { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string? CreatedBy { get; set; }

    /// <summary>Whether this configuration differs from another by at least one characteristic.</summary>
    public bool DiffersFrom(TampaoConfiguration other)
    {
        if (other is null || Values.Count != other.Values.Count)
            return true;
        foreach (var (key, value) in Values)
        {
            if (!other.Values.TryGetValue(key, out var otherValue) || value != otherValue)
                return true;
        }
        return false;
    }
}

/// <summary>
/// Canonical JSON key of a configuration (used for UNIQUE(values_json) reuse).
/// Produced from the ordered field/value map so that "Ø 28,95 · Calote 4" is the
/// same configuration regardless of insertion order or value stringification.
/// </summary>
public static class TampaoConfigurationKey
{
    /// <summary>Builds a stable, deterministic JSON object key for the given values.</summary>
    public static string Serialize(IReadOnlyDictionary<string, decimal> values)
    {
        var ordered = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (k, v) in values)
            ordered[k] = v;
        var parts = ordered.Select(kv => $"\"{Escape(kv.Key)}\":{FormatNumber(kv.Value)}");
        return "{" + string.Join(",", parts) + "}";
    }

    private static string Escape(string value) =>
        System.Text.Json.JsonEncodedText.Encode(value).ToString();

    private static string FormatNumber(decimal value)
    {
        // Normalize trailing zeros so 4, 4.0, 4.00 collapse to the same key.
        var rounded = decimal.Round(value, 4, System.MidpointRounding.AwayFromZero);
        return rounded.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture);
    }
}