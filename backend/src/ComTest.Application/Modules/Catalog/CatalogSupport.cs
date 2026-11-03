using System.Globalization;
using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Catalog.Contracts;
using ComTest.Application.Modules.Media.Contracts;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Domain.Modules.Catalog;
using ComTest.Shared.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace ComTest.Application.Modules.Catalog;

internal static class CatalogRules
{
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";
    public const int MaxSlugLength = 200;
    public const int MaxNameLength = 300;
    public const int MaxDescriptionLength = 100_000;
    public const int MaxTranslations = 3;
    public const int MaxVariants = 200;
    public const decimal MaxAmount = 9_999_999_999m;

    private static readonly string[] Languages = ["tk", "ru", "en"];

    public static IRuleBuilderOptions<T, string> ValidSlug<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(MaxSlugLength).Matches(SlugPattern);

    public static IRuleBuilderOptions<T, string> ValidCatalogLanguage<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(language => Languages.Contains(language));

    public static bool HaveUniqueLanguages<T>(IReadOnlyList<T> items, Func<T, string> language) =>
        items.Select(language).Distinct(StringComparer.Ordinal).Count() == items.Count;

    public static string CurrentLanguage() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    /// <summary>The visitor's language, then the store's own, then any: a page is never left blank.</summary>
    public static T? PickTranslation<T>(IEnumerable<T> translations, string language, string fallback)
        where T : class, ITranslation
    {
        var list = translations.ToList();

        return list.Find(item => item.Language == language)
            ?? list.Find(item => item.Language == fallback)
            ?? list.OrderBy(item => item.Language, StringComparer.Ordinal).FirstOrDefault();
    }
}

internal static class CatalogImages
{
    public static List<ProductImageDto> ToDtos(IEnumerable<ProductImage> images, IReadOnlyDictionary<Guid, MediaUrls> urls) =>
        images
            .OrderBy(image => image.SortOrder)
            .Select(image =>
            {
                var found = urls.GetValueOrDefault(image.MediaAssetId);

                return new ProductImageDto(image.MediaAssetId, image.AltText, found?.Thumbnail, found?.Medium, found?.Large);
            })
            .ToList();
}

/// <summary>
/// Storefront cache. Every key and tag starts with the tenant, so one store can never receive
/// another store's cached data, and a catalog change clears only that store's entries.
/// </summary>
public sealed class CatalogCache(HybridCache cache)
{
    private static readonly HybridCacheEntryOptions Options = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
    };

    public static string Tag(Guid tenantId) => $"tenant:{tenantId:N}:catalog";

    public async Task<T> GetOrCreateAsync<T>(Guid tenantId, string key, Func<CancellationToken, ValueTask<T>> factory, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"tenant:{tenantId:N}:catalog:{key}",
            factory,
            Options,
            [Tag(tenantId)],
            cancellationToken);

    public async Task InvalidateAsync(ITenantContext tenantContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (tenantContext.TenantId is { } tenantId)
        {
            await cache.RemoveByTagAsync(Tag(tenantId), cancellationToken);
        }
    }
}

public sealed class CatalogReader(IApplicationDbContext db, ITenantSettings tenantSettings) : ICatalogReader
{
    public Task<bool> IsMediaInUseAsync(Guid mediaAssetId, CancellationToken cancellationToken) =>
        db.Products.AnyAsync(product => product.Images.Any(image => image.MediaAssetId == mediaAssetId), cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, PurchasableVariant>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
    {
        var language = CatalogRules.CurrentLanguage();
        var fallback = (await tenantSettings.GetCurrentAsync(cancellationToken))?.DefaultLanguage ?? language;

        var rows = await (
            from variant in db.ProductVariants.AsNoTracking()
            join product in db.Products.AsNoTracking().Include(item => item.Translations) on variant.ProductId equals product.Id
            where variantIds.Contains(variant.Id)
            select new { Variant = variant, Product = product })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.Variant.Id,
            row => new PurchasableVariant(
                row.Variant.Id,
                row.Product.Id,
                row.Variant.Sku,
                CatalogRules.PickTranslation(row.Product.Translations, language, fallback)?.Name ?? row.Product.Slug,
                row.Product.Slug,
                row.Variant.Options,
                row.Variant.Price.Amount,
                row.Variant.Price.Currency,
                row.Product.TaxGroup,
                row.Product.Status == ProductStatus.Published && row.Variant.IsActive));
    }
}
