namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// U-17 — Normalized available value of a comparable field
/// (N10 <c>tampao_field_values</c>; GLM-TP-04, TAMPOES_DESIGN_BRIEF §4/§7). Values
/// are NORMALIZED so equivalent variants like <c>4</c>/<c>4.0</c>/<c>4,00</c> are a
/// single canonical value (UNIQUE(def_id, value_numeric)). Deactivating removes a
/// value from new dropdowns without deleting configurations/history.
/// </summary>
public sealed class TampaoFieldValue
{
    public Guid TampaoFieldValueId { get; set; } = Guid.NewGuid();

    public Guid TampaoFieldDefId { get; set; }

    /// <summary>Normalized numeric value.</summary>
    public decimal ValueNumeric { get; set; }

    /// <summary>Display label (unit handled by the field, not repeated here).</summary>
    public string ValueLabel { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool Active { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}