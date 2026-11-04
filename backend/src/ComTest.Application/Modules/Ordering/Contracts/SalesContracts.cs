namespace ComTest.Application.Modules.Ordering.Contracts;

/// <summary>A half-open period: `From` belongs to it, `To` does not, so neighbouring periods never count an order twice.</summary>
public sealed record SalesPeriod(DateTimeOffset From, DateTimeOffset To, string Currency);

public sealed record SalesTotals(int Orders, long Units, decimal Revenue, decimal Discounts, decimal Shipping);

public sealed record SalesDay(DateOnly Date, int Orders, decimal Revenue);

public sealed record VariantSales(Guid ProductId, Guid VariantId, string Sku, string ProductName, long Units, decimal Revenue);

public sealed record BuyerSales(string Name, string Phone, int Orders, decimal Revenue, bool BoughtBefore, bool IsGuest);

/// <summary>
/// What counts as a sale, in one place: an order that was neither cancelled nor sent back, in the
/// store's currency. Reports read through this instead of each deciding that for itself.
/// </summary>
public interface ISalesReader
{
    Task<SalesTotals> GetTotalsAsync(SalesPeriod period, CancellationToken cancellationToken);

    /// <summary>Only days with sales. Days are counted in the caller's own time zone.</summary>
    Task<IReadOnlyList<SalesDay>> GetDaysAsync(SalesPeriod period, int utcOffsetMinutes, CancellationToken cancellationToken);

    Task<IReadOnlyList<VariantSales>> GetVariantSalesAsync(SalesPeriod period, CancellationToken cancellationToken);

    /// <summary>A phone number stands for one buyer, because a guest has nothing else that identifies them.</summary>
    Task<IReadOnlyList<BuyerSales>> GetBuyersAsync(SalesPeriod period, CancellationToken cancellationToken);
}
