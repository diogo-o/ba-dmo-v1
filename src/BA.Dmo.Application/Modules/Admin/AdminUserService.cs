using System.Text.Json;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Application.Shared.Persistence;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Application.Modules.Admin;

/// <summary>
/// Administration use cases for internal users (Plan-V3 04_ACC §9, U-06).
/// Every operation re-checks the canonical capability server-side through the
/// gate (hiding UI is not security — GLM-ACC-04), executes through the
/// repository port, and writes the global audit fact (GLM-ACC-11). Secrets
/// (passwords/tokens/service-role) never enter audit entries or results.
/// Self-lockout invariant: GLM-ACC-10. Concurrency: GLM-ACC-12/BT-06.
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const int PasswordPolicyMinLength = 8;

    // User-safe message for a missing required schema migration (N26). No SQL,
    // table/column names, SQLSTATE, connection details or stack traces — the
    // technical cause stays server-side (log only). Matches the established
    // BackendUnavailable path used by identity resolution.
    private const string SchemaMigrationUnavailableCode = "SCHEMA_MIGRATION_REQUIRED";
    private const string SchemaMigrationUnavailableMessage =
        "A configuração do sistema de administração está incompleta. " +
        "Contacte um administrador para aplicar as migrações de base de dados em falta.";

    private readonly AdminAuthorizationGate _gate;
    private readonly IAdminRepository _repository;
    private readonly IAdminProvisioningAdapter _provisioning;
    private readonly GrantNormalizer _normalizer;
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
        _normalizer = new GrantNormalizer(CanonicalModuleCatalog.Instance);
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<IReadOnlyList<AdminUserRow>, DomainError>> ListAsync(
        string? search, CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<IReadOnlyList<AdminUserRow>, DomainError>.Failure(gate.Error);

        IReadOnlyList<AdminUserRow> users;
        try
        {
            users = await _repository.ListUsersAsync(search, cancellationToken);
        }
        catch (SchemaMigrationRequiredException)
        {
            // N26 not applied: a schema/configuration failure -> safe
            // BackendUnavailable error. NEVER an empty list (would hide the
            // problem) and never a false "not found". The Portuguese message
            // leaks no technical detail.
            return Result<IReadOnlyList<AdminUserRow>, DomainError>.Failure(
                DomainError.BackendUnavailable(
                    SchemaMigrationUnavailableCode, SchemaMigrationUnavailableMessage));
        }
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

        AdminUserRow? user;
        try
        {
            user = await _repository.GetUserAsync(actorId, cancellationToken);
        }
        catch (SchemaMigrationRequiredException)
        {
            // N26 not applied: a schema/configuration failure -> safe
            // BackendUnavailable. NEVER a false not-found (would suggest the
            // user does not exist); the message leaks no technical detail.
            return Result<AdminUserRow, DomainError>.Failure(
                DomainError.BackendUnavailable(
                    SchemaMigrationUnavailableCode, SchemaMigrationUnavailableMessage));
        }
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
    /// the internal user. Provider failure persists nothing; duplicate
    /// registration is an explicit conflict. Retrying after a failed internal
    /// insert is safe (provisioning is idempotent). No default credentials.
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

        var template = await _repository.GetTemplateAsync(request.TemplateId, cancellationToken);
        if (template is null || !template.Active)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.Validation(
                "ADMIN_TEMPLATE_INVALID",
                "O template de acesso não existe ou está inativo."));

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
        await _repository.CreateInternalUserAsync(
            actorId,
            provisioned.Value.AuthUserId,
            request.DisplayName.Trim(),
            string.IsNullOrWhiteSpace(request.ProfileTitle) ? null : request.ProfileTitle.Trim(),
            request.TemplateId,
            request.Active,
            now,
            cancellationToken);

        await AuditAsync(gate.Value, "create", "internal_user", actorId,
            request.DisplayName.Trim(), "succeeded", null, now, cancellationToken);

        return Result<AdminUserRow, DomainError>.Success(new AdminUserRow(
            actorId,
            provisioned.Value.AuthUserId,
            request.DisplayName.Trim(),
            string.IsNullOrWhiteSpace(request.ProfileTitle) ? null : request.ProfileTitle.Trim(),
            request.TemplateId,
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
            await _repository.UpdateUserAsync(
                request.ActorId,
                request.DisplayName.Trim(),
                string.IsNullOrWhiteSpace(request.ProfileTitle) ? null : request.ProfileTitle.Trim(),
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
            $"display_name={existing.DisplayName}; profile_title={existing.ProfileTitle}",
            now, cancellationToken);

        return Result<AdminUserRow, DomainError>.Success(existing with
        {
            DisplayName = request.DisplayName.Trim(),
            ProfileTitle = string.IsNullOrWhiteSpace(request.ProfileTitle)
                ? null
                : request.ProfileTitle.Trim(),
            UpdatedAtUtc = now
        });
    }

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

        var template = await _repository.GetTemplateAsync(request.TemplateId, cancellationToken);
        if (template is null)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.Validation(
                "ADMIN_TEMPLATE_INVALID", "O template de acesso não existe."));

        var now = _clock.UtcNow;
        bool applied;
        try
        {
            applied = await _repository.ChangeUserTemplateAsync(
                request.ActorId, request.TemplateId,
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
            $"template_id={existing.TemplateId} → {request.TemplateId}",
            now, cancellationToken);

        return Result<AdminUserRow, DomainError>.Success(existing with
        {
            TemplateId = request.TemplateId,
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
    /// Composite save of the Admin user form: display/profile fields,
    /// template assignment and activation are applied as separate guarded
    /// use cases (each re-authorized and audited), refreshing the
    /// concurrency version between steps. Any failed step stops the flow
    /// and returns its explicit result.
    /// </summary>
    public async Task<Result<AdminUserRow, DomainError>> SaveUserAsync(
        string actorId,
        string displayName,
        string? profileTitle,
        string templateId,
        bool active,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        var version = expectedUpdatedAt;

        var updated = await UpdateUserAsync(
            new UpdateAdminUserRequest(actorId, displayName, profileTitle, version),
            cancellationToken);
        if (updated.IsFailure)
            return updated;
        version = updated.Value.UpdatedAtUtc;

        if (updated.Value.TemplateId != templateId)
        {
            var changed = await ChangeTemplateAsync(
                new ChangeUserTemplateRequest(actorId, templateId, version),
                cancellationToken);
            if (changed.IsFailure)
                return changed;
            version = changed.Value.UpdatedAtUtc;
        }

        if (updated.Value.Active != active)
        {
            var activation = await SetActiveAsync(
                new SetUserActiveRequest(actorId, active, version),
                cancellationToken);
            if (activation.IsFailure)
                return activation;
            return activation;
        }

        return updated;
    }

    /// <summary>
    /// Composite save of the Admin user edit form (contract §6.5): runs the
    /// existing profile/template/state save, then persists the per-user module
    /// overrides with the refreshed concurrency version. When <paramref
    /// name="modules"/> is null the module section is left untouched (callers
    /// that do not post modules keep the existing SaveUserAsync behavior).
    /// </summary>
    public async Task<Result<AdminUserRow, DomainError>> SaveUserWithModulesAsync(
        string actorId,
        string displayName,
        string? profileTitle,
        string templateId,
        bool active,
        DateTimeOffset expectedUpdatedAt,
        IReadOnlyList<TemplateGrantInput>? modules,
        CancellationToken cancellationToken = default)
    {
        var saved = await SaveUserAsync(
            actorId, displayName, profileTitle, templateId, active,
            expectedUpdatedAt, cancellationToken);
        if (saved.IsFailure)
            return saved;

        if (modules is null)
            return saved;

        return await SaveUserModulesAsync(actorId, modules, saved.Value.UpdatedAtUtc, cancellationToken);
    }

    /// <summary>
    /// Persists this user's per-user module grants (internal_users.modules_override,
    /// contract §6.4). Modules are canonical-validated with the SAME validator
    /// used for templates (GLM-ACC-03: unknown module / capability / area /
    /// duplicates are REJECTED). The Job On guard is MANDATORY and server-side:
    /// a user whose template grants the admin module (a Pure-Admin path) OR whose
    /// posted set includes the admin module can never receive a jobon grant —
    /// this keeps /jobon denied for Pure Admin end-to-end. A self-lockout
    /// guard (GLM-ACC-10) refuses a write that removes the acting admin's own
    /// admin path while no other active admin remains. Writes ONLY this
    /// actor's row; template rows are never touched. The write is audited
    /// (actionCode "change_modules") without any secret material.
    /// </summary>
    public async Task<Result<AdminUserRow, DomainError>> SaveUserModulesAsync(
        string actorId,
        IReadOnlyList<TemplateGrantInput> grants,
        DateTimeOffset expectedUpdatedAt,
        CancellationToken cancellationToken = default)
    {
        var gate = _gate.Require(CanonicalCapabilities.AdminGerir);
        if (gate.IsFailure)
            return Result<AdminUserRow, DomainError>.Failure(gate.Error);

        var existing = await _repository.GetUserAsync(actorId, cancellationToken);
        if (existing is null)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.NotFound(
                "INTERNAL_USER_NOT_FOUND", "Utilizador interno não encontrado."));

        var grantsList = grants ?? new List<TemplateGrantInput>();

        // Same canonical validation reused for templates (rejects unknown
        // modules, capabilities not owned by their module, area grants,
        // duplicates) — nothing silent (GLM-ACC-03).
        var validation = ValidateGrants(grantsList);
        if (validation.IsFailure)
            return Result<AdminUserRow, DomainError>.Failure(validation.Error);

        // OWNER GUARD: /jobon must stay denied for users with an admin path.
        var template = await _repository.GetTemplateAsync(existing.TemplateId, cancellationToken);
        if (template is null)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.Validation(
                "ADMIN_TEMPLATE_INVALID",
                "O template de acesso do utilizador não existe."));

        var templateParsed = AccessTemplateGrantsParser.Parse(template.ModulesJson);
        var templateGrantsAdmin = templateParsed.IsSuccess
            && templateParsed.Value.Any(g =>
                g.ModuleId == CanonicalModuleCatalog.AdminModuleId);

        var postedGrantsAdmin = grantsList.Any(g =>
            g.ModuleId == CanonicalModuleCatalog.AdminModuleId);
        var postedHasJobon = grantsList.Any(g =>
            g.ModuleId == CanonicalModuleCatalog.JobonModuleId);

        if ((templateGrantsAdmin || postedGrantsAdmin) && postedHasJobon)
            return Result<AdminUserRow, DomainError>.Failure(DomainError.Validation(
                "ADMIN_USER_JOON_DENIED",
                "O módulo Job On não pode ser atribuído a utilizadores com acesso de administração."));

        // GLM-ACC-10 self-lockout, service side (this write has no in-transaction
        // invariant like the sibling template/activation writes). The target
        // loses its admin path only when it currently holds one (template or
        // existing override) and the posted override drops the admin module.
        // When the acting admin IS the target and no other active admin
        // remains, refuse — mirroring the sibling ADMIN_SELF_LOCKOUT guard.
        if (gate.Value.ActorId == actorId && existing.Active && !postedGrantsAdmin)
        {
            var currentGrants = existing.ModulesOverrideJson is null
                ? templateParsed
                : AccessTemplateGrantsParser.Parse(existing.ModulesOverrideJson);
            var currentlyHasAdmin = currentGrants.IsSuccess
                && currentGrants.Value.Any(g => g.ModuleId == CanonicalModuleCatalog.AdminModuleId);
            if (currentlyHasAdmin)
            {
                var remaining = await _repository.CountActiveAdminsAsync(
                    excludeActorId: actorId, cancellationToken);
                if (remaining == 0)
                    return Result<AdminUserRow, DomainError>.Failure(DomainError.DomainConflict(
                        "ADMIN_SELF_LOCKOUT",
                        "Operação recusada: deve permanecer pelo menos um administrador ativo " +
                        "com template ativo que conceda admin.gerir."));
            }
        }

        var now = _clock.UtcNow;
        try
        {
            await _repository.SetUserModulesOverrideAsync(
                actorId, validation.Value, expectedUpdatedAt, now, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Result<AdminUserRow, DomainError>.Failure(
                DomainError.ConcurrencyConflict("ADMIN_CONCURRENCY_CONFLICT", ex.Message));
        }

        await AuditAsync(gate.Value, "change_modules", "internal_user", actorId,
            existing.DisplayName, "succeeded",
            $"per-user module overrides saved for {existing.DisplayName}",
            now, cancellationToken);

        return Result<AdminUserRow, DomainError>.Success(existing with
        {
            ModulesOverrideJson = validation.Value,
            UpdatedAtUtc = now
        });
    }

    /// <summary>
    /// Strict canonical validation of submitted per-user grant overrides —
    /// identical to the template validator (AdminTemplateService.ValidateGrants,
    /// GLM-ACC-03). Any entry outside the canonical catalog rejects the whole
    /// write. Returns the canonical JSON persisted in internal_users.modules_override.
    /// </summary>
    private Result<string, DomainError> ValidateGrants(
        IReadOnlyList<TemplateGrantInput> grants)
    {
        var input = (grants ?? new List<TemplateGrantInput>())
            .Where(g => g is not null && !string.IsNullOrWhiteSpace(g.ModuleId))
            .Select(g => new ModuleGrant(
                g.ModuleId.Trim(),
                g.Capabilities ?? Array.Empty<string>()));

        var normalized = _normalizer.Normalize(input);
        if (normalized.DiscardedEntries.Count > 0)
            return Result<string, DomainError>.Failure(DomainError.Validation(
                "ACCESS_TEMPLATE_GRANTS_INVALID",
                "Os módulos contêm entradas fora do catálogo canónico: " +
                string.Join("; ", normalized.DiscardedEntries)));

        var payload = normalized.Grants
            .Select(g => new
            {
                moduleId = g.ModuleId,
                capabilities = g.Capabilities.OrderBy(c => c, StringComparer.Ordinal).ToArray()
            })
            .OrderBy(g => g.moduleId, StringComparer.Ordinal);

        return Result<string, DomainError>.Success(
            JsonSerializer.Serialize(payload, JsonOptions));
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
