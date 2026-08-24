using BA.Dmo.Domain.Modules.JobOn;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// U-13 canonical activity lookup <c>Resolve(line, at)</c> tests (TD-27,
/// modules/05 §5.5): single candidate, none, ambiguous overlap, null-end
/// upper bound = next planned start, and exclusion of non-active states.
/// </summary>
public class JobOnActivityResolverTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    private static JobOnEntity Active(DateTimeOffset start, DateTimeOffset? end = null)
    {
        var jobOn = new JobOnEntity("202608", "LINHA-1", start, end, Array.Empty<JobOnRevision>());
        jobOn.TransitionTo(JobOnLifecycleState.Planeado);
        return jobOn;
    }

    private static JobOnEntity Rascunho(DateTimeOffset start, DateTimeOffset? end = null) =>
        new("202608", "LINHA-1", start, end, Array.Empty<JobOnRevision>());

    [Fact]
    public void Resolve_SingleCandidateInsideInterval_ReturnsSingle()
    {
        var candidates = new[] { Active(T0, T0.AddHours(8)) };

        var result = JobOnActivityResolver.Resolve(candidates, T0.AddHours(2));

        Assert.Equal(JobOnResolutionKind.Single, result.Kind);
        Assert.Single(result.Candidates);
    }

    [Fact]
    public void Resolve_NoCandidate_ReturnsNone()
    {
        var candidates = new[] { Active(T0, T0.AddHours(8)) };

        var result = JobOnActivityResolver.Resolve(candidates, T0.AddDays(1));

        Assert.Equal(JobOnResolutionKind.None, result.Kind);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Resolve_EmptyCandidates_ReturnsNone()
    {
        var result = JobOnActivityResolver.Resolve(Array.Empty<JobOnEntity>(), T0);

        Assert.Equal(JobOnResolutionKind.None, result.Kind);
    }

    [Fact]
    public void Resolve_AtBeforeStart_ReturnsNone()
    {
        var candidates = new[] { Active(T0, T0.AddHours(8)) };

        var result = JobOnActivityResolver.Resolve(candidates, T0.AddHours(-1));

        Assert.Equal(JobOnResolutionKind.None, result.Kind);
    }

    [Fact]
    public void Resolve_AtOnEndBoundary_IsExcluded()
    {
        var candidates = new[] { Active(T0, T0.AddHours(8)) };

        var result = JobOnActivityResolver.Resolve(candidates, T0.AddHours(8));

        Assert.Equal(JobOnResolutionKind.None, result.Kind);
    }

    [Fact]
    public void Resolve_TwoOverlappingCandidates_ReturnsAmbiguous()
    {
        var candidates = new[]
        {
            Active(T0, T0.AddHours(8)),
            Active(T0.AddHours(2), T0.AddHours(10))
        };

        var result = JobOnActivityResolver.Resolve(candidates, T0.AddHours(4));

        Assert.Equal(JobOnResolutionKind.Ambiguous, result.Kind);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void Resolve_NullEnd_UsesNextPlannedStartAsUpperBound()
    {
        // First has no end; its upper bound is the second's planned start.
        var candidates = new[]
        {
            Active(T0, end: null),
            Active(T0.AddHours(8), T0.AddHours(16))
        };

        // Just before the next start → first is the single candidate.
        var beforeNext = JobOnActivityResolver.Resolve(candidates, T0.AddHours(7));
        Assert.Equal(JobOnResolutionKind.Single, beforeNext.Kind);
        Assert.Equal(candidates[0].Id, beforeNext.Candidates[0].Id);

        // At/after the next start → only the second covers.
        var atNext = JobOnActivityResolver.Resolve(candidates, T0.AddHours(8));
        Assert.Equal(JobOnResolutionKind.Single, atNext.Kind);
        Assert.Equal(candidates[1].Id, atNext.Candidates[0].Id);
    }

    [Fact]
    public void Resolve_LastCandidateWithNullEnd_IsUnbounded()
    {
        var candidates = new[] { Active(T0, end: null) };

        var result = JobOnActivityResolver.Resolve(candidates, T0.AddDays(30));

        Assert.Equal(JobOnResolutionKind.Single, result.Kind);
    }

    [Fact]
    public void Resolve_NonActiveStates_AreExcluded()
    {
        var candidates = new[]
        {
            Rascunho(T0, T0.AddHours(8)),
            Active(T0.AddHours(2), T0.AddHours(10))
        };

        var result = JobOnActivityResolver.Resolve(candidates, T0.AddHours(4));

        // Only the active candidate is considered.
        Assert.Equal(JobOnResolutionKind.Single, result.Kind);
        Assert.Equal(candidates[1].Id, result.Candidates[0].Id);
    }
}
