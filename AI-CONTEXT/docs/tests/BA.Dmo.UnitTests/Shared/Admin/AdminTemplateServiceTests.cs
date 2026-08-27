using BA.Dmo.Application.Modules.Admin;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Shared.Admin;

/// <summary>
/// U-06 access-template tests (Plan-V3 04_ACC §9, GLM-ACC-03/10/12):
/// strict canonical validation (unknown modules/capabilities and ownership
/// violations reject the write — never silently granted), capability-gated
/// mutations, self-lockout and optimistic concurrency. Single template model:
/// the U-04 catalog/normalizer, no second model.
/// </summary>
public class AdminTemplateServiceTests
{
    private readonly FakeAdminRepository _repository = new();
    private readonly FakeCurrentUserAccessor _identity = new();
    private readonly AdminTemplateService _service;

    public AdminTemplateServiceTests()
    {
        var gate = new AdminAuthorizationGate(_identity);
        _service = new AdminTemplateService(
            gate,
            _repository,
            new GrantNormalizer(CanonicalModuleCatalog.Instance),
            new FixedClock(new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero)));

        _repository.Templates["tpl-1"] = new AdminTemplateRow(
            "tpl-1", "Template 1",
            "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}]",
            Active: true,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        _identity.GrantAdmin();
    }

    [Fact]
    public async Task CreateTemplate_ValidGrants_PersistsCanonicalJson()
    {
        var result = await _service.CreateAsync(new CreateTemplateRequest(
            "tpl-novo", "Template Novo",
            new[]
            {
                new TemplateGrantInput("controlo", Array.Empty<string>()),
                new TemplateGrantInput("boquilhas", Array.Empty<string>())
            },
            FunctionalProfileNames.OperatorController));

        Assert.True(result.IsSuccess);
        var saved = _repository.Templates["tpl-novo"];
        // Canonical: modules sorted, capabilities sorted, exact ids only.
        Assert.Equal(
            "[{\"moduleId\":\"boquilhas\",\"capabilities\":[]}," +
            "{\"moduleId\":\"controlo\",\"capabilities\":[]}]",
            saved.ModulesJson);
        // D-1: the template-owned functional profile is persisted with the
        // template (one authoritative write path).
        Assert.Equal(
            FunctionalProfileNames.OperatorController,
            _repository.TemplateProfiles["tpl-novo"]);
        Assert.Equal("create", Assert.Single(_repository.Audits).ActionCode);
        Assert.Equal("access_template", _repository.Audits[0].EntityType);
    }

    [Theory]
    [InlineData("ghost_module", "")]                    // unknown module
    [InlineData("boquilhas", "peso.aprovar")]          // capability of another module
    [InlineData("peso", "")]                           // internal, nonassignable
    [InlineData("jobon", "jobon.view")]                // capabilities come from profile
    public async Task CreateTemplate_InvalidGrants_AreRejected_WithExplicitReport(
        string moduleId, string capability)
    {
        var result = await _service.CreateAsync(new CreateTemplateRequest(
            "tpl-invalido", "Template Inválido",
            new[]
            {
                new TemplateGrantInput(moduleId,
                    string.IsNullOrEmpty(capability) ? Array.Empty<string>() : new[] { capability })
            },
            FunctionalProfileNames.OperatorController));

        Assert.True(result.IsFailure);
        Assert.Equal("ACCESS_TEMPLATE_GRANTS_INVALID", result.Error.Code);
        Assert.DoesNotContain("tpl-invalido", _repository.Templates.Keys);
        Assert.Empty(_repository.Audits); // nothing persisted, nothing audited
    }

    [Fact]
    public async Task CreateTemplate_DuplicateId_IsConflict()
    {
        var result = await _service.CreateAsync(new CreateTemplateRequest(
            "tpl-1", "Outro nome", Array.Empty<TemplateGrantInput>(),
            FunctionalProfileNames.OperatorController));

        Assert.True(result.IsFailure);
        Assert.Equal("ACCESS_TEMPLATE_EXISTS", result.Error.Code);
    }

    // ---- product rules (D-1): Admin profile ⇔ admin module only -------------

    [Fact]
    public async Task CreateTemplate_AdminProfile_WithOperationalModules_IsRejected()
    {
        var result = await _service.CreateAsync(new CreateTemplateRequest(
            "tpl-hybrid", "Híbrido",
            new[] { new TemplateGrantInput("boquilhas", Array.Empty<string>()) },
            FunctionalProfileNames.Admin));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_PROFILE_TEMPLATE_MISMATCH", result.Error.Code);
        Assert.DoesNotContain("tpl-hybrid", _repository.Templates.Keys);
    }

    [Fact]
    public async Task CreateTemplate_OperationalProfile_WithAdminModule_IsRejected()
    {
        var result = await _service.CreateAsync(new CreateTemplateRequest(
            "tpl-op-admin", "Operacional com admin",
            new[] { new TemplateGrantInput("admin", Array.Empty<string>()) },
            FunctionalProfileNames.OperatorController));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_PROFILE_TEMPLATE_MISMATCH", result.Error.Code);
        Assert.DoesNotContain("tpl-op-admin", _repository.Templates.Keys);
    }

    [Fact]
    public async Task CreateTemplate_AdminProfile_AdminModuleOnly_IsAccepted()
    {
        var result = await _service.CreateAsync(new CreateTemplateRequest(
            "tpl-admin-only", "Administração",
            new[] { new TemplateGrantInput("admin", Array.Empty<string>()) },
            FunctionalProfileNames.Admin));

        Assert.True(result.IsSuccess);
        Assert.Equal(FunctionalProfileNames.Admin, _repository.TemplateProfiles["tpl-admin-only"]);
    }

    [Fact]
    public async Task CreateTemplate_InvalidFunctionalProfile_IsRejected()
    {
        var result = await _service.CreateAsync(new CreateTemplateRequest(
            "tpl-bad-profile", "Perfil inválido",
            new[] { new TemplateGrantInput("boquilhas", Array.Empty<string>()) },
            "Metrologia"));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_PROFILE_TEMPLATE_MISMATCH", result.Error.Code);
        Assert.DoesNotContain("tpl-bad-profile", _repository.Templates.Keys);
    }

    [Fact]
    public async Task UpdateTemplate_ValidChange_PersistsAndAudits()
    {
        var result = await _service.UpdateAsync(new UpdateTemplateRequest(
            "tpl-1", "Template 1 (revisto)",
            new[] { new TemplateGrantInput("controlo", Array.Empty<string>()) },
            Active: true,
            _repository.Templates["tpl-1"].UpdatedAtUtc,
            FunctionalProfileNames.OperatorController));

        Assert.True(result.IsSuccess);
        Assert.Contains("\"moduleId\":\"controlo\"", _repository.Templates["tpl-1"].ModulesJson);
        Assert.Equal(
            FunctionalProfileNames.OperatorController,
            _repository.TemplateProfiles["tpl-1"]);
        Assert.Equal("update_modules", Assert.Single(_repository.Audits).ActionCode);
    }

    [Fact]
    public async Task UpdateTemplate_DeactivationLockout_IsRejected()
    {
        _repository.LockoutNextWrite = true;

        var result = await _service.UpdateAsync(new UpdateTemplateRequest(
            "tpl-1", "Template 1",
            new[] { new TemplateGrantInput("boquilhas", Array.Empty<string>()) },
            Active: false,
            _repository.Templates["tpl-1"].UpdatedAtUtc,
            FunctionalProfileNames.OperatorController));

        Assert.True(result.IsFailure);
        Assert.Equal("ADMIN_SELF_LOCKOUT", result.Error.Code);
        Assert.True(_repository.Templates["tpl-1"].Active); // unchanged
    }

    [Fact]
    public async Task UpdateTemplate_StaleVersion_IsConcurrencyConflict()
    {
        _repository.ConcurrencyNextWrite = true;

        var result = await _service.UpdateAsync(new UpdateTemplateRequest(
            "tpl-1", "Nome", new[] { new TemplateGrantInput("boquilhas", Array.Empty<string>()) },
            Active: true,
            _repository.Templates["tpl-1"].UpdatedAtUtc,
            FunctionalProfileNames.OperatorController));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.ConcurrencyConflict, result.Error.Category);
    }

    [Fact]
    public async Task Mutations_WithoutCapability_AreDenied()
    {
        _identity.GrantNone();

        var create = await _service.CreateAsync(new CreateTemplateRequest(
            "tpl-x", "X", Array.Empty<TemplateGrantInput>(),
            FunctionalProfileNames.OperatorController));
        var update = await _service.UpdateAsync(new UpdateTemplateRequest(
            "tpl-1", "X", Array.Empty<TemplateGrantInput>(), true,
            _repository.Templates["tpl-1"].UpdatedAtUtc,
            FunctionalProfileNames.OperatorController));

        Assert.Equal(ErrorCategory.Forbidden, create.Error.Category);
        Assert.Equal(ErrorCategory.Forbidden, update.Error.Category);
        Assert.Empty(_repository.Writes);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser? User { get; set; }

        public CurrentUser? Current => User;

        public void GrantAdmin() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            "Administrador", new[] { "admin" },
            new[] { "admin.gerir", "audit.view", "audit.export" });

        public void GrantNone() => User = new CurrentUser(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
            "Operador", new[] { "boquilhas" }, Array.Empty<string>());
    }

    private sealed class FixedClock(DateTimeOffset fixedUtcNow) : IClock
    {
        public DateTimeOffset UtcNow => fixedUtcNow;
    }
}
