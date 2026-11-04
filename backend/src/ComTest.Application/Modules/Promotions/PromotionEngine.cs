using System.Security.Cryptography;
using System.Text;
using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Catalog.Contracts;
using ComTest.Application.Modules.Promotions.Contracts;
using ComTest.Domain.Modules.Promotions;
using ComTest.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Promotions;

public sealed class PromotionEngine(IApplicationDbContext db, ICatalogReader catalog, TimeProvider timeProvider) : IPromotionEngine
{
    public async Task<Result<PromotionQuote>> QuoteAsync(PromotionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();
        var code = string.IsNullOrWhiteSpace(request.Code) ? null : PromotionCode.Normalize(request.Code);

        var running = await db.Promotions
            .AsNoTracking()
            .Where(item => item.IsActive && item.Currency == request.Currency && (item.Code == null || item.Code == code))
            .ToListAsync(cancellationToken);

        running = running.Where(item => item.IsRunning(now)).ToList();

        var paths = await catalog.GetCategoryPathsAsync(request.Lines.Select(line => line.VariantId).ToList(), cancellationToken);
        var lines = request.Lines
            .Select(line => new PromotionLine(line.LineTotal, paths.GetValueOrDefault(line.VariantId) ?? new HashSet<Guid>()))
            .ToList();
        var subtotal = lines.Sum(line => line.LineTotal);

        Promotion? withCode = null;

        if (code is not null)
        {
            withCode = running.Find(item => item.Code == code);

            // One answer for a code that never existed and one that ended, so nothing is learned from trying.
            if (withCode is null || await IsUsedUpAsync(withCode, request.UserId, request.Phone, cancellationToken))
            {
                return Error.Validation(ErrorCodes.Promotion.CodeNotValid);
            }

            if (withCode.MinSubtotal is { } minimum && subtotal < minimum)
            {
                return Error.Validation(ErrorCodes.Promotion.MinSubtotalNotMet);
            }

            if (withCode.CategoryId is not null && PromotionCalculator.Eligible(withCode, lines) <= 0m)
            {
                return Error.Validation(ErrorCodes.Promotion.NotApplicable);
            }
        }

        var automatic = new List<Promotion>();

        foreach (var promotion in running.Where(item => item.IsAutomatic))
        {
            var fits = (promotion.MinSubtotal is null || subtotal >= promotion.MinSubtotal)
                && (promotion.CategoryId is null || PromotionCalculator.Eligible(promotion, lines) > 0m);

            if (fits && !await IsUsedUpAsync(promotion, request.UserId, request.Phone, cancellationToken))
            {
                automatic.Add(promotion);
            }
        }

        var outcome = PromotionCalculator.Best(withCode, automatic, lines, request.Shipping);

        return new PromotionQuote(
            outcome.GoodsDiscount,
            outcome.ShippingDiscount,
            outcome.Shares.Select(share => new AppliedPromotion(share.Promotion.Id, share.Promotion.Name, share.Promotion.Code, share.Amount)).ToList(),
            withCode is not null && outcome.Shares.All(share => share.Promotion.Id != withCode.Id));
    }

    public async Task<Result> RedeemAsync(PromotionQuote quote, Guid orderId, Guid? userId, string? phone, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);

        foreach (var applied in quote.Applied)
        {
            // The check and the count happen as one step in the database, so two buyers cannot both take the last use.
            var taken = await db.Promotions
                .Where(item => item.Id == applied.PromotionId && item.IsActive && (item.UsageLimit == null || item.UsageCount < item.UsageLimit))
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsageCount, item => item.UsageCount + 1), cancellationToken);

            if (taken == 0)
            {
                return Result.Failure(Error.Conflict(ErrorCodes.Promotion.CodeNotValid));
            }

            var limit = await db.Promotions.AsNoTracking()
                .Where(item => item.Id == applied.PromotionId)
                .Select(item => item.PerCustomerLimit)
                .FirstAsync(cancellationToken);

            var key = CustomerKey(userId, phone);
            int? sequence = null;

            if (limit is { } perCustomer)
            {
                var used = await db.PromotionRedemptions.CountAsync(item => item.PromotionId == applied.PromotionId && item.CustomerKey == key, cancellationToken);

                if (used >= perCustomer)
                {
                    return Result.Failure(Error.Conflict(ErrorCodes.Promotion.CodeNotValid));
                }

                sequence = used + 1;
            }

            db.PromotionRedemptions.Add(new PromotionRedemption(applied.PromotionId, orderId, key, sequence, applied.Amount));
        }

        return Result.Success();
    }

    public async Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var redemptions = await db.PromotionRedemptions.Where(item => item.OrderId == orderId).ToListAsync(cancellationToken);

        foreach (var redemption in redemptions)
        {
            await db.Promotions
                .Where(item => item.Id == redemption.PromotionId && item.UsageCount > 0)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsageCount, item => item.UsageCount - 1), cancellationToken);

            db.PromotionRedemptions.Remove(redemption);
        }
    }

    private async Task<bool> IsUsedUpAsync(Promotion promotion, Guid? userId, string? phone, CancellationToken cancellationToken)
    {
        if (promotion.PerCustomerLimit is not { } limit || (userId is null && phone is null))
        {
            return false;
        }

        var key = CustomerKey(userId, phone);
        var used = await db.PromotionRedemptions.CountAsync(item => item.PromotionId == promotion.Id && item.CustomerKey == key, cancellationToken);

        return used >= limit;
    }

    /// <summary>A signed-in buyer is their account. A guest has only the phone number they gave.</summary>
    private static string CustomerKey(Guid? userId, string? phone)
    {
        var identity = userId is { } id ? "user:" + id.ToString("N") : "phone:" + phone;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
}
