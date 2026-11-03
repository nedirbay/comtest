using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Persistence;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComTest.Infrastructure.Identity;

public static class SessionRevocationReasons
{
    public const string Logout = "logout";
    public const string UserRequest = "user_request";
    public const string PasswordChanged = "password_changed";
    public const string PasswordReset = "password_reset";
    public const string TwoFactorDisabled = "two_factor_disabled";
    public const string TokenReuse = "token_reuse";
    public const string AccountUnavailable = "account_unavailable";
}

/// <summary>Creates, rotates and revokes sessions and their refresh tokens.</summary>
public sealed class SessionManager(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    PlatformDataAccess platformData,
    AccessTokenIssuer accessTokens,
    SecurityEventLogger securityEvents,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider)
{
    public async Task<AuthTokens> StartAsync(ApplicationUser user, bool mfa, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(client);

        var now = timeProvider.GetUtcNow();
        var session = new UserSession
        {
            UserId = user.Id,
            Mfa = mfa,
            ActiveTenantId = await FindOnlyTenantAsync(user.Id, cancellationToken),
            IpAddress = client.IpAddress,
            UserAgent = SecurityEventLogger.Truncate(client.UserAgent),
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(options.Value.SessionMaxDays),
        };

        db.UserSessions.Add(session);

        return await IssueTokensAsync(user, session, now, cancellationToken);
    }

    public async Task<Result<AuthTokens>> RotateAsync(string rawToken, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var invalid = Error.Unauthorized(ErrorCodes.Auth.RefreshInvalid);
        var now = timeProvider.GetUtcNow();
        var hash = SecureTokens.Hash(rawToken);

        var token = await db.RefreshTokens
            .Include(candidate => candidate.Session)
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);

        if (token is null)
        {
            return invalid;
        }

        var session = token.Session;

        if (token.ConsumedAt is { } consumedAt)
        {
            if (now - consumedAt <= TimeSpan.FromSeconds(options.Value.RefreshReuseGraceSeconds))
            {
                return Error.Conflict(ErrorCodes.Auth.RefreshConflict);
            }

            await HandleReuseAsync(session, client, cancellationToken);

            return invalid;
        }

        if (session.RevokedAt is not null || session.ExpiresAt <= now || token.ExpiresAt <= now)
        {
            return invalid;
        }

        var user = await users.FindByIdAsync(session.UserId.ToString());

        if (user is null || await users.IsLockedOutAsync(user))
        {
            await RevokeAsync(session.Id, SessionRevocationReasons.AccountUnavailable, cancellationToken);

            return invalid;
        }

        // Atomic claim: of any concurrent requests with this token, exactly one succeeds.
        var claimed = await db.RefreshTokens
            .Where(candidate => candidate.Id == token.Id && candidate.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.ConsumedAt, now), cancellationToken);

        if (claimed == 0)
        {
            return Error.Conflict(ErrorCodes.Auth.RefreshConflict);
        }

        token.ConsumedAt = now;
        session.LastSeenAt = now;
        session.IpAddress = client.IpAddress;
        session.UserAgent = SecurityEventLogger.Truncate(client.UserAgent);

        return await IssueTokensAsync(user, session, now, cancellationToken);
    }

    public async Task<Guid?> RevokeByTokenAsync(string rawToken, string reason, CancellationToken cancellationToken)
    {
        var hash = SecureTokens.Hash(rawToken);

        var session = await db.RefreshTokens
            .Where(candidate => candidate.TokenHash == hash)
            .Select(candidate => new { candidate.SessionId, candidate.Session.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return null;
        }

        await RevokeAsync(session.SessionId, reason, cancellationToken);

        return session.UserId;
    }

    public async Task<bool> RevokeAsync(Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var affected = await db.UserSessions
            .Where(session => session.Id == sessionId && session.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.RevokedAt, now)
                    .SetProperty(session => session.RevokedReason, reason),
                cancellationToken);

        return affected > 0;
    }

    public async Task RevokeAllAsync(Guid userId, string reason, CancellationToken cancellationToken, Guid? exceptSessionId = null)
    {
        var now = timeProvider.GetUtcNow();

        await db.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null && session.Id != exceptSessionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.RevokedAt, now)
                    .SetProperty(session => session.RevokedReason, reason),
                cancellationToken);
    }

    public async Task MarkMfaAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await db.UserSessions
            .Where(session => session.Id == sessionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.Mfa, true), cancellationToken);

    /// <summary>A consumed token came back, so it may be stolen: end every session of that user.</summary>
    private async Task HandleReuseAsync(UserSession session, ClientInfo client, CancellationToken cancellationToken)
    {
        await RevokeAllAsync(session.UserId, SessionRevocationReasons.TokenReuse, cancellationToken);

        var user = await users.FindByIdAsync(session.UserId.ToString());

        if (user is not null)
        {
            await users.UpdateSecurityStampAsync(user);
        }

        await securityEvents.WriteAsync(SecurityEventType.RefreshTokenReuseDetected, session.UserId, client, cancellationToken);
    }

    private async Task<AuthTokens> IssueTokensAsync(ApplicationUser user, UserSession session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var rawToken = SecureTokens.NewRefreshToken();
        var requestedExpiry = now.AddDays(settings.RefreshTokenDays);
        var refreshExpiresAt = requestedExpiry < session.ExpiresAt ? requestedExpiry : session.ExpiresAt;

        db.RefreshTokens.Add(new RefreshToken
        {
            SessionId = session.Id,
            TokenHash = SecureTokens.Hash(rawToken),
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt,
        });

        await db.SaveChangesAsync(cancellationToken);

        var access = await accessTokens.IssueAsync(user, session, cancellationToken);

        return new AuthTokens(
            access.Token,
            access.ExpiresAt,
            rawToken,
            refreshExpiresAt,
            TwoFactorSetupRequired: !user.TwoFactorEnabled && await IsPrivilegedAsync(user, cancellationToken));
    }

    /// <summary>True when the user holds any role that must use two-factor sign-in, in any firm.</summary>
    public async Task<bool> IsPrivilegedAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var roles = await users.GetRolesAsync(user);

        if (roles.Any(Roles.RequireTwoFactor.Contains))
        {
            return true;
        }

        return await platformData.RunAsync(
            data => data.TenantMembers.AnyAsync(member => member.UserId == user.Id, cancellationToken));
    }

    /// <summary>Someone working in one firm should not have to choose it after every sign-in.</summary>
    private async Task<Guid?> FindOnlyTenantAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tenantIds = await platformData.RunAsync(
            data => data.TenantMembers
                .Where(member => member.UserId == userId)
                .Select(member => member.TenantId)
                .Take(2)
                .ToListAsync(cancellationToken));

        return tenantIds.Count == 1 ? tenantIds[0] : null;
    }
}

/// <summary>Runs on every authenticated request, so a revoked session stops working at once.</summary>
public sealed class SessionTokenValidator(AppDbContext db, TimeProvider timeProvider)
{
    public async Task<bool> IsValidAsync(Guid userId, Guid sessionId, string securityStamp, Guid? tenantId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var current = await db.UserSessions
            .AsNoTracking()
            .Where(session => session.Id == sessionId
                && session.UserId == userId
                && session.RevokedAt == null
                && session.ExpiresAt > now)
            .Select(session => new { session.User.SecurityStamp, session.ActiveTenantId })
            .FirstOrDefaultAsync(cancellationToken);

        return current is { SecurityStamp: not null }
            && current.ActiveTenantId == tenantId
            && SecureTokens.FixedTimeEquals(current.SecurityStamp, securityStamp);
    }
}
