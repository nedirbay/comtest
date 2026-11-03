using ComTest.Shared.Domain;

namespace ComTest.Domain.Modules.Tenancy;

public enum TenantStatus
{
    Active,
    Suspended,
}

public enum TenantPlan
{
    Free,
    Standard,
    Business,
}

public sealed record TenantCreated(Guid TenantId, string Slug, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>A firm using the platform. This table is global: it is how a request finds its tenant.</summary>
public sealed class Tenant : AuditableEntity, IVersioned
{
    private Tenant()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public TenantStatus Status { get; private set; }

    public TenantPlan Plan { get; private set; }

    public int MaxProducts { get; private set; }

    public int MaxStaff { get; private set; }

    public int StorageQuotaMegabytes { get; private set; }

    public string DefaultCurrency { get; private set; } = string.Empty;

    public string DefaultLanguage { get; private set; } = string.Empty;

    public string? LogoUrl { get; private set; }

    public string? ContactEmail { get; private set; }

    public string? ContactPhone { get; private set; }

    public string? Address { get; private set; }

    public string? WorkingHours { get; private set; }

    public uint Version { get; private set; }

    public static Tenant Create(string name, string slug, string defaultCurrency, string defaultLanguage, DateTimeOffset now)
    {
        var tenant = new Tenant
        {
            Name = name,
            Slug = slug,
            Status = TenantStatus.Active,
            DefaultCurrency = defaultCurrency,
            DefaultLanguage = defaultLanguage,
        };

        tenant.ChangePlan(TenantPlan.Free);
        tenant.Raise(new TenantCreated(tenant.Id, slug, now));

        return tenant;
    }

    public void UpdateSettings(
        string name,
        string defaultCurrency,
        string defaultLanguage,
        string? logoUrl,
        string? contactEmail,
        string? contactPhone,
        string? address,
        string? workingHours)
    {
        Name = name;
        DefaultCurrency = defaultCurrency;
        DefaultLanguage = defaultLanguage;
        LogoUrl = logoUrl;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        Address = address;
        WorkingHours = workingHours;
    }

    public void ChangePlan(TenantPlan plan)
    {
        Plan = plan;
        (MaxProducts, MaxStaff, StorageQuotaMegabytes) = plan switch
        {
            TenantPlan.Free => (100, 3, 500),
            TenantPlan.Standard => (5_000, 15, 10_000),
            TenantPlan.Business => (100_000, 100, 100_000),
            _ => throw new ArgumentOutOfRangeException(nameof(plan)),
        };
    }

    public void Suspend() => Status = TenantStatus.Suspended;

    public void Activate() => Status = TenantStatus.Active;
}

/// <summary>A firm's own web address. Only verified domains route to the tenant.</summary>
public sealed class TenantDomain : AuditableEntity
{
    private TenantDomain()
    {
    }

    public Guid OwnerTenantId { get; private set; }

    public string Hostname { get; private set; } = string.Empty;

    public DateTimeOffset? VerifiedAt { get; private set; }

    public static TenantDomain Create(Guid tenantId, string hostname) => new()
    {
        OwnerTenantId = tenantId,
        Hostname = hostname.ToLowerInvariant(),
    };

    public void MarkVerified(DateTimeOffset at) => VerifiedAt = at;
}

/// <summary>Links a user to a firm with a staff role. A user's role always comes from this row.</summary>
public sealed class TenantMember : TenantOwnedEntity
{
    private TenantMember()
    {
    }

    public Guid UserId { get; private set; }

    public string Role { get; private set; } = string.Empty;

    public static TenantMember Create(Guid tenantId, Guid userId, string role)
    {
        var member = new TenantMember { UserId = userId, Role = role };
        member.AssignTenant(tenantId);

        return member;
    }

    public void ChangeRole(string role) => Role = role;
}

public sealed class StaffInvitation : TenantOwnedEntity
{
    private StaffInvitation()
    {
    }

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string Role { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static StaffInvitation Create(string normalizedEmail, string role, string tokenHash, DateTimeOffset expiresAt) => new()
    {
        NormalizedEmail = normalizedEmail,
        Role = role,
        TokenHash = tokenHash,
        ExpiresAt = expiresAt,
    };

    public bool IsPending(DateTimeOffset now) => AcceptedAt is null && RevokedAt is null && ExpiresAt > now;

    public void Accept(DateTimeOffset at) => AcceptedAt = at;

    public void Revoke(DateTimeOffset at) => RevokedAt = at;
}
