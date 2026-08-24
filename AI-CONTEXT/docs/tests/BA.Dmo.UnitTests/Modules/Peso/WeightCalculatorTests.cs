using BA.Dmo.Domain.Modules.Peso;
using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.UnitTests.Modules.Peso;

/// <summary>
/// U-10 — WeightCalculator tests (GLM-PESO-05, TD-25/TD-28; 09_TEST §9 D1–D8).
/// Single C# source of truth: density table 5–35 exact values, rounding rules,
/// volume/glass/calote formulas and delta null-rules. These are deterministic
/// acceptance calculations for confirmed inputs.
/// </summary>
public class WeightCalculatorTests
{
    // ---- density TD-25 table (D5) ----------------------------------------

    [Theory]
    [InlineData(5, 0.99888)]
    [InlineData(6, 0.99885)]
    [InlineData(7, 0.99882)]
    [InlineData(8, 0.99877)]
    [InlineData(9, 0.99871)]
    [InlineData(10, 0.99863)]
    [InlineData(11, 0.99854)]
    [InlineData(12, 0.99844)]
    [InlineData(13, 0.99832)]
    [InlineData(14, 0.99819)]
    [InlineData(15, 0.99805)]
    [InlineData(16, 0.99789)]
    [InlineData(17, 0.99773)]
    [InlineData(18, 0.99765)]
    [InlineData(19, 0.99737)]
    [InlineData(20, 0.99717)]
    [InlineData(21, 0.99696)]
    [InlineData(22, 0.99674)]
    [InlineData(23, 0.99652)]
    [InlineData(24, 0.99628)]
    [InlineData(25, 0.99603)]
    [InlineData(26, 0.99577)]
    [InlineData(27, 0.99551)]
    [InlineData(28, 0.99523)]
    [InlineData(29, 0.99494)]
    [InlineData(30, 0.99485)]
    [InlineData(31, 0.99435)]
    [InlineData(32, 0.99403)]
    [InlineData(33, 0.99371)]
    [InlineData(34, 0.99339)]
    [InlineData(35, 0.99305)]
    public void LookupDensity_IntTemperature5To35_ReturnsExactDensity(int celsius, double expected)
    {
        var result = WeightCalculator.LookupDensity(celsius);

        Assert.True(result.IsSuccess);
        Assert.Equal((decimal)expected, result.Value);
        Assert.Equal(31, WeightCalculator.WaterDensityByCelsius.Count);
    }

    // ---- rounding boundaries (D1–D4) -------------------------------------

    [Fact]
    public void LookupDensity_RoundsToNearestInteger_AwayFromZero()
    {
        // D1: T=20.4 → 0.99717 (nearest int = 20).
        Assert.Equal(0.99717m, WeightCalculator.LookupDensity(20.4m).Value);
        Assert.Equal(0.99888m, WeightCalculator.LookupDensity(4.50m).Value); // D2 → rounds to 5
        Assert.Equal(0.99305m, WeightCalculator.LookupDensity(35.49m).Value); // D4 → rounds to 35
    }

    [Fact]
    public void LookupDensity_BelowMinimum_IsDomainError()
    {
        // D3: T=4.49 → below 5 → error.
        var result = WeightCalculator.LookupDensity(4.49m);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.ValidationError, result.Error.Category);
        Assert.Equal("PESO_TEMPERATURE_OUT_OF_RANGE", result.Error.Code);
    }

    [Fact]
    public void LookupDensity_AboveMaximum_IsDomainError()
    {
        // D4: T=35.50 → above 35 → error.
        var result = WeightCalculator.LookupDensity(35.50m);
        Assert.True(result.IsFailure);
        Assert.Equal("PESO_TEMPERATURE_OUT_OF_RANGE", result.Error.Code);
    }

    // ---- volumes (TD-28, D6–D8) ------------------------------------------

    [Fact]
    public void EstimateGlassWeight_Nnpb_SubtractsPu_AddsNeck()
    {
        // glass = (capacity + volumeNeck − volumePu) × 2.4027
        decimal capacity = 150m, volumeNeck = 5m, volumePu = 2m;
        var expected = (150m + 5m - 2m) * 2.4027m;

        var result = WeightCalculator.EstimateGlassWeight(capacity, volumeNeck, volumePu, PesoModuleCatalog.ConstantNnpb);

        Assert.Equal(WeightCalculator.Round2(expected), result);
    }

    [Fact]
    public void EstimateGlassWeight_Ps_UsesConstant24321()
    {
        var result = WeightCalculator.EstimateGlassWeight(150m, 5m, 2m, PesoModuleCatalog.ConstantPs);
        var expected = (150m + 5m - 2m) * 2.4231m;
        Assert.Equal(WeightCalculator.Round2(expected), result);
    }

    [Fact]
    public void EstimateGlassWeight_NullCapacity_ReturnsNull()
    {
        Assert.Null(WeightCalculator.EstimateGlassWeight(null, 5m, 2m, PesoModuleCatalog.ConstantNnpb));
    }

    [Fact]
    public void VolumeFromWeight_NullOrZeroWeight_ReturnsNull()
    {
        // D7: weight null/zero → volume null.
        Assert.Null(WeightCalculator.VolumeFromWeight(null, 0.99717m));
        Assert.Null(WeightCalculator.VolumeFromWeight(0m, 0.99717m));
        var valid = WeightCalculator.VolumeFromWeight(151.93m, 0.99717m);
        Assert.True(valid.HasValue);
    }

    [Fact]
    public void CaloteVolume_DoesNotInfluenceGlassWeight()
    {
        // D8: calote formula π·s²·(3r−s)/3 is independent of glass formula.
        var s = 4m; var r = 3m;
        var calote = WeightCalculator.CaloteVolume(s, r);

        // A big calote change never changes the glass estimate (same glass inputs).
        var glassBefore = WeightCalculator.EstimateGlassWeight(100m, 0m, 0m, PesoModuleCatalog.ConstantNnpb);
        var glassAfter = WeightCalculator.EstimateGlassWeight(100m, 0m, 0m, PesoModuleCatalog.ConstantNnpb);
        Assert.True(calote > 0);
        Assert.Equal(glassBefore, glassAfter);
    }

    // ---- deltas (GLM-PESO-05/11) -----------------------------------------

    [Fact]
    public void DeltasVs_PreviousNullOrZero_ReturnsNullNull()
    {
        var (d, p) = WeightCalculator.DeltaVs(230.97m, null);
        Assert.Null(d);
        Assert.Null(p);

        (d, p) = WeightCalculator.DeltaVs(230.97m, 0m);
        Assert.Null(d);
        Assert.Null(p);

        (d, p) = WeightCalculator.DeltaVs(null, 230m);
        Assert.Null(d);
        Assert.Null(p);
    }

    [Fact]
    public void DeltasVs_ComputesDeltaAndPercent()
    {
        var (delta, percent) = WeightCalculator.DeltaVs(230.97m, 200m);
        Assert.Equal(30.97m, delta);
        Assert.Equal(WeightCalculator.Round2(30.97m / 200m * 100m), percent);
    }

    [Fact]
    public void GlassAverage_EmptyOrNoValid_ReturnsNull()
    {
        Assert.Null(WeightCalculator.GlassAverage([]));
        Assert.Null(WeightCalculator.GlassAverage([null, null]));
    }

    [Fact]
    public void GlassAverage_AveragesValidReadings()
    {
        var avg = WeightCalculator.GlassAverage([230.97m, 230.53m, null]);
        Assert.Equal(WeightCalculator.Round2((230.97m + 230.53m) / 2m), avg);
    }
}