using BA.Dmo.Domain.Modules.Pegamentos;

namespace BA.Dmo.Application.Modules.Pegamentos;

/// <summary>
/// Create a new Pegamento control anchored to an exact Job On revision.
/// No redundant JobOnId — it is derived from the resolved revision context (TD-26).
/// </summary>
public sealed record CreatePegamentoRequest(
    Guid JobOnRevisionId,
    decimal? Tolerance,
    string? Notes);

/// <summary>Update editable fields on an existing control.</summary>
public sealed record UpdatePegamentoRequest(
    Guid ControloId,
    decimal? Tolerance,
    string? Notes);

/// <summary>Add a measurement to a control. ToolNumber is mandatory for NEW measurements.</summary>
public sealed record AddMeasurementRequest(
    Guid ControloId,
    PegamentoComponentKey Component,
    int ToolNumber,
    decimal Costura,
    decimal? ContraCostura);

/// <summary>Close a control.</summary>
public sealed record CloseControlRequest(
    Guid ControloId);

/// <summary>Filter for searching controls. Uses historical reference snapshot text, NOT current master IDs.</summary>
public sealed record ControlFilterRequest(
    string? Reference,
    string? ProductionCode,
    string? MachineCode,
    DateTime? From,
    DateTime? To);

/// <summary>Control detail returned to the UI. All values are historical snapshots.</summary>
public sealed record PegamentoControlDetail(
    Guid ControloId,
    Guid JobOnId,
    Guid JobOnRevisionId,
    string ProductionCode,
    string MachineCode,
    string Reference,
    // CM tool snapshot (historical)
    string? CmReference,
    string? CmLot,
    decimal? CmNominal,
    // BQ tool snapshot (historical)
    string? BqReference,
    string? BqLot,
    decimal? BqNominal,
    // MF tool snapshot (historical)
    string? MfReference,
    string? MfLot,
    decimal? MfNominal,
    decimal Tolerance,
    string Status,
    string? Notas,
    IReadOnlyList<PegamentoMeasurementDetail> Measurements,
    DateTimeOffset CreatedAtUtc,
    string? CreatedBy);

/// <summary>Measurement detail returned to the UI. ToolNumber nullable for pre-N15 historical rows.</summary>
public sealed record PegamentoMeasurementDetail(
    Guid MedicaoId,
    string ComponentKey,
    int? ToolNumber,
    decimal Costura,
    decimal? ContraCostura,
    decimal? Ovalizacao,
    decimal? Media,
    string ToleranceStatus,
    DateTimeOffset CreatedAtUtc);

/// <summary>List item for history/consultation views. All values are historical snapshots.</summary>
public sealed record PegamentoControlItem(
    Guid ControloId,
    Guid JobOnId,
    Guid JobOnRevisionId,
    string ProductionCode,
    string MachineCode,
    string Reference,
    string Status,
    DateTimeOffset CreatedAtUtc);