using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Shared.Identity;

/// <summary>
/// External authentication identity from Supabase Auth (Plan-V3 GLM-ACC-01):
/// the Supabase Auth user UUID. Application authorization is NEVER derived
/// from Supabase role names — only from internal_users → access templates →
/// catalog (GLM-ACC-02/03).
/// </summary>
public sealed record AuthUser(Guid AuthUserId, string Email);

/// <summary>
/// HI-4: the outcome of <see cref="IAdminProvisioningAdapter.EnsureAuthUserWithStatusAsync"/>.
/// <see cref="AccountPreExisted"/> is true when the account was NOT created
/// now (the 409/422 idempotent path matched an existing account) — the
/// operator may not know that account's password, so the bootstrap flow must
/// offer a recovery path instead of assuming a known credential.
/// </summary>
public sealed record EnsuredAuthUser(Guid AuthUserId, string Email, bool AccountPreExisted);

/// <summary>
/// Supabase authentication boundary (Plan-V3 GLM-ARCH-14, PV-06, 06_DATA §14).
/// Application/Web never depend on provider SDK/HTTP types; the concrete
/// implementation lives in Infrastructure behind this port. The normal
/// request pipeline never uses service_role credentials (PV-07).
/// </summary>
public interface ISupabaseAuthAdapter
{
    /// <summary>
    /// Verifies email/password credentials against Supabase Auth. Failures are
    /// generic (never reveal whether an email exists) and fail closed.
    /// </summary>
    Task<Result<AuthUser, DomainError>> SignInWithPasswordAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Privileged provisioning boundary (Plan-V3 GLM-ARCH-14, PV-07, 06_DATA §14–15).
/// The ONLY component allowed to use service_role credentials; exclusively
/// for explicit privileged operations: the bootstrap-admin CLI, and the
/// admin.gerir-gated user create / password-reset use cases in the Web
/// pipeline (TD-16). Isolated from the normal authentication pipeline — no
/// non-admin page or handler may ever reach it.
/// </summary>
public interface IAdminProvisioningAdapter
{
    /// <summary>
    /// Ensures a Supabase Auth user exists for the email (created when
    /// absent). Idempotent. Service_role stays server-side and never appears
    /// in messages, claims or browser assets.
    /// </summary>
    Task<Result<AuthUser, DomainError>> EnsureAuthUserAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// HI-4: same guarantee as <see cref="EnsureAuthUserAsync"/>, but reports
    /// whether the account PRE-EXISTED (idempotent 409/422 path) instead of
    /// being created now. Callers that must offer a recovery path for a
    /// possibly-unknown password (bootstrap-admin) use this form.
    /// </summary>
    Task<Result<EnsuredAuthUser, DomainError>> EnsureAuthUserWithStatusAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates an admin-driven password reset for an existing Auth account
    /// (04_ACC §9). Explicit confirmation happens in the Admin use case; the
    /// current password is never retrieved or shown; no secret value may
    /// reach audit logs or responses.
    /// </summary>
    Task<Result<bool, DomainError>> RequestPasswordResetAsync(
        Guid authUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batched admin-auth email lookup. Accepts a page of Auth user IDs and
    /// returns a dictionary mapping each found ID to its email. Missing users
    /// are silently omitted (degraded to null on the caller side). The
    /// implementation must not make one request per user.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetUserEmailsAsync(
        IReadOnlyCollection<Guid> authUserIds,
        CancellationToken cancellationToken = default);
}
