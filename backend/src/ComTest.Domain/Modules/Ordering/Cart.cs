using ComTest.Shared.Domain;

namespace ComTest.Domain.Modules.Ordering;

/// <summary>A signed-in user's cart, or a guest's identified by a secret cookie (only its hash is kept here).</summary>
public sealed class Cart : TenantOwnedEntity
{
    public const int MaxLineQuantity = 1000;
    public const int MaxLines = 100;

    private readonly List<CartLine> _lines = [];

    private Cart()
    {
    }

    public Guid? UserId { get; private set; }

    public string? GuestTokenHash { get; private set; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public IReadOnlyCollection<CartLine> Lines => _lines;

    public static Cart ForUser(Guid userId, DateTimeOffset now) => new() { UserId = userId, LastActivityAt = now };

    public static Cart ForGuest(string tokenHash, DateTimeOffset now) => new() { GuestTokenHash = tokenHash, LastActivityAt = now };

    public bool CanAddLine(Guid variantId) => _lines.Count < MaxLines || _lines.Exists(line => line.ProductVariantId == variantId);

    public void Add(Guid variantId, int quantity, decimal unitPrice, DateTimeOffset now)
    {
        var line = _lines.Find(item => item.ProductVariantId == variantId);

        if (line is null)
        {
            _lines.Add(new CartLine(variantId, Math.Min(quantity, MaxLineQuantity), unitPrice));
        }
        else
        {
            line.SetQuantity(Math.Min(line.Quantity + quantity, MaxLineQuantity));
        }

        LastActivityAt = now;
    }

    public bool SetQuantity(Guid variantId, int quantity, DateTimeOffset now)
    {
        var line = _lines.Find(item => item.ProductVariantId == variantId);

        if (line is null)
        {
            return false;
        }

        line.SetQuantity(quantity);
        LastActivityAt = now;

        return true;
    }

    public bool Remove(Guid variantId, DateTimeOffset now)
    {
        LastActivityAt = now;

        return _lines.RemoveAll(item => item.ProductVariantId == variantId) > 0;
    }

    public void Clear(DateTimeOffset now)
    {
        _lines.Clear();
        LastActivityAt = now;
    }

    /// <summary>Moves a guest's lines into this cart when the guest signs in.</summary>
    public void MergeFrom(Cart guest, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(guest);

        foreach (var line in guest._lines.Where(line => CanAddLine(line.ProductVariantId)))
        {
            Add(line.ProductVariantId, line.Quantity, line.AddedUnitPrice, now);
        }
    }
}

public sealed class CartLine : TenantOwnedEntity
{
    private CartLine()
    {
    }

    internal CartLine(Guid variantId, int quantity, decimal addedUnitPrice)
    {
        ProductVariantId = variantId;
        Quantity = quantity;
        AddedUnitPrice = addedUnitPrice;
    }

    public Guid CartId { get; private set; }

    public Guid ProductVariantId { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Only for telling the customer that the price changed. Never used to charge.</summary>
    public decimal AddedUnitPrice { get; private set; }

    internal void SetQuantity(int quantity) => Quantity = quantity;
}
