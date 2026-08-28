using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Shared.Admin;

/// <summary>
/// U-06 Admin user-management tests (Plan-V3 04_ACC §9–12, GLM-ACC-10/11/12).
/// High-value coverage: capability gate on every mutation, provisioning
/// happy/error paths, duplicate handling, concurrency conflict, self-lockout,
/// audit facts without secrets, fail-closed authorization. All collaborators
/// are fakes — no live Supabase/DB.
/// </summary>
public class AdminUserServiceTests
{
    private static readonly Guid NewAuthUserId =
        Guid.Parse("99999999-8888-7777-6666-555555555555");

    private readonly FakeAdminRepository _repository = new();
    private readonly FakeProvisioning _provisioning = new();
    private readonly FakeCurrentUserAccessor _identity = new();
    private readonly AdminUserService _service;

    public AdminUserServiceTests()
    {
        var gate = new AdminAuthorizationGate(_identity);
        _service = new AdminUserService(
            gate, _repository, _provisioning, new FixedClock(
                new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero)));

        _repository.Templates["tpl-active"] = new AdminTemplateRow(
            "tpl-active", "Template ativo", "[]", Active: true,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _repository.Templates["tpl-inactive"] = new AdminTemplateRow(
            "tpl-inactive", "Template inativo", "[]", Active: false,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _repository.Templates["tpl-second"] = new AdminTemplateRow(
            "tpl-second", "Segundo template", "[]", Active: true,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        // Template-owned functional profiles (D-1): the authority the service
        // validates against and the source of the displayed profile.
        _repository.TemplateProfiles["tpl-active"] = FunctionalProfileNames.OperatorController;
        _repository.TemplateProfiles["tpl-second"] = FunctionalProfileNames.Responsible;
        _repository.TemplateProfiles["tpl-inactive"] = FunctionalProfileNames.OperatorController;

        _repository.Users["user-1"] = new AdminUserRow(
            "user-1", Guid.Parse("11111111-2222-3333-4444-555555555555"),
            "Utilizador Um", FunctionalProfileNames.OperatorController, "tpl-active", Active: true,
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));

        _identity.GrantAdmin();
    }

    // ---- authorization gate (fail closed, capability only) ----------------

    [Fact]
    public async Task Mutation_WithoutCapability_IsDenied_AndWritesNothing()
    {
        _identity.GrantNone();

        var result = await _service.SetActiveAsync(
            new SetUserActiveRequest("user-1", false, Version("user-1")));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
        Assert.Empty(_repository.Writes);
        Assert.Empty(_repository.Audits);
    }

    [Fact]
    public async Task Mutation_WithoutResolvedIdentity_IsDenied()
    {
        _identity.User = null;

        var result = await _service.CreateUserAsync(new CreateAdminUserRequest(
            "novo@ba-dmo.example", "password", "Novo", "tpl-active"));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
        Assert.Empty(_provisioning.Calls);
    }

    // ---- create user (provisioning boundary) ------------------------------

    [Fact]
    public async Task CreateUser_HappyPath_ProvisionsPersistsAndAudits_WithoutSecrets()
    {
        var result = await _service.CreateUserAsync(new CreateAdminUserRequest(
            "novo@ba-dmo.example", "secret-password-value", "Novo Utilizador", "tpl-active"));

        Assert.True(result.IsSuccess);
        Assert.Equal("create", Assert.Single(_repository.Audits).ActionCode);
        Assert.Equal("internal_user", _repository.Audits[0].EntityType);
        Assert.Equal("admin", _repository.Audits[0].ModuleId);
        // Secrets never reach audit entries or results (GLM-DATA-06/U-06 rule).
        Assert.DoesNotContain(_repository.Audits, a =>
            (a.Reason ?? string.Empty).Contains("secret-password-value", StringComparison.Ordinal)
            || (a.EntityLabelSnapshot ?? string.Empty).Contains("secret-password-value", StringComparison.Ordinal));
        Assert.Single(_provisioning.Calls);
    }

    [Fact]
    public async Task CreateUser_AssignsExactlyOneTemplate_AndProfileComesFromTemplateProfile()
    {
        // D-2: exactly ONE template is assigned (internal_users.template_id).
        // D-1: the displayed profile comes from the template-owned profile
        // (read join) — no legacy mirror write exists (SCHEMA-RAT-03B).
        var result = await _service.CreateUserAsync(new CreateAdminUserRequest(
            "novo@ba-dmo.example", "password", "Novo Utilizador", "tpl-active"));

        Assert.True(result.IsSuccess);
        var row = _repository.Users[NewAuthUserId.ToString()];
        Assert.Equal("tpl-active", row.TemplateId);
        Assert.Single(row.AssignedTemplateIds);
        Assert.Equal("tpl-active", row.TemplateIds!.Single());
        Assert.Equal(FunctionalProfileNames.OperatorController, row.ProfileTitle);
        Assert.True(row.Active);
    }

    [Fact]
    public async Task CreateUser_InactiveTemplate_IsRejected_BeforeProvisioning()
    {
        var result = await _service.CreateUserAsync(new CreateAdminUserRequest(
            "novo@ba-dmo.example", "password", "Novo", "tpl-inactive"));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_TEMPLATE_INVALID", result.Error.Code);
        Assert.Empty(_provisioning.Calls);
        Assert.Empty(_repository.Writes);
    }

    [Fact]
    public async Task CreateUser_TemplateWithoutFunctionalProfile_IsRejected_BeforeProvisioning()
    {
        // D-1: the template-owned profile is required; a template without one
        // is not assignable (no invented profile), fail closed.
        _repository.TemplateProfiles.Remove("tpl-active");

        var result = await _service.CreateUserAsync(new CreateAdminUserRequest(
            "novo@ba-dmo.example", "password", "Novo", "tpl-active"));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_TEMPLATE_PROFILE_MISSING", result.Error.Code);
        Assert.Empty(_provisioning.Calls);
        Assert.Empty(_repository.Writes);
    }

    [Fact]
    public async Task CreateUser_WeakPassword_IsRejected_BeforeProvisioning()
    {
        var result = await _service.CreateUserAsync(new CreateAdminUserRequest(
            "novo@ba-dmo.example", "short12", "Novo", "tpl-active"));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_USER_WEAK_PASSWORD", result.Error.Code);
        Assert.Empty(_provisioning.Calls);
        Assert.Empty(_repository.Writes);
        Assert.Empty(_repository.Audits);
    }

    [Fact]
    public async Task CreateUser_InvalidEmail_IsRejected_BeforeProvisioning()
    {
        var result = await _service.CreateUserAsync(new CreateAdminUserRequest(
            "not-an-email", "longenough-password", "Novo", "tpl-active"));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_USER_INVALID_EMAIL", result.Error.Code);
        Assert.Empty(_provisioning.Calls);
        Assert.Empty(_repository.Writes);
        Assert.Empty(_repository.Audits);
    }

    [Fact]
    public async Task CreateUser_ProviderFailure_PersistsNothing()
    {
        _provisioning.FailEnsure = DomainError.BackendUnavailable(
            "AUTH_PROVIDER_UNAVAILABLE", "Provider down.");

        var result = await _service.CreateUserAsync(new CreateAdminUserRequest(
            "novo@ba-dmo.example", "password", "Novo", "tpl-active"));

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_PROVIDER_UNAVAILABLE", result.Error.Code);
        Assert.DoesNotContain(_repository.Writes, w => w.StartsWith("create:", StringComparison.Ordinal));
        Assert.Empty(_repository.Audits);
    }

    [Fact]
    public async Task CreateUser_DuplicateRegistration_IsExplicitConflict()
    {
        _provisioning.ProvisionedAuthUserId = _repository.Users["user-1"].AuthUserId.GetValueOrDefault();

        var result = await _service.CreateUserAsync(new CreateAdminUserRequest(
            "existente@ba-dmo.example", "password", "Duplicado", "tpl-active"));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_USER_ALREADY_REGISTERED", result.Error.Code);
    }

    [Fact]
    public async Task CreateUser_ConcurrentAuthDuplicate_Raw23505_MapsToSameCleanConflict()
    {
        // uq_internal_users_auth_user raced between the pre-check and the insert
        // (audit ADM-06/ON-02): the insert throws and the service maps it to the
        // SAME clean conflict as the fast-path pre-check.
        _repository.FailAuthDuplicate = true;

        var result = await _service.CreateUserAsync(new CreateAdminUserRequest(
            "novo@ba-dmo.example", "password", "Novo", "tpl-active"));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_USER_ALREADY_REGISTERED", result.Error.Code);
        Assert.DoesNotContain(_repository.Users, u => u.Key == NewAuthUserId.ToString());
        Assert.Empty(_repository.Audits);
    }

    // ---- B8: partial failure (Auth created, internal insert fails) ----------
    // The irreversible Auth provisioning happens first; the internal insert then
    // throws. The smallest safe recovery in this architecture (Postgres + external
    // Auth cannot share one transaction) is: surface the failure, then let the
    // operator retry — provisioning is idempotent, so the retry reconciles the
    // mapping and never leaves a silent orphaned identity.

    [Fact]
    public async Task CreateUser_InternalInsertThrows_SurfacesAndRetryReconcilesNoOrphan()
    {
        var request = new CreateAdminUserRequest(
            "novo@ba-dmo.example", "secret-password-value", "Novo Utilizador", "tpl-active");

        // First attempt: Auth provisioned (one provisioning call), internal insert throws.
        _repository.FailCreateInternalOnce = true;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateUserAsync(request));
        Assert.Single(_provisioning.Calls);          // Auth identity was created
        Assert.DoesNotContain(_repository.Users, u => u.Key == NewAuthUserId.ToString()); // no internal mapping yet

        // Retry: provisioning is idempotent (finds existing Auth), internal insert completes.
        var retry = await _service.CreateUserAsync(request);
        Assert.True(retry.IsSuccess);
        Assert.Equal(2, _provisioning.Calls.Count);   // expected: create/ensure called again, reconciled
        Assert.True(_repository.Users.ContainsKey(NewAuthUserId.ToString()));
        Assert.Equal(NewAuthUserId, _repository.Users[NewAuthUserId.ToString()].AuthUserId);
        // No duplicate internal mapping for the same Auth identity.
        Assert.Single(_repository.Users.Values.Where(u => u.AuthUserId == NewAuthUserId));
    }

    // ---- edit / concurrency ------------------------------------------------

    [Fact]
    public async Task UpdateUser_PersistsAndAudits()
    {
        var result = await _service.UpdateUserAsync(new UpdateAdminUserRequest(
            "user-1", "Nome Novo", Version("user-1")));

        Assert.True(result.IsSuccess);
        Assert.Equal("Nome Novo", _repository.Users["user-1"].DisplayName);
        Assert.Equal("update", Assert.Single(_repository.Audits).ActionCode);
    }

    [Fact]
    public async Task UpdateUser_NeverChangesProfile_WhichIsTemplateOwned()
    {
        // D-1: the functional profile is template-owned; a user edit never
        // rewrites the profile (the legacy user-level mirror was retired in
        // SCHEMA-RAT-03B) — divergences are healed by the template-owned
        // profile resolution, not by user-level writes.
        var result = await _service.UpdateUserAsync(new UpdateAdminUserRequest(
            "user-1", "Nome Novo", Version("user-1")));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            FunctionalProfileNames.OperatorController,
            _repository.Users["user-1"].ProfileTitle);
        Assert.Equal("tpl-active", _repository.Users["user-1"].TemplateId);
    }

    [Fact]
    public async Task UpdateUser_StaleVersion_IsConcurrencyConflict_WithReloadMessage()
    {
        _repository.ConcurrencyNextWrite = true;

        var result = await _service.UpdateUserAsync(new UpdateAdminUserRequest(
            "user-1", "Nome Novo", Version("user-1")));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.ConcurrencyConflict, result.Error.Category);
        Assert.Contains("Recarregue", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_repository.Audits);
    }

    // ---- self-lockout (GLM-ACC-10) -----------------------------------------

    [Fact]
    public async Task DeactivateLastAdmin_IsRejected_AsSelfLockout()
    {
        _repository.LockoutNextWrite = true;

        var result = await _service.SetActiveAsync(
            new SetUserActiveRequest("user-1", false, Version("user-1")));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_SELF_LOCKOUT", result.Error.Code);
        Assert.True(_repository.Users["user-1"].Active); // unchanged
    }

    [Fact]
    public async Task DeactivateAdmin_WhenAnotherAdminRemains_IsAllowed_AndAudited()
    {
        var result = await _service.SetActiveAsync(
            new SetUserActiveRequest("user-1", false, Version("user-1")));

        Assert.True(result.IsSuccess);
        Assert.False(_repository.Users["user-1"].Active);
        Assert.Equal("deactivate", Assert.Single(_repository.Audits).ActionCode);
    }

    [Fact]
    public async Task ChangeTemplate_LockoutRejected_LeavesUserUnchanged()
    {
        _repository.LockoutNextWrite = true;

        var result = await _service.ChangeTemplateAsync(
            new ChangeUserTemplateRequest("user-1", "tpl-active", Version("user-1")));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_SELF_LOCKOUT", result.Error.Code);
        Assert.Equal("tpl-active", _repository.Users["user-1"].TemplateId);
    }

    // ---- 2/8. TEMPLATE CHANGE REPLACES effective access ---------------------

    [Fact]
    public async Task ChangeTemplate_ReplacesPreviousTemplate_DoesNotAccumulate()
    {
        // D-2: changing the template REPLACES the canonical single assignment:
        // the previous template does not accumulate anywhere.
        var result = await _service.ChangeTemplateAsync(
            new ChangeUserTemplateRequest("user-1", "tpl-second", Version("user-1")));

        Assert.True(result.IsSuccess);
        var row = _repository.Users["user-1"];
        Assert.Equal("tpl-second", row.TemplateId);
        Assert.Equal(["tpl-second"], row.AssignedTemplateIds);
        Assert.Equal(FunctionalProfileNames.Responsible, row.ProfileTitle);
        Assert.Equal("change_template", Assert.Single(_repository.Audits).ActionCode);
    }

    [Fact]
    public async Task ChangeTemplate_InactiveTemplate_IsRejected()
    {
        var result = await _service.ChangeTemplateAsync(
            new ChangeUserTemplateRequest("user-1", "tpl-inactive", Version("user-1")));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_TEMPLATE_INVALID", result.Error.Code);
        Assert.Equal("tpl-active", _repository.Users["user-1"].TemplateId);
    }

    [Fact]
    public async Task SaveUser_NewTemplate_ReplacesPriorEffectiveTemplate()
    {
        // Composite edit: a different template replaces the previous one and
        // only one assignment remains.
        var result = await _service.SaveUserAsync(
            "user-1", "Nome Novo", "tpl-second", active: true, Version("user-1"));

        Assert.True(result.IsSuccess);
        var row = _repository.Users["user-1"];
        Assert.Equal("tpl-second", row.TemplateId);
        Assert.Single(row.AssignedTemplateIds);
        Assert.Equal("tpl-second", row.TemplateIds!.Single());
        Assert.Equal(FunctionalProfileNames.Responsible, row.ProfileTitle);
    }

    [Fact]
    public async Task SaveUser_SameTemplate_DoesNotEmitTemplateChange()
    {
        var result = await _service.SaveUserAsync(
            "user-1", "Nome Novo", "tpl-active", active: true, Version("user-1"));

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(_repository.Writes, w => w.StartsWith("change_templates:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListAsync_EmptyRepository_ReturnsEmptySuccess()
    {
        _repository.Users.Clear();

        var result = await _service.ListAsync(null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Empty(_provisioning.EmailLookupCalls);
    }

    [Fact]
    public async Task GetAsync_UnknownUser_ReturnsNotFound()
    {
        var result = await _service.GetAsync("unknown-user");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, result.Error.Category);
        Assert.Equal("INTERNAL_USER_NOT_FOUND", result.Error.Code);
    }

    // ---- password reset (privileged adapter path) ---------------------------

    [Fact]
    public async Task PasswordReset_GoesThroughPrivilegedAdapter_AndAuditsWithoutSecrets()
    {
        var result = await _service.RequestPasswordResetAsync("user-1");

        Assert.True(result.IsSuccess);
        var call = Assert.Single(_provisioning.ResetCalls);
        Assert.Equal(_repository.Users["user-1"].AuthUserId, call);

        var audit = Assert.Single(_repository.Audits);
        Assert.Equal("password_reset_request", audit.ActionCode);
        Assert.Equal("user-1", audit.EntityId);
        Assert.DoesNotContain("password", audit.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // ---- list enrichment (batched auth-email lookup) ------------------------

    [Fact]
    public async Task ListAsync_EnrichesMatchingRows_WithAuthEmail()
    {
        // A returned Auth email enriches the matching AdminUserRow.
        var authId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        _provisioning.Emails[authId] = "user@ba-dmo.example";

        var result = await _service.ListAsync(null);
        Assert.True(result.IsSuccess);
        var users = result.Value;

        var row = Assert.Single(users);
        Assert.Equal("user@ba-dmo.example", row.AuthEmail);
        Assert.Single(_provisioning.EmailLookupCalls);
    }

    [Fact]
    public async Task ListAsync_MissingAuthUser_LeavesAuthEmailNull()
    {
        // A requested Auth id absent from the lookup result stays null and the
        // row list is still returned (no throw, no filtering out).
        var result = await _service.ListAsync(null);
        Assert.True(result.IsSuccess);
        var users = result.Value;

        var row = Assert.Single(users);
        Assert.Null(row.AuthEmail);
        Assert.Equal(
            _repository.Users["user-1"].AuthUserId,
            Assert.Single(_provisioning.EmailLookupCalls));
    }

    [Fact]
    public async Task ListAsync_LookupFailure_ReturnsTheAdminList_WithoutThrowing_AndWithNullEmail()
    {
        // A lookup failure must not surface: the list comes back whole with a
        // null email (graceful degradation).
        _provisioning.FailLookup = new HttpRequestException("provider down");

        var result = await _service.ListAsync(null);
        Assert.True(result.IsSuccess);
        var users = result.Value;

        Assert.Single(users);
        Assert.All(users, u => Assert.Null(u.AuthEmail));
    }

    [Fact]
    public async Task ListAsync_WithoutAdminGerir_ReturnsEmpty_AndDoesNotTriggerEmailLookup()
    {
        // A caller without admin.gerir cannot obtain the list and the email
        // lookup is never triggered.
        _identity.GrantNone();

        var result = await _service.ListAsync(null);

        Assert.True(result.IsFailure);
        Assert.Empty(_provisioning.EmailLookupCalls);
    }

    private DateTimeOffset Version(string actorId) => _repository.Users[actorId].UpdatedAtUtc;

    private sealed class FakeProvisioning : IAdminProvisioningAdapter
    {
        public Guid ProvisionedAuthUserId { get; set; } = NewAuthUserId;

        public List<(string Email, string Password)> Calls { get; } = [];

        public List<Guid> ResetCalls { get; } = [];

        /// <summary>Lookup results to return keyed by Auth user id (email enrichment).</summary>
        public Dictionary<Guid, string> Emails { get; } = new();

        /// <summary>Requested Auth ids seen by the batched email lookup.</summary>
        public List<Guid> EmailLookupCalls { get; } = [];

        /// <summary>When set, the email lookup fails (service degrades to null).</summary>
        public Exception? FailLookup { get; set; }

        public DomainError? FailEnsure { get; set; }

        public Task<Result<AuthUser, DomainError>> EnsureAuthUserAsync(
            string email, string password, CancellationToken cancellationToken = default)
        {
            Calls.Add((email, password));
            return FailEnsure is not null
                ? Task.FromResult(Result<AuthUser, DomainError>.Failure(FailEnsure))
                : Task.FromResult(Result<AuthUser, DomainError>.Success(
                    new AuthUser(ProvisionedAuthUserId, email)));
        }

        public Task<Result<EnsuredAuthUser, DomainError>> EnsureAuthUserWithStatusAsync(
            string email, string password, CancellationToken cancellationToken = default)
        {
            Calls.Add((email, password));
            return FailEnsure is not null
                ? Task.FromResult(Result<EnsuredAuthUser, DomainError>.Failure(FailEnsure))
                : Task.FromResult(Result<EnsuredAuthUser, DomainError>.Success(
                    new EnsuredAuthUser(ProvisionedAuthUserId, email, AccountPreExisted: false)));
        }

        public Task<Result<bool, DomainError>> RequestPasswordResetAsync(
            Guid authUserId, CancellationToken cancellationToken = default)
        {
            ResetCalls.Add(authUserId);
            return Task.FromResult(Result<bool, DomainError>.Success(true));
        }

        public Task<IReadOnlyDictionary<Guid, string>> GetUserEmailsAsync(
            IReadOnlyCollection<Guid> authUserIds,
            CancellationToken cancellationToken = default)
        {
            EmailLookupCalls.AddRange(authUserIds);
            if (FailLookup is not null)
                throw FailLookup;

            var result = new Dictionary<Guid, string>();
            foreach (var id in authUserIds)
            {
                if (Emails.TryGetValue(id, out var email) && !string.IsNullOrWhiteSpace(email))
                    result[id] = email;
            }

            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(result);
        }
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser? User { get; set; }

        public CurrentUser? Current => User;

        public void GrantAdmin() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            "Administrador",
            new[] { "admin" },
            new[] { "admin.gerir", "audit.view", "audit.export" });

        public void GrantNone() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
            "Operador",
            new[] { "boquilhas" },
            Array.Empty<string>());
    }

    private sealed class FixedClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }
}
