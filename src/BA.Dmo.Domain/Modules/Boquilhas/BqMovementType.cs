namespace BA.Dmo.Domain.Modules.Boquilhas;

/// <summary>
/// U-19 — Append-only movement TYPE of a BQ trace (06_DATA §3.2 / N03_bq CHECK
/// ck_bq_movements_type): <c>inicio/saida/entrada/irreparavel/linha/contagem/fim</c>.
/// </summary>
public enum BqMovementType
{
    Inicio,
    Saida,
    Entrada,
    Irreparavel,
    Linha,
    Contagem,
    Fim
}

/// <summary>
/// U-19 — Append-only movement fact of a BQ trace (06_DATA §3.2 / N03_bq
/// <c>bq_movements</c>). A movement is immutable; a correction is a NEW movement,
/// and "deleting" a movement is a void recorded separately (never a physical
/// delete). <see cref="Qty"/> is null ONLY for the <c>linha</c> (line-change)
/// type; <see cref="RepairerId"/> is the actually-chosen repairer for repair
/// movements (later config changes never rewrite history — TD-15).
/// </summary>
public sealed class BqMovement
{
    public Guid BqMovementId { get; set; } = Guid.NewGuid();

    public Guid BqTraceId { get; set; }

    public BqMovementType MovementType { get; set; }

    /// <summary>Quantity; null only for <c>linha</c> (line-change) movements.</summary>
    public decimal? Qty { get; set; }

    /// <summary>
    /// Exceptional quantity recorded on a return that exceeds the expected
    /// repair balance (20→25 case). This is a FIELD OF THE RETURN FACT, NOT an
    /// authorization (UD-09): it is always derivable, never a gate.
    /// </summary>
    public decimal? ExceptionalReceivedQty { get; set; }

    public string? Line { get; set; }

    /// <summary>repairers.repairer_id chosen for this movement (NULL = sem associação).</summary>
    public Guid? RepairerId { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? ActorId { get; set; }
}

/// <summary>
/// U-19 — Movement type codec (06_DATA §3.2 / N03_bq CHECK ck_bq_movements_type).
/// Types: <c>inicio/saida/entrada/irreparavel/linha/contagem/fim</c>. Corrections
/// (contagem) carry deltas and never make a balance negative (BQ-RULE-006).
/// </summary>
public static class BqMovementTypeCodec
{
    public static string ToStorage(BqMovementType type) => type switch
    {
        BqMovementType.Inicio => "inicio",
        BqMovementType.Saida => "saida",
        BqMovementType.Entrada => "entrada",
        BqMovementType.Irreparavel => "irreparavel",
        BqMovementType.Linha => "linha",
        BqMovementType.Contagem => "contagem",
        BqMovementType.Fim => "fim",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static BqMovementType FromStorage(string? value) => value switch
    {
        "inicio" => BqMovementType.Inicio,
        "saida" => BqMovementType.Saida,
        "entrada" => BqMovementType.Entrada,
        "irreparavel" => BqMovementType.Irreparavel,
        "linha" => BqMovementType.Linha,
        "contagem" => BqMovementType.Contagem,
        "fim" => BqMovementType.Fim,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown movement type.")
    };
}