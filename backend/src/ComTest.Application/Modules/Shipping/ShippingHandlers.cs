using System.Globalization;
using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Shipping.Contracts;
using ComTest.Domain.Modules.Shipping;
using ComTest.Shared.Domain;
using ComTest.Shared.Messaging;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Shipping;

public sealed record SaveShippingZoneRequest(string Name, IReadOnlyList<string> Regions, bool IsActive = true);

internal sealed class SaveShippingZoneRequestValidator : AbstractValidator<SaveShippingZoneRequest>
{
    public SaveShippingZoneRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Regions).NotEmpty().Must(regions => regions.Count <= 200);
        RuleForEach(x => x.Regions).NotEmpty().MaximumLength(100);
    }
}

public sealed record SaveShippingMethodRequest(
    ShippingMethodType Type,
    IReadOnlyDictionary<string, string> Names,
    decimal Price,
    string Currency,
    Guid? ZoneId = null,
    decimal? FreeOverAmount = null,
    int? MinDays = null,
    int? MaxDays = null,
    string? PickupAddress = null,
    int SortOrder = 0,
    bool IsActive = true);

internal sealed class SaveShippingMethodRequestValidator : AbstractValidator<SaveShippingMethodRequest>
{
    private static readonly string[] Languages = ["tk", "ru", "en"];

    public SaveShippingMethodRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Names).NotEmpty().Must(names => names.All(name => Languages.Contains(name.Key) && name.Value.Length is > 0 and <= 200));
        RuleFor(x => x.Price).InclusiveBetween(0m, 1_000_000m);
        RuleFor(x => x.Currency).Must(Money.IsSupportedCurrency);
        RuleFor(x => x.ZoneId).NotNull().When(x => x.Type == ShippingMethodType.Courier);
        RuleFor(x => x.ZoneId).Null().When(x => x.Type == ShippingMethodType.Pickup);
        RuleFor(x => x.PickupAddress).NotEmpty().When(x => x.Type == ShippingMethodType.Pickup);
        RuleFor(x => x.PickupAddress).MaximumLength(500);
        RuleFor(x => x.FreeOverAmount).GreaterThanOrEqualTo(0m).When(x => x.FreeOverAmount is not null);
        RuleFor(x => x.MinDays).InclusiveBetween(0, 365).When(x => x.MinDays is not null);
        RuleFor(x => x.MaxDays).InclusiveBetween(0, 365).When(x => x.MaxDays is not null);
    }
}

public sealed record ListShippingZonesQuery : IQuery<IReadOnlyList<ShippingZoneDto>>;

public sealed record CreateShippingZoneCommand(SaveShippingZoneRequest Request) : ICommand<ShippingZoneDto>;

public sealed record UpdateShippingZoneCommand(Guid Id, SaveShippingZoneRequest Request) : ICommand<ShippingZoneDto>;

public sealed record DeleteShippingZoneCommand(Guid Id) : ICommand<bool>;

public sealed record ListShippingMethodsQuery : IQuery<IReadOnlyList<ShippingMethodDto>>;

public sealed record CreateShippingMethodCommand(SaveShippingMethodRequest Request) : ICommand<ShippingMethodDto>;

public sealed record UpdateShippingMethodCommand(Guid Id, SaveShippingMethodRequest Request) : ICommand<ShippingMethodDto>;

public sealed record DeleteShippingMethodCommand(Guid Id) : ICommand<bool>;

public sealed record ListShippingRegionsQuery : IQuery<IReadOnlyList<string>>;

public sealed class ShippingHandlers(IApplicationDbContext db) :
    IQueryHandler<ListShippingZonesQuery, IReadOnlyList<ShippingZoneDto>>,
    ICommandHandler<CreateShippingZoneCommand, ShippingZoneDto>,
    ICommandHandler<UpdateShippingZoneCommand, ShippingZoneDto>,
    ICommandHandler<DeleteShippingZoneCommand, bool>,
    IQueryHandler<ListShippingMethodsQuery, IReadOnlyList<ShippingMethodDto>>,
    ICommandHandler<CreateShippingMethodCommand, ShippingMethodDto>,
    ICommandHandler<UpdateShippingMethodCommand, ShippingMethodDto>,
    ICommandHandler<DeleteShippingMethodCommand, bool>,
    IQueryHandler<ListShippingRegionsQuery, IReadOnlyList<string>>,
    IShippingRates
{
    public async Task<Result<IReadOnlyList<ShippingZoneDto>>> HandleAsync(ListShippingZonesQuery query, CancellationToken cancellationToken)
    {
        var zones = await db.ShippingZones.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ShippingZoneDto>>(zones.Select(ToDto).ToList());
    }

    public async Task<Result<ShippingZoneDto>> HandleAsync(CreateShippingZoneCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var zone = ShippingZone.Create(command.Request.Name, command.Request.Regions);

        db.ShippingZones.Add(zone);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(zone);
    }

    public async Task<Result<ShippingZoneDto>> HandleAsync(UpdateShippingZoneCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var zone = await db.ShippingZones.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (zone is null)
        {
            return Error.NotFound(ErrorCodes.Shipping.ZoneNotFound);
        }

        zone.Update(command.Request.Name, command.Request.Regions, command.Request.IsActive);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(zone);
    }

    public async Task<Result<bool>> HandleAsync(DeleteShippingZoneCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var zone = await db.ShippingZones.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (zone is null)
        {
            return Error.NotFound(ErrorCodes.Shipping.ZoneNotFound);
        }

        db.ShippingZones.Remove(zone);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<Result<IReadOnlyList<ShippingMethodDto>>> HandleAsync(ListShippingMethodsQuery query, CancellationToken cancellationToken)
    {
        var methods = await db.ShippingMethods.AsNoTracking().OrderBy(item => item.SortOrder).ThenBy(item => item.Id).ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ShippingMethodDto>>(methods.Select(ToDto).ToList());
    }

    public async Task<Result<ShippingMethodDto>> HandleAsync(CreateShippingMethodCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        if (await ZoneMissingAsync(request.ZoneId, cancellationToken))
        {
            return Error.NotFound(ErrorCodes.Shipping.ZoneNotFound);
        }

        var method = ShippingMethod.Create(
            request.Type,
            request.ZoneId,
            request.Names,
            Money.Of(request.Price, request.Currency),
            request.FreeOverAmount,
            request.MinDays,
            request.MaxDays,
            request.PickupAddress,
            request.SortOrder);

        db.ShippingMethods.Add(method);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(method);
    }

    public async Task<Result<ShippingMethodDto>> HandleAsync(UpdateShippingMethodCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var method = await db.ShippingMethods.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (method is null)
        {
            return Error.NotFound(ErrorCodes.Shipping.MethodNotFound);
        }

        if (await ZoneMissingAsync(request.ZoneId, cancellationToken))
        {
            return Error.NotFound(ErrorCodes.Shipping.ZoneNotFound);
        }

        method.Update(
            request.ZoneId,
            request.Names,
            Money.Of(request.Price, request.Currency),
            request.FreeOverAmount,
            request.MinDays,
            request.MaxDays,
            request.PickupAddress,
            request.SortOrder,
            request.IsActive);

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(method);
    }

    public async Task<Result<bool>> HandleAsync(DeleteShippingMethodCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var method = await db.ShippingMethods.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (method is null)
        {
            return Error.NotFound(ErrorCodes.Shipping.MethodNotFound);
        }

        db.ShippingMethods.Remove(method);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<Result<IReadOnlyList<string>>> HandleAsync(ListShippingRegionsQuery query, CancellationToken cancellationToken)
    {
        var zones = await db.ShippingZones.AsNoTracking().Where(item => item.IsActive).ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<string>>(zones.SelectMany(zone => zone.Regions).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());
    }

    public async Task<IReadOnlyList<ShippingOption>> GetOptionsAsync(string? region, decimal subtotal, string currency, CancellationToken cancellationToken)
    {
        var zones = await db.ShippingZones.AsNoTracking().Where(item => item.IsActive).ToListAsync(cancellationToken);
        var servingZones = region is null ? [] : zones.Where(zone => zone.Covers(region)).Select(zone => zone.Id).ToList();

        var methods = await db.ShippingMethods
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return methods
            .Where(method => string.Equals(method.Price.Currency, currency, StringComparison.Ordinal))
            .Where(method => method.Type == ShippingMethodType.Pickup || (method.ZoneId is { } zoneId && servingZones.Contains(zoneId)))
            .Select(method =>
            {
                var price = method.PriceFor(subtotal);

                return new ShippingOption(
                    method.Id,
                    method.Type == ShippingMethodType.Pickup ? ShippingTypes.Pickup : ShippingTypes.Courier,
                    PickName(method),
                    price.Amount,
                    price.Currency,
                    method.MinDays,
                    method.MaxDays,
                    method.PickupAddress);
            })
            .ToList();
    }

    private static string PickName(ShippingMethod method)
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        return method.Names.TryGetValue(language, out var name)
            ? name
            : method.Names.OrderBy(item => item.Key, StringComparer.Ordinal).First().Value;
    }

    private async Task<bool> ZoneMissingAsync(Guid? zoneId, CancellationToken cancellationToken) =>
        zoneId is { } id && !await db.ShippingZones.AnyAsync(item => item.Id == id, cancellationToken);

    private static ShippingZoneDto ToDto(ShippingZone zone) => new(zone.Id, zone.Name, zone.Regions, zone.IsActive);

    private static ShippingMethodDto ToDto(ShippingMethod method) => new(
        method.Id,
        method.Type.ToString(),
        method.ZoneId,
        method.Names,
        method.Price.Amount,
        method.Price.Currency,
        method.FreeOverAmount,
        method.MinDays,
        method.MaxDays,
        method.PickupAddress,
        method.SortOrder,
        method.IsActive);
}
