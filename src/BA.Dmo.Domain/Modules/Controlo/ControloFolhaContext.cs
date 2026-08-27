namespace BA.Dmo.Domain.Modules.Controlo;

/// <summary>
/// R010 — Immutable production context from which a Folha de Controlo is created:
/// job_on_id + EXACT job_on_revision_id + production/reference/machine + the snapshot
/// components of that revision. A later Job On revision never reinterprets a sheet
/// because the sheet pins this revision and copies the component snapshots.
/// </summary>
public sealed record ControloFolhaProductionContext(
    Guid JobOnId,
    Guid JobOnRevisionId,
    string ProductionCode,
    string Reference,
    string MachineCode,
    IReadOnlyList<ControloFolhaComponent> Components);

/// <summary>A Resumo snapshot component of the Job On revision (MP_CM/MF/BQ/PU/CS).</summary>
public sealed record ControloFolhaComponent(
    string Family,
    Guid? SourceToolId,
    Guid? SourceLotId,
    string? ReferenceSnapshot,
    string? LotSnapshot,
    string? TechnicalNameSnapshot);
