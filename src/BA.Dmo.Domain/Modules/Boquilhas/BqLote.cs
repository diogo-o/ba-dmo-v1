namespace BA.Dmo.Domain.Modules.Boquilhas;

/// <summary>
/// U-19 — Master lot identity of a Boquilha (06_DATA §3.2 / N03_bq <c>bq_lotes</c>).
/// UNIQUE(reference, batch_code); reference <c>^[A-Z][0-9]{3}$</c>. The active /
/// preparing operational state is DERIVED from traces, never persisted. This is
/// the BQ OPERATIONAL identity — it is NOT the Ferramentas CM/MF <c>tool_lotes</c>
/// (N04 BOUNDARY NOTE).
/// </summary>
public sealed class BqLote
{
    public Guid BqLoteId { get; set; } = Guid.NewGuid();

    public string Reference { get; set; } = string.Empty;

    public string BatchCode { get; set; } = string.Empty;

    public IReadOnlyList<string> AllowedLines { get; set; } = Array.Empty<string>();

    public BqLifecycleState LifecycleState { get; set; } = BqLifecycleState.Available;

    public string? CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// U-19 — Final immutable snapshot captured when a trace/lot is CLOSED
/// (BOQUILHAS_INTERFACE_BEHAVIOR §5 + GLM-BQ-05): opening/config summary,
/// current-state (saldos), close metadata (who/when/reason). Future repairer,
/// line or configuration edits NEVER mutate the closed snapshot.
/// </summary>
public sealed class BqCloseSnapshot
{
    public Guid BqLoteId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string BatchCode { get; set; } = string.Empty;

    public BqTracePurpose Purpose { get; set; }

    public string? StartLine { get; set; }

    public IReadOnlyList<string> AllowedLines { get; set; } = Array.Empty<string>();

    /// <summary>Serialized current-state (saldos) at close time.</summary>
    public string SaldosJson { get; set; } = string.Empty;

    /// <summary>Total count of recorded movements at close time.</summary>
    public int MovementCount { get; set; }

    public string? Reason { get; set; }

    public string? ClosedBy { get; set; }

    public DateTimeOffset ClosedAtUtc { get; set; }
}