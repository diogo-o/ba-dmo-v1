namespace BA.Dmo.Domain.Modules.JobOn;

/// <summary>
/// Typed field values per component (N05). Each component has multiple fields with dedicated columns.
/// Value types: text, integer, decimal, boolean, date, select.
/// </summary>
public sealed record JobOnComponentField
{
    /// <summary>Primary key.</summary>
    public Guid JobOnComponentFieldId { get; init; }

    /// <summary>Parent component ID.</summary>
    public Guid JobOnComponentId { get; init; }

    /// <summary>Field key (e.g., "tipo", "diametro_corpo").</summary>
    public string FieldKey { get; init; } = null!;

    /// <summary>Type of value stored.</summary>
    public string ValueType { get; init; } = null!; // text/integer/decimal/boolean/date/select

    /// <summary>Text value.</summary>
    public string? ValueText { get; init; }

    /// <summary>Integer value.</summary>
    public int? ValueInteger { get; init; }

    /// <summary>Decimal value.</summary>
    public decimal? ValueDecimal { get; init; }

    /// <summary>Boolean value.</summary>
    public bool? ValueBoolean { get; init; }

    /// <summary>Date value.</summary>
    public DateTime? ValueDate { get; init; }

    /// <summary>Display order.</summary>
    public int DisplayOrder { get; init; }
}

/// <summary>
/// CAL row entry (N05). One row per calibration measurement element.
/// </summary>
public sealed record JobOnComponentRow
{
    /// <summary>Primary key.</summary>
    public Guid JobOnComponentRowId { get; init; }

    /// <summary>Parent component ID.</summary>
    public Guid JobOnComponentId { get; init; }

    /// <summary>Element label (e.g., "Bucha marcada").</summary>
    public string ElementLabel { get; init; } = null!;

    /// <summary>Decimal value.</summary>
    public decimal? ValueDecimal { get; init; }

    /// <summary>Text value.</summary>
    public string? ValueText { get; init; }

    /// <summary>Unit (e.g., "mm").</summary>
    public string? Unit { get; init; }

    /// <summary>Quantity in machine.</summary>
    public decimal? MachineQuantity { get; init; }

    /// <summary>Display order.</summary>
    public int DisplayOrder { get; init; }
}
