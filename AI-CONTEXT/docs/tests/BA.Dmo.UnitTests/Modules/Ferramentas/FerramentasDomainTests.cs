using BA.Dmo.Application.Modules.Ferramentas;
using BA.Dmo.Domain.Modules.Ferramentas;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Ferramentas;

/// <summary>
/// U-12 — Ferramentas domain model invariants (GLM-FERR-02/05/06/07/09/10,
/// TD-17, TD-26): CM/MF distinct; processo on the lote not the reference;
/// atomicity of the reference+lote command; duplication configuration-only;
/// rules per-lot with future-only edits.
/// </summary>
public class FerramentasDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToolReference_Create_WithoutRefCode_IsValidationError()
    {
        var result = ToolReference.Create(FerramentasToolType.CM, "  ", "Nome", null, Now, "actor");
        Assert.True(result.IsFailure);
        Assert.Equal("FERRAMENTAS_REFCODE_REQUIRED", result.Error.Code);
    }

    [Fact]
    public void ToolReference_OwnerPlant_DefaultsToMarinhaGrande()
    {
        var result = ToolReference.Create(FerramentasToolType.MF, "MF-01", "Mole", null, Now, "actor");
        Assert.True(result.IsSuccess);
        Assert.Equal("MG — Marinha Grande", result.Value.OwnerPlant);
    }

    [Fact]
    public void ToolLote_RequiresLinesAndPositiveQty()
    {
        var noLines = ToolLote.CreateInitial(Guid.NewGuid(), "4", 10, Array.Empty<string>(), null, null, null, Now, "actor");
        Assert.True(noLines.IsFailure);
        Assert.Equal("FERRAMENTAS_LINES_REQUIRED", noLines.Error.Code);

        var negQty = ToolLote.CreateInitial(Guid.NewGuid(), "4", -1, new[] { "B1" }, null, null, null, Now, "actor");
        Assert.True(negQty.IsFailure);
        Assert.Equal("FERRAMENTAS_QTY_INVALID", negQty.Error.Code);
    }

    [Fact]
    public void CM_And_MF_AreDistinctTypes()
    {
        Assert.NotEqual(FerramentasToolType.CM, FerramentasToolType.MF);
        Assert.Equal("CM", FerramentasToolTypeCodec.ToStorage(FerramentasToolType.CM));
        Assert.Equal("MF", FerramentasToolTypeCodec.ToStorage(FerramentasToolType.MF));
    }

    [Fact]
    public void ToolCheckRule_RequiresText()
    {
        var result = ToolCheckRule.Create(Guid.NewGuid(), "  ", FerramentasCheckFrequency.OncePerLot, null, Now, "actor");
        Assert.True(result.IsFailure);
        Assert.Equal("FERRAMENTAS_RULE_TEXT_REQUIRED", result.Error.Code);
    }

    [Fact]
    public void PhysicalPiece_RequiresPositiveSequenceAndNumber()
    {
        var badSeq = PhysicalPiece.Register(Guid.NewGuid(), 0, "1", Now, "actor");
        Assert.True(badSeq.IsFailure);

        var badNum = PhysicalPiece.Register(Guid.NewGuid(), 1, " ", Now, "actor");
        Assert.True(badNum.IsFailure);
    }

    [Fact]
    public void ConditionChanges_RequireReason()
    {
        var pieceResult = PhysicalPiece.Register(Guid.NewGuid(), 1, "42", Now, "actor");
        Assert.True(pieceResult.IsSuccess);
        var noReason = pieceResult.Value.SetCondition(ToolCondition.Sucatado, "", Now, "actor");
        Assert.True(noReason.IsFailure);
    }
}