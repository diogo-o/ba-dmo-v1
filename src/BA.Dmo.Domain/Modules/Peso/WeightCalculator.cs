using BA.Dmo.Domain.Shared.Kernel;

namespace BA.Dmo.Domain.Modules.Peso;

/// <summary>
/// SINGLE authoritative weight/volume calculation engine of Peso (GLM-PESO-05,
/// TD-12/TD-25/TD-28). The preview JS must NEVER duplicate these formulas:
/// the water-density table and constants are injected server-side only.
/// All presented decimals are capped at two places.
/// </summary>
public static class WeightCalculator
{
    /// <summary>
    /// Canonical water density table (g/cm³), 5–35 °C — TD-25, recovered from
    /// <c>WeightCalculator.cs</c> and confirmed by <c>WeightCalculatorTests.cs</c>
    /// (GAP-002 RESOLVED). Indexed by integer temperature 5..35.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, decimal> WaterDensityByCelsius = new Dictionary<int, decimal>
    {
        [5] = 0.99888m, [6] = 0.99885m, [7] = 0.99882m, [8] = 0.99877m, [9] = 0.99871m,
        [10] = 0.99863m, [11] = 0.99854m, [12] = 0.99844m, [13] = 0.99832m, [14] = 0.99819m,
        [15] = 0.99805m, [16] = 0.99789m, [17] = 0.99773m, [18] = 0.99765m, [19] = 0.99737m,
        [20] = 0.99717m, [21] = 0.99696m, [22] = 0.99674m, [23] = 0.99652m, [24] = 0.99628m,
        [25] = 0.99603m, [26] = 0.99577m, [27] = 0.99551m, [28] = 0.99523m, [29] = 0.99494m,
        [30] = 0.99485m, [31] = 0.99435m, [32] = 0.99403m, [33] = 0.99371m, [34] = 0.99339m,
        [35] = 0.99305m
    };

    /// <summary>The minimum supported water temperature (inclusive).</summary>
    public const decimal MinTemperatureCelsius = 5m;

    /// <summary>The maximum supported water temperature (inclusive).</summary>
    public const decimal MaxTemperatureCelsius = 35m;

    /// <summary>
    /// Looks up the water density at a decimal Celsius temperature.
    /// Rounds to the nearest integer with <see cref="MidpointRounding.AwayFromZero"/>
    /// (GLM-TST-09 D1–D5; e.g. T=4.50 → 5 °C). The rounded integer must lie in
    /// 5–35 °C, otherwise returns a domain error — no interpolation, no
    /// fallback, no external formula (GLM-PESO-05).
    /// </summary>
    public static Result<decimal, DomainError> LookupDensity(decimal temperatureCelsius)
    {
        var rounded = Math.Round(temperatureCelsius, MidpointRounding.AwayFromZero);
        var key = (int)rounded;
        if (key < (int)MinTemperatureCelsius || key > (int)MaxTemperatureCelsius)
            return Result<decimal, DomainError>.Failure(DomainError.Validation(
                "PESO_TEMPERATURE_OUT_OF_RANGE",
                $"Temperatura fora do intervalo suportado (5–35 °C). Valor: {temperatureCelsius:0.##}."));

        return Result<decimal, DomainError>.Success(WaterDensityByCelsius[key]);
    }

    /// <summary>
    /// Volume of a sample: <c>volume = weight / density</c>. A null or zero
    /// weight yields null (GLM-PESO-05 / TD-28; GLM-TST-09 D7).
    /// </summary>
    public static decimal? VolumeFromWeight(decimal? weightInAir, decimal? density)
    {
        if (weightInAir is null || weightInAir.Value == 0m || density is null || density.Value == 0m)
            return null;
        return Round2(weightInAir.Value / density.Value);
    }

    /// <summary>
    /// Estimated glass weight (TD-28, DG-02 RESOLVED):
    /// <c>glass = (capacity + volumeNeck − volumePu) × constante[tipo]</c>
    /// with volumePu (punção) SUBTRACTED and volumeNeck (marisa) ADDED.
    /// <c>volume_tampao</c> (calote formula) never enters the glass-weight formula.
    /// The constant is the editable peso_settings value (default NNPB/PS).
    /// </summary>
    public static decimal? EstimateGlassWeight(
        decimal? capacity,
        decimal? volumeNeck,
        decimal? volumePu,
        decimal constant)
    {
        if (capacity is null)
            return null;
        var neck = volumeNeck ?? 0m;
        var pu = volumePu ?? 0m;
        return Round2((capacity.Value + neck - pu) * constant);
    }

    /// <summary>
    /// Cap volume (calote), <c>π·s²·(3r−s)/3</c>. Explicitly NOT part of the
    /// glass-weight formula (TD-28; GLM-TST-09 D8); used only for the tampão
    /// volume presentation.
    /// </summary>
    public static decimal CaloteVolume(decimal s, decimal r)
    {
        var pie = 3.14159265358979323846m;
        return Round2(pie * s * s * (3m * r - s) / 3m);
    }

    /// <summary>
    /// Glass average = the average <c>diferenca_peso</c> of the readings
    /// (GLM-PESO-05). Returns null when no reading is present.
    /// </summary>
    public static decimal? GlassAverage(IReadOnlyList<decimal?> glassWeights)
    {
        if (glassWeights is null || glassWeights.Count == 0)
            return null;
        var valid = glassWeights.Where(w => w.HasValue).ToList();
        if (valid.Count == 0)
            return null;
        return Round2(valid.Sum(w => w!.Value) / valid.Count);
    }

    /// <summary>
    /// Delta vs previous/nominal as <c>[delta, pct]</c>. Both are null/null when
    /// the comparison basis is null or zero (GLM-PESO-05/GLM-PESO-11).
    /// </summary>
    public static (decimal? Delta, decimal? Percent) DeltaVs(
        decimal? current,
        decimal? basis)
    {
        if (current is null || basis is null || basis.Value == 0m)
            return (null, null);
        var delta = Round2(current.Value - basis.Value);
        var percent = Round2(delta / basis.Value * 100m);
        return (delta, percent);
    }

    /// <summary>Rounds a decimal to at most two places (GLM-PESO-05 §5).</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}