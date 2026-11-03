using ComTest.Domain.Modules.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComTest.Infrastructure.Persistence.Configurations;

internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses");
        builder.Property(item => item.Name).HasMaxLength(200);
        builder.Property(item => item.Code).HasMaxLength(50);
        builder.Property(item => item.Address).HasMaxLength(500);
        builder.HasIndex(item => new { item.TenantId, item.Code }).IsUnique().HasFilter(CatalogMapping.NotDeleted);
    }
}

internal sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        // The last line of defence: even a bug in the code cannot store an impossible quantity.
        builder.ToTable("stock_items", table =>
            table.HasCheckConstraint("ck_stock_items_quantities", "on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand"));

        builder.HasIndex(item => new { item.WarehouseId, item.ProductVariantId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ProductVariantId });
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Modules.Catalog.ProductVariant>().WithMany().HasForeignKey(item => item.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");
        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.Reason).HasMaxLength(500);
        builder.HasIndex(item => new { item.TenantId, item.ProductVariantId, item.CreatedAt });
        builder.HasIndex(item => item.ReferenceId);
    }
}

internal sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations", table =>
            table.HasCheckConstraint("ck_stock_reservations_quantity", "quantity > 0"));

        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(item => new { item.ReferenceId, item.Status });
        builder.HasOne<StockItem>().WithMany().HasForeignKey(item => item.StockItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
