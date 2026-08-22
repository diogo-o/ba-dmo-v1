namespace BA.Dmo.Domain.Modules.Boquilhas;

/// <summary>
/// U-19 — Status of a return-excess discrepancy (06_DATA §3.2 / N03_bq
/// <c>bq_discrepancies</c>: open/under_review/resolved). The excess of a return
/// over the expected repair balance is ALWAYS recorded (warning, never a block,
/// UD-08/UD-09); resolution is a separate auditable event that never rewrites the
/// original return.
/// </summary>
public enum BqDiscrepancyStatus
{
    Open,
    UnderReview,
    Resolved
}

/// <summary>
/// U-19 — Codec between <see cref="BqDiscrepancyStatus"/> and the persisted text values.
/// </summary>
public static class BqDiscrepancyStatusCodec
{
    public static string ToStorage(BqDiscrepancyStatus s) => s switch
    {
        BqDiscrepancyStatus.Open => "open",
        BqDiscrepancyStatus.UnderReview => "under_review",
        BqDiscrepancyStatus.Resolved => "resolved",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, null)
    };

    public static BqDiscrepancyStatus FromStorage(string? v) => v switch
    {
        "open" => BqDiscrepancyStatus.Open,
        "under_review" => BqDiscrepancyStatus.UnderReview,
        "resolved" => BqDiscrepancyStatus.Resolved,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown discrepancy status.")
    };
}

/// <summary>
/// U-19 — A first-class record of a return excess (C27): expected vs actual vs
/// excess, with an auditable resolution (note, author, date). Never deletes the
/// original return movement.
/// </summary>
public sealed class BqDiscrepancy
{
    public Guid BqDiscrepancyId { get; set; } = Guid.NewGuid();

    public Guid BqLoteId { get; set; }

    public Guid? BqTraceId { get; set; }

    public decimal ExpectedQty { get; set; }

    public decimal ActualQty { get; set; }

    public decimal ExcessQty { get; set; }

    public BqDiscrepancyStatus Status { get; set; } = BqDiscrepancyStatus.Open;

    public string? ResolutionNote { get; set; }

    public string? ResolvedBy { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

/// <summary>
/// U-19 — Lifecycle audit event of a lot (06_DATA §3.2 / N03_bq
/// <c>bq_lifecycle_history</c>): archived/scrapped/restored/retired + reason +
/// actor. Lifecycle changes require no active trace (BQ-RULE-008).
/// </summary>
public sealed class BqLifecycleEvent
{
    public Guid BqLifecycleEventId { get; set; } = Guid.NewGuid();

    public Guid BqLoteId { get; set; }

    public BqLifecycleEventKind Kind { get; set; }

    public string? Reason { get; set; }

    public string? ActorId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}

public enum BqLifecycleEventKind
{
    Archived,
    Scrapped,
    Restored,
    Retired
}

public static class BqLifecycleEventKindCodec
{
    public static string ToStorage(BqLifecycleEventKind k) => k switch
    {
        BqLifecycleEventKind.Archived => "archived",
        BqLifecycleEventKind.Scrapped => "scrapped",
        BqLifecycleEventKind.Restored => "restored",
        BqLifecycleEventKind.Retired => "retired",
        _ => throw new ArgumentOutOfRangeException(nameof(k), k, null)
    };

    public static BqLifecycleEventKind FromStorage(string? v) => v switch
    {
        "archived" => BqLifecycleEventKind.Archived,
        "scrapped" => BqLifecycleEventKind.Scrapped,
        "restored" => BqLifecycleEventKind.Restored,
        "retired" => BqLifecycleEventKind.Retired,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown lifecycle event.")
    };
}