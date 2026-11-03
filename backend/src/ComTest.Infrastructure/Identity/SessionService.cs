using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Persistence;
using ComTest.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Infrastructure.Identity;

public sealed class SessionService(
    AppDbContext db,
    SessionManager sessions,
    SecurityEventLogger securityEvents,
    TimeProvider timeProvider) : ISessionService
{
    public async Task<IReadOnlyList<SessionDto>> ListAsync(Guid userId, Guid? currentSessionId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        return await db.UserSessions
            .AsNoTracking()
            .Where(session => session.UserId == userId && session.RevokedAt == null && session.ExpiresAt > now)
            .OrderByDescending(session => session.LastSeenAt)
            .Select(session => new SessionDto(
                session.Id,
                session.Id == currentSessionId,
                session.IpAddress,
                session.UserAgent,
                session.CreatedAt,
                session.LastSeenAt,
                session.ExpiresAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>The owner check is part of the lookup: another user's session looks like a missing one.</summary>
    public async Task<Result> RevokeAsync(Guid userId, Guid sessionId, ClientInfo client, CancellationToken cancellationToken)
    {
        var owned = await db.UserSessions.AnyAsync(
            session => session.Id == sessionId && session.UserId == userId && session.RevokedAt == null,
            cancellationToken);

        if (!owned)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Auth.SessionNotFound));
        }

        await sessions.RevokeAsync(sessionId, SessionRevocationReasons.UserRequest, cancellationToken);
        await securityEvents.WriteAsync(SecurityEventType.SessionRevoked, userId, client, cancellationToken);

        return Result.Success();
    }

    public async Task RevokeOthersAsync(Guid userId, Guid? currentSessionId, ClientInfo client, CancellationToken cancellationToken)
    {
        await sessions.RevokeAllAsync(userId, SessionRevocationReasons.UserRequest, cancellationToken, currentSessionId);
        await securityEvents.WriteAsync(SecurityEventType.OtherSessionsRevoked, userId, client, cancellationToken);
    }
}
