using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.ReparacaoInterna;

/// <summary>
/// R009 — Reparação Interna domain rules (OWNER DECISION; supersedes the earlier
/// hard-block wording of REPARACAO_INTERNA_DESIGN_BRIEF §6 / GLM-RI-08 for this module).
/// NO operational hard blocks: a repair fact is recorded as it happened. Automatic
/// production context (line production / reference / lot) is ASSISTANCE and is never used
/// to prevent legitimate recording. The only rejections are purely structural/technical
/// (unknown line, missing/empty number, missing authenticated operator). Mismatches
/// (lot/reference/context/number-lot scope, repeated numbers, unusual quantity) surface
/// only as information; they never block confirmation.
/// </summary>
public static class InternalRepairRules
{
    /// <summary>Non-blocking informational code when the number was not confirmed in the context lot.</summary>
    public const string ContextMismatchInfoCode = "REPINT_CONTEXT_MISMATCH_INFO";

    /// <summary>Non-blocking informational code when no effective production context is auto-resolvable.</summary>
    public const string NoActiveContextInfoCode = "REPINT_NO_ACTIVE_CONTEXT_INFO";

    /// <summary>
    /// R009 — no hard block on the production-context resolution. Single/None/Ambiguous all
    /// allow recording; context is nullable assistance. Always succeeds.
    /// </summary>
    public static Result<Unit, DomainError> EvalCollectibleWhen(InternalRepairResolutionKind kind)
        => Result<Unit, DomainError>.Success(new Unit());

    /// <summary>
    /// R009 — no hard block on the number-vs-lot scope. Always succeeds; the caller may use
    /// the returned boolean to surface a non-blocking informational note when the number was
    /// not confirmed in the resolved lot. The record is persisted regardless.
    /// </summary>
    public static bool NumberInContextLot(InternalRepairContext? context, InternalRepairToolType type, Guid? pieceLotId)
    {
        if (context is null || pieceLotId is null || pieceLotId == Guid.Empty)
            return false;
        var allowedLots = type switch
        {
            InternalRepairToolType.CM => context.CmLotIds,
            InternalRepairToolType.MF => context.MfLotIds,
            InternalRepairToolType.BQ => context.BqLotIds,
            _ => (IReadOnlyList<Guid>?)null
        };
        return allowedLots is not null && allowedLots.Contains(pieceLotId.Value);
    }
}

/// <summary>
/// Minimal unit type used for rule results that carry no payload
/// (kept local — see DomainError usage).
/// </summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}