using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.JobOn;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// Filesystem-based implementation of IJobOnImageProvider.
/// Resolves the article image using the existing infrastructure:
///   main_documents_output_root (app_settings)
/// + production_folder (job_on)
/// + image_asset_id (job_on_revision.current_revision)
/// Returns null when any part of the chain is missing — never throws.
/// </summary>
public sealed class FileSystemJobOnImageProvider : IJobOnImageProvider
{
    private readonly IJobOnRepository _repository;
    private readonly IAppSettingsReader _settings;
    private readonly IDbConnectionFactory _connectionFactory;

    public FileSystemJobOnImageProvider(
        IJobOnRepository repository,
        IAppSettingsReader settings,
        IDbConnectionFactory connectionFactory)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<ImageResolution?> ResolveAsync(Guid jobOnId, CancellationToken ct = default)
    {
        try
        {
            // 1. Load Job On (carries production_folder on the entity now)
            var jobOn = await _repository.GetByIdAsync(jobOnId, ct);
            if (jobOn is null) return null;

            // 2. Get output root from app settings
            var outputRoot = await _settings.GetOutputRootAsync(ct);
            if (string.IsNullOrWhiteSpace(outputRoot)) return null;

            // 3. Get production folder from entity
            var productionFolder = jobOn.ProductionFolder;
            if (string.IsNullOrWhiteSpace(productionFolder)) return null;

            // 4. Get image_asset_id from current revision
            var imageAssetId = jobOn.CurrentRevision?.ImageAssetId;
            if (string.IsNullOrWhiteSpace(imageAssetId)) return null;

            // 5. Build full path and read file
            var fullPath = Path.Combine(outputRoot, productionFolder, imageAssetId);
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
