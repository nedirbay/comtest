using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Persistence;

namespace ComTest.Infrastructure.Identity;

public sealed class SecurityEventLogger(AppDbContext db, TimeProvider timeProvider)
{
    private const int MaxUserAgentLength = 256;

    public async Task WriteAsync(
        SecurityEventType type,
        Guid? userId,
        ClientInfo? client,
        CancellationToken cancellationToken,
        string? attemptedLogin = null)
    {
        db.SecurityEvents.Add(new SecurityEvent
        {
            Type = type,
            UserId = userId,
            IdentifierHash = attemptedLogin is null
                ? null
                : SecureTokens.Hash(attemptedLogin.Trim().ToUpperInvariant()),
            IpAddress = client?.IpAddress,
            UserAgent = Truncate(client?.UserAgent),
            OccurredAt = timeProvider.GetUtcNow(),
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    internal static string? Truncate(string? userAgent) =>
        userAgent is { Length: > MaxUserAgentLength } ? userAgent[..MaxUserAgentLength] : userAgent;
}
