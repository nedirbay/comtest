using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Catalog.Contracts;
using ComTest.Application.Modules.Inventory.Contracts;
using ComTest.Domain.Modules.Inventory;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Inventory;

public sealed record SaveWarehouseRequest(string Name, string Code, string? Address = null, bool IsDefault = false, bool IsActive = true);

internal sealed class SaveWarehouseRequestValidator : AbstractValidator<SaveWarehouseRequest>
{
    public SaveWarehouseRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed record CreateWarehouseCommand(SaveWarehouseRequest Request) : ICommand<WarehouseDto>;

public sealed record UpdateWarehouseCommand(Guid Id, SaveWarehouseRequest Request) : ICommand<WarehouseDto>;

public sealed record DeleteWarehouseCommand(Guid Id) : ICommand<bool>;

public sealed record ListWarehousesQuery : IQuery<IReadOnlyList<WarehouseDto>>;

public sealed class WarehouseHandlers(IApplicationDbContext db) :
    ICommandHandler<CreateWarehouseCommand, WarehouseDto>,
    ICommandHandler<UpdateWarehouseCommand, WarehouseDto>,
    ICommandHandler<DeleteWarehouseCommand, bool>,
    IQueryHandler<ListWarehousesQuery, IReadOnlyList<WarehouseDto>>
{
    public async Task<Result<WarehouseDto>> HandleAsync(CreateWarehouseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        if (await db.Warehouses.AnyAsync(item => item.Code == request.Code, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Inventory.WarehouseCodeTaken);
        }

        // The first warehouse is the default, so that stock always has somewhere to go.
        var isDefault = request.IsDefault || !await db.Warehouses.AnyAsync(cancellationToken);

        if (isDefault)
        {
            await ClearDefaultAsync(cancellationToken);
        }

        var warehouse = Warehouse.Create(request.Name, request.Code, request.Address, isDefault);

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(warehouse);
    }

    public async Task<Result<WarehouseDto>> HandleAsync(UpdateWarehouseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (warehouse is null)
        {
            return Error.NotFound(ErrorCodes.Inventory.WarehouseNotFound);
        }

        if (await db.Warehouses.AnyAsync(item => item.Code == request.Code && item.Id != command.Id, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Inventory.WarehouseCodeTaken);
        }

        if (request.IsDefault && !warehouse.IsDefault)
        {
            await ClearDefaultAsync(cancellationToken);
            warehouse.SetDefault(true);
        }

        warehouse.Update(request.Name, request.Code, request.Address, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(warehouse);
    }

    public async Task<Result<bool>> HandleAsync(DeleteWarehouseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (warehouse is null)
        {
            return Error.NotFound(ErrorCodes.Inventory.WarehouseNotFound);
        }

        if (await db.StockItems.AnyAsync(item => item.WarehouseId == command.Id && item.OnHand > 0, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Inventory.WarehouseNotEmpty);
        }

        db.Warehouses.Remove(warehouse);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<Result<IReadOnlyList<WarehouseDto>>> HandleAsync(ListWarehousesQuery query, CancellationToken cancellationToken)
    {
        var warehouses = await db.Warehouses
            .AsNoTracking()
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Code)
            .Select(item => new WarehouseDto(item.Id, item.Name, item.Code, item.Address, item.IsDefault, item.IsActive))
            .ToListAsync(cancellationToken);

        return warehouses;
    }

    private async Task ClearDefaultAsync(CancellationToken cancellationToken)
    {
        foreach (var current in await db.Warehouses.Where(item => item.IsDefault).ToListAsync(cancellationToken))
        {
            current.SetDefault(false);
        }
    }

    private static WarehouseDto ToDto(Warehouse warehouse) =>
        new(warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address, warehouse.IsDefault, warehouse.IsActive);
}

public sealed record AdjustStockRequest(Guid WarehouseId, Guid VariantId, int Delta, StockMovementType Type, string Reason);

internal sealed class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    private const int MaxDelta = 1_000_000;

    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.Delta).NotEqual(0).InclusiveBetween(-MaxDelta, MaxDelta);

        // Only staff actions: sales and reservations must come from real orders.
        RuleFor(x => x.Type).Must(type => type is StockMovementType.Receipt or StockMovementType.Adjustment);
        RuleFor(x => x.Delta).GreaterThan(0).When(x => x.Type == StockMovementType.Receipt);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed record SetThresholdRequest(int Threshold);

internal sealed class SetThresholdRequestValidator : AbstractValidator<SetThresholdRequest>
{
    public SetThresholdRequestValidator()
    {
        RuleFor(x => x.Threshold).InclusiveBetween(0, 1_000_000);
    }
}

public sealed record AdjustStockCommand(AdjustStockRequest Request) : ICommand<StockItemDto>;

public sealed record SetThresholdCommand(Guid StockItemId, int Threshold) : ICommand<bool>;

public sealed record ListStockQuery(PageRequest Page, Guid? VariantId, Guid? WarehouseId, bool LowOnly) : IQuery<PagedResult<StockItemDto>>;

public sealed record ListMovementsQuery(PageRequest Page, Guid? VariantId, Guid? WarehouseId) : IQuery<PagedResult<StockMovementDto>>;

public sealed class StockHandlers(IApplicationDbContext db, ICatalogReader catalog, TimeProvider timeProvider) :
    ICommandHandler<AdjustStockCommand, StockItemDto>,
    ICommandHandler<SetThresholdCommand, bool>,
    IQueryHandler<ListStockQuery, PagedResult<StockItemDto>>,
    IQueryHandler<ListMovementsQuery, PagedResult<StockMovementDto>>
{
    public async Task<Result<StockItemDto>> HandleAsync(AdjustStockCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var warehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.WarehouseId, cancellationToken);

        if (warehouse is null)
        {
            return Error.NotFound(ErrorCodes.Inventory.WarehouseNotFound);
        }

        var variants = await catalog.GetVariantsAsync([request.VariantId], cancellationToken);

        if (!variants.TryGetValue(request.VariantId, out var variant))
        {
            return Error.NotFound(ErrorCodes.Inventory.VariantNotFound);
        }

        var now = timeProvider.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var stockItemId = await FindOrCreateAsync(request.WarehouseId, request.VariantId, cancellationToken);

        // Stock may only fall as far as what is promised to existing orders.
        var affected = await db.StockItems
            .Where(item => item.Id == stockItemId && item.OnHand + request.Delta >= item.Reserved)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.OnHand, item => item.OnHand + request.Delta)
                    .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);

        if (affected == 0)
        {
            return Error.Conflict(ErrorCodes.Inventory.InsufficientStock);
        }

        await StockLedger.RecordAsync(db, timeProvider, stockItemId, request.Type, request.Delta, 0, request.Reason, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var stock = await db.StockItems.AsNoTracking().FirstAsync(item => item.Id == stockItemId, cancellationToken);

        return ToDto(stock, warehouse.Code, variant);
    }

    public async Task<Result<bool>> HandleAsync(SetThresholdCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var stock = await db.StockItems.FirstOrDefaultAsync(item => item.Id == command.StockItemId, cancellationToken);

        if (stock is null)
        {
            return Error.NotFound(ErrorCodes.Inventory.StockItemNotFound);
        }

        stock.SetThreshold(command.Threshold);
        stock.CheckLowStock(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<Result<PagedResult<StockItemDto>>> HandleAsync(ListStockQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows =
            from stock in db.StockItems.AsNoTracking()
            join warehouse in db.Warehouses.AsNoTracking() on stock.WarehouseId equals warehouse.Id
            select new { Stock = stock, WarehouseCode = warehouse.Code };

        if (query.VariantId is { } variantId)
        {
            rows = rows.Where(row => row.Stock.ProductVariantId == variantId);
        }

        if (query.WarehouseId is { } warehouseId)
        {
            rows = rows.Where(row => row.Stock.WarehouseId == warehouseId);
        }

        if (query.LowOnly)
        {
            rows = rows.Where(row => row.Stock.OnHand - row.Stock.Reserved <= row.Stock.LowStockThreshold);
        }

        var total = await rows.LongCountAsync(cancellationToken);

        var page = await rows
            .OrderBy(row => row.Stock.OnHand - row.Stock.Reserved)
            .ThenBy(row => row.Stock.Id)
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .ToListAsync(cancellationToken);

        var variants = await catalog.GetVariantsAsync(page.Select(row => row.Stock.ProductVariantId).Distinct().ToList(), cancellationToken);

        var items = page
            .Select(row => ToDto(row.Stock, row.WarehouseCode, variants.GetValueOrDefault(row.Stock.ProductVariantId)))
            .ToList();

        return new PagedResult<StockItemDto>(items, query.Page.Page, query.Page.PageSize, total);
    }

    public async Task<Result<PagedResult<StockMovementDto>>> HandleAsync(ListMovementsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var movements = db.StockMovements.AsNoTracking();

        if (query.VariantId is { } variantId)
        {
            movements = movements.Where(item => item.ProductVariantId == variantId);
        }

        if (query.WarehouseId is { } warehouseId)
        {
            movements = movements.Where(item => item.WarehouseId == warehouseId);
        }

        var total = await movements.LongCountAsync(cancellationToken);

        var items = await movements
            .OrderByDescending(item => item.Id)
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .Select(item => new StockMovementDto(
                item.Id,
                item.WarehouseId,
                item.ProductVariantId,
                item.Type,
                item.OnHandDelta,
                item.ReservedDelta,
                item.OnHandAfter,
                item.ReservedAfter,
                item.Reason,
                item.ReferenceId,
                item.CreatedAt,
                item.CreatedBy))
            .ToListAsync(cancellationToken);

        return new PagedResult<StockMovementDto>(items, query.Page.Page, query.Page.PageSize, total);
    }

    private async Task<Guid> FindOrCreateAsync(Guid warehouseId, Guid variantId, CancellationToken cancellationToken)
    {
        var existing = await db.StockItems
            .Where(item => item.WarehouseId == warehouseId && item.ProductVariantId == variantId)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is { } id)
        {
            return id;
        }

        var stock = StockItem.Create(warehouseId, variantId);

        db.StockItems.Add(stock);
        await db.SaveChangesAsync(cancellationToken);

        return stock.Id;
    }

    private static StockItemDto ToDto(StockItem stock, string warehouseCode, PurchasableVariant? variant) => new(
        stock.Id,
        stock.WarehouseId,
        warehouseCode,
        stock.ProductVariantId,
        variant?.Sku,
        variant?.ProductName,
        stock.OnHand,
        stock.Reserved,
        stock.Available,
        stock.LowStockThreshold,
        stock.Available <= stock.LowStockThreshold);
}
