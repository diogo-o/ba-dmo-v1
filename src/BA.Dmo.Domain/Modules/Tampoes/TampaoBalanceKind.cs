namespace BA.Dmo.Domain.Modules.Tampoes;

/// <summary>
/// U-17 — The two Tampões balances (N10 <c>tampao_saldos</c>; GLM-TP-04,
/// TAMPOES_DESIGN_BRIEF §2). Exactly two states: <c>Enchidos</c> and
/// <c>Por encher</c>. The <c>Maquinado</c> third state does NOT exist without an
/// explicit functional decision.
/// </summary>
public enum TampaoBalanceKind
{
    Enchidos,
    PorEncher
}