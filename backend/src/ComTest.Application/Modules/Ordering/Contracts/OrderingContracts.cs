using ComTest.Domain.Modules.Ordering;

namespace ComTest.Application.Modules.Ordering.Contracts;

/// <summary>Lets the payments module update an order. Changes are saved by the caller, in one transaction.</summary>
public interface IOrderPaymentSync
{
    Task MarkPaidAsync(Guid orderId, CancellationToken cancellationToken);

    Task MarkRefundedAsync(Guid orderId, bool fully, CancellationToken cancellationToken);
}

/// <summary>Signed tokens that let a guest follow one order without an account.</summary>
public interface IOrderTrackingTokens
{
    string Create(Guid tenantId, Guid orderId);

    /// <returns>The ids from a genuine, unexpired token, otherwise null.</returns>
    (Guid TenantId, Guid OrderId)? Read(string token);
}

public sealed class CheckoutOptions
{
    public const string SectionName = "Checkout";

    /// <summary>An order nobody confirms in this time is cancelled and its stock freed.</summary>
    public int PendingOrderHours { get; set; } = 48;

    /// <summary>Unconfirmed guest orders allowed per phone number, as they hold real stock.</summary>
    public int MaxOpenGuestOrdersPerPhone { get; set; } = 3;
}

public sealed record CartOwner(Guid? UserId, string? GuestToken)
{
    public override string ToString() => nameof(CartOwner);
}

public sealed record CartLineDto(
    Guid VariantId,
    Guid? ProductId,
    string? ProductName,
    string? ProductSlug,
    string? Sku,
    IReadOnlyDictionary<string, string>? Options,
    decimal? UnitPrice,
    string? Currency,
    int Quantity,
    decimal? LineTotal,
    bool IsAvailable,
    bool InsufficientStock,
    decimal? PreviousUnitPrice);

public sealed record CartDto(IReadOnlyList<CartLineDto> Lines, decimal Subtotal, string Currency, int ItemCount, bool HasProblems);

/// <summary>The cart, plus what the API must do with the guest cookie.</summary>
public sealed record CartResult(CartDto Cart, string? NewGuestToken, bool ClearGuestCookie)
{
    public override string ToString() => nameof(CartResult);
}

public sealed record TotalsDto(decimal Subtotal, decimal Discount, decimal Tax, decimal Shipping, decimal Total, string Currency);

public sealed record ShippingOptionDto(Guid MethodId, string Type, string Name, decimal Price, string Currency, int? MinDays, int? MaxDays, string? PickupAddress);

public sealed record PaymentOptionDto(string Code, string? Instructions);

public sealed record CheckoutQuoteDto(
    CartDto Cart,
    IReadOnlyList<ShippingOptionDto> ShippingOptions,
    IReadOnlyList<PaymentOptionDto> PaymentOptions,
    TotalsDto Totals);

public sealed record OrderItemDto(Guid VariantId, Guid ProductId, string Sku, string ProductName, IReadOnlyDictionary<string, string> Options, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderContactDto(string Name, string Phone, string? Email);

public sealed record OrderAddressDto(string Recipient, string Phone, string? Region, string? District, string? City, string? Line1, string? Line2, string? PostalCode);

public sealed record TrackingDto(string? Carrier, string? Number, string? Url, DateTimeOffset? ShippedAt, DateTimeOffset? DeliveredAt);

public sealed record OrderDto(
    Guid Id,
    string Number,
    OrderStatus Status,
    OrderPaymentState PaymentState,
    string PaymentMethod,
    TotalsDto Totals,
    IReadOnlyList<OrderItemDto> Items,
    OrderContactDto Contact,
    OrderAddressDto ShipTo,
    string ShippingMethodName,
    string ShippingType,
    TrackingDto Tracking,
    string? CustomerNote,
    DateTimeOffset PlacedAt);

public sealed record OrderStatusChangeDto(OrderStatus? From, OrderStatus To, string? Note, DateTimeOffset At, Guid? By);

public sealed record OrderNoteDto(Guid Id, string Text, DateTimeOffset At, Guid? By);

public sealed record AdminOrderDto(OrderDto Order, Guid? CustomerUserId, IReadOnlyList<OrderStatusChangeDto> History, IReadOnlyList<OrderNoteDto> Notes, uint Version);

public sealed record OrderListItemDto(Guid Id, string Number, OrderStatus Status, OrderPaymentState PaymentState, string PaymentMethod, decimal Total, string Currency, int ItemCount, string ContactName, string ContactPhone, DateTimeOffset PlacedAt);

/// <summary>What a guest with a tracking link sees: no phone number, no street address, no staff data.</summary>
public sealed record OrderTrackingDto(
    string Number,
    OrderStatus Status,
    OrderPaymentState PaymentState,
    string PaymentMethod,
    TotalsDto Totals,
    IReadOnlyList<OrderTrackingItemDto> Items,
    string ShippingMethodName,
    TrackingDto Tracking,
    DateTimeOffset PlacedAt);

public sealed record OrderTrackingItemDto(string ProductName, IReadOnlyDictionary<string, string> Options, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record PlacedOrderDto(OrderDto Order, string TrackingToken, string? PaymentRedirectUrl, string? PaymentInstructions)
{
    public override string ToString() => nameof(PlacedOrderDto);
}

public sealed record CustomerSummaryDto(Guid? UserId, string Name, string Phone, string? Email, int OrderCount, decimal TotalSpent, string Currency, DateTimeOffset LastOrderAt);
