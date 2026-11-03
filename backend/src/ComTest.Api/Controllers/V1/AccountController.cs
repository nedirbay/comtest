using Asp.Versioning;
using ComTest.Api.Hosting;
using ComTest.Application.Modules.Identity.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComTest.Api.Controllers.V1;

/// <summary>Self-service for the signed-in user. Every action works only on the caller's own account.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/account")]
[Authorize]
public sealed class AccountController(
    IAccountService accounts,
    ISessionService sessions,
    ITwoFactorService twoFactor,
    IPhoneVerificationService phone,
    ITenantSessionService tenantSessions) : ApiControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        FromResult(await accounts.GetProfileAsync(UserId, cancellationToken));

    [HttpPut("me")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        FromResult(await accounts.UpdateProfileAsync(UserId, request, Client, cancellationToken));

    /// <summary>Sends a link to the new address. The change happens when that link is opened.</summary>
    [HttpPost("email")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ChangeEmail(ChangeEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await accounts.RequestEmailChangeAsync(UserId, request, Client, cancellationToken);

        return result.IsSuccess ? Accepted() : Failure(result.Error);
    }

    [HttpPost("password")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        FromResult(await accounts.ChangePasswordAsync(UserId, SessionId, request, Client, cancellationToken));

    [HttpGet("sessions")]
    [ProducesResponseType<IReadOnlyList<SessionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Sessions(CancellationToken cancellationToken) =>
        Ok(await sessions.ListAsync(UserId, SessionId, cancellationToken));

    [HttpDelete("sessions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken cancellationToken) =>
        FromResult(await sessions.RevokeAsync(UserId, id, Client, cancellationToken));

    [HttpDelete("sessions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeOtherSessions(CancellationToken cancellationToken)
    {
        await sessions.RevokeOthersAsync(UserId, SessionId, Client, cancellationToken);

        return NoContent();
    }

    [HttpPost("two-factor/setup")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType<TwoFactorSetupDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> BeginTwoFactorSetup(CancellationToken cancellationToken) =>
        FromResult(await twoFactor.BeginSetupAsync(UserId, cancellationToken));

    [HttpPost("two-factor/enable")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType<RecoveryCodesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorCodeRequest request, CancellationToken cancellationToken) =>
        FromResult(await twoFactor.EnableAsync(UserId, SessionId, request, Client, cancellationToken));

    [HttpPost("two-factor/disable")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorRequest request, CancellationToken cancellationToken) =>
        FromResult(await twoFactor.DisableAsync(UserId, SessionId, request, Client, cancellationToken));

    [HttpPost("two-factor/recovery-codes")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType<RecoveryCodesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RegenerateRecoveryCodes(TwoFactorCodeRequest request, CancellationToken cancellationToken) =>
        FromResult(await twoFactor.RegenerateRecoveryCodesAsync(UserId, request, Client, cancellationToken));

    [HttpGet("tenants")]
    [ProducesResponseType<IReadOnlyList<UserMembership>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Tenants(CancellationToken cancellationToken) =>
        Ok(await tenantSessions.ListMembershipsAsync(UserId, cancellationToken));

    [HttpPost("tenant")]
    [ProducesResponseType<SwitchedTenant>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SwitchTenant(SwitchTenantRequest request, CancellationToken cancellationToken) =>
        FromResult(await tenantSessions.SwitchAsync(UserId, SessionId!.Value, request.TenantId, cancellationToken));

    [HttpPost("phone/send-code")]
    [EnableRateLimiting(RateLimitPolicies.Sms)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendPhoneCode(SendPhoneCodeRequest request, CancellationToken cancellationToken) =>
        FromResult(await phone.SendCodeAsync(UserId, request, Client, cancellationToken));

    [HttpPost("phone/verify")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> VerifyPhone(VerifyPhoneRequest request, CancellationToken cancellationToken) =>
        FromResult(await phone.VerifyAsync(UserId, request, Client, cancellationToken));
}
