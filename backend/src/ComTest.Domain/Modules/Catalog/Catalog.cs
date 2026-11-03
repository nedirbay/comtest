using ComTest.Shared.Domain;

namespace ComTest.Domain.Modules.Catalog;

public enum ProductStatus
{
    Draft,
    Published,
    Archived,
}

public enum AttributeDataType
{
    Text,
    Number,
    Boolean,
    Select,
}

public sealed record ProductPublished(Guid ProductId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record VariantPriceChanged(Guid ProductId, Guid VariantId, decimal OldAmount, decimal NewAmount, string Currency, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record TranslationInput(string Language, string Name, string? Description = null);

public sealed class CategoryTranslation : TenantOwnedEntity, ITranslation
{
    private CategoryTranslation()
    {
    }

    public CategoryTranslation(string language, string name, string? description)
    {
        Language = language;
        Name = name;
        Description = description;
    }

    public Guid CategoryId { get; private set; }

    public string Language { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }
}

public sealed class Category : SoftDeletableEntity
{
    private readonly List<CategoryTranslation> _translations = [];

    private Category()
    {
    }

    public Guid? ParentId { get; private set; }

    public string Slug { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<CategoryTranslation> Translations => _translations;

    public static Category Create(Guid? parentId, string slug, int sortOrder, IEnumerable<TranslationInput> translations)
    {
        var category = new Category { ParentId = parentId, Slug = slug, SortOrder = sortOrder, IsActive = true };
        category.SetTranslations(translations);

        return category;
    }

    public void Update(Guid? parentId, string slug, int sortOrder, bool isActive, IEnumerable<TranslationInput> translations)
    {
        ParentId = parentId;
        Slug = slug;
        SortOrder = sortOrder;
        IsActive = isActive;
        SetTranslations(translations);
    }

    private void SetTranslations(IEnumerable<TranslationInput> translations)
    {
        var inputs = translations.ToList();

        _translations.RemoveAll(existing => inputs.TrueForAll(input => input.Language != existing.Language));

        foreach (var input in inputs)
        {
            var existing = _translations.Find(item => item.Language == input.Language);

            if (existing is null)
            {
                _translations.Add(new CategoryTranslation(input.Language, input.Name, input.Description));
            }
            else
            {
                existing.Update(input.Name, input.Description);
            }
        }
    }
}

public sealed class Brand : SoftDeletableEntity
{
    private Brand()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? LogoUrl { get; private set; }

    public static Brand Create(string name, string slug, string? logoUrl) => new() { Name = name, Slug = slug, LogoUrl = logoUrl };

    public void Update(string name, string slug, string? logoUrl)
    {
        Name = name;
        Slug = slug;
        LogoUrl = logoUrl;
    }
}

public sealed class AttributeTranslation : TenantOwnedEntity, ITranslation
{
    private AttributeTranslation()
    {
    }

    public AttributeTranslation(string language, string name)
    {
        Language = language;
        Name = name;
    }

    public Guid AttributeDefinitionId { get; private set; }

    public string Language { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public void Rename(string name) => Name = name;
}

/// <summary>A characteristic products can have, such as memory size. Categories choose which apply.</summary>
public sealed class AttributeDefinition : SoftDeletableEntity
{
    private readonly List<AttributeTranslation> _translations = [];

    private AttributeDefinition()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public AttributeDataType DataType { get; private set; }

    /// <summary>Allowed values when <see cref="DataType"/> is Select.</summary>
    public IReadOnlyList<string> Options { get; private set; } = [];

    public IReadOnlyCollection<AttributeTranslation> Translations => _translations;

    public static AttributeDefinition Create(string code, AttributeDataType dataType, IReadOnlyList<string> options, IEnumerable<TranslationInput> translations)
    {
        var attribute = new AttributeDefinition { Code = code, DataType = dataType, Options = options };

        foreach (var translation in translations)
        {
            attribute._translations.Add(new AttributeTranslation(translation.Language, translation.Name));
        }

        return attribute;
    }

    public bool Accepts(string value) => DataType switch
    {
        AttributeDataType.Number => decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _),
        AttributeDataType.Boolean => value is "true" or "false",
        AttributeDataType.Select => Options.Contains(value),
        _ => value.Length > 0,
    };
}

/// <summary>Says that products in a category have this attribute.</summary>
public sealed class CategoryAttributeLink : TenantOwnedEntity
{
    private CategoryAttributeLink()
    {
    }

    public Guid CategoryId { get; private set; }

    public Guid AttributeDefinitionId { get; private set; }

    public bool IsRequired { get; private set; }

    public static CategoryAttributeLink Create(Guid categoryId, Guid attributeId, bool isRequired) =>
        new() { CategoryId = categoryId, AttributeDefinitionId = attributeId, IsRequired = isRequired };
}

public sealed record ProductTranslationInput(
    string Language,
    string Name,
    string? ShortDescription = null,
    string? Description = null,
    string? MetaTitle = null,
    string? MetaDescription = null);

public sealed class ProductTranslation : TenantOwnedEntity, ITranslation
{
    private ProductTranslation()
    {
    }

    public ProductTranslation(ProductTranslationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Language = input.Language;
        Update(input);
    }

    public Guid ProductId { get; private set; }

    public string Language { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? ShortDescription { get; private set; }

    public string? Description { get; private set; }

    public string? MetaTitle { get; private set; }

    public string? MetaDescription { get; private set; }

    public void Update(ProductTranslationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Name = input.Name;
        ShortDescription = input.ShortDescription;
        Description = input.Description;
        MetaTitle = input.MetaTitle;
        MetaDescription = input.MetaDescription;
    }
}

public sealed class ProductAttributeValue : TenantOwnedEntity
{
    private ProductAttributeValue()
    {
    }

    public ProductAttributeValue(Guid attributeId, string value)
    {
        AttributeDefinitionId = attributeId;
        Value = value;
    }

    public Guid ProductId { get; private set; }

    public Guid AttributeDefinitionId { get; private set; }

    public string Value { get; private set; } = string.Empty;
}

public sealed record VariantInput(
    Guid? Id,
    string Sku,
    string? Barcode,
    Money Price,
    decimal? CompareAtAmount,
    decimal? CostAmount,
    IReadOnlyDictionary<string, string> Options,
    bool IsActive);

public sealed class ProductVariant : TenantOwnedEntity
{
    private ProductVariant()
    {
    }

    internal ProductVariant(VariantInput input)
    {
        Sku = input.Sku;
        Price = input.Price;
        Apply(input);
    }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string? Barcode { get; private set; }

    public Money Price { get; private set; } = null!;

    /// <summary>The old price shown crossed out. Same currency as <see cref="Price"/>.</summary>
    public decimal? CompareAtAmount { get; private set; }

    /// <summary>What the variant costs the firm. Staff-only: no storefront response may contain it.</summary>
    public decimal? CostAmount { get; private set; }

    /// <summary>Choices that make this variant, such as colour and size.</summary>
    public IReadOnlyDictionary<string, string> Options { get; private set; } = new Dictionary<string, string>();

    public bool IsActive { get; private set; }

    internal void Update(VariantInput input)
    {
        Sku = input.Sku;
        Price = input.Price;
        Apply(input);
    }

    private void Apply(VariantInput input)
    {
        Barcode = input.Barcode;
        CompareAtAmount = input.CompareAtAmount;
        CostAmount = input.CostAmount;
        Options = input.Options;
        IsActive = input.IsActive;
    }
}

/// <summary>One record per price change, for disputes and reports.</summary>
public sealed class PriceChange : TenantOwnedEntity
{
    private PriceChange()
    {
    }

    public PriceChange(Guid variantId, Money oldPrice, Money newPrice)
    {
        ProductVariantId = variantId;
        OldPrice = oldPrice;
        NewPrice = newPrice;
    }

    public Guid ProductId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public Money OldPrice { get; private set; } = null!;

    public Money NewPrice { get; private set; } = null!;
}

public sealed class Product : SoftDeletableEntity
{
    private readonly List<ProductTranslation> _translations = [];
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductAttributeValue> _attributeValues = [];
    private readonly List<PriceChange> _priceChanges = [];

    private Product()
    {
    }

    public string Slug { get; private set; } = string.Empty;

    public ProductStatus Status { get; private set; }

    public Guid? CategoryId { get; private set; }

    public Guid? BrandId { get; private set; }

    public string? TaxGroup { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Bumped on every edit, so that a change to a child row also changes this row's version.</summary>
    public int Revision { get; private set; }

    public IReadOnlyCollection<ProductTranslation> Translations => _translations;

    public IReadOnlyCollection<ProductVariant> Variants => _variants;

    public IReadOnlyCollection<ProductAttributeValue> AttributeValues => _attributeValues;

    public IReadOnlyCollection<PriceChange> PriceChanges => _priceChanges;

    public static Product Create(string slug, Guid? categoryId, Guid? brandId, string? taxGroup) => new()
    {
        Slug = slug,
        CategoryId = categoryId,
        BrandId = brandId,
        TaxGroup = taxGroup,
        Status = ProductStatus.Draft,
    };

    public void UpdateDetails(string slug, Guid? categoryId, Guid? brandId, string? taxGroup)
    {
        Slug = slug;
        CategoryId = categoryId;
        BrandId = brandId;
        TaxGroup = taxGroup;
        Revision++;
    }

    public void SetTranslations(IReadOnlyList<ProductTranslationInput> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        _translations.RemoveAll(existing => !translations.Any(input => input.Language == existing.Language));

        foreach (var input in translations)
        {
            var existing = _translations.Find(item => item.Language == input.Language);

            if (existing is null)
            {
                _translations.Add(new ProductTranslation(input));
            }
            else
            {
                existing.Update(input);
            }
        }
    }

    public void SetAttributeValues(IEnumerable<(Guid AttributeId, string Value)> values)
    {
        _attributeValues.Clear();
        _attributeValues.AddRange(values.Select(value => new ProductAttributeValue(value.AttributeId, value.Value)));
    }

    /// <summary>Adds, updates and removes variants, recording every price change.</summary>
    public void SetVariants(IReadOnlyList<VariantInput> variants, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(variants);

        _variants.RemoveAll(existing => !variants.Any(input => input.Id == existing.Id));

        foreach (var input in variants)
        {
            var existing = input.Id is null ? null : _variants.Find(item => item.Id == input.Id);

            if (existing is null)
            {
                _variants.Add(new ProductVariant(input));
                continue;
            }

            if (existing.Price != input.Price)
            {
                _priceChanges.Add(new PriceChange(existing.Id, existing.Price, input.Price));
                Raise(new VariantPriceChanged(Id, existing.Id, existing.Price.Amount, input.Price.Amount, input.Price.Currency, now));
            }

            existing.Update(input);
        }
    }

    /// <summary>False when the product is not ready to be sold.</summary>
    public bool CanPublish() => _translations.Count > 0 && _variants.Exists(variant => variant.IsActive);

    public void Publish(DateTimeOffset now)
    {
        if (!CanPublish())
        {
            throw new InvalidOperationException("A product needs a name and an active variant before publishing.");
        }

        Status = ProductStatus.Published;
        PublishedAt ??= now;
        Raise(new ProductPublished(Id, now));
    }

    public void Archive() => Status = ProductStatus.Archived;

    public void MoveToDraft() => Status = ProductStatus.Draft;
}
