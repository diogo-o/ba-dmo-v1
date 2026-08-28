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

/// <summary>Search tools or positions (Consulta).</summary>
public sealed record ConsultarRequest(
    string? ToolType,
    string? Reference,
    string? Lot,
    string? PositionCode);

/// <summary>
/// Correct the physical location found by the operator. A null/blank found
/// position means the tool is not physically present in the warehouse.
/// </summary>
public sealed record CorrigirLocalizacaoRequest(
    Guid ToolId,
    string? FoundPositionCode,
    string? Observations);

public sealed record CorrigirLocalizacaoResult(
    string? RegisteredPositionCode,
    string? FoundPositionCode);

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

/// <summary>
/// Read-only movement projection used by the Armazém recent/history surfaces.
/// Tool identity is resolved through the owner lookup; the warehouse repository
/// supplies only its own movement, stock and position facts.
/// </summary>
public sealed record ArmazemMovementRow(
    Guid MovementId,
    Guid ToolId,
    string Type,
    string Reference,
    string Lot,
    string Direction,
    string? PositionCode,
    string? Destination,
    string? ActorId,
    DateTimeOffset OccurredAtUtc);
