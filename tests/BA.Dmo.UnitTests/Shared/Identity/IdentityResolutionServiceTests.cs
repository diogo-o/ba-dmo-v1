using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Application.Shared.Identity;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Shared.Identity;

/// <summary>
/// U-05 identity resolution tests (Plan-V3 GLM-ACC-01, GLM-ARCH-18):
/// authoritative pipeline auth_user_id → internal_users → template → U-04
/// access; fail-closed behavior; no role-name routing; Job On landing.
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
        string modulesJson = "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]") =>
        new(
            ActorId: "actor-1",
            AuthUserId: AuthUserId,
            DisplayName: "Utilizador Um",
            ProfileTitle: "Metrologia",
            UserActive: userActive,
            TemplateId: "tpl-1",
            TemplateName: "Template 1",
            TemplateActive: templateActive,
            ModulesJson: modulesJson);

    [Fact]
    public async Task ValidActiveUserAndTemplate_ResolveAuthoritativeIdentity()
    {
        _repository.User = Record();

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsSuccess);
        var resolved = result.Value;
        Assert.Equal("actor-1", resolved.ActorId);
        Assert.Equal("Metrologia", resolved.ProfileTitle);
        Assert.Equal(AuthUserId, resolved.User.InternalUserId);
        Assert.True(resolved.User.HasModule("boquilhas"));
        // Universal Job On query access (UD-16) — derived, never from claims.
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

    [Fact]
    public async Task InvalidGrantEntries_AreDiscarded_NotSilentlyRepaired()
    {
        _repository.User = Record(modulesJson:
            "[{\"moduleId\":\"ghost\",\"capabilities\":[\"peso.aprovar\"]}," +
            "{\"moduleId\":\"peso\",\"capabilities\":[\"peso.aprovar\",\"ghost.cap\"]}]");

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.True(result.IsSuccess);
        var access = result.Value.Access;
        Assert.True(access.HasModule("peso"));
        Assert.True(access.HasCapability("peso.aprovar"));
        Assert.False(access.HasModule("ghost"));
        Assert.False(access.HasCapability("ghost.cap"));
    }

    [Fact]
    public async Task AdminGrants_LandOnAdmin_InsteadOfJobOn()
    {
        // Owner decision: an Administrator's only working area is the Admin
        // page. It is NOT granted jobon.view, so it lands on /admin (not the
        // universal Job On work landing).
        _repository.User = Record(modulesJson:
            "[{\"moduleId\":\"admin\",\"capabilities\":[\"admin.gerir\",\"audit.view\"]}]");

        var result = await _service.ResolveAsync(AuthUserId);

        Assert.Equal("/admin", result.Value.FirstPage.Page!.Route);
        Assert.False(result.Value.Access.HasCapability("jobon.view"));
        Assert.True(result.Value.Access.HasModule("admin"));
        Assert.True(result.Value.Access.HasCapability("admin.gerir"));
    }

    [Fact]
    public async Task TemplateNames_DoNotInfluenceResolution()
    {
        // Identical grants under role-sounding template names resolve
        // identically: behavior derives from grants only.
        _repository.User = Record();
        var operador = await _service.ResolveAsync(AuthUserId);

        _repository.User = Record() with { TemplateId = "tpl-2", TemplateName = "Administrador" };
        var administrador = await _service.ResolveAsync(AuthUserId);

        Assert.Equal(operador.Value.FirstPage.Page!.Route, administrador.Value.FirstPage.Page!.Route);
        Assert.Equal(
            operador.Value.Access.AuthorizedModuleIds.OrderBy(x => x),
            administrador.Value.Access.AuthorizedModuleIds.OrderBy(x => x));
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
        // HI-2: duplicate internal rows for one auth_user_id is a
        // data-integrity condition. It must NOT be misclassified as a
        // backend outage (that diagnosis points at a healthy database).
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
