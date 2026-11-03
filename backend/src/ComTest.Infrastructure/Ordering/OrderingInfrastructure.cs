using ComTest.Application.Modules.Ordering;
using ComTest.Application.Modules.Ordering.Contracts;
using ComTest.Domain.Modules.Ordering;
using ComTest.Infrastructure.Persistence;
using ComTest.Shared.Messaging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ComTest.Infrastructure.Ordering;

/// <summary>Tracking links are signed and encrypted, so that nobody can make one for another order.</summary>
public sealed class OrderTrackingTokens(IDataProtectionProvider dataProtection) : IOrderTrackingTokens
{
    private const char Separator = '|';

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(180);

    private readonly ITimeLimitedDataProtector _protector =
        dataProtection.CreateProtector("ComTest.Ordering.TrackingToken.v1").ToTimeLimitedDataProtector();

    public string Create(Guid tenantId, Guid orderId) => _protector.Protect($"{tenantId:N}{Separator}{orderId:N}", Lifetime);

    public (Guid TenantId, Guid OrderId)? Read(string token)
    {
        try
        {
            var parts = _protector.Unprotect(token).Split(Separator);

            return parts.Length == 2 && Guid.TryParse(parts[0], out var tenantId) && Guid.TryParse(parts[1], out var orderId)
                ? (tenantId, orderId)
                : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}

/// <summary>Cancels orders nobody confirmed in time, so that they stop holding stock, and clears abandoned guest carts.</summary>
public sealed partial class SalesHousekeeping(
    IServiceScopeFactory scopeFactory,
    PlatformDataAccess platformData,
    TimeProvider timeProvider,
    ILogger<SalesHousekeeping> logger)
{
    private const int BatchSize = 200;

    private static readonly TimeSpan GuestCartLifetime = TimeSpan.FromDays(30);

    public async Task<int> ExpirePendingOrdersAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var expired = await platformData.RunAsync(data => data.Orders
            .AsNoTracking()
            .Where(order => order.Status == OrderStatus.Pending && order.ExpiresAt != null && order.ExpiresAt <= now)
            .OrderBy(order => order.ExpiresAt)
            .Take(BatchSize)
            .Select(order => new { order.TenantId, order.Id })
            .ToListAsync(cancellationToken));

        var cancelled = 0;

        foreach (var order in expired)
        {
            // Each order in its own scope, so that one failure leaves the rest unaffected.
            await using var scope = scopeFactory.CreateAsyncScope();

            scope.ServiceProvider.GetRequiredService<TenantContext>().Use(order.TenantId);

            try
            {
                var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ExpireOrderCommand, bool>>();
                var result = await handler.HandleAsync(new ExpireOrderCommand(order.Id), cancellationToken);

                if (result is { IsSuccess: true, Value: true })
                {
                    cancelled++;
                }
            }
            catch (DbUpdateConcurrencyException exception)
            {
                // Staff changed the order at the same moment. The next run looks at it again.
                LogExpiryConflict(logger, exception, order.Id);
            }
        }

        return cancelled;
    }

    public async Task<int> DeleteAbandonedGuestCartsAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - GuestCartLifetime;

        return await platformData.RunAsync(data => data.Carts
            .Where(cart => cart.UserId == null && cart.LastActivityAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} changed while expiring it, so it is left for the next run")]
    private static partial void LogExpiryConflict(ILogger logger, Exception exception, Guid orderId);
}

public sealed partial class SalesHousekeepingService(SalesHousekeeping housekeeping, ILogger<SalesHousekeepingService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await housekeeping.ExpirePendingOrdersAsync(stoppingToken);
                await housekeeping.DeleteAbandonedGuestCartsAsync(stoppingToken);
            }
#pragma warning disable CA1031 // A failed run is retried at the next interval.
            catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
            {
                LogRunFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Sales housekeeping failed")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}

public static class OrderingServiceRegistration
{
    public static IServiceCollection AddOrderingInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<CheckoutOptions>().BindConfiguration(CheckoutOptions.SectionName);
        services.AddSingleton<IOrderTrackingTokens, OrderTrackingTokens>();
        services.AddSingleton<SalesHousekeeping>();
        services.AddHostedService<SalesHousekeepingService>();

        return services;
    }
}
