using System.Data;
using System.Text.Json;
using BA.Dmo.Application.Modules.Pegamentos;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Pegamentos;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// U-11 — Resolves the exact immutable Job On revision production context
/// required by Pegamentos (GLM-PEG-08, TD-18). Reads ONLY from the pinned
/// job_on_revision_id snapshots — never from live job_on state.
/// Fails closed when any required CM/BQ/MF component, reference, or nominal
/// is missing.
/// </summary>
public sealed class DapperJobOnProductionContextLookup : IJobOnProductionContextLookup
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperJobOnProductionContextLookup(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<PegamentoProductionContext?> ResolveAsync(
        Guid jobOnRevisionId, CancellationToken ct = default)
    {
        const string revisionSql = @"
SELECT jr.job_on_revision_id, jr.job_on_id,
       jr.production_snapshot, jr.reference_snapshot, jr.machine_snapshot
FROM job_on_revision jr
WHERE jr.job_on_revision_id = @JobOnRevisionId;";

        const string componentsSql = @"
SELECT family, reference_snapshot, lot_snapshot
FROM job_on_component
WHERE job_on_revision_id = @JobOnRevisionId
  AND family IN ('MP_CM', 'BQ', 'MF');";

        const string nominalSql = @"
SELECT jc.family, jcf.value_decimal
FROM job_on_component jc
JOIN job_on_component_field jcf ON jcf.job_on_component_id = jc.job_on_component_id
WHERE jc.job_on_revision_id = @JobOnRevisionId
  AND jc.family IN ('MP_CM', 'BQ', 'MF')
  AND jcf.field_key = 'nominal';";

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            dynamic? revision = await Db.QuerySingleOrDefaultAsync<dynamic>(
                conn, revisionSql, new { JobOnRevisionId = jobOnRevisionId }, cancellationToken: ct);

            if (revision is null)
                return null;

            // ---- Resolve production code from the exact revision snapshot ----
            var productionCode = ExtractStringFromSnapshot(
                revision.production_snapshot, "production_code");
            if (string.IsNullOrWhiteSpace(productionCode))
                return null;

            // ---- Resolve machine code from the exact revision snapshot ----
            var machineCode = ExtractStringFromSnapshot(
                revision.machine_snapshot, "machine_code");
            if (string.IsNullOrWhiteSpace(machineCode))
                return null;

            // ---- Resolve reference from the exact revision snapshot ----
            var reference = ExtractReferenceFromSnapshot(revision.reference_snapshot);
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            // ---- Load components ----
            var components = await Db.QueryAsync<dynamic>(
                conn, componentsSql, new { JobOnRevisionId = jobOnRevisionId }, cancellationToken: ct);

            // ---- Load nominals ----
            var nominals = await Db.QueryAsync<dynamic>(
                conn, nominalSql, new { JobOnRevisionId = jobOnRevisionId }, cancellationToken: ct);

            // ---- Build nominal lookup keyed by family ----
            var nominalByFamily = new Dictionary<string, decimal?>();
            foreach (var n in nominals)
            {
                nominalByFamily[(string)n.family] = n.value_decimal is null ? null : (decimal)n.value_decimal;
            }

            // ---- Build component snapshots keyed by family ----
            var snapshots = new Dictionary<string, PegamentoToolSnapshot>();
            foreach (var c in components)
            {
                var family = (string)c.family;
                var refSnapshot = (string?)c.reference_snapshot;
                if (string.IsNullOrWhiteSpace(refSnapshot))
                    return null; // fail closed: reference required

                snapshots[family] = new PegamentoToolSnapshot(
                    Key: family switch
                    {
                        "MP_CM" => PegamentoComponentKey.CM,
                        "BQ" => PegamentoComponentKey.BQ,
                        "MF" => PegamentoComponentKey.MF,
                        _ => throw new InvalidOperationException($"Unknown family: {family}")
                    },
                    ReferenceSnapshot: refSnapshot,
                    LotSnapshot: (string?)c.lot_snapshot);
            }

            // ---- Fail closed: all three components required ----
            if (!snapshots.TryGetValue("MP_CM", out var cmSnapshot) ||
                !snapshots.TryGetValue("BQ", out var bqSnapshot) ||
                !snapshots.TryGetValue("MF", out var mfSnapshot))
            {
                return null;
            }

            // ---- Fail closed: all three nominals required ----
            if (!nominalByFamily.TryGetValue("MP_CM", out var cmNominal) || cmNominal is null ||
                !nominalByFamily.TryGetValue("BQ", out var bqNominal) || bqNominal is null ||
                !nominalByFamily.TryGetValue("MF", out var mfNominal) || mfNominal is null)
            {
                return null;
            }

            return new PegamentoProductionContext(
                JobOnId: (Guid)revision.job_on_id,
                JobOnRevisionId: (Guid)revision.job_on_revision_id,
                ProductionCode: productionCode,
                MachineCode: machineCode,
                Reference: reference,
                CmSnapshot: cmSnapshot,
                BqSnapshot: bqSnapshot,
                MfSnapshot: mfSnapshot,
                CmNominal: cmNominal,
                BqNominal: bqNominal,
                MfNominal: mfNominal);
        }
        finally
        {
            if (conn is IAsyncDisposable a) await a.DisposeAsync();
            else conn.Dispose();
        }
    }

    /// <summary>
    /// Extracts a string value from a Job On snapshot JSON using the
    /// established SnapshotJson contract (e.g. {"production_code": "..."}).
    /// Returns null on malformed JSON or missing property.
    /// </summary>
    private static string? ExtractStringFromSnapshot(object? raw, string propertyName)
    {
        if (raw is null || raw is DBNull)
            return null;

        var text = raw switch
        {
            string s => s,
            _ => raw.ToString()
        };

        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty(propertyName, out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// Extracts the article reference from the revision's reference_snapshot.
    /// The reference snapshot may be a JSON object with a "reference" property
    /// or a plain string. Returns null on malformed/missing.
    /// </summary>
    private static string? ExtractReferenceFromSnapshot(object? raw)
    {
        if (raw is null || raw is DBNull)
            return null;

        var text = raw switch
        {
            string s => s,
            _ => raw.ToString()
        };

        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                return doc.RootElement.GetString();

            if (doc.RootElement.TryGetProperty("reference", out var refProp) &&
                refProp.ValueKind == JsonValueKind.String)
            {
                return refProp.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}