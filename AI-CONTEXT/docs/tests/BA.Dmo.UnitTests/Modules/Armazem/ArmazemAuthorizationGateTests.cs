using BA.Dmo.Application.Modules.Armazem;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Armazem;

/// <summary>
/// U-14 — Armazém authorization gate (GLM-ACC-04, modules/07 §2): module entry
/// is required and the gate fails closed when no identity resolves.
/// </summary>
public class ArmazemAuthorizationGateTests
{
    [Fact]
    public void Require_WithModule_SucceedsAndReturnsCanonicalActor()
    {
        var gate = new ArmazemAuthorizationGate(ArmazemCurrentUser.Authorized(), new ArmazemFakeAuthorship("arm-actor"));
        var result = gate.Require();
        Assert.True(result.IsSuccess);
        Assert.Equal("arm-actor", result.Value.ActorId);
    }

    [Fact]
    public void Require_WithoutModule_IsForbidden()
    {
        var gate = new ArmazemAuthorizationGate(ArmazemCurrentUser.WithoutModule(), new ArmazemFakeAuthorship("arm-actor"));
        var result = gate.Require();
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }
}