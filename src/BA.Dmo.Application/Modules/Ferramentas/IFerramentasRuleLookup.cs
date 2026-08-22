using BA.Dmo.Domain.Modules.JobOn;

namespace BA.Dmo.Application.Modules.Ferramentas;

/// <summary>
/// Minimal cross-module lookup contract: Ferramentas is the AUTHORITATIVE source
/// for verification-rule configuration (modules/06 §8, GLM-FERR-08; modules/05 §7,
/// GLM-JOB-07). Job On consumes this to materialize occurrences. This port NEVER
/// couples Ferramentas to Job On tables.
/// </summary>
public interface IFerramentasRuleLookup
{
    /// <summary>
    /// Resolves the ACTIVE verification rules of a tool lote as the Job On
    /// contract (SourceRuleId, RuleText, Frequency). Inactive rules are excluded.
    /// </summary>
    Task<IReadOnlyList<VerificationRule>> ResolveActiveRulesAsync(Guid toolLoteId, CancellationToken ct = default);
}