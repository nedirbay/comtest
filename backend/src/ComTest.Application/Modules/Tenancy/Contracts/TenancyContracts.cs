using ComTest.Domain.Modules.Tenancy;

namespace ComTest.Application.Modules.Tenancy.Contracts;

public sealed record TenantInfo(Guid Id, string Slug, string Name, TenantStatus Status);

/// <summary>Finds tenants for routing. Results are cached briefly, as every request needs this.</summary>
public interface ITenantDirectory
{
    Task<TenantInfo?> FindBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<TenantInfo?> FindByHostnameAsync(string hostname, CancellationToken cancellationToken);

    Task<TenantInfo?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken);

    void Invalidate(TenantInfo tenant);
}

public sealed record MembershipDto(Guid TenantId, string TenantName, string TenantSlug, string Role);

public sealed record TenantDto(Guid Id, string Name, string Slug, string Status, string Plan);

public sealed record PublicStoreDto(
    string Name,
    string Slug,
    string DefaultCurrency,
    string DefaultLanguage,
    string? LogoUrl,
    string? ContactEmail,
    string? ContactPhone,
    string? Address,
    string? WorkingHours);

public sealed record StoreSettingsDto(
    string Name,
    string Slug,
    string Plan,
    int MaxProducts,
    int MaxStaff,
    int StorageQuotaMegabytes,
    string DefaultCurrency,
    string DefaultLanguage,
    string? LogoUrl,
    string? ContactEmail,
    string? ContactPhone,
    string? Address,
    string? WorkingHours,
    uint Version);

public sealed record StaffMemberDto(Guid UserId, string Email, string FullName, string Role, DateTimeOffset JoinedAt);

public sealed record StaffInvitationDto(Guid Id, string Email, string Role, DateTimeOffset ExpiresAt);
