using Asp.Versioning;
using ComTest.Api.Hosting;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Reporting;
using ComTest.Application.Modules.Reporting.Contracts;
using ComTest.Infrastructure.Identity.Authorization;
using ComTest.Shared.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComTest.Api.Controllers.V1;

/// <summary>What the store has sold. Figures about profit and cost need `reports.financial` on top of this.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/reports")]
[HasPermission(Permissions.ReportsRead)]
public sealed class ReportsController : TenantControllerBase
{
    [HttpGet("sales")]
    [ProducesResponseType<SalesReportDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Sales(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int utcOffsetMinutes,
        [FromServices] IQueryHandler<GetSalesReportQuery, SalesReportDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetSalesReportQuery(new ReportPeriodRequest(from, to, utcOffsetMinutes)), cancellationToken));

    [HttpGet("products")]
    [ProducesResponseType<ProductReportDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Products(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? sort,
        [FromQuery] int? limit,
        [FromServices] IQueryHandler<GetProductReportQuery, ProductReportDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetProductReportQuery(new ReportPeriodRequest(from, to), sort, limit), cancellationToken));

    [HttpGet("customers")]
    [ProducesResponseType<CustomerReportDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Customers(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? limit,
        [FromServices] IQueryHandler<GetCustomerReportQuery, CustomerReportDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetCustomerReportQuery(new ReportPeriodRequest(from, to), limit), cancellationToken));

    [HttpGet("stock")]
    [ProducesResponseType<StockReportDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Stock(
        [FromServices] IQueryHandler<GetStockReportQuery, StockReportDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetStockReportQuery(), cancellationToken));

    [HttpGet("export")]
    [EnableRateLimiting(RateLimitPolicies.ReportExport)]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK, "text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> Export(
        [FromQuery] ReportKind kind,
        [FromQuery] ReportFormat format,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int utcOffsetMinutes,
        [FromServices] IQueryHandler<ExportReportQuery, ReportFile> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ExportReportQuery(kind, format, new ReportPeriodRequest(from, to, utcOffsetMinutes)), cancellationToken);

        return result.IsFailure ? Failure(result.Error) : File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}
