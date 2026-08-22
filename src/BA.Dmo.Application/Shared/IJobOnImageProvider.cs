namespace BA.Dmo.Application.Shared;

/// <summary>
/// Resolves the article image associated with a Job On / production.
/// The concrete implementation may read from filesystem, object storage, or any
/// backend — application code depends only on this interface.
/// </summary>
public interface IJobOnImageProvider
{
    /// <summary>
    /// Attempts to resolve the article image for the given Job On.
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
