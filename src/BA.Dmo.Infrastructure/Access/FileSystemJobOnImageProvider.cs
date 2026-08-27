using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared;
namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// Filesystem-based implementation of IJobOnImageProvider.
/// Resolves the reference-owned article image using:
///   main_documents_output_root (app_settings / company image directory)
/// + image_asset_id (article_reference_images, keyed by Article/Reference)
/// Returns null when any part of the chain is missing — never throws.
/// </summary>
public sealed class FileSystemJobOnImageProvider : IJobOnImageProvider
{
    private readonly IJobOnRepository _repository;
    private readonly IArticleReferenceImageRepository _articleImages;
    private readonly IAppSettingsReader _settings;

    public FileSystemJobOnImageProvider(
        IJobOnRepository repository,
        IArticleReferenceImageRepository articleImages,
        IAppSettingsReader settings)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _articleImages = articleImages ?? throw new ArgumentNullException(nameof(articleImages));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<ImageResolution?> ResolveAsync(Guid jobOnId, CancellationToken ct = default)
    {
        try
        {
            // 1. Load the Job On only to resolve its Article/Reference context.
            var jobOn = await _repository.GetByIdAsync(jobOnId, ct);
            if (jobOn is null) return null;

            var referenceCode = ArticleReferenceImageRules.ExtractReferenceCode(
                jobOn.CurrentRevision?.ReferenceSnapshot);
            if (string.IsNullOrWhiteSpace(referenceCode)) return null;

            // 2. Resolve the current master association for that Reference.
            var association = await _articleImages.GetAsync(referenceCode, ct);
            if (association is null) return null;

            // 3. Get the configured company image-directory root.
            var outputRoot = await _settings.GetOutputRootAsync(ct);
            if (string.IsNullOrWhiteSpace(outputRoot)) return null;

            if (!ArticleReferenceImageRules.TryNormalizeImageAssetId(
                    association.ImageAssetId,
                    out var imageAssetId))
                return null;

            // 4. Resolve only a validated file name under the configured root.
            var fullPath = Path.Combine(outputRoot, imageAssetId);
            if (!File.Exists(fullPath)) return null;

            var bytes = await File.ReadAllBytesAsync(fullPath, ct);
            var mimeType = DetectMimeType(imageAssetId);

            return new ImageResolution(bytes, mimeType);
        }
        catch
        {
            // Never throw — absence of image is valid
            return null;
        }
    }

    private static string DetectMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg" // fallback
        };
    }
}
