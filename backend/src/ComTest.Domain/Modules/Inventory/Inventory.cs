using ComTest.Shared.Domain;

namespace ComTest.Domain.Modules.Inventory;

public enum StockMovementType
{
    Receipt,
    Adjustment,
    Reservation,
    ReservationRelease,
    Sale,
    Return,
}

public enum ReservationStatus
{
    Active,
    Committed,
    Released,
}

public sealed record LowStockDetected(Guid StockItemId, Guid WarehouseId, Guid VariantId, int Available, int Threshold, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed class Warehouse : SoftDeletableEntity
{
    private Warehouse()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public static Warehouse Create(string name, string code, string? address, bool isDefault) => new()
    {
        Name = name,
        Code = code,
        Address = address,
        IsDefault = isDefault,
        IsActive = true,
    };

    public void Update(string name, string code, string? address, bool isActive)
    {
        Name = name;
        Code = code;
        Address = address;
        IsActive = isActive;
    }

    public void SetDefault(bool isDefault) => IsDefault = isDefault;
}

/// <summary>
/// How much of one variant one warehouse holds. Quantities change only through conditional
/// SQL updates, and a database constraint keeps them valid, so that two simultaneous orders
/// can never sell the same unit.
/// </summary>
public sealed class StockItem : TenantOwnedEntity
{
    private StockItem()
    {
    }

    public Guid WarehouseId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public int OnHand { get; private set; }

    public int Reserved { get; private set; }

    public int LowStockThreshold { get; private set; }

    public DateTimeOffset? LowStockNotifiedAt { get; private set; }

    public int Available => OnHand - Reserved;

    public static StockItem Create(Guid warehouseId, Guid variantId) => new()
    {
        WarehouseId = warehouseId,
        ProductVariantId = variantId,
    };

    public void SetThreshold(int threshold) => LowStockThreshold = threshold;

    /// <summary>Raises the alert once per dip below the threshold, and rearms it after a restock.</summary>
    public void CheckLowStock(DateTimeOffset now)
    {
        if (Available > LowStockThreshold)
        {
            LowStockNotifiedAt = null;

            return;
        }

        if (LowStockNotifiedAt is null && LowStockThreshold > 0)
        {
            LowStockNotifiedAt = now;
            Raise(new LowStockDetected(Id, WarehouseId, ProductVariantId, Available, LowStockThreshold, now));
        }
    }
}

/// <summary>Append-only history of every stock change. CreatedBy records who made it.</summary>
public sealed class StockMovement : TenantOwnedEntity
{
    private StockMovement()
    {
    }

    public Guid StockItemId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public StockMovementType Type { get; private set; }

    public int OnHandDelta { get; private set; }

    public int ReservedDelta { get; private set; }

    public int OnHandAfter { get; private set; }

    public int ReservedAfter { get; private set; }

    public string? Reason { get; private set; }

    public Guid? ReferenceId { get; private set; }

    public static StockMovement Create(
        Guid stockItemId,
        Guid warehouseId,
        Guid variantId,
        StockMovementType type,
        int onHandDelta,
        int reservedDelta,
        int onHandAfter,
        int reservedAfter,
        string? reason,
        Guid? referenceId) => new()
        {
            StockItemId = stockItemId,
            WarehouseId = warehouseId,
            ProductVariantId = variantId,
            Type = type,
            OnHandDelta = onHandDelta,
            ReservedDelta = reservedDelta,
            OnHandAfter = onHandAfter,
            ReservedAfter = reservedAfter,
            Reason = reason,
            ReferenceId = referenceId,
        };
}

/// <summary>Units set aside for one order, so that the order can be shipped or cancelled exactly.</summary>
public sealed class StockReservation : TenantOwnedEntity
{
    private StockReservation()
    {
    }

    public Guid ReferenceId { get; private set; }

    public Guid StockItemId { get; private set; }

    public int Quantity { get; private set; }

    public ReservationStatus Status { get; private set; }

    public static StockReservation Create(Guid referenceId, Guid stockItemId, int quantity) => new()
    {
        ReferenceId = referenceId,
        StockItemId = stockItemId,
        Quantity = quantity,
        Status = ReservationStatus.Active,
    };

    public void MarkCommitted() => Status = ReservationStatus.Committed;

    public void MarkReleased() => Status = ReservationStatus.Released;
}
