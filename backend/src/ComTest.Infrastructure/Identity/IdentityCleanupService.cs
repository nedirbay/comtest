using ComTest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ComTest.Infrastructure.Identity;

/// <summary>Removes expired sessions, refresh tokens and phone codes, once they are of no further use.</summary>
public sealed partial class IdentityCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<IdentityCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    // Consumed tokens are kept past expiry so that reuse of a stolen token is still detected.
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = timeProvider.GetUtcNow() - Retention;

        await db.RefreshTokens.Where(token => token.ExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        await db.UserSessions.Where(session => session.ExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        await db.PhoneVerificationCodes.Where(code => code.ExpiresAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanAsync(stoppingToken);
            }
#pragma warning disable CA1031 // A failed run is retried at the next interval.
            catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
            {
                LogCleanupFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Identity cleanup failed")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
