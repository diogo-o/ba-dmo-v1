using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Shared.Identity;

/// <summary>
/// Identity resolution tests for the final access model (SCHEMA-RAT-03A
/// D-1/D-2): the user's single effective template comes from the canonical
/// direct FK (internal_users.template_id) and the functional profile comes
/// from the template (access_template_profiles). The N27 junction and the
/// legacy user-level profile mirror column (retired in SCHEMA-RAT-03B) are
/// NOT authorities — resolution fails closed on invalid/inconsistent data
/// and never merges templates.
/// </summary>
public class IdentityResolutionServiceTests
{
    private static readonly Guid AuthUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly FakeInternalUserRepository _repository = new();
    private readonly IdentityResolutionService _service;

    public IdentityResolutionServiceTests()
    {
        _service = new IdentityResolutionService(
            _repository,
            new AccessResolver(
                CanonicalModuleCatalog.Instance,
                CanonicalPageCatalog.Instance,
                CanonicalModuleCatalog.AreaChildren));
    }

    private static InternalUserRecord Record(
        bool userActive = true,
        bool templateActive = true,
        string modulesJson = "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]") =>
        new(
            ActorId: "actor-1",
            AuthUserId: AuthUserId,
            DisplayName: "Utilizador Um",
            ProfileTitle: FunctionalProfileNames.OperatorController,
            UserActive: userActive,
            TemplateId: "tpl-1",
            TemplateName: "Template 1",
            TemplateActive: templateActive,
            ModulesJson: modulesJson,
            FunctionalProfile: FunctionalProfileNames.OperatorController);

    // ---- 1. USER WITH ONE TEMPLATE: direct FK + template-owned profile ------

    [Fact]
    public async Task ValidActiveUserAndTemplate_ResolveAuthoritativeIdentity()
    {
        _repository.User = Record();

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsSuccess);
        var resolved = result.Value;
        Assert.Equal("actor-1", resolved.ActorId);
        Assert.Equal("Template 1", resolved.ProfileTitle);
        Assert.Equal(AuthUserId, resolved.User.InternalUserId);
        Assert.True(resolved.User.HasModule("boquilhas"));
        Assert.True(resolved.User.HasModule("jobon"));
        Assert.True(resolved.User.HasCapability("jobon.view"));
        Assert.False(resolved.User.HasModule("admin"));
    }

    [Fact]
    public async Task Landing_IsJobOn_AfterSuccessfulResolution()
    {
        _repository.User = Record();

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.Equal(FirstPageOutcome.Landing, result.Value.FirstPage.Outcome);
        Assert.Equal("/jobon", result.Value.FirstPage.Page!.Route);
    }

    // ---- fail-closed states ------------------------------------------------

    [Fact]
    public async Task MissingInternalUser_FailsClosed_WithInternalUserInactive()
    {
        _repository.User = null;

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsFailure);
        Assert.Equal("INTERNAL_USER_INACTIVE", result.Error.Code);
    }

    [Fact]
    public async Task InactiveInternalUser_IsDenied()
    {
        _repository.User = Record(userActive: false);

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsFailure);
        Assert.Equal("INTERNAL_USER_INACTIVE", result.Error.Code);
    }

    [Fact]
    public async Task InactiveTemplate_IsDenied()
    {
        _repository.User = Record(templateActive: false);

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsFailure);
        Assert.Equal("ACCESS_TEMPLATE_INACTIVE", result.Error.Code);
    }

    [Fact]
    public async Task MalformedTemplateGrants_FailClosed()
    {
        _repository.User = Record(modulesJson: "{ not json");

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsFailure);
        Assert.Equal("ACCESS_TEMPLATE_INACTIVE", result.Error.Code);
    }

    // ---- 6. ZERO/MULTIPLE INVALID STATES: fail-closed ----------------------

    [Fact]
    public async Task MissingTemplateFunctionalProfile_FailsClosed_AsFunctionalProfileInvalid()
    {
        // The template exists but has NO access_template_profiles row (the Dapper
        // repo returns FunctionalProfile = null). The template-owned profile is
        // the authority: a missing profile is invalid, never a fallback.
        _repository.User = Record() with { FunctionalProfile = null };

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsFailure);
        Assert.Equal("FUNCTIONAL_PROFILE_INVALID", result.Error.Code);
        Assert.Equal(ErrorCategory.Unauthorized, result.Error.Category);
    }

    [Fact]
    public async Task UnknownFunctionalProfile_FailsClosed()
    {
        _repository.User = Record() with { FunctionalProfile = "Metrologia" };

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsFailure);
        Assert.Equal("FUNCTIONAL_PROFILE_INVALID", result.Error.Code);
    }

    // ---- 4. PROFILE MIRROR DIVERGENCE: template profile wins ----------------

    [Fact]
    public async Task ProfileMirrorDivergence_FollowsTemplateProfile_NotProfileTitleMirror()
    {
        // The legacy profile mirror value says Operador /
        // Controlador; the template-owned profile (access_template_profiles)
        // says Responsável. Authorization MUST follow the template profile.
        _repository.User = Record(modulesJson:
            "[{\"moduleId\":\"jobon\",\"capabilities\":[]},{\"moduleId\":\"controlo\",\"capabilities\":[]}]") with
        {
            ProfileTitle = FunctionalProfileNames.OperatorController,
            FunctionalProfile = FunctionalProfileNames.Responsible
        };

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsSuccess);
        var access = result.Value.Access;
        // Responsável projections apply (jobon.edit + jobon.configure +
        // controlo.review + peso.aprovar); Operador/Controlador-only
        // projections (controlo.edit/controlo.submit) do not.
        Assert.True(access.HasCapability("jobon.edit"));
        Assert.True(access.HasCapability("jobon.configure"));
        Assert.True(access.HasModule("controlo"));
        Assert.True(access.HasCapability("controlo.review"));
        Assert.True(access.HasCapability("peso.aprovar"));
        Assert.False(access.HasCapability("controlo.edit"));
        Assert.False(access.HasCapability("controlo.submit"));
    }

    [Fact]
    public async Task InvalidGrantEntries_AreDiscarded_NotSilentlyRepaired()
    {
        _repository.User = Record(modulesJson:
            "[{\"moduleId\":\"ghost\",\"capabilities\":[\"peso.aprovar\"]}," +
            "{\"moduleId\":\"controlo\",\"capabilities\":[\"controlo.review\",\"ghost.cap\"]}]") with
        {
            FunctionalProfile = FunctionalProfileNames.OperatorController
        };

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsSuccess);
        var access = result.Value.Access;
        Assert.True(access.HasModule("controlo"));
        Assert.True(access.HasModule("peso"));
        Assert.True(access.HasModule("pegamentos"));
        Assert.True(access.HasCapability("controlo.edit"));
        Assert.False(access.HasCapability("controlo.review"));
        Assert.False(access.HasCapability("peso.aprovar"));
        Assert.False(access.HasModule("ghost"));
        Assert.False(access.HasCapability("ghost.cap"));
    }

    // ---- 9. ADMIN TEMPLATE --------------------------------------------------

    [Fact]
    public async Task AdminGrants_LandOnAdmin_InsteadOfJobOn()
    {
        _repository.User = Record(modulesJson:
            "[{\"moduleId\":\"admin\",\"capabilities\":[]}]") with
        {
            ProfileTitle = FunctionalProfileNames.OperatorController,
            FunctionalProfile = FunctionalProfileNames.Admin,
            TemplateName = "Administrador"
        };

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.Equal("/admin", result.Value.FirstPage.Page!.Route);
        Assert.Equal("Administrador", result.Value.ProfileTitle);
        Assert.False(result.Value.Access.HasCapability("jobon.view"));
        Assert.True(result.Value.Access.HasModule("admin"));
        Assert.True(result.Value.Access.HasCapability("admin.gerir"));
        Assert.False(result.Value.Access.HasModule("boquilhas"));
    }

    // ---- 10. OPERATIONAL TEMPLATE: never admin ------------------------------

    [Fact]
    public async Task OperationalTemplate_DoesNotGainAdminAccess_EvenWithLegacyProfileTitleAdmin()
    {
        // Legacy mirror says Admin but the template profile is operational:
        // access is operational — the mirror never grants admin.
        _repository.User = Record() with
        {
            ProfileTitle = FunctionalProfileNames.Admin,
            FunctionalProfile = FunctionalProfileNames.OperatorController
        };

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Access.HasModule("admin"));
        Assert.False(result.Value.Access.HasCapability("admin.gerir"));
        Assert.True(result.Value.Access.HasModule("jobon"));
    }

    [Fact]
    public async Task ModulesOverride_IsDormant_AndDoesNotReplaceTemplateModules()
    {
        _repository.User = Record(modulesJson:
            "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]") with
        {
            ModulesOverrideJson =
                "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\"]}]"
        };

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Access.HasModule("boquilhas"));
        Assert.False(result.Value.Access.HasModule("admin"));
    }

    [Fact]
    public async Task TemplateNames_DoNotGrantPermissions()
    {
        _repository.User = Record() with { TemplateName = "Administrador" };

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Administrador", result.Value.ProfileTitle);
        Assert.True(result.Value.Access.HasModule("jobon"));
        Assert.True(result.Value.Access.HasModule("boquilhas"));
        Assert.False(result.Value.Access.HasModule("admin"));
    }

    [Fact]
    public async Task RepositoryFailure_FailsClosed()
    {
        _repository.ThrowOnFind = true;

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.BackendUnavailable, result.Error.Category);
    }

    [Fact]
    public async Task AmbiguousIdentity_FailsClosed_AsIdentityAmbiguous_NotBackendUnavailable()
    {
        _repository.ThrowAmbiguous = true;

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsFailure);
        Assert.Equal("IDENTITY_AMBIGUOUS", result.Error.Code);
        Assert.Equal(ErrorCategory.Unauthorized, result.Error.Category);
        Assert.NotEqual(ErrorCategory.BackendUnavailable, result.Error.Category);
    }

    [Fact]
    public async Task EmptyAuthUserId_FailsClosed()
    {
        var result = await _service.ResolveAsync(Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal("INTERNAL_USER_INACTIVE", result.Error.Code);
    }

    private sealed class FakeInternalUserRepository : IInternalUserRepository
    {
        public InternalUserRecord? User { get; set; }
        public bool ThrowOnFind { get; set; }
        public bool ThrowAmbiguous { get; set; }

        public Task<InternalUserRecord?> FindByAuthUserIdAsync(
            Guid authUserId, CancellationToken cancellationToken = default)
        {
            if (ThrowAmbiguous)
                throw new AmbiguousIdentityException(authUserId);
            if (ThrowOnFind)
                throw new InvalidOperationException("Simulated database failure.");
            return Task.FromResult(User);
        }

        public Task<bool> AdminExistsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task CreateBootstrapAdminAsync(
            BootstrapAdminCreation creation, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}