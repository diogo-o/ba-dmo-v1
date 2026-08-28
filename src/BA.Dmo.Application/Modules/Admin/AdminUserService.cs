using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Admin;

/// <summary>
/// Administration use cases for internal users (Plan-V3 04_ACC §9, U-06).
/// Every operation re-checks the canonical capability server-side through the
/// gate (hiding UI is not security — GLM-ACC-04), executes through the
/// repository port, and writes the global audit fact (GLM-ACC-11). Secrets
/// (passwords/tokens/service-role) never enter audit entries or results.
/// Self-lockout invariant: GLM-ACC-10. Concurrency: GLM-ACC-12/BT-06.
///
/// SCHEMA-RAT-03A (D-1/D-2): user create/edit/save assign EXACTLY ONE
/// template (internal_users.template_id is the canonical store; changing it
/// REPLACES current access — no accumulation). The functional profile is
/// template-owned (access_template_profiles): it is never part of user
/// requests and never written per-user.
/// </summary>
public sealed class AdminUserService
{
    /// <summary>
    /// Minimum accepted length for an initial password. A conservative floor
    /// that is consistent with the Supabase Auth provider's own minimum, so
    /// an account that passes this check will never be rejected by the
    /// provider on length grounds. Higher-value policies are enforced by the
    /// privileged adapter contract (which never accepts a blank value).
    /// </summary>
    private const int PasswordPolicyMinLength = 8;

    private readonly AdminAuthorizationGate _gate;
    private readonly IAdminRepository _repository;
    private readonly IAdminProvisioningAdapter _provisioning;
    private readonly IClock _clock;

    public AdminUserService(
        AdminAuthorizationGate gate,
        IAdminRepository repository,
        IAdminProvisioningAdapter provisioning,
        IClock clock)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _provisioning = provisioning ?? throw new ArgumentNullException(nameof(provisioning));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<AdminUserRow>, DomainError>> ListAsync(
        string? search, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<IReadOnlyList<AdminUserRow>, DomainError>.Failure(gate.Error);

        var users = await _repository.ListUsersAsync(search, cancellationToken);
        if (users.Count == 0)
            return Result<IReadOnlyList<AdminUserRow>, DomainError>.Success(users);

        // Enrich with real auth emails via batched lookup.
        var authIds = users
            .Where(u => u.AuthUserId.HasValue)
            .Select(u => u.AuthUserId!.Value)
            .Distinct()
            .ToList();

        if (authIds.Count == 0)
            return Result<IReadOnlyList<AdminUserRow>, DomainError>.Success(users);

        IReadOnlyDictionary<Guid, string> emails;
        try
        {
            emails = await _provisioning.GetUserEmailsAsync(authIds, cancellationToken);
        }
        catch
        {
            // Degrade safely: leave AuthEmail null on lookup failure.
            return Result<IReadOnlyList<AdminUserRow>, DomainError>.Success(users);
        }

        if (emails.Count == 0)
            return Result<IReadOnlyList<AdminUserRow>, DomainError>.Success(users);

        var enriched = users.Select(u =>
        {
            if (u.AuthUserId.HasValue && emails.TryGetValue(u.AuthUserId.Value, out var email))
                return u with { AuthEmail = email };
            return u;
        }).ToList();
        return Result<IReadOnlyList<AdminUserRow>, DomainError>.Success(enriched);
    }

    public async Task<Result<AdminUserRow, DomainError>> GetAsync(
        string actorId, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<AdminUserRow, DomainError>.Failure(gate.Error);

        var user = await _repository.GetUserAsync(actorId, cancellationToken);
        if (user is null)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.NotFound(
                "INTERNAL_USER_NOT_FOUND", "Utilizador interno não encontrado."));

        // Real Supabase Auth email (contract §5): enrich the single row the
        // same way ListAsync enriches the list. Any lookup failure degrades to
        // AuthEmail = null (the page renders "Email indisponível") — never
        // propagates an error and never exposes a service-role path here.
        if (user.AuthUserId.HasValue)
        {
            try
            {
                var emails = await _provisioning.GetUserEmailsAsync(
                    new[] { user.AuthUserId.Value }, cancellationToken);
                if (emails.TryGetValue(user.AuthUserId.Value, out var email))
                    user = user with { AuthEmail = email };
            }
            catch
            {
                // Degrade safely: leave AuthEmail null (same as ListAsync).
            }
        }

        return Result<AdminUserRow, DomainError>.Success(user);
    }

    /// <summary>
    /// Creates the Auth account through the PRIVILEGED adapter (TD-16), then
    /// the internal user with EXACTLY ONE template assignment (D-2). The
    /// functional profile is template-owned — it is resolved from the
    /// template, never supplied by the request. Provider failure persists
    /// nothing; duplicate registration is an explicit conflict. Retrying
    /// after a failed internal insert is safe (provisioning is idempotent).
    /// No default credentials.
    /// </summary>
    public async Task<Result<AdminUserRow, DomainError>> CreateUserAsync(
        CreateAdminUserRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<AdminUserRow, DomainError>.Failure(gate.Error);

        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.DisplayName))
            return Result<AdminUserRow, DomainError>.Failure(DomainError.Validation(
                "ADMIN_USER_INVALID",
                "Email, palavra-passe e nome são obrigatórios."));

        // Server-side input validation (GLM-ACC-13/04_ACC §9): never rely on
        // client-side attributes for a privileged account-creation POST. Email
        // format and the initial-password policy are checked here so a weak or
        // malformed credential is rejected before any Supabase call. The
        // password value is validated and then used ONLY by the provider call —
        // it is never persisted, echoed or audited.
        if (!IsValidEmail(request.Email))
            return Result<AdminUserRow, DomainError>.Failure(DomainError.Validation(
                "ADMIN_USER_INVALID_EMAIL",
                "O email não tem um formato válido."));

        if (request.Password.Length < PasswordPolicyMinLength)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.Validation(
                "ADMIN_USER_WEAK_PASSWORD",
                $"A palavra-passe deve ter pelo menos {PasswordPolicyMinLength} caracteres."));

        var template = await RequireAssignableTemplateAsync(
            request.TemplateId, cancellationToken);
        if (template is not null)
            return Result<AdminUserRow, DomainError>.Failure(template);

        var provisioned = await _provisioning.EnsureAuthUserAsync(
            request.Email, request.Password, cancellationToken);
        if (provisioned.IsFailure)
            return Result<AdminUserRow, DomainError>.Failure(provisioned.Error);

        if (await _repository.AuthUserIdAlreadyRegisteredAsync(
                provisioned.Value.AuthUserId, cancellationToken))
            return Result<AdminUserRow, DomainError>.Failure(DomainError.DomainConflict(
                "ADMIN_USER_ALREADY_REGISTERED",
                "Já existe um utilizador interno associado a esta conta de autenticação."));

        var now = _clock.UtcNow;
        var actorId = provisioned.Value.AuthUserId.ToString();
        try
        {
            await _repository.CreateInternalUserAsync(
                actorId,
                provisioned.Value.AuthUserId,
                request.DisplayName.Trim(),
                request.TemplateId.Trim(),
                request.Active,
                now,
                cancellationToken);
        }
        catch (InternalUserAuthDuplicateException)
        {
            // uq_internal_users_auth_user raced (audit ADM-06/ON-02): the same
            // Auth account was linked concurrently — same clean conflict as the
            // fast-path pre-check.
            return Result<AdminUserRow, DomainError>.Failure(DomainError.DomainConflict(
                "ADMIN_USER_ALREADY_REGISTERED",
                "Já existe um utilizador interno associado a esta conta de autenticação."));
        }

        await AuditAsync(gate.Value, "create", "internal_user", actorId,
            request.DisplayName.Trim(), "succeeded", null, now, cancellationToken);

        var profileTitle = await _repository.GetTemplateFunctionalProfileAsync(
            request.TemplateId.Trim(), cancellationToken);

        return Result<AdminUserRow, DomainError>.Success(new AdminUserRow(
            actorId,
            provisioned.Value.AuthUserId,
            request.DisplayName.Trim(),
            profileTitle,
            request.TemplateId.Trim(),
            request.Active,
            now));
    }

    public async Task<Result<AdminUserRow, DomainError>> UpdateUserAsync(
        UpdateAdminUserRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<AdminUserRow, DomainError>.Failure(gate.Error);

        var existing = await _repository.GetUserAsync(request.ActorId, cancellationToken);
        if (existing is null)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.NotFound(
                "INTERNAL_USER_NOT_FOUND", "Utilizador interno não encontrado."));

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return Result<AdminUserRow, DomainError>.Failure(DomainError.Validation(
                "ADMIN_USER_INVALID", "O nome não pode ficar vazio."));

        var now = _clock.UtcNow;
        try
        {
            // D-1: the functional profile is template-owned; the user-level
            // write updates identity/display fields only (the legacy profile
            // mirror was retired in SCHEMA-RAT-03B and is never touched).
            await _repository.UpdateUserAsync(
                request.ActorId,
                request.DisplayName.Trim(),
                request.ExpectedUpdatedAt,
                now,
                cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<AdminUserRow, DomainError>.Failure(
                DomainError.ConcurrencyConflict("ADMIN_CONCURRENCY_CONFLICT", ex.Message));
        }

        await AuditAsync(gate.Value, "update", "internal_user", request.ActorId,
            request.DisplayName.Trim(), "succeeded",
            $"display_name={existing.DisplayName}",
            now, cancellationToken);

        return Result<AdminUserRow, DomainError>.Success(existing with
        {
            DisplayName = request.DisplayName.Trim(),
            UpdatedAtUtc = now
        });
    }

    /// <summary>
    /// Guarded single-template change (D-2): replaces internal_users.template_id
    /// — the previous template's access does NOT accumulate. The user's
    /// functional profile and modules follow the NEW template automatically.
    /// </summary>
    public async Task<Result<AdminUserRow, DomainError>> ChangeTemplateAsync(
        ChangeUserTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<AdminUserRow, DomainError>.Failure(gate.Error);

        var existing = await _repository.GetUserAsync(request.ActorId, cancellationToken);
        if (existing is null)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.NotFound(
                "INTERNAL_USER_NOT_FOUND", "Utilizador interno não encontrado."));

        var templateError = await RequireAssignableTemplateAsync(
            request.TemplateId, cancellationToken);
        if (templateError is not null)
            return Result<AdminUserRow, DomainError>.Failure(templateError);

        var now = _clock.UtcNow;
        bool applied;
        try
        {
            applied = await _repository.ChangeUserTemplateAsync(
                request.ActorId, request.TemplateId.Trim(),
                request.ExpectedUpdatedAt, now, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<AdminUserRow, DomainError>.Failure(
                DomainError.ConcurrencyConflict("ADMIN_CONCURRENCY_CONFLICT", ex.Message));
        }

        if (!applied)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.DomainConflict(
                "ADMIN_SELF_LOCKOUT",
                "Operação recusada: deve permanecer pelo menos um administrador ativo " +
                "com template ativo que conceda admin.gerir."));

        await AuditAsync(gate.Value, "change_template", "internal_user", request.ActorId,
            existing.DisplayName, "succeeded",
            $"template_id={existing.TemplateId} → {request.TemplateId.Trim()}",
            now, cancellationToken);

        var profileTitle = await _repository.GetTemplateFunctionalProfileAsync(
            request.TemplateId.Trim(), cancellationToken);

        return Result<AdminUserRow, DomainError>.Success(existing with
        {
            TemplateId = request.TemplateId.Trim(),
            ProfileTitle = profileTitle,
            UpdatedAtUtc = now
        });
    }

    public async Task<Result<AdminUserRow, DomainError>> SetActiveAsync(
        SetUserActiveRequest request, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<AdminUserRow, DomainError>.Failure(gate.Error);

        var existing = await _repository.GetUserAsync(request.ActorId, cancellationToken);
        if (existing is null)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.NotFound(
                "INTERNAL_USER_NOT_FOUND", "Utilizador interno não encontrado."));

        var now = _clock.UtcNow;
        bool applied;
        try
        {
            applied = await _repository.SetUserActiveAsync(
                request.ActorId, request.Active,
                request.ExpectedUpdatedAt, now, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<AdminUserRow, DomainError>.Failure(
                DomainError.ConcurrencyConflict("ADMIN_CONCURRENCY_CONFLICT", ex.Message));
        }

        if (!applied)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.DomainConflict(
                "ADMIN_SELF_LOCKOUT",
                "Operação recusada: deve permanecer pelo menos um administrador ativo " +
                "com template ativo que conceda admin.gerir."));

        await AuditAsync(gate.Value, request.Active ? "activate" : "deactivate",
            "internal_user", request.ActorId, existing.DisplayName, "succeeded", null,
            now, cancellationToken);

        return Result<AdminUserRow, DomainError>.Success(existing with
        {
            Active = request.Active,
            UpdatedAtUtc = now
        });
    }

    /// <summary>
    /// Composite save of the Admin user form: display fields, SINGLE template
    /// assignment and activation are applied as separate guarded use cases
    /// (each re-authorized and audited), refreshing the concurrency version
    /// between steps. Any failed step stops the flow and returns its explicit
    /// result. Changing the template REPLACES the previous effective access.
    /// </summary>
    public async Task<Result<AdminUserRow, DomainError>> SaveUserAsync(
        string actorId,
        string displayName,
        string templateId,
        bool active,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        var version = expectedUpdatedAt;

        var updated = await UpdateUserAsync(
            new UpdateAdminUserRequest(actorId, displayName, version),
            cancellationToken);
        if (updated.IsFailure)
            return updated;
        version = updated.Value.UpdatedAtUtc;
        var current = updated.Value;

        var normalizedTemplateId = templateId.Trim();
        if (!string.Equals(
            current.TemplateId, normalizedTemplateId, StringComparison.Ordinal))
        {
            var changed = await ChangeTemplateAsync(
                new ChangeUserTemplateRequest(actorId, normalizedTemplateId, version),
                cancellationToken);
            if (changed.IsFailure)
                return changed;
            version = changed.Value.UpdatedAtUtc;
            current = changed.Value;
        }

        if (current.Active != active)
        {
            var activation = await SetActiveAsync(
                new SetUserActiveRequest(actorId, active, version),
                cancellationToken);
            if (activation.IsFailure)
                return activation;
            return activation;
        }

        return Result<AdminUserRow, DomainError>.Success(current);
    }

    /// <summary>
    /// Admin-initiated password reset (04_ACC §9): explicit action, audited
    /// with executor/affected/result, privileged adapter only; the current
    /// password is never shown or recovered and no secret is audited.
    /// </summary>
    public async Task<Result<bool, DomainError>> RequestPasswordResetAsync(
        string targetActorId, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<bool, DomainError>.Failure(gate.Error);

        var target = await _repository.GetUserAsync(targetActorId, cancellationToken);
        if (target is null)
            return Result<bool, DomainError>.Failure(DomainError.NotFound(
                "INTERNAL_USER_NOT_FOUND", "Utilizador interno não encontrado."));

        if (target.AuthUserId is null)
            return Result<bool, DomainError>.Failure(DomainError.Validation(
                "ADMIN_USER_NO_AUTH_ACCOUNT",
                "O utilizador interno não tem conta de autenticação associada pelo que não pode iniciar um reset de palavra-passe."));

        var reset = await _provisioning.RequestPasswordResetAsync(
            target.AuthUserId.Value, cancellationToken);
        if (reset.IsFailure)
            return Result<bool, DomainError>.Failure(reset.Error);

        await AuditAsync(gate.Value, "password_reset_request", "internal_user",
            targetActorId, target.DisplayName, "succeeded", null,
            _clock.UtcNow, cancellationToken);

        return Result<bool, DomainError>.Success(true);
    }

    /// <summary>
    /// Validates that the template can be assigned to a user (D-2): exists,
    /// active and its template-owned functional profile is one of the three
    /// canonical values. Returns a DomainError when invalid, null when valid.
    /// </summary>
    private async Task<DomainError?> RequireAssignableTemplateAsync(
        string templateId, CancellationToken cancellationToken)
    {
        var id = templateId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            return DomainError.Validation(
                "ADMIN_TEMPLATE_INVALID", "Selecione um template de acesso.");

        var template = await _repository.GetTemplateAsync(id, cancellationToken);
        if (template is null || !template.Active)
            return DomainError.Validation(
                "ADMIN_TEMPLATE_INVALID",
                "O template selecionado deve existir e estar ativo.");

        if (!FunctionalProfileNames.TryParse(
            await _repository.GetTemplateFunctionalProfileAsync(id, cancellationToken),
            out _))
        {
            return DomainError.Validation(
                "ADMIN_TEMPLATE_PROFILE_MISSING",
                "O template selecionado não tem perfil funcional configurado. " +
                "Configure o perfil do template na Administração.");
        }

        return null;
    }

    private static bool IsValidEmail(string email)
    {
        // Conservative RFC-5321-compatible shape without external validation
        // dependencies: exactly one '@' with non-empty local and domain parts.
        var domainSeparator = email.IndexOf('@');
        return domainSeparator > 0
            && domainSeparator == email.LastIndexOf('@')
            && domainSeparator < email.Length - 1
            && !email.Contains(' ', StringComparison.Ordinal);
    }

    private Task AuditAsync(
        AdminExecutor executor,
        string actionCode,
        string entityType,
        string entityId,
        string entityLabel,
        string result,
        string? detail,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        _repository.InsertAuditEventAsync(new AuditEntry(
            now,
            executor.ActorId,
            executor.DisplayName,
            CanonicalCapabilities.AdminModuleId,
            actionCode,
            entityType,
            entityId,
            entityLabel,
            result,
            detail), cancellationToken);
}

/// <summary>Canonical capability ids used by Administration (modules/00).</summary>
public static class CanonicalCapabilities
{
    public const string AdminModuleId = "admin";
    public const string AdminGerir = "admin.gerir";
    public const string AuditView = "audit.view";
    public const string AuditExport = "audit.export";
}
