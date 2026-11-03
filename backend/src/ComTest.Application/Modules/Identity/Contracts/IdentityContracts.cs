using ComTest.Shared.Results;

namespace ComTest.Application.Modules.Identity.Contracts;

// Requests and responses that carry secrets override ToString, so that logging one by
// accident cannot write a password or token out the way a record's default ToString would.
public sealed record ClientInfo(string? IpAddress, string? UserAgent);

public sealed record RegisterRequest(string Email, string Password, string FullName, string? Locale = null)
{
    public override string ToString() => nameof(RegisterRequest);
}

public sealed record LoginRequest(string Login, string Password)
{
    public override string ToString() => nameof(LoginRequest);
}

public sealed record TwoFactorLoginRequest(string ChallengeToken, string? Code = null, string? RecoveryCode = null)
{
    public override string ToString() => nameof(TwoFactorLoginRequest);
}

public sealed record ConfirmEmailRequest(Guid UserId, string Token)
{
    public override string ToString() => nameof(ConfirmEmailRequest);
}

public sealed record EmailRequest(string Email);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword)
{
    public override string ToString() => nameof(ResetPasswordRequest);
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword)
{
    public override string ToString() => nameof(ChangePasswordRequest);
}

public sealed record UpdateProfileRequest(string FullName, string? Locale = null);

public sealed record ChangeEmailRequest(string Password, string NewEmail)
{
    public override string ToString() => nameof(ChangeEmailRequest);
}

public sealed record ConfirmEmailChangeRequest(Guid UserId, string NewEmail, string Token)
{
    public override string ToString() => nameof(ConfirmEmailChangeRequest);
}

public sealed record TwoFactorCodeRequest(string Code)
{
    public override string ToString() => nameof(TwoFactorCodeRequest);
}

public sealed record DisableTwoFactorRequest(string Password, string Code)
{
    public override string ToString() => nameof(DisableTwoFactorRequest);
}

public sealed record SendPhoneCodeRequest(string PhoneNumber);

public sealed record VerifyPhoneRequest(string Code)
{
    public override string ToString() => nameof(VerifyPhoneRequest);
}

public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    bool TwoFactorSetupRequired)
{
    public override string ToString() => nameof(AuthTokens);
}

/// <summary>Either signed-in tokens, or a challenge to finish with a second factor.</summary>
public sealed record LoginOutcome(AuthTokens? Tokens, string? TwoFactorChallengeToken)
{
    public override string ToString() => nameof(LoginOutcome);
}

public sealed record SessionDto(
    Guid Id,
    bool IsCurrent,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt);

public sealed record ProfileDto(
    Guid Id,
    string Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    string FullName,
    string Locale,
    bool TwoFactorEnabled,
    IReadOnlyList<string> Roles);

public sealed record TwoFactorSetupDto(string SharedKey, string AuthenticatorUri)
{
    public override string ToString() => nameof(TwoFactorSetupDto);
}

public sealed record RecoveryCodesDto(IReadOnlyList<string> Codes)
{
    public override string ToString() => nameof(RecoveryCodesDto);
}

public interface ISignInService
{
    Task<Result<LoginOutcome>> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken cancellationToken);

    Task<Result<AuthTokens>> CompleteTwoFactorAsync(TwoFactorLoginRequest request, ClientInfo client, CancellationToken cancellationToken);

    Task<Result<AuthTokens>> RefreshAsync(string refreshToken, ClientInfo client, CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, ClientInfo client, CancellationToken cancellationToken);
}

public interface IAccountService
{
    Task<Result> RegisterAsync(RegisterRequest request, ClientInfo client, CancellationToken cancellationToken);

    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, ClientInfo client, CancellationToken cancellationToken);

    Task ResendConfirmationAsync(EmailRequest request, CancellationToken cancellationToken);

    Task ForgotPasswordAsync(EmailRequest request, ClientInfo client, CancellationToken cancellationToken);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, ClientInfo client, CancellationToken cancellationToken);

    Task<Result> ChangePasswordAsync(Guid userId, Guid? currentSessionId, ChangePasswordRequest request, ClientInfo client, CancellationToken cancellationToken);

    Task<Result<ProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<ProfileDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, ClientInfo client, CancellationToken cancellationToken);

    /// <summary>Answers the same whether or not the new address is free, so it cannot be used to probe accounts.</summary>
    Task<Result> RequestEmailChangeAsync(Guid userId, ChangeEmailRequest request, ClientInfo client, CancellationToken cancellationToken);

    Task<Result> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, ClientInfo client, CancellationToken cancellationToken);
}

public interface ISessionService
{
    Task<IReadOnlyList<SessionDto>> ListAsync(Guid userId, Guid? currentSessionId, CancellationToken cancellationToken);

    Task<Result> RevokeAsync(Guid userId, Guid sessionId, ClientInfo client, CancellationToken cancellationToken);

    Task RevokeOthersAsync(Guid userId, Guid? currentSessionId, ClientInfo client, CancellationToken cancellationToken);
}

public interface ITwoFactorService
{
    Task<Result<TwoFactorSetupDto>> BeginSetupAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<RecoveryCodesDto>> EnableAsync(Guid userId, Guid? currentSessionId, TwoFactorCodeRequest request, ClientInfo client, CancellationToken cancellationToken);

    Task<Result> DisableAsync(Guid userId, Guid? currentSessionId, DisableTwoFactorRequest request, ClientInfo client, CancellationToken cancellationToken);

    Task<Result<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(Guid userId, TwoFactorCodeRequest request, ClientInfo client, CancellationToken cancellationToken);
}

public sealed record SwitchedTenant(string AccessToken, DateTimeOffset ExpiresAt)
{
    public override string ToString() => nameof(SwitchedTenant);
}

public sealed record SwitchTenantRequest(Guid TenantId);

public interface ITenantSessionService
{
    /// <summary>Makes a tenant active in the session and issues an access token with that tenant's role.</summary>
    Task<Result<SwitchedTenant>> SwitchAsync(Guid userId, Guid sessionId, Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserMembership>> ListMembershipsAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record UserMembership(Guid TenantId, string TenantName, string TenantSlug, string Role);

public interface IPhoneVerificationService
{
    Task<Result> SendCodeAsync(Guid userId, SendPhoneCodeRequest request, ClientInfo client, CancellationToken cancellationToken);

    Task<Result> VerifyAsync(Guid userId, VerifyPhoneRequest request, ClientInfo client, CancellationToken cancellationToken);
}
