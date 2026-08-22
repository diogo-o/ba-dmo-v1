using BA.Dmo.Domain.Modules.Pegamentos;

namespace BA.Dmo.Application.Modules.Pegamentos;

/// <summary>
/// Explicit cross-module Application lookup contract.
/// Resolves the exact immutable Job On revision context required by Pegamentos.
/// Infrastructure implements this using the existing Job On persistence/read model.
/// </summary>
public interface IJobOnProductionContextLookup
{
    /// <summary>
    /// Resolves the exact production context for a pinned revision.
    /// Returns null when the revision does not exist or lacks required components.
    /// </summary>
    Task<PegamentoProductionContext?> ResolveAsync(Guid jobOnRevisionId, CancellationToken ct = default);
}