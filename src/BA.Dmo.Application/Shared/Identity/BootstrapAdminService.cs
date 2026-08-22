using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Shared.Identity;

/// <summary>
/// Inputs of the one-shot bootstrap-admin operation (GLM-ACC-13, 06_DATA §15).
/// No defaults, no hardcoded credentials: every value must be supplied
/// explicitly (environment variables at the CLI) or the operation fails.
/// </summary>
public sealed record BootstrapAdminOptions(
    string Email,
    string Password,
    string DisplayName);

public enum BootstrapAdminOutcome
{
    /// <summary>The bootstrap admin was created now.</summary>
    Created,

    /// <summary>A valid admin already exists; nothing was written (idempotent).</summary>
    AlreadyExists,

    /// <summary>
    /// HI-4 (D-HI4-1): the internal admin was created now, but the Auth
    /// account PRE-EXISTED (idempotent 409/422 path) — a recovery link was
    /// automatically requested for it, because its password may be unknown.
    /// </summary>
    PreExistedRecovered
}

/// <summary>
/// One-shot bootstrap of the first Admin (Plan-V3 GLM-ACC-13, 06_DATA §15,
/// PV-08): CLI-only, explicit, idempotent, auditable. Creates a REAL
/// Supabase Auth account through the privileged provisioning adapter plus a
/// minimal admin.gerir template and an active internal user. Never grants
/// functional modules automatically; never seeds fictitious users; never
/// runs from HTTP, hosted services or normal startup.
/// </summary>
public sealed class BootstrapAdminService
{
    /// <summary>Fixed identity of the bootstrap template (idempotent key).</summary>
    public const string BootstrapTemplateId = "tpl-bootstrap-admin";

    public const string BootstrapTemplateName = "Administração (bootstrap)";

    /// <summary>Minimal grants: admin.gerir only (GLM-ACC-13).</summary>
    public const string BootstrapModulesJson =
        "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\"]}]";

    private readonly IAdminProvisioningAdapter _provisioning;
    private readonly IInternalUserRepository _repository;
    private readonly IClock _clock;

    public BootstrapAdminService(
        IAdminProvisioningAdapter provisioning,
        IInternalUserRepository repository,
        IClock clock)
    {
        _provisioning = provisioning ?? throw new ArgumentNullException(nameof(provisioning));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<BootstrapAdminOutcome, DomainError>> RunAsync(
        BootstrapAdminOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Email)
            || string.IsNullOrWhiteSpace(options.Password)
            || string.IsNullOrWhiteSpace(options.DisplayName))
            return Result<BootstrapAdminOutcome, DomainError>.Failure(
                DomainError.Validation(
                    "BOOTSTRAP_CONFIGURATION_MISSING",
                    "Bootstrap-admin requires email, password and display name. " +
                    "Provide the explicit environment configuration; nothing is defaulted."));

        // Idempotency (GLM-ACC-13): a valid admin already present → no writes.
        if (await _repository.AdminExistsAsync(cancellationToken))
            return Result<BootstrapAdminOutcome, DomainError>.Success(
                BootstrapAdminOutcome.AlreadyExists);

        var authUser = await _provisioning.EnsureAuthUserWithStatusAsync(
            options.Email, options.Password, cancellationToken);
        if (authUser.IsFailure)
            return Result<BootstrapAdminOutcome, DomainError>.Failure(authUser.Error);

        var outcome = BootstrapAdminOutcome.Created;
        if (authUser.Value.AccountPreExisted)
        {
            // HI-4 (owner decision D-HI4-1): the Auth account pre-existed, so
            // the operator may not know its password. Automatically request
            // a recovery link (sent to the account's email — never echoed)
            // BEFORE persisting the internal admin row; on failure nothing
            // is written and the operator can safely retry (idempotent).
            var recovery = await _provisioning.RequestPasswordResetAsync(
                authUser.Value.AuthUserId, cancellationToken);
            if (recovery.IsFailure)
                return Result<BootstrapAdminOutcome, DomainError>.Failure(recovery.Error);
            outcome = BootstrapAdminOutcome.PreExistedRecovered;
        }

        var creation = new BootstrapAdminCreation(
            ActorId: authUser.Value.AuthUserId.ToString(),
            AuthUserId: authUser.Value.AuthUserId,
            DisplayName: options.DisplayName.Trim(),
            TemplateId: BootstrapTemplateId,
            TemplateName: BootstrapTemplateName,
            ModulesJson: BootstrapModulesJson,
            CreatedAtUtc: _clock.UtcNow);

        await _repository.CreateBootstrapAdminAsync(creation, cancellationToken);

        return Result<BootstrapAdminOutcome, DomainError>.Success(outcome);
    }
}
