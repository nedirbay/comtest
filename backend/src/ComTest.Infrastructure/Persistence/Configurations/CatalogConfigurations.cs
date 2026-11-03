using System.Text.Json;
using ComTest.Domain.Modules.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComTest.Infrastructure.Persistence.Configurations;

internal static class CatalogMapping
{
    /// <summary>A slug is free again once its owner is deleted, since deleted rows are only hidden.</summary>
    public const string NotDeleted = "NOT is_deleted";

    public static void ConfigureAsJson<T>(this PropertyBuilder<T> property, Func<T> empty)
        where T : class
    {
        property
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonSerializerOptions.Default),
                value => JsonSerializer.Deserialize<T>(value, JsonSerializerOptions.Default) ?? empty(),
                new ValueComparer<T>(
                    (left, right) => JsonSerializer.Serialize(left, JsonSerializerOptions.Default) == JsonSerializer.Serialize(right, JsonSerializerOptions.Default),
                    value => JsonSerializer.Serialize(value, JsonSerializerOptions.Default).GetHashCode(StringComparison.Ordinal),
                    value => value));
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.Property(item => item.Slug).HasMaxLength(200);
        builder.HasIndex(item => new { item.TenantId, item.Slug }).IsUnique().HasFilter(CatalogMapping.NotDeleted);
        builder.HasIndex(item => new { item.TenantId, item.ParentId });
        builder.HasOne<Category>().WithMany().HasForeignKey(item => item.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Translations).WithOne().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CategoryTranslationConfiguration : IEntityTypeConfiguration<CategoryTranslation>
{
    public void Configure(EntityTypeBuilder<CategoryTranslation> builder)
    {
        builder.ToTable("category_translations");
        builder.Property(item => item.Language).HasMaxLength(5);
        builder.Property(item => item.Name).HasMaxLength(300);
        builder.Property(item => item.Description).HasMaxLength(5000);
        builder.HasIndex(item => new { item.CategoryId, item.Language }).IsUnique();
    }
}

internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands");
        builder.Property(item => item.Name).HasMaxLength(300);
        builder.Property(item => item.Slug).HasMaxLength(200);
        builder.Property(item => item.LogoUrl).HasMaxLength(500);
        builder.HasIndex(item => new { item.TenantId, item.Slug }).IsUnique().HasFilter(CatalogMapping.NotDeleted);
    }
}

internal sealed class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeDefinition> builder)
    {
        builder.ToTable("attribute_definitions");
        builder.Property(item => item.Code).HasMaxLength(200);
        builder.Property(item => item.DataType).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Options).ConfigureAsJson<IReadOnlyList<string>>(() => []);
        builder.HasIndex(item => new { item.TenantId, item.Code }).IsUnique().HasFilter(CatalogMapping.NotDeleted);
        builder.HasMany(item => item.Translations).WithOne().HasForeignKey(item => item.AttributeDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AttributeTranslationConfiguration : IEntityTypeConfiguration<AttributeTranslation>
{
    public void Configure(EntityTypeBuilder<AttributeTranslation> builder)
    {
        builder.ToTable("attribute_translations");
        builder.Property(item => item.Language).HasMaxLength(5);
        builder.Property(item => item.Name).HasMaxLength(300);
        builder.HasIndex(item => new { item.AttributeDefinitionId, item.Language }).IsUnique();
    }
}

internal sealed class CategoryAttributeLinkConfiguration : IEntityTypeConfiguration<CategoryAttributeLink>
{
    public void Configure(EntityTypeBuilder<CategoryAttributeLink> builder)
    {
        builder.ToTable("category_attributes");
        builder.HasIndex(item => new { item.CategoryId, item.AttributeDefinitionId }).IsUnique();
        builder.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AttributeDefinition>().WithMany().HasForeignKey(item => item.AttributeDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.Property(item => item.Slug).HasMaxLength(200);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.TaxGroup).HasMaxLength(50);
        builder.HasIndex(item => new { item.TenantId, item.Slug }).IsUnique().HasFilter(CatalogMapping.NotDeleted);
        builder.HasIndex(item => new { item.TenantId, item.Status, item.PublishedAt });
        builder.HasIndex(item => new { item.TenantId, item.CategoryId });
        builder.HasIndex(item => new { item.TenantId, item.BrandId });
        builder.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Brand>().WithMany().HasForeignKey(item => item.BrandId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(item => item.Translations).WithOne().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.Variants).WithOne().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.AttributeValues).WithOne().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.PriceChanges).WithOne().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProductTranslationConfiguration : IEntityTypeConfiguration<ProductTranslation>
{
    public void Configure(EntityTypeBuilder<ProductTranslation> builder)
    {
        builder.ToTable("product_translations");
        builder.Property(item => item.Language).HasMaxLength(5);
        builder.Property(item => item.Name).HasMaxLength(300);
        builder.Property(item => item.ShortDescription).HasMaxLength(1000);
        builder.Property(item => item.MetaTitle).HasMaxLength(200);
        builder.Property(item => item.MetaDescription).HasMaxLength(500);
        builder.HasIndex(item => new { item.ProductId, item.Language }).IsUnique();
    }
}

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");
        builder.Property(item => item.Sku).HasMaxLength(100);
        builder.Property(item => item.Barcode).HasMaxLength(50);
        builder.MapMoney(item => item.Price);
        builder.Property(item => item.CompareAtAmount).HasPrecision(18, 2);
        builder.Property(item => item.CostAmount).HasPrecision(18, 2);
        builder.Property(item => item.Options).ConfigureAsJson<IReadOnlyDictionary<string, string>>(() => new Dictionary<string, string>());
        builder.HasIndex(item => new { item.TenantId, item.Sku }).IsUnique();
        builder.HasIndex(item => item.ProductId);
    }
}

internal sealed class ProductAttributeValueConfiguration : IEntityTypeConfiguration<ProductAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProductAttributeValue> builder)
    {
        builder.ToTable("product_attribute_values");
        builder.Property(item => item.Value).HasMaxLength(500);
        builder.HasIndex(item => new { item.ProductId, item.AttributeDefinitionId }).IsUnique();
        builder.HasOne<AttributeDefinition>().WithMany().HasForeignKey(item => item.AttributeDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PriceChangeConfiguration : IEntityTypeConfiguration<PriceChange>
{
    public void Configure(EntityTypeBuilder<PriceChange> builder)
    {
        builder.ToTable("price_changes");
        builder.MapMoney(item => item.OldPrice);
        builder.MapMoney(item => item.NewPrice);
        builder.HasIndex(item => new { item.ProductId, item.CreatedAt });
    }
}
