using BA.Dmo.Domain.Modules.ReparacaoInterna;

namespace BA.Dmo.Application.Modules.ReparacaoInterna;

// ---- Commands --------------------------------------------------------------

/// <summary>
/// R009 — Register one or more internal repair facts on a line. <see cref="Numbers"/>
/// may contain REPEATED values; each value is persisted as its own occurrence record with
/// the same context (5,5,7 → three records). Repeated numbers are valid and never
/// deduplicated. Tool type is CM | MF (BQ is not an internal repair type).
/// </summary>
public sealed record RegisterReparacaoRequest(
    string Line,
    InternalRepairToolType ToolType,
    IReadOnlyList<string> Numbers,
    string? OverrideProduction = null,
    string? OverrideReference = null);

/// <summary>
/// R009 — Correct/override an internal repair record (capability reparacao_interna.corrigir).
/// Overriding an auto-derived context never modifies Job On. The number is a single
/// occurrence; to correct multiple numbers the operator issues one correction each.
/// </summary>
public sealed record CorrigirReparacaoRequest(
    Guid RecordId,
    string Line,
    InternalRepairToolType ToolType,
    string IndividualNumber,
    Guid? JobOnId,
    Guid? JobOnRevisionId,
    string? ProductionCode,
    string? Reference,
    Guid? LotId,
    string? Reason);

// ---- Queries ---------------------------------------------------------------

/// <summary>Filters for the internal-repair history list (brief §7).</summary>
public sealed record InternalRepairFilter(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Line,
    Guid? JobOnId,
    InternalRepairToolType? ToolType,
    string? Number,
    string? OperatorId,
    bool OnlyCorrected);

// ---- DTOs returned to the UI ------------------------------------------------

/// <summary>Compact per-line card: line + active reference (read-only) or 'Sem Job On ativo'.</summary>
public sealed record InternalRepairLineCard(
    string Line,
    string? Reference,
    string? ProductionCode,
    bool HasActiveContext);

/// <summary>Resolved context plus the resolution state for the Registo tab.</summary>
public sealed record InternalRepairContextDto(
    InternalRepairResolutionKind Kind,
    Guid? JobOnId,
    Guid? JobOnRevisionId,
    string? ProductionCode,
    string? Reference,
    string? MachineCode,
    DateTimeOffset? ValidFromUtc,
    DateTimeOffset? ValidToUtc,
    IReadOnlyList<InternalRepairCandidateDto> Candidates);

/// <summary>One ambiguous-context candidate for explicit user choice.</summary>
public sealed record InternalRepairCandidateDto(
    Guid JobOnId,
    Guid JobOnRevisionId,
    string ProductionCode,
    string Reference,
    string? MachineCode,
    DateTimeOffset? ValidFromUtc,
    DateTimeOffset? ValidToUtc);

/// <summary>History row (brief §7 columns) — the latest valid version per chain root.</summary>
public sealed record InternalRepairHistoryRow(
    Guid RecordId,
    DateTimeOffset DataHora,
    string Line,
    string? ProductionCode,
    string? Reference,
    string? Lote,
    string ToolType,
    string IndividualNumber,
    string? OperatorId,
    bool IsCorrected,
    Guid? ChainRootId);

/// <summary>Full detail (brief §8) including the whole correction sequence.</summary>
public sealed record InternalRepairDetailDto(
    Guid RecordId,
    string Line,
    Guid? JobOnId,
    Guid? JobOnRevisionId,
    string? ProductionCode,
    string? Reference,
    string? Lote,
    string ToolType,
    string IndividualNumber,
    string? OperatorId,
    DateTimeOffset OccurredAtUtc,
    bool IsCorrected,
    string? CorrectionReason,
    DateTimeOffset? CorrectedAtUtc,
    string? CorrectedBy,
    IReadOnlyList<InternalRepairDetailDto> CorrectionChain);