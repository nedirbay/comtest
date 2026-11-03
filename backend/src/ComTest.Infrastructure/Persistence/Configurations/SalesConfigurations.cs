using ComTest.Domain.Modules.Customers;
using ComTest.Domain.Modules.Ordering;
using ComTest.Domain.Modules.Payments;
using ComTest.Domain.Modules.Shipping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComTest.Infrastructure.Persistence.Configurations;

internal sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("customer_addresses");
        builder.Property(item => item.Label).HasMaxLength(100);
        builder.ComplexProperty(item => item.Details, details =>
        {
            details.Property(value => value.Recipient).HasMaxLength(200);
            details.Property(value => value.Phone).HasMaxLength(16);
            details.Property(value => value.Region).HasMaxLength(100);
            details.Property(value => value.District).HasMaxLength(100);
            details.Property(value => value.City).HasMaxLength(100);
            details.Property(value => value.Line1).HasMaxLength(300);
            details.Property(value => value.Line2).HasMaxLength(300);
            details.Property(value => value.PostalCode).HasMaxLength(20);
        });
        builder.HasIndex(item => new { item.TenantId, item.UserId });
        builder.HasOne<Identity.ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ShippingZoneConfiguration : IEntityTypeConfiguration<ShippingZone>
{
    public void Configure(EntityTypeBuilder<ShippingZone> builder)
    {
        builder.ToTable("shipping_zones");
        builder.Property(item => item.Name).HasMaxLength(200);
        builder.Property(item => item.Regions).ConfigureAsJson<IReadOnlyList<string>>(() => []);
        builder.HasIndex(item => item.TenantId);
    }
}

internal sealed class ShippingMethodConfiguration : IEntityTypeConfiguration<ShippingMethod>
{
    public void Configure(EntityTypeBuilder<ShippingMethod> builder)
    {
        builder.ToTable("shipping_methods");
        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Names).ConfigureAsJson<IReadOnlyDictionary<string, string>>(() => new Dictionary<string, string>());
        builder.MapMoney(item => item.Price);
        builder.Property(item => item.FreeOverAmount).HasPrecision(18, 2);
        builder.Property(item => item.PickupAddress).HasMaxLength(500);
        builder.HasIndex(item => item.TenantId);
        builder.HasOne<ShippingZone>().WithMany().HasForeignKey(item => item.ZoneId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        // The amount columns cannot leave their valid range, whatever the code does.
        builder.ToTable("payments", table => table.HasCheckConstraint(
            "ck_payments_refunds",
            "refunded_amount >= 0 AND pending_refund_amount >= 0 AND refunded_amount + pending_refund_amount <= amount_amount"));

        builder.Property(item => item.Method).HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
        builder.MapMoney(item => item.Amount);
        builder.Property(item => item.RefundedAmount).HasPrecision(18, 2);
        builder.Property(item => item.PendingRefundAmount).HasPrecision(18, 2);
        builder.Property(item => item.Reference).HasMaxLength(200);

        // One payment per order: an order cannot be charged twice.
        builder.HasIndex(item => new { item.TenantId, item.OrderId }).IsUnique();
        builder.HasOne<Order>().WithMany().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Refunds).WithOne().HasForeignKey(item => item.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Events).WithOne().HasForeignKey(item => item.PaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds");
        builder.MapMoney(item => item.Amount);
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(item => item.PaymentId);
    }
}

internal sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.ToTable("payment_events");
        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.Property(item => item.Note).HasMaxLength(1000);
        builder.HasIndex(item => item.PaymentId);
    }
}

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("carts");
        builder.Property(item => item.GuestTokenHash).HasMaxLength(64);
        builder.HasIndex(item => new { item.TenantId, item.UserId }).IsUnique().HasFilter("user_id IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.GuestTokenHash }).IsUnique().HasFilter("guest_token_hash IS NOT NULL");
        builder.HasIndex(item => item.LastActivityAt);
        builder.HasOne<Identity.ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(item => item.Lines).WithOne().HasForeignKey(item => item.CartId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CartLineConfiguration : IEntityTypeConfiguration<CartLine>
{
    public void Configure(EntityTypeBuilder<CartLine> builder)
    {
        builder.ToTable("cart_lines", table => table.HasCheckConstraint("ck_cart_lines_quantity", "quantity > 0"));
        builder.Property(item => item.AddedUnitPrice).HasPrecision(18, 2);
        builder.HasIndex(item => new { item.CartId, item.ProductVariantId }).IsUnique();
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.Property(item => item.Number).HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.PaymentState).HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.ShippingMethodName).HasMaxLength(200);
        builder.Property(item => item.ShippingType).HasMaxLength(20);
        builder.Property(item => item.PaymentMethod).HasMaxLength(50);
        builder.Property(item => item.CustomerNote).HasMaxLength(1000);
        builder.Property(item => item.IdempotencyKeyHash).HasMaxLength(64);
        builder.Property(item => item.Carrier).HasMaxLength(100);
        builder.Property(item => item.TrackingNumber).HasMaxLength(100);
        builder.Property(item => item.TrackingUrl).HasMaxLength(500);

        builder.ComplexProperty(item => item.Contact, contact =>
        {
            contact.Property(value => value.Name).HasMaxLength(200);
            contact.Property(value => value.Phone).HasMaxLength(16);
            contact.Property(value => value.Email).HasMaxLength(254);
        });

        builder.ComplexProperty(item => item.ShipTo, address =>
        {
            address.Property(value => value.Recipient).HasMaxLength(200);
            address.Property(value => value.Phone).HasMaxLength(16);
            address.Property(value => value.Region).HasMaxLength(100);
            address.Property(value => value.District).HasMaxLength(100);
            address.Property(value => value.City).HasMaxLength(100);
            address.Property(value => value.Line1).HasMaxLength(300);
            address.Property(value => value.Line2).HasMaxLength(300);
            address.Property(value => value.PostalCode).HasMaxLength(20);
        });

        builder.MapMoney(item => item.Subtotal);
        builder.MapMoney(item => item.Discount);
        builder.MapMoney(item => item.Tax);
        builder.MapMoney(item => item.Shipping);
        builder.MapMoney(item => item.GrandTotal);

        builder.HasIndex(item => new { item.TenantId, item.Number }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKeyHash }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.Status, item.PlacedAt });
        builder.HasIndex(item => new { item.TenantId, item.CustomerUserId, item.PlacedAt });
        builder.HasIndex(item => new { item.Status, item.ExpiresAt });

        // Orders outlive accounts: they are financial records.
        builder.HasOne<Identity.ApplicationUser>().WithMany().HasForeignKey(item => item.CustomerUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(item => item.Items).WithOne().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.StatusHistory).WithOne().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Notes).WithOne().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", table => table.HasCheckConstraint("ck_order_items_quantity", "quantity > 0"));
        builder.Property(item => item.Sku).HasMaxLength(100);
        builder.Property(item => item.ProductName).HasMaxLength(300);
        builder.Property(item => item.Options).ConfigureAsJson<IReadOnlyDictionary<string, string>>(() => new Dictionary<string, string>());
        builder.MapMoney(item => item.UnitPrice);
        builder.MapMoney(item => item.LineTotal);
        builder.HasIndex(item => item.OrderId);
    }
}

internal sealed class OrderStatusChangeConfiguration : IEntityTypeConfiguration<OrderStatusChange>
{
    public void Configure(EntityTypeBuilder<OrderStatusChange> builder)
    {
        builder.ToTable("order_status_changes");
        builder.Property(item => item.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Note).HasMaxLength(1000);
        builder.HasIndex(item => item.OrderId);
    }
}

internal sealed class OrderNoteConfiguration : IEntityTypeConfiguration<OrderNote>
{
    public void Configure(EntityTypeBuilder<OrderNote> builder)
    {
        builder.ToTable("order_notes");
        builder.Property(item => item.Text).HasMaxLength(2000);
        builder.HasIndex(item => item.OrderId);
    }
}
