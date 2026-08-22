namespace BA.Dmo.Domain.Modules.JobOn;

/// <summary>
/// Verification frequency from the Ferramentas rule (modules/05 §7). The Job On
/// materializes occurrences from these rules; checks are confirmed by an
/// authorized operator (<c>jobon.confirmar</c>).
/// </summary>
public enum VerificationFrequency
{
    /// <summary>"Uma vez neste lote" — one occurrence per tool/lot.</summary>
    OncePerLot,

    /// <summary>"Por fabrico" — one occurrence per production.</summary>
    PerProduction
}

/// <summary>
/// Lookup contract for a verification rule defined in the Ferramentas module
/// (modules/05 §7). The Ferramentas module does not exist yet (U-12); this is
/// the minimal contract the Job On consumes to materialize occurrences.
/// </summary>
public sealed record VerificationRule(
    Guid SourceRuleId,
    string RuleText,
    VerificationFrequency Frequency);

/// <summary>
/// Materializes verification occurrences for a Job On component from the
/// Ferramentas rules (modules/05 §7, GLM-JOB-07). Each rule yields one
/// occurrence per component; the frequency distinguishes once-per-lot from
/// per-production semantics (relevant when the same rule spans several lots).
/// Occurrences start <c>pendente</c> with <c>completion_source = manual_job_on</c>
/// (N05 constraint); operator/date are recorded only after confirmation.
/// </summary>
public static class JobOnVerificationGenerator
{
    public static IReadOnlyList<JobOnVerificationOccurrence> Generate(
        Guid jobOnComponentId,
        IEnumerable<VerificationRule> rules,
        DateTime now)
    {
        if (rules is null)
            return Array.Empty<JobOnVerificationOccurrence>();

        var occurrences = new List<JobOnVerificationOccurrence>();
        foreach (var rule in rules)
        {
            if (rule is null || rule.SourceRuleId == Guid.Empty)
                continue;

            occurrences.Add(new JobOnVerificationOccurrence
            {
                JobOnVerificationOccurrenceId = Guid.NewGuid(),
                JobOnComponentId = jobOnComponentId,
                SourceRuleId = rule.SourceRuleId,
                RuleTextSnapshot = rule.RuleText,
                Status = "pendente",
                CompletionSource = "manual_job_on",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        return occurrences;
    }
}
