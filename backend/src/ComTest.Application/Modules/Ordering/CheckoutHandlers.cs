using System.Security.Cryptography;
using System.Text;
using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Customers.Contracts;
using ComTest.Application.Modules.Inventory.Contracts;
using ComTest.Application.Modules.Ordering.Contracts;
using ComTest.Application.Modules.Payments.Contracts;
using ComTest.Application.Modules.Shipping.Contracts;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Domain.Modules.Ordering;
using ComTest.Shared.Domain;
using ComTest.Shared.Messaging;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComTest.Application.Modules.Ordering;

public sealed record CheckoutQuoteRequest(string? Region = null, Guid? ShippingMethodId = null);

internal sealed class CheckoutQuoteRequestValidator : AbstractValidator<CheckoutQuoteRequest>
{
    public CheckoutQuoteRequestValidator()
    {
        RuleFor(x => x.Region).MaximumLength(100);
    }
}

public sealed record CheckoutQuoteQuery(CartOwner Owner, CheckoutQuoteRequest Request) : IQuery<CheckoutQuoteDto>;

internal static class CheckoutRules
{
    /// <summary>The API's spelling of a stored name, the same as its enums: `Courier` becomes `courier`.</summary>
    public static string ApiName(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}

public sealed record ContactRequest(string Name, string Phone, string? Email = null);

internal sealed class ContactRequestValidator : AbstractValidator<ContactRequest>
{
    public ContactRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().Matches(AddressRules.PhonePattern);
        RuleFor(x => x.Email).MaximumLength(254).EmailAddress().When(x => x.Email is not null);
    }
}

/// <summary>Holds no prices or totals to trust. ExpectedTotal only lets the server say that the price has moved.</summary>
public sealed record PlaceOrderRequest(
    Guid ShippingMethodId,
    string PaymentMethod,
    ContactRequest? Contact = null,
    Guid? AddressId = null,
    AddressData? Address = null,
    string? CustomerNote = null,
    decimal? ExpectedTotal = null);

internal sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(x => x.ShippingMethodId).NotEmpty();
        RuleFor(x => x.PaymentMethod).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Contact!).SetValidator(new ContactRequestValidator()).When(x => x.Contact is not null);
        RuleFor(x => x.Address!).SetValidator(new AddressDataValidator()).When(x => x.Address is not null);
        RuleFor(x => x.CustomerNote).MaximumLength(1000);
    }
}

public sealed record PlaceOrderCommand(CartOwner Owner, string IdempotencyKey, PlaceOrderRequest Request) : ICommand<PlacedOrderDto>
{
    public override string ToString() => nameof(PlaceOrderCommand);
}

public sealed class CheckoutHandlers(
    IApplicationDbContext db,
    ITenantSettings tenantSettings,
    CartStore store,
    CartPricing pricing,
    IShippingRates shipping,
    IOrderPayments payments,
    IStockService stock,
    IAddressBook addressBook,
    IUserDirectory users,
    IOrderTrackingTokens trackingTokens,
    IOptions<CheckoutOptions> checkoutOptions,
    TimeProvider timeProvider) :
    IQueryHandler<CheckoutQuoteQuery, CheckoutQuoteDto>,
    ICommandHandler<PlaceOrderCommand, PlacedOrderDto>
{
    private const string UniqueViolation = "23505";

    public async Task<Result<CheckoutQuoteDto>> HandleAsync(CheckoutQuoteQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = await tenantSettings.GetCurrentAsync(cancellationToken);

        if (settings is null)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var loaded = await store.LoadAsync(query.Owner, createIfMissing: false, cancellationToken);
        var cart = await pricing.PriceAsync(loaded.Cart, settings.DefaultCurrency, cancellationToken);
        var shippingOptions = await shipping.GetOptionsAsync(query.Request.Region, cart.Subtotal, settings.DefaultCurrency, cancellationToken);
        var methods = await payments.GetAvailableMethodsAsync(cancellationToken);

        var chosen = query.Request.ShippingMethodId is { } methodId
            ? shippingOptions.FirstOrDefault(option => option.MethodId == methodId)
            : null;

        if (query.Request.ShippingMethodId is not null && chosen is null)
        {
            return Error.Validation(ErrorCodes.Shipping.MethodUnavailable);
        }

        var shippingCost = chosen?.Price ?? 0m;

        return new CheckoutQuoteDto(
            cart,
            shippingOptions.Select(option => new ShippingOptionDto(option.MethodId, CheckoutRules.ApiName(option.Type), option.Name, option.Price, option.Currency, option.MinDays, option.MaxDays, option.PickupAddress)).ToList(),
            methods.Select(method => new PaymentOptionDto(method.Code, method.Instructions)).ToList(),
            new TotalsDto(cart.Subtotal, 0m, 0m, shippingCost, cart.Subtotal + shippingCost, settings.DefaultCurrency));
    }

    public async Task<Result<PlacedOrderDto>> HandleAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var settings = await tenantSettings.GetCurrentAsync(cancellationToken);

        if (settings is null)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var keyHash = HashKey(command.Owner, command.IdempotencyKey);

        // The same request sent twice, such as after a lost response, gets the same order.
        var replay = await FindByKeyAsync(keyHash, settings.TenantId, cancellationToken);

        if (replay is not null)
        {
            return replay;
        }

        var loaded = await store.LoadAsync(command.Owner, createIfMissing: false, cancellationToken);

        if (loaded.Cart is null || loaded.Cart.Lines.Count == 0)
        {
            return Error.Conflict(ErrorCodes.Cart.Empty);
        }

        var cart = await pricing.PriceAsync(loaded.Cart, settings.DefaultCurrency, cancellationToken);

        if (cart.Lines.Any(line => !line.IsAvailable))
        {
            return Error.Conflict(ErrorCodes.Cart.ItemUnavailable);
        }

        var request = command.Request;
        var shipTo = await ResolveAddressAsync(command.Owner.UserId, request, cancellationToken);

        if (shipTo.IsFailure)
        {
            return shipTo.Error;
        }

        var contact = await ResolveContactAsync(command.Owner.UserId, request, shipTo.Value, cancellationToken);

        if (contact.IsFailure)
        {
            return contact.Error;
        }

        if (command.Owner.UserId is null && await HasTooManyOpenOrdersAsync(contact.Value.Phone, cancellationToken))
        {
            return Error.TooManyRequests(ErrorCodes.Checkout.TooManyOpenOrders);
        }

        var shippingOptions = await shipping.GetOptionsAsync(shipTo.Value?.Region, cart.Subtotal, settings.DefaultCurrency, cancellationToken);
        var method = shippingOptions.FirstOrDefault(option => option.MethodId == request.ShippingMethodId);

        if (method is null)
        {
            return Error.Validation(ErrorCodes.Shipping.MethodUnavailable);
        }

        if (method.Type == ShippingTypes.Courier && shipTo.Value is null)
        {
            return Error.Validation(ErrorCodes.Shipping.AddressRequired);
        }

        var now = timeProvider.GetUtcNow();
        var currency = settings.DefaultCurrency;

        var order = Order.Place(
            new OrderDraft(
                command.Owner.UserId,
                contact.Value,
                ToShipTo(shipTo.Value, contact.Value),
                method.MethodId,
                method.Name,
                method.Type,
                request.PaymentMethod,
                request.CustomerNote,
                keyHash,
                Money.Zero(currency),
                Money.Zero(currency),
                Money.Of(method.Price, currency),
                now.AddHours(checkoutOptions.Value.PendingOrderHours),
                ToOrderItems(cart, currency)),
            now);

        if (request.ExpectedTotal is { } expected && expected != order.GrandTotal.Amount)
        {
            return Error.Conflict(ErrorCodes.Checkout.TotalChanged);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.Orders.Add(order);

        var payment = await payments.CreateAsync(order.Id, request.PaymentMethod, order.GrandTotal, cancellationToken);

        if (payment.IsFailure)
        {
            return payment.Error;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Two identical requests raced, and the unique index stopped the second. Answer with the first.
            await transaction.RollbackAsync(cancellationToken);

            var winner = await FindByKeyAsync(keyHash, settings.TenantId, cancellationToken);

            return winner is null ? Error.Conflict(ErrorCodes.Common.Conflict) : winner;
        }

        var reserved = await stock.ReserveAsync(
            order.Id,
            order.Items.Select(item => new StockRequest(item.ProductVariantId, item.Quantity)).ToList(),
            cancellationToken);

        if (reserved.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);

            return reserved.Error;
        }

        loaded.Cart.Clear(now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PlacedOrderDto(
            OrderMappings.ToDto(order),
            trackingTokens.Create(settings.TenantId, order.Id),
            payment.Value.Initiation.RedirectUrl,
            payment.Value.Initiation.Instructions);
    }

    private static string HashKey(CartOwner owner, string key)
    {
        // Includes the requester, so that one person's key can never match another person's order.
        var requester = owner.UserId?.ToString("N") ?? owner.GuestToken ?? "anonymous";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{requester}:{key}")));
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is System.Data.Common.DbException { SqlState: UniqueViolation };

    /// <summary>Copies each name and price as it is at this moment, so that later catalog edits do not rewrite the order.</summary>
    private static List<OrderItemInput> ToOrderItems(CartDto cart, string currency) =>
        cart.Lines
            .Select(line => new OrderItemInput(
                line.VariantId,
                line.ProductId!.Value,
                line.Sku!,
                line.ProductName!,
                line.Options!,
                Money.Of(line.UnitPrice!.Value, currency),
                line.Quantity))
            .ToList();

    private async Task<PlacedOrderDto?> FindByKeyAsync(string keyHash, Guid tenantId, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.IdempotencyKeyHash == keyHash, cancellationToken);

        return order is null ? null : new PlacedOrderDto(OrderMappings.ToDto(order), trackingTokens.Create(tenantId, order.Id), null, null);
    }

    private async Task<bool> HasTooManyOpenOrdersAsync(string phone, CancellationToken cancellationToken)
    {
        var open = await db.Orders.CountAsync(
            order => order.CustomerUserId == null && order.Status == OrderStatus.Pending && order.Contact.Phone == phone,
            cancellationToken);

        return open >= checkoutOptions.Value.MaxOpenGuestOrdersPerPhone;
    }

    private async Task<Result<AddressData?>> ResolveAddressAsync(Guid? userId, PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.AddressId is not { } addressId)
        {
            return Result.Success(request.Address);
        }

        var owned = userId is { } id ? await addressBook.FindOwnedAsync(id, addressId, cancellationToken) : null;

        return owned is null ? Result.Failure<AddressData?>(Error.NotFound(ErrorCodes.Customer.AddressNotFound)) : Result.Success<AddressData?>(owned);
    }

    private async Task<Result<ContactInfo>> ResolveContactAsync(Guid? userId, PlaceOrderRequest request, AddressData? address, CancellationToken cancellationToken)
    {
        var profile = userId is { } id ? await users.FindAsync(id, cancellationToken) : null;

        var name = request.Contact?.Name ?? address?.Recipient ?? profile?.FullName;
        var phone = request.Contact?.Phone ?? address?.Phone;
        var email = request.Contact?.Email ?? profile?.Email;

        return string.IsNullOrEmpty(name) || string.IsNullOrEmpty(phone)
            ? Error.Validation(ErrorCodes.Checkout.ContactRequired)
            : new ContactInfo(name, phone, email);
    }

    private static ShipToAddress ToShipTo(AddressData? address, ContactInfo contact) =>
        address is null
            ? new ShipToAddress(contact.Name, contact.Phone, null, null, null, null, null, null)
            : new ShipToAddress(address.Recipient, address.Phone, address.Region, address.District, address.City, address.Line1, address.Line2, address.PostalCode);
}
