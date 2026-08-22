namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// U-17 — Configurable comparable field of a Tampões configuration
/// (N10 <c>tampao_field_defs</c>; GLM-TP-04, TAMPOES_DESIGN_BRIEF §7). A field is
/// defined SEPARATELY from its values (name, unit, precision, order, active).
/// Not a composite string and never a per-config column. Renaming a field/unit or
/// changing precision never silently reinterprets already-saved values (a
/// compatible/ incompatible change would require explicit migration) — GLM-TP-05.5.
/// </summary>
public sealed class TampaoFieldDef
{
    public Guid TampaoFieldDefId { get; set; } = Guid.NewGuid();

    /// <summary>Visible field name (e.g. "Diâmetro", "Profundidade/Calote").</summary>
    public string FieldName { get; set; } = null!;

    /// <summary>Unit (e.g. "mm").</summary>
    public string? Unit { get; set; }

    /// <summary>Max number of decimals for display (≤2 per dead rule).</summary>
    public int? PrecisionDigits { get; set; }

    /// <summary>Presentation order.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Active fields appear in Consulta / transforms / filters (brief §7).</summary>
    public bool Active { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}