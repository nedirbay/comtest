using Asp.Versioning;
using ComTest.Application.Modules.Catalog;
using ComTest.Application.Modules.Catalog.Contracts;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Domain.Modules.Catalog;
using ComTest.Infrastructure.Identity.Authorization;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComTest.Api.Controllers.V1;

/// <summary>Public catalog of the store this request's address belongs to.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/catalog")]
[AllowAnonymous]
public sealed class CatalogController : TenantControllerBase
{
    [HttpGet("categories")]
    [ProducesResponseType<IReadOnlyList<StoreCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Categories(
        [FromServices] IQueryHandler<GetStoreCategoriesQuery, IReadOnlyList<StoreCategoryDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetStoreCategoriesQuery(), cancellationToken));

    [HttpGet("brands")]
    [ProducesResponseType<IReadOnlyList<StoreBrandDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Brands(
        [FromServices] IQueryHandler<GetStoreBrandsQuery, IReadOnlyList<StoreBrandDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetStoreBrandsQuery(), cancellationToken));

    [HttpGet("products")]
    [ProducesResponseType<PagedResult<StoreProductListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Products(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? sort,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? brandId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? q,
        [FromServices] IQueryHandler<ListStoreProductsQuery, PagedResult<StoreProductListItemDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(
            new ListStoreProductsQuery(PageRequests.From(page, pageSize), sort, categoryId, brandId, minPrice, maxPrice, q),
            cancellationToken));

    [HttpGet("products/{slug}")]
    [ProducesResponseType<StoreProductDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Product(
        string slug,
        [FromServices] IQueryHandler<GetStoreProductQuery, StoreProductDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetStoreProductQuery(slug), cancellationToken));
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/catalog/categories")]
public sealed class AdminCategoriesController : TenantControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListCategoriesQuery, IReadOnlyList<CategoryDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListCategoriesQuery(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        SaveCategoryRequest request,
        [FromServices] ICommandHandler<CreateCategoryCommand, CategoryDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new CreateCategoryCommand(request), cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        SaveCategoryRequest request,
        [FromServices] ICommandHandler<UpdateCategoryCommand, CategoryDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new UpdateCategoryCommand(id, request), cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteCategoryCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteCategoryCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    [HttpPost("{id:guid}/attributes")]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AssignAttribute(
        Guid id,
        AssignCategoryAttributeRequest request,
        [FromServices] ICommandHandler<AssignCategoryAttributeCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AssignCategoryAttributeCommand(id, request), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/catalog/brands")]
public sealed class AdminBrandsController : TenantControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType<IReadOnlyList<BrandDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListBrandsQuery, IReadOnlyList<BrandDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListBrandsQuery(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType<BrandDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        SaveBrandRequest request,
        [FromServices] ICommandHandler<CreateBrandCommand, BrandDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new CreateBrandCommand(request), cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType<BrandDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        SaveBrandRequest request,
        [FromServices] ICommandHandler<UpdateBrandCommand, BrandDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new UpdateBrandCommand(id, request), cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteBrandCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteBrandCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/catalog/attributes")]
public sealed class AdminAttributesController : TenantControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType<IReadOnlyList<AttributeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListAttributesQuery, IReadOnlyList<AttributeDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListAttributesQuery(), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType<AttributeDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        CreateAttributeRequest request,
        [FromServices] ICommandHandler<CreateAttributeCommand, AttributeDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new CreateAttributeCommand(request), cancellationToken));
}

public sealed record ChangeProductStatusRequest(ProductStatus Status);

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/catalog/products")]
public sealed class AdminProductsController : TenantControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType<PagedResult<ProductAdminListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? sort,
        [FromQuery] ProductStatus? status,
        [FromQuery] string? q,
        [FromServices] IQueryHandler<ListProductsQuery, PagedResult<ProductAdminListItemDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListProductsQuery(PageRequests.From(page, pageSize), sort, status, q), cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        Guid id,
        [FromServices] IQueryHandler<GetProductQuery, ProductAdminDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetProductQuery(id), cancellationToken));

    [HttpGet("{id:guid}/price-history")]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType<IReadOnlyList<PriceChangeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PriceHistory(
        Guid id,
        [FromServices] IQueryHandler<GetPriceHistoryQuery, IReadOnlyList<PriceChangeDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetPriceHistoryQuery(id), cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        SaveProductRequest request,
        [FromServices] ICommandHandler<CreateProductCommand, ProductAdminDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new CreateProductCommand(request), cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        SaveProductRequest request,
        [FromServices] ICommandHandler<UpdateProductCommand, ProductAdminDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new UpdateProductCommand(id, request), cancellationToken));

    [HttpPut("{id:guid}/status")]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType<ProductAdminDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        ChangeProductStatusRequest request,
        [FromServices] ICommandHandler<ChangeProductStatusCommand, ProductAdminDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ChangeProductStatusCommand(id, request.Status), cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteProductCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteProductCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}
