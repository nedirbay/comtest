using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ComTest.Infrastructure.Identity.Authorization;

/// <summary>Requires a permission, such as <c>[HasPermission(Permissions.CatalogWrite)]</c>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(PermissionPolicy.Prefix + permission)
{
    public string Permission { get; } = permission;
}

public static class PermissionPolicy
{
    public const string Prefix = "permission:";
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Builds permission policies on demand, so permissions need no individual registration.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentNullException.ThrowIfNull(policyName);

        if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName);
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[PermissionPolicy.Prefix.Length..]))
            .Build();
    }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var isPrivileged = context.User.FindAll(AppClaims.Role).Any(claim => Roles.RequireTwoFactor.Contains(claim.Value));

        if (isPrivileged && !context.User.HasClaim(claim => claim.Type == AppClaims.Mfa))
        {
            context.Fail(new AuthorizationFailureReason(this, ErrorCodes.Auth.MfaRequired));

            return Task.CompletedTask;
        }

        if (context.User.HasClaim(Permissions.ClaimType, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
