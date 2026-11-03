using ComTest.Application.Modules.Identity.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Infrastructure.Identity;

/// <summary>Roles and their default permissions, delivered by migration so every environment has them.</summary>
internal static class IdentitySeedData
{
    private static readonly Dictionary<string, Guid> RoleIds = new(StringComparer.Ordinal)
    {
        [Roles.PlatformAdmin] = new("0195b2a0-0000-7000-8000-000000000001"),
        [Roles.StoreOwner] = new("0195b2a0-0000-7000-8000-000000000002"),
        [Roles.Manager] = new("0195b2a0-0000-7000-8000-000000000003"),
        [Roles.Staff] = new("0195b2a0-0000-7000-8000-000000000004"),
        [Roles.Customer] = new("0195b2a0-0000-7000-8000-000000000005"),
    };

    public static void Seed(ModelBuilder builder)
    {
        builder.Entity<ApplicationRole>().HasData(
            RoleIds.Select(role => new ApplicationRole(role.Value, role.Key)));

        var claimId = 0;

        var claims =
            from role in Permissions.ByRole.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            from permission in role.Value
            select new IdentityRoleClaim<Guid>
            {
                Id = ++claimId,
                RoleId = RoleIds[role.Key],
                ClaimType = Permissions.ClaimType,
                ClaimValue = permission,
            };

        builder.Entity<IdentityRoleClaim<Guid>>().HasData(claims.ToList());
    }
}
