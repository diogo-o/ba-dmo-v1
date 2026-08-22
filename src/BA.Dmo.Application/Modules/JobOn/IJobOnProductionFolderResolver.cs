namespace BA.Dmo.Application.Modules.JobOn;

/// <summary>
/// Resolves the Job On production folder for a given Job On id.
/// Combines global Main Documents / Output Directory with per-JobOn production_folder.
/// Consumers (Peso, Pegamentos) display the resolved path but do not choose a different folder.
/// </summary>
public interface IJobOnProductionFolderResolver
{
    /// <summary>
    /// Resolves the production folder identifier for the given Job On.
    /// Returns null if not configured.
    /// </summary>
    Task<string?> ResolveAsync(Guid jobOnId, CancellationToken ct = default);
}