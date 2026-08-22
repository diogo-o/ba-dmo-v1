namespace BA.Dmo.Domain.Modules.Armazem;

/// <summary>
/// U-14 — Armazém module constants (GLM-ARM-01..03). Module <c>armazem</c> has
/// no operational capability in V1: module entry grants the workflows (04_ACC).
/// The physical position code is exactly 4 digits (owner decision).
/// </summary>
public static class ArmazemModuleCatalog
{
    public const string ModuleId = "armazem";

    /// <summary>A physical position code is exactly four digits.</summary>
    public const string PositionCodePattern = @"^\d{4}$";
}