namespace BA.Dmo.Application.Modules.Admin;

/// <summary>
/// Persistence port of the Administration module (Plan-V3 U-06 scope;
/// tables from U-02 N01). Parameterized SQL only; optimistic concurrency via
/// updated_at (GLM-ACC-12/BT-06) is enforced inside the implementation with
/// the U-03 ConcurrencyGuard. No Supabase RPC; no module domain data.
/// </summary>
public interface IAdminRepository
{
    // ---- internal users -------------------------------------------------
    Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(
        string? search, CancellationToken cancellationToken = default);

    Task<AdminUserRow?> GetUserAsync(string actorId, CancellationToken cancellationToken = default);

    Task<bool> AuthUserIdAlreadyRegisteredAsync(
        Guid authUserId, CancellationToken cancellationToken = default);

    /// <summary>Inserts the internal user (idempotent-safe for the create flow).</summary>
    Task CreateInternalUserAsync(
        string actorId,
        Guid authUserId,
        string displayName,
        string? profileTitle,
        string templateId,
        bool active,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarded update (WHERE updated_at = expected). Throws
    /// <see cref="BA.Dmo.Application.Shared.Persistence.ConcurrencyConflictException"/>
    /// on stale writes.
    /// </summary>
    Task UpdateUserAsync(
        string actorId,
        string displayName,
        string? profileTitle,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarded template change with the self-lockout invariant validated in
    /// the same transaction (GLM-ACC-10). Returns false (write rolled back)
    /// when the change would leave no functional admin path.
    /// </summary>
    Task<bool> ChangeUserTemplateAsync(
        string actorId,
        string templateId,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarded activation change with the self-lockout invariant validated
    /// in the same transaction (GLM-ACC-10). Returns false (write rolled
    /// back) when the change would leave no functional admin path.
    /// </summary>
    Task<bool> SetUserActiveAsync(
        string actorId,
        bool active,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarded write of this user's per-user module override (internal_users.
    /// modules_override jsonb). Writes ONLY modules_override (+ updated_at);
    /// template rows are never touched, so other users on the same template are
    /// unaffected (contract §6). Throws
    /// <see cref="BA.Dmo.Application.Shared.Persistence.ConcurrencyConflictException"/>
    /// on stale writes (same guard style as the sibling user writes).
    /// </summary>
    Task SetUserModulesOverrideAsync(
        string actorId,
        string modulesJson,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);

    // ---- self-lockout support (GLM-ACC-10) -------------------------------
    /// <summary>
    /// Count of ACTIVE internal users with an ACTIVE template granting
    /// admin.gerir, optionally excluding one actor (the write target).
    /// </summary>
    Task<int> CountActiveAdminsAsync(
        string? excludeActorId = null,
        CancellationToken cancellationToken = default);
    // ---- access templates ------------------------------------------------
    Task<IReadOnlyList<AdminTemplateRow>> ListTemplatesAsync(
        CancellationToken cancellationToken = default);

    Task<AdminTemplateRow?> GetTemplateAsync(
        string templateId, CancellationToken cancellationToken = default);

    Task CreateTemplateAsync(
        string templateId,
        string name,
        string modulesJson,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarded template update with the self-lockout invariant validated in
    /// the same transaction (GLM-ACC-10). Returns false (write rolled back)
    /// when the change would leave no functional admin path.
    /// </summary>
    Task<bool> UpdateTemplateAsync(
        string templateId,
        string name,
        string modulesJson,
        bool active,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);

    // ---- audit (append-only, GLM-ACC-11/TD-19) ---------------------------
    Task InsertAuditEventAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged audit query. PageSize &lt;= 0 means "no limit" (export path);
    /// the UI uses the canonical 20/40/60 sizes.
    /// </summary>
    Task<AuditQueryResult> QueryAuditAsync(
        AuditQueryFilter filter, CancellationToken cancellationToken = default);
}
