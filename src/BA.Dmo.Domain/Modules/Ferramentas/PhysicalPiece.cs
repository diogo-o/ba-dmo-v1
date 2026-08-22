using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Ferramentas;

/// <summary>
/// Condition/state change of a physical tool (GLM-FERR-07). Condition changes are
/// recorded FACTS with a reason/actor — never silently derived or invented.
/// </summary>
public enum ToolCondition
{
    New,
    Repaired,
    NotRepaired,
    Sucatado
}

/// <summary>Codec between the domain condition enum and the stored text.</summary>
public static class ToolConditionCodec
{
    public static string ToStorage(ToolCondition state) => state switch
    {
        ToolCondition.New => "new",
        ToolCondition.Repaired => "repaired",
        ToolCondition.NotRepaired => "not_repaired",
        ToolCondition.Sucatado => "sucatado",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, $"Unknown tool condition: {state}")
    };

    public static ToolCondition FromStorage(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "new" => ToolCondition.New,
        "repaired" => ToolCondition.Repaired,
        "not_repaired" => ToolCondition.NotRepaired,
        "sucatado" => ToolCondition.Sucatado,
        // N04 default is 'operational' — an operational piece with no recorded
        // condition is treated as New (no invented condition facts).
        "operational" => ToolCondition.New,
        _ => ToolCondition.New
    };
}

/// <summary>
/// Individual numbered physical piece of a lot (N04 <c>physical_pieces</c>; CM/MF per
/// number, TD-22; BQ flows by quantity). The Piece id is immutable and preserved across
/// duplications. Values (condition/status) change by explicit fact.
/// </summary>
public sealed class PhysicalPiece
{
    public Guid PhysicalPieceId { get; set; } = Guid.NewGuid();

    public Guid ToolLoteId { get; set; }

    public int Sequence { get; set; }

    public string Number { get; set; } = string.Empty;

    /// <summary>Operational status (GLM-FERR-07); defaults to "operational".</summary>
    public string Status { get; set; } = "operational";

    /// <summary>Explicit condition state (fact).</summary>
    public ToolCondition Condition { get; set; } = ToolCondition.New;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Result<PhysicalPiece, DomainError> Register(
        Guid toolLoteId,
        int sequence,
        string number,
        DateTimeOffset nowUtc,
        string? createdBy)
    {
        if (sequence < 1)
            return Result<PhysicalPiece, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_PIECE_SEQUENCE_INVALID", "A sequência da peça deve ser positiva."));

        var num = number?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(num))
            return Result<PhysicalPiece, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_PIECE_NUMBER_REQUIRED", "O número da peça é obrigatório."));

        return Result<PhysicalPiece, DomainError>.Success(new PhysicalPiece
        {
            PhysicalPieceId = Guid.NewGuid(),
            ToolLoteId = toolLoteId,
            Sequence = sequence,
            Number = num,
            Status = "operational",
            Condition = ToolCondition.New,
            CreatedAtUtc = nowUtc,
            CreatedBy = createdBy,
            UpdatedAtUtc = nowUtc,
            UpdatedBy = createdBy
        });
    }

    public Result<PhysicalPiece, DomainError> SetCondition(
        ToolCondition condition, string reason, DateTimeOffset nowUtc, string? actorId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result<PhysicalPiece, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_CONDITION_REASON_REQUIRED",
                "É obrigatória a indicação do motivo ao alterar a condição."));

        Condition = condition;
        UpdatedAtUtc = nowUtc;
        UpdatedBy = actorId;

        return Result<PhysicalPiece, DomainError>.Success(this);
    }
}