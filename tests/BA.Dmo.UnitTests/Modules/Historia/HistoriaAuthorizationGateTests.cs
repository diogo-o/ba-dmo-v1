using BA.Dmo.Application.Modules.Historia;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Historia;

/// <summary>
/// U-18 — História authorization gate (modules/11 GLM-HIST-02, TD-24).
/// Verify: entry requires the <c>historia</c> module; the visible origin scope is
/// exactly the intersection of the identity's granted modules with the origin
/// module set; admin events are included only with audit.view; empty scope is
/// still authorized (the view shows an empty state) — only a missing
/// <c>historia</c> grant is Forbidden.
/// </summary>
public class HistoriaAuthorizationGateTests
{
    [Fact]
    public void Require_WithHistoriaAndOrigins_ResolvesGrantedOriginsOnly()
    {
        var gate = new HistoriaAuthorizationGate(
            HistoriaCurrentUser.WithModules("peso", "tampoes", "jobon"));

        var result = gate.Require();

        Assert.True(result.IsSuccess);
        // TD-24: only the granted origin modules are visible (boquilhas/pegamentos
        // etc. are NOT granted and must not appear).
        Assert.Equal(new[] { "jobon", "peso", "tampoes" },
            result.Value.VisibleOriginModuleIds);
        Assert.False(result.Value.IncludeAdminWithAuditView);
    }

    [Fact]
    public void Require_WithAuditView_IncludesAdmin()
    {
        var gate = new HistoriaAuthorizationGate(
            HistoriaCurrentUser.WithModulesAndCapabilities(
                new[] { "peso" }, new[] { "audit.view" }));

        var result = gate.Require();

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "peso" }, result.Value.VisibleOriginModuleIds);
        Assert.True(result.Value.IncludeAdminWithAuditView);
    }

    [Fact]
    public void Require_WithNoOriginModules_IsAuthorizedWithEmptyScope()
    {
        var gate = new HistoriaAuthorizationGate(
            HistoriaCurrentUser.WithModules());

        var result = gate.Require();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.VisibleOriginModuleIds);
    }

    [Fact]
    public void Require_WithoutHistoriaModule_IsForbidden()
    {
        var gate = new HistoriaAuthorizationGate(HistoriaCurrentUser.WithoutHistoriaModule());

        var result = gate.Require();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    [Fact]
    public void Require_WithNoIdentity_IsForbidden()
    {
        var gate = new HistoriaAuthorizationGate(HistoriaCurrentUser.None());

        var result = gate.Require();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }
}

/// <summary>Fake current-user accessor controlling modules/capabilities.</summary>
public sealed class HistoriaCurrentUser
{
    public static ICurrentUserAccessor WithModules(params string[] modules) =>
        From(modules, Array.Empty<string>(), hasHistoria: true);

    public static ICurrentUserAccessor WithModulesAndCapabilities(
        string[] modules, string[] capabilities) =>
        From(modules, capabilities, hasHistoria: true);

    public static ICurrentUserAccessor WithoutHistoriaModule() =>
        From(new[] { "peso", "tampoes" }, Array.Empty<string>(), hasHistoria: false);

    public static ICurrentUserAccessor None() => new FakeUser(null);

    private static ICurrentUserAccessor From(
        string[] modules, string[] capabilities, bool hasHistoria)
    {
        var all = new List<string>(modules);
        if (hasHistoria)
            all.Add(HistoriaModuleCatalog.ModuleId);
        return new FakeUser(new CurrentUser(
            Guid.NewGuid(), "Operador História", all, capabilities));
    }

    private sealed class FakeUser(CurrentUser? user) : ICurrentUserAccessor
    {
        public CurrentUser? Current => user;
    }
}