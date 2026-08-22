namespace BA.Dmo.Domain.Modules.Boquilhas;

/// <summary>
/// U-19 — Utilisation (life/wear) reading of a trace (06_DATA §3.2 / N03_bq
/// <c>bq_utilisation_readings</c>: reading_kind initial/final, value 0–100).
/// Utilisation is TIME-OF-LIFE / WEAR, NOT quantity (BOQUILHAS_INTERFACE_BEHAVIOR
/// §7): a value near the limit is a warning only, never a block (UD-12). SAP
/// fields are manual readings only — no SAP read/write (06_DATA §10).
/// </summary>
public sealed class BqUtilisationReading
{
    public Guid BqUtilisationReadingId { get; set; } = Guid.NewGuid();

    public Guid BqTraceId { get; set; }

    public BqUtilisationReadingKind ReadingKind { get; set; }

    public decimal Value { get; set; }

    public string? ActorId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}

public enum BqUtilisationReadingKind
{
    Initial,
    Final
}

public static class BqUtilisationReadingKindCodec
{
    public static string ToStorage(BqUtilisationReadingKind k) => k switch
    {
        BqUtilisationReadingKind.Initial => "initial",
        BqUtilisationReadingKind.Final => "final",
        _ => throw new ArgumentOutOfRangeException(nameof(k), k, null)
    };

    public static BqUtilisationReadingKind FromStorage(string? v) => v switch
    {
        "initial" => BqUtilisationReadingKind.Initial,
        "final" => BqUtilisationReadingKind.Final,
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, "Unknown utilisation reading.")
    };
}