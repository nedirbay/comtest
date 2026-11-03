using Asp.Versioning;
using ComTest.Api.Hosting;
using ComTest.Api.Security;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComTest.Api.Controllers.V1;

public sealed record LoginResponse(
    bool TwoFactorRequired,
    string? ChallengeToken,
    string? AccessToken,
    DateTimeOffset? ExpiresAt,
    bool TwoFactorSetupRequired)
{
    public override string ToString() => nameof(LoginResponse);
}

/// <summary>Public sign-in endpoints. Each one is listed as anonymous and has its own rate limit.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
[AllowAnonymous]
public sealed class AuthController(
    ISignInService signIn,
    IAccountService accounts,
    RefreshCookie refreshCookie) : ApiControllerBase
{
    private const string SignedInDescription = "Signed in. The refresh token is set in an HttpOnly cookie.";

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await accounts.RegisterAsync(request, Client, cancellationToken);

        return result.IsSuccess ? Accepted() : Failure(result.Error);
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken cancellationToken) =>
        FromResult(await accounts.ConfirmEmailAsync(request, Client, cancellationToken));

    [HttpPost("confirm-email-change")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ConfirmEmailChange(ConfirmEmailChangeRequest request, CancellationToken cancellationToken) =>
        FromResult(await accounts.ConfirmEmailChangeAsync(request, Client, cancellationToken));

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendConfirmation(EmailRequest request, CancellationToken cancellationToken)
    {
        await accounts.ResendConfirmationAsync(request, cancellationToken);

        return Accepted();
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK, Description = "Signed in: accessToken is set and the refresh token is in an HttpOnly cookie. If twoFactorRequired is true, only challengeToken is set, no cookie is written, and sign-in continues at login/two-factor.")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await signIn.LoginAsync(request, Client, cancellationToken);

        if (result.IsFailure)
        {
            return Failure(result.Error);
        }

        return result.Value.Tokens is { } tokens
            ? SignedIn(tokens)
            : Ok(new LoginResponse(true, result.Value.TwoFactorChallengeToken, null, null, false));
    }

    [HttpPost("login/two-factor")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK, Description = SignedInDescription)]
    public async Task<IActionResult> LoginTwoFactor(TwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await signIn.CompleteTwoFactorAsync(request, Client, cancellationToken);

        return result.IsSuccess ? SignedIn(result.Value) : Failure(result.Error);
    }

    [HttpPost("refresh")]
    [RequireCsrfHeader]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK, Description = SignedInDescription)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var token = refreshCookie.Read(Request);

        if (string.IsNullOrEmpty(token))
        {
            return Failure(Error.Unauthorized(ErrorCodes.Auth.RefreshInvalid));
        }

        var result = await signIn.RefreshAsync(token, Client, cancellationToken);

        if (result.IsSuccess)
        {
            return SignedIn(result.Value);
        }

        // On a concurrent refresh the browser may already hold the newer cookie, so leave it alone.
        if (result.Error.Code != ErrorCodes.Auth.RefreshConflict)
        {
            refreshCookie.Delete(Response);
        }

        return Failure(result.Error);
    }

    [HttpPost("logout")]
    [RequireCsrfHeader]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [ProducesResponseType(StatusCodes.Status204NoContent, Description = "Signed out. The refresh cookie is cleared.")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await signIn.LogoutAsync(refreshCookie.Read(Request), Client, cancellationToken);
        refreshCookie.Delete(Response);

        return NoContent();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken cancellationToken)
    {
        await accounts.ForgotPasswordAsync(request, Client, cancellationToken);

        return Accepted();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        FromResult(await accounts.ResetPasswordAsync(request, Client, cancellationToken));

    private OkObjectResult SignedIn(AuthTokens tokens)
    {
        refreshCookie.Write(Response, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);

        return Ok(new LoginResponse(false, null, tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.TwoFactorSetupRequired));
    }
}
