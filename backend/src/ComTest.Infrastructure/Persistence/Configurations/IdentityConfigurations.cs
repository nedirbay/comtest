using ComTest.Infrastructure.Identity;
using ComTest.Infrastructure.Persistence.Outbox;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComTest.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");
        builder.Property(user => user.FullName).HasMaxLength(200);
        builder.Property(user => user.Locale).HasMaxLength(5);
        builder.Property(user => user.LastTotpCodeHash).HasMaxLength(64);

        // The database is the final guard against two accounts claiming one email or number.
        builder.HasIndex(user => user.NormalizedEmail).HasDatabaseName("ix_users_normalized_email").IsUnique();
        builder.HasIndex(user => user.PhoneNumber)
            .HasDatabaseName("ix_users_confirmed_phone_number")
            .IsUnique()
            .HasFilter("phone_number_confirmed");
    }
}

internal sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder) => builder.ToTable("roles");
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder) => builder.ToTable("user_roles");
}

internal sealed class UserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("user_claims");
}

internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("user_logins");
}

internal sealed class RoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder) => builder.ToTable("role_claims");
}

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("user_tokens");
}

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");
        builder.Property(session => session.IpAddress).HasMaxLength(45);
        builder.Property(session => session.UserAgent).HasMaxLength(256);
        builder.Property(session => session.RevokedReason).HasMaxLength(50);
        builder.HasIndex(session => new { session.UserId, session.RevokedAt });
        builder.HasIndex(session => session.ActiveTenantId);
        builder.HasOne(session => session.User).WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.Property(token => token.TokenHash).HasMaxLength(64);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.ExpiresAt);
        builder.HasOne(token => token.Session).WithMany().HasForeignKey(token => token.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PhoneVerificationCodeConfiguration : IEntityTypeConfiguration<PhoneVerificationCode>
{
    public void Configure(EntityTypeBuilder<PhoneVerificationCode> builder)
    {
        builder.ToTable("phone_verification_codes");
        builder.Property(code => code.PhoneNumber).HasMaxLength(16);
        builder.Property(code => code.CodeHash).HasMaxLength(64);
        builder.HasIndex(code => new { code.UserId, code.CreatedAt });
        builder.HasIndex(code => new { code.PhoneNumber, code.CreatedAt });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(code => code.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("security_events");
        builder.Property(entry => entry.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(entry => entry.IdentifierHash).HasMaxLength(64);
        builder.Property(entry => entry.IpAddress).HasMaxLength(45);
        builder.Property(entry => entry.UserAgent).HasMaxLength(256);
        builder.HasIndex(entry => new { entry.UserId, entry.OccurredAt });
        builder.HasIndex(entry => new { entry.Type, entry.OccurredAt });
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.Property(message => message.Type).HasMaxLength(300);
        builder.Property(message => message.Payload).HasColumnType("jsonb");
        builder.Property(message => message.LastError).HasMaxLength(200);
        builder.HasIndex(message => message.NextAttemptAt)
            .HasFilter("processed_at IS NULL AND dead_lettered_at IS NULL");
    }
}
