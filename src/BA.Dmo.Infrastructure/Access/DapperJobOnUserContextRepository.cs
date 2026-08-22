using System.Data;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// R011 — Dapper implementation of <see cref="IJobOnUserContextRepository"/> over the
/// additive <c>jobon_user_current</c> table (N24). Records/reads the Job On context THIS
/// user explicitly opened from the Universal Landing. Upsert-only (one current row per
/// user) with a readable snapshot (production/reference/machine) and the open timestamp.
/// Every row is scoped by the canonical internal <c>actor_id</c>; it is NEVER a global
/// "newest Job On".
/// </summary>
public sealed class DapperJobOnUserContextRepository : IJobOnUserContextRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperJobOnUserContextRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task SetCurrentAsync(
        string actorId,
        Guid jobOnId,
        string productionCode,
        string reference,
        string machineCode,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO jobon_user_current (
    actor_id, job_on_id, production_code, reference, machine_code, opened_at_utc)
VALUES (
    @ActorId, @JobOnId, @ProductionCode, @Reference, @MachineCode, now())
ON CONFLICT (actor_id) DO UPDATE SET
    job_on_id = EXCLUDED.job_on_id,
    production_code = EXCLUDED.production_code,
    reference = EXCLUDED.reference,
    machine_code = EXCLUDED.machine_code,
    opened_at_utc = now();";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            await Db.ExecuteAsync(connection, sql, new
            {
                ActorId = actorId,
                JobOnId = jobOnId,
                ProductionCode = productionCode,
                Reference = reference,
                MachineCode = machineCode
            }, cancellationToken: cancellationToken);
        }
        finally
        {
            if (connection is IAsyncDisposable a) await a.DisposeAsync();
            else connection.Dispose();
        }
    }

    public async Task<JobOnUserCurrent?> GetCurrentAsync(
        string actorId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT job_on_id, production_code, reference, machine_code, opened_at_utc
FROM jobon_user_current
WHERE actor_id = @ActorId;";

        var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        try
        {
            dynamic? row = await Db.QuerySingleOrDefaultAsync<dynamic>(
                connection, sql, new { ActorId = actorId }, cancellationToken: cancellationToken);
            if (row is null)
                return null;

            return new JobOnUserCurrent(
                (Guid)row.job_on_id,
                (string)row.production_code,
                row.reference as string ?? "",
                row.machine_code as string ?? "",
                (DateTimeOffset)row.opened_at_utc);
        }
        finally
        {
            if (connection is IAsyncDisposable a) await a.DisposeAsync();
            else connection.Dispose();
        }
    }
}