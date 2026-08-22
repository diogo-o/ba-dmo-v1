namespace BA.Dmo.Application.Modules.Admin;

/// <summary>
/// Admin read/write models (Plan-V3 04_ACC §9, 06_DATA §3.1). Rows mirror
/// the U-02 schema exactly; no permission data is duplicated on users —
/// grants live only in templates (GLM-ACC-02).
/// </summary>
public sealed record AdminUserRow(
    string ActorId,
    Guid? AuthUserId,
    string DisplayName,
    string? ProfileTitle,
    string TemplateId,
    bool Active,
    DateTimeOffset UpdatedAtUtc,
    string? AuthEmail = null,
    string? ModulesOverrideJson = null);

public sealed record AdminTemplateRow(
    string TemplateId,
    string Name,
    string ModulesJson,
    bool Active,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Audit query filters (04_ACC §9 Auditoria tab): year, user, module, action,
/// result and date interval. Page sizes are the canonical 20/40/60.
/// </summary>
public sealed record AuditQueryFilter(
    int? Year,
    string? ActorUserId,
    string? ModuleId,
    string? ActionCode,
    string? Result,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Page,
    int PageSize)
{
    public static readonly int[] CanonicalPageSizes = [20, 40, 60];

    public static bool IsValidPageSize(int pageSize) =>
        CanonicalPageSizes.Contains(pageSize);
}

public sealed record AuditEventRow(
    DateTimeOffset OccurredAtUtc,
    int Year,
    string? ActorUserId,
    string? ActorNameSnapshot,
    string ModuleId,
    string ActionCode,
    string EntityType,
    string EntityId,
    string? EntityLabelSnapshot,
    string Result,
    string? Reason);

public sealed record AuditQueryResult(
    IReadOnlyList<AuditEventRow> Rows,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// One audit fact (GLM-ACC-11/TD-19): append-only, factual, no scores,
/// never secrets (passwords/tokens/service-role are forbidden content).
/// </summary>
public sealed record AuditEntry(
    DateTimeOffset OccurredAtUtc,
    string? ActorUserId,
    string? ActorNameSnapshot,
    string ModuleId,
    string ActionCode,
    string EntityType,
    string EntityId,
    string? EntityLabelSnapshot,
    string Result,
    string? Reason,
    string? BeforeSummary = null,
    string? AfterSummary = null);

/// <summary>
/// Request to create an internal user (+ Auth account, TD-16). The executor
/// identity is resolved server-side by the authorization gate — never
/// supplied by the posted form. Initial activation state defaults to active.
/// </summary>
public sealed record CreateAdminUserRequest(
    string Email,
    string Password,
    string DisplayName,
    string? ProfileTitle,
    string TemplateId,
    bool Active = true);

/// <summary>Request to edit identity/display fields of an internal user.</summary>
public sealed record UpdateAdminUserRequest(
    string ActorId,
    string DisplayName,
    string? ProfileTitle,
    DateTimeOffset ExpectedUpdatedAt);

/// <summary>Request to change the template of an internal user.</summary>
public sealed record ChangeUserTemplateRequest(
    string ActorId,
    string TemplateId,
    DateTimeOffset ExpectedUpdatedAt);

/// <summary>Request to activate/deactivate an internal user.</summary>
public sealed record SetUserActiveRequest(
    string ActorId,
    bool Active,
    DateTimeOffset ExpectedUpdatedAt);

/// <summary>Template grant as edited by Administration.</summary>
public sealed record TemplateGrantInput(string ModuleId, IReadOnlyList<string> Capabilities);

/// <summary>Request to create an access template.</summary>
public sealed record CreateTemplateRequest(
    string TemplateId,
    string Name,
    IReadOnlyList<TemplateGrantInput> Grants);

/// <summary>Request to update an access template.</summary>
public sealed record UpdateTemplateRequest(
    string TemplateId,
    string Name,
    IReadOnlyList<TemplateGrantInput> Grants,
    bool Active,
    DateTimeOffset ExpectedUpdatedAt);

/// <summary>One mirror display entry edited by Administration.</summary>
public sealed record MirrorEntryInput(string ModuleId, int DisplayOrder, bool Active);
