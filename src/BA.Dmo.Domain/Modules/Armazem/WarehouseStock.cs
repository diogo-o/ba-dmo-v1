namespace BA.Dmo.Domain.Modules.Armazem;

/// <summary>
/// U-14 — Occupation fact of a position by a tool lot (N09
/// <c>warehouse_stock</c>). Release keeps the row (<c>released_at_utc</c> set)
/// so the partial unique index allows re-occupation; historical facts are
/// preserved (GLM-DATA-04). <c>fora</c> is DERIVED (no active row), never stored.
/// </summary>
public sealed class WarehouseStock
{
    public Guid WarehouseStockId { get; set; } = Guid.NewGuid();

    public Guid WarehouseLocationId { get; set; }

    /// <summary>Stable tool lot id from the owning tool domain (via IToolIdentityResolver).</summary>
    public Guid ToolId { get; set; }

    public DateTimeOffset OccupiedSinceUtc { get; set; }

    public string? OccupiedBy { get; set; }

    public DateTimeOffset? ReleasedAtUtc { get; set; }

    public string? ReleasedBy { get; set; }

    public bool IsActive => !ReleasedAtUtc.HasValue;
}