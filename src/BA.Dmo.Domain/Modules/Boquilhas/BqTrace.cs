namespace BA.Dmo.Domain.Modules.Boquilhas;

/// <summary>
/// U-19 — Status of a BQ trace (06_DATA §3.2 / N03_bq <c>bq_traces.status</c>:
/// active/closed). Only ONE active trace per lot; close produces an immutable
/// final snapshot; reopen is allowed only on the LAST closed trace and only when
/// no other trace is active (BQ-RULE-007).
/// </summary>
public enum BqTraceStatus
{
    Active,
    Closed
}

/// <summary>
/// U-19 — Purpose of a BQ trace (<c>production</c>/<c>repair</c>).
/// </summary>
public enum BqTracePurpose
{
    Production,
    Repair
}

/// <summary>
/// U-19 — Codec between <see cref="BqTraceStatus"/> and the persisted text values.
/// </summary>
public static class BqTraceStatusCodec
{
    public static string ToStorage(BqTraceStatus s) => s switch
    {
        BqTraceStatus.Active => "active",
        BqTraceStatus.Closed => "closed",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, null)
    };

    public static BqTraceStatus FromStorage(string? v) => v switch
    {
        "active" => BqTraceStatus.Active,
        "closed" => BqTraceStatus.Closed,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown trace status.")
    };
}

/// <summary>
/// U-19 — Codec between <see cref="BqTracePurpose"/> and the persisted text values.
/// </summary>
public static class BqTracePurposeCodec
{
    public static string ToStorage(BqTracePurpose p) => p switch
    {
        BqTracePurpose.Production => "production",
        BqTracePurpose.Repair => "repair",
        _ => throw new ArgumentOutOfRangeException(nameof(p), p, null)
    };

    public static BqTracePurpose FromStorage(string? v) => v switch
    {
        "production" => BqTracePurpose.Production,
        "repair" => BqTracePurpose.Repair,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown trace purpose.")
    };
}

/// <summary>
/// U-19 — A production/repair trace of one lot (06_DATA §3.2 / N03_bq
/// <c>bq_traces</c>). One active trace per lot; <c>start_line</c> is mandatory
/// when a production trace begins (TD-14). <see cref="ReopenHistory"/> and
/// <see cref="DeletedMovements"/> are JSONB arms keeping reopen/void facts.
/// </summary>
public sealed class BqTrace
{
    public Guid BqTraceId { get; set; } = Guid.NewGuid();

    public Guid BqLoteId { get; set; }

    public BqTraceStatus Status { get; set; } = BqTraceStatus.Active;

    public BqTracePurpose Purpose { get; set; } = BqTracePurpose.Production;

    public string? StartLine { get; set; }

    public decimal? SapStart { get; set; }

    public decimal? SapEnd { get; set; }

    /// <summary>JSONB array of reopen events (auditable reopen history).</summary>
    public string? ReopenHistory { get; set; }

    /// <summary>JSONB array of voided movement ids (never a physical delete).</summary>
    public string? DeletedMovements { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}