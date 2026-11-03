using ComTest.Shared.Domain;
using ComTest.Shared.Results;

namespace ComTest.Application.Modules.Payments.Contracts;

public static class PaymentMethodCodes
{
    public const string CashOnDelivery = "cashOnDelivery";
    public const string BankTransfer = "bankTransfer";
}

public sealed record PaymentRequest(Guid PaymentId, Guid OrderId, Money Amount);

/// <summary>What the customer must do next: be sent to a hosted page, follow instructions, or nothing.</summary>
public sealed record PaymentInitiation(string? RedirectUrl, string? Instructions);

/// <summary>
/// A way to pay. Online providers will send the customer to the bank's own page, so card
/// details never pass through this system.
/// </summary>
public interface IPaymentProvider
{
    string Code { get; }

    Task<bool> IsEnabledAsync(CancellationToken cancellationToken);

    Task<PaymentInitiation> InitiateAsync(PaymentRequest request, CancellationToken cancellationToken);
}

public sealed record PaymentMethodOption(string Code, string? Instructions);

public sealed record CreatedPayment(Guid PaymentId, PaymentInitiation Initiation);

public sealed record RefundDto(Guid Id, decimal Amount, string Currency, string Reason, string Status, Guid RequestedBy, Guid? DecidedBy, DateTimeOffset? DecidedAt);

public sealed record PaymentEventDto(string Type, decimal? Amount, string? Note, DateTimeOffset At, Guid? By);

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    string Method,
    string Status,
    decimal Amount,
    string Currency,
    decimal RefundedAmount,
    decimal PendingRefundAmount,
    string? Reference,
    DateTimeOffset? PaidAt,
    IReadOnlyList<RefundDto> Refunds,
    IReadOnlyList<PaymentEventDto> Events,
    uint Version);

/// <summary>What the ordering module asks of payments. Both run inside the caller's transaction.</summary>
public interface IOrderPayments
{
    Task<IReadOnlyList<PaymentMethodOption>> GetAvailableMethodsAsync(CancellationToken cancellationToken);

    /// <summary>Adds the payment without saving, so that it commits together with the order.</summary>
    Task<Result<CreatedPayment>> CreateAsync(Guid orderId, string methodCode, Money amount, CancellationToken cancellationToken);

    /// <summary>Stops expecting money for a cancelled order. Money already received stays until refunded.</summary>
    Task CancelIfUnpaidAsync(Guid orderId, CancellationToken cancellationToken);
}
