using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Catalog.Contracts;
using ComTest.Domain.Modules.Catalog;
using ComTest.Shared.Messaging;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Catalog;

public sealed record TranslationRequest(string Language, string Name, string? Description = null);

internal sealed class TranslationRequestValidator : AbstractValidator<TranslationRequest>
{
    public TranslationRequestValidator()
    {
        RuleFor(x => x.Language).ValidCatalogLanguage();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CatalogRules.MaxNameLength);
        RuleFor(x => x.Description).MaximumLength(5000);
    }
}

public sealed record SaveCategoryRequest(
    Guid? ParentId,
    string Slug,
    int SortOrder,
    IReadOnlyList<TranslationRequest> Translations,
    bool IsActive = true,
    uint? Version = null);

internal sealed class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(x => x.Slug).ValidSlug();
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 100_000);
        RuleFor(x => x.Translations).NotEmpty().Must(items => items.Count <= CatalogRules.MaxTranslations && CatalogRules.HaveUniqueLanguages(items, item => item.Language));
        RuleForEach(x => x.Translations).SetValidator(new TranslationRequestValidator());
    }
}

public sealed record CreateCategoryCommand(SaveCategoryRequest Request) : ICommand<CategoryDto>;

public sealed record UpdateCategoryCommand(Guid Id, SaveCategoryRequest Request) : ICommand<CategoryDto>;

public sealed record DeleteCategoryCommand(Guid Id) : ICommand<bool>;

public sealed record ListCategoriesQuery : IQuery<IReadOnlyList<CategoryDto>>;

internal static class CategoryMappings
{
    public static CategoryDto ToDto(this Category category) => new(
        category.Id,
        category.ParentId,
        category.Slug,
        category.SortOrder,
        category.IsActive,
        category.Translations.Select(item => new TranslationDto(item.Language, item.Name, item.Description)).ToList(),
        category.Version);

    public static IEnumerable<TranslationInput> ToInputs(this IEnumerable<TranslationRequest> items) =>
        items.Select(item => new TranslationInput(item.Language, item.Name, item.Description));
}

public sealed class CategoryHandlers(IApplicationDbContext db, ITenantContext tenantContext, CatalogCache cache) :
    ICommandHandler<CreateCategoryCommand, CategoryDto>,
    ICommandHandler<UpdateCategoryCommand, CategoryDto>,
    ICommandHandler<DeleteCategoryCommand, bool>,
    IQueryHandler<ListCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<Result<CategoryDto>> HandleAsync(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var problem = await ValidateAsync(null, request, cancellationToken);

        if (problem is not null)
        {
            return problem;
        }

        var category = Category.Create(request.ParentId, request.Slug, request.SortOrder, request.Translations.ToInputs());

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(tenantContext, cancellationToken);

        return category.ToDto();
    }

    public async Task<Result<CategoryDto>> HandleAsync(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var category = await db.Categories.Include(item => item.Translations).FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (category is null)
        {
            return Error.NotFound(ErrorCodes.Catalog.CategoryNotFound);
        }

        if (request.Version is { } version && version != category.Version)
        {
            return Error.Conflict(ErrorCodes.Common.ConcurrencyConflict);
        }

        var problem = await ValidateAsync(category.Id, request, cancellationToken);

        if (problem is not null)
        {
            return problem;
        }

        category.Update(request.ParentId, request.Slug, request.SortOrder, request.IsActive, request.Translations.ToInputs());

        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(tenantContext, cancellationToken);

        return category.ToDto();
    }

    public async Task<Result<bool>> HandleAsync(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await db.Categories.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (category is null)
        {
            return Error.NotFound(ErrorCodes.Catalog.CategoryNotFound);
        }

        if (await db.Categories.AnyAsync(item => item.ParentId == command.Id, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Catalog.CategoryHasChildren);
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(tenantContext, cancellationToken);

        return true;
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> HandleAsync(ListCategoriesQuery query, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Include(item => item.Translations)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Slug)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CategoryDto>>(categories.Select(item => item.ToDto()).ToList());
    }

    private async Task<Error?> ValidateAsync(Guid? id, SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        if (await db.Categories.AnyAsync(item => item.Slug == request.Slug && item.Id != id, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Catalog.SlugTaken);
        }

        if (request.ParentId is not { } parentId)
        {
            return null;
        }

        // Walking up from the new parent must never reach this category, or the tree would loop.
        var parents = await db.Categories.AsNoTracking().Select(item => new { item.Id, item.ParentId }).ToDictionaryAsync(item => item.Id, item => item.ParentId, cancellationToken);

        if (!parents.ContainsKey(parentId))
        {
            return Error.NotFound(ErrorCodes.Catalog.CategoryNotFound);
        }

        for (Guid? current = parentId; current is { } node; current = parents.GetValueOrDefault(node))
        {
            if (node == id)
            {
                return Error.Conflict(ErrorCodes.Catalog.CategoryCycle);
            }
        }

        return null;
    }
}

public sealed record SaveBrandRequest(string Name, string Slug, string? LogoUrl = null, uint? Version = null);

internal sealed class SaveBrandRequestValidator : AbstractValidator<SaveBrandRequest>
{
    public SaveBrandRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(CatalogRules.MaxNameLength);
        RuleFor(x => x.Slug).ValidSlug();
        RuleFor(x => x.LogoUrl)
            .MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            .When(x => x.LogoUrl is not null);
    }
}

public sealed record CreateBrandCommand(SaveBrandRequest Request) : ICommand<BrandDto>;

public sealed record UpdateBrandCommand(Guid Id, SaveBrandRequest Request) : ICommand<BrandDto>;

public sealed record DeleteBrandCommand(Guid Id) : ICommand<bool>;

public sealed record ListBrandsQuery : IQuery<IReadOnlyList<BrandDto>>;

public sealed class BrandHandlers(IApplicationDbContext db, ITenantContext tenantContext, CatalogCache cache) :
    ICommandHandler<CreateBrandCommand, BrandDto>,
    ICommandHandler<UpdateBrandCommand, BrandDto>,
    ICommandHandler<DeleteBrandCommand, bool>,
    IQueryHandler<ListBrandsQuery, IReadOnlyList<BrandDto>>
{
    public async Task<Result<BrandDto>> HandleAsync(CreateBrandCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        if (await db.Brands.AnyAsync(item => item.Slug == request.Slug, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Catalog.SlugTaken);
        }

        var brand = Brand.Create(request.Name, request.Slug, request.LogoUrl);

        db.Brands.Add(brand);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(brand);
    }

    public async Task<Result<BrandDto>> HandleAsync(UpdateBrandCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var brand = await db.Brands.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (brand is null)
        {
            return Error.NotFound(ErrorCodes.Catalog.BrandNotFound);
        }

        if (request.Version is { } version && version != brand.Version)
        {
            return Error.Conflict(ErrorCodes.Common.ConcurrencyConflict);
        }

        if (await db.Brands.AnyAsync(item => item.Slug == request.Slug && item.Id != command.Id, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Catalog.SlugTaken);
        }

        brand.Update(request.Name, request.Slug, request.LogoUrl);

        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(tenantContext, cancellationToken);

        return ToDto(brand);
    }

    public async Task<Result<bool>> HandleAsync(DeleteBrandCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var brand = await db.Brands.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (brand is null)
        {
            return Error.NotFound(ErrorCodes.Catalog.BrandNotFound);
        }

        db.Brands.Remove(brand);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(tenantContext, cancellationToken);

        return true;
    }

    public async Task<Result<IReadOnlyList<BrandDto>>> HandleAsync(ListBrandsQuery query, CancellationToken cancellationToken)
    {
        var brands = await db.Brands
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new BrandDto(item.Id, item.Name, item.Slug, item.LogoUrl, item.Version))
            .ToListAsync(cancellationToken);

        return brands;
    }

    private static BrandDto ToDto(Brand brand) => new(brand.Id, brand.Name, brand.Slug, brand.LogoUrl, brand.Version);
}

public sealed record CreateAttributeRequest(
    string Code,
    AttributeDataType DataType,
    IReadOnlyList<string>? Options,
    IReadOnlyList<TranslationRequest> Translations);

internal sealed class CreateAttributeRequestValidator : AbstractValidator<CreateAttributeRequest>
{
    public CreateAttributeRequestValidator()
    {
        RuleFor(x => x.Code).ValidSlug();
        RuleFor(x => x.DataType).IsInEnum();
        RuleFor(x => x.Options).NotEmpty().When(x => x.DataType == AttributeDataType.Select);
        RuleFor(x => x.Options).Must(options => options is null || options.Count <= 100);
        RuleFor(x => x.Translations).NotEmpty().Must(items => items.Count <= CatalogRules.MaxTranslations && CatalogRules.HaveUniqueLanguages(items, item => item.Language));
        RuleForEach(x => x.Translations).SetValidator(new TranslationRequestValidator());
    }
}

public sealed record CreateAttributeCommand(CreateAttributeRequest Request) : ICommand<AttributeDto>;

public sealed record ListAttributesQuery : IQuery<IReadOnlyList<AttributeDto>>;

public sealed record AssignCategoryAttributeRequest(Guid AttributeId, bool IsRequired);

public sealed record AssignCategoryAttributeCommand(Guid CategoryId, AssignCategoryAttributeRequest Request) : ICommand<bool>;

public sealed class AttributeHandlers(IApplicationDbContext db) :
    ICommandHandler<CreateAttributeCommand, AttributeDto>,
    IQueryHandler<ListAttributesQuery, IReadOnlyList<AttributeDto>>,
    ICommandHandler<AssignCategoryAttributeCommand, bool>
{
    public async Task<Result<AttributeDto>> HandleAsync(CreateAttributeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        if (await db.AttributeDefinitions.AnyAsync(item => item.Code == request.Code, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Catalog.CodeTaken);
        }

        var attribute = AttributeDefinition.Create(request.Code, request.DataType, request.Options ?? [], request.Translations.ToInputs());

        db.AttributeDefinitions.Add(attribute);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(attribute);
    }

    public async Task<Result<IReadOnlyList<AttributeDto>>> HandleAsync(ListAttributesQuery query, CancellationToken cancellationToken)
    {
        var attributes = await db.AttributeDefinitions
            .AsNoTracking()
            .Include(item => item.Translations)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AttributeDto>>(attributes.Select(ToDto).ToList());
    }

    public async Task<Result<bool>> HandleAsync(AssignCategoryAttributeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await db.Categories.AnyAsync(item => item.Id == command.CategoryId, cancellationToken))
        {
            return Error.NotFound(ErrorCodes.Catalog.CategoryNotFound);
        }

        if (!await db.AttributeDefinitions.AnyAsync(item => item.Id == command.Request.AttributeId, cancellationToken))
        {
            return Error.NotFound(ErrorCodes.Catalog.AttributeNotFound);
        }

        var exists = await db.CategoryAttributes.AnyAsync(
            item => item.CategoryId == command.CategoryId && item.AttributeDefinitionId == command.Request.AttributeId,
            cancellationToken);

        if (!exists)
        {
            db.CategoryAttributes.Add(CategoryAttributeLink.Create(command.CategoryId, command.Request.AttributeId, command.Request.IsRequired));
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static AttributeDto ToDto(AttributeDefinition attribute) => new(
        attribute.Id,
        attribute.Code,
        attribute.DataType,
        attribute.Options,
        attribute.Translations.Select(item => new TranslationDto(item.Language, item.Name, null)).ToList());
}
