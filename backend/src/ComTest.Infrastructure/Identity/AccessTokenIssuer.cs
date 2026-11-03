using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ComTest.Infrastructure.Identity;

public static class AppClaims
{
    public const string Subject = "sub";
    public const string SessionId = "sid";
    public const string SecurityStamp = "sstamp";
    public const string Role = "role";
    public const string Mfa = "mfa";
    public const string TenantId = "tid";

    /// <summary>Marks a token as an access token, so no other signed token can be used in its place.</summary>
    public const string AccessTokenType = "at+jwt";
}

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt)
{
    public override string ToString() => nameof(IssuedAccessToken);
}

public sealed class AccessTokenIssuer(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    PlatformDataAccess platformData,
    JwtKeyRing keys,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider)
{
    private static readonly JsonWebTokenHandler TokenHandler = new() { SetDefaultTimesOnTokenCreation = false };

    public async Task<IssuedAccessToken> IssueAsync(ApplicationUser user, UserSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(session);

        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.AccessTokenMinutes);

        var roles = (await users.GetRolesAsync(user)).ToList();

        if (session.ActiveTenantId is { } activeTenantId)
        {
            var tenantRole = await platformData.RunAsync(
                data => data.TenantMembers
                    .Where(member => member.TenantId == activeTenantId && member.UserId == user.Id)
                    .Select(member => member.Role)
                    .FirstOrDefaultAsync(cancellationToken));

            if (tenantRole is not null)
            {
                roles.Add(tenantRole);
            }
        }

        var permissions = await GetPermissionsAsync(roles, cancellationToken);

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [AppClaims.Subject] = user.Id.ToString("D"),
            [AppClaims.SessionId] = session.Id.ToString("D"),
            [AppClaims.SecurityStamp] = user.SecurityStamp!,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            [AppClaims.Role] = roles.ToArray(),
            [Permissions.ClaimType] = permissions,
        };

        if (session.Mfa)
        {
            claims[AppClaims.Mfa] = true;
        }

        if (session.ActiveTenantId is { } tenantId)
        {
            claims[AppClaims.TenantId] = tenantId.ToString("D");
        }

        var token = TokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = claims,
            SigningCredentials = keys.SigningCredentials,
            TokenType = AppClaims.AccessTokenType,
        });

        return new IssuedAccessToken(token, expiresAt);
    }

    private async Task<string[]> GetPermissionsAsync(List<string> roles, CancellationToken cancellationToken)
    {
        var normalizedRoles = roles.Select(role => role.ToUpperInvariant()).ToList();

        var query =
            from claim in db.RoleClaims
            join role in db.Roles on claim.RoleId equals role.Id
            where claim.ClaimType == Permissions.ClaimType && normalizedRoles.Contains(role.NormalizedName!)
            select claim.ClaimValue!;

        return await query.Distinct().ToArrayAsync(cancellationToken);
    }
}
