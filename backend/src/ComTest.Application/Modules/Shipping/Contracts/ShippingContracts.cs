using ComTest.Domain.Modules.Shipping;

namespace ComTest.Application.Modules.Shipping.Contracts;

public sealed record ShippingOption(
    Guid MethodId,
    string Type,
    string Name,
    decimal Price,
    string Currency,
    int? MinDays,
    int? MaxDays,
    string? PickupAddress);

public static class ShippingTypes
{
    public const string Courier = "Courier";
    public const string Pickup = "Pickup";
}

public interface IShippingRates
{
    /// <summary>The methods that can serve this region, priced for this subtotal. Pickup needs no region.</summary>
    Task<IReadOnlyList<ShippingOption>> GetOptionsAsync(string? region, decimal subtotal, string currency, CancellationToken cancellationToken);
}

public sealed record ShippingZoneDto(Guid Id, string Name, IReadOnlyList<string> Regions, bool IsActive);

public sealed record ShippingMethodDto(
    Guid Id,
    ShippingMethodType Type,
    Guid? ZoneId,
    IReadOnlyDictionary<string, string> Names,
    decimal Price,
    string Currency,
    decimal? FreeOverAmount,
    int? MinDays,
    int? MaxDays,
    string? PickupAddress,
    int SortOrder,
    bool IsActive);
