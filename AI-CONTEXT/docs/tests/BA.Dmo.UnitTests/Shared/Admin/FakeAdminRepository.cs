using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Persistence;

namespace BA.Dmo.UnitTests.Shared.Admin;

/// <summary>
/// In-memory fake of the Admin persistence port (confined to tests/*).
/// Tracks writes/audits and can simulate the self-lockout rejection and
/// optimistic-concurrency conflict behaviors.
/// </summary>
public sealed class FakeAdminRepository : IAdminRepository
{
    public Dictionary<string, AdminUserRow> Users { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, AdminTemplateRow> Templates { get; } = new(StringComparer.Ordinal);

    public List<AuditEntry> Audits { get; } = [];

    public List<string> Writes { get; } = [];

    public List<MirrorEntryInput> SavedMirrorEntries { get; } = [];

    /// <summary>When true, the next guarded write is rejected as self-lockout.</summary>
    public bool LockoutNextWrite { get; set; }

    /// <summary>When true, the next internal-user create throws (simulates a DB failure after Auth provisioning).</summary>
    public bool FailCreateInternalOnce { get; set; }

    /// <summary>When true, the next guarded write throws a concurrency conflict.</summary>
    public bool ConcurrencyNextWrite { get; set; }

    /// <summary>
    /// When true, ListUsersAsync/GetUserAsync throw
    /// <see cref="SchemaMigrationRequiredException"/> — simulates the
    /// N26-not-applied condition (missing internal_users.modules_override).
    /// </summary>
    public bool ThrowSchemaMigrationRequired { get; set; }

    public int ActiveAdminCount { get; set; } = 1;

    public Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(
        string? search, CancellationToken cancellationToken = default)
    {
        if (ThrowSchemaMigrationRequired)
            throw new SchemaMigrationRequiredException();
        IEnumerable<AdminUserRow> rows = Users.Values;
        if (!string.IsNullOrWhiteSpace(search))
            rows = rows.Where(u =>
                u.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<AdminUserRow>>(rows.ToList());
    }

    public Task<AdminUserRow?> GetUserAsync(
        string actorId, CancellationToken cancellationToken = default)
    {
        if (ThrowSchemaMigrationRequired)
            throw new SchemaMigrationRequiredException();
        return Task.FromResult(Users.TryGetValue(actorId, out var user) ? user : null);
    }

    public Task<bool> AuthUserIdAlreadyRegisteredAsync(
        Guid authUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.Any(u => u.AuthUserId == authUserId));

    public Task CreateInternalUserAsync(
        string actorId, Guid authUserId, string displayName, string? profileTitle,
        string templateId, bool active, DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (FailCreateInternalOnce)
        {
            FailCreateInternalOnce = false;
            throw new InvalidOperationException("simulated internal-user create failure");
        }

        Writes.Add($"create:{actorId}");
        Users[actorId] = new AdminUserRow(
            actorId, authUserId, displayName, profileTitle, templateId, active, createdAtUtc);
        return Task.CompletedTask;
    }

    public Task UpdateUserAsync(
        string actorId, string displayName, string? profileTitle,
        DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ThrowIfConcurrencySimulated();
        Writes.Add($"update:{actorId}");
        var user = Users[actorId];
        Users[actorId] = user with
        {
            DisplayName = displayName,
            ProfileTitle = profileTitle,
            UpdatedAtUtc = updatedAtUtc
        };
        return Task.CompletedTask;
    }

    public Task<bool> ChangeUserTemplateAsync(
        string actorId, string templateId, DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
        => ReplaceUserAccessTemplatesAsync(
            actorId, [templateId], expectedUpdatedAt, updatedAtUtc, cancellationToken);

    public Task<bool> ReplaceUserAccessTemplatesAsync(
        string actorId, IReadOnlyList<string> templateIds, DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ThrowIfConcurrencySimulated();
        if (LockoutNextWrite)
        {
            LockoutNextWrite = false;
            return Task.FromResult(false);
        }

        Writes.Add($"change_templates:{actorId}");
        var user = Users[actorId];
        Users[actorId] = user with
        {
            TemplateId = templateIds[0],
            TemplateIds = templateIds.ToArray(),
            UpdatedAtUtc = updatedAtUtc
        };
        return Task.FromResult(true);
    }

    public Task<bool> SetUserActiveAsync(
        string actorId, bool active, DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ThrowIfConcurrencySimulated();
        if (LockoutNextWrite)
        {
            LockoutNextWrite = false;
            return Task.FromResult(false);
        }

        Writes.Add($"set_active:{actorId}:{active}");
        var user = Users[actorId];
        Users[actorId] = user with { Active = active, UpdatedAtUtc = updatedAtUtc };
        return Task.FromResult(true);
    }

    public Task SetUserModulesOverrideAsync(
        string actorId, string modulesJson, DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default)
    {
        ThrowIfConcurrencySimulated();
        Writes.Add($"set_modules_override:{actorId}");
        var user = Users[actorId];
        Users[actorId] = user with { ModulesOverrideJson = modulesJson, UpdatedAtUtc = updatedAtUtc };
        return Task.CompletedTask;
    }

    public Task<int> CountActiveAdminsAsync(
        string? excludeActorId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(ActiveAdminCount);

    public Task<IReadOnlyList<AdminTemplateRow>> ListTemplatesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AdminTemplateRow>>(Templates.Values.ToList());

    public Task<AdminTemplateRow?> GetTemplateAsync(
        string templateId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Templates.TryGetValue(templateId, out var t) ? t : null);

    public Task CreateTemplateAsync(
        string templateId, string name, string modulesJson, DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        Writes.Add($"create_template:{templateId}");
        Templates[templateId] = new AdminTemplateRow(
            templateId, name, modulesJson, true, createdAtUtc);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateTemplateAsync(
        string templateId, string name, string modulesJson, bool active,
        DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ThrowIfConcurrencySimulated();
        if (LockoutNextWrite)
        {
            LockoutNextWrite = false;
            return Task.FromResult(false);
        }

        Writes.Add($"update_template:{templateId}");
        Templates[templateId] = new AdminTemplateRow(
            templateId, name, modulesJson, active, updatedAtUtc);
        return Task.FromResult(true);
    }

    public Task InsertAuditEventAsync(
        AuditEntry entry, CancellationToken cancellationToken = default)
    {
        Audits.Add(entry);
        return Task.CompletedTask;
    }

    public Task<AuditQueryResult> QueryAuditAsync(
        AuditQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var rows = Audits.Select(a => new AuditEventRow(
            a.OccurredAtUtc, a.OccurredAtUtc.Year, a.ActorUserId, a.ActorNameSnapshot,
            a.ModuleId, a.ActionCode, a.EntityType, a.EntityId, a.EntityLabelSnapshot,
            a.Result, a.Reason)).ToList();
        return Task.FromResult(new AuditQueryResult(
            rows, rows.Count, filter.Page, filter.PageSize));
    }

    private void ThrowIfConcurrencySimulated()
    {
        if (!ConcurrencyNextWrite)
            return;
        ConcurrencyNextWrite = false;
        throw new ConcurrencyConflictException(
            "Este registo foi alterado por outro administrador. Recarregue e tente novamente.");
    }
}
