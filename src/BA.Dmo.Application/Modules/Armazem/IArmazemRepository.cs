using BA.Dmo.Domain.Modules.Armazem;

namespace BA.Dmo.Application.Modules.Armazem;

/// <summary>
/// U-14 — Armazém read/write port (N09, GLM-ARM-04). Owns warehouse persistence
/// only. Multi-table writes (stock + movement; Substituir release+occupy) run
/// inside one transaction (GLM-DATA-05) inside the implementation. Movement
/// facts are append-only; <c>fora</c> is derived, never stored.
/// </summary>
public interface IArmazemRepository
{
    // ---- Locations ---------------------------------------------------------
    Task<Guid> GetOrCreateLocationAsync(string code, string? kind, CancellationToken ct = default);
    Task<WarehouseLocation?> GetLocationByCodeAsync(string code, CancellationToken ct = default);
    Task<WarehouseLocation?> GetLocationByIdAsync(Guid warehouseLocationId, CancellationToken ct = default);

    // ---- Stock -------------------------------------------------------------
    Task<WarehouseStock?> GetActiveStockByLocationAsync(Guid warehouseLocationId, CancellationToken ct = default);
    Task<WarehouseStock?> GetActiveStockByToolIdAsync(Guid toolId, CancellationToken ct = default);
    Task<IReadOnlyList<WarehouseStock>> GetStockByLocationAsync(Guid warehouseLocationId, CancellationToken ct = default);

    // ---- Writes (atomic) ---------------------------------------------------
    Task<Guid> RegisterEntradaAsync(
        WarehouseStock stock, WarehouseMovement movement, CancellationToken ct = default);
    Task RegisterSaidaAsync(
        Guid stockId, string? releasedBy, DateTimeOffset releasedAtUtc,
        WarehouseMovement movement, CancellationToken ct = default);
    Task CorrectLocationAsync(
        Guid? currentStockId,
        WarehouseStock? correctedStock,
        WarehouseMovement? outMovement,
        WarehouseMovement? inMovement,
        CancellationToken ct = default);

    // ---- Historical / consultation ----------------------------------------
    Task<IReadOnlyList<WarehouseMovement>> GetMovementHistoryAsync(Guid toolId, CancellationToken ct = default);
    Task<IReadOnlyList<WarehouseMovementFact>> ListMovementFactsAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int limit,
        CancellationToken ct = default);

    // ---- Audit -------------------------------------------------------------
    Task InsertAuditEventAsync(
        Guid? entityId, string eventType, string? beforeSnapshot, string? afterSnapshot,
        string actorId, CancellationToken ct = default);
}

/// <summary>
/// Armazém-owned persistence projection. It deliberately contains only
/// warehouse facts; reference/type/lot remain owned by the injected tool
/// identity resolver.
/// </summary>
public sealed record WarehouseMovementFact(
    Guid ToolId,
    string? PositionCode,
    WarehouseMovement Movement);
