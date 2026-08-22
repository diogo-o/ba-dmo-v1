namespace BA.Dmo.Domain.Modules.JobOn;

/// <summary>
/// Outcome of the canonical activity lookup <c>Resolve(line, at)</c> (TD-27,
/// modules/05 §5.5). Consumers (Reparação Interna, Boquilhas, Peso, Pegamentos)
/// block in an actionable way when no active Job On exists, and never
/// auto-select when several candidates overlap the same interval.
/// </summary>
public enum JobOnResolutionKind
{
    /// <summary>No active Job On covers <c>at</c> for the line.</summary>
    None,

    /// <summary>Exactly one active Job On covers <c>at</c>.</summary>
    Single,

    /// <summary>Several active Job Ons overlap <c>at</c> — explicit choice required.</summary>
    Ambiguous
}

/// <summary>Result of <see cref="JobOnActivityResolver.Resolve"/>.</summary>
public sealed record JobOnResolution(
    JobOnResolutionKind Kind,
    IReadOnlyList<JobOn> Candidates)
{
    public static JobOnResolution None() =>
        new(JobOnResolutionKind.None, Array.Empty<JobOn>());
}

/// <summary>
/// Canonical activity lookup <c>Resolve(line, at)</c> (TD-27, modules/05 §5.5).
/// Candidates are Job Ons of the line in state {planeado, em_fabrico} whose
/// <c>at</c> falls inside [planned_start_at, planned_end_at]. When
/// <c>planned_end_at</c> is null the upper bound is the next
/// <c>planned_start_at</c> of the same line (legacy provenance: v2 derived
/// <c>data_saida</c> as the next <c>data_entrada</c> of the same line).
/// 1 candidate → returned; several overlapping → explicit choice (never
/// auto-selection); 0 → None.
/// </summary>
public static class JobOnActivityResolver
{
    public static JobOnResolution Resolve(
        IReadOnlyList<JobOn> candidates,
        DateTimeOffset at)
    {
        if (candidates is null)
            return JobOnResolution.None();

        // Only active states are resolvable; rascunho is never active and
        // fechado/cancelado are excluded from activity resolution (TD-27).
        var ordered = candidates
            .Where(c => c.IsActive && c.PlannedStartAt.HasValue)
            .OrderBy(c => c.PlannedStartAt)
            .ToList();

        var matches = new List<JobOn>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var candidate = ordered[i];
            var start = candidate.PlannedStartAt!.Value;

            // Effective upper bound: planned_end_at, else the next planned
            // start of the same line, else unbounded.
            DateTimeOffset? end = candidate.PlannedEndAt;
            if (!end.HasValue && i + 1 < ordered.Count)
                end = ordered[i + 1].PlannedStartAt;

            if (at >= start && (!end.HasValue || at < end.Value))
                matches.Add(candidate);
        }

        return matches.Count switch
        {
            0 => JobOnResolution.None(),
            1 => new JobOnResolution(JobOnResolutionKind.Single, matches),
            _ => new JobOnResolution(JobOnResolutionKind.Ambiguous, matches)
        };
    }
}
