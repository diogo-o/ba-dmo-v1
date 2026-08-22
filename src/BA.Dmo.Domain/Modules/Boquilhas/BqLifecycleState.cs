namespace BA.Dmo.Domain.Modules.Boquilhas;

/// <summary>
/// U-19 — Persisted lifecycle state of a BQ lot (06_DATA §3.2 / N03_bq:
/// <c>bq_lotes.lifecycle_state</c>). The operational "active/preparing" state is
/// DERIVED from traces, never persisted (GLM-DATA-04.5: no invented states).
/// Active (available) lots shown in the operational tabs; archived/scrapped are
/// historical.
/// </summary>
public enum BqLifecycleState
{
    Available,
    Archived,
    Scrapped
}

/// <summary>
/// Codec between <see cref="BqLifecycleState"/> and the persisted text values
/// (N03_bq CHECK ck_bq_lotes_lifecycle).
/// </summary>
public static class BqLifecycleStateCodec
{
    public static string ToStorage(BqLifecycleState state) => state switch
    {
        BqLifecycleState.Available => "available",
        BqLifecycleState.Archived => "archived",
        BqLifecycleState.Scrapped => "scrapped",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };

    public static BqLifecycleState FromStorage(string? value) => value switch
    {
        "available" => BqLifecycleState.Available,
        "archived" => BqLifecycleState.Archived,
        "scrapped" => BqLifecycleState.Scrapped,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown lifecycle state.")
    };
}