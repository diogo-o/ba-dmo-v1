using BA.Dmo.Domain.Modules.Armazem;

namespace BA.Dmo.Application.Modules.Armazem;

/// <summary>Register an entry (Entrada / Repor) of a tool at a position.</summary>
public sealed record RegistrarEntradaRequest(
    string ToolType,
    string? Reference,
    string? Lot,
    string PositionCode,
    string? Destination,
    string? Observations);

/// <summary>Register an immediate withdrawal (Retirar / Saída) — destination optional.</summary>
public sealed record RegistrarSaidaRequest(
    string ToolType,
    string? Reference,
    string? Lot,
    string? Destination,
    string? Observations);

/// <summary>Replace the tool occupying a position with another tool (one atomic workflow).</summary>
public sealed record SubstituirRequest(
    string PositionCode,
    string NewToolType,
    string? NewReference,
    string? NewLot,
    string? Observations);

/// <summary>Search tools or positions (Consulta).</summary>
public sealed record ConsultarRequest(
    string? ToolType,
    string? Reference,
    string? Lot,
    string? PositionCode);

// ---- DTOs returned to the UI ----------------------------------------------

public sealed record ArmazemSearchHit(
    WarehouseToolIdentity Tool,
    string? CurrentPositionCode,
    string LocationContext); // "armazem" | "fora" | "nao_registado"

public sealed record ArmazemConsultationRow(
    Guid ToolId,
    string Type,
    string Reference,
    string? TechnicalName,
    string Lot,
    string? PositionCode,
    string LocationContext,
    bool HasReferenceConflict);

public sealed record ArmazemLocationRow(
    string PositionCode,
    IReadOnlyList<ArmazemConsultationRow> Occupants,
    bool HasReferenceConflict);

public sealed record ArmazemHistoryEntry(
    string Direction,
    string? PositionCode,
    string? Destination,
    string? Observations,
    string? ActorId,
    DateTimeOffset OccurredAtUtc);