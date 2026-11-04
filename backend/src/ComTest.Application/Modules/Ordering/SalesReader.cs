using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Ordering.Contracts;
using ComTest.Domain.Modules.Ordering;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Ordering;

public sealed class SalesReader(IApplicationDbContext db) : ISalesReader
{
    public async Task<SalesTotals> GetTotalsAsync(SalesPeriod period, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        var orders = Sales(period);

        var totals = await orders
            .GroupBy(_ => 1)
            .Select(group => new SalesTotals(
                group.Count(),
                0,
                group.Sum(order => order.GrandTotal.Amount),
                group.Sum(order => order.Discount.Amount),
                group.Sum(order => order.Shipping.Amount)))
            .FirstOrDefaultAsync(cancellationToken);

        if (totals is null)
        {
            return new SalesTotals(0, 0, 0m, 0m, 0m);
        }

        var units = await orders.SelectMany(order => order.Items).SumAsync(item => (long)item.Quantity, cancellationToken);

        return totals with { Units = units };
    }

    public async Task<IReadOnlyList<SalesDay>> GetDaysAsync(SalesPeriod period, int utcOffsetMinutes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        var rows = await Sales(period)
            .GroupBy(order => order.PlacedAt.AddMinutes(utcOffsetMinutes).Date)
            .Select(group => new { Date = group.Key, Orders = group.Count(), Revenue = group.Sum(order => order.GrandTotal.Amount) })
            .OrderBy(row => row.Date)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new SalesDay(DateOnly.FromDateTime(row.Date), row.Orders, row.Revenue)).ToList();
    }

    public async Task<IReadOnlyList<VariantSales>> GetVariantSalesAsync(SalesPeriod period, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        return await Sales(period)
            .SelectMany(order => order.Items)
            .GroupBy(item => new { item.ProductId, item.ProductVariantId })
            .Select(group => new VariantSales(
                group.Key.ProductId,
                group.Key.ProductVariantId,
                group.Max(item => item.Sku)!,
                group.Max(item => item.ProductName)!,
                group.Sum(item => (long)item.Quantity),
                group.Sum(item => item.LineTotal.Amount)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BuyerSales>> GetBuyersAsync(SalesPeriod period, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        var buyers = await Sales(period)
            .GroupBy(order => order.Contact.Phone)
            .Select(group => new
            {
                Phone = group.Key,
                Name = group.Max(order => order.Contact.Name)!,
                Orders = group.Count(),
                Revenue = group.Sum(order => order.GrandTotal.Amount),
                IsGuest = group.All(order => order.CustomerUserId == null),
            })
            .ToListAsync(cancellationToken);

        var phones = buyers.Select(buyer => buyer.Phone).ToList();

        var earlier = (await db.Orders
            .AsNoTracking()
            .Where(order => order.PlacedAt < period.From && order.Status != OrderStatus.Cancelled && phones.Contains(order.Contact.Phone))
            .Select(order => order.Contact.Phone)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        return buyers
            .Select(buyer => new BuyerSales(buyer.Name, buyer.Phone, buyer.Orders, buyer.Revenue, earlier.Contains(buyer.Phone), buyer.IsGuest))
            .ToList();
    }

    private IQueryable<Order> Sales(SalesPeriod period) =>
        db.Orders.AsNoTracking().Where(order =>
            order.PlacedAt >= period.From
            && order.PlacedAt < period.To
            && order.GrandTotal.Currency == period.Currency
            && order.Status != OrderStatus.Cancelled
            && order.Status != OrderStatus.Returned);
}
