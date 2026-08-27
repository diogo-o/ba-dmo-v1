using BA.Dmo.Application.Modules.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

public sealed class FakeArticleReferenceImageRepository : IArticleReferenceImageRepository
{
    public Dictionary<string, ArticleReferenceImage> Associations { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<(Guid JobOnId, Guid? RevisionId, string EventType, string? Before, string? After)> AuditFacts { get; } = [];

    public Task<ArticleReferenceImage?> GetAsync(
        string referenceCode,
        CancellationToken cancellationToken = default)
    {
        Associations.TryGetValue(
            ArticleReferenceImageRules.NormalizeReferenceCode(referenceCode),
            out var association);
        return Task.FromResult(association);
    }

    public Task SetAsync(
        ArticleReferenceImage association,
        Guid jobOnId,
        Guid? jobOnRevisionId,
        string eventType,
        string? beforeImageAssetId,
        string actorId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        Associations[association.ReferenceCode] = association;
        AuditFacts.Add((jobOnId, jobOnRevisionId, eventType, beforeImageAssetId, association.ImageAssetId));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(
        string referenceCode,
        Guid jobOnId,
        Guid? jobOnRevisionId,
        string eventType,
        string beforeImageAssetId,
        string actorId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        Associations.Remove(ArticleReferenceImageRules.NormalizeReferenceCode(referenceCode));
        AuditFacts.Add((jobOnId, jobOnRevisionId, eventType, beforeImageAssetId, null));
        return Task.CompletedTask;
    }
}
