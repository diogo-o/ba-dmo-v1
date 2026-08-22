namespace BA.Dmo.Domain.Modules.ReparacaoExterna;

/// <summary>
/// Immutable per-send snapshot of the repairer used for an exit list
/// (N08 <c>repair_exits.repairer_snapshot</c> jsonb; REPARACAO_EXTERNA_DESIGN_BRIEF
/// §10, GLM-RE-05). Changing a repairer association or deactivating a repairer
/// NEVER rewrites the history already captured by this snapshot.
/// </summary>
public sealed record RepairerSnapshot(Guid RepairerId, string Name, bool Active);