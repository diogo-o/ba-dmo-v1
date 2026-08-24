using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.UnitTests.Shared.Access;

/// <summary>
/// U-01 kernel unit tests: current user projection and accessor contract
/// (Plan-V3 GLM-ARCH-03; server-side grants only — GLM-ARCH-14/18).
/// </summary>
public class CurrentUserTests
{
    [Fact]
    public void CurrentUser_NormalizesGrants_AndAnswersQueries()
    {
        var user = new CurrentUser(
            Guid.NewGuid(),
            "  Operador A  ",
            [" jobon ", "peso", "", null!],
            ["jobon.view", " peso.aprovar "]);

        Assert.Equal("Operador A", user.DisplayName);
        Assert.True(user.HasModule("jobon"));
        Assert.True(user.HasModule("peso"));
        Assert.False(user.HasModule("admin"));
        Assert.True(user.HasCapability("jobon.view"));
        Assert.True(user.HasCapability("peso.aprovar"));
        Assert.False(user.HasCapability("admin.gerir"));
        Assert.False(user.HasModule(null!));
        Assert.False(user.HasCapability("  "));
    }

    [Fact]
    public void CurrentUser_EmptyIdOrBlankName_AreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new CurrentUser(Guid.Empty, "Nome", [], []));
        Assert.Throws<ArgumentException>(() =>
            new CurrentUser(Guid.NewGuid(), "   ", [], []));
    }

    [Fact]
    public void Accessor_ReturnsNull_WhenNoUserIsResolved()
    {
        ICurrentUserAccessor accessor = new NullCurrentUserAccessor();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Accessor_ReturnsResolvedUser_WhenPresent()
    {
        var user = new CurrentUser(Guid.NewGuid(), "Responsável", ["peso"], ["peso.aprovar"]);
        ICurrentUserAccessor accessor = new FixedCurrentUserAccessor(user);

        Assert.Same(user, accessor.Current);
    }

    /// <summary>Test double confined to tests/* (allowed by GLM-ARCH-18).</summary>
    private sealed class NullCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser? Current => null;
    }

    /// <summary>Test double confined to tests/* (allowed by GLM-ARCH-18).</summary>
    private sealed class FixedCurrentUserAccessor(CurrentUser user) : ICurrentUserAccessor
    {
        public CurrentUser? Current => user;
    }
}
