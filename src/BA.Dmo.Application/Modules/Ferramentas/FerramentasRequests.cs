using BA.Dmo.Domain.Modules.Ferramentas;

namespace BA.Dmo.Application.Modules.Ferramentas;

/// <summary>Create a new tool reference + its first lot (atomic operation).</summary>
public sealed record CreateFerramentasRequest(
    FerramentasToolType ToolType,
    string RefCode,
    string? TechnicalName,
    string? OwnerPlant,
    string Lote,
    int? Qty,
    IReadOnlyList<string>? AllowedLines,
    string? DrawingCode,
    string? DrawingRevision,
    string? Processo);

/// <summary>Create a NEW lot from a base lot, copying configuration only.</summary>
public sealed record CreateLoteFromBaseRequest(
    Guid BaseLoteId,
    string Lote,
    int? Qty,
    IReadOnlyList<string>? AllowedLines,
    string? DrawingCode,
    string? DrawingRevision);

/// <summary>Edit master reference fields (audited; not retroactive to lots).</summary>
public sealed record EditFerramentasRequest(
    Guid ReferenceId,
    string? TechnicalName,
    string? OwnerPlant);

/// <summary>Edit lot-scoped fields.</summary>
public sealed record EditLoteRequest(
    Guid LoteId,
    int? Qty,
    IReadOnlyList<string>? AllowedLines,
    string? DrawingCode,
    string? DrawingRevision);

/// <summary>Register a physical piece on a lot.</summary>
public sealed record RegisterPieceRequest(
    Guid LoteId,
    int Sequence,
    string Number);

/// <summary>Set the explicit condition/state of a lot's physical piece (fact with reason).</summary>
public sealed record SetConditionRequest(
    Guid LoteId,
    string Number,
    ToolCondition Condition,
    string Reason);

/// <summary>Create or edit a verification rule on a lot.</summary>
public sealed record CheckRuleRequest(
    Guid LoteId,
    string RuleText,
    FerramentasCheckFrequency Frequency);

/// <summary>Deactivate/reactivate a verification rule.</summary>
public sealed record ToggleRuleRequest(
    Guid RuleId,
    bool Active);

// ---- DTOs returned to the UI ----

public sealed record FerramentasReferenceItem(
    Guid ReferenceId,
    string ToolType,
    string RefCode,
    string? TechnicalName,
    string? OwnerPlant,
    string? Processo,
    string AllowedLinesCsv,
    int LotesCount);

public sealed record FerramentasReferenceDetail(
    Guid ReferenceId,
    string ToolType,
    string RefCode,
    string? TechnicalName,
    string? OwnerPlant,
    IReadOnlyList<FerramentasLoteItem> Lotes);

public sealed record FerramentasLoteItem(
    Guid LoteId,
    Guid ReferenceId,
    string Lote,
    int? Qty,
    IReadOnlyList<string> AllowedLines,
    string? DrawingCode,
    string? DrawingRevision,
    string? Processo,
    Guid? CopiedFromToolLoteId);

public sealed record FerramentasPieceItem(
    Guid PieceId,
    Guid LoteId,
    int Sequence,
    string Number,
    string? Status,
    string Condition);

public sealed record FerramentasCheckRuleItem(
    Guid RuleId,
    Guid LoteId,
    string RuleText,
    string Frequency,
    bool Active,
    Guid? CopiedFromRuleId);

public sealed record FerramentasOccurrenceItem(
    Guid OccurrenceId,
    Guid RuleId,
    Guid? JobOnId,
    string Status,
    string CompletionSource,
    string? CompletedBy,
    DateTimeOffset? CompletedAtUtc);