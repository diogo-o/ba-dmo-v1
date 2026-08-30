using BA.Dmo.Application.Modules.JobOn;
using BA.Dmo.Application.Shared.Access;
using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Shared.Access;
using BA.Dmo.Domain.Shared.Kernel;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// R011 — "Current open Job On" context (Owner §14/§15). Tests that opening a Job On
/// preserves the EXACT production identity for the authenticated user (user-scoped, not the
/// globally-newest, not a clock derivation), and that a user without edit permission can
/// still record/read it (view planning is enough), while a missing Job On is refused.
/// </summary>
public class JobOnUserContextTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeJobOnRepository _repository = new();
    private readonly FakeJobOnUserContextRepository _userContext = new();
    private readonly LocalFakeCurrentUserAccessor _identity = new();
    private readonly JobOnService _service;

    public JobOnUserContextTests()
    {
        var gate = new JobOnAuthorizationGate(_identity);
        _service = new JobOnService(
            gate, _repository, _userContext, new LocalFixedClock(
                new DateTimeOffset(2026, 8, 17, 18, 0, 0, TimeSpan.Zero)),
            new FakeFerramentasToolLookup());
    }

    private JobOnEntity CreateJobOn(string production = "202601", string machine = "B1")
    {
        var jobOn = new JobOnEntity(production, machine, Start, null, Array.Empty<JobOnRevision>());
        jobOn.SetId(Guid.NewGuid());
        _repository.JobOns[jobOn.Id] = jobOn;
        return jobOn;
    }

    [Fact]
    public async Task SetCurrentOpen_PreservesExactProductionIdentity()
    {
        _identity.GrantJobOn();
        var jobOn = CreateJobOn("202601", "B1");
        var jobOn2 = CreateJobOn("202602", "B2");

        var set = await _service.SetCurrentOpenAsync(jobOn.Id);
        Assert.True(set.IsSuccess);
        Assert.Equal(LocalFakeCurrentUserAccessor.ExecutorId.ToString(), _userContext.LastActorId);
        Assert.Equal(jobOn.Id, _userContext.Current!.JobOnId);
        Assert.Equal("202601", _userContext.Current.ProductionCode);
        Assert.Equal("B1", _userContext.Current.MachineCode);

        // Opening a later production overwrites the user's current context.
        await _service.SetCurrentOpenAsync(jobOn2.Id);
        Assert.Equal(jobOn2.Id, _userContext.Current.JobOnId);
    }

    [Fact]
    public async Task GetCurrentOpen_Returns_TheUserExplicitlyOpenedJobOn()
    {
        _identity.GrantJobOn();
        var jobOn = CreateJobOn("202601", "C1");
        await _service.SetCurrentOpenAsync(jobOn.Id);

        var read = await _service.GetCurrentOpenAsync();
        Assert.True(read.IsSuccess);
        Assert.Equal(jobOn.Id, read.Value.JobOnId);
        Assert.Equal("202601", read.Value.ProductionCode);
    }

    [Fact]
    public async Task GetCurrentOpen_WithoutContext_IsNotFound_NotFabricated()
    {
        _identity.GrantJobOn();
        var read = await _service.GetCurrentOpenAsync();
        Assert.True(read.IsFailure);
        Assert.Equal("JOBON_CURRENT_NOT_FOUND", read.Error.Code);
    }

    [Fact]
    public async Task SetCurrentOpen_ForMissingJobOn_IsNotFound()
    {
        _identity.GrantJobOn();
        var set = await _service.SetCurrentOpenAsync(Guid.NewGuid());
        Assert.True(set.IsFailure);
        Assert.Equal("JOBON_NOT_FOUND", set.Error.Code);
        Assert.Null(_userContext.Current);
    }

    [Fact]
    public async Task AUser_WithoutEdit_CanStillOpenAndReadPlanningContext()
    {
        // jobon.view is granted but jobon.edit is NOT — viewing/opening planning is enough.
        _identity.GrantViewOnly();
        var jobOn = CreateJobOn("202603", "B3");

        var set = await _service.SetCurrentOpenAsync(jobOn.Id);
        Assert.True(set.IsSuccess);

        var read = await _service.GetCurrentOpenAsync();
        Assert.True(read.IsSuccess);
        Assert.Equal(jobOn.Id, read.Value.JobOnId);
    }

    [Fact]
    public void CanonicalSixLines_AreSupported_AndDistinct()
    {
        var lines = JobOnLineCatalog.Lines;
        Assert.Equal(6, lines.Count);
        Assert.Equal(6, lines.Distinct().Count());
        Assert.Equal(new[] { "B1", "B2", "B3", "C1", "C2", "C3" }, lines);
    }
}

/// <summary>Local clock for deterministic timestamps.</summary>
internal sealed class LocalFixedClock(DateTimeOffset fixedUtcNow) : IClock
{
    public DateTimeOffset UtcNow => fixedUtcNow;
}

/// <summary>R011 — canonical six-line constant for unit-level reference (mirrors platform).</summary>
internal static class JobOnLineCatalog
{
    public static readonly IReadOnlyList<string> Lines = new[] { "B1", "B2", "B3", "C1", "C2", "C3" };
}

/// <summary>Local identity accessor for these tests (module jobon, capability-driven).</summary>
internal sealed class LocalFakeCurrentUserAccessor : ICurrentUserAccessor
{
    public static readonly Guid ExecutorId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    public CurrentUser? User { get; private set; }
    public CurrentUser? Current => User;

    public void GrantJobOn() => User = new CurrentUser(
        ExecutorId,
        "Responsável Técnico",
        new[] { "jobon" },
        new[] { "jobon.view", "jobon.edit", "jobon.configure", "jobon.confirmar" });

    public void GrantViewOnly() => User = new CurrentUser(
        ExecutorId,
        "Operador",
        new[] { "jobon" },
        new[] { "jobon.view" });
}