using ComTest.Domain.Modules.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComTest.Infrastructure.Persistence.Configurations;

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets");
        builder.Property(item => item.DisplayName).HasMaxLength(200);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.IncomingKey).HasMaxLength(200);
        builder.Property(item => item.BaseKey).HasMaxLength(200);
        builder.Property(item => item.RejectionCode).HasMaxLength(100);
        builder.HasIndex(item => new { item.TenantId, item.Status });
        builder.HasIndex(item => new { item.Status, item.UploadExpiresAt });
    }
}
