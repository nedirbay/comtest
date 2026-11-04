namespace ComTest.Application.Modules.Reporting.Contracts;

public sealed record SalesFiguresDto(int Orders, long Units, decimal Revenue, decimal AverageOrderValue, decimal Discounts, decimal Shipping);

public sealed record SalesDayDto(DateOnly Date, int Orders, decimal Revenue);

/// <summary>Previous is the period of the same length just before this one, for comparison.</summary>
public sealed record SalesReportDto(
    DateTimeOffset From,
    DateTimeOffset To,
    string Currency,
    SalesFiguresDto Figures,
    SalesFiguresDto Previous,
    decimal Refunded,
    IReadOnlyList<SalesDayDto> Days);

/// <summary>EstimatedProfit is filled in only for those allowed `reports.financial`.</summary>
public sealed record ProductSalesDto(Guid ProductId, string Name, long Units, decimal Revenue, decimal? EstimatedProfit);

public sealed record ProductReportDto(DateTimeOffset From, DateTimeOffset To, string Currency, long TotalCount, IReadOnlyList<ProductSalesDto> Items);

/// <summary>Name and phone are masked for those without `customers.read`.</summary>
public sealed record BuyerDto(string Name, string Phone, int Orders, decimal Revenue, bool IsGuest);

public sealed record CustomerReportDto(
    DateTimeOffset From,
    DateTimeOffset To,
    string Currency,
    int Buyers,
    int NewBuyers,
    int ReturningBuyers,
    IReadOnlyList<BuyerDto> Top);

/// <summary>StockValue is filled in only for those allowed `reports.financial`.</summary>
public sealed record StockReportDto(int Variants, long OnHand, long Reserved, int LowStock, int OutOfStock, decimal? StockValue, string Currency);

public enum ReportKind
{
    Sales,
    Products,
    Customers,
    Stock,
}

public enum ReportFormat
{
    Csv,
    Xlsx,
}

public sealed record ReportFile(byte[] Content, string ContentType, string FileName);

/// <summary>A report ready to be written out. A cell is text, a number, a date or empty.</summary>
public sealed record ReportTable(string Name, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);

public interface IReportFileWriter
{
    ReportFile Write(ReportTable table, ReportFormat format);
}
