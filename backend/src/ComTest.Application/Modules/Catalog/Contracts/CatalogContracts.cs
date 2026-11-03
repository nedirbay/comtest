using ComTest.Domain.Modules.Catalog;

namespace ComTest.Application.Modules.Catalog.Contracts;

public sealed record TranslationDto(string Language, string Name, string? Description);

public sealed record CategoryDto(Guid Id, Guid? ParentId, string Slug, int SortOrder, bool IsActive, IReadOnlyList<TranslationDto> Translations, uint Version);

public sealed record BrandDto(Guid Id, string Name, string Slug, string? LogoUrl, uint Version);

/// <summary>A brand as visitors see it, without the fields staff need for editing.</summary>
public sealed record StoreBrandDto(Guid Id, string Name, string Slug, string? LogoUrl);

public sealed record AttributeDto(Guid Id, string Code, AttributeDataType DataType, IReadOnlyList<string> Options, IReadOnlyList<TranslationDto> Translations);

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

public sealed record ProductImageDto(Guid MediaAssetId, string? AltText, Uri? Thumbnail, Uri? Medium, Uri? Large);

public sealed record ProductAdminDto(
    Guid Id,
    string Slug,
    ProductStatus Status,
    Guid? CategoryId,
    Guid? BrandId,
    string? TaxGroup,
    IReadOnlyList<ProductTranslationDto> Translations,
    IReadOnlyList<VariantAdminDto> Variants,
    IReadOnlyList<AttributeValueDto> Attributes,
    IReadOnlyList<ProductImageDto> Images,
    uint Version);

public sealed record ProductAdminListItemDto(Guid Id, string Slug, ProductStatus Status, string? Name, int VariantCount, DateTimeOffset CreatedAt);

public sealed record PriceChangeDto(Guid VariantId, decimal OldAmount, decimal NewAmount, string Currency, DateTimeOffset ChangedAt, Guid? ChangedBy);

// Storefront read models: only what a visitor may see, in one language.
public sealed record StoreCategoryDto(Guid Id, string Slug, string Name, string? Description, IReadOnlyList<StoreCategoryDto> Children);

public sealed record StoreProductListItemDto(Guid Id, string Slug, string Name, decimal? MinPrice, string? Currency, decimal? CompareAtAmount, bool InStock, Uri? ImageUrl);

public sealed record StoreVariantDto(Guid Id, string Sku, decimal Price, string Currency, decimal? CompareAtAmount, IReadOnlyDictionary<string, string> Options, bool InStock);

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
    IReadOnlyList<StoreAttributeDto> Attributes,
    IReadOnlyList<ProductImageDto> Images);

/// <summary>A variant as other modules need it for selling. The price here is the only price that counts.</summary>
public sealed record PurchasableVariant(
    Guid VariantId,
    Guid ProductId,
    string Sku,
    string ProductName,
    string ProductSlug,
    IReadOnlyDictionary<string, string> Options,
    decimal Price,
    string Currency,
    string? TaxGroup,
    bool IsPurchasable);

public interface ICatalogReader
{
    Task<bool> IsMediaInUseAsync(Guid mediaAssetId, CancellationToken cancellationToken);

    /// <summary>Finds variants in the current store. Others are simply absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, PurchasableVariant>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken);
}
