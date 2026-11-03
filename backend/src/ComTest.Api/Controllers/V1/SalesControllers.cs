using System.Text.RegularExpressions;
using Asp.Versioning;
using ComTest.Api.Hosting;
using ComTest.Api.Security;
using ComTest.Application.Modules.Customers;
using ComTest.Application.Modules.Customers.Contracts;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Ordering;
using ComTest.Application.Modules.Ordering.Contracts;
using ComTest.Application.Modules.Payments;
using ComTest.Application.Modules.Payments.Contracts;
using ComTest.Application.Modules.Shipping;
using ComTest.Application.Modules.Shipping.Contracts;
using ComTest.Domain.Modules.Ordering;
using ComTest.Infrastructure.Identity.Authorization;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComTest.Api.Controllers.V1;

/// <summary>Works for guests and signed-in customers alike. A guest is known only by a cookie.</summary>
public abstract class CartControllerBase(GuestCartCookie guestCookie) : TenantControllerBase
{
    protected CartOwner Owner => new(CurrentUserId, guestCookie.Read(Request));

    protected IActionResult CartResponse(Result<CartResult> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsFailure)
        {
            return Failure(result.Error);
        }

        if (result.Value.NewGuestToken is { } token)
        {
            guestCookie.Write(Response, token);
        }
        else if (result.Value.ClearGuestCookie)
        {
            guestCookie.Delete(Response);
        }

        return Ok(result.Value.Cart);
    }
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/cart")]
[AllowAnonymous]
public sealed class CartController(GuestCartCookie guestCookie) : CartControllerBase(guestCookie)
{
    private const string CartDescription = "The cart, priced now. A guest's first added item sets the HttpOnly guest cart cookie; after sign-in the guest cart is merged in and that cookie is cleared.";

    [HttpGet]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK, Description = CartDescription)]
    public async Task<IActionResult> Get(
        [FromServices] IQueryHandler<GetCartQuery, CartResult> handler,
        CancellationToken cancellationToken) =>
        CartResponse(await handler.HandleAsync(new GetCartQuery(Owner), cancellationToken));

    [HttpPost("items")]
    [RequireCsrfHeader]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK, Description = CartDescription)]
    public async Task<IActionResult> Add(
        AddCartItemRequest request,
        [FromServices] ICommandHandler<AddCartItemCommand, CartResult> handler,
        CancellationToken cancellationToken) =>
        CartResponse(await handler.HandleAsync(new AddCartItemCommand(Owner, request), cancellationToken));

    [HttpPut("items/{variantId:guid}")]
    [RequireCsrfHeader]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK, Description = CartDescription)]
    public async Task<IActionResult> SetQuantity(
        Guid variantId,
        SetCartItemRequest request,
        [FromServices] ICommandHandler<SetCartItemCommand, CartResult> handler,
        CancellationToken cancellationToken) =>
        CartResponse(await handler.HandleAsync(new SetCartItemCommand(Owner, variantId, request.Quantity), cancellationToken));

    [HttpDelete("items/{variantId:guid}")]
    [RequireCsrfHeader]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK, Description = CartDescription)]
    public async Task<IActionResult> Remove(
        Guid variantId,
        [FromServices] ICommandHandler<RemoveCartItemCommand, CartResult> handler,
        CancellationToken cancellationToken) =>
        CartResponse(await handler.HandleAsync(new RemoveCartItemCommand(Owner, variantId), cancellationToken));

    [HttpDelete]
    [RequireCsrfHeader]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK, Description = CartDescription)]
    public async Task<IActionResult> Clear(
        [FromServices] ICommandHandler<ClearCartCommand, CartResult> handler,
        CancellationToken cancellationToken) =>
        CartResponse(await handler.HandleAsync(new ClearCartCommand(Owner), cancellationToken));
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/checkout")]
[AllowAnonymous]
public sealed partial class CheckoutController(GuestCartCookie guestCookie) : CartControllerBase(guestCookie)
{
    private const string IdempotencyHeader = "Idempotency-Key";

    [HttpPost("quote")]
    [RequireCsrfHeader]
    [ProducesResponseType<CheckoutQuoteDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Quote(
        CheckoutQuoteRequest request,
        [FromServices] IQueryHandler<CheckoutQuoteQuery, CheckoutQuoteDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new CheckoutQuoteQuery(Owner, request), cancellationToken));

    /// <summary>Places the order. The client names what it wants; every amount is worked out here.</summary>
    [HttpPost]
    [RequireCsrfHeader]
    [EnableRateLimiting(RateLimitPolicies.Checkout)]
    [ProducesResponseType<PlacedOrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Place(
        PlaceOrderRequest request,
        [FromServices] ICommandHandler<PlaceOrderCommand, PlacedOrderDto> handler,
        CancellationToken cancellationToken)
    {
        var key = Request.Headers[IdempotencyHeader].ToString();

        if (!IdempotencyKeyPattern().IsMatch(key))
        {
            return Failure(Error.Validation(ErrorCodes.Checkout.IdempotencyKeyRequired));
        }

        return FromResult(await handler.HandleAsync(new PlaceOrderCommand(Owner, key, request), cancellationToken));
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{8,64}$")]
    private static partial Regex IdempotencyKeyPattern();
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/shipping/regions")]
[AllowAnonymous]
public sealed class ShippingRegionsController : TenantControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListShippingRegionsQuery, IReadOnlyList<string>> handler,
        CancellationToken cancellationToken) =>
        CurrentTenantId is null
            ? Failure(TenantRequired)
            : FromResult(await handler.HandleAsync(new ListShippingRegionsQuery(), cancellationToken));
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/orders/track")]
[AllowAnonymous]
public sealed class OrderTrackingController : TenantControllerBase
{
    /// <summary>A POST, so that the token stays out of URLs and therefore out of proxy logs.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Tracking)]
    [ProducesResponseType<OrderTrackingDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Track(
        TrackOrderRequest request,
        [FromServices] IQueryHandler<TrackOrderQuery, OrderTrackingDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new TrackOrderQuery(request.Token), cancellationToken));
}

/// <summary>A customer's own orders. Every query carries the caller's id, so others' orders look absent.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/account/orders")]
[Authorize]
public sealed class AccountOrdersController : TenantControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<OrderListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IQueryHandler<ListMyOrdersQuery, PagedResult<OrderListItemDto>> handler,
        CancellationToken cancellationToken) =>
        CurrentTenantId is null
            ? Failure(TenantRequired)
            : FromResult(await handler.HandleAsync(new ListMyOrdersQuery(UserId, PageRequests.From(page, pageSize)), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        Guid id,
        [FromServices] IQueryHandler<GetMyOrderQuery, OrderDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetMyOrderQuery(UserId, id), cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromServices] ICommandHandler<CancelMyOrderCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new CancelMyOrderCommand(UserId, id), cancellationToken));
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/account/addresses")]
[Authorize]
public sealed class AccountAddressesController : TenantControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AddressDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListAddressesQuery, IReadOnlyList<AddressDto>> handler,
        CancellationToken cancellationToken) =>
        CurrentTenantId is null
            ? Failure(TenantRequired)
            : FromResult(await handler.HandleAsync(new ListAddressesQuery(UserId), cancellationToken));

    [HttpPost]
    [ProducesResponseType<AddressDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        SaveAddressRequest request,
        [FromServices] ICommandHandler<CreateAddressCommand, AddressDto> handler,
        CancellationToken cancellationToken) =>
        CurrentTenantId is null
            ? Failure(TenantRequired)
            : FromResult(await handler.HandleAsync(new CreateAddressCommand(UserId, request), cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AddressDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        SaveAddressRequest request,
        [FromServices] ICommandHandler<UpdateAddressCommand, AddressDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new UpdateAddressCommand(UserId, id, request), cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteAddressCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteAddressCommand(UserId, id), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/orders")]
public sealed class AdminOrdersController : TenantControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.OrdersRead)]
    [ProducesResponseType<PagedResult<OrderListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? sort,
        [FromQuery] OrderStatus? status,
        [FromQuery] OrderPaymentState? paymentState,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? q,
        [FromQuery] Guid? customerUserId,
        [FromServices] IQueryHandler<ListOrdersQuery, PagedResult<OrderListItemDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(
            new ListOrdersQuery(new OrderFilter(PageRequests.From(page, pageSize), sort, status, paymentState, from?.ToUniversalTime(), to?.ToUniversalTime(), q, customerUserId)),
            cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.OrdersRead)]
    [ProducesResponseType<AdminOrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        Guid id,
        [FromServices] IQueryHandler<GetAdminOrderQuery, AdminOrderDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetAdminOrderQuery(id), cancellationToken));

    [HttpPut("{id:guid}/status")]
    [HasPermission(Permissions.OrdersWrite)]
    [ProducesResponseType<AdminOrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        ChangeOrderStatusRequest request,
        [FromServices] ICommandHandler<ChangeOrderStatusCommand, AdminOrderDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ChangeOrderStatusCommand(id, request), cancellationToken));

    [HttpPost("{id:guid}/notes")]
    [HasPermission(Permissions.OrdersWrite)]
    [ProducesResponseType<AdminOrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddNote(
        Guid id,
        AddOrderNoteRequest request,
        [FromServices] ICommandHandler<AddOrderNoteCommand, AdminOrderDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new AddOrderNoteCommand(id, request.Text), cancellationToken));

    [HttpGet("{id:guid}/payment")]
    [HasPermission(Permissions.OrdersRead)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Payment(
        Guid id,
        [FromServices] IQueryHandler<GetOrderPaymentQuery, PaymentDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetOrderPaymentQuery(id), cancellationToken));

    [HttpPost("{id:guid}/payment/confirm")]
    [HasPermission(Permissions.PaymentsConfirm)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmPayment(
        Guid id,
        ConfirmPaymentRequest request,
        [FromServices] ICommandHandler<ConfirmPaymentCommand, PaymentDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ConfirmPaymentCommand(UserId, id, request), cancellationToken));

    [HttpPost("{id:guid}/payment/refunds")]
    [HasPermission(Permissions.PaymentsRefund)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestRefund(
        Guid id,
        RequestRefundRequest request,
        [FromServices] ICommandHandler<RequestRefundCommand, PaymentDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new RequestRefundCommand(UserId, id, request), cancellationToken));

    [HttpPost("{id:guid}/payment/refunds/{refundId:guid}/approve")]
    [HasPermission(Permissions.PaymentsRefundApprove)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveRefund(
        Guid id,
        Guid refundId,
        [FromServices] ICommandHandler<ApproveRefundCommand, PaymentDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ApproveRefundCommand(UserId, id, refundId), cancellationToken));

    [HttpPost("{id:guid}/payment/refunds/{refundId:guid}/reject")]
    [HasPermission(Permissions.PaymentsRefundApprove)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectRefund(
        Guid id,
        Guid refundId,
        RejectRefundRequest request,
        [FromServices] ICommandHandler<RejectRefundCommand, PaymentDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new RejectRefundCommand(UserId, id, refundId, request.Note), cancellationToken));
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/customers")]
public sealed class AdminCustomersController : TenantControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.CustomersRead)]
    [ProducesResponseType<PagedResult<CustomerSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? q,
        [FromServices] IQueryHandler<ListCustomersQuery, PagedResult<CustomerSummaryDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListCustomersQuery(PageRequests.From(page, pageSize), q), cancellationToken));
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/shipping")]
public sealed class AdminShippingController : TenantControllerBase
{
    [HttpGet("zones")]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType<IReadOnlyList<ShippingZoneDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Zones(
        [FromServices] IQueryHandler<ListShippingZonesQuery, IReadOnlyList<ShippingZoneDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListShippingZonesQuery(), cancellationToken));

    [HttpPost("zones")]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType<ShippingZoneDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateZone(
        SaveShippingZoneRequest request,
        [FromServices] ICommandHandler<CreateShippingZoneCommand, ShippingZoneDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new CreateShippingZoneCommand(request), cancellationToken));

    [HttpPut("zones/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType<ShippingZoneDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateZone(
        Guid id,
        SaveShippingZoneRequest request,
        [FromServices] ICommandHandler<UpdateShippingZoneCommand, ShippingZoneDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new UpdateShippingZoneCommand(id, request), cancellationToken));

    [HttpDelete("zones/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteZone(
        Guid id,
        [FromServices] ICommandHandler<DeleteShippingZoneCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteShippingZoneCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    [HttpGet("methods")]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType<IReadOnlyList<ShippingMethodDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Methods(
        [FromServices] IQueryHandler<ListShippingMethodsQuery, IReadOnlyList<ShippingMethodDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListShippingMethodsQuery(), cancellationToken));

    [HttpPost("methods")]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType<ShippingMethodDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateMethod(
        SaveShippingMethodRequest request,
        [FromServices] ICommandHandler<CreateShippingMethodCommand, ShippingMethodDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new CreateShippingMethodCommand(request), cancellationToken));

    [HttpPut("methods/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType<ShippingMethodDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMethod(
        Guid id,
        SaveShippingMethodRequest request,
        [FromServices] ICommandHandler<UpdateShippingMethodCommand, ShippingMethodDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new UpdateShippingMethodCommand(id, request), cancellationToken));

    [HttpDelete("methods/{id:guid}")]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMethod(
        Guid id,
        [FromServices] ICommandHandler<DeleteShippingMethodCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteShippingMethodCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}
