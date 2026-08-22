using BA.Dmo.Domain.Modules.JobOn;
using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.Domain.Modules.ReparacaoInterna;

/// <summary>
/// R009 — Reparação Interna production-activation projection (OWNER DECISION).
///
/// For a given line, the effective production at a requested/current timestamp is the
/// production with the MOST RECENT start date whose activation moment is
/// <c>&lt;= at</c>. The activation moment is the production's start date at 09:00 local
/// factory time. The END DATE is NOT used for this projection — the next production
/// naturally supersedes the previous. This is deterministic from persisted
/// <c>planned_start_at</c> values whenever the line context is requested; it never depends
/// on a background "ping", a scheduled worker, or the page being open (R009 §4–§5).
///
/// Only the candidate Job Ons of the SAME line are considered (line-scoped; B1 never
/// resolves from the B2 schedule) and only the active lifecycle states (planeado/em_fabrico)
/// are candidates (R009 §2/§3). If no activation is <c>&lt;= at</c>, no auto-context is
/// produced (None) — but recording remains allowed (R009: no hard blocks).
/// </summary>
public static class ReparacaoInternaProductionProjection
{
    /// <summary>
    /// Factory-local UTC offset at the nominal activation clock of 09:00. The codebase
    /// stores UTC; per the factory context (base reference ba-dmo-beta) the factory local
    /// time is Europe/Lisbon. A fixed +01:00 (WEST, summer daylight time) is used as the
    /// canonical activation offset. Kept as a single constant so the 09:00 rule is explicit
    /// and unit-testable.
    /// </summary>
    public static readonly TimeSpan FactoryLocalOffsetUtc = TimeSpan.FromHours(1);

    /// <summary>
    /// Computes the activation moment (UTC) for a production whose persisted start is
    /// <paramref name="plannedStartAt"/>. The start's LOCAL calendar date (start shifted by
    /// the factory offset) at 09:00 local is the activation instant, returned as UTC.
    /// </summary>
    public static DateTimeOffset ActivationUtc(DateTimeOffset plannedStartAt)
    {
        var localStart = plannedStartAt.ToOffset(FactoryLocalOffsetUtc);
        var localActivation = new DateTimeOffset(
            localStart.Year, localStart.Month, localStart.Day, 9, 0, 0, FactoryLocalOffsetUtc);
        return localActivation.ToUniversalTime();
    }

    /// <summary>
    /// Selects the effective production among the line candidates at <paramref name="at"/>.
    /// Returns the candidate with the greatest <c>ActivationUtc</c> that is <c>&lt;= at</c>;
    /// the most recent start supersedes earlier ones (no end-date test). Returns null when
    /// no candidate has activated yet or there are no candidates. Ties with equal activation
    /// require explicit choice (never auto-select).
    /// </summary>
    public static JobOnEntity? SelectEffective(IReadOnlyList<JobOnEntity>? candidates, DateTimeOffset at)
    {
        if (candidates is null || candidates.Count == 0)
            return null;

        JobOnEntity? best = null;
        DateTimeOffset bestActivation = default;
        foreach (var candidate in candidates)
        {
            if (!candidate.IsActive || !candidate.PlannedStartAt.HasValue)
                continue;
            var activation = ActivationUtc(candidate.PlannedStartAt.Value);
            if (activation > at)
                continue; // not yet activated
            if (best is null || activation > bestActivation)
            {
                best = candidate;
                bestActivation = activation;
            }
        }
        return best;
    }
}