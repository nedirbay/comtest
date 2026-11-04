using ComTest.Domain.Modules.Promotions;
using ComTest.Shared.Results;

namespace ComTest.Application.Modules.Promotions.Contracts;

public sealed record PromotionDto(
    Guid Id,
    string? Code,
    string Name,
    PromotionType Type,
    decimal Value,
    string Currency,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    decimal? MinSubtotal,
    Guid? CategoryId,
    int? UsageLimit,
    int UsageCount,
    int? PerCustomerLimit,
    bool CombinesWithOthers,
    bool IsActive);

public sealed record NewPromotionCodeDto(string Code);

public sealed record PromotionCartLine(Guid VariantId, decimal LineTotal);

/// <summary>Everything a discount can depend on. Prices come from the server's own cart, never from the buyer.</summary>
public sealed record PromotionRequest(string? Code, IReadOnlyList<PromotionCartLine> Lines, decimal Shipping, string Currency, Guid? UserId, string? Phone);

public sealed record AppliedPromotion(Guid PromotionId, string Name, string? Code, decimal Amount);

/// <summary>CodeIgnored means the buyer's code works, but a better offer took its place.</summary>
public sealed record PromotionQuote(decimal GoodsDiscount, decimal ShippingDiscount, IReadOnlyList<AppliedPromotion> Applied, bool CodeIgnored)
{
    public static PromotionQuote None { get; } = new(0m, 0m, [], false);
}

/// <summary>How the checkout reaches discounts, without knowing how they are worked out.</summary>
public interface IPromotionEngine
{
    Task<Result<PromotionQuote>> QuoteAsync(PromotionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Takes one use of each promotion for an order. It runs inside the order's transaction, so
    /// a failed order gives the uses back. Nothing is saved until the caller saves.
    /// </summary>
    Task<Result> RedeemAsync(PromotionQuote quote, Guid orderId, Guid? userId, string? phone, CancellationToken cancellationToken);

    /// <summary>A cancelled order gives its uses back. Safe to call for an order that used none.</summary>
    Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken);
}
