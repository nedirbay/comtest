using System.Globalization;
using System.Threading.RateLimiting;
using ComTest.Application.Abstractions;
using ComTest.Infrastructure.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ComTest.Api.Hosting;

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string AccountAction = "account-action";
    public const string Refresh = "refresh";
    public const string Sms = "sms";
    public const string Checkout = "checkout";
    public const string Tracking = "tracking";
    public const string ReportExport = "report-export";

    private const int WindowSegments = 6;

    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromMinutes(1);

    /// <summary>
    /// A limiter keeps its counts per API instance. Shared counters across instances are
    /// planned with Redis (task B18).
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitSettings>>((options, settingsAccessor) =>
            {
                var settings = settingsAccessor.Value;

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, _) =>
                {
                    // Sliding-window limiters do not report a wait time, so give a safe upper estimate.
                    var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var reported)
                        ? reported
                        : DefaultRetryAfter;

                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                    return ValueTask.CompletedTask;
                };

                options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                    PartitionedRateLimiter.Create<HttpContext, string>(context =>
                        SlidingWindow(CallerKey(context), settings.GlobalPerMinute, TimeSpan.FromMinutes(1))),
                    PartitionedRateLimiter.Create<HttpContext, string>(context =>
                        context.RequestServices.GetRequiredService<ITenantContext>().TenantId is { } tenantId
                            ? SlidingWindow("tenant:" + tenantId.ToString("N"), settings.TenantPerMinute, TimeSpan.FromMinutes(1))
                            : RateLimitPartition.GetNoLimiter("no-tenant")));

                options.AddPolicy(Login, context =>
                    SlidingWindow(IpKey(context), settings.LoginPerMinute, TimeSpan.FromMinutes(1)));
                options.AddPolicy(AccountAction, context =>
                    SlidingWindow(IpKey(context), settings.AccountActionPerMinute, TimeSpan.FromMinutes(1)));
                options.AddPolicy(Refresh, context =>
                    SlidingWindow(IpKey(context), settings.RefreshPerMinute, TimeSpan.FromMinutes(1)));
                options.AddPolicy(Sms, context =>
                    SlidingWindow(CallerKey(context), settings.SmsPerHour, TimeSpan.FromHours(1)));
                options.AddPolicy(Checkout, context =>
                    SlidingWindow(CallerKey(context), settings.CheckoutPerMinute, TimeSpan.FromMinutes(1)));
                options.AddPolicy(Tracking, context =>
                    SlidingWindow(IpKey(context), settings.TrackingPerMinute, TimeSpan.FromMinutes(1)));
                options.AddPolicy(ReportExport, context =>
                    SlidingWindow(CallerKey(context), settings.ReportExportPerMinute, TimeSpan.FromMinutes(1)));
            });

        return services;
    }

    private static RateLimitPartition<string> SlidingWindow(string key, int permits, TimeSpan window) =>
        RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window,
            SegmentsPerWindow = WindowSegments,
            QueueLimit = 0,
        });

    private static string IpKey(HttpContext context) =>
        "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private static string CallerKey(HttpContext context) =>
        context.User.FindFirst(AppClaims.Subject)?.Value is { } userId ? "user:" + userId : IpKey(context);
}
