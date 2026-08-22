using System.Data;
using System.Text.Json;
using BA.Dmo.Application.Modules.Controlo;
using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Modules.Controlo;
using BA.Dmo.Domain.Shared.Kernel;
using BA.Dmo.Infrastructure.Persistence;
using Dapper;
using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.Infrastructure.Access;

/// <summary>
/// R010 — Dapper implementation of <see cref="IControloProductionContextLookup"/>.
/// Resolves the production context at the Job On's CURRENT revision: production/reference/
/// machine from the revision snapshots + the MP_CM/MF/BQ component (source ids + snapshots)
/// rows. Read-only; the created sheet pins job_on_revision_id so a later revision never
/// reinterprets it (TD-18 / Peso / Pegamentos pattern).
/// </summary>
public sealed class DapperControloProductionContextLookup : IControloProductionContextLookup
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IJobOnRepository _jobOnRepository;

    public DapperControloProductionContextLookup(
        IDbConnectionFactory connectionFactory,
        IJobOnRepository jobOnRepository)
    {
        _connectionFactory = connectionFactory;
        _jobOnRepository = jobOnRepository;
    }

    public async Task<Result<ControloFolhaProductionContext, DomainError>> ResolveAsync(
        Guid jobOnId, CancellationToken ct = default)
    {
        var jobOn = await _jobOnRepository.GetByIdAsync(jobOnId, ct);
        if (jobOn is null)
            return Result<ControloFolhaProductionContext, DomainError>.Failure(DomainError.NotFound(
                "CONTROLO_JOBON_NOT_FOUND", "Job On/produção não encontrado."));

        return await ResolveJobOnAsync(jobOn, ct);
    }

    public async Task<Result<ControloFolhaProductionContext, DomainError>> ResolveByProductionAsync(
        string productionCode, string? machineCode, CancellationToken ct = default)
    {
        var jobOn = await _jobOnRepository.GetByProductionCodeAsync(productionCode, ct);
        if (jobOn is null)
            return Result<ControloFolhaProductionContext, DomainError>.Failure(DomainError.NotFound(
                "CONTROLO_JOBON_NOT_FOUND", "Não foi encontrada a produção indicada."));
        if (!string.IsNullOrWhiteSpace(machineCode) &&
            !string.Equals(jobOn.MachineCode?.Trim(), machineCode.Trim(), StringComparison.Ordinal))
            return Result<ControloFolhaProductionContext, DomainError>.Failure(DomainError.DomainConflict(
                "CONTROLO_MACHINE_MISMATCH", "A produção não corresponde à máquina/linha indicada."));

        return await ResolveJobOnAsync(jobOn, ct);
    }

    private async Task<Result<ControloFolhaProductionContext, DomainError>> ResolveJobOnAsync(
        JobOnEntity jobOn, CancellationToken ct)
    {

        var revisionId = jobOn.CurrentRevisionId
            ?? await _jobOnRepository.GetCurrentRevisionIdAsync(jobOn.Id, ct);
        if (revisionId is null)
            return Result<ControloFolhaProductionContext, DomainError>.Failure(DomainError.DomainConflict(
                "CONTROLO_NO_REVISION", "O Job On/produção não tem revisão atual."));

        var conn = await _connectionFactory.OpenConnectionAsync(ct);
        try
        {
            const string revisionSql = @"
SELECT production_snapshot, reference_snapshot, machine_snapshot
FROM job_on_revision WHERE job_on_revision_id = @RevisionId;";
            dynamic? revision = await Db.QuerySingleOrDefaultAsync<dynamic>(
                conn, revisionSql, new { RevisionId = revisionId }, cancellationToken: ct);
            if (revision is null)
                return Result<ControloFolhaProductionContext, DomainError>.Failure(DomainError.DomainConflict(
                    "CONTROLO_REVISION_MISSING", "A revisão atual do Job On não existe."));

            var productionCode = ExtractString(revision.production_snapshot, "production_code")
                ?? jobOn.ProductionCode;
            var machineCode = ExtractString(revision.machine_snapshot, "machine_code")
                ?? jobOn.MachineCode;
            var reference = ExtractReference(revision.reference_snapshot);
            if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(productionCode))
                return Result<ControloFolhaProductionContext, DomainError>.Failure(DomainError.DomainConflict(
                    "CONTROLO_CONTEXT_INCOMPLETE", "Não foi possível resolver referência/produção do contexto."));

            const string componentsSql = @"
SELECT c.family, c.source_tool_id, c.source_lot_id,
       c.reference_snapshot, c.lot_snapshot, c.technical_name_snapshot
FROM job_on_component c
WHERE c.job_on_revision_id = @RevisionId
  AND c.family IN ('MP_CM', 'MF', 'BQ')
ORDER BY c.family, c.reference_snapshot;";
            var rows = await Db.QueryAsync<dynamic>(conn, componentsSql, new { RevisionId = revisionId }, cancellationToken: ct);

            var components = rows
                .Select(r => new ControloFolhaComponent(
                    (string)r.family,
                    r.source_tool_id as Guid?,
                    r.source_lot_id as Guid?,
                    r.reference_snapshot as string,
                    r.lot_snapshot as string,
                    r.technical_name_snapshot as string))
                .ToList()
                .AsReadOnly();

            return Result<ControloFolhaProductionContext, DomainError>.Success(
                new ControloFolhaProductionContext(
                    jobOn.Id, revisionId.Value, productionCode, reference, machineCode, components));
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
}