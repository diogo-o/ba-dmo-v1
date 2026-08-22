namespace BA.Dmo.Application.Shared.Identity;

/// <summary>
/// Internal application identity row (Plan-V3 06_DATA §3.1, GLM-ACC-01.3):
/// internal_users joined with its access template. actor_id is the stable
/// application identity (authorship); auth_user_id is the logical Supabase
/// Auth link (uuid, no FK to auth.users).
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
    string? ModulesOverrideJson = null);

/// <summary>
/// Persistence contract of the identity foundation (U-05). Parameterized SQL
/// only; implemented in Infrastructure over the U-03 persistence foundation.
/// </summary>
public interface IInternalUserRepository
{
    /// <summary>Internal user + template for an authenticated Supabase user.</summary>
    Task<InternalUserRecord?> FindByAuthUserIdAsync(
        Guid authUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether at least one ACTIVE internal user with an ACTIVE template
    /// granting admin.gerir exists (bootstrap idempotency, GLM-ACC-13).
    /// </summary>
    Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the bootstrap admin (minimal admin.gerir template + active
    /// internal user + audit event) atomically. Never grants functional
    /// modules automatically (GLM-ACC-13).
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
