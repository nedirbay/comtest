using ComTest.Domain.Modules.Inventory;
using ComTest.Shared.Results;

namespace ComTest.Application.Modules.Inventory.Contracts;

public sealed record StockRequest(Guid VariantId, int Quantity);

/// <summary>Stock operations other modules use. All of them must run inside the caller's transaction.</summary>
/// <summary>One variant across every warehouse.</summary>
public sealed record StockLevel(Guid VariantId, int OnHand, int Reserved, bool IsLow)
{
    public int Available => OnHand - Reserved;
}

public interface IStockService
{
    /// <summary>Sets units aside. On failure the caller must roll back, as earlier lines may have succeeded.</summary>
    Task<Result> ReserveAsync(Guid referenceId, IReadOnlyList<StockRequest> lines, CancellationToken cancellationToken);

    /// <summary>The goods have left: reserved units are removed from stock.</summary>
    Task CommitAsync(Guid referenceId, CancellationToken cancellationToken);

    /// <summary>The goods stay: reserved units become available again.</summary>
    Task ReleaseAsync(Guid referenceId, CancellationToken cancellationToken);

    /// <summary>Shipped goods came back: committed units return to stock, once.</summary>
    Task RestockAsync(Guid referenceId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> GetAvailableAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockLevel>> GetLevelsAsync(CancellationToken cancellationToken);
}

public sealed record WarehouseDto(Guid Id, string Name, string Code, string? Address, bool IsDefault, bool IsActive);

public sealed record StockItemDto(
    Guid Id,
    Guid WarehouseId,
    string WarehouseCode,
    Guid VariantId,
    string? Sku,
    string? ProductName,
    int OnHand,
    int Reserved,
    int Available,
    int LowStockThreshold,
    bool IsLow);

public sealed record StockMovementDto(
    Guid Id,
    Guid WarehouseId,
    Guid VariantId,
    StockMovementType Type,
    int OnHandDelta,
    int ReservedDelta,
    int OnHandAfter,
    int ReservedAfter,
    string? Reason,
    Guid? ReferenceId,
    DateTimeOffset At,
    Guid? By);
