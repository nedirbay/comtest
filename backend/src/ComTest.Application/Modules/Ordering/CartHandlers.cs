using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Catalog.Contracts;
using ComTest.Application.Modules.Inventory.Contracts;
using ComTest.Application.Modules.Ordering.Contracts;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Domain.Modules.Ordering;
using ComTest.Shared.Messaging;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Ordering;

public sealed record AddCartItemRequest(Guid VariantId, int Quantity);

internal sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.Quantity).InclusiveBetween(1, Cart.MaxLineQuantity);
    }
}

public sealed record SetCartItemRequest(int Quantity);

internal sealed class SetCartItemRequestValidator : AbstractValidator<SetCartItemRequest>
{
    public SetCartItemRequestValidator()
    {
        RuleFor(x => x.Quantity).InclusiveBetween(1, Cart.MaxLineQuantity);
    }
}

public sealed record GetCartQuery(CartOwner Owner) : IQuery<CartResult>;

public sealed record AddCartItemCommand(CartOwner Owner, AddCartItemRequest Request) : ICommand<CartResult>;

public sealed record SetCartItemCommand(CartOwner Owner, Guid VariantId, int Quantity) : ICommand<CartResult>;

public sealed record RemoveCartItemCommand(CartOwner Owner, Guid VariantId) : ICommand<CartResult>;

public sealed record ClearCartCommand(CartOwner Owner) : ICommand<CartResult>;

internal static class GuestCartTokens
{
    private const int RandomBytes = 32;

    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RandomBytes));

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Finds the owner's cart, and folds a guest's cart into it once they have signed in.</summary>
public sealed class CartStore(IApplicationDbContext db, TimeProvider timeProvider)
{
    public sealed record Loaded(Cart? Cart, string? NewGuestToken, bool ClearGuestCookie);

    public async Task<Loaded> LoadAsync(CartOwner owner, bool createIfMissing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var now = timeProvider.GetUtcNow();

        if (owner.UserId is not { } userId)
        {
            return await LoadGuestAsync(owner.GuestToken, createIfMissing, now, cancellationToken);
        }

        var cart = await db.Carts.Include(item => item.Lines).FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (cart is null && (createIfMissing || owner.GuestToken is not null))
        {
            cart = Cart.ForUser(userId, now);
            db.Carts.Add(cart);
        }

        if (owner.GuestToken is null)
        {
            return new Loaded(cart, null, false);
        }

        var guestHash = GuestCartTokens.Hash(owner.GuestToken);
        var guest = await db.Carts.Include(item => item.Lines).FirstOrDefaultAsync(item => item.GuestTokenHash == guestHash, cancellationToken);

        if (guest is not null)
        {
            cart!.MergeFrom(guest, now);
            db.Carts.Remove(guest);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new Loaded(cart, null, true);
    }

    private async Task<Loaded> LoadGuestAsync(string? token, bool createIfMissing, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (token is not null)
        {
            var hash = GuestCartTokens.Hash(token);
            var existing = await db.Carts.Include(item => item.Lines).FirstOrDefaultAsync(item => item.GuestTokenHash == hash, cancellationToken);

            if (existing is not null)
            {
                return new Loaded(existing, null, false);
            }
        }

        if (!createIfMissing)
        {
            return new Loaded(null, null, false);
        }

        var newToken = GuestCartTokens.New();
        var cart = Cart.ForGuest(GuestCartTokens.Hash(newToken), now);

        db.Carts.Add(cart);

        return new Loaded(cart, newToken, false);
    }
}

/// <summary>Works out what a cart costs right now. Prices always come from the catalog, never from the client.</summary>
public sealed class CartPricing(ICatalogReader catalog, IStockService stock)
{
    public async Task<CartDto> PriceAsync(Cart? cart, string currency, CancellationToken cancellationToken)
    {
        if (cart is null || cart.Lines.Count == 0)
        {
            return new CartDto([], 0m, currency, 0, false);
        }

        var variantIds = cart.Lines.Select(line => line.ProductVariantId).ToList();
        var variants = await catalog.GetVariantsAsync(variantIds, cancellationToken);
        var available = await stock.GetAvailableAsync(variantIds, cancellationToken);

        var lines = cart.Lines
            .OrderBy(line => line.Id)
            .Select(line =>
            {
                var variant = variants.GetValueOrDefault(line.ProductVariantId);
                var sellable = variant is { IsPurchasable: true } && string.Equals(variant.Currency, currency, StringComparison.Ordinal);
                var unitPrice = variant?.Price;

                return new CartLineDto(
                    line.ProductVariantId,
                    variant?.ProductId,
                    variant?.ProductName,
                    variant?.ProductSlug,
                    variant?.Sku,
                    variant?.Options,
                    unitPrice,
                    variant?.Currency,
                    line.Quantity,
                    sellable ? unitPrice * line.Quantity : null,
                    sellable,
                    available.GetValueOrDefault(line.ProductVariantId) < line.Quantity,
                    unitPrice is { } current && current != line.AddedUnitPrice ? line.AddedUnitPrice : null);
            })
            .ToList();

        return new CartDto(
            lines,
            lines.Sum(line => line.LineTotal ?? 0m),
            currency,
            lines.Sum(line => line.Quantity),
            lines.Exists(line => !line.IsAvailable || line.InsufficientStock));
    }
}

public sealed class CartHandlers(
    IApplicationDbContext db,
    CartStore store,
    CartPricing pricing,
    ICatalogReader catalog,
    ITenantSettings tenantSettings,
    TimeProvider timeProvider) :
    IQueryHandler<GetCartQuery, CartResult>,
    ICommandHandler<AddCartItemCommand, CartResult>,
    ICommandHandler<SetCartItemCommand, CartResult>,
    ICommandHandler<RemoveCartItemCommand, CartResult>,
    ICommandHandler<ClearCartCommand, CartResult>
{
    public async Task<Result<CartResult>> HandleAsync(GetCartQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = await tenantSettings.GetCurrentAsync(cancellationToken);

        if (settings is null)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var loaded = await store.LoadAsync(query.Owner, createIfMissing: false, cancellationToken);

        return await ToResultAsync(loaded, settings.DefaultCurrency, cancellationToken);
    }

    public async Task<Result<CartResult>> HandleAsync(AddCartItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var settings = await tenantSettings.GetCurrentAsync(cancellationToken);

        if (settings is null)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var variants = await catalog.GetVariantsAsync([command.Request.VariantId], cancellationToken);

        if (!variants.TryGetValue(command.Request.VariantId, out var variant) || !variant.IsPurchasable)
        {
            return Error.NotFound(ErrorCodes.Cart.ItemUnavailable);
        }

        if (!string.Equals(variant.Currency, settings.DefaultCurrency, StringComparison.Ordinal))
        {
            return Error.Conflict(ErrorCodes.Cart.CurrencyMismatch);
        }

        var loaded = await store.LoadAsync(command.Owner, createIfMissing: true, cancellationToken);

        if (!loaded.Cart!.CanAddLine(command.Request.VariantId))
        {
            return Error.Conflict(ErrorCodes.Cart.TooManyLines);
        }

        loaded.Cart.Add(command.Request.VariantId, command.Request.Quantity, variant.Price, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return await ToResultAsync(loaded, settings.DefaultCurrency, cancellationToken);
    }

    public async Task<Result<CartResult>> HandleAsync(SetCartItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await ChangeAsync(
            command.Owner,
            cart => cart.SetQuantity(command.VariantId, command.Quantity, timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public async Task<Result<CartResult>> HandleAsync(RemoveCartItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await ChangeAsync(
            command.Owner,
            cart => cart.Remove(command.VariantId, timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public async Task<Result<CartResult>> HandleAsync(ClearCartCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await ChangeAsync(
            command.Owner,
            cart =>
            {
                cart.Clear(timeProvider.GetUtcNow());

                return true;
            },
            cancellationToken);
    }

    private async Task<Result<CartResult>> ChangeAsync(CartOwner owner, Func<Cart, bool> change, CancellationToken cancellationToken)
    {
        var settings = await tenantSettings.GetCurrentAsync(cancellationToken);

        if (settings is null)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var loaded = await store.LoadAsync(owner, createIfMissing: false, cancellationToken);

        if (loaded.Cart is null || !change(loaded.Cart))
        {
            return Error.NotFound(ErrorCodes.Cart.LineNotFound);
        }

        await db.SaveChangesAsync(cancellationToken);

        return await ToResultAsync(loaded, settings.DefaultCurrency, cancellationToken);
    }

    private async Task<Result<CartResult>> ToResultAsync(CartStore.Loaded loaded, string currency, CancellationToken cancellationToken) =>
        new CartResult(await pricing.PriceAsync(loaded.Cart, currency, cancellationToken), loaded.NewGuestToken, loaded.ClearGuestCookie);
}
