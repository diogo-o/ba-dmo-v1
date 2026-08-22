using BA.Dmo.Domain.Modules.ReparacaoInterna;

namespace BA.Dmo.Application.Modules.ReparacaoInterna;

/// <summary>
/// U-16 — Cross-module read-only lookup that resolves the ACTIVE production
/// context of a line at a point in time (TD-27 <c>Resolve(line, at)</c>). Reads
/// ONLY from the active Job On/revision/component read model — never writes and
/// never creates an absent Job On (REPARACAO_INTERNA_DESIGN_BRIEF §3/§6).
/// Infrastructure implements this against the existing Job On persistence.
/// </summary>
public interface IJobOnActiveContextLookup
{
    /// <summary>
    /// Resolves the active context for <paramref name="line"/> at
    /// <paramref name="at"/>. Returns None / Single / Ambiguous — never
    /// auto-selects in ambiguity.
    /// </summary>
    Task<InternalRepairContextResolution> ResolveActiveAsync(
        string line, DateTimeOffset at, CancellationToken ct = default);
}