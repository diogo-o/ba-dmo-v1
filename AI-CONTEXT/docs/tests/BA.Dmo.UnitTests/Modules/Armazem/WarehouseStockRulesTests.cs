using BA.Dmo.Domain.Modules.Armazem;

namespace BA.Dmo.UnitTests.Modules.Armazem;

/// <summary>
/// U-14 — Pure stock/rule tests (GLM-ARM-04/08): occupation 1:1, <c>fora</c>
/// derived from active facts, and 4-digit position-code validation (owner decision).
/// </summary>
public class WarehouseStockRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 18, 0, 0, TimeSpan.Zero);

    private static WarehouseStock Active(Guid toolId) => new()
    {
        WarehouseStockId = Guid.NewGuid(),
        ToolId = toolId,
        OccupiedSinceUtc = Now
    };

    [Fact]
    public void PositionCode_ExactlyFourDigits_IsValid()
    {
        Assert.True(WarehouseLocation.IsValidPositionCode("2421"));
        Assert.True(WarehouseLocation.IsValidPositionCode("0001"));
    }

    [Theory]
    [InlineData("242")]
    [InlineData("24211")]
    [InlineData("24A1")]
    [InlineData("")]
    [InlineData(null)]
    public void PositionCode_NotExactlyFourDigits_IsInvalid(string? code)
    {
        Assert.False(WarehouseLocation.IsValidPositionCode(code));
    }

    [Fact]
    public void IsPositionOccupied_ActiveRow_ReturnsTrue()
    {
        var occupied = WarehouseStockRules.IsPositionOccupied(new[] { Active(Guid.NewGuid()) });
        Assert.True(occupied);
    }

    [Fact]
    public void IsPositionOccupied_NoActiveRows_ReturnsFalse()
    {
        var released = Active(Guid.NewGuid());
        released.ReleasedAtUtc = Now;
        Assert.False(WarehouseStockRules.IsPositionOccupied(new[] { released }));
    }

    [Fact]
    public void IsFora_NoActiveOccupation_ReturnsTrue()
    {
        var released = Active(Guid.NewGuid());
        released.ReleasedAtUtc = Now.AddHours(1);
        Assert.True(WarehouseStockRules.IsFora(new[] { released }));
    }

    [Fact]
    public void IsFora_ActiveOccupation_ReturnsFalse()
    {
        Assert.False(WarehouseStockRules.IsFora(new[] { Active(Guid.NewGuid()) }));
    }

    [Fact]
    public void HasReferenceConflict_TwoDifferentReferences_ReturnsTrue()
    {
        var a = Active(Guid.NewGuid());
        var b = Active(Guid.NewGuid());
        var refs = new Dictionary<Guid, string> { [a.ToolId] = "REF-A", [b.ToolId] = "REF-B" };
        var result = WarehouseStockRules.HasReferenceConflict(
            new[] { a, b }, Guid.NewGuid(), "REF-C", id => refs.GetValueOrDefault(id));
        Assert.True(result);
    }

    [Fact]
    public void HasReferenceConflict_SameReference_ReturnsFalse()
    {
        var a = Active(Guid.NewGuid());
        var b = Active(Guid.NewGuid());
        var refs = new Dictionary<Guid, string> { [a.ToolId] = "REF-S", [b.ToolId] = "REF-S" };
        var result = WarehouseStockRules.HasReferenceConflict(
            new[] { a, b }, Guid.NewGuid(), "REF-S", id => refs.GetValueOrDefault(id));
        Assert.False(result);
    }
}