using System.Security.Cryptography;
using ComTest.Shared.Domain;

namespace ComTest.Domain.Modules.Promotions;

public enum PromotionType
{
    Percentage,
    FixedAmount,
    FreeShipping,
}

public static class PromotionCode
{
    public const int GeneratedLength = 12;
    public const int MinLength = 6;
    public const int MaxLength = 32;
    public const string Pattern = "^[A-Z0-9-]+$";

    // No 0/O or 1/I/L: people read these codes from a screen and type them in.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    /// <summary>About 59 bits from a secure source, so that trying codes one by one gets nowhere.</summary>
    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, GeneratedLength);

    public static string Normalize(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return code.Trim().ToUpperInvariant();
    }
}

/// <summary>What staff decide about a promotion. A null code makes it automatic.</summary>
public sealed record PromotionTerms(
    string? Code,
    string Name,
    PromotionType Type,
    decimal Value,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    decimal? MinSubtotal,
    Guid? CategoryId,
    int? UsageLimit,
    int? PerCustomerLimit,
    bool CombinesWithOthers,
    bool IsActive);

public sealed class Promotion : SoftDeletableEntity
{
    private Promotion()
    {
    }

    public string? Code { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public PromotionType Type { get; private set; }

    /// <summary>A percentage or an amount in <see cref="Currency"/>, depending on the type.</summary>
    public decimal Value { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public DateTimeOffset? StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    public decimal? MinSubtotal { get; private set; }

    public Guid? CategoryId { get; private set; }

    public int? UsageLimit { get; private set; }

    /// <summary>Changed only by an atomic database update, never through this object, as two buyers can race for the last use.</summary>
    public int UsageCount { get; private set; }

    public int? PerCustomerLimit { get; private set; }

    public bool CombinesWithOthers { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsAutomatic => Code is null;

    public static Promotion Create(PromotionTerms terms, string currency)
    {
        var promotion = new Promotion { Currency = currency };

        promotion.Apply(terms);

        return promotion;
    }

    public void Update(PromotionTerms terms) => Apply(terms);

    public bool IsRunning(DateTimeOffset now) =>
        IsActive
        && (StartsAt is null || StartsAt <= now)
        && (EndsAt is null || EndsAt > now)
        && (UsageLimit is null || UsageCount < UsageLimit);

    private void Apply(PromotionTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        Code = terms.Code;
        Name = terms.Name;
        Type = terms.Type;
        Value = terms.Type == PromotionType.FreeShipping ? 0m : terms.Value;
        StartsAt = terms.StartsAt;
        EndsAt = terms.EndsAt;
        MinSubtotal = terms.MinSubtotal;
        CategoryId = terms.CategoryId;
        UsageLimit = terms.UsageLimit;
        PerCustomerLimit = terms.PerCustomerLimit;
        CombinesWithOthers = terms.CombinesWithOthers;
        IsActive = terms.IsActive;
    }
}

/// <summary>
/// One use of a promotion by one order. Sequence is only set under a per-customer limit: it counts
/// that customer's uses, and its unique index stops two of their orders from taking the same place.
/// </summary>
public sealed class PromotionRedemption : TenantOwnedEntity
{
    private PromotionRedemption()
    {
    }

    public PromotionRedemption(Guid promotionId, Guid orderId, string customerKey, int? sequence, decimal amount)
    {
        PromotionId = promotionId;
        OrderId = orderId;
        CustomerKey = customerKey;
        Sequence = sequence;
        Amount = amount;
    }

    public Guid PromotionId { get; private set; }

    public Guid OrderId { get; private set; }

    /// <summary>A hash, so that this table holds no phone number of its own.</summary>
    public string CustomerKey { get; private set; } = string.Empty;

    public int? Sequence { get; private set; }

    public decimal Amount { get; private set; }
}

public sealed record PromotionLine(decimal LineTotal, IReadOnlySet<Guid> Categories);

public sealed record PromotionShare(Promotion Promotion, decimal Amount);

public sealed record PromotionOutcome(decimal GoodsDiscount, decimal ShippingDiscount, IReadOnlyList<PromotionShare> Shares)
{
    public static PromotionOutcome None { get; } = new(0m, 0m, []);

    public decimal Value => GoodsDiscount + ShippingDiscount;
}

/// <summary>
/// The rules for combining discounts. A promotion stands alone unless it says it combines, and those
/// that do can add up. The buyer gets the best allowed set, and goods can never cost less than zero.
/// </summary>
public static class PromotionCalculator
{
    public static decimal Eligible(Promotion promotion, IReadOnlyList<PromotionLine> lines)
    {
        ArgumentNullException.ThrowIfNull(promotion);
        ArgumentNullException.ThrowIfNull(lines);

        return promotion.CategoryId is { } categoryId
            ? lines.Where(line => line.Categories.Contains(categoryId)).Sum(line => line.LineTotal)
            : lines.Sum(line => line.LineTotal);
    }

    public static PromotionOutcome Best(Promotion? withCode, IReadOnlyList<Promotion> automatic, IReadOnlyList<PromotionLine> lines, decimal shipping)
    {
        ArgumentNullException.ThrowIfNull(automatic);
        ArgumentNullException.ThrowIfNull(lines);

        var combining = automatic.Where(item => item.CombinesWithOthers).ToList();
        var options = new List<IReadOnlyList<Promotion>>();

        if (withCode is not null)
        {
            options.Add([withCode]);

            if (withCode.CombinesWithOthers && combining.Count > 0)
            {
                options.Add([withCode, .. combining]);
            }
        }

        options.AddRange(automatic.Select(item => (IReadOnlyList<Promotion>)[item]));

        if (combining.Count > 1)
        {
            options.Add(combining);
        }

        // MaxBy keeps the first of equals, and a set with the buyer's own code comes first.
        return options.Select(option => Apply(option, lines, shipping)).MaxBy(outcome => outcome.Value) ?? PromotionOutcome.None;
    }

    private static PromotionOutcome Apply(IReadOnlyList<Promotion> promotions, IReadOnlyList<PromotionLine> lines, decimal shipping)
    {
        var subtotal = lines.Sum(line => line.LineTotal);
        var remaining = subtotal;
        var shippingDiscount = 0m;
        var shares = new List<PromotionShare>();

        foreach (var promotion in promotions)
        {
            if (promotion.Type == PromotionType.FreeShipping)
            {
                var waived = shipping - shippingDiscount;

                shippingDiscount = shipping;
                shares.Add(new PromotionShare(promotion, waived));

                continue;
            }

            var amount = Math.Min(GoodsAmount(promotion, lines), remaining);

            remaining -= amount;
            shares.Add(new PromotionShare(promotion, amount));
        }

        return new PromotionOutcome(subtotal - remaining, shippingDiscount, shares);
    }

    private static decimal GoodsAmount(Promotion promotion, IReadOnlyList<PromotionLine> lines)
    {
        var eligible = Eligible(promotion, lines);

        return promotion.Type == PromotionType.Percentage
            ? Math.Round(eligible * promotion.Value / 100m, 2, MidpointRounding.AwayFromZero)
            : Math.Min(promotion.Value, eligible);
    }
}
