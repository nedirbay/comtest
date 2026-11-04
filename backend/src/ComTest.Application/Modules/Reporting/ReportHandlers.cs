using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Catalog.Contracts;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Inventory.Contracts;
using ComTest.Application.Modules.Ordering.Contracts;
using ComTest.Application.Modules.Payments.Contracts;
using ComTest.Application.Modules.Reporting.Contracts;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Shared.Messaging;
using ComTest.Shared.Results;

namespace ComTest.Application.Modules.Reporting;

public sealed record ReportPeriodRequest(DateTimeOffset? From, DateTimeOffset? To, int UtcOffsetMinutes = 0);

public sealed record GetSalesReportQuery(ReportPeriodRequest Period) : IQuery<SalesReportDto>;

public sealed record GetProductReportQuery(ReportPeriodRequest Period, string? Sort, int? Limit) : IQuery<ProductReportDto>;

public sealed record GetCustomerReportQuery(ReportPeriodRequest Period, int? Limit) : IQuery<CustomerReportDto>;

public sealed record GetStockReportQuery : IQuery<StockReportDto>;

public sealed record ExportReportQuery(ReportKind Kind, ReportFormat Format, ReportPeriodRequest Period) : IQuery<ReportFile>;

internal static class ReportRules
{
    public const int DefaultDays = 30;
    public const int MaxDays = 366;
    public const int MaxOffsetMinutes = 14 * 60;
    public const int DefaultLimit = 20;
    public const int MaxLimit = 200;

    /// <summary>A file is built in memory, so its length has to have an end.</summary>
    public const int MaxExportRows = 10_000;

    public static Result<(DateTimeOffset From, DateTimeOffset To)> Resolve(ReportPeriodRequest request, DateTimeOffset now)
    {
        var to = request.To ?? now;
        var from = request.From ?? to.AddDays(-DefaultDays);

        if (from >= to || to - from > TimeSpan.FromDays(MaxDays) || Math.Abs(request.UtcOffsetMinutes) > MaxOffsetMinutes)
        {
            return Error.Validation(ErrorCodes.Report.InvalidPeriod);
        }

        return (from, to);
    }

    public static int ClampLimit(int? limit) => Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

    public static SalesFiguresDto ToFigures(SalesTotals totals) => new(
        totals.Orders,
        totals.Units,
        totals.Revenue,
        totals.Orders == 0 ? 0m : Math.Round(totals.Revenue / totals.Orders, 2, MidpointRounding.AwayFromZero),
        totals.Discounts,
        totals.Shipping);

    /// <summary>Every day of the period, as a day without sales is a fact too and a chart needs it.</summary>
    public static List<SalesDayDto> FillDays(IReadOnlyList<SalesDay> days, DateTimeOffset from, DateTimeOffset to, int utcOffsetMinutes)
    {
        var offset = TimeSpan.FromMinutes(utcOffsetMinutes);
        var byDate = days.ToDictionary(day => day.Date);
        var first = DateOnly.FromDateTime(from.ToOffset(offset).DateTime);
        var last = DateOnly.FromDateTime(to.AddTicks(-1).ToOffset(offset).DateTime);
        var result = new List<SalesDayDto>();

        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var day = byDate.GetValueOrDefault(date);

            result.Add(new SalesDayDto(date, day?.Orders ?? 0, day?.Revenue ?? 0m));
        }

        return result;
    }

    public static string MaskName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "***" : name.Trim()[0] + "***";

    public static string MaskPhone(string phone) =>
        phone.Length > 2 ? "***" + phone[^2..] : "***";
}

public sealed class ReportHandlers(
    ISalesReader sales,
    ICatalogReader catalog,
    IStockService stock,
    IOrderPayments payments,
    ITenantSettings tenantSettings,
    ICurrentUser currentUser,
    IReportFileWriter writer,
    TimeProvider timeProvider) :
    IQueryHandler<GetSalesReportQuery, SalesReportDto>,
    IQueryHandler<GetProductReportQuery, ProductReportDto>,
    IQueryHandler<GetCustomerReportQuery, CustomerReportDto>,
    IQueryHandler<GetStockReportQuery, StockReportDto>,
    IQueryHandler<ExportReportQuery, ReportFile>
{
    public async Task<Result<SalesReportDto>> HandleAsync(GetSalesReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (await tenantSettings.GetCurrentAsync(cancellationToken) is not { } settings)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var period = ReportRules.Resolve(query.Period, timeProvider.GetUtcNow());

        if (period.IsFailure)
        {
            return period.Error;
        }

        var (from, to) = period.Value;
        var current = new SalesPeriod(from, to, settings.DefaultCurrency);

        var figures = await sales.GetTotalsAsync(current, cancellationToken);
        var previous = await sales.GetTotalsAsync(new SalesPeriod(from - (to - from), from, settings.DefaultCurrency), cancellationToken);
        var days = await sales.GetDaysAsync(current, query.Period.UtcOffsetMinutes, cancellationToken);
        var refunded = await payments.GetRefundedAsync(from, to, settings.DefaultCurrency, cancellationToken);

        return new SalesReportDto(
            from,
            to,
            settings.DefaultCurrency,
            ReportRules.ToFigures(figures),
            ReportRules.ToFigures(previous),
            refunded,
            ReportRules.FillDays(days, from, to, query.Period.UtcOffsetMinutes));
    }

    public async Task<Result<ProductReportDto>> HandleAsync(GetProductReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Sort is not (null or "" or "revenue" or "units"))
        {
            return Error.Validation(ErrorCodes.Common.InvalidSort);
        }

        return await LoadProductsAsync(query.Period, query.Sort is "units", ReportRules.ClampLimit(query.Limit), cancellationToken);
    }

    public async Task<Result<CustomerReportDto>> HandleAsync(GetCustomerReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await LoadCustomersAsync(query.Period, ReportRules.ClampLimit(query.Limit), cancellationToken);
    }

    public async Task<Result<StockReportDto>> HandleAsync(GetStockReportQuery query, CancellationToken cancellationToken)
    {
        if (await tenantSettings.GetCurrentAsync(cancellationToken) is not { } settings)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var levels = await stock.GetLevelsAsync(cancellationToken);
        var costs = await LoadCostsAsync(levels.Select(level => level.VariantId).ToList(), cancellationToken);

        return new StockReportDto(
            levels.Count,
            levels.Sum(level => (long)level.OnHand),
            levels.Sum(level => (long)level.Reserved),
            levels.Count(level => level.IsLow),
            levels.Count(level => level.Available <= 0),
            costs is null ? null : levels.Sum(level => level.OnHand * costs.GetValueOrDefault(level.VariantId)),
            settings.DefaultCurrency);
    }

    public async Task<Result<ReportFile>> HandleAsync(ExportReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var table = query.Kind switch
        {
            ReportKind.Sales => await BuildSalesTableAsync(query.Period, cancellationToken),
            ReportKind.Products => await BuildProductsTableAsync(query.Period, cancellationToken),
            ReportKind.Customers => await BuildCustomersTableAsync(query.Period, cancellationToken),
            ReportKind.Stock => await BuildStockTableAsync(cancellationToken),
            _ => Result.Failure<ReportTable>(Error.Validation(ErrorCodes.Common.BadRequest)),
        };

        if (table.IsFailure)
        {
            return table.Error;
        }

        return writer.Write(table.Value, query.Format);
    }

    private bool CanSeeProfit => currentUser.HasPermission(Permissions.ReportsFinancial);

    /// <returns>Cost per variant, or null when the caller may not see costs.</returns>
    private async Task<IReadOnlyDictionary<Guid, decimal>?> LoadCostsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken) =>
        CanSeeProfit ? await catalog.GetCostsAsync(variantIds, cancellationToken) : null;

    private async Task<Result<ProductReportDto>> LoadProductsAsync(ReportPeriodRequest request, bool byUnits, int limit, CancellationToken cancellationToken)
    {
        if (await tenantSettings.GetCurrentAsync(cancellationToken) is not { } settings)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var period = ReportRules.Resolve(request, timeProvider.GetUtcNow());

        if (period.IsFailure)
        {
            return period.Error;
        }

        var (from, to) = period.Value;
        var rows = await sales.GetVariantSalesAsync(new SalesPeriod(from, to, settings.DefaultCurrency), cancellationToken);
        var costs = await LoadCostsAsync(rows.Select(row => row.VariantId).ToList(), cancellationToken);

        var products = rows
            .GroupBy(row => row.ProductId)
            .Select(group => new ProductSalesDto(
                group.Key,
                group.Max(row => row.ProductName)!,
                group.Sum(row => row.Units),
                group.Sum(row => row.Revenue),
                costs is null ? null : group.Sum(row => row.Revenue - (row.Units * costs.GetValueOrDefault(row.VariantId)))))
            .ToList();

        var ordered = byUnits
            ? products.OrderByDescending(item => item.Units).ThenByDescending(item => item.Revenue)
            : products.OrderByDescending(item => item.Revenue).ThenByDescending(item => item.Units);

        return new ProductReportDto(from, to, settings.DefaultCurrency, products.Count, ordered.ThenBy(item => item.ProductId).Take(limit).ToList());
    }

    private async Task<Result<CustomerReportDto>> LoadCustomersAsync(ReportPeriodRequest request, int limit, CancellationToken cancellationToken)
    {
        if (await tenantSettings.GetCurrentAsync(cancellationToken) is not { } settings)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var period = ReportRules.Resolve(request, timeProvider.GetUtcNow());

        if (period.IsFailure)
        {
            return period.Error;
        }

        var (from, to) = period.Value;
        var buyers = await sales.GetBuyersAsync(new SalesPeriod(from, to, settings.DefaultCurrency), cancellationToken);
        var canSeeContacts = currentUser.HasPermission(Permissions.CustomersRead);

        return new CustomerReportDto(
            from,
            to,
            settings.DefaultCurrency,
            buyers.Count,
            buyers.Count(buyer => !buyer.BoughtBefore),
            buyers.Count(buyer => buyer.BoughtBefore),
            buyers
                .OrderByDescending(buyer => buyer.Revenue)
                .ThenBy(buyer => buyer.Phone, StringComparer.Ordinal)
                .Take(limit)
                .Select(buyer => new BuyerDto(
                    canSeeContacts ? buyer.Name : ReportRules.MaskName(buyer.Name),
                    canSeeContacts ? buyer.Phone : ReportRules.MaskPhone(buyer.Phone),
                    buyer.Orders,
                    buyer.Revenue,
                    buyer.IsGuest))
                .ToList());
    }

    private async Task<Result<ReportTable>> BuildSalesTableAsync(ReportPeriodRequest request, CancellationToken cancellationToken)
    {
        var report = await HandleAsync(new GetSalesReportQuery(request), cancellationToken);

        if (report.IsFailure)
        {
            return report.Error;
        }

        return new ReportTable(
            "sales",
            [ReportText.Date, ReportText.Orders, ReportText.Revenue(report.Value.Currency)],
            report.Value.Days.Select(day => (IReadOnlyList<object?>)[day.Date, day.Orders, day.Revenue]).ToList());
    }

    private async Task<Result<ReportTable>> BuildProductsTableAsync(ReportPeriodRequest request, CancellationToken cancellationToken)
    {
        var report = await LoadProductsAsync(request, byUnits: false, ReportRules.MaxExportRows, cancellationToken);

        if (report.IsFailure)
        {
            return report.Error;
        }

        var currency = report.Value.Currency;

        return CanSeeProfit
            ? new ReportTable(
                "products",
                [ReportText.Product, ReportText.Units, ReportText.Revenue(currency), ReportText.Profit(currency)],
                report.Value.Items.Select(item => (IReadOnlyList<object?>)[item.Name, item.Units, item.Revenue, item.EstimatedProfit]).ToList())
            : new ReportTable(
                "products",
                [ReportText.Product, ReportText.Units, ReportText.Revenue(currency)],
                report.Value.Items.Select(item => (IReadOnlyList<object?>)[item.Name, item.Units, item.Revenue]).ToList());
    }

    private async Task<Result<ReportTable>> BuildCustomersTableAsync(ReportPeriodRequest request, CancellationToken cancellationToken)
    {
        var report = await LoadCustomersAsync(request, ReportRules.MaxExportRows, cancellationToken);

        if (report.IsFailure)
        {
            return report.Error;
        }

        return new ReportTable(
            "customers",
            [ReportText.Customer, ReportText.Phone, ReportText.Orders, ReportText.Revenue(report.Value.Currency)],
            report.Value.Top.Select(buyer => (IReadOnlyList<object?>)[buyer.Name, buyer.Phone, buyer.Orders, buyer.Revenue]).ToList());
    }

    private async Task<Result<ReportTable>> BuildStockTableAsync(CancellationToken cancellationToken)
    {
        if (await tenantSettings.GetCurrentAsync(cancellationToken) is not { } settings)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var levels = (await stock.GetLevelsAsync(cancellationToken)).Take(ReportRules.MaxExportRows).ToList();
        var variantIds = levels.Select(level => level.VariantId).ToList();
        var variants = await catalog.GetVariantsAsync(variantIds, cancellationToken);
        var costs = await LoadCostsAsync(variantIds, cancellationToken);

        List<string> columns = [ReportText.Sku, ReportText.Product, ReportText.OnHand, ReportText.ReservedUnits, ReportText.Available];

        if (costs is not null)
        {
            columns.Add(ReportText.StockValue(settings.DefaultCurrency));
        }

        var rows = levels
            .Select(level =>
            {
                var variant = variants.GetValueOrDefault(level.VariantId);
                List<object?> row = [variant?.Sku, variant?.ProductName, level.OnHand, level.Reserved, level.Available];

                if (costs is not null)
                {
                    row.Add(level.OnHand * costs.GetValueOrDefault(level.VariantId));
                }

                return (IReadOnlyList<object?>)row;
            })
            .ToList();

        return new ReportTable("stock", columns, rows);
    }
}
