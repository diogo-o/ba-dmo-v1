using BA.Dmo.Application.Modules.ReparacaoExterna;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.ReparacaoExterna;

/// <summary>
/// U-15 — Reparação Externa authorization gate (GLM-ACC-03/04, GLM-RE-13): module
/// <c>reparacao_externa</c> is required; fails closed when no identity or no module.
/// </summary>
public class ReparacaoExternaAuthorizationGateTests
{
    [Fact]
    public void Require_WithModuleGrant_Succeeds()
    {
        var gate = new ReparacaoExternaAuthorizationGate(
            ReparacaoExternaCurrentUser.Authorized(), new ReparacaoExternaFakeAuthorship());
        var result = gate.Require();
        Assert.True(result.IsSuccess);
        Assert.Equal("repex-actor", result.Value.ActorId);
    }

    [Fact]
    public void Require_WithoutIdentity_FailsClosed()
    {
        var gate = new ReparacaoExternaAuthorizationGate(
            new ReparacaoExternaCurrentUser(null), new ReparacaoExternaFakeAuthorship());
        var result = gate.Require();
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }

    [Fact]
    public void Require_WithoutModuleGrant_FailsClosed()
    {
        // No identity at all ⇒ no module grant ⇒ forbidden.
        var gate = new ReparacaoExternaAuthorizationGate(
            ReparacaoExternaCurrentUser.WithoutModule(), new ReparacaoExternaFakeAuthorship());
        var result = gate.Require();
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }
}