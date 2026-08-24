using BA.Dmo.Domain.Modules.JobOn;

using JobOnEntity = BA.Dmo.Domain.Modules.JobOn.JobOn;

namespace BA.Dmo.UnitTests.Modules.JobOn;

/// <summary>
/// U-13 Job On domain tests (modules/05 §4/§5.4/§6.2, TD-18/TD-27).
/// Covers lifecycle transitions, cancellation, duplication (origin immutable,
/// everything copied, new dates) and revision immutability.
/// </summary>
public class JobOnDomainTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    private static JobOnEntity NewJobOn(DateTimeOffset? start = null, DateTimeOffset? end = null) =>
        new("202608", "LINHA-1", start ?? Start, end, Array.Empty<JobOnRevision>());

    // ---- lifecycle transitions (TD-27) -----------------------------------

    [Fact]
    public void Transition_RascunhoToPlaneado_IsValid()
    {
        var jobOn = NewJobOn();

        jobOn.TransitionTo(JobOnLifecycleState.Planeado);

        Assert.Equal(JobOnLifecycleState.Planeado, jobOn.LifecycleState);
        Assert.True(jobOn.IsActive);
    }

    [Fact]
    public void Transition_PlaneadoToEmFabrico_IsValid()
    {
        var jobOn = NewJobOn();
        jobOn.TransitionTo(JobOnLifecycleState.Planeado);

        jobOn.TransitionTo(JobOnLifecycleState.EmFabrico);

        Assert.Equal(JobOnLifecycleState.EmFabrico, jobOn.LifecycleState);
        Assert.True(jobOn.IsActive);
    }

    [Fact]
    public void Transition_EmFabricoToFechado_IsValid()
    {
        var jobOn = NewJobOn();
        jobOn.TransitionTo(JobOnLifecycleState.Planeado);
        jobOn.TransitionTo(JobOnLifecycleState.EmFabrico);

        jobOn.TransitionTo(JobOnLifecycleState.Fechado);

        Assert.Equal(JobOnLifecycleState.Fechado, jobOn.LifecycleState);
        Assert.False(jobOn.IsActive);
    }

    [Theory]
    [InlineData(JobOnLifecycleState.EmFabrico)]
    [InlineData(JobOnLifecycleState.Fechado)]
    public void Transition_FromRascunhoToInvalidState_Throws(JobOnLifecycleState target)
    {
        var jobOn = NewJobOn();

        Assert.Throws<Exception>(() => jobOn.TransitionTo(target));
        Assert.Equal(JobOnLifecycleState.Rascunho, jobOn.LifecycleState);
    }

    [Fact]
    public void Transition_FromPlaneadoToFechado_Throws()
    {
        var jobOn = NewJobOn();
        jobOn.TransitionTo(JobOnLifecycleState.Planeado);

        Assert.Throws<Exception>(() => jobOn.TransitionTo(JobOnLifecycleState.Fechado));
    }

    [Fact]
    public void Close_OnlyFromEmFabrico_RecordsTimestamp()
    {
        var jobOn = NewJobOn();
        jobOn.TransitionTo(JobOnLifecycleState.Planeado);
        jobOn.TransitionTo(JobOnLifecycleState.EmFabrico);
        var now = new DateTime(2026, 8, 17, 18, 0, 0, DateTimeKind.Utc);

        jobOn.Close(now);

        Assert.Equal(JobOnLifecycleState.Fechado, jobOn.LifecycleState);
        Assert.Equal(now, jobOn.ClosedAtUtc);
    }

    [Fact]
    public void Close_FromRascunho_Throws()
    {
        var jobOn = NewJobOn();

        Assert.Throws<Exception>(() => jobOn.Close(DateTime.UtcNow));
    }

    [Fact]
    public void Cancel_FromPlaneado_RecordsReasonAndActor()
    {
        var jobOn = NewJobOn();
        jobOn.TransitionTo(JobOnLifecycleState.Planeado);
        var now = new DateTime(2026, 8, 17, 18, 0, 0, DateTimeKind.Utc);

        jobOn.Cancel("Motivo de cancelamento", "actor-1", now);

        Assert.Equal(JobOnLifecycleState.Cancelado, jobOn.LifecycleState);
        Assert.Equal("actor-1", jobOn.CancelledBy);
        Assert.Equal("Motivo de cancelamento", jobOn.CancelReason);
        Assert.Equal(now, jobOn.CancelledAtUtc);
        Assert.False(jobOn.IsActive);
    }

    [Fact]
    public void Cancel_FromEmFabrico_Throws()
    {
        var jobOn = NewJobOn();
        jobOn.TransitionTo(JobOnLifecycleState.Planeado);
        jobOn.TransitionTo(JobOnLifecycleState.EmFabrico);

        Assert.Throws<Exception>(() => jobOn.Cancel("motivo", "actor-1", DateTime.UtcNow));
    }

    // ---- duplication (modules/05 §6.2) -----------------------------------

    [Fact]
    public void DuplicateFrom_CopiesSnapshot_NewDates_AndPinsOrigin()
    {
        var source = NewJobOn(Start, Start.AddHours(8));
        source.TransitionTo(JobOnLifecycleState.Planeado);
        var newStart = new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.Zero);

        var duplicated = JobOnEntity.DuplicateFrom(
            source, "202620", "LINHA-1", newStart, newStart.AddHours(8),
            Array.Empty<JobOnRevision>());

        Assert.Equal("202620", duplicated.ProductionCode);
        Assert.Equal(newStart, duplicated.PlannedStartAt);
        Assert.Equal(source.Id, duplicated.CopiedFromJobOnId);
        // Origin is immutable: source keeps its own production/dates.
        Assert.Equal("202608", source.ProductionCode);
        Assert.Equal(Start, source.PlannedStartAt);
    }

    [Fact]
    public void DuplicateFrom_DoesNotMutateSource()
    {
        var source = NewJobOn(Start, Start.AddHours(8));

        var duplicated = JobOnEntity.DuplicateFrom(
            source, "202620", "LINHA-1", Start.AddDays(1), null,
            Array.Empty<JobOnRevision>());

        Assert.NotSame(source, duplicated);
        Assert.Equal(JobOnLifecycleState.Rascunho, source.LifecycleState);
        Assert.Equal(JobOnLifecycleState.Rascunho, duplicated.LifecycleState);
    }

    // ---- revision immutability (TD-18) -----------------------------------

    [Fact]
    public void CloneWithChanges_CreatesNewRevision_DoesNotMutateOriginal()
    {
        var original = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = Guid.NewGuid(),
            RevisionNumber = 1,
            GeneralNotes = "Notas originais",
            SavedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var cloned = original.CloneWithChanges(generalNotes: "Notas novas", changeReason: "Correção");

        Assert.NotEqual(original.JobOnRevisionId, cloned.JobOnRevisionId);
        Assert.Equal(2, cloned.RevisionNumber);
        Assert.Equal("Notas novas", cloned.GeneralNotes);
        Assert.Equal("Correção", cloned.ChangeReason);
        // Original untouched.
        Assert.Equal(1, original.RevisionNumber);
        Assert.Equal("Notas originais", original.GeneralNotes);
        Assert.Null(original.ChangeReason);
    }

    [Fact]
    public void SaveRevision_SetsCurrentRevisionId()
    {
        var jobOn = NewJobOn();
        var revision = new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = jobOn.Id,
            RevisionNumber = 1
        };

        jobOn.SaveRevision(revision);

        Assert.Equal(revision.JobOnRevisionId, jobOn.CurrentRevisionId);
    }

    // ---- lifecycle codec (N05 status column) -----------------------------

    [Theory]
    [InlineData("rascunho", JobOnLifecycleState.Rascunho)]
    [InlineData("planeado", JobOnLifecycleState.Planeado)]
    [InlineData("em_fabrico", JobOnLifecycleState.EmFabrico)]
    [InlineData("fechado", JobOnLifecycleState.Fechado)]
    [InlineData("cancelado", JobOnLifecycleState.Cancelado)]
    public void Codec_Parse_ReadsN05Status(string storage, JobOnLifecycleState expected)
    {
        Assert.Equal(expected, JobOnLifecycleStateCodec.Parse(storage));
    }

    [Theory]
    [InlineData(JobOnLifecycleState.Rascunho, "rascunho")]
    [InlineData(JobOnLifecycleState.Planeado, "planeado")]
    [InlineData(JobOnLifecycleState.EmFabrico, "em_fabrico")]
    [InlineData(JobOnLifecycleState.Fechado, "fechado")]
    [InlineData(JobOnLifecycleState.Cancelado, "cancelado")]
    public void Codec_ToStorage_WritesN05Status(JobOnLifecycleState state, string expected)
    {
        Assert.Equal(expected, JobOnLifecycleStateCodec.ToStorage(state));
    }

    [Fact]
    public void Codec_Parse_UnknownStatus_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => JobOnLifecycleStateCodec.Parse("desconhecido"));
    }
}
