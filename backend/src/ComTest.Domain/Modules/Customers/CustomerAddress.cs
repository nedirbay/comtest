using ComTest.Shared.Domain;

namespace ComTest.Domain.Modules.Customers;

public sealed record AddressDetails(
    string Recipient,
    string Phone,
    string Region,
    string? District,
    string? City,
    string Line1,
    string? Line2,
    string? PostalCode);

/// <summary>A delivery address in a user's address book. Only its owner can see or change it.</summary>
public sealed class CustomerAddress : SoftDeletableEntity
{
    private CustomerAddress()
    {
    }

    public Guid UserId { get; private set; }

    public string? Label { get; private set; }

    public AddressDetails Details { get; private set; } = null!;

    public bool IsDefault { get; private set; }

    public static CustomerAddress Create(Guid userId, string? label, AddressDetails details, bool isDefault) => new()
    {
        UserId = userId,
        Label = label,
        Details = details,
        IsDefault = isDefault,
    };

    public void Update(string? label, AddressDetails details)
    {
        Label = label;
        Details = details;
    }

    public void SetDefault(bool isDefault) => IsDefault = isDefault;
}
