using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Admin;

/// <summary>
/// In-memory fake of the Admin persistence port (confined to tests/*).
/// Tracks writes/audits and can simulate the self-lockout rejection and
/// optimistic-concurrency conflict behaviors. SCHEMA-RAT-03A (D-1/D-2):
/// single-template assignment (internal_users.template_id is the canonical
/// store) and template-owned functional profiles (TemplateProfiles
/// dictionary). SCHEMA-RAT-03B: no legacy-mirror side effects exist — a
/// row's ProfileTitle simply mirrors what the authority read join returns.
/// </summary>
public sealed class FakeAdminRepository : IAdminRepository
{
    public Dictionary<string, AdminUserRow> Users { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, AdminTemplateRow> Templates { get; } = new(StringComparer.Ordinal);

    /// <summary>template_id → functional profile (template-owned authority).</summary>
    public Dictionary<string, string> TemplateProfiles { get; } = new(StringComparer.Ordinal);

    public List<AuditEntry> Audits { get; } = [];

    public List<string> Writes { get; } = [];

    /// <summary>When true, the next guarded write is rejected as self-lockout.</summary>
    public bool LockoutNextWrite { get; set; }

    /// <summary>When true, the next internal-user create throws (simulates a DB failure after Auth provisioning).</summary>
    public bool FailCreateInternalOnce { get; set; }

    /// <summary>When true, CreateInternalUserAsync throws InternalUserAuthDuplicateException (audit ADM-06 mapping test).</summary>
    public bool FailAuthDuplicate { get; set; }

    /// <summary>When true, the next guarded write throws a concurrency conflict.</summary>
    public bool ConcurrencyNextWrite { get; set; }

    public int ActiveAdminCount { get; set; } = 1;

    public Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(
        string? search, CancellationToken cancellationToken = default)
    {
        IEnumerable<AdminUserRow> rows = Users.Values;
        if (!string.IsNullOrWhiteSpace(search))
            rows = rows.Where(u =>
                u.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<AdminUserRow>>(rows.ToList());
    }

    public Task<AdminUserRow?> GetUserAsync(
        string actorId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Users.TryGetValue(actorId, out var user) ? user : null);
    }

    public Task<bool> AuthUserIdAlreadyRegisteredAsync(
        Guid authUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.Any(u => u.AuthUserId == authUserId));

    public Task CreateInternalUserAsync(
        string actorId, Guid authUserId, string displayName,
        string templateId, bool active, DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (FailAuthDuplicate)
            throw new InternalUserAuthDuplicateException(
                "Já existe um utilizador interno associado a esta conta de autenticação.");
        if (FailCreateInternalOnce)
        {
            FailCreateInternalOnce = false;
            throw new InvalidOperationException("simulated internal-user create failure");
        }

        Writes.Add($"create:{actorId}");
        // The row's profile is the template-owned profile (what the read join
        // returns); no legacy mirror write is simulated (SCHEMA-RAT-03B).
        var profile = TemplateProfiles.TryGetValue(templateId, out var functionalProfile)
            ? functionalProfile
            : null;
        Users[actorId] = new AdminUserRow(
            actorId, authUserId, displayName, profile, templateId, active, createdAtUtc)
        {
            TemplateIds = [templateId]
        };
        return Task.CompletedTask;
    }

    public Task UpdateUserAsync(
        string actorId, string displayName,
        DateTimeOffset expectedUpdatedAt, DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ThrowIfConcurrencySimulated();
        Writes.Add($"update:{actorId}");
        var user = Users[actorId];
        // D-1: the user write never touches the profile (template-owned).
        Users[actorId] = user with
        {
            DisplayName = displayName,
            UpdatedAtUtc = updatedAtUtc
        };
        return Task.CompletedTask;
    }

    public Task<bool> ChangeUserTemplateAsync(
        string actorId, string templateId, DateTimeOffset expectedUpdatedAt,
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
        // SCHEMA-RAT-03B: no legacy mirror write — the row's profile is
        // simply what the authority read returns for the new template.
        Users[actorId] = user with
        {
            TemplateId = templateId,
            TemplateIds = [templateId],
            ProfileTitle = TemplateProfiles.TryGetValue(templateId, out var profile)
                ? profile
                : user.ProfileTitle,
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

    public Task<IReadOnlyList<AdminTemplateRow>> ListTemplatesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AdminTemplateRow>>(Templates.Values.ToList());

    public Task<AdminTemplateRow?> GetTemplateAsync(
        string templateId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Templates.TryGetValue(templateId, out var t) ? t : null);

    public Task<string?> GetTemplateFunctionalProfileAsync(
        string templateId, CancellationToken cancellationToken = default) =>
        Task.FromResult(TemplateProfiles.TryGetValue(templateId, out var profile) ? profile : null);

    public Task<IReadOnlyDictionary<string, string>> ListTemplateFunctionalProfilesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string>(TemplateProfiles, StringComparer.Ordinal));

    public Task CreateTemplateAsync(
        string templateId, string name, string modulesJson, string functionalProfile,
        DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default)
    {
        Writes.Add($"create_template:{templateId}");
        Templates[templateId] = new AdminTemplateRow(
            templateId, name, modulesJson, true, createdAtUtc);
        TemplateProfiles[templateId] = functionalProfile;
        return Task.CompletedTask;
    }

    public Task<bool> UpdateTemplateAsync(
        string templateId, string name, string modulesJson, bool active, string functionalProfile,
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
        TemplateProfiles[templateId] = functionalProfile;
        // Read-side contract (SCHEMA-RAT-03B): users of the template resolve
        // the new profile through the authority read — the stored row profile
        // follows the template-owned profile, exactly like the real join.
        foreach (var actorId in Users.Keys.ToList())
        {
            var user = Users[actorId];
            if (user.TemplateId == templateId && user.ProfileTitle != functionalProfile)
                Users[actorId] = user with { ProfileTitle = functionalProfile };
        }

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
