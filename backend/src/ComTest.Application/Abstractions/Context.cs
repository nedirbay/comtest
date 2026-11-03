namespace ComTest.Application.Abstractions;

/// <summary>The authenticated caller, or an anonymous one outside of a user request.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? SessionId { get; }

    bool HasPermission(string permission);
}

/// <summary>The tenant whose data the current scope may touch.</summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    /// <summary>True only for explicit platform-level work that spans tenants.</summary>
    bool IsPlatformAccess { get; }
}
