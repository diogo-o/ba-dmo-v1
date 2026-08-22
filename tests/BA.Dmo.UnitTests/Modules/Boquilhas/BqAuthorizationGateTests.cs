using BA.Dmo.Application.Modules.Boquilhas;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Boquilhas;

/// <summary>
/// U-19 — Boquilhas authorization gate (GLM-BQ-02): module presence grants FULL
/// access (no capability / no operator-vs-responsável split); missing identity or
/// missing <c>boquilhas</c> module is Forbidden. Server-side guard (corrects the
/// legacy gap that had no module guard).
/// </summary>
public class BqAuthorizationGateTests
{
    [Fact]
    public void Require_WithModule_IsAuthorized()
    {
        var gate = new BqAuthorizationGate(BqCurrentUser.Authorized(), new BqFakeAuthorship());

        var result = gate.Require();

        Assert.True(result.IsSuccess);
        Assert.Equal("bq-actor", result.Value.ActorId);
    }

    [Fact]
    public void Require_WithoutModule_IsForbidden()
    {
        var gate = new BqAuthorizationGate(BqCurrentUser.WithoutModule(), new BqFakeAuthorship());

        var result = gate.Require();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Forbidden, result.Error.Category);
    }
}