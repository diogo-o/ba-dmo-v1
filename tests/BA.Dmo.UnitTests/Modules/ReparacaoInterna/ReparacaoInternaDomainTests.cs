using BA.Dmo.Domain.Modules.ReparacaoInterna;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.ReparacaoInterna;

/// <summary>
/// R009 — Reparação Interna domain invariants: Create records a repair fact with NO hard
/// block (context is nullable assistance; only structural facts are mandatory); the exact
/// historical production context (job_on_id/job_on_revision_id/production/reference/lot) is
/// persisted so history never depends on current_revision_id; CreateCorrection produces a NEW
/// record (original preserved, GLM-DATA-07) and rejects re-correcting a correction; BQ is a
/// valid third type; repeated numbers (distinct rows) are structurally valid.
/// </summary>
public class ReparacaoInternaDomainTests
{
    private static readonly DateTimeOffset When = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid JobOnId = Guid.NewGuid();
    private static readonly Guid RevisionId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidInputAndContext_Succeeds()
    {
        var result = InternalRepairRecord.Create(
            "B1", JobOnId, RevisionId, "202608", "REF-1", Guid.NewGuid(),
            InternalRepairToolType.CM, "1234", "repan-actor", When, When);
        Assert.True(result.IsSuccess);
        Assert.Equal("B1", result.Value.Line);
        Assert.Equal("1234", result.Value.IndividualNumber);
        Assert.Equal(InternalRepairToolType.CM, result.Value.ToolType);
        Assert.False(result.Value.IsCorrection);
        Assert.Null(result.Value.CorrectionOfId);
        // Historical context snapshot persisted (GAP 2 fix).
        Assert.Equal(JobOnId, result.Value.JobOnId);
        Assert.Equal(RevisionId, result.Value.JobOnRevisionId);
        Assert.Equal("202608", result.Value.ProductionCode);
        Assert.Equal("REF-1", result.Value.Reference);
    }

    [Fact]
    public void Create_WithoutContext_StillSucceeds_NoHardBlock()
    {
        // R009: no production context (all null) must NOT block; only structural facts required.
        var result = InternalRepairRecord.Create(
            "B1", null, null, null, null, null,
            InternalRepairToolType.MF, "5", "repan-actor", When, When);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.JobOnId);
        Assert.Null(result.Value.JobOnRevisionId);
        Assert.Null(result.Value.ProductionCode);
        Assert.Null(result.Value.Reference);
    }

    [Fact]
    public void Create_BQIsAValidThirdType()
    {
        var result = InternalRepairRecord.Create(
            "C1", null, null, null, "REF-1", null,
            InternalRepairToolType.BQ, "77", "repan-actor", When, When);
        Assert.True(result.IsSuccess);
        Assert.Equal(InternalRepairToolType.BQ, result.Value.ToolType);
        Assert.Equal("BQ", InternalRepairToolTypeCodec.ToStorage(result.Value.ToolType));
    }

    [Theory]
    [InlineData("", "1234")]
    [InlineData("B9", "1234")]
    [InlineData("B1", "")]
    public void Create_StructurallyInvalid_IsARejection(string line, string number)
    {
        // Structural/technical only: unknown line or empty number remains rejected.
        var result = InternalRepairRecord.Create(
            line, JobOnId, RevisionId, "202608", "REF-1", null,
            InternalRepairToolType.MF, number, "repan-actor", When, When);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_WithoutOperator_Fails()
    {
        var result = InternalRepairRecord.Create(
            "B1", JobOnId, RevisionId, "202608", "REF-1", null,
            InternalRepairToolType.CM, "1234", "", When, When);
        Assert.True(result.IsFailure);
        Assert.Equal("REPINT_OPERATOR_REQUIRED", result.Error.Code);
    }

    [Fact]
    public void Create_CapturesServerSideOperatorAndTime()
    {
        var on = new DateTimeOffset(2026, 8, 18, 13, 30, 0, TimeSpan.Zero);
        var result = InternalRepairRecord.Create(
            "C2", null, null, null, null, null, InternalRepairToolType.MF, "9999", "repan-actor", on, on);
        Assert.True(result.IsSuccess);
        Assert.Equal("repan-actor", result.Value.OperatorId);
        Assert.Equal(on, result.Value.OccurredAtUtc);
    }

    [Fact]
    public void CreateCorrection_PreservesOriginal_AndAddsNewRow()
    {
        var originalOn = new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);
        var originalResult = InternalRepairRecord.Create(
            "B1", JobOnId, RevisionId, "202608", "REF-1", Guid.NewGuid(),
            InternalRepairToolType.CM, "1234", "repan-actor", originalOn, originalOn);
        var original = originalResult.Value;
        var originalId = original.InternalRepairRecordId;

        var correctedAt = new DateTimeOffset(2026, 8, 18, 15, 0, 0, TimeSpan.Zero);
        var correction = original.CreateCorrection(
            "B1", InternalRepairToolType.CM, "5678", null, null, null, null, null,
            "chefe-actor", "erro de dígito", correctedAt, "{\"line\":\"B1\"}");

        Assert.True(correction.IsSuccess);
        var record = correction.Value;
        Assert.NotEqual(originalId, record.InternalRepairRecordId);
        Assert.Equal(originalId, record.CorrectionOfId);
        Assert.True(record.IsCorrection);
        // Original operator and occurred-at are preserved (read-only).
        Assert.Equal(original.OperatorId, record.OperatorId);
        Assert.Equal(original.OccurredAtUtc, record.OccurredAtUtc);
        // Original untouched; correction carries its own correction author.
        Assert.Equal("1234", original.IndividualNumber);
        Assert.Null(original.CorrectionOfId);
        Assert.Equal("5678", record.IndividualNumber);
        Assert.Equal("erro de dígito", record.CorrectionReason);
        Assert.Equal("chefe-actor", record.CreatedBy);
    }

    [Fact]
    public void CreateCorrection_OfACorrection_Fails()
    {
        var originalResult = InternalRepairRecord.Create(
            "B1", JobOnId, RevisionId, "202608", "REF-1", null,
            InternalRepairToolType.CM, "1234", "repan-actor", When, When);
        var original = originalResult.Value;
        var first = original.CreateCorrection(
            "B1", InternalRepairToolType.CM, "5678", null, null, null, null, null,
            "chefe-actor", null, When, "{}").Value;
        var second = first.CreateCorrection(
            "B1", InternalRepairToolType.CM, "9999", null, null, null, null, null,
            "chefe-actor", null, When, "{}");
        Assert.True(second.IsFailure);
        Assert.Equal("REPINT_CORRECTION_CHAIN", second.Error.Code);
    }

    [Fact]
    public void Rules_EvalCollectibleWhen_NeverBlocks()
    {
        // R009: Single/None/Ambiguous all allow recording (context is assistance).
        Assert.True(InternalRepairRules.EvalCollectibleWhen(InternalRepairResolutionKind.Single).IsSuccess);
        Assert.True(InternalRepairRules.EvalCollectibleWhen(InternalRepairResolutionKind.Ambiguous).IsSuccess);
        Assert.True(InternalRepairRules.EvalCollectibleWhen(InternalRepairResolutionKind.None).IsSuccess);
    }

    [Fact]
    public void Rules_NumberInContextLot_MatchesTypeLot_AndReturnsFalseOnMismatch()
    {
        var cmLot = Guid.NewGuid();
        var context = new InternalRepairContext(
            Guid.NewGuid(), RevisionId, "B1", "202608", "REF-1", "B1",
            new[] { cmLot }, new List<Guid>(), new List<Guid>(),
            When, null);

        Assert.True(InternalRepairRules.NumberInContextLot(context, InternalRepairToolType.CM, cmLot));
        Assert.False(InternalRepairRules.NumberInContextLot(context, InternalRepairToolType.CM, Guid.NewGuid()));
        Assert.False(InternalRepairRules.NumberInContextLot(context, InternalRepairToolType.CM, null));
        // MF is not in the CM lot scope → false (information only, never a block).
        Assert.False(InternalRepairRules.NumberInContextLot(context, InternalRepairToolType.MF, cmLot));
    }
}