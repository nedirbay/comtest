using System.Reflection;
using ComTest.Application.Abstractions;
using ComTest.Domain.Modules.Catalog;
using ComTest.Domain.Modules.Customers;
using ComTest.Domain.Modules.Inventory;
using ComTest.Domain.Modules.Media;
using ComTest.Domain.Modules.Ordering;
using ComTest.Domain.Modules.Payments;
using ComTest.Domain.Modules.Shipping;
using ComTest.Domain.Modules.Tenancy;
using ComTest.Infrastructure.Identity;
using ComTest.Infrastructure.Persistence.Outbox;
using ComTest.Shared.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IDataProtectionKeyContext, IApplicationDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();

    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();

    public DbSet<StaffInvitation> StaffInvitations => Set<StaffInvitation>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();

    public DbSet<CategoryAttributeLink> CategoryAttributes => Set<CategoryAttributeLink>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<StockItem> StockItems => Set<StockItem>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    public DbSet<ShippingZone> ShippingZones => Set<ShippingZone>();

    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PhoneVerificationCode> PhoneVerificationCodes => Set<PhoneVerificationCode>();

    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    Task<int> IApplicationDbContext.SaveChangesAsync(CancellationToken cancellationToken) => base.SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        IdentitySeedData.Seed(builder);
        ApplyEntityConventions(builder);
    }

    private void ApplyEntityConventions(ModelBuilder builder)
    {
        var softDeleteFilter = typeof(AppDbContext).GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!;
        var tenantFilter = typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in builder.Model.GetEntityTypes().Where(type => type.BaseType is null && !type.IsOwned() && !type.ClrType.IsAbstract).ToList())
        {
            var clrType = entityType.ClrType;

            // Ids are created in code. Without this, EF would mistake a new child with an id
            // for an existing row and send an UPDATE instead of an INSERT.
            if (typeof(Entity).IsAssignableFrom(clrType))
            {
                builder.Entity(clrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                softDeleteFilter.MakeGenericMethod(clrType).Invoke(null, [builder]);
            }

            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                tenantFilter.MakeGenericMethod(clrType).Invoke(this, [builder]);
            }

            if (typeof(IVersioned).IsAssignableFrom(clrType))
            {
                builder.Entity(clrType).Property(nameof(IVersioned.Version)).IsRowVersion();
            }
        }
    }

    private static void ApplySoftDeleteFilter<T>(ModelBuilder builder)
        where T : class, ISoftDeletable =>
        builder.Entity<T>().HasQueryFilter(QueryFilters.SoftDelete, entity => !entity.IsDeleted);

    private void ApplyTenantFilter<T>(ModelBuilder builder)
        where T : class, ITenantOwned =>
        builder.Entity<T>().HasQueryFilter(
            QueryFilters.Tenant,
            entity => tenantContext.IsPlatformAccess || entity.TenantId == tenantContext.TenantId);
}
