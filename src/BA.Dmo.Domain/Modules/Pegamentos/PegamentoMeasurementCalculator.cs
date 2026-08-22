namespace BA.Dmo.Domain.Modules.Pegamentos;

/// <summary>
/// Pure calculation engine for Pegamentos measurements (TD-32).
/// No state, no side effects — deterministic C# only (GLM-PESO-05 rule: no calculation in JS).
/// </summary>
public static class PegamentoMeasurementCalculator
{
    /// <summary>
    /// Ovalização = Costura − Contra costura (null when Contra costura missing).
    /// </summary>
    public static decimal? Ovalizacao(decimal costura, decimal? contraCostura)
    {
        return contraCostura.HasValue ? costura - contraCostura.Value : null;
    }

    /// <summary>
    /// Média = (Costura + Contra costura) / 2; with one value only, média = that value.
    /// </summary>
    public static decimal? Media(decimal costura, decimal? contraCostura)
    {
        return contraCostura.HasValue
            ? (costura + contraCostura.Value) / 2m
            : costura;
    }

    /// <summary>
    /// Tolerance check: nominal ± tolerance corridor.
    /// A point only enters alert when it reaches or exceeds the corresponding limit line.
    /// 
    /// lowerLimit = nominal - tolerance
    /// upperLimit = nominal + tolerance
    /// 
    /// measuredValue > lowerLimit && measuredValue < upperLimit → Ok
    /// measuredValue <= lowerLimit || measuredValue >= upperLimit → Exceeded
    /// 
    /// Example: nominal=52.00, tolerance=0.20
    ///   51.81 → Ok
    ///   52.00 → Ok  
    ///   52.19 → Ok
    ///   51.80 → Exceeded (reaches boundary)
    ///   52.20 → Exceeded (reaches boundary)
    /// </summary>
    public static PegamentoToleranceStatus CheckTolerance(
        decimal measuredValue,
        decimal nominal,
        decimal tolerance)
    {
        var lowerLimit = nominal - tolerance;
        var upperLimit = nominal + tolerance;

        if (measuredValue > lowerLimit && measuredValue < upperLimit)
            return PegamentoToleranceStatus.Ok;

        // Reaching or exceeding the boundary triggers alert
        return PegamentoToleranceStatus.Exceeded;
    }
}