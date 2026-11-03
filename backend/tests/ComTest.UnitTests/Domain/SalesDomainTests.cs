using ComTest.Domain.Modules.Inventory;
using ComTest.Domain.Modules.Ordering;
using ComTest.Domain.Modules.Payments;
using ComTest.Domain.Modules.Shipping;
using ComTest.Shared.Domain;
using ComTest.Shared.Results;

namespace ComTest.UnitTests.Domain;

public sealed class OrderStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<OrderStatus, OrderStatus> Allowed => new()
    {
        { OrderStatus.Pending, OrderStatus.Confirmed },
        { OrderStatus.Pending, OrderStatus.Cancelled },
        { OrderStatus.Confirmed, OrderStatus.Packed },
        { OrderStatus.Confirmed, OrderStatus.Cancelled },
        { OrderStatus.Packed, OrderStatus.Shipped },
        { OrderStatus.Packed, OrderStatus.Cancelled },
        { OrderStatus.Shipped, OrderStatus.Delivered },
        { OrderStatus.Shipped, OrderStatus.Returned },
        { OrderStatus.Delivered, OrderStatus.Returned },
    };

    [Fact]
    public void Transitions_AreExactlyThoseAllowed()
    {
        var allowed = Allowed.Select(row => ((OrderStatus)row[0], (OrderStatus)row[1])).ToHashSet();

        foreach (var from in Enum.GetValues<OrderStatus>())
        {
            foreach (var to in Enum.GetValues<OrderStatus>())
            {
                Assert.True(Order.CanChange(from, to) == allowed.Contains((from, to)), $"{from} -> {to}");
            }
        }
    }

    [Fact]
    public void ForbiddenTransition_Throws()
    {
        var order = NewOrder();

        Assert.Throws<InvalidOperationException>(() => order.ChangeStatus(OrderStatus.Shipped, null, Now));
    }

    [Fact]
    public void Totals_AreWorkedOutFromTheLines()
    {
        var order = NewOrder(shipping: 20m, discount: 5m);

        Assert.Equal(250m, order.Subtotal.Amount);
        Assert.Equal(265m, order.GrandTotal.Amount);
    }

    [Fact]
    public void AnOversizedDiscount_StopsAtZero_RatherThanPayingTheCustomer()
    {
        var order = NewOrder(discount: 9999m);

        Assert.Equal(0m, order.GrandTotal.Amount);
    }

    [Fact]
    public void AnOrderWithoutItems_IsRefused() =>
        Assert.Throws<ArgumentException>(() => Order.Place(Draft([]), Now));

    [Fact]
    public void Confirming_StopsTheExpiryClock_AndHistoryRecordsEveryStep()
    {
        var order = NewOrder();

        Assert.NotNull(order.ExpiresAt);

        order.ChangeStatus(OrderStatus.Confirmed, "ok", Now);

        Assert.Null(order.ExpiresAt);
        Assert.Equal(2, order.StatusHistory.Count);
    }

    [Fact]
    public void MarkPaid_RaisesTheEventOnce()
    {
        var order = NewOrder();
        order.ClearDomainEvents();

        order.MarkPaid(Now);
        order.MarkPaid(Now);

        Assert.Single(order.DomainEvents, domainEvent => domainEvent is OrderPaid);
    }

    [Fact]
    public void Paying_StopsTheExpiryClock()
    {
        var order = NewOrder();

        order.MarkPaid(Now);

        Assert.Null(order.ExpiresAt);
    }

    [Fact]
    public void OrderNumbers_AreRandomAndUnambiguous()
    {
        var numbers = Enumerable.Range(0, 200).Select(_ => OrderNumber.New()).ToList();

        Assert.Equal(200, numbers.Distinct(StringComparer.Ordinal).Count());
        Assert.All(numbers, number => Assert.DoesNotContain(number[1..], character => "01OIL".Contains(character, StringComparison.Ordinal)));
    }

    private static Order NewOrder(decimal shipping = 0m, decimal discount = 0m) =>
        Order.Place(
            Draft(
                [new OrderItemInput(Guid.NewGuid(), Guid.NewGuid(), "SKU", "Telefon", new Dictionary<string, string>(), Money.Of(100m, "TMT"), 2), new OrderItemInput(Guid.NewGuid(), Guid.NewGuid(), "SKU2", "Çehol", new Dictionary<string, string>(), Money.Of(50m, "TMT"), 1)],
                shipping,
                discount),
            Now);

    private static OrderDraft Draft(IReadOnlyList<OrderItemInput> items, decimal shipping = 0m, decimal discount = 0m) => new(
        null,
        new ContactInfo("Aýna", "+99365000000", null),
        new ShipToAddress("Aýna", "+99365000000", "ashgabat", null, null, "Köçe 1", null, null),
        Guid.NewGuid(),
        "Kurýer",
        "Courier",
        "cashOnDelivery",
        null,
        "hash",
        Money.Of(discount, "TMT"),
        Money.Zero("TMT"),
        Money.Of(shipping, "TMT"),
        Now.AddHours(48),
        items);
}

public sealed class CartTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddingTheSameVariant_AddsUpAndStopsAtTheLimit()
    {
        var variant = Guid.NewGuid();
        var cart = Cart.ForGuest("hash", Now);

        cart.Add(variant, 2, 10m, Now);
        cart.Add(variant, 3, 10m, Now);

        Assert.Equal(5, Assert.Single(cart.Lines).Quantity);

        cart.Add(variant, Cart.MaxLineQuantity, 10m, Now);

        Assert.Equal(Cart.MaxLineQuantity, Assert.Single(cart.Lines).Quantity);
    }

    [Fact]
    public void Merge_AddsGuestLines_ToTheUsersCart()
    {
        var shared = Guid.NewGuid();
        var guestOnly = Guid.NewGuid();
        var guest = Cart.ForGuest("hash", Now);
        var user = Cart.ForUser(Guid.NewGuid(), Now);

        guest.Add(shared, 2, 10m, Now);
        guest.Add(guestOnly, 1, 10m, Now);
        user.Add(shared, 3, 10m, Now);

        user.MergeFrom(guest, Now);

        Assert.Equal(2, user.Lines.Count);
        Assert.Equal(5, user.Lines.Single(line => line.ProductVariantId == shared).Quantity);
    }

    [Fact]
    public void LineLimit_IsChecked_ButAKnownVariantCanStillGrow()
    {
        var cart = Cart.ForGuest("hash", Now);
        var first = Guid.NewGuid();

        cart.Add(first, 1, 10m, Now);

        for (var index = 1; index < Cart.MaxLines; index++)
        {
            cart.Add(Guid.NewGuid(), 1, 10m, Now);
        }

        Assert.False(cart.CanAddLine(Guid.NewGuid()));
        Assert.True(cart.CanAddLine(first));
    }
}

public sealed class PaymentDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();

    [Fact]
    public void Confirm_IsIdempotent()
    {
        var payment = NewPayment();

        Assert.True(payment.Confirm(Requester, "ref", null, Now).IsSuccess);
        Assert.True(payment.Confirm(Requester, "other", null, Now).IsSuccess);

        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal("ref", payment.Reference);
        Assert.Single(payment.Events, item => item.Type == PaymentEventType.Confirmed);
        Assert.Single(payment.DomainEvents);
    }

    [Fact]
    public void Cancelled_CannotBeConfirmed()
    {
        var payment = NewPayment();
        payment.CancelIfUnpaid();

        Assert.Equal(ErrorCodes.Payment.InvalidState, payment.Confirm(Requester, null, null, Now).Error!.Code);
    }

    [Fact]
    public void Cancel_LeavesReceivedMoneyAlone()
    {
        var payment = PaidPayment();

        payment.CancelIfUnpaid();

        Assert.Equal(PaymentStatus.Paid, payment.Status);
    }

    [Fact]
    public void Refund_RequiresAPaidPayment() =>
        Assert.Equal(ErrorCodes.Payment.InvalidState, NewPayment().RequestRefund(10m, "r", Requester, false, Now).Error!.Code);

    [Fact]
    public void Refunds_CannotExceedWhatWasPaid()
    {
        var payment = PaidPayment();

        Assert.True(payment.RequestRefund(60m, "r", Requester, false, Now).IsSuccess);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);

        Assert.Equal(ErrorCodes.Payment.RefundExceedsPaid, payment.RequestRefund(40.01m, "r", Requester, false, Now).Error!.Code);

        Assert.True(payment.RequestRefund(40m, "r", Requester, false, Now).IsSuccess);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void PendingRefunds_CountAgainstTheLimit()
    {
        var payment = PaidPayment();

        Assert.True(payment.RequestRefund(80m, "r", Requester, needsApproval: true, Now).IsSuccess);
        Assert.Equal(0m, payment.RefundedAmount);

        Assert.True(payment.RequestRefund(30m, "r", Requester, false, Now).IsFailure);
    }

    [Fact]
    public void LargeRefund_NeedsSomeoneElsesApproval()
    {
        var payment = PaidPayment();
        var refund = payment.RequestRefund(80m, "r", Requester, needsApproval: true, Now).Value;

        Assert.Equal(RefundStatus.PendingApproval, refund.Status);
        Assert.Equal(ErrorCodes.Payment.SameApprover, payment.ApproveRefund(refund.Id, Requester, Now).Error!.Code);

        Assert.True(payment.ApproveRefund(refund.Id, Approver, Now).IsSuccess);
        Assert.Equal(80m, payment.RefundedAmount);
        Assert.Equal(0m, payment.PendingRefundAmount);
        Assert.Equal(Approver, refund.DecidedBy);

        Assert.Equal(ErrorCodes.Payment.RefundNotFound, payment.ApproveRefund(refund.Id, Approver, Now).Error!.Code);
    }

    [Fact]
    public void RejectedRefund_FreesTheAmount()
    {
        var payment = PaidPayment();
        var refund = payment.RequestRefund(80m, "r", Requester, needsApproval: true, Now).Value;

        Assert.True(payment.RejectRefund(refund.Id, Approver, "no", Now).IsSuccess);

        Assert.Equal(0m, payment.PendingRefundAmount);
        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.True(payment.RequestRefund(100m, "r", Requester, false, Now).IsSuccess);
    }

    [Fact]
    public void Journal_RecordsEveryStep()
    {
        var payment = PaidPayment();
        payment.RequestRefund(10m, "r", Requester, false, Now);

        Assert.Equal(
            [PaymentEventType.Created, PaymentEventType.Confirmed, PaymentEventType.RefundRequested, PaymentEventType.RefundCompleted],
            payment.Events.Select(item => item.Type));
    }

    private static Payment NewPayment() => Payment.Create(Guid.NewGuid(), PaymentMethodType.CashOnDelivery, Money.Of(100m, "TMT"));

    private static Payment PaidPayment()
    {
        var payment = NewPayment();
        payment.Confirm(Requester, null, null, Now);

        return payment;
    }
}

public sealed class ShippingDomainTests
{
    private static ShippingMethod Method(decimal price, decimal? freeOver) => ShippingMethod.Create(
        ShippingMethodType.Courier,
        Guid.NewGuid(),
        new Dictionary<string, string> { ["tk"] = "Kurýer" },
        Money.Of(price, "TMT"),
        freeOver,
        null,
        null,
        null,
        0);

    [Theory]
    [InlineData(999.99, 20)]
    [InlineData(1000, 0)]
    [InlineData(5000, 0)]
    public void Price_DropsToZero_AboveTheFreeThreshold(decimal subtotal, decimal expected) =>
        Assert.Equal(expected, Method(20m, 1000m).PriceFor(subtotal).Amount);

    [Fact]
    public void WithoutAThreshold_ThePriceAlwaysApplies() =>
        Assert.Equal(20m, Method(20m, null).PriceFor(1_000_000m).Amount);

    [Theory]
    [InlineData("ashgabat", true)]
    [InlineData(" ASHGABAT ", true)]
    [InlineData("mary", false)]
    public void Zone_MatchesRegionsWhateverTheCase(string region, bool expected) =>
        Assert.Equal(expected, ShippingZone.Create("Şäher", ["Ashgabat", "ahal"]).Covers(region));
}

public sealed class LowStockTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WithAThreshold_AlertsOnce()
    {
        var stock = StockItem.Create(Guid.NewGuid(), Guid.NewGuid());
        stock.SetThreshold(5);

        stock.CheckLowStock(Now);
        stock.CheckLowStock(Now);

        Assert.Single(stock.DomainEvents, item => item is LowStockDetected);
    }

    [Fact]
    public void WithoutAThreshold_NeverAlerts()
    {
        var stock = StockItem.Create(Guid.NewGuid(), Guid.NewGuid());

        stock.CheckLowStock(Now);

        Assert.Empty(stock.DomainEvents);
    }
}
