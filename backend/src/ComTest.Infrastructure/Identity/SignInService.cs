using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Persistence;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComTest.Infrastructure.Identity;

public sealed class SignInService(
    UserManager<ApplicationUser> users,
    IPasswordHasher<ApplicationUser> passwordHasher,
    AppDbContext db,
    SessionManager sessions,
    SecurityEventLogger securityEvents,
    TwoFactorCodeVerifier codeVerifier,
    IDataProtectionProvider dataProtection,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider) : ISignInService
{
    private const string ChallengeSeparator = "|";

    // Verified when no account exists, so a wrong login takes as long as a wrong password.
    private static readonly Lazy<string> DummyPasswordHash = new(() =>
        new PasswordHasher<ApplicationUser>(
            Options.Create(new PasswordHasherOptions { IterationCount = IdentityServiceRegistration.PasswordIterations }))
        .HashPassword(new ApplicationUser(), Guid.NewGuid().ToString("N")));

    private readonly ITimeLimitedDataProtector _challengeProtector =
        dataProtection.CreateProtector("ComTest.Identity.TwoFactorChallenge.v1").ToTimeLimitedDataProtector();

    private static Error InvalidCredentials => Error.Unauthorized(ErrorCodes.Auth.InvalidCredentials);

    public async Task<Result<LoginOutcome>> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await FindByLoginAsync(request.Login, cancellationToken);

        if (user is null)
        {
            VerifyDummyPassword(request.Password);
            await securityEvents.WriteAsync(SecurityEventType.LoginFailed, null, client, cancellationToken, request.Login);

            return InvalidCredentials;
        }

        if (await users.IsLockedOutAsync(user))
        {
            VerifyDummyPassword(request.Password);
            await securityEvents.WriteAsync(SecurityEventType.LoginBlockedByLockout, user.Id, client, cancellationToken);

            return InvalidCredentials;
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await RecordFailedAttemptAsync(user, SecurityEventType.LoginFailed, client, cancellationToken);

            return InvalidCredentials;
        }

        // Safe to reveal: only someone who knows the correct password gets this far.
        if (!user.EmailConfirmed)
        {
            await securityEvents.WriteAsync(SecurityEventType.LoginEmailNotConfirmed, user.Id, client, cancellationToken);

            return Error.Forbidden(ErrorCodes.Auth.EmailNotConfirmed);
        }

        if (user.TwoFactorEnabled)
        {
            await securityEvents.WriteAsync(SecurityEventType.TwoFactorChallenged, user.Id, client, cancellationToken);

            return new LoginOutcome(null, CreateChallenge(user));
        }

        var tokens = await CompleteSignInAsync(user, mfa: false, SecurityEventType.LoginSucceeded, client, cancellationToken);

        return new LoginOutcome(tokens, null);
    }

    public async Task<Result<AuthTokens>> CompleteTwoFactorAsync(TwoFactorLoginRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invalidCode = Error.Unauthorized(ErrorCodes.Auth.InvalidTwoFactorCode);
        var user = await ReadChallengeAsync(request.ChallengeToken);

        if (user is null || await users.IsLockedOutAsync(user))
        {
            return invalidCode;
        }

        var usedRecoveryCode = request.RecoveryCode is not null;
        var verified = usedRecoveryCode
            ? (await users.RedeemTwoFactorRecoveryCodeAsync(user, request.RecoveryCode!)).Succeeded
            : await codeVerifier.VerifyAsync(user, request.Code ?? string.Empty);

        if (!verified)
        {
            await RecordFailedAttemptAsync(user, SecurityEventType.TwoFactorFailed, client, cancellationToken);

            return invalidCode;
        }

        return await CompleteSignInAsync(
            user,
            mfa: true,
            usedRecoveryCode ? SecurityEventType.RecoveryCodeUsed : SecurityEventType.TwoFactorSucceeded,
            client,
            cancellationToken);
    }

    public Task<Result<AuthTokens>> RefreshAsync(string refreshToken, ClientInfo client, CancellationToken cancellationToken) =>
        sessions.RotateAsync(refreshToken, client, cancellationToken);

    public async Task LogoutAsync(string? refreshToken, ClientInfo client, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var userId = await sessions.RevokeByTokenAsync(refreshToken, SessionRevocationReasons.Logout, cancellationToken);

        if (userId is not null)
        {
            await securityEvents.WriteAsync(SecurityEventType.Logout, userId, client, cancellationToken);
        }
    }

    private async Task<ApplicationUser?> FindByLoginAsync(string login, CancellationToken cancellationToken)
    {
        var trimmed = login.Trim();

        if (trimmed.Contains('@', StringComparison.Ordinal))
        {
            return await users.FindByEmailAsync(trimmed);
        }

        // Phone sign-in only for confirmed numbers: anyone can type an unverified number.
        return await db.Users.FirstOrDefaultAsync(
            user => user.PhoneNumber == trimmed && user.PhoneNumberConfirmed,
            cancellationToken);
    }

    private void VerifyDummyPassword(string password) =>
        passwordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash.Value, password);

    private async Task RecordFailedAttemptAsync(ApplicationUser user, SecurityEventType type, ClientInfo client, CancellationToken cancellationToken)
    {
        await users.AccessFailedAsync(user);
        await securityEvents.WriteAsync(type, user.Id, client, cancellationToken);

        if (await users.IsLockedOutAsync(user))
        {
            await securityEvents.WriteAsync(SecurityEventType.AccountLockedOut, user.Id, client, cancellationToken);
        }
    }

    private async Task<AuthTokens> CompleteSignInAsync(ApplicationUser user, bool mfa, SecurityEventType type, ClientInfo client, CancellationToken cancellationToken)
    {
        await users.ResetAccessFailedCountAsync(user);

        user.LastLoginAt = timeProvider.GetUtcNow();
        await users.UpdateAsync(user);

        var tokens = await sessions.StartAsync(user, mfa, client, cancellationToken);
        await securityEvents.WriteAsync(type, user.Id, client, cancellationToken);

        return tokens;
    }

    private string CreateChallenge(ApplicationUser user) =>
        _challengeProtector.Protect(
            string.Join(ChallengeSeparator, user.Id.ToString("D"), user.SecurityStamp),
            TimeSpan.FromMinutes(options.Value.TwoFactorChallengeMinutes));

    private async Task<ApplicationUser?> ReadChallengeAsync(string challenge)
    {
        string payload;

        try
        {
            payload = _challengeProtector.Unprotect(challenge);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }

        var parts = payload.Split(ChallengeSeparator);

        if (parts.Length != 2)
        {
            return null;
        }

        var user = await users.FindByIdAsync(parts[0]);

        return user is not null && string.Equals(user.SecurityStamp, parts[1], StringComparison.Ordinal) ? user : null;
    }
}
