using System.Text.Json;

namespace BA.Dmo.Application.Modules.JobOn;

/// <summary>
/// Current image association owned by the master Article/Reference context.
/// Job On consumes this association; immutable production revisions do not own it.
/// </summary>
public sealed record ArticleReferenceImage(
    string ReferenceCode,
    string ImageAssetId,
    string? UpdatedBy = null,
    DateTimeOffset? UpdatedAtUtc = null);

/// <summary>Persistence port for the reference-owned article image.</summary>
public interface IArticleReferenceImageRepository
{
    Task<ArticleReferenceImage?> GetAsync(
        string referenceCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically sets the master association and appends the Job On audit fact.
    /// The supplied revision id is attribution context only; it is never mutated.
    /// </summary>
    Task SetAsync(
        ArticleReferenceImage association,
        Guid jobOnId,
        Guid? jobOnRevisionId,
        string eventType,
        string? beforeImageAssetId,
        string actorId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically removes the master association and appends the Job On audit fact.
    /// No Job On revision is created or changed.
    /// </summary>
    Task RemoveAsync(
        string referenceCode,
        Guid jobOnId,
        Guid? jobOnRevisionId,
        string eventType,
        string beforeImageAssetId,
        string actorId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>Canonical parsing and validation for the Article/Reference image key.</summary>
public static class ArticleReferenceImageRules
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };

    public static string ExtractReferenceCode(string? snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
            return string.Empty;

        var raw = snapshot.Trim();
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                return NormalizeReferenceCode(doc.RootElement.GetString());

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var propertyName in new[] { "article_reference", "reference", "code", "value" })
                {
                    if (doc.RootElement.TryGetProperty(propertyName, out var property)
                        && property.ValueKind == JsonValueKind.String)
                        return NormalizeReferenceCode(property.GetString());
                }
            }
        }
        catch (JsonException)
        {
            return NormalizeReferenceCode(raw);
        }

        return string.Empty;
    }

    public static string NormalizeReferenceCode(string? referenceCode) =>
        (referenceCode ?? string.Empty).Trim().ToUpperInvariant();

    public static bool TryNormalizeImageAssetId(string? imageAssetId, out string normalized)
    {
        normalized = (imageAssetId ?? string.Empty).Trim();
        if (normalized.Length == 0
            || Path.IsPathRooted(normalized)
            || normalized.Contains("..", StringComparison.Ordinal)
            || normalized.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0
            || !string.Equals(Path.GetFileName(normalized), normalized, StringComparison.Ordinal)
            || !AllowedExtensions.Contains(Path.GetExtension(normalized)))
        {
            normalized = string.Empty;
            return false;
        }

        return true;
    }
}
