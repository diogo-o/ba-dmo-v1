using BA.Dmo.Domain.Modules.Boquilhas;

namespace BA.Dmo.Application.Modules.Boquilhas;

// ---- Requests (commands) ----------------------------------------------------

/// <summary>Create a lot + its first production trace + START + initial utilisation in one transaction.</summary>
public sealed record CreateBqLoteRequest(
    string Reference,
    string BatchCode,
    IReadOnlyList<string> AllowedLines,
    decimal InitialQuantity,
    decimal? InitialUtilisation,
    string? Notes);

/// <summary>Register a movement (Saída/Entrada/Não reparadas/Linha/Corrigir contagem) on the active trace.</summary>
public sealed record RegisterBqMovementRequest(
    Guid BqLoteId,
    Guid BqTraceId,
    BqMovementType MovementType,
    decimal? Qty,
    Guid? RepairerId,
    string? Line,
    string? Notes);

/// <summary>Close an active trace: final snapshot + counts + reason.</summary>
public sealed record CloseBqTraceRequest(
    Guid BqLoteId,
    Guid BqTraceId,
    decimal? FinalUtilisation,
    string? Reason);

/// <summary>Reopen the last closed trace (no other active trace allowed).</summary>
public sealed record ReopenBqTraceRequest(
    Guid BqLoteId,
    Guid BqTraceId,
    string? Reason);

/// <summary>Edit editable lot fields (reference/batch_code/allowed_lines).</summary>
public sealed record EditBqLoteRequest(
    Guid BqLoteId,
    string? Reference,
    string? BatchCode,
    IReadOnlyList<string>? AllowedLines,
    string? ChangeNote);

/// <summary>Append a lifecycle event (archive/scrap/restore).</summary>
public sealed record BqLifecycleRequest(
    Guid BqLoteId,
    BqLifecycleEventKind Kind,
    string? Reason);

/// <summary>Resolve an open return-excess discrepancy (auditable, never rewrites the return).</summary>
public sealed record ResolveBqDiscrepancyRequest(
    Guid BqDiscrepancyId,
    string? ResolutionNote);

// ---- Repairers --------------------------------------------------------------

public sealed record CreateBqRepairerRequest(string Name);
public sealed record UpdateBqRepairerRequest(Guid RepairerId, string? Name, bool? Active);
public sealed record SetLineRepairerDefaultRequest(
    string Line,
    Guid? DefaultRepairerId,
    IReadOnlyList<Guid> AllowedRepairerIds);

// ---- Queries / read DTOs -----------------------------------------------------

public sealed record BqLoteDto(
    Guid BqLoteId,
    string Reference,
    string BatchCode,
    IReadOnlyList<string> AllowedLines,
    BqLifecycleState LifecycleState,
    string ActorLabel);

public sealed record BqMovementRowDto(
    Guid BqMovementId,
    BqMovementType MovementType,
    decimal? Qty,
    decimal? ExceptionalReceivedQty,
    string? Line,
    Guid? RepairerId,
    string? RepairerName,
    string? Notes,
    string? ActorId,
    DateTimeOffset OccurredAtUtc,
    BqSaldos SaldoAfter,
    string? Reference = null,
    string? BatchCode = null);

public sealed record BqSaldosDto(
    decimal Prod,
    decimal Repair,
    decimal Irreparable,
    decimal ExceptionalReceived,
    decimal TransactionalBalance,
    decimal PhysicalInventory);

public sealed record BqDiscrepancyDto(
    Guid BqDiscrepancyId,
    Guid BqLoteId,
    Guid? BqTraceId,
    decimal ExpectedQty,
    decimal ActualQty,
    decimal ExcessQty,
    BqDiscrepancyStatus Status,
    string? ResolutionNote,
    DateTimeOffset CreatedAtUtc);

public sealed record BqRepairerDto(Guid RepairerId, string Name, bool Active, IReadOnlySet<string> SupportedTypes);

public sealed record BqLineRepairerDefaultDto(
    string Line,
    Guid? DefaultRepairerId,
    IReadOnlyList<Guid> AllowedRepairerIds);

public sealed record BqTraceDto(
    Guid BqTraceId,
    BqTraceStatus Status,
    BqTracePurpose Purpose,
    string? StartLine,
    decimal? SapStart,
    decimal? SapEnd);

public sealed record BqLotSummaryDto(
    BqLoteDto Lote,
    BqTraceDto? ActiveTrace,
    BqSaldosDto Saldo,
    decimal? InitialUtilisation,
    decimal? CurrentUtilisation,
    int MovementCount);