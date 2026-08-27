using System.Data;
using System.Text.Json;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// Dapper persistence for the master Article/Reference image association.
/// Association writes and their Job On audit facts commit atomically.
/// </summary>
public sealed class DapperArticleReferenceImageRepository : IArticleReferenceImageRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperArticleReferenceImageRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<ArticleReferenceImage?> GetAsync(
        string referenceCode,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT reference_code AS ReferenceCode,
       image_asset_id AS ImageAssetId,
       updated_by AS UpdatedBy,
       updated_at_utc AS UpdatedAtUtc
FROM article_reference_images
WHERE reference_code = @ReferenceCode;
""";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            return await Db.QuerySingleOrDefaultAsync<ArticleReferenceImage>(
                connection,
                sql,
                new { ReferenceCode = ArticleReferenceImageRules.NormalizeReferenceCode(referenceCode) },
                cancellationToken: cancellationToken);
        }
        finally
        {
            await DisposeAsync(connection);
        }
    }

    public Task SetAsync(
        ArticleReferenceImage association,
        Guid jobOnId,
        Guid? jobOnRevisionId,
        string eventType,
        string? beforeImageAssetId,
        string actorId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default) =>
        DapperUnitOfWork.RunAsync(
            _connectionFactory,
            async (connection, transaction, ct) =>
            {
                const string upsertSql = """
INSERT INTO article_reference_images (
    reference_code, image_asset_id, updated_by, updated_at_utc)
VALUES (
    @ReferenceCode, @ImageAssetId, @UpdatedBy, @UpdatedAtUtc)
ON CONFLICT (reference_code) DO UPDATE SET
    image_asset_id = EXCLUDED.image_asset_id,
    updated_by = EXCLUDED.updated_by,
    updated_at_utc = EXCLUDED.updated_at_utc;
""";

                await Db.ExecuteAsync(
                    connection,
                    upsertSql,
                    new
                    {
                        ReferenceCode = ArticleReferenceImageRules.NormalizeReferenceCode(association.ReferenceCode),
                        association.ImageAssetId,
                        UpdatedBy = actorId,
                        UpdatedAtUtc = occurredAtUtc
                    },
                    transaction,
                    ct);

                await InsertAuditAsync(
                    connection,
                    transaction,
                    jobOnId,
                    jobOnRevisionId,
                    eventType,
                    association.ReferenceCode,
                    beforeImageAssetId,
                    association.ImageAssetId,
                    actorId,
                    occurredAtUtc,
                    ct);
                return 0;
            },
            cancellationToken);

    public Task RemoveAsync(
        string referenceCode,
        Guid jobOnId,
        Guid? jobOnRevisionId,
        string eventType,
        string beforeImageAssetId,
        string actorId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default) =>
        DapperUnitOfWork.RunAsync(
            _connectionFactory,
            async (connection, transaction, ct) =>
            {
                const string deleteSql = """
DELETE FROM article_reference_images
WHERE reference_code = @ReferenceCode;
""";

                var affected = await Db.ExecuteAsync(
                    connection,
                    deleteSql,
                    new { ReferenceCode = ArticleReferenceImageRules.NormalizeReferenceCode(referenceCode) },
                    transaction,
                    ct);

                if (affected != 1)
                    throw new InvalidOperationException(
                        $"Expected exactly one Article/Reference image association to be removed, got {affected}.");

                await InsertAuditAsync(
                    connection,
                    transaction,
                    jobOnId,
                    jobOnRevisionId,
                    eventType,
                    referenceCode,
                    beforeImageAssetId,
                    null,
                    actorId,
                    occurredAtUtc,
                    ct);
                return 0;
            },
            cancellationToken);

    private static Task InsertAuditAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid jobOnId,
        Guid? jobOnRevisionId,
        string eventType,
        string referenceCode,
        string? beforeImageAssetId,
        string? afterImageAssetId,
        string actorId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
INSERT INTO job_on_audit_event (
    job_on_id, job_on_revision_id, event_type,
    before_snapshot, after_snapshot, actor_id, occurred_at_utc)
VALUES (
    @JobOnId, @JobOnRevisionId, @EventType,
    CAST(@BeforeSnapshot AS jsonb), CAST(@AfterSnapshot AS jsonb),
    @ActorId, @OccurredAtUtc);
""";

        static string? Snapshot(string reference, string? imageAssetId) =>
            imageAssetId is null
                ? null
                : JsonSerializer.Serialize(new
                {
                    reference = ArticleReferenceImageRules.NormalizeReferenceCode(reference),
                    image_asset_id = imageAssetId
                });

        return Db.ExecuteAsync(
            connection,
            sql,
            new
            {
                JobOnId = jobOnId,
                JobOnRevisionId = (object?)jobOnRevisionId ?? DBNull.Value,
                EventType = eventType,
                BeforeSnapshot = (object?)Snapshot(referenceCode, beforeImageAssetId) ?? DBNull.Value,
                AfterSnapshot = (object?)Snapshot(referenceCode, afterImageAssetId) ?? DBNull.Value,
                ActorId = actorId,
                OccurredAtUtc = occurredAtUtc
            },
            transaction,
            cancellationToken);
    }

    private static async ValueTask DisposeAsync(IDbConnection connection)
    {
        if (connection is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            connection.Dispose();
    }
}
