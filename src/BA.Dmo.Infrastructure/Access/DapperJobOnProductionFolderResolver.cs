using System.Data;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-11 — Resolves the Job On production folder from the job_on table
/// (N13_jobon_production_folder.sql). The production folder is owned by the
/// Job On production context — consumers (Peso, Pegamentos) display the
/// resolved value but never choose a different folder.
/// </summary>
public sealed class DapperJobOnProductionFolderResolver : IJobOnProductionFolderResolver
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperJobOnProductionFolderResolver(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<string?> ResolveAsync(Guid jobOnId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT production_folder
FROM job_on
WHERE job_on_id = @JobOnId;";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            return await Db.QuerySingleOrDefaultAsync<string?>(
                conn, sql, new { JobOnId = jobOnId }, cancellationToken: ct);
        }
        finally
        {
            if (conn is IAsyncDisposable a) await a.DisposeAsync();
            else conn.Dispose();
        }
    }
}