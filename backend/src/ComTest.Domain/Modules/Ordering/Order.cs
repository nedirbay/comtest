using System.Security.Cryptography;
using ComTest.Shared.Domain;

namespace ComTest.Domain.Modules.Ordering;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Packed,
    Shipped,
    Delivered,
    Cancelled,
    Returned,
}

public enum OrderPaymentState
{
    Unpaid,
    Paid,
    PartiallyRefunded,
    Refunded,
}

public sealed record OrderCreated(Guid OrderId, string Number, decimal Total, string Currency, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderPaid(Guid OrderId, string Number, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderShipped(Guid OrderId, string Number, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderDelivered(Guid OrderId, string Number, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderCancelled(Guid OrderId, string Number, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderReturned(Guid OrderId, string Number, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ContactInfo(string Name, string Phone, string? Email);

/// <summary>Where the order goes, copied at purchase so that later address edits do not rewrite history.</summary>
public sealed record ShipToAddress(
    string Recipient,
    string Phone,
    string? Region,
    string? District,
    string? City,
    string? Line1,
    string? Line2,
    string? PostalCode);

public sealed record OrderItemInput(
    Guid VariantId,
    Guid ProductId,
    string Sku,
    string ProductName,
    IReadOnlyDictionary<string, string> Options,
    Money UnitPrice,
    int Quantity);

public sealed record OrderDraft(
    Guid? CustomerUserId,
    ContactInfo Contact,
    ShipToAddress ShipTo,
    Guid? ShippingMethodId,
    string ShippingMethodName,
    string ShippingType,
    string PaymentMethod,
    string? CustomerNote,
    string IdempotencyKeyHash,
    Money Discount,
    Money Tax,
    Money Shipping,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<OrderItemInput> Items);

/// <summary>Random and unguessable: seeing one order number reveals nothing about any other, or about sales volume.</summary>
public static class OrderNumber
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int Length = 10;

    public static string New() => "T" + RandomNumberGenerator.GetString(Alphabet, Length);
}

public sealed class OrderItem : TenantOwnedEntity
{
    private OrderItem()
    {
    }

    internal OrderItem(OrderItemInput input)
    {
        ProductVariantId = input.VariantId;
        ProductId = input.ProductId;
        Sku = input.Sku;
        ProductName = input.ProductName;
        Options = input.Options;
        UnitPrice = input.UnitPrice;
        Quantity = input.Quantity;
        LineTotal = input.UnitPrice.Multiply(input.Quantity);
    }

    public Guid OrderId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public IReadOnlyDictionary<string, string> Options { get; private set; } = new Dictionary<string, string>();

    public Money UnitPrice { get; private set; } = null!;

    public int Quantity { get; private set; }

    public Money LineTotal { get; private set; } = null!;
}

/// <summary>Append-only history of status changes. CreatedBy and CreatedAt say who and when.</summary>
public sealed class OrderStatusChange : TenantOwnedEntity
{
    private OrderStatusChange()
    {
    }

    internal OrderStatusChange(OrderStatus? from, OrderStatus to, string? note)
    {
        FromStatus = from;
        ToStatus = to;
        Note = note;
    }

    public Guid OrderId { get; private set; }

    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    public string? Note { get; private set; }
}

/// <summary>A note for staff only. Customers never see these.</summary>
public sealed class OrderNote : TenantOwnedEntity
{
    private OrderNote()
    {
    }

    internal OrderNote(string text)
    {
        Text = text;
    }

    public Guid OrderId { get; private set; }

    public string Text { get; private set; } = string.Empty;
}

public sealed class Order : TenantOwnedEntity
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Packed, OrderStatus.Cancelled],
        [OrderStatus.Packed] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Returned],
        [OrderStatus.Delivered] = [OrderStatus.Returned],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Returned] = [],
    };

    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusChange> _statusHistory = [];
    private readonly List<OrderNote> _notes = [];

    private Order()
    {
    }

    public string Number { get; private set; } = string.Empty;

    public Guid? CustomerUserId { get; private set; }

    public ContactInfo Contact { get; private set; } = null!;

    public ShipToAddress ShipTo { get; private set; } = null!;

    public Guid? ShippingMethodId { get; private set; }

    public string ShippingMethodName { get; private set; } = string.Empty;

    public string ShippingType { get; private set; } = string.Empty;

    public string PaymentMethod { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; }

    public OrderPaymentState PaymentState { get; private set; }

    public Money Subtotal { get; private set; } = null!;

    public Money Discount { get; private set; } = null!;

    public Money Tax { get; private set; } = null!;

    public Money Shipping { get; private set; } = null!;

    public Money GrandTotal { get; private set; } = null!;

    public string? CustomerNote { get; private set; }

    public string IdempotencyKeyHash { get; private set; } = string.Empty;

    public DateTimeOffset PlacedAt { get; private set; }

    /// <summary>An order nobody confirms by this time is cancelled, so that it stops holding stock.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public string? Carrier { get; private set; }

    public string? TrackingNumber { get; private set; }

    public string? TrackingUrl { get; private set; }

    public DateTimeOffset? ShippedAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    public IReadOnlyCollection<OrderStatusChange> StatusHistory => _statusHistory;

    public IReadOnlyCollection<OrderNote> Notes => _notes;

    public static Order Place(OrderDraft draft, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (draft.Items.Count == 0)
        {
            throw new ArgumentException("An order needs at least one item.", nameof(draft));
        }

        var order = new Order
        {
            Number = OrderNumber.New(),
            CustomerUserId = draft.CustomerUserId,
            Contact = draft.Contact,
            ShipTo = draft.ShipTo,
            ShippingMethodId = draft.ShippingMethodId,
            ShippingMethodName = draft.ShippingMethodName,
            ShippingType = draft.ShippingType,
            PaymentMethod = draft.PaymentMethod,
            CustomerNote = draft.CustomerNote,
            IdempotencyKeyHash = draft.IdempotencyKeyHash,
            Discount = draft.Discount,
            Tax = draft.Tax,
            Shipping = draft.Shipping,
            ExpiresAt = draft.ExpiresAt,
            PlacedAt = now,
            Status = OrderStatus.Pending,
            PaymentState = OrderPaymentState.Unpaid,
        };

        order._items.AddRange(draft.Items.Select(item => new OrderItem(item)));

        var subtotal = order._items.Aggregate(Money.Zero(draft.Discount.Currency), (sum, item) => sum.Add(item.LineTotal));

        order.Subtotal = subtotal;
        order.GrandTotal = subtotal.SubtractUpToZero(draft.Discount).Add(draft.Tax).Add(draft.Shipping);

        order._statusHistory.Add(new OrderStatusChange(null, OrderStatus.Pending, null));
        order.Raise(new OrderCreated(order.Id, order.Number, order.GrandTotal.Amount, order.GrandTotal.Currency, now));

        return order;
    }

    public static bool CanChange(OrderStatus from, OrderStatus to) => Transitions[from].Contains(to);

    public void ChangeStatus(OrderStatus to, string? note, DateTimeOffset now)
    {
        if (!CanChange(Status, to))
        {
            throw new InvalidOperationException($"An order cannot go from {Status} to {to}.");
        }

        _statusHistory.Add(new OrderStatusChange(Status, to, note));
        Status = to;
        ExpiresAt = null;

        switch (to)
        {
            case OrderStatus.Shipped:
                ShippedAt = now;
                Raise(new OrderShipped(Id, Number, now));
                break;
            case OrderStatus.Delivered:
                DeliveredAt = now;
                Raise(new OrderDelivered(Id, Number, now));
                break;
            case OrderStatus.Cancelled:
                Raise(new OrderCancelled(Id, Number, now));
                break;
            case OrderStatus.Returned:
                Raise(new OrderReturned(Id, Number, now));
                break;
            default:
                break;
        }
    }

    public void SetTracking(string? carrier, string? trackingNumber, string? trackingUrl)
    {
        Carrier = carrier;
        TrackingNumber = trackingNumber;
        TrackingUrl = trackingUrl;
    }

    public void AddNote(string text) => _notes.Add(new OrderNote(text));

    public void MarkPaid(DateTimeOffset now)
    {
        if (PaymentState == OrderPaymentState.Unpaid)
        {
            PaymentState = OrderPaymentState.Paid;

            // Someone has paid: the order must no longer be cancelled for lack of attention.
            ExpiresAt = null;
            Raise(new OrderPaid(Id, Number, now));
        }
    }

    public void MarkRefunded(bool fully) =>
        PaymentState = fully ? OrderPaymentState.Refunded : OrderPaymentState.PartiallyRefunded;
}
