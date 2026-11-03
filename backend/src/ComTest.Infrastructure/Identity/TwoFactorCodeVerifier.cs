using Microsoft.AspNetCore.Identity;

namespace ComTest.Infrastructure.Identity;

/// <summary>Verifies authenticator codes and refuses to accept the same code twice.</summary>
public sealed class TwoFactorCodeVerifier(UserManager<ApplicationUser> users, TimeProvider timeProvider)
{
    // Identity accepts codes from neighbouring 30-second steps, so remember a code at least that long.
    private static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(5);

    public async Task<bool> VerifyAsync(ApplicationUser user, string code)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = timeProvider.GetUtcNow();
        var codeHash = SecureTokens.Hash($"{user.Id:D}:{code}");

        if (user.LastTotpCodeHash is not null
            && user.LastTotpUsedAt is { } usedAt
            && now - usedAt < ReplayWindow
            && SecureTokens.FixedTimeEquals(user.LastTotpCodeHash, codeHash))
        {
            return false;
        }

        var valid = await users.VerifyTwoFactorTokenAsync(
            user,
            users.Options.Tokens.AuthenticatorTokenProvider,
            code);

        if (!valid)
        {
            return false;
        }

        user.LastTotpCodeHash = codeHash;
        user.LastTotpUsedAt = now;

        return (await users.UpdateAsync(user)).Succeeded;
    }
}
