namespace BA.Dmo.Domain.Modules.ReparacaoExterna;

/// <summary>
/// U-15 — Reparação Externa module constants (GLM-RE-01..02, UD-13). Module
/// <c>reparacao_externa</c> is a single assignable module with six internal tabs
/// (Boquilhas / Contra moldes / Moldes finais / Envios / Histórico / Definições);
/// the tabs are NOT separately assignable.
/// V1 functional scope is CM + MF external repair batches; BQ functional repair
/// is deliberately deferred to U-19 (owner decision A) — the Boquilhas tab remains
/// present in the shell UI but holds no fake BQ behavior.
/// </summary>
public static class ReparacaoExternaModuleCatalog
{
    public const string ModuleId = "reparacao_externa";

    /// <summary>Canonical repair types of the external cycle (TD-22).</summary>
    public static readonly IReadOnlyList<string> RepairTypes = new[] { "BQ", "CM", "MF" };
}