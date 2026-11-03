using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Messaging;
using ComTest.Infrastructure.Persistence;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ComTest.Infrastructure.Identity;

public sealed class PhoneVerificationService(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    ISmsSender smsSender,
    SecurityEventLogger securityEvents,
    TimeProvider timeProvider) : IPhoneVerificationService
{
    private const int MaxFailedAttempts = 5;
    private const int MaxCodesPerUserPerHour = 5;
    private const int MaxCodesPerNumberPerHour = 5;
    private const string UniqueViolation = "23505";

    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private static Error InvalidCode => Error.Validation(ErrorCodes.Auth.PhoneCodeInvalid);

    public async Task<Result> SendCodeAsync(Guid userId, SendPhoneCodeRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();
        var hourAgo = now - Hour;

        var recent = await db.PhoneVerificationCodes
            .AsNoTracking()
            .Where(code => code.CreatedAt > hourAgo && (code.UserId == userId || code.PhoneNumber == request.PhoneNumber))
            .Select(code => new { code.UserId, code.PhoneNumber, code.CreatedAt })
            .ToListAsync(cancellationToken);

        var limited = recent.Any(code => code.UserId == userId && now - code.CreatedAt < ResendInterval)
            || recent.Count(code => code.UserId == userId) >= MaxCodesPerUserPerHour
            || recent.Count(code => code.PhoneNumber == request.PhoneNumber) >= MaxCodesPerNumberPerHour;

        if (limited)
        {
            return Result.Failure(Error.TooManyRequests(ErrorCodes.Auth.PhoneRateLimited));
        }

        var code = SecureTokens.NewNumericCode();

        if (!await smsSender.SendAsync(request.PhoneNumber, code, cancellationToken))
        {
            return Result.Failure(Error.Failure(ErrorCodes.Auth.SmsUnavailable));
        }

        db.PhoneVerificationCodes.Add(new PhoneVerificationCode
        {
            UserId = userId,
            PhoneNumber = request.PhoneNumber,
            CodeHash = HashCode(userId, request.PhoneNumber, code),
            CreatedAt = now,
            ExpiresAt = now + CodeLifetime,
        });

        await db.SaveChangesAsync(cancellationToken);
        await securityEvents.WriteAsync(SecurityEventType.PhoneCodeSent, userId, client, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> VerifyAsync(Guid userId, VerifyPhoneRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();

        var pending = await db.PhoneVerificationCodes
            .Where(code => code.UserId == userId && code.ConsumedAt == null && code.ExpiresAt > now)
            .OrderByDescending(code => code.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (pending is null || pending.FailedAttempts >= MaxFailedAttempts)
        {
            return Result.Failure(InvalidCode);
        }

        if (!SecureTokens.FixedTimeEquals(pending.CodeHash, HashCode(userId, pending.PhoneNumber, request.Code)))
        {
            pending.FailedAttempts++;
            await db.SaveChangesAsync(cancellationToken);
            await securityEvents.WriteAsync(SecurityEventType.PhoneCodeFailed, userId, client, cancellationToken);

            return Result.Failure(InvalidCode);
        }

        pending.ConsumedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        var user = await users.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.Failure(Error.Unauthorized(ErrorCodes.Common.Unauthorized));
        }

        user.PhoneNumber = pending.PhoneNumber;
        user.PhoneNumberConfirmed = true;

        try
        {
            var updated = await users.UpdateAsync(user);

            if (!updated.Succeeded)
            {
                return Result.Failure(Error.Conflict(ErrorCodes.Common.ConcurrencyConflict));
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // Reported only after the code is proven, so strangers cannot probe which numbers exist.
            return Result.Failure(Error.Conflict(ErrorCodes.Auth.PhoneTaken));
        }

        await users.UpdateSecurityStampAsync(user);
        await securityEvents.WriteAsync(SecurityEventType.PhoneVerified, user.Id, client, cancellationToken);

        return Result.Success();
    }

    private static string HashCode(Guid userId, string phoneNumber, string code) =>
        SecureTokens.Hash($"{userId:D}:{phoneNumber}:{code}");
}
