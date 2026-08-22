namespace BA.Dmo.Domain.Modules.Armazem;

/// <summary>
/// U-14 — Movement fact of a tool into/out of the warehouse (N09
/// <c>warehouse_movements</c>). Append-only. Direction is <c>in</c> or <c>out</c>;
/// destination (Produção/Reparação) is OPTIONAL for U-14 (owner decision D).
/// </summary>
public sealed class WarehouseMovement
{
    public Guid WarehouseMovementId { get; set; } = Guid.NewGuid();

    public Guid? WarehouseStockId { get; set; }

    public WarehouseMovementDirection Direction { get; set; }

    public decimal? Qty { get; set; }

    public string? Destination { get; set; }

    public string? ActorId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}

public enum WarehouseMovementDirection
{
    In,
    Out
}

/// <summary>Codec between the domain enum and the N09 stored text discriminator.</summary>
public static class WarehouseMovementDirectionCodec
{
    public static string ToStorage(WarehouseMovementDirection direction) => direction switch
    {
        WarehouseMovementDirection.In => "in",
        WarehouseMovementDirection.Out => "out",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction.")
    };

    public static WarehouseMovementDirection FromStorage(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "in" => WarehouseMovementDirection.In,
        "out" => WarehouseMovementDirection.Out,
        _ => throw new InvalidOperationException($"Unknown persisted warehouse direction: {value}")
    };
}