using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Catalog.Contracts;
using ComTest.Domain.Modules.Catalog;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using ComTest.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Catalog;

public sealed record GetStoreCategoriesQuery : IQuery<IReadOnlyList<StoreCategoryDto>>;

public sealed record GetStoreProductQuery(string Slug) : IQuery<StoreProductDto>;

public sealed record ListStoreProductsQuery(
    PageRequest Page,
    string? Sort,
    Guid? CategoryId,
    Guid? BrandId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? Search) : IQuery<PagedResult<StoreProductListItemDto>>;

/// <summary>What visitors see: published products only, in their language, without staff-only fields.</summary>
public sealed class StorefrontQueries(IApplicationDbContext db, ITenantContext tenantContext, CatalogCache cache) :
    IQueryHandler<GetStoreCategoriesQuery, IReadOnlyList<StoreCategoryDto>>,
    IQueryHandler<GetStoreProductQuery, StoreProductDto>,
    IQueryHandler<ListStoreProductsQuery, PagedResult<StoreProductListItemDto>>
{
    private const int MaxSearchLength = 200;

    public async Task<Result<IReadOnlyList<StoreCategoryDto>>> HandleAsync(GetStoreCategoriesQuery query, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var language = CatalogRules.CurrentLanguage();
        var fallback = await GetStoreLanguageAsync(tenantId, cancellationToken);

        var tree = await cache.GetOrCreateAsync(
            tenantId,
            $"categories:{language}",
            async token =>
            {
                var categories = await db.Categories
                    .AsNoTracking()
                    .Include(item => item.Translations)
                    .Where(item => item.IsActive)
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Slug)
                    .ToListAsync(token);

                return BuildTree(categories, null, language, fallback);
            },
            cancellationToken);

        return Result.Success<IReadOnlyList<StoreCategoryDto>>(tree);
    }

    public async Task<Result<StoreProductDto>> HandleAsync(GetStoreProductQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (tenantContext.TenantId is not { } tenantId)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var language = CatalogRules.CurrentLanguage();
        var fallback = await GetStoreLanguageAsync(tenantId, cancellationToken);

        var product = await cache.GetOrCreateAsync(
            tenantId,
            $"product:{query.Slug}:{language}",
            token => new ValueTask<StoreProductDto?>(LoadProductAsync(query.Slug, language, fallback, token)),
            cancellationToken);

        return product is null ? Error.NotFound(ErrorCodes.Catalog.ProductNotFound) : product;
    }

    public async Task<Result<PagedResult<StoreProductListItemDto>>> HandleAsync(ListStoreProductsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (tenantContext.TenantId is not { } tenantId)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var language = CatalogRules.CurrentLanguage();
        var fallback = await GetStoreLanguageAsync(tenantId, cancellationToken);
        var products = db.Products.AsNoTracking().Where(product => product.Status == ProductStatus.Published);

        if (query.CategoryId is { } categoryId)
        {
            products = products.Where(product => product.CategoryId == categoryId);
        }

        if (query.BrandId is { } brandId)
        {
            products = products.Where(product => product.BrandId == brandId);
        }

        if (query.MinPrice is { } minPrice)
        {
            products = products.Where(product => product.Variants.Any(variant => variant.IsActive && variant.Price.Amount >= minPrice));
        }

        if (query.MaxPrice is { } maxPrice)
        {
            products = products.Where(product => product.Variants.Any(variant => variant.IsActive && variant.Price.Amount <= maxPrice));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            term = term.Length > MaxSearchLength ? term[..MaxSearchLength] : term;

            // Escaped, so that % and _ in the user's text are searched for literally.
            var pattern = "%" + term.Replace(@"\", @"\\", StringComparison.Ordinal)
                .Replace("%", @"\%", StringComparison.Ordinal)
                .Replace("_", @"\_", StringComparison.Ordinal) + "%";

            pattern = pattern.ToLowerInvariant();

#pragma warning disable CA1304, CA1311 // This ToLower runs in the database as lower(), not in .NET.
            products = products.Where(product => product.Translations.Any(item => EF.Functions.Like(item.Name.ToLower(), pattern, @"\")));
#pragma warning restore CA1304, CA1311
        }

        var sorted = query.Sort switch
        {
            null or "" or "-publishedAt" => Result.Success(products.OrderByDescending(product => product.PublishedAt).ThenBy(product => product.Id).AsQueryable()),
            "price" => Result.Success(products.OrderBy(product => product.Variants.Where(variant => variant.IsActive).Min(variant => variant.Price.Amount)).ThenBy(product => product.Id).AsQueryable()),
            "-price" => Result.Success(products.OrderByDescending(product => product.Variants.Where(variant => variant.IsActive).Min(variant => variant.Price.Amount)).ThenBy(product => product.Id).AsQueryable()),
            "publishedAt" => Result.Success(products.OrderBy(product => product.PublishedAt).ThenBy(product => product.Id).AsQueryable()),
            _ => Result.Failure<IQueryable<Product>>(Error.Validation(ErrorCodes.Common.InvalidSort)),
        };

        if (sorted.IsFailure)
        {
            return sorted.Error;
        }

        var total = await products.LongCountAsync(cancellationToken);

        var items = await sorted.Value
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .Select(product => new StoreProductListItemDto(
                product.Id,
                product.Slug,
                product.Translations.Where(item => item.Language == language).Select(item => item.Name).FirstOrDefault()
                    ?? product.Translations.Where(item => item.Language == fallback).Select(item => item.Name).FirstOrDefault()
                    ?? product.Translations.OrderBy(item => item.Language).Select(item => item.Name).FirstOrDefault()
                    ?? string.Empty,
                product.Variants.Where(variant => variant.IsActive).Min(variant => (decimal?)variant.Price.Amount),
                product.Variants.Where(variant => variant.IsActive).OrderBy(variant => variant.Price.Amount).Select(variant => variant.Price.Currency).FirstOrDefault(),
                product.Variants.Where(variant => variant.IsActive).OrderBy(variant => variant.Price.Amount).Select(variant => variant.CompareAtAmount).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedResult<StoreProductListItemDto>(items, query.Page.Page, query.Page.PageSize, total);
    }

    private Task<string> GetStoreLanguageAsync(Guid tenantId, CancellationToken cancellationToken) =>
        db.Tenants.AsNoTracking().Where(tenant => tenant.Id == tenantId).Select(tenant => tenant.DefaultLanguage).FirstAsync(cancellationToken);

    private static List<StoreCategoryDto> BuildTree(List<Category> categories, Guid? parentId, string language, string fallback) =>
        categories
            .Where(item => item.ParentId == parentId)
            .Select(item =>
            {
                var translation = CatalogRules.PickTranslation(item.Translations, language, fallback);

                return new StoreCategoryDto(item.Id, item.Slug, translation?.Name ?? item.Slug, translation?.Description, BuildTree(categories, item.Id, language, fallback));
            })
            .ToList();

    private async Task<StoreProductDto?> LoadProductAsync(string slug, string language, string fallback, CancellationToken cancellationToken)
    {
        var product = await db.Products
            .AsNoTracking()
            .Include(item => item.Translations)
            .Include(item => item.Variants)
            .Include(item => item.AttributeValues)
            .FirstOrDefaultAsync(item => item.Slug == slug && item.Status == ProductStatus.Published, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var brandName = product.BrandId is { } brandId
            ? await db.Brands.AsNoTracking().Where(item => item.Id == brandId).Select(item => item.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        var attributeIds = product.AttributeValues.Select(item => item.AttributeDefinitionId).ToList();
        var definitions = await db.AttributeDefinitions
            .AsNoTracking()
            .Include(item => item.Translations)
            .Where(item => attributeIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var translation = CatalogRules.PickTranslation(product.Translations, language, fallback);

        return new StoreProductDto(
            product.Id,
            product.Slug,
            translation?.Name ?? product.Slug,
            translation?.ShortDescription,
            translation?.Description,
            translation?.MetaTitle,
            translation?.MetaDescription,
            product.CategoryId,
            product.BrandId,
            brandName,
            product.Variants
                .Where(item => item.IsActive)
                .OrderBy(item => item.Price.Amount)
                .Select(item => new StoreVariantDto(item.Id, item.Sku, item.Price.Amount, item.Price.Currency, item.CompareAtAmount, item.Options))
                .ToList(),
            product.AttributeValues
                .Where(item => definitions.ContainsKey(item.AttributeDefinitionId))
                .Select(item =>
                {
                    var definition = definitions[item.AttributeDefinitionId];

                    return new StoreAttributeDto(
                        definition.Code,
                        CatalogRules.PickTranslation(definition.Translations, language, fallback)?.Name ?? definition.Code,
                        item.Value);
                })
                .ToList());
    }
}
