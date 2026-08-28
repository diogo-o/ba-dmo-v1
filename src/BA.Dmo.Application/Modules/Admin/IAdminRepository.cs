namespace BA.Dmo.Application.Modules.Admin;

/// <summary>
/// Persistence port of the Administration module (Plan-V3 U-06 scope;
/// tables from U-02 N01). Parameterized SQL only; optimistic concurrency via
/// updated_at (GLM-ACC-12/BT-06) is enforced inside the implementation with
/// the U-03 ConcurrencyGuard. No Supabase RPC; no module domain data.
///
/// SCHEMA-RAT-03A (D-1/D-2): user assignment is SINGLE-TEMPLATE and the
/// canonical store is internal_users.template_id (direct FK). The functional
/// profile is TEMPLATE-owned (access_template_profiles): template create/
/// update write the profile in the same transaction as the template row and
/// admin user projections read it through a join.
///
/// SCHEMA-RAT-03B: the legacy mirrors (the N27 junction and the user-level
/// profile mirror column) are RETIRED — no runtime write or read touches
/// either structure; N33_legacy_access_mirror_quiescence.sql revokes
/// ba_dmo_app privileges on both as the mechanical kill switch.
/// </summary>
public interface IAdminRepository
{
    // ---- internal users -------------------------------------------------
    Task<IReadOnlyList<AdminUserRow>> ListUsersAsync(
        string? search, CancellationToken cancellationToken = default);

    Task<AdminUserRow?> GetUserAsync(string actorId, CancellationToken cancellationToken = default);

    Task<bool> AuthUserIdAlreadyRegisteredAsync(
        Guid authUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the internal user (idempotent-safe for the create flow) with a
    /// single template assignment (internal_users.template_id). The legacy
    /// mirrors (junction + user-level profile mirror column) are RETIRED and
    /// are not written here (SCHEMA-RAT-03B).
    /// </summary>
    Task CreateInternalUserAsync(
        string actorId,
        Guid authUserId,
        string displayName,
        string templateId,
        bool active,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarded update of identity/display fields (WHERE updated_at = expected).
    /// NEVER writes the functional profile: the profile is template-owned.
    /// Throws
    /// <see cref="BA.Dmo.Application.Shared.Persistence.ConcurrencyConflictException"/>
    /// on stale writes.
    /// </summary>
    Task UpdateUserAsync(
        string actorId,
        string displayName,
        DateTimeOffset expectedUpdatedAt,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarded SINGLE-TEMPLATE change with the self-lockout invariant
    /// validated in the same transaction (GLM-ACC-10). Replaces
    /// internal_users.template_id (the canonical assignment). Returns false
    /// (write rolled back) when the change would leave no functional admin
    /// path. The legacy junction mirror is not written (SCHEMA-RAT-03B).
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

    // ---- access templates ------------------------------------------------
    Task<IReadOnlyList<AdminTemplateRow>> ListTemplatesAsync(
        CancellationToken cancellationToken = default);

    Task<AdminTemplateRow?> GetTemplateAsync(
        string templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Functional profile (one of the three canonical values) of a template.
    /// </summary>
    Task<string?> GetTemplateFunctionalProfileAsync(
        string templateId, CancellationToken cancellationToken = default);

    /// <summary>template_id → functional_profile for every template.</summary>
    Task<IReadOnlyDictionary<string, string>> ListTemplateFunctionalProfilesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the access template AND its template-owned functional profile
    /// in ONE transaction (profile authority, D-1). The N31 AFTER INSERT
    /// trigger also derives a deterministic initial profile; the explicit
    /// functionalProfile upsert wins.
    /// </summary>
    Task CreateTemplateAsync(
        string templateId,
        string name,
        string modulesJson,
        string functionalProfile,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarded template update with the self-lockout invariant validated in
    /// the same transaction (GLM-ACC-10). Writes access_templates, upserts
    /// access_template_profiles — the only profile write; the legacy user
    /// profile mirror is not re-derived (SCHEMA-RAT-03B). Returns false
    /// (write rolled back) when the change would leave no functional admin
    /// path.
    /// </summary>
    Task<bool> UpdateTemplateAsync(
        string templateId,
        string name,
        string modulesJson,
        bool active,
        string functionalProfile,
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