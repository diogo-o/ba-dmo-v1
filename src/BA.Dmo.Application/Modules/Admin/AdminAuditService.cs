using System.Text;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Admin;

/// <summary>
/// Auditoria tab of Administration (Plan-V3 04_ACC §9, UD-17/TD-19):
/// factual, append-only global audit events; filters by year/user/module/
/// action/result/interval; canonical pagination 20/40/60; annual export
/// requires audit.export. Read-only: this service never writes audit rows
/// and never computes scores/rankings/evaluations. Export content is the
/// factual row set only — no secrets exist in or reach it.
/// </summary>
public sealed class AdminAuditService
{
    private readonly AdminAuthorizationGate _gate;
    private readonly IAdminRepository _repository;

    public AdminAuditService(AdminAuthorizationGate gate, IAdminRepository repository)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Result<AuditQueryResult, DomainError>> QueryAsync(
        AuditQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AuditView);
        if (gate.IsFailure)
            return Result<AuditQueryResult, DomainError>.Failure(gate.Error);

        if (!AuditQueryFilter.IsValidPageSize(filter.PageSize))
            return Result<AuditQueryResult, DomainError>.Failure(DomainError.Validation(
                "AUDIT_PAGE_SIZE_INVALID",
                "A paginação canónica da auditoria é 20/40/60."));

        if (filter.Page < 1)
            return Result<AuditQueryResult, DomainError>.Failure(DomainError.Validation(
                "AUDIT_PAGE_INVALID", "A página deve ser maior ou igual a 1."));

        var result = await _repository.QueryAuditAsync(filter, cancellationToken);
        return Result<AuditQueryResult, DomainError>.Success(result);
    }

    /// <summary>
    /// Authorized annual export (audit.export). Emits CSV of the factual
    /// columns for the filtered set; no binaries, no secrets.
    /// </summary>
    public async Task<Result<string, DomainError>> ExportAsync(
        AuditQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AuditExport);
        if (gate.IsFailure)
            return Result<string, DomainError>.Failure(gate.Error);

        var unlimited = filter with { Page = 1, PageSize = 0 };
        var result = await _repository.QueryAuditAsync(unlimited, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine(
            "occurred_at_utc;year;actor_user_id;actor_name;module_id;action_code;" +
            "entity_type;entity_id;entity_label;result;reason");
        foreach (var row in result.Rows)
        {
            csv.AppendLine(string.Join(";",
                row.OccurredAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                row.Year,
                Csv(row.ActorUserId),
                Csv(row.ActorNameSnapshot),
                Csv(row.ModuleId),
                Csv(row.ActionCode),
                Csv(row.EntityType),
                Csv(row.EntityId),
                Csv(row.EntityLabelSnapshot),
                Csv(row.Result),
                Csv(row.Reason)));
        }

        return Result<string, DomainError>.Success(csv.ToString());
    }

    private static string Csv(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Replace(';', ',');
}
