using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Ordering.Contracts;
using ComTest.Application.Modules.Payments.Contracts;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Domain.Modules.Payments;
using ComTest.Shared.Domain;
using ComTest.Shared.Messaging;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Payments;

public sealed class CashOnDeliveryProvider(ITenantSettings settings) : IPaymentProvider
{
    public string Code => PaymentMethodCodes.CashOnDelivery;

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
        (await settings.GetCurrentAsync(cancellationToken))?.AcceptsCashOnDelivery == true;

    public Task<PaymentInitiation> InitiateAsync(PaymentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentInitiation(null, null));
}

public sealed class BankTransferProvider(ITenantSettings settings) : IPaymentProvider
{
    public string Code => PaymentMethodCodes.BankTransfer;

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
        (await settings.GetCurrentAsync(cancellationToken))?.AcceptsBankTransfer == true;

    public async Task<PaymentInitiation> InitiateAsync(PaymentRequest request, CancellationToken cancellationToken) =>
        new(null, (await settings.GetCurrentAsync(cancellationToken))?.BankTransferInstructions);
}

public sealed class OrderPayments(IApplicationDbContext db, IEnumerable<IPaymentProvider> providers) : IOrderPayments
{
    public async Task<decimal> GetRefundedAsync(DateTimeOffset from, DateTimeOffset to, string currency, CancellationToken cancellationToken) =>
        await db.Refunds
            .AsNoTracking()
            .Where(refund => refund.Status == RefundStatus.Completed
                && refund.DecidedAt >= from
                && refund.DecidedAt < to
                && refund.Amount.Currency == currency)
            .SumAsync(refund => refund.Amount.Amount, cancellationToken);

    public async Task<IReadOnlyList<PaymentMethodOption>> GetAvailableMethodsAsync(CancellationToken cancellationToken)
    {
        var options = new List<PaymentMethodOption>();

        foreach (var provider in providers)
        {
            if (await provider.IsEnabledAsync(cancellationToken))
            {
                var preview = await provider.InitiateAsync(new PaymentRequest(Guid.Empty, Guid.Empty, Money.Zero("TMT")), cancellationToken);

                options.Add(new PaymentMethodOption(provider.Code, preview.Instructions));
            }
        }

        return options;
    }

    public async Task<Result<CreatedPayment>> CreateAsync(Guid orderId, string methodCode, Money amount, CancellationToken cancellationToken)
    {
        var provider = providers.FirstOrDefault(item => string.Equals(item.Code, methodCode, StringComparison.Ordinal));

        if (provider is null || !await provider.IsEnabledAsync(cancellationToken))
        {
            return Error.Validation(ErrorCodes.Payment.MethodUnavailable);
        }

        var method = methodCode == PaymentMethodCodes.BankTransfer ? PaymentMethodType.BankTransfer : PaymentMethodType.CashOnDelivery;
        var payment = Payment.Create(orderId, method, amount);

        db.Payments.Add(payment);

        var initiation = await provider.InitiateAsync(new PaymentRequest(payment.Id, orderId, amount), cancellationToken);

        return new CreatedPayment(payment.Id, initiation);
    }

    public async Task CancelIfUnpaidAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await db.Payments
            .Include(item => item.Events)
            .FirstOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);

        payment?.CancelIfUnpaid();
    }
}

public sealed record ConfirmPaymentRequest(string? Reference = null, string? Note = null);

internal sealed class ConfirmPaymentRequestValidator : AbstractValidator<ConfirmPaymentRequest>
{
    public ConfirmPaymentRequestValidator()
    {
        RuleFor(x => x.Reference).MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed record RequestRefundRequest(decimal Amount, string Reason);

internal sealed class RequestRefundRequestValidator : AbstractValidator<RequestRefundRequest>
{
    public RequestRefundRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m).LessThanOrEqualTo(10_000_000_000m);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed record RejectRefundRequest(string? Note = null);

public sealed record GetOrderPaymentQuery(Guid OrderId) : IQuery<PaymentDto>;

public sealed record ConfirmPaymentCommand(Guid ActorId, Guid OrderId, ConfirmPaymentRequest Request) : ICommand<PaymentDto>;

public sealed record RequestRefundCommand(Guid ActorId, Guid OrderId, RequestRefundRequest Request) : ICommand<PaymentDto>;

public sealed record ApproveRefundCommand(Guid ActorId, Guid OrderId, Guid RefundId) : ICommand<PaymentDto>;

public sealed record RejectRefundCommand(Guid ActorId, Guid OrderId, Guid RefundId, string? Note) : ICommand<PaymentDto>;

public sealed class PaymentHandlers(
    IApplicationDbContext db,
    IOrderPaymentSync orders,
    ITenantSettings tenantSettings,
    TimeProvider timeProvider) :
    IQueryHandler<GetOrderPaymentQuery, PaymentDto>,
    ICommandHandler<ConfirmPaymentCommand, PaymentDto>,
    ICommandHandler<RequestRefundCommand, PaymentDto>,
    ICommandHandler<ApproveRefundCommand, PaymentDto>,
    ICommandHandler<RejectRefundCommand, PaymentDto>
{
    public async Task<Result<PaymentDto>> HandleAsync(GetOrderPaymentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var payment = await LoadAsync(query.OrderId, cancellationToken);

        return payment is null ? Error.NotFound(ErrorCodes.Payment.NotFound) : ToDto(payment);
    }

    public async Task<Result<PaymentDto>> HandleAsync(ConfirmPaymentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payment = await LoadAsync(command.OrderId, cancellationToken);

        if (payment is null)
        {
            return Error.NotFound(ErrorCodes.Payment.NotFound);
        }

        var now = timeProvider.GetUtcNow();
        var confirmed = payment.Confirm(command.ActorId, command.Request.Reference, command.Request.Note, now);

        if (confirmed.IsFailure)
        {
            return confirmed.Error;
        }

        await orders.MarkPaidAsync(command.OrderId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(payment);
    }

    public async Task<Result<PaymentDto>> HandleAsync(RequestRefundCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payment = await LoadAsync(command.OrderId, cancellationToken);

        if (payment is null)
        {
            return Error.NotFound(ErrorCodes.Payment.NotFound);
        }

        var threshold = (await tenantSettings.GetCurrentAsync(cancellationToken))?.RefundApprovalThreshold;
        var needsApproval = threshold is { } limit && command.Request.Amount >= limit;

        var refund = payment.RequestRefund(command.Request.Amount, command.Request.Reason, command.ActorId, needsApproval, timeProvider.GetUtcNow());

        if (refund.IsFailure)
        {
            return refund.Error;
        }

        await SyncOrderAsync(payment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(payment);
    }

    public async Task<Result<PaymentDto>> HandleAsync(ApproveRefundCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payment = await LoadAsync(command.OrderId, cancellationToken);

        if (payment is null)
        {
            return Error.NotFound(ErrorCodes.Payment.RefundNotFound);
        }

        var approved = payment.ApproveRefund(command.RefundId, command.ActorId, timeProvider.GetUtcNow());

        if (approved.IsFailure)
        {
            return approved.Error;
        }

        await SyncOrderAsync(payment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(payment);
    }

    public async Task<Result<PaymentDto>> HandleAsync(RejectRefundCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var payment = await LoadAsync(command.OrderId, cancellationToken);

        if (payment is null)
        {
            return Error.NotFound(ErrorCodes.Payment.RefundNotFound);
        }

        var rejected = payment.RejectRefund(command.RefundId, command.ActorId, command.Note, timeProvider.GetUtcNow());

        if (rejected.IsFailure)
        {
            return rejected.Error;
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(payment);
    }

    private async Task SyncOrderAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (payment.RefundedAmount > 0m)
        {
            await orders.MarkRefundedAsync(payment.OrderId, payment.Status == PaymentStatus.Refunded, cancellationToken);
        }
    }

    private Task<Payment?> LoadAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Payments
            .Include(item => item.Refunds)
            .Include(item => item.Events)
            .FirstOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);

    private static PaymentDto ToDto(Payment payment) => new(
        payment.Id,
        payment.OrderId,
        payment.Method,
        payment.Status,
        payment.Amount.Amount,
        payment.Amount.Currency,
        payment.RefundedAmount,
        payment.PendingRefundAmount,
        payment.Reference,
        payment.PaidAt,
        payment.Refunds
            .OrderBy(item => item.Id)
            .Select(item => new RefundDto(item.Id, item.Amount.Amount, item.Amount.Currency, item.Reason, item.Status, item.RequestedBy, item.DecidedBy, item.DecidedAt))
            .ToList(),
        payment.Events
            .OrderBy(item => item.Id)
            .Select(item => new PaymentEventDto(item.Type, item.Amount, item.Note, item.CreatedAt, item.CreatedBy))
            .ToList(),
        payment.Version);
}
