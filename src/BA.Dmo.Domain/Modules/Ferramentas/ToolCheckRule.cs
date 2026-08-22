using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Ferramentas;

/// <summary>
/// Frequency of a verification rule (modules/06 §3.5; JobOnVerificationGenerator
/// contract). "uma_vez_no_lote" → one occurrence per tool/lot; "por_fabrico" →
/// one per production.
/// </summary>
public enum FerramentasCheckFrequency
{
    OncePerLot,
    PerProduction
}

/// <summary>Codec between the domain frequency enum and the N04 stored text.</summary>
public static class FerramentasCheckFrequencyCodec
{
    public static string ToStorage(FerramentasCheckFrequency frequency) => frequency switch
    {
        FerramentasCheckFrequency.OncePerLot => "uma_vez_no_lote",
        FerramentasCheckFrequency.PerProduction => "por_fabrico",
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, $"Unknown frequency: {frequency}")
    };

    public static FerramentasCheckFrequency FromStorage(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "uma_vez_no_lote" => FerramentasCheckFrequency.OncePerLot,
        "por_fabrico" => FerramentasCheckFrequency.PerProduction,
        _ => throw new InvalidOperationException($"Unknown persisted check frequency: {value}")
    };
}

/// <summary>
/// A verification rule configured on a lot card (N04 <c>tool_check_rules</c>;
/// TD-33 / GLM-FERR-08). Edits apply to the FUTURE and never rewrite occurrences or
/// history. When copied on duplication, keeps its origin in <c>CopiedFromRuleId</c>.
/// </summary>
public sealed class ToolCheckRule
{
    public Guid ToolCheckRuleId { get; set; } = Guid.NewGuid();

    public Guid ToolLoteId { get; set; }

    public string RuleText { get; set; } = string.Empty;

    public FerramentasCheckFrequency Frequency { get; set; }

    public bool Active { get; set; } = true;

    public Guid? CopiedFromRuleId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public static Result<ToolCheckRule, DomainError> Create(
        Guid toolLoteId,
        string ruleText,
        FerramentasCheckFrequency frequency,
        Guid? copiedFromRuleId,
        DateTimeOffset nowUtc,
        string? createdBy)
    {
        var text = ruleText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return Result<ToolCheckRule, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_RULE_TEXT_REQUIRED", "O texto da regra de verificação é obrigatório."));

        return Result<ToolCheckRule, DomainError>.Success(new ToolCheckRule
        {
            ToolCheckRuleId = Guid.NewGuid(),
            ToolLoteId = toolLoteId,
            RuleText = text,
            Frequency = frequency,
            Active = true,
            CopiedFromRuleId = copiedFromRuleId,
            CreatedAtUtc = nowUtc,
            CreatedBy = createdBy,
            UpdatedAtUtc = nowUtc,
            UpdatedBy = createdBy
        });
    }

    public Result<ToolCheckRule, DomainError> Edit(
        string ruleText, FerramentasCheckFrequency frequency, DateTimeOffset nowUtc, string? actorId)
    {
        var text = ruleText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return Result<ToolCheckRule, DomainError>.Failure(DomainError.Validation(
                "FERRAMENTAS_RULE_TEXT_REQUIRED", "O texto da regra de verificação é obrigatório."));

        RuleText = text;
        Frequency = frequency;
        UpdatedAtUtc = nowUtc;
        UpdatedBy = actorId;

        return Result<ToolCheckRule, DomainError>.Success(this);
    }
}