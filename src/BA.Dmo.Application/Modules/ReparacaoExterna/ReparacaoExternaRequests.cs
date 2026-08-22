using BA.Dmo.Domain.Modules.ReparacaoExterna;

namespace BA.Dmo.Application.Modules.ReparacaoExterna;

// ---- Commands --------------------------------------------------------------

/// <summary>Create a new external repair exit list (V1 CM/MF).</summary>
public sealed record CreateExitRequest(
    RepairType RepairType,
    Guid? RepairerId,
    DateOnly? PlannedDate,
    IReadOnlyList<NewExitItemRequest> Items,
    string? ProductionContext);

/// <summary>One CM/MF item of a new exit list.</summary>
public sealed record NewExitItemRequest(
    Guid PhysicalPieceId,
    string Number);

/// <summary>Add an item to an exit list that is still in preparation.</summary>
public sealed record AddExitItemRequest(Guid RepairExitId, Guid PhysicalPieceId, string Number);

/// <summary>Remove an item from an exit list still in preparation.</summary>
public sealed record RemoveExitItemRequest(Guid RepairExitId, Guid RepairExitItemId);

/// <summary>Disponibilizar the prepared list for warehouse pickup (Preparação → A retirar).</summary>
public sealed record DisponibilizarExitRequest(Guid RepairExitId);

/// <summary>Confirm the physical pickup of one item (out fact + warehouse release).</summary>
public sealed record ConfirmPickupRequest(Guid RepairExitItemId);

/// <summary>Confirm the physical return of one item (in fact + warehouse re-occupation).</summary>
public sealed record ConfirmReturnRequest(Guid RepairExitItemId, string PositionCode);

// ---- Repairer management (Definições) --------------------------------------

/// <summary>Create a repairer (active by default) with optional supported types (CM/MF/BQ).</summary>
public sealed record CreateRepairerRequest(string Name, IReadOnlyList<string>? SupportedTypes = null);

/// <summary>Update a repairer's editable fields (name) and supported types.</summary>
public sealed record UpdateRepairerRequest(Guid RepairerId, string Name, IReadOnlyList<string>? SupportedTypes = null);

/// <summary>Deactivate a repairer (never delete).</summary>
public sealed record DeactivateRepairerRequest(Guid RepairerId);

/// <summary>Set the default repairer for a line + tool type.</summary>
public sealed record UpsertLineDefaultRequest(string Line, string ToolType, Guid RepairerId);

// ---- DTOs returned to the UI ------------------------------------------------

public sealed record RepairExitItemDto(
    Guid RepairExitItemId,
    Guid? BqLoteId,
    Guid? PhysicalPieceId,
    string? Reference,
    string? Lot,
    decimal? Qty,
    string? Number,
    bool Picked,
    DateTimeOffset? OutAtUtc,
    string? OutOperatorId,
    DateTimeOffset? InAtUtc,
    string? InOperatorId,
    string Status,
    string? PositionCode,
    string PositionContext);

public sealed record RepairExitDto(
    Guid RepairExitId,
    string RepairType,
    Guid? RepairerId,
    string? RepairerName,
    DateOnly? PlannedDate,
    string Status,
    string? CreatedBy,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<RepairExitItemDto> Items);

public sealed record RepairerDto(Guid RepairerId, string Name, bool Active, IReadOnlySet<string> SupportedTypes);

public sealed record LineRepairerDefaultDto(string Line, string ToolType, Guid RepairerId);

public sealed record RepairHistoryRow(
    string? ListId,
    string Type,
    string? Reference,
    string? Lot,
    decimal? Qty,
    string? Number,
    string? RepairerName,
    DateTimeOffset? Saida,
    string? OperadorSaida,
    DateTimeOffset? Entrada,
    string? OperadorEntrada,
    string State);