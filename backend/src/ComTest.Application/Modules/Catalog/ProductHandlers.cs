using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Catalog.Contracts;
using ComTest.Domain.Modules.Catalog;
using ComTest.Shared.Domain;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Catalog;

public sealed record ProductTranslationRequest(
    string Language,
    string Name,
    string? ShortDescription = null,
    string? Description = null,
    string? MetaTitle = null,
    string? MetaDescription = null);

public sealed record VariantRequest(
    Guid? Id,
    string Sku,
    decimal Price,
    string Currency,
    string? Barcode = null,
    decimal? CompareAtAmount = null,
    decimal? CostAmount = null,
    IReadOnlyDictionary<string, string>? Options = null,
    bool IsActive = true);

public sealed record AttributeValueRequest(Guid AttributeId, string Value);

public sealed record SaveProductRequest(
    string Slug,
    IReadOnlyList<ProductTranslationRequest> Translations,
    IReadOnlyList<VariantRequest> Variants,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    string? TaxGroup = null,
    IReadOnlyList<AttributeValueRequest>? Attributes = null,
    uint? Version = null);

internal sealed class ProductTranslationRequestValidator : AbstractValidator<ProductTranslationRequest>
{
    public ProductTranslationRequestValidator()
    {
        RuleFor(x => x.Language).ValidCatalogLanguage();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CatalogRules.MaxNameLength);
        RuleFor(x => x.ShortDescription).MaximumLength(1000);
        RuleFor(x => x.Description).MaximumLength(CatalogRules.MaxDescriptionLength);
        RuleFor(x => x.MetaTitle).MaximumLength(200);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
    }
}

internal sealed class VariantRequestValidator : AbstractValidator<VariantRequest>
{
    public VariantRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Barcode).MaximumLength(50);
        RuleFor(x => x.Price).InclusiveBetween(0m, CatalogRules.MaxAmount);
        RuleFor(x => x.Currency).Must(Money.IsSupportedCurrency);
        RuleFor(x => x.CompareAtAmount).InclusiveBetween(0m, CatalogRules.MaxAmount).When(x => x.CompareAtAmount is not null);
        RuleFor(x => x.CostAmount).InclusiveBetween(0m, CatalogRules.MaxAmount).When(x => x.CostAmount is not null);
        RuleFor(x => x.Options).Must(options => options is null || options.Count <= 10);
    }
}

internal sealed class SaveProductRequestValidator : AbstractValidator<SaveProductRequest>
{
    public SaveProductRequestValidator()
    {
        RuleFor(x => x.Slug).ValidSlug();
        RuleFor(x => x.TaxGroup).MaximumLength(50);
        RuleFor(x => x.Translations).NotEmpty().Must(items => items.Count <= CatalogRules.MaxTranslations && CatalogRules.HaveUniqueLanguages(items, item => item.Language));
        RuleForEach(x => x.Translations).SetValidator(new ProductTranslationRequestValidator());
        RuleFor(x => x.Variants).NotEmpty().Must(items => items.Count <= CatalogRules.MaxVariants && items.Select(item => item.Sku).Distinct(StringComparer.Ordinal).Count() == items.Count);
        RuleForEach(x => x.Variants).SetValidator(new VariantRequestValidator());
        RuleFor(x => x.Attributes).Must(items => items is null || items.Count <= 100);
    }
}

public sealed record CreateProductCommand(SaveProductRequest Request) : ICommand<ProductAdminDto>;

public sealed record UpdateProductCommand(Guid Id, SaveProductRequest Request) : ICommand<ProductAdminDto>;

public sealed record DeleteProductCommand(Guid Id) : ICommand<bool>;

public sealed record ChangeProductStatusCommand(Guid Id, ProductStatus Status) : ICommand<ProductAdminDto>;

public sealed record GetProductQuery(Guid Id) : IQuery<ProductAdminDto>;

public sealed record GetPriceHistoryQuery(Guid ProductId) : IQuery<IReadOnlyList<PriceChangeDto>>;

public sealed record ListProductsQuery(PageRequest Page, string? Sort, ProductStatus? Status) : IQuery<PagedResult<ProductAdminListItemDto>>;

public sealed class ProductHandlers(
    IApplicationDbContext db,
    ITenantContext tenantContext,
    IHtmlCleaner htmlCleaner,
    CatalogCache cache,
    TimeProvider timeProvider) :
    ICommandHandler<CreateProductCommand, ProductAdminDto>,
    ICommandHandler<UpdateProductCommand, ProductAdminDto>,
    ICommandHandler<DeleteProductCommand, bool>,
    ICommandHandler<ChangeProductStatusCommand, ProductAdminDto>,
    IQueryHandler<GetProductQuery, ProductAdminDto>,
    IQueryHandler<GetPriceHistoryQuery, IReadOnlyList<PriceChangeDto>>,
    IQueryHandler<ListProductsQuery, PagedResult<ProductAdminListItemDto>>
{
    private static readonly SortMap<Product> AdminSort = new SortMap<Product>()
        .Add("slug", product => product.Slug)
        .Add("status", product => product.Status)
        .Add("createdAt", product => product.CreatedAt);

    public async Task<Result<ProductAdminDto>> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var tenantId = tenantContext.TenantId;
        var maxProducts = await db.Tenants.Where(tenant => tenant.Id == tenantId).Select(tenant => tenant.MaxProducts).FirstOrDefaultAsync(cancellationToken);

        if (await db.Products.CountAsync(cancellationToken) >= maxProducts)
        {
            return Error.Conflict(ErrorCodes.Catalog.ProductLimitReached);
        }

        var problem = await ValidateAsync(null, request, cancellationToken);

        if (problem is not null)
        {
            return problem;
        }

        var product = Product.Create(request.Slug, request.CategoryId, request.BrandId, request.TaxGroup);

        Apply(product, request);

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(product);
    }

    public async Task<Result<ProductAdminDto>> HandleAsync(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var product = await LoadAsync(command.Id, cancellationToken);

        if (product is null)
        {
            return Error.NotFound(ErrorCodes.Catalog.ProductNotFound);
        }

        if (request.Version is { } version && version != product.Version)
        {
            return Error.Conflict(ErrorCodes.Common.ConcurrencyConflict);
        }

        var problem = await ValidateAsync(product.Id, request, cancellationToken);

        if (problem is not null)
        {
            return problem;
        }

        product.UpdateDetails(request.Slug, request.CategoryId, request.BrandId, request.TaxGroup);
        Apply(product, request);

        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(tenantContext, cancellationToken);

        return ToDto(product);
    }

    public async Task<Result<bool>> HandleAsync(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await db.Products.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (product is null)
        {
            return Error.NotFound(ErrorCodes.Catalog.ProductNotFound);
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(tenantContext, cancellationToken);

        return true;
    }

    public async Task<Result<ProductAdminDto>> HandleAsync(ChangeProductStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await LoadAsync(command.Id, cancellationToken);

        if (product is null)
        {
            return Error.NotFound(ErrorCodes.Catalog.ProductNotFound);
        }

        switch (command.Status)
        {
            case ProductStatus.Published when !product.CanPublish():
                return Error.Conflict(ErrorCodes.Catalog.NotReadyToPublish);
            case ProductStatus.Published:
                product.Publish(timeProvider.GetUtcNow());
                break;
            case ProductStatus.Archived:
                product.Archive();
                break;
            default:
                product.MoveToDraft();
                break;
        }

        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(tenantContext, cancellationToken);

        return ToDto(product);
    }

    public async Task<Result<ProductAdminDto>> HandleAsync(GetProductQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var product = await LoadAsync(query.Id, cancellationToken);

        return product is null ? Error.NotFound(ErrorCodes.Catalog.ProductNotFound) : ToDto(product);
    }

    public async Task<Result<IReadOnlyList<PriceChangeDto>>> HandleAsync(GetPriceHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var product = await db.Products
            .AsNoTracking()
            .Include(item => item.PriceChanges)
            .FirstOrDefaultAsync(item => item.Id == query.ProductId, cancellationToken);

        if (product is null)
        {
            return Error.NotFound(ErrorCodes.Catalog.ProductNotFound);
        }

        return Result.Success<IReadOnlyList<PriceChangeDto>>(product.PriceChanges
            .OrderByDescending(change => change.CreatedAt)
            .Select(change => new PriceChangeDto(
                change.ProductVariantId,
                change.OldPrice.Amount,
                change.NewPrice.Amount,
                change.NewPrice.Currency,
                change.CreatedAt,
                change.CreatedBy))
            .ToList());
    }

    public async Task<Result<PagedResult<ProductAdminListItemDto>>> HandleAsync(ListProductsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var products = db.Products.AsNoTracking();

        if (query.Status is { } status)
        {
            products = products.Where(product => product.Status == status);
        }

        var ordered = AdminSort.Apply(products, query.Sort, product => product.Id);

        if (ordered.IsFailure)
        {
            return ordered.Error;
        }

        var total = await products.LongCountAsync(cancellationToken);

        var items = await ordered.Value
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .Select(product => new ProductAdminListItemDto(
                product.Id,
                product.Slug,
                product.Status.ToString(),
                product.Translations.OrderBy(item => item.Language).Select(item => item.Name).FirstOrDefault(),
                product.Variants.Count,
                product.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductAdminListItemDto>(items, query.Page.Page, query.Page.PageSize, total);
    }

    private void Apply(Product product, SaveProductRequest request)
    {
        product.SetTranslations(request.Translations
            .Select(item => new ProductTranslationInput(
                item.Language,
                item.Name,
                item.ShortDescription,
                item.Description is null ? null : htmlCleaner.Clean(item.Description),
                item.MetaTitle,
                item.MetaDescription))
            .ToList());

        product.SetVariants(
            request.Variants
                .Select(item => new VariantInput(
                    item.Id,
                    item.Sku,
                    item.Barcode,
                    Money.Of(item.Price, item.Currency),
                    item.CompareAtAmount,
                    item.CostAmount,
                    item.Options ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    item.IsActive))
                .ToList(),
            timeProvider.GetUtcNow());

        product.SetAttributeValues((request.Attributes ?? []).Select(item => (item.AttributeId, item.Value)));
    }

    private Task<Product?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        db.Products
            .Include(item => item.Translations)
            .Include(item => item.Variants)
            .Include(item => item.AttributeValues)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    private async Task<Error?> ValidateAsync(Guid? id, SaveProductRequest request, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(item => item.Slug == request.Slug && item.Id != id, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Catalog.SlugTaken);
        }

        var skus = request.Variants.Select(item => item.Sku).ToList();

        if (await db.ProductVariants.AnyAsync(item => skus.Contains(item.Sku) && item.ProductId != id, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Catalog.SkuTaken);
        }

        if (request.BrandId is { } brandId && !await db.Brands.AnyAsync(item => item.Id == brandId, cancellationToken))
        {
            return Error.NotFound(ErrorCodes.Catalog.BrandNotFound);
        }

        if (request.CategoryId is { } categoryId && !await db.Categories.AnyAsync(item => item.Id == categoryId, cancellationToken))
        {
            return Error.NotFound(ErrorCodes.Catalog.CategoryNotFound);
        }

        return await ValidateAttributesAsync(request, cancellationToken);
    }

    private async Task<Error?> ValidateAttributesAsync(SaveProductRequest request, CancellationToken cancellationToken)
    {
        var values = request.Attributes ?? [];
        var attributeIds = values.Select(item => item.AttributeId).ToList();

        var definitions = await db.AttributeDefinitions
            .AsNoTracking()
            .Where(item => attributeIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var value in values)
        {
            if (!definitions.TryGetValue(value.AttributeId, out var definition))
            {
                return Error.NotFound(ErrorCodes.Catalog.AttributeNotFound);
            }

            if (!definition.Accepts(value.Value))
            {
                return Error.Validation(ErrorCodes.Catalog.InvalidAttributeValue);
            }
        }

        if (request.CategoryId is { } categoryId)
        {
            var required = await db.CategoryAttributes
                .AsNoTracking()
                .Where(item => item.CategoryId == categoryId && item.IsRequired)
                .Select(item => item.AttributeDefinitionId)
                .ToListAsync(cancellationToken);

            if (required.Exists(attributeId => !attributeIds.Contains(attributeId)))
            {
                return Error.Validation(ErrorCodes.Catalog.RequiredAttributeMissing);
            }
        }

        return null;
    }

    private static ProductAdminDto ToDto(Product product) => new(
        product.Id,
        product.Slug,
        product.Status.ToString(),
        product.CategoryId,
        product.BrandId,
        product.TaxGroup,
        product.Translations
            .Select(item => new ProductTranslationDto(item.Language, item.Name, item.ShortDescription, item.Description, item.MetaTitle, item.MetaDescription))
            .ToList(),
        product.Variants
            .Select(item => new VariantAdminDto(item.Id, item.Sku, item.Barcode, item.Price.Amount, item.Price.Currency, item.CompareAtAmount, item.CostAmount, item.Options, item.IsActive))
            .ToList(),
        product.AttributeValues.Select(item => new AttributeValueDto(item.AttributeDefinitionId, item.Value)).ToList(),
        product.Version);
}
