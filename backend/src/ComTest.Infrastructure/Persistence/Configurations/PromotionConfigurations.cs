using ComTest.Domain.Modules.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComTest.Infrastructure.Persistence.Configurations;

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("promotions");
        builder.Ignore(item => item.IsAutomatic);
        builder.Property(item => item.Code).HasMaxLength(PromotionCode.MaxLength);
        builder.Property(item => item.Name).HasMaxLength(200);
        builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Currency).HasMaxLength(3);
        builder.HasIndex(item => new { item.TenantId, item.Code }).IsUnique().HasFilter("code IS NOT NULL AND NOT is_deleted");
    }
}

internal sealed class PromotionRedemptionConfiguration : IEntityTypeConfiguration<PromotionRedemption>
{
    public void Configure(EntityTypeBuilder<PromotionRedemption> builder)
    {
        builder.ToTable("promotion_redemptions");
        builder.Property(item => item.CustomerKey).HasMaxLength(64);
        builder.HasOne<Promotion>().WithMany().HasForeignKey(item => item.PromotionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.TenantId, item.OrderId });

        // Sequence is null without a per-customer limit, and PostgreSQL counts each null as different.
        builder.HasIndex(item => new { item.TenantId, item.PromotionId, item.CustomerKey, item.Sequence }).IsUnique();
    }
}
