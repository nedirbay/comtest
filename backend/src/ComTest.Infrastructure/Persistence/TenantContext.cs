using ComTest.Application.Abstractions;

namespace ComTest.Infrastructure.Persistence;

/// <summary>Scoped holder for the tenant of the current request or background job.</summary>
public sealed class TenantContext : ITenantContext, ITenantScope
{
    public Guid? TenantId { get; private set; }

    public bool IsPlatformAccess { get; private set; }

    public void Use(Guid tenantId)
    {
        TenantId = tenantId;
        IsPlatformAccess = false;
    }

    public void UsePlatformAccess()
    {
        TenantId = null;
        IsPlatformAccess = true;
    }
}
