namespace BA.Dmo.Application.Shared.Identity;

/// <summary>
/// Internal application identity row (Plan-V3 06_DATA §3.1, GLM-ACC-01.3,
/// SCHEMA-RAT-03A D-1/D-2):
/// internal_users joined with its ONE effective access template (via the
/// canonical direct FK internal_users.template_id) and that template's
/// functional profile (access_template_profiles, template-owned authority).
///
/// Authority contract (owner decisions D-1/D-2):
///   * TemplateId              — the user's single effective template
///                                (internal_users.template_id, canonical).
///   * FunctionalProfile       — the functional profile resolved through the
///                                template (access_template_profiles), the
///                                only profile consumed by authorization.
///   * ProfileTitle            — legacy compatibility mirror
///                                (internal_users.profile_title). Never a
///                                functional-access authority.
///   * ModulesOverrideJson     — dormant N26 legacy data; never consulted by
///                                resolution.
///
/// actor_id is the stable application identity (authorship); auth_user_id is
/// the logical Supabase Auth link (uuid, no FK to auth.users).
/// </summary>
public sealed record InternalUserRecord(
    string ActorId,
    Guid AuthUserId,
    string DisplayName,
    string? ProfileTitle,
    bool UserActive,
    string TemplateId,
    string TemplateName,
    bool TemplateActive,
    string ModulesJson,
    string? ModulesOverrideJson = null,
    string? FunctionalProfile = null);

/// <summary>
/// Persistence contract of the identity foundation (U-05). Parameterized SQL
/// only; implemented in Infrastructure over the U-03 persistence foundation.
/// The current-effective-template shape is resolved exclusively through
/// internal_users.template_id (SCHEMA-RAT-03A D-2); the N27 junction is not
/// consulted for identity resolution.
/// </summary>
public interface IInternalUserRepository
{
    /// <summary>Internal user + canonical template + template-owned functional profile for an authenticated Supabase user.</summary>
    Task<InternalUserRecord?> FindByAuthUserIdAsync(
        Guid authUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether at least one ACTIVE internal user with an ACTIVE template
    /// granting admin.gerir exists (bootstrap idempotency, GLM-ACC-13).
    /// Admin-ness is template-owned: functional_profile = 'Admin' on the
    /// template's access_template_profiles row AND the admin module grant.
    /// </summary>
    Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the bootstrap admin (minimal admin.gerir template + active
    /// internal user + audit event) atomically. Never grants functional
    /// modules automatically (GLM-ACC-13). The user's profile_title mirror is
    /// derived from the template's functional profile; the junction is
    /// maintained one-way only (direct FK remains authority).
    /// </summary>
    Task CreateBootstrapAdminAsync(
        BootstrapAdminCreation creation,
        CancellationToken cancellationToken = default);
}

/// <summary>Data required to create the bootstrap admin (no defaults).</summary>
public sealed record BootstrapAdminCreation(
    string ActorId,
    Guid AuthUserId,
    string DisplayName,
    string TemplateId,
    string TemplateName,
    string ModulesJson,
    DateTimeOffset CreatedAtUtc);