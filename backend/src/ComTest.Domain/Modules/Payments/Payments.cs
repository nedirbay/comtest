using ComTest.Shared.Domain;
using ComTest.Shared.Results;

namespace ComTest.Domain.Modules.Payments;

public enum PaymentMethodType
{
    CashOnDelivery,
    BankTransfer,
}

public enum PaymentStatus
{
    Pending,
    Paid,
    PartiallyRefunded,
    Refunded,
    Cancelled,
}

public enum RefundStatus
{
    PendingApproval,
    Completed,
    Rejected,
}

public enum PaymentEventType
{
    Created,
    Confirmed,
    Cancelled,
    RefundRequested,
    RefundCompleted,
    RefundRejected,
}

public sealed record PaymentConfirmed(Guid PaymentId, Guid OrderId, decimal Amount, string Currency, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record RefundCompleted(Guid PaymentId, Guid RefundId, decimal Amount, string Currency, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Append-only journal of what happened to a payment. CreatedBy records who did it.</summary>
public sealed class PaymentEvent : TenantOwnedEntity
{
    private PaymentEvent()
    {
    }

    internal PaymentEvent(PaymentEventType type, decimal? amount, string? note)
    {
        Type = type;
        Amount = amount;
        Note = note;
    }

    public Guid PaymentId { get; private set; }

    public PaymentEventType Type { get; private set; }

    public decimal? Amount { get; private set; }

    public string? Note { get; private set; }
}

public sealed class Refund : TenantOwnedEntity
{
    private Refund()
    {
    }

    internal Refund(Money amount, string reason, Guid requestedBy, RefundStatus status)
    {
        Amount = amount;
        Reason = reason;
        RequestedBy = requestedBy;
        Status = status;
    }

    public Guid PaymentId { get; private set; }

    public Money Amount { get; private set; } = null!;

    public string Reason { get; private set; } = string.Empty;

    public RefundStatus Status { get; private set; }

    public Guid RequestedBy { get; private set; }

    public Guid? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    internal void Decide(RefundStatus status, Guid? decidedBy, DateTimeOffset at)
    {
        Status = status;
        DecidedBy = decidedBy;
        DecidedAt = at;
    }
}

public sealed class Payment : TenantOwnedEntity
{
    private readonly List<Refund> _refunds = [];
    private readonly List<PaymentEvent> _events = [];

    private Payment()
    {
    }

    public Guid OrderId { get; private set; }

    public PaymentMethodType Method { get; private set; }

    public PaymentStatus Status { get; private set; }

    public Money Amount { get; private set; } = null!;

    public decimal RefundedAmount { get; private set; }

    public decimal PendingRefundAmount { get; private set; }

    public string? Reference { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public Guid? ConfirmedBy { get; private set; }

    public IReadOnlyCollection<Refund> Refunds => _refunds;

    public IReadOnlyCollection<PaymentEvent> Events => _events;

    public static Payment Create(Guid orderId, PaymentMethodType method, Money amount)
    {
        var payment = new Payment
        {
            OrderId = orderId,
            Method = method,
            Amount = amount,
            Status = PaymentStatus.Pending,
        };

        payment._events.Add(new PaymentEvent(PaymentEventType.Created, amount.Amount, null));

        return payment;
    }

    /// <summary>Records the money as received. Confirming twice changes nothing, so nothing is counted twice.</summary>
    public Result Confirm(Guid? by, string? reference, string? note, DateTimeOffset at)
    {
        if (Status is PaymentStatus.Paid or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded)
        {
            return Result.Success();
        }

        if (Status != PaymentStatus.Pending)
        {
            return Result.Failure(Error.Conflict(ErrorCodes.Payment.InvalidState));
        }

        Status = PaymentStatus.Paid;
        PaidAt = at;
        ConfirmedBy = by;
        Reference = reference;

        _events.Add(new PaymentEvent(PaymentEventType.Confirmed, Amount.Amount, note));
        Raise(new PaymentConfirmed(Id, OrderId, Amount.Amount, Amount.Currency, at));

        return Result.Success();
    }

    /// <summary>Only money not yet received can be cancelled. Received money goes back through a refund.</summary>
    public void CancelIfUnpaid()
    {
        if (Status == PaymentStatus.Pending)
        {
            Status = PaymentStatus.Cancelled;
            _events.Add(new PaymentEvent(PaymentEventType.Cancelled, null, null));
        }
    }

    public Result<Refund> RequestRefund(decimal amount, string reason, Guid requestedBy, bool needsApproval, DateTimeOffset at)
    {
        if (Status is not (PaymentStatus.Paid or PaymentStatus.PartiallyRefunded))
        {
            return Error.Conflict(ErrorCodes.Payment.InvalidState);
        }

        var money = Money.Of(amount, Amount.Currency);

        // What is already refunded and what awaits approval both count, so two requests cannot overdraw.
        if (RefundedAmount + PendingRefundAmount + money.Amount > Amount.Amount)
        {
            return Error.Validation(ErrorCodes.Payment.RefundExceedsPaid);
        }

        var refund = new Refund(money, reason, requestedBy, RefundStatus.PendingApproval);

        _refunds.Add(refund);
        _events.Add(new PaymentEvent(PaymentEventType.RefundRequested, money.Amount, reason));
        PendingRefundAmount += money.Amount;

        if (!needsApproval)
        {
            Complete(refund, null, at);
        }

        return refund;
    }

    public Result ApproveRefund(Guid refundId, Guid approverId, DateTimeOffset at)
    {
        var refund = FindPending(refundId);

        if (refund is null)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Payment.RefundNotFound));
        }

        if (refund.RequestedBy == approverId)
        {
            return Result.Failure(Error.Forbidden(ErrorCodes.Payment.SameApprover));
        }

        Complete(refund, approverId, at);

        return Result.Success();
    }

    public Result RejectRefund(Guid refundId, Guid deciderId, string? note, DateTimeOffset at)
    {
        var refund = FindPending(refundId);

        if (refund is null)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Payment.RefundNotFound));
        }

        PendingRefundAmount -= refund.Amount.Amount;
        refund.Decide(RefundStatus.Rejected, deciderId, at);
        _events.Add(new PaymentEvent(PaymentEventType.RefundRejected, refund.Amount.Amount, note));

        return Result.Success();
    }

    private Refund? FindPending(Guid refundId) =>
        _refunds.Find(item => item.Id == refundId && item.Status == RefundStatus.PendingApproval);

    private void Complete(Refund refund, Guid? approverId, DateTimeOffset at)
    {
        PendingRefundAmount -= refund.Amount.Amount;
        RefundedAmount += refund.Amount.Amount;
        Status = RefundedAmount >= Amount.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;

        refund.Decide(RefundStatus.Completed, approverId, at);
        _events.Add(new PaymentEvent(PaymentEventType.RefundCompleted, refund.Amount.Amount, null));
        Raise(new RefundCompleted(Id, refund.Id, refund.Amount.Amount, refund.Amount.Currency, at));
    }
}
