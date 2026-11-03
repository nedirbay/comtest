using FluentValidation;

namespace ComTest.Application.Modules.Customers.Contracts;

public sealed record AddressData(
    string Recipient,
    string Phone,
    string Region,
    string? District,
    string? City,
    string Line1,
    string? Line2,
    string? PostalCode);

public sealed record AddressDto(Guid Id, string? Label, AddressData Address, bool IsDefault);

public interface IAddressBook
{
    /// <returns>The address only when it belongs to this user. Someone else's looks like a missing one.</returns>
    Task<AddressData?> FindOwnedAsync(Guid userId, Guid addressId, CancellationToken cancellationToken);
}

public static class AddressRules
{
    public const string PhonePattern = @"^\+[1-9][0-9]{7,14}$";
}

public sealed class AddressDataValidator : AbstractValidator<AddressData>
{
    public AddressDataValidator()
    {
        RuleFor(x => x.Recipient).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().Matches(AddressRules.PhonePattern);
        RuleFor(x => x.Region).NotEmpty().MaximumLength(100);
        RuleFor(x => x.District).MaximumLength(100);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Line2).MaximumLength(300);
        RuleFor(x => x.PostalCode).MaximumLength(20);
    }
}
