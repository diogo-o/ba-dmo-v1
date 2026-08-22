using BA.Dmo.Domain.Modules.Controlo;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Controlo;

/// <summary>
/// R010 — Read-only cross-module lookup that resolves the production context (job_on_id +
/// exact current job_on_revision_id + production/reference/machine + component snapshot)
/// for a Folha de Controlo at creation. Reads ONLY the Job On read model (job_on_revision
/// snapshots + job_on_component rows); never writes. The sheet pins this revision so a later
/// Job On revision never reinterprets it.
/// </summary>
public interface IControloProductionContextLookup
{
    /// <summary>Resolves the production context for a Job On at its current revision.</summary>
    Task<Result<ControloFolhaProductionContext, DomainError>> ResolveAsync(
        Guid jobOnId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the production context identified by production code + machine/line (the
    /// exact context a selected Peso production row carries), so the Folha de Controlo can be
    /// opened for it without re-selecting the production.
    /// </summary>
    Task<Result<ControloFolhaProductionContext, DomainError>> ResolveByProductionAsync(
        string productionCode, string? machineCode, CancellationToken ct = default);
}