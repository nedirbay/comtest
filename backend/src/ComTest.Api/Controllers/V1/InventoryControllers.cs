using Asp.Versioning;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Inventory;
using ComTest.Application.Modules.Inventory.Contracts;
using ComTest.Infrastructure.Identity.Authorization;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using Microsoft.AspNetCore.Mvc;

namespace ComTest.Api.Controllers.V1;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/inventory/warehouses")]
public sealed class AdminWarehousesController : TenantControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.InventoryRead)]
    [ProducesResponseType<IReadOnlyList<WarehouseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListWarehousesQuery, IReadOnlyList<WarehouseDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListWarehousesQuery(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.InventoryWrite)]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        SaveWarehouseRequest request,
        [FromServices] ICommandHandler<CreateWarehouseCommand, WarehouseDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new CreateWarehouseCommand(request), cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.InventoryWrite)]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        SaveWarehouseRequest request,
        [FromServices] ICommandHandler<UpdateWarehouseCommand, WarehouseDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new UpdateWarehouseCommand(id, request), cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.InventoryWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteWarehouseCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteWarehouseCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/inventory")]
public sealed class AdminStockController : TenantControllerBase
{
    [HttpGet("stock")]
    [HasPermission(Permissions.InventoryRead)]
    [ProducesResponseType<PagedResult<StockItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Stock(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] Guid? variantId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] bool lowOnly,
        [FromServices] IQueryHandler<ListStockQuery, PagedResult<StockItemDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListStockQuery(PageRequests.From(page, pageSize), variantId, warehouseId, lowOnly), cancellationToken));

    [HttpGet("movements")]
    [HasPermission(Permissions.InventoryRead)]
    [ProducesResponseType<PagedResult<StockMovementDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Movements(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] Guid? variantId,
        [FromQuery] Guid? warehouseId,
        [FromServices] IQueryHandler<ListMovementsQuery, PagedResult<StockMovementDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListMovementsQuery(PageRequests.From(page, pageSize), variantId, warehouseId), cancellationToken));

    [HttpPost("stock/adjustments")]
    [HasPermission(Permissions.InventoryWrite)]
    [ProducesResponseType<StockItemDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Adjust(
        AdjustStockRequest request,
        [FromServices] ICommandHandler<AdjustStockCommand, StockItemDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new AdjustStockCommand(request), cancellationToken));

    [HttpPut("stock/{id:guid}/threshold")]
    [HasPermission(Permissions.InventoryWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetThreshold(
        Guid id,
        SetThresholdRequest request,
        [FromServices] ICommandHandler<SetThresholdCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new SetThresholdCommand(id, request.Threshold), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}
