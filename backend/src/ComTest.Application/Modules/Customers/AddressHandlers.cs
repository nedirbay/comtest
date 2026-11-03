using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Customers.Contracts;
using ComTest.Domain.Modules.Customers;
using ComTest.Shared.Messaging;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Customers;

public sealed record SaveAddressRequest(AddressData Address, string? Label = null, bool IsDefault = false);

internal sealed class SaveAddressRequestValidator : AbstractValidator<SaveAddressRequest>
{
    public SaveAddressRequestValidator()
    {
        RuleFor(x => x.Label).MaximumLength(100);
        RuleFor(x => x.Address).NotNull().SetValidator(new AddressDataValidator());
    }
}

public sealed record ListAddressesQuery(Guid UserId) : IQuery<IReadOnlyList<AddressDto>>;

public sealed record CreateAddressCommand(Guid UserId, SaveAddressRequest Request) : ICommand<AddressDto>;

public sealed record UpdateAddressCommand(Guid UserId, Guid AddressId, SaveAddressRequest Request) : ICommand<AddressDto>;

public sealed record DeleteAddressCommand(Guid UserId, Guid AddressId) : ICommand<bool>;

/// <summary>Every query includes the user's id, so one customer can never reach another's address.</summary>
public sealed class AddressHandlers(IApplicationDbContext db) :
    IQueryHandler<ListAddressesQuery, IReadOnlyList<AddressDto>>,
    ICommandHandler<CreateAddressCommand, AddressDto>,
    ICommandHandler<UpdateAddressCommand, AddressDto>,
    ICommandHandler<DeleteAddressCommand, bool>,
    IAddressBook
{
    private const int MaxAddresses = 20;

    public async Task<Result<IReadOnlyList<AddressDto>>> HandleAsync(ListAddressesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var addresses = await db.CustomerAddresses
            .AsNoTracking()
            .Where(item => item.UserId == query.UserId)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AddressDto>>(addresses.Select(ToDto).ToList());
    }

    public async Task<Result<AddressDto>> HandleAsync(CreateAddressCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var count = await db.CustomerAddresses.CountAsync(item => item.UserId == command.UserId, cancellationToken);

        if (count >= MaxAddresses)
        {
            return Error.Conflict(ErrorCodes.Customer.AddressLimitReached);
        }

        var isDefault = command.Request.IsDefault || count == 0;

        if (isDefault)
        {
            await ClearDefaultAsync(command.UserId, cancellationToken);
        }

        var address = CustomerAddress.Create(command.UserId, command.Request.Label, ToDetails(command.Request.Address), isDefault);

        db.CustomerAddresses.Add(address);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(address);
    }

    public async Task<Result<AddressDto>> HandleAsync(UpdateAddressCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var address = await db.CustomerAddresses
            .FirstOrDefaultAsync(item => item.Id == command.AddressId && item.UserId == command.UserId, cancellationToken);

        if (address is null)
        {
            return Error.NotFound(ErrorCodes.Customer.AddressNotFound);
        }

        if (command.Request.IsDefault && !address.IsDefault)
        {
            await ClearDefaultAsync(command.UserId, cancellationToken);
            address.SetDefault(true);
        }

        address.Update(command.Request.Label, ToDetails(command.Request.Address));
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(address);
    }

    public async Task<Result<bool>> HandleAsync(DeleteAddressCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var address = await db.CustomerAddresses
            .FirstOrDefaultAsync(item => item.Id == command.AddressId && item.UserId == command.UserId, cancellationToken);

        if (address is null)
        {
            return Error.NotFound(ErrorCodes.Customer.AddressNotFound);
        }

        db.CustomerAddresses.Remove(address);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<AddressData?> FindOwnedAsync(Guid userId, Guid addressId, CancellationToken cancellationToken)
    {
        var address = await db.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == addressId && item.UserId == userId, cancellationToken);

        return address is null ? null : ToData(address.Details);
    }

    private async Task ClearDefaultAsync(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var current in await db.CustomerAddresses.Where(item => item.UserId == userId && item.IsDefault).ToListAsync(cancellationToken))
        {
            current.SetDefault(false);
        }
    }

    private static AddressDetails ToDetails(AddressData data) =>
        new(data.Recipient, data.Phone, data.Region, data.District, data.City, data.Line1, data.Line2, data.PostalCode);

    private static AddressData ToData(AddressDetails details) =>
        new(details.Recipient, details.Phone, details.Region, details.District, details.City, details.Line1, details.Line2, details.PostalCode);

    private static AddressDto ToDto(CustomerAddress address) =>
        new(address.Id, address.Label, ToData(address.Details), address.IsDefault);
}
