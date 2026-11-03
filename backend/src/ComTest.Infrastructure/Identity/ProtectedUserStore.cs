using ComTest.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ComTest.Infrastructure.Identity;

/// <summary>
/// Identity stores authenticator keys and recovery codes as plain text by default. Here the
/// key is encrypted and recovery codes are hashed, so a database leak does not reveal them.
/// </summary>
public sealed class ProtectedUserStore : UserStore<ApplicationUser, ApplicationRole, AppDbContext, Guid>
{
    private readonly IDataProtector _keyProtector;

    public ProtectedUserStore(AppDbContext context, IDataProtectionProvider dataProtection, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
        ArgumentNullException.ThrowIfNull(dataProtection);

        _keyProtector = dataProtection.CreateProtector("ComTest.Identity.AuthenticatorKey.v1");
    }

    public override Task SetAuthenticatorKeyAsync(ApplicationUser user, string key, CancellationToken cancellationToken) =>
        base.SetAuthenticatorKeyAsync(user, _keyProtector.Protect(key), cancellationToken);

    public override async Task<string?> GetAuthenticatorKeyAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var protectedKey = await base.GetAuthenticatorKeyAsync(user, cancellationToken);

        return protectedKey is null ? null : _keyProtector.Unprotect(protectedKey);
    }

    public override Task ReplaceCodesAsync(ApplicationUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken) =>
        base.ReplaceCodesAsync(user, recoveryCodes.Select(SecureTokens.Hash), cancellationToken);

    public override Task<bool> RedeemCodeAsync(ApplicationUser user, string code, CancellationToken cancellationToken) =>
        base.RedeemCodeAsync(user, SecureTokens.Hash(code), cancellationToken);
}
