using ComTest.Shared.Domain;

namespace ComTest.Domain.Modules.Shipping;

public enum ShippingMethodType
{
    Courier,
    Pickup,
}

/// <summary>A delivery area, such as a set of regions that share prices.</summary>
public sealed class ShippingZone : SoftDeletableEntity
{
    private ShippingZone()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyList<string> Regions { get; private set; } = [];

    public bool IsActive { get; private set; }

    public static ShippingZone Create(string name, IEnumerable<string> regions) => new()
    {
        Name = name,
        Regions = Normalize(regions),
        IsActive = true,
    };

    public static string NormalizeRegion(string region)
    {
        ArgumentNullException.ThrowIfNull(region);

#pragma warning disable CA1308 // Region codes are stored in lower case by convention.
        return region.Trim().ToLowerInvariant();
#pragma warning restore CA1308
    }

    public void Update(string name, IEnumerable<string> regions, bool isActive)
    {
        Name = name;
        Regions = Normalize(regions);
        IsActive = isActive;
    }

    public bool Covers(string region) => Regions.Contains(NormalizeRegion(region));

    private static List<string> Normalize(IEnumerable<string> regions) =>
        regions.Select(NormalizeRegion).Distinct(StringComparer.Ordinal).ToList();
}

public sealed class ShippingMethod : SoftDeletableEntity
{
    private ShippingMethod()
    {
    }

    /// <summary>The area served. Pickup has none, as the customer comes to the shop.</summary>
    public Guid? ZoneId { get; private set; }

    public ShippingMethodType Type { get; private set; }

    public IReadOnlyDictionary<string, string> Names { get; private set; } = new Dictionary<string, string>();

    public Money Price { get; private set; } = null!;

    /// <summary>Orders of at least this amount ship free. Same currency as <see cref="Price"/>.</summary>
    public decimal? FreeOverAmount { get; private set; }

    public int? MinDays { get; private set; }

    public int? MaxDays { get; private set; }

    public string? PickupAddress { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public static ShippingMethod Create(
        ShippingMethodType type,
        Guid? zoneId,
        IReadOnlyDictionary<string, string> names,
        Money price,
        decimal? freeOverAmount,
        int? minDays,
        int? maxDays,
        string? pickupAddress,
        int sortOrder)
    {
        var method = new ShippingMethod { Type = type };

        method.Update(zoneId, names, price, freeOverAmount, minDays, maxDays, pickupAddress, sortOrder, isActive: true);

        return method;
    }

    public void Update(
        Guid? zoneId,
        IReadOnlyDictionary<string, string> names,
        Money price,
        decimal? freeOverAmount,
        int? minDays,
        int? maxDays,
        string? pickupAddress,
        int sortOrder,
        bool isActive)
    {
        ZoneId = zoneId;
        Names = names;
        Price = price;
        FreeOverAmount = freeOverAmount;
        MinDays = minDays;
        MaxDays = maxDays;
        PickupAddress = pickupAddress;
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    public Money PriceFor(decimal subtotal) =>
        FreeOverAmount is { } threshold && subtotal >= threshold ? Money.Zero(Price.Currency) : Price;
}
