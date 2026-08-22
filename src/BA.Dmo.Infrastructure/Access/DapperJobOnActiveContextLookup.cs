using System.Data;
using System.Text.Json;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Modules.ReparacaoInterna;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Modules.ReparacaoInterna;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// R009 — Dapper implementation of <see cref="IJobOnActiveContextLookup"/>.
/// Resolves the EFFECTIVE production context of a line at a point in time using the owner
/// production-activation rule (<see cref="ReparacaoInternaProductionProjection"/>): most
/// recent <c>planned_start_at</c> activated at 09:00 local factory, line-scoped, NO
/// end-date test (R009 §3). It then reads the current revision snapshots and the
/// MP_CM/MF/BQ component lot links. Read-only — never creates or mutates Job On data.
///
/// This FIXES the earlier GAP 1 defect: the previous implementation passed
/// <c>GetActiveAsync(line, at, at)</c>, whose inverted interval predicate
/// (<c>planned_start_at &gt;= now AND end &lt;= now</c>) suppressed real in-progress
/// productions. The projection now feeds all active line candidates to the deterministic
/// start-date rule instead and never uses that inverted filter.
/// </summary>
public sealed class DapperJobOnActiveContextLookup : IJobOnActiveContextLookup
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IJobOnRepository _jobOnRepository;

    public DapperJobOnActiveContextLookup(
        IDbConnectionFactory connectionFactory,
        IJobOnRepository jobOnRepository)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
        _jobOnRepository = jobOnRepository
            ?? throw new ArgumentNullException(nameof(jobOnRepository));
    }

    public async Task<InternalRepairContextResolution> ResolveActiveAsync(
        string line, DateTimeOffset at, CancellationToken ct = default)
    {
        // 1. Active Job Ons of the line (planeado/em_fabrico). NO from/to filter — the
        //    production projection below owns the start-date selection (GAP 1 fix). The
        //    inverted interval predicate in GetActiveAsync(from,to) must not be used here.
        var active = await _jobOnRepository.GetActiveAsync(line, cancellationToken: ct);

        // 2. Effective production per the owner activation rule (most recent start, 09:00).
        var effective = ReparacaoInternaProductionProjection.SelectEffective(active, at);
        if (effective is null)
            return InternalRepairContextResolution.None();

        // 3. Read the effective revision snapshots + MP_CM/MF/BQ component lots.
        var current = await ReadRevisionContextAsync(effective, ct);
        if (current is null)
            return InternalRepairContextResolution.None();

        return InternalRepairContextResolution.Single(
            new InternalRepairContext(
                effective.Id,
                current.JobOnRevisionId,
                line,
                current.ProductionCode,
                current.Reference,
                current.MachineCode,
                current.CmLotIds,
                current.MfLotIds,
                current.BqLotIds,
                ReparacaoInternaProductionProjection.ActivationUtc(effective.PlannedStartAt!.Value),
                null));
    }

    private async Task<RevisionContext?> ReadRevisionContextAsync(JobOn jobOn, CancellationToken ct)
    {
        var currentRevisionId = jobOn.CurrentRevisionId
            ?? await _jobOnRepository.GetCurrentRevisionIdAsync(jobOn.Id, ct);
        if (currentRevisionId is null)
            return null;

        const string revisionSql = @"
SELECT jr.job_on_revision_id, jr.job_on_id,
       jr.production_snapshot, jr.reference_snapshot, jr.machine_snapshot
FROM job_on_revision jr
WHERE jr.job_on_revision_id = @RevisionId;";

        const string lotsSql = @"
SELECT family, source_lot_id
FROM job_on_component
WHERE job_on_revision_id = @RevisionId
  AND family IN ('MP_CM', 'MF', 'BQ');";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? revision = await Db.QuerySingleOrDefaultAsync<dynamic>(
                conn, revisionSql, new { RevisionId = currentRevisionId }, cancellationToken: ct);
            if (revision is null)
                return null;

            var productionCode = ExtractString(revision.production_snapshot, "production_code");
            var machineCode = ExtractString(revision.machine_snapshot, "machine_code");
            var reference = ExtractReference(revision.reference_snapshot);
            if (string.IsNullOrWhiteSpace(revision.job_on_revision_id?.ToString())
                || string.IsNullOrWhiteSpace(productionCode)
                || string.IsNullOrWhiteSpace(reference))
                return null;

            var rows = await Db.QueryAsync<dynamic>(
                conn, lotsSql, new { RevisionId = currentRevisionId }, cancellationToken: ct);

            var cmLots = new List<Guid>();
            var mfLots = new List<Guid>();
            var bqLots = new List<Guid>();
            foreach (var r in rows)
            {
                var family = (string)r.family;
                var lotId = r.source_lot_id as Guid?;
                if (lotId is null) continue;
                if (family == "MP_CM") cmLots.Add(lotId.Value);
                else if (family == "MF") mfLots.Add(lotId.Value);
                else if (family == "BQ") bqLots.Add(lotId.Value);
            }

            return new RevisionContext(
                (Guid)revision.job_on_revision_id,
                productionCode, reference, machineCode, cmLots, mfLots, bqLots);
        }
        finally
        {
            if (conn is IAsyncDisposable a) await a.DisposeAsync();
            else conn.Dispose();
        }
    }

    private static string? ExtractString(object? raw, string propertyName)
    {
        if (raw is null || raw is DBNull) return null;
        var text = raw switch { string s => s, _ => raw.ToString() };
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
        }
        catch (JsonException) { return null; }
    }

    private static string? ExtractReference(object? raw)
    {
        if (raw is null || raw is DBNull) return null;
        var text = raw switch { string s => s, _ => raw.ToString() };
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                return doc.RootElement.GetString();
            return doc.RootElement.TryGetProperty("reference", out var refProp) && refProp.ValueKind == JsonValueKind.String
                ? refProp.GetString()
                : null;
        }
        catch (JsonException) { return null; }
    }

    private sealed record RevisionContext(
        Guid JobOnRevisionId,
        string ProductionCode,
        string Reference,
        string? MachineCode,
        List<Guid> CmLotIds,
        List<Guid> MfLotIds,
        List<Guid> BqLotIds);
}