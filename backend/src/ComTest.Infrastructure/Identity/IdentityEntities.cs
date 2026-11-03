using Microsoft.AspNetCore.Identity;

namespace ComTest.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public string FullName { get; set; } = string.Empty;

    public string Locale { get; set; } = "tk";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Hash of the last accepted authenticator code, so a code cannot be replayed.</summary>
    public string? LastTotpCodeHash { get; set; }

    public DateTimeOffset? LastTotpUsedAt { get; set; }
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(Guid id, string name)
        : base(name)
    {
        Id = id;
        NormalizedName = name.ToUpperInvariant();
        ConcurrencyStamp = id.ToString("N");
    }
}

/// <summary>One signed-in device. Revoking it cuts off both its access and refresh tokens.</summary>
public sealed class UserSession
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid UserId { get; init; }

    public ApplicationUser User { get; init; } = null!;

    /// <summary>True when this session passed a second factor.</summary>
    public bool Mfa { get; set; }

    /// <summary>The firm this session is working in. The staff role in the token comes from it.</summary>
    public Guid? ActiveTenantId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Absolute limit: rotating refresh tokens cannot extend a session past this.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }
}

/// <summary>A single-use refresh token. Only its hash is stored.</summary>
public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid SessionId { get; init; }

    public UserSession Session { get; init; } = null!;

    public required string TokenHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? ConsumedAt { get; set; }
}

public sealed class PhoneVerificationCode
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid UserId { get; init; }

    public required string PhoneNumber { get; init; }

    public required string CodeHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public int FailedAttempts { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}

public enum SecurityEventType
{
    Registered,
    EmailConfirmed,
    LoginSucceeded,
    LoginFailed,
    LoginBlockedByLockout,
    AccountLockedOut,
    LoginEmailNotConfirmed,
    TwoFactorChallenged,
    TwoFactorSucceeded,
    TwoFactorFailed,
    RecoveryCodeUsed,
    TwoFactorEnabled,
    TwoFactorDisabled,
    RecoveryCodesRegenerated,
    RefreshTokenReuseDetected,
    Logout,
    SessionRevoked,
    OtherSessionsRevoked,
    PasswordResetRequested,
    PasswordResetCompleted,
    PasswordChanged,
    PasswordChangeFailed,
    PhoneCodeSent,
    PhoneCodeFailed,
    PhoneVerified,
}

/// <summary>Append-only security log. The application role cannot update or delete these rows.</summary>
public sealed class SecurityEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public SecurityEventType Type { get; init; }

    public Guid? UserId { get; init; }

    /// <summary>Hash of the attempted login, so failures can be correlated without storing it.</summary>
    public string? IdentifierHash { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}
