using BA.Dmo.Application.Modules.JobOn;

namespace BA.Dmo.UnitTests.Modules.Pegamentos;

/// <summary>
/// In-memory fake of the shared Job On production folder resolver.
/// Returns a configured per-JobOn production folder id; null when absent.
/// </summary>
public sealed class FakeJobOnProductionFolderResolver : IJobOnProductionFolderResolver
{
    public Dictionary<Guid, string> FolderByJobOn { get; } = new();

    public string? DefaultFolder { get; set; }

    public Task<string?> ResolveAsync(Guid jobOnId, CancellationToken ct = default)
    {
        if (FolderByJobOn.TryGetValue(jobOnId, out var folder))
            return Task.FromResult<string?>(folder);
        return Task.FromResult(DefaultFolder);
    }
}