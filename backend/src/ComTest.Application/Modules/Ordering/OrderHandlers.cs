using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Inventory.Contracts;
using ComTest.Application.Modules.Ordering.Contracts;
using ComTest.Application.Modules.Payments.Contracts;
using ComTest.Domain.Modules.Ordering;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Ordering;

internal static class OrderMappings
{
    public static TotalsDto ToTotals(Order order) => new(
        order.Subtotal.Amount,
        order.Discount.Amount,
        order.Tax.Amount,
        order.Shipping.Amount,
        order.GrandTotal.Amount,
        order.GrandTotal.Currency);

    public static TrackingDto ToTracking(Order order) =>
        new(order.Carrier, order.TrackingNumber, order.TrackingUrl, order.ShippedAt, order.DeliveredAt);

    public static OrderDto ToDto(Order order) => new(
        order.Id,
        order.Number,
        order.Status.ToString(),
        order.PaymentState.ToString(),
        order.PaymentMethod,
        ToTotals(order),
        order.Items
            .OrderBy(item => item.Id)
            .Select(item => new OrderItemDto(item.ProductVariantId, item.ProductId, item.Sku, item.ProductName, item.Options, item.UnitPrice.Amount, item.Quantity, item.LineTotal.Amount))
            .ToList(),
        new OrderContactDto(order.Contact.Name, order.Contact.Phone, order.Contact.Email),
        new OrderAddressDto(order.ShipTo.Recipient, order.ShipTo.Phone, order.ShipTo.Region, order.ShipTo.District, order.ShipTo.City, order.ShipTo.Line1, order.ShipTo.Line2, order.ShipTo.PostalCode),
        order.ShippingMethodName,
        order.ShippingType,
        ToTracking(order),
        order.CustomerNote,
        order.PlacedAt);

    public static OrderTrackingDto ToTrackingView(Order order) => new(
        order.Number,
        order.Status.ToString(),
        order.PaymentState.ToString(),
        order.PaymentMethod,
        ToTotals(order),
        order.Items
            .OrderBy(item => item.Id)
            .Select(item => new OrderTrackingItemDto(item.ProductName, item.Options, item.UnitPrice.Amount, item.Quantity, item.LineTotal.Amount))
            .ToList(),
        order.ShippingMethodName,
        ToTracking(order),
        order.PlacedAt);

    public static AdminOrderDto ToAdminDto(Order order) => new(
        ToDto(order),
        order.CustomerUserId,
        order.StatusHistory
            .OrderBy(item => item.Id)
            .Select(item => new OrderStatusChangeDto(item.FromStatus?.ToString(), item.ToStatus.ToString(), item.Note, item.CreatedAt, item.CreatedBy))
            .ToList(),
        order.Notes
            .OrderBy(item => item.Id)
            .Select(item => new OrderNoteDto(item.Id, item.Text, item.CreatedAt, item.CreatedBy))
            .ToList(),
        order.Version);
}

/// <summary>Applies a status change with its effects on stock and payment, all or nothing.</summary>
public sealed class OrderWorkflow(IApplicationDbContext db, IStockService stock, IOrderPayments payments, TimeProvider timeProvider)
{
    public async Task<Result> ChangeStatusAsync(Order order, OrderStatus to, string? note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (!Order.CanChange(order.Status, to))
        {
            return Result.Failure(Error.Conflict(ErrorCodes.Order.InvalidTransition));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        order.ChangeStatus(to, note, timeProvider.GetUtcNow());

        switch (to)
        {
            case OrderStatus.Shipped:
                await stock.CommitAsync(order.Id, cancellationToken);
                break;
            case OrderStatus.Cancelled:
                await stock.ReleaseAsync(order.Id, cancellationToken);
                await payments.CancelIfUnpaidAsync(order.Id, cancellationToken);
                break;
            case OrderStatus.Returned:
                await stock.RestockAsync(order.Id, cancellationToken);
                break;
            default:
                break;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class OrderPaymentSync(IApplicationDbContext db, TimeProvider timeProvider) : IOrderPaymentSync
{
    public async Task MarkPaidAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);

        order?.MarkPaid(timeProvider.GetUtcNow());
    }

    public async Task MarkRefundedAsync(Guid orderId, bool fully, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);

        order?.MarkRefunded(fully);
    }
}

public sealed record ChangeOrderStatusRequest(OrderStatus Status, string? Note = null, string? Carrier = null, string? TrackingNumber = null, string? TrackingUrl = null);

internal sealed class ChangeOrderStatusRequestValidator : AbstractValidator<ChangeOrderStatusRequest>
{
    public ChangeOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Carrier).MaximumLength(100);
        RuleFor(x => x.TrackingNumber).MaximumLength(100);

        // Only http(s): this link is shown to the customer.
        RuleFor(x => x.TrackingUrl)
            .MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            .When(x => x.TrackingUrl is not null);
    }
}

public sealed record AddOrderNoteRequest(string Text);

internal sealed class AddOrderNoteRequestValidator : AbstractValidator<AddOrderNoteRequest>
{
    public AddOrderNoteRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
    }
}

public sealed record TrackOrderRequest(string Token)
{
    public override string ToString() => nameof(TrackOrderRequest);
}

internal sealed class TrackOrderRequestValidator : AbstractValidator<TrackOrderRequest>
{
    public TrackOrderRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(1000);
    }
}

public sealed record OrderFilter(
    PageRequest Page,
    string? Sort,
    OrderStatus? Status,
    OrderPaymentState? PaymentState,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Search,
    Guid? CustomerUserId);

public sealed record ListOrdersQuery(OrderFilter Filter) : IQuery<PagedResult<OrderListItemDto>>;

public sealed record GetAdminOrderQuery(Guid OrderId) : IQuery<AdminOrderDto>;

public sealed record ChangeOrderStatusCommand(Guid OrderId, ChangeOrderStatusRequest Request) : ICommand<AdminOrderDto>;

public sealed record AddOrderNoteCommand(Guid OrderId, string Text) : ICommand<AdminOrderDto>;

public sealed record ListCustomersQuery(PageRequest Page, string? Search) : IQuery<PagedResult<CustomerSummaryDto>>;

public sealed record ListMyOrdersQuery(Guid UserId, PageRequest Page) : IQuery<PagedResult<OrderListItemDto>>;

public sealed record GetMyOrderQuery(Guid UserId, Guid OrderId) : IQuery<OrderDto>;

public sealed record CancelMyOrderCommand(Guid UserId, Guid OrderId) : ICommand<OrderDto>;

public sealed record TrackOrderQuery(string Token) : IQuery<OrderTrackingDto>
{
    public override string ToString() => nameof(TrackOrderQuery);
}

public sealed record ExpireOrderCommand(Guid OrderId) : ICommand<bool>;

public sealed class OrderHandlers(
    IApplicationDbContext db,
    OrderWorkflow workflow,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IOrderTrackingTokens trackingTokens,
    TimeProvider timeProvider) :
    IQueryHandler<ListOrdersQuery, PagedResult<OrderListItemDto>>,
    IQueryHandler<GetAdminOrderQuery, AdminOrderDto>,
    ICommandHandler<ChangeOrderStatusCommand, AdminOrderDto>,
    ICommandHandler<AddOrderNoteCommand, AdminOrderDto>,
    IQueryHandler<ListCustomersQuery, PagedResult<CustomerSummaryDto>>,
    IQueryHandler<ListMyOrdersQuery, PagedResult<OrderListItemDto>>,
    IQueryHandler<GetMyOrderQuery, OrderDto>,
    ICommandHandler<CancelMyOrderCommand, OrderDto>,
    IQueryHandler<TrackOrderQuery, OrderTrackingDto>,
    ICommandHandler<ExpireOrderCommand, bool>
{
    private const int MaxSearchLength = 100;

    private static readonly SortMap<Order> AdminSort = new SortMap<Order>()
        .Add("placedAt", order => order.PlacedAt)
        .Add("total", order => order.GrandTotal.Amount)
        .Add("status", order => order.Status)
        .Add("number", order => order.Number);

    public async Task<Result<PagedResult<OrderListItemDto>>> HandleAsync(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = query.Filter;
        var orders = db.Orders.AsNoTracking();

        if (filter.Status is { } status)
        {
            orders = orders.Where(order => order.Status == status);
        }

        if (filter.PaymentState is { } paymentState)
        {
            orders = orders.Where(order => order.PaymentState == paymentState);
        }

        if (filter.From is { } from)
        {
            orders = orders.Where(order => order.PlacedAt >= from);
        }

        if (filter.To is { } to)
        {
            orders = orders.Where(order => order.PlacedAt <= to);
        }

        if (filter.CustomerUserId is { } customerUserId)
        {
            orders = orders.Where(order => order.CustomerUserId == customerUserId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            term = term.Length > MaxSearchLength ? term[..MaxSearchLength] : term;

            orders = orders.Where(order => order.Number == term || order.Contact.Phone == term);
        }

        var sorted = AdminSort.Apply(orders, filter.Sort ?? "-placedAt", order => order.Id);

        if (sorted.IsFailure)
        {
            return sorted.Error;
        }

        return await ToPageAsync(orders, sorted.Value, filter.Page, cancellationToken);
    }

    public async Task<Result<AdminOrderDto>> HandleAsync(GetAdminOrderQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var order = await LoadAsync(query.OrderId, cancellationToken);

        return order is null ? Error.NotFound(ErrorCodes.Order.NotFound) : OrderMappings.ToAdminDto(order);
    }

    public async Task<Result<AdminOrderDto>> HandleAsync(ChangeOrderStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        // Undoing a sale takes more authority than moving it forward.
        if (request.Status is OrderStatus.Cancelled or OrderStatus.Returned && !currentUser.HasPermission(Permissions.OrdersManage))
        {
            return Error.Forbidden(ErrorCodes.Common.Forbidden);
        }

        var order = await LoadAsync(command.OrderId, cancellationToken);

        if (order is null)
        {
            return Error.NotFound(ErrorCodes.Order.NotFound);
        }

        if (request.Status == OrderStatus.Shipped)
        {
            order.SetTracking(request.Carrier, request.TrackingNumber, request.TrackingUrl);
        }

        var changed = await workflow.ChangeStatusAsync(order, request.Status, request.Note, cancellationToken);

        return changed.IsFailure ? changed.Error : OrderMappings.ToAdminDto(order);
    }

    public async Task<Result<AdminOrderDto>> HandleAsync(AddOrderNoteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await LoadAsync(command.OrderId, cancellationToken);

        if (order is null)
        {
            return Error.NotFound(ErrorCodes.Order.NotFound);
        }

        order.AddNote(command.Text);
        await db.SaveChangesAsync(cancellationToken);

        return OrderMappings.ToAdminDto(order);
    }

    public async Task<Result<PagedResult<CustomerSummaryDto>>> HandleAsync(ListCustomersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var orders = db.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            orders = orders.Where(order => order.Contact.Phone == term || order.Contact.Email == term);
        }

        // Guests have no account, so people are told apart by account or else by phone number.
        var grouped = orders
            .GroupBy(order => new { order.CustomerUserId, order.Contact.Phone })
            .Select(group => new
            {
                group.Key.CustomerUserId,
                group.Key.Phone,
                Name = group.Max(order => order.Contact.Name)!,
                Email = group.Max(order => order.Contact.Email),
                OrderCount = group.Count(),
                TotalSpent = group.Where(order => order.Status != OrderStatus.Cancelled).Sum(order => order.GrandTotal.Amount),
                Currency = group.Max(order => order.GrandTotal.Currency)!,
                LastOrderAt = group.Max(order => order.PlacedAt),
            });

        var total = await grouped.LongCountAsync(cancellationToken);

        var rows = await grouped
            .OrderByDescending(row => row.LastOrderAt)
            .ThenBy(row => row.Phone)
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new CustomerSummaryDto(row.CustomerUserId, row.Name, row.Phone, row.Email, row.OrderCount, row.TotalSpent, row.Currency, row.LastOrderAt))
            .ToList();

        return new PagedResult<CustomerSummaryDto>(items, query.Page.Page, query.Page.PageSize, total);
    }

    public async Task<Result<PagedResult<OrderListItemDto>>> HandleAsync(ListMyOrdersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var orders = db.Orders.AsNoTracking().Where(order => order.CustomerUserId == query.UserId);

        return await ToPageAsync(orders, orders.OrderByDescending(order => order.PlacedAt).ThenBy(order => order.Id), query.Page, cancellationToken);
    }

    public async Task<Result<OrderDto>> HandleAsync(GetMyOrderQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var order = await db.Orders
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == query.OrderId && item.CustomerUserId == query.UserId, cancellationToken);

        return order is null ? Error.NotFound(ErrorCodes.Order.NotFound) : OrderMappings.ToDto(order);
    }

    public async Task<Result<OrderDto>> HandleAsync(CancelMyOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await db.Orders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == command.OrderId && item.CustomerUserId == command.UserId, cancellationToken);

        if (order is null)
        {
            return Error.NotFound(ErrorCodes.Order.NotFound);
        }

        // Customers may change their mind only until the shop has started working on the order.
        // Once money has been received, cancelling also means a refund, so staff must handle it.
        if (order.Status != OrderStatus.Pending || order.PaymentState != OrderPaymentState.Unpaid)
        {
            return Error.Conflict(ErrorCodes.Order.InvalidTransition);
        }

        var changed = await workflow.ChangeStatusAsync(order, OrderStatus.Cancelled, null, cancellationToken);

        return changed.IsFailure ? changed.Error : OrderMappings.ToDto(order);
    }

    public async Task<Result<OrderTrackingDto>> HandleAsync(TrackOrderQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var invalid = Error.NotFound(ErrorCodes.Order.TrackingTokenInvalid);

        // A token from one store means nothing at another store's address.
        if (trackingTokens.Read(query.Token) is not { } ids || ids.TenantId != tenantContext.TenantId)
        {
            return invalid;
        }

        var order = await db.Orders
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == ids.OrderId, cancellationToken);

        return order is null ? invalid : OrderMappings.ToTrackingView(order);
    }

    public async Task<Result<bool>> HandleAsync(ExpireOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = timeProvider.GetUtcNow();
        var order = await db.Orders.FirstOrDefaultAsync(item => item.Id == command.OrderId, cancellationToken);

        if (order is not { Status: OrderStatus.Pending, PaymentState: OrderPaymentState.Unpaid, ExpiresAt: not null } || order.ExpiresAt > now)
        {
            return false;
        }

        var changed = await workflow.ChangeStatusAsync(order, OrderStatus.Cancelled, "expired", cancellationToken);

        return changed.IsFailure ? changed.Error : true;
    }

    private static async Task<PagedResult<OrderListItemDto>> ToPageAsync(
        IQueryable<Order> filtered,
        IQueryable<Order> sorted,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var total = await filtered.LongCountAsync(cancellationToken);

        var items = await sorted
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(order => new OrderListItemDto(
                order.Id,
                order.Number,
                order.Status.ToString(),
                order.PaymentState.ToString(),
                order.PaymentMethod,
                order.GrandTotal.Amount,
                order.GrandTotal.Currency,
                order.Items.Sum(item => item.Quantity),
                order.Contact.Name,
                order.Contact.Phone,
                order.PlacedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderListItemDto>(items, page.Page, page.PageSize, total);
    }

    private Task<Order?> LoadAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Orders
            .Include(item => item.Items)
            .Include(item => item.StatusHistory)
            .Include(item => item.Notes)
            .FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);
}
