namespace BA.Dmo.Domain.Modules.ReparacaoInterna;

/// <summary>
/// R009 — Resolved production context for a Reparação Interna record
/// (OWNER DECISION, REPARACAO_INTERNA_DESIGN_BRIEF §3/§9). Resolved read-only from the
/// line's production sequence using the owner activation rule (most recent
/// <c>planned_start_at</c> activated at 09:00 local factory, no end-date test).
/// The reference/production are the Job On revision snapshots; the CM/MF/BQ lot scope
/// comes from the <c>MP_CM</c>/<c>MF</c>/<c>BQ</c> component <c>source_lot_id</c> links.
/// Auto-context is ASSISTANCE, never a block: when it cannot be resolved the record is
/// still saved with empty/unknown context.
/// </summary>
public sealed record InternalRepairContext(
    Guid JobOnId,
    Guid JobOnRevisionId,
    string Line,
    string ProductionCode,
    string Reference,
    string? MachineCode,
    IReadOnlyList<Guid> CmLotIds,
    IReadOnlyList<Guid> MfLotIds,
    IReadOnlyList<Guid> BqLotIds,
    DateTimeOffset? ActivatedFromUtc,
    DateTimeOffset? ValidToUtc);

/// <summary>
/// Outcome of the active-context lookup for a (line, at). Never auto-selects between
/// candidates and — R009 — NEVER blocks: <c>None</c> is a normal, recordable state
/// (auto-context is assistance). Only <c>Single</c> supplies an auto-context to prefill.
/// </summary>
public sealed record InternalRepairContextResolution
{
    public InternalRepairResolutionKind Kind { get; }

    /// <summary>Resolved single context (only when <see cref="Kind"/> is <c>Single</c>).</summary>
    public InternalRepairContext? Context { get; }

    /// <summary>Candidate context summaries for the explicit-choice state (Ambiguous).</summary>
    public IReadOnlyList<InternalRepairContextCandidate> Candidates { get; }

    private InternalRepairContextResolution(
        InternalRepairResolutionKind kind,
        InternalRepairContext? context,
        IReadOnlyList<InternalRepairContextCandidate> candidates)
    {
        Kind = kind;
        Context = context;
        Candidates = candidates;
    }

    public static InternalRepairContextResolution None() =>
        new(InternalRepairResolutionKind.None, null, Array.Empty<InternalRepairContextCandidate>());

    public static InternalRepairContextResolution Single(InternalRepairContext context) =>
        new(InternalRepairResolutionKind.Single, context, Array.Empty<InternalRepairContextCandidate>());

    public static InternalRepairContextResolution Ambiguous(
        IReadOnlyList<InternalRepairContextCandidate> candidates) =>
        new(InternalRepairResolutionKind.Ambiguous, null, candidates);
}

/// <summary>State of the production-context lookup (R009 owner projection).</summary>
public enum InternalRepairResolutionKind
{
    /// <summary>No effective production context is auto-resolvable at <c>at</c>.</summary>
    None,

    /// <summary>Exactly one effective production context — auto-prefill.</summary>
    Single,

    /// <summary>Several candidates tie — explicit choice required (not auto).</summary>
    Ambiguous
}

/// <summary>
/// Compact candidate presented when the effective context is ambiguous (Production,
/// Reference, Machine and activation window). The user must choose explicitly.
/// </summary>
public sealed record InternalRepairContextCandidate(
    Guid JobOnId,
    Guid JobOnRevisionId,
    string Line,
    string ProductionCode,
    string Reference,
    string? MachineCode,
    DateTimeOffset? ValidFromUtc,
    DateTimeOffset? ValidToUtc);