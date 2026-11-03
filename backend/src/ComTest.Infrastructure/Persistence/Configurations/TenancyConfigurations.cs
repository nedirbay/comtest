using ComTest.Domain.Modules.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComTest.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.Property(tenant => tenant.Name).HasMaxLength(200);
        builder.Property(tenant => tenant.Slug).HasMaxLength(63);
        builder.Property(tenant => tenant.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(tenant => tenant.Plan).HasConversion<string>().HasMaxLength(20);
        builder.Property(tenant => tenant.DefaultCurrency).HasMaxLength(3).IsFixedLength();
        builder.Property(tenant => tenant.DefaultLanguage).HasMaxLength(5);
        builder.Property(tenant => tenant.LogoUrl).HasMaxLength(500);
        builder.Property(tenant => tenant.ContactEmail).HasMaxLength(254);
        builder.Property(tenant => tenant.ContactPhone).HasMaxLength(32);
        builder.Property(tenant => tenant.Address).HasMaxLength(500);
        builder.Property(tenant => tenant.WorkingHours).HasMaxLength(200);
        builder.Property(tenant => tenant.BankTransferInstructions).HasMaxLength(2000);
        builder.Property(tenant => tenant.RefundApprovalThreshold).HasPrecision(18, 2);
        builder.HasIndex(tenant => tenant.Slug).IsUnique();
    }
}

internal sealed class TenantDomainConfiguration : IEntityTypeConfiguration<TenantDomain>
{
    public void Configure(EntityTypeBuilder<TenantDomain> builder)
    {
        builder.ToTable("tenant_domains");
        builder.Property(domain => domain.Hostname).HasMaxLength(253);
        builder.HasIndex(domain => domain.Hostname).IsUnique();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(domain => domain.OwnerTenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TenantMemberConfiguration : IEntityTypeConfiguration<TenantMember>
{
    public void Configure(EntityTypeBuilder<TenantMember> builder)
    {
        builder.ToTable("tenant_members");
        builder.Property(member => member.Role).HasMaxLength(50);
        builder.HasIndex(member => new { member.TenantId, member.UserId }).IsUnique();
        builder.HasIndex(member => member.UserId);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(member => member.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Identity.ApplicationUser>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StaffInvitationConfiguration : IEntityTypeConfiguration<StaffInvitation>
{
    public void Configure(EntityTypeBuilder<StaffInvitation> builder)
    {
        builder.ToTable("staff_invitations");
        builder.Property(invitation => invitation.NormalizedEmail).HasMaxLength(254);
        builder.Property(invitation => invitation.Role).HasMaxLength(50);
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(64);
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => invitation.TenantId);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(invitation => invitation.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
