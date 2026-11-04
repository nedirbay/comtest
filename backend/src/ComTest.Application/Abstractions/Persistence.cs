using ComTest.Domain.Modules.Catalog;
using ComTest.Domain.Modules.Customers;
using ComTest.Domain.Modules.Inventory;
using ComTest.Domain.Modules.Media;
using ComTest.Domain.Modules.Ordering;
using ComTest.Domain.Modules.Payments;
using ComTest.Domain.Modules.Promotions;
using ComTest.Domain.Modules.Shipping;
using ComTest.Domain.Modules.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ComTest.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<TenantDomain> TenantDomains { get; }

    DbSet<TenantMember> TenantMembers { get; }

    DbSet<StaffInvitation> StaffInvitations { get; }

    DbSet<Category> Categories { get; }

    DbSet<Brand> Brands { get; }

    DbSet<AttributeDefinition> AttributeDefinitions { get; }

    DbSet<CategoryAttributeLink> CategoryAttributes { get; }

    DbSet<Product> Products { get; }

    DbSet<ProductVariant> ProductVariants { get; }

    DbSet<Warehouse> Warehouses { get; }

    DbSet<StockItem> StockItems { get; }

    DbSet<StockMovement> StockMovements { get; }

    DbSet<StockReservation> StockReservations { get; }

    DbSet<CustomerAddress> CustomerAddresses { get; }

    DbSet<ShippingZone> ShippingZones { get; }

    DbSet<ShippingMethod> ShippingMethods { get; }

    DbSet<Payment> Payments { get; }

    DbSet<Refund> Refunds { get; }

    DbSet<Cart> Carts { get; }

    DbSet<Order> Orders { get; }

    DbSet<MediaAsset> MediaAssets { get; }

    DbSet<Promotion> Promotions { get; }

    DbSet<PromotionRedemption> PromotionRedemptions { get; }

    DatabaseFacade Database { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Chooses the tenant for this scope. Use before the first database call that needs it.</summary>
public interface ITenantScope
{
    void Use(Guid tenantId);
}

public sealed record UserSummary(Guid Id, string Email, string FullName, bool EmailConfirmed);

/// <summary>What other modules may know about users, without depending on the identity system.</summary>
public interface IUserDirectory
{
    Task<UserSummary?> FindAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSummary>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    string NormalizeEmail(string email);

    /// <summary>Makes the user's current access tokens stop working, so new roles apply at once.</summary>
    Task InvalidateAccessTokensAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Ends the user's access to a tenant in every session.</summary>
    Task DetachFromTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>Removes anything from rich text that could run in a visitor's browser.</summary>
public interface IHtmlCleaner
{
    string Clean(string html);
}

public interface IStaffInvitationMailer
{
    void Send(string email, string tenantName, string token);
}
