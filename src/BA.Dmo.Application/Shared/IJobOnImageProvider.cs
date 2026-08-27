namespace BA.Dmo.Application.Shared;

/// <summary>
/// Resolves the master Article/Reference image consumed by a Job On.
/// The concrete implementation may read from filesystem, object storage, or any
/// backend — application code depends only on this interface.
/// </summary>
public interface IJobOnImageProvider
{
    /// <summary>
    /// Resolves the Job On's readable Article/Reference and then returns that
    /// reference's current image. The image is not owned by a Job On revision.
    /// Returns an ImageResolution when available; null otherwise.
    /// Never throws — absence of image is a valid result.
    /// </summary>
    Task<ImageResolution?> ResolveAsync(Guid jobOnId, CancellationToken ct = default);
}

/// <summary>
/// Resolved image artifact from any storage backend.
/// The consumer (PDF renderer, UI endpoint) receives bytes + MIME type only.
/// </summary>
public sealed record ImageResolution(
    byte[] Bytes,
    string MimeType);
