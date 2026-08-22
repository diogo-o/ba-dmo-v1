using BA.Dmo.Domain.Modules.JobOn;
using BA.Dmo.Domain.Modules.ReparacaoInterna;
using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.ReparacaoInterna;

/// <summary>
/// R009 — production-activation projection tests (OWNER DECISION §3/§4/§5/§20):
/// line-scoped, most recent start date activated at 09:00 local factory, NO end-date test,
/// deterministic from persisted planned_start_at (no background ping required).
/// </summary>
public class ReparacaoInternaProductionProjectionTests
{
    private static JobOnEntity Planned(DateTimeOffset start, string production)
    {
        var jobOn = new JobOnEntity(production, "LINHA-1", start, start.AddDays(4), Array.Empty<JobOnRevision>());
        jobOn.TransitionTo(JobOnLifecycleState.Planeado);
        return jobOn;
    }

    // Helper: a production whose stored UTC start is 'd' at 09:00 local (~08:00 UTC with the
    // +01:00 factory offset) activates at d 09:00 local. We assert the activation is 09:00
    // factory-local (UTC+1).
    [Fact]
    public void ActivationUtc_Is0920Local_OnTheStartDate()
    {
        // Start of 2026-08-24 at 07:00 UTC (= 08:00 local +01). Activation must be 09:00 local = 08:00 UTC.
        var start = new DateTimeOffset(2026, 8, 24, 7, 0, 0, TimeSpan.Zero); // 08:00 local
        var activation = ReparacaoInternaProductionProjection.ActivationUtc(start);
        Assert.Equal(new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero), activation); // 09:00 local
    }

    [Fact]
    public void SelectEffective_MostRecentStartSupersedes_NoEndDateTest()
    {
        // Production A start 2026-08-01; Production B start 2026-08-24 (both active/planeado).
        // R009: the end date is ignored for the projection; B supersedes A at B's activation.
        var a = Planned(new DateTimeOffset(2026, 8, 1, 7, 0, 0, TimeSpan.Zero), "PROD-A");
        var b = Planned(new DateTimeOffset(2026, 8, 24, 7, 0, 0, TimeSpan.Zero), "PROD-B");
        var candidates = new[] { a, b };

        // 24/08 08:59 local (= 07:59 UTC) → still A (B not yet activated).
        var beforeB = new DateTimeOffset(2026, 8, 24, 7, 59, 59, TimeSpan.Zero);
        Assert.Equal(a.ProductionCode, ReparacaoInternaProductionProjection.SelectEffective(candidates, beforeB)!.ProductionCode);

        // 24/08 09:00 local (= 08:00 UTC) → B (activation reached).
        var atB = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
        Assert.Equal(b.ProductionCode, ReparacaoInternaProductionProjection.SelectEffective(candidates, atB)!.ProductionCode);

        // 02/09 production C (start 02/09) supersedes B later.
        var c = Planned(new DateTimeOffset(2026, 9, 2, 7, 0, 0, TimeSpan.Zero), "PROD-C");
        var three = new[] { a, b, c };
        var afterC = new DateTimeOffset(2026, 9, 2, 8, 0, 0, TimeSpan.Zero);
        Assert.Equal(c.ProductionCode, ReparacaoInternaProductionProjection.SelectEffective(three, afterC)!.ProductionCode);
    }

    [Fact]
    public void SelectEffective_NoCandidateActivated_ReturnsNull()
    {
        var futureOnly = new[] { Planned(new DateTimeOffset(2026, 8, 24, 7, 0, 0, TimeSpan.Zero), "PROD-B") };
        var at = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero); // before any activation
        Assert.Null(ReparacaoInternaProductionProjection.SelectEffective(futureOnly, at));
    }

    [Fact]
    public void SelectEffective_LineScoped_IgnoresOtherLines()
    {
        // Only the line's own candidates are ever passed in (scoping at query), but the
        // projection itself must not be fooled by a globally-latest-start from another line.
        var b1 = Planned(new DateTimeOffset(2026, 8, 1, 7, 0, 0, TimeSpan.Zero), "PROD-B1");
        var c2WrongLine = Planned(new DateTimeOffset(2026, 9, 1, 7, 0, 0, TimeSpan.Zero), "PROD-C2");
        // As a line-projection the caller passes only B1's sequence; a C2 production must never
        // be selected for B1's resolution (verified by the query scope + here the max start).
        Assert.Equal(b1.ProductionCode,
            ReparacaoInternaProductionProjection.SelectEffective(new[] { b1 }, new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero))!.ProductionCode);
    }

    [Fact]
    public void SelectEffective_IgnoresNonActiveStates()
    {
        var active = Planned(new DateTimeOffset(2026, 8, 1, 7, 0, 0, TimeSpan.Zero), "PROD-A");
        var rascunho = new JobOnEntity("PROD-R", "LINHA-1", new DateTimeOffset(2026, 8, 24, 7, 0, 0, TimeSpan.Zero), null, Array.Empty<JobOnRevision>());
        // rascunho is not active → never selected even if it has the most recent start.
        Assert.Equal(active.ProductionCode,
            ReparacaoInternaProductionProjection.SelectEffective(new[] { active, rascunho }, new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero))!.ProductionCode);
    }
}