using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Identity;

namespace ComTest.Infrastructure.Identity;

public sealed class TwoFactorService(
    UserManager<ApplicationUser> users,
    IUserStore<ApplicationUser> userStore,
    TwoFactorCodeVerifier codeVerifier,
    SessionManager sessions,
    SecurityEventLogger securityEvents) : ITwoFactorService
{
    private const int RecoveryCodeCount = 10;
    private const string IssuerName = "ComTest";

    private static Error InvalidCode => Error.Validation(ErrorCodes.Auth.InvalidTwoFactorCode);

    private static Error Unknown => Error.Unauthorized(ErrorCodes.Common.Unauthorized);

    public async Task<Result<TwoFactorSetupDto>> BeginSetupAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Unknown;
        }

        if (user.TwoFactorEnabled)
        {
            return Error.Conflict(ErrorCodes.Auth.TwoFactorAlreadyEnabled);
        }

        // Not ResetAuthenticatorKeyAsync: that also changes the security stamp, which would sign
        // the user out in the middle of setup, although nothing has been protected by this key yet.
        var keyStore = (IUserAuthenticatorKeyStore<ApplicationUser>)userStore;
        await keyStore.SetAuthenticatorKeyAsync(user, users.GenerateNewAuthenticatorKey(), cancellationToken);
        await users.UpdateAsync(user);

        var key = await users.GetAuthenticatorKeyAsync(user);
        var label = Uri.EscapeDataString($"{IssuerName}:{user.Email}");
        var uri = $"otpauth://totp/{label}?secret={key}&issuer={IssuerName}&digits=6";

        return new TwoFactorSetupDto(key!, uri);
    }

    public async Task<Result<RecoveryCodesDto>> EnableAsync(Guid userId, Guid? currentSessionId, TwoFactorCodeRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Unknown;
        }

        if (user.TwoFactorEnabled)
        {
            return Error.Conflict(ErrorCodes.Auth.TwoFactorAlreadyEnabled);
        }

        if (await users.GetAuthenticatorKeyAsync(user) is null || !await codeVerifier.VerifyAsync(user, request.Code))
        {
            return InvalidCode;
        }

        await users.SetTwoFactorEnabledAsync(user, true);

        // This session has just proved the second factor, so it needs no second challenge.
        if (currentSessionId is { } sessionId)
        {
            await sessions.MarkMfaAsync(sessionId, cancellationToken);
        }

        await securityEvents.WriteAsync(SecurityEventType.TwoFactorEnabled, user.Id, client, cancellationToken);

        return await GenerateRecoveryCodesAsync(user);
    }

    public async Task<Result> DisableAsync(Guid userId, Guid? currentSessionId, DisableTwoFactorRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.Failure(Unknown);
        }

        if (!user.TwoFactorEnabled)
        {
            return Result.Failure(Error.Conflict(ErrorCodes.Auth.TwoFactorNotEnabled));
        }

        if (await sessions.IsPrivilegedAsync(user, cancellationToken))
        {
            return Result.Failure(Error.Forbidden(ErrorCodes.Auth.TwoFactorRequiredForStaff));
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);

            return Result.Failure(Error.Validation(ErrorCodes.Auth.CurrentPasswordInvalid));
        }

        if (!await codeVerifier.VerifyAsync(user, request.Code))
        {
            await users.AccessFailedAsync(user);

            return Result.Failure(InvalidCode);
        }

        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
        await sessions.RevokeAllAsync(user.Id, SessionRevocationReasons.TwoFactorDisabled, cancellationToken, currentSessionId);
        await securityEvents.WriteAsync(SecurityEventType.TwoFactorDisabled, user.Id, client, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(Guid userId, TwoFactorCodeRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Unknown;
        }

        if (!user.TwoFactorEnabled)
        {
            return Error.Conflict(ErrorCodes.Auth.TwoFactorNotEnabled);
        }

        if (!await codeVerifier.VerifyAsync(user, request.Code))
        {
            return InvalidCode;
        }

        await securityEvents.WriteAsync(SecurityEventType.RecoveryCodesRegenerated, user.Id, client, cancellationToken);

        return await GenerateRecoveryCodesAsync(user);
    }

    private async Task<Result<RecoveryCodesDto>> GenerateRecoveryCodesAsync(ApplicationUser user)
    {
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);

        return new RecoveryCodesDto([.. codes!]);
    }
}
