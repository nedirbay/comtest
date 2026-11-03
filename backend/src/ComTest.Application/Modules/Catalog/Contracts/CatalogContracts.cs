namespace ComTest.Application.Modules.Catalog.Contracts;

public sealed record TranslationDto(string Language, string Name, string? Description);

public sealed record CategoryDto(Guid Id, Guid? ParentId, string Slug, int SortOrder, bool IsActive, IReadOnlyList<TranslationDto> Translations, uint Version);

public sealed record BrandDto(Guid Id, string Name, string Slug, string? LogoUrl, uint Version);

public sealed record AttributeDto(Guid Id, string Code, string DataType, IReadOnlyList<string> Options, IReadOnlyList<TranslationDto> Translations);

public sealed record ProductTranslationDto(string Language, string Name, string? ShortDescription, string? Description, string? MetaTitle, string? MetaDescription);

/// <summary>Staff view of a variant. CostAmount must never appear in a storefront response.</summary>
public sealed record VariantAdminDto(
    Guid Id,
    string Sku,
    string? Barcode,
    decimal Price,
    string Currency,
    decimal? CompareAtAmount,
    decimal? CostAmount,
    IReadOnlyDictionary<string, string> Options,
    bool IsActive);

public sealed record AttributeValueDto(Guid AttributeId, string Value);

public sealed record ProductAdminDto(
    Guid Id,
    string Slug,
    string Status,
    Guid? CategoryId,
    Guid? BrandId,
    string? TaxGroup,
    IReadOnlyList<ProductTranslationDto> Translations,
    IReadOnlyList<VariantAdminDto> Variants,
    IReadOnlyList<AttributeValueDto> Attributes,
    uint Version);

public sealed record ProductAdminListItemDto(Guid Id, string Slug, string Status, string? Name, int VariantCount, DateTimeOffset CreatedAt);

public sealed record PriceChangeDto(Guid VariantId, decimal OldAmount, decimal NewAmount, string Currency, DateTimeOffset ChangedAt, Guid? ChangedBy);

// Storefront read models: only what a visitor may see, in one language.
public sealed record StoreCategoryDto(Guid Id, string Slug, string Name, string? Description, IReadOnlyList<StoreCategoryDto> Children);

public sealed record StoreProductListItemDto(Guid Id, string Slug, string Name, decimal? MinPrice, string? Currency, decimal? CompareAtAmount);

public sealed record StoreVariantDto(Guid Id, string Sku, decimal Price, string Currency, decimal? CompareAtAmount, IReadOnlyDictionary<string, string> Options);

public sealed record StoreAttributeDto(string Code, string Name, string Value);

public sealed record StoreProductDto(
    Guid Id,
    string Slug,
    string Name,
    string? ShortDescription,
    string? Description,
    string? MetaTitle,
    string? MetaDescription,
    Guid? CategoryId,
    Guid? BrandId,
    string? BrandName,
    IReadOnlyList<StoreVariantDto> Variants,
    IReadOnlyList<StoreAttributeDto> Attributes);
