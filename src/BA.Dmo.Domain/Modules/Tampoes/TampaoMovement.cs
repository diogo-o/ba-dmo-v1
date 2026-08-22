namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// U-17 — Immutable Tampões quantity movement (N10 <c>tampao_movements</c>;
/// GLM-TP-05, GLM-DATA-04.1). Adicionar/Remover target a single balance;
/// Alterar estado / Alterar configuração are atomic origin→destination transfers.
/// Every movement keeps origin, destination, quantity, before/after balances,
/// operator and timestamp; append-only trigger prevents UPDATE/DELETE. Corrections
/// are NEW rows; the original is never rewritten (GLM-DATA-07).
/// </summary>
public sealed class TampaoMovement
{
    public Guid TampaoMovementId { get; set; } = Guid.NewGuid();

    public TampaoMovementType MovementType { get; set; }

    /// <summary>Origin configuration (null for adicionar; set for remover/estado/configuração).</summary>
    public Guid? OriginConfigurationId { get; set; }

    /// <summary>Destination configuration (set for estado/configuração; null otherwise).</summary>
    public Guid? DestinationConfigurationId { get; set; }

    /// <summary>Positive integer quantity (≥1).</summary>
    public int Qty { get; set; }

    /// <summary>Balances before the movement (enchidos/por_encher jsonb).</summary>
    public string? BalancesBefore { get; set; }

    /// <summary>Balances after the movement (enchidos/por_encher jsonb).</summary>
    public string? BalancesAfter { get; set; }

    public string? ActorId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Whether this is a single-balance movement (adicionar/remover).</summary>
    public bool IsSingleBalance =>
        MovementType is TampaoMovementType.Adicionar or TampaoMovementType.Remover;
}