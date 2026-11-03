using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Inventory.Contracts;
using ComTest.Domain.Modules.Inventory;
using ComTest.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Inventory;

public sealed class StockService(IApplicationDbContext db, TimeProvider timeProvider) : IStockService
{
    private const int MaxAttemptsPerWarehouse = 3;

    public async Task<Result> ReserveAsync(Guid referenceId, IReadOnlyList<StockRequest> lines, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);

        // Always in the same order, so that two orders for the same items cannot deadlock.
        foreach (var line in lines.OrderBy(item => item.VariantId))
        {
            var remaining = line.Quantity;

            var candidates = await (
                from stock in db.StockItems
                join warehouse in db.Warehouses on stock.WarehouseId equals warehouse.Id
                where stock.ProductVariantId == line.VariantId && warehouse.IsActive
                orderby warehouse.IsDefault descending, stock.WarehouseId
                select stock.Id)
                .ToListAsync(cancellationToken);

            foreach (var stockItemId in candidates)
            {
                if (remaining == 0)
                {
                    break;
                }

                var taken = await TakeAsync(stockItemId, remaining, cancellationToken);

                if (taken > 0)
                {
                    remaining -= taken;
                    db.StockReservations.Add(StockReservation.Create(referenceId, stockItemId, taken));
                    await RecordAsync(stockItemId, StockMovementType.Reservation, 0, taken, null, referenceId, cancellationToken);
                }
            }

            if (remaining > 0)
            {
                return Result.Failure(new Error(
                    ErrorCodes.Inventory.InsufficientStock,
                    ErrorType.Conflict,
                    new Dictionary<string, string[]>(StringComparer.Ordinal) { ["items"] = [line.VariantId.ToString("D")] }));
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task CommitAsync(Guid referenceId, CancellationToken cancellationToken)
    {
        foreach (var reservation in await LoadAsync(referenceId, ReservationStatus.Active, cancellationToken))
        {
            await ChangeQuantitiesAsync(reservation.StockItemId, -reservation.Quantity, -reservation.Quantity, cancellationToken);
            reservation.MarkCommitted();
            await RecordAsync(reservation.StockItemId, StockMovementType.Sale, -reservation.Quantity, -reservation.Quantity, null, referenceId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(Guid referenceId, CancellationToken cancellationToken)
    {
        foreach (var reservation in await LoadAsync(referenceId, ReservationStatus.Active, cancellationToken))
        {
            await ChangeQuantitiesAsync(reservation.StockItemId, 0, -reservation.Quantity, cancellationToken);
            reservation.MarkReleased();
            await RecordAsync(reservation.StockItemId, StockMovementType.ReservationRelease, 0, -reservation.Quantity, null, referenceId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestockAsync(Guid referenceId, CancellationToken cancellationToken)
    {
        foreach (var reservation in await LoadAsync(referenceId, ReservationStatus.Committed, cancellationToken))
        {
            await ChangeQuantitiesAsync(reservation.StockItemId, reservation.Quantity, 0, cancellationToken);
            reservation.MarkReleased();
            await RecordAsync(reservation.StockItemId, StockMovementType.Return, reservation.Quantity, 0, null, referenceId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetAvailableAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variantIds);

        if (variantIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var totals = await (
            from stock in db.StockItems
            join warehouse in db.Warehouses on stock.WarehouseId equals warehouse.Id
            where variantIds.Contains(stock.ProductVariantId) && warehouse.IsActive
            group stock by stock.ProductVariantId into variant
            select new { VariantId = variant.Key, Available = variant.Sum(item => item.OnHand - item.Reserved) })
            .ToListAsync(cancellationToken);

        return totals.ToDictionary(item => item.VariantId, item => item.Available);
    }

    /// <summary>
    /// The condition in the UPDATE is what prevents overselling. PostgreSQL rechecks it after
    /// waiting for a competing transaction, so a stale read can only make this take too little.
    /// </summary>
    private async Task<int> TakeAsync(Guid stockItemId, int wanted, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        for (var attempt = 0; attempt < MaxAttemptsPerWarehouse; attempt++)
        {
            var available = await db.StockItems
                .AsNoTracking()
                .Where(item => item.Id == stockItemId)
                .Select(item => item.OnHand - item.Reserved)
                .FirstOrDefaultAsync(cancellationToken);

            var take = Math.Min(available, wanted);

            if (take <= 0)
            {
                return 0;
            }

            var affected = await db.StockItems
                .Where(item => item.Id == stockItemId && item.OnHand - item.Reserved >= take)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.Reserved, item => item.Reserved + take)
                        .SetProperty(item => item.UpdatedAt, now),
                    cancellationToken);

            if (affected == 1)
            {
                return take;
            }
        }

        return 0;
    }

    private async Task ChangeQuantitiesAsync(Guid stockItemId, int onHandDelta, int reservedDelta, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await db.StockItems
            .Where(item => item.Id == stockItemId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.OnHand, item => item.OnHand + onHandDelta)
                    .SetProperty(item => item.Reserved, item => item.Reserved + reservedDelta)
                    .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);
    }

    private Task<List<StockReservation>> LoadAsync(Guid referenceId, ReservationStatus status, CancellationToken cancellationToken) =>
        db.StockReservations.Where(item => item.ReferenceId == referenceId && item.Status == status).ToListAsync(cancellationToken);

    private Task RecordAsync(Guid stockItemId, StockMovementType type, int onHandDelta, int reservedDelta, string? reason, Guid? referenceId, CancellationToken cancellationToken) =>
        StockLedger.RecordAsync(db, timeProvider, stockItemId, type, onHandDelta, reservedDelta, reason, referenceId, cancellationToken);
}

internal static class StockLedger
{
    /// <summary>Writes the movement with the row's values after the change, and checks the low-stock alert.</summary>
    public static async Task RecordAsync(
        IApplicationDbContext db,
        TimeProvider timeProvider,
        Guid stockItemId,
        StockMovementType type,
        int onHandDelta,
        int reservedDelta,
        string? reason,
        Guid? referenceId,
        CancellationToken cancellationToken)
    {
        // Quantities were changed in SQL, so a tracked copy would be out of date. Read it fresh.
        var stock = await db.StockItems.FirstAsync(item => item.Id == stockItemId, cancellationToken);
        await db.Entry(stock).ReloadAsync(cancellationToken);

        db.StockMovements.Add(StockMovement.Create(
            stock.Id,
            stock.WarehouseId,
            stock.ProductVariantId,
            type,
            onHandDelta,
            reservedDelta,
            stock.OnHand,
            stock.Reserved,
            reason,
            referenceId));

        stock.CheckLowStock(timeProvider.GetUtcNow());
    }
}
