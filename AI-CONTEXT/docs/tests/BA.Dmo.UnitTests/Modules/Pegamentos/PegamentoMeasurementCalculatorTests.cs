using BA.Dmo.Domain.Modules.Pegamentos;

namespace BA.Dmo.UnitTests.Modules.Pegamentos;

/// <summary>
/// U-11 — Pegamentos measurement calculation engine tests (TD-32 / GLM-PEG-05).
/// Ovalização = Costura − Contra costura; Média = (c + n)/2 (single value = that
/// value); tolerance corridor ±0.20 with boundary = Exceeded.
/// </summary>
public class PegamentoMeasurementCalculatorTests
{
    // ---- Ovalização ----

    [Fact]
    public void Ovalizacao_BothValues_Difference()
    {
        Assert.Equal(0.30m, PegamentoMeasurementCalculator.Ovalizacao(52.30m, 52.00m));
        Assert.Equal(-0.15m, PegamentoMeasurementCalculator.Ovalizacao(38.35m, 38.50m));
    }

    [Fact]
    public void Ovalizacao_MissingContraCostura_IsNull()
    {
        Assert.Null(PegamentoMeasurementCalculator.Ovalizacao(52.00m, null));
    }

    // ---- Média ----

    [Fact]
    public void Media_BothValues_Average()
    {
        Assert.Equal(52.15m, PegamentoMeasurementCalculator.Media(52.30m, 52.00m));
    }

    [Fact]
    public void Media_SingleValue_IsThatValue()
    {
        Assert.Equal(52.00m, PegamentoMeasurementCalculator.Media(52.00m, null));
    }

    // ---- Tolerance check (TD-32: boundary = Exceeded) ----

    private const decimal Nominal = 52.00m;
    private const decimal Tolerance = 0.20m;

    [Fact]
    public void Tolerance_InsideCorridor_IsOk()
    {
        Assert.Equal(PegamentoToleranceStatus.Ok, PegamentoMeasurementCalculator.CheckTolerance(51.81m, Nominal, Tolerance));
        Assert.Equal(PegamentoToleranceStatus.Ok, PegamentoMeasurementCalculator.CheckTolerance(52.00m, Nominal, Tolerance));
        Assert.Equal(PegamentoToleranceStatus.Ok, PegamentoMeasurementCalculator.CheckTolerance(52.19m, Nominal, Tolerance));
    }

    [Fact]
    public void Tolerance_OnBoundary_IsExceeded()
    {
        Assert.Equal(PegamentoToleranceStatus.Exceeded, PegamentoMeasurementCalculator.CheckTolerance(51.80m, Nominal, Tolerance));
        Assert.Equal(PegamentoToleranceStatus.Exceeded, PegamentoMeasurementCalculator.CheckTolerance(52.20m, Nominal, Tolerance));
    }

    [Fact]
    public void Tolerance_BeyondCorridor_IsExceeded()
    {
        Assert.Equal(PegamentoToleranceStatus.Exceeded, PegamentoMeasurementCalculator.CheckTolerance(51.00m, Nominal, Tolerance));
        Assert.Equal(PegamentoToleranceStatus.Exceeded, PegamentoMeasurementCalculator.CheckTolerance(53.00m, Nominal, Tolerance));
    }

    // ---- N39: one-sided measurement (contra_costura absent) ----
    // Absence never becomes a validation blocker: Média falls back to the
    // single value and the tolerance corridor applies to it.

    [Fact]
    public void Tolerance_OneSidedMeasurement_UsesSingleValueFallback()
    {
        Assert.Equal(PegamentoToleranceStatus.Ok,
            PegamentoMeasurementCalculator.CheckTolerance(
                PegamentoMeasurementCalculator.Media(52.10m, null)!.Value, Nominal, Tolerance));
        Assert.Equal(PegamentoToleranceStatus.Exceeded,
            PegamentoMeasurementCalculator.CheckTolerance(
                PegamentoMeasurementCalculator.Media(52.30m, null)!.Value, Nominal, Tolerance));
    }
}