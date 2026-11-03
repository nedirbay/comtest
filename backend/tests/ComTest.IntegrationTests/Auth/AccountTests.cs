using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace ComTest.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class PasswordResetTests(ApiFixture api)
{
    [Fact]
    public async Task ForgotPassword_AnswersTheSameForUnknownEmail_AndSendsNothing()
    {
        var address = $"unknown{Guid.NewGuid():N}@example.test";
        using var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = address });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(api.Emails.For(address));
    }

    [Fact]
    public async Task ResetPassword_ChangesThePassword_AndEndsAllSessions()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = account.Email })).StatusCode);

        var (userId, token) = api.Emails.ReadLink(account.Email);
        var newPassword = ApiFixture.NewPassword();

        var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { userId, token, newPassword });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = account.Password });
        var newLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = newPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        using var oldSession = api.CreateAuthorizedClient(signedIn.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/v1/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", signedIn.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task ResetToken_WorksOnlyOnce()
    {
        var account = await api.CreateConfirmedAccountAsync();
        using var client = api.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = account.Email });

        var (userId, token) = api.Emails.ReadLink(account.Email);

        var first = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { userId, token, newPassword = ApiFixture.NewPassword() });
        var second = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { userId, token, newPassword = ApiFixture.NewPassword() });

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(ErrorCodes.Auth.InvalidToken, await ReadCode(second));
    }

    [Fact]
    public async Task ResetToken_CannotBeUsedOnAnotherAccount()
    {
        var victim = await api.CreateConfirmedAccountAsync();
        var attacker = await api.CreateConfirmedAccountAsync();
        using var client = api.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = attacker.Email });

        var (_, token) = api.Emails.ReadLink(attacker.Email);

        var attempt = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new { userId = victim.UserId, token, newPassword = ApiFixture.NewPassword() });

        Assert.Equal(HttpStatusCode.BadRequest, attempt.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { login = victim.Email, password = victim.Password })).StatusCode);
    }

    [Fact]
    public async Task ResetToken_LivesForThirtyMinutes()
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();

        var options = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Identity.DataProtectionTokenProviderOptions>>();

        Assert.Equal(TimeSpan.FromMinutes(30), options.Value.TokenLifespan);
    }

    [Fact]
    public async Task EmailLink_CarriesTheTokenInTheFragment()
    {
        var account = await api.RegisterAsync();

        var body = api.Emails.For(account.Email)[^1].TextBody;

        Assert.Contains("confirm-email#userId=", body, StringComparison.Ordinal);
        Assert.DoesNotContain("?token=", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangePassword_RequiresTheCurrentOne_AndEndsOtherSessions()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var other = await api.LoginAsync(account);
        var current = await api.LoginAsync(account);
        using var client = api.CreateAuthorizedClient(current.AccessToken);

        var wrong = await client.PostAsJsonAsync("/api/v1/account/password", new { currentPassword = "wrong-password-value", newPassword = ApiFixture.NewPassword() });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(ErrorCodes.Auth.CurrentPasswordInvalid, await ReadCode(wrong));

        var newPassword = ApiFixture.NewPassword();
        var changed = await client.PostAsJsonAsync("/api/v1/account/password", new { currentPassword = account.Password, newPassword });

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using var anonymous = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", other.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", current.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_RejectsATamperedToken()
    {
        var account = await api.RegisterAsync();
        var (userId, _) = api.Emails.ReadLink(account.Email);
        using var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId, token = "not-a-valid-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<string?> ReadCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}

[Collection(ApiCollection.Name)]
public sealed class TwoFactorTests(ApiFixture api)
{
    [Fact]
    public async Task Setup_ThenEnable_ReturnsRecoveryCodes_AndLoginRequiresASecondFactor()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var (key, codes) = await EnableTwoFactorAsync(account);

        Assert.Equal(10, codes.Length);

        using var client = api.CreateClient();
        var step1 = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = account.Password });
        var body = await step1.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("twoFactorRequired").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("accessToken").ValueKind);
        Assert.False(step1.Headers.Contains("Set-Cookie"));

        var step2 = await client.PostAsJsonAsync(
            "/api/v1/auth/login/two-factor",
            new { challengeToken = body.GetProperty("challengeToken").GetString(), code = FreshCode(key) });

        Assert.Equal(HttpStatusCode.OK, step2.StatusCode);

        var signedIn = await SignedIn.FromResponseAsync(step2);
        var token = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(signedIn.AccessToken);

        Assert.Contains(token.Claims, claim => claim.Type == "mfa");
    }

    [Fact]
    public async Task ChallengeToken_IsNotAnAccessToken()
    {
        var account = await api.CreateConfirmedAccountAsync();
        await EnableTwoFactorAsync(account);

        var challenge = await ChallengeAsync(account);
        using var client = api.CreateAuthorizedClient(challenge);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/me")).StatusCode);
    }

    [Fact]
    public async Task WrongCode_IsRejected_AndCountsTowardLockout()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var (key, _) = await EnableTwoFactorAsync(account);
        var challenge = await ChallengeAsync(account);
        using var client = api.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var wrong = await client.PostAsJsonAsync("/api/v1/auth/login/two-factor", new { challengeToken = challenge, code = "000000" });

            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var correct = await client.PostAsJsonAsync("/api/v1/auth/login/two-factor", new { challengeToken = challenge, code = FreshCode(key) });

        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
    }

    [Fact]
    public async Task AuthenticatorCode_CannotBeReplayed()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var (key, _) = await EnableTwoFactorAsync(account);
        using var client = api.CreateClient();
        var code = Totp.Compute(key);

        await api.ExecuteAsOwnerAsync(
            "UPDATE users SET last_totp_code_hash = NULL, last_totp_used_at = NULL WHERE id = @id",
            ("id", account.UserId));

        var first = await client.PostAsJsonAsync("/api/v1/auth/login/two-factor", new { challengeToken = await ChallengeAsync(account), code });
        var replay = await client.PostAsJsonAsync("/api/v1/auth/login/two-factor", new { challengeToken = await ChallengeAsync(account), code });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task RecoveryCode_WorksOnlyOnce()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var (_, codes) = await EnableTwoFactorAsync(account);
        using var client = api.CreateClient();

        var first = await client.PostAsJsonAsync("/api/v1/auth/login/two-factor", new { challengeToken = await ChallengeAsync(account), recoveryCode = codes[0] });
        var second = await client.PostAsJsonAsync("/api/v1/auth/login/two-factor", new { challengeToken = await ChallengeAsync(account), recoveryCode = codes[0] });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Secrets_AreNotStoredInPlainText()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var (key, codes) = await EnableTwoFactorAsync(account);

        var storedKey = await api.QueryAsOwnerAsync<string>(
            "SELECT value FROM user_tokens WHERE user_id = @id AND name = 'AuthenticatorKey'",
            ("id", account.UserId));
        var storedCodes = await api.QueryAsOwnerAsync<string>(
            "SELECT value FROM user_tokens WHERE user_id = @id AND name = 'RecoveryCodes'",
            ("id", account.UserId));

        Assert.NotNull(storedKey);
        Assert.NotEqual(key, storedKey);
        Assert.DoesNotContain(key, storedKey, StringComparison.Ordinal);
        Assert.All(codes, code => Assert.DoesNotContain(code, storedCodes!, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Staff_CannotUsePermissions_UntilTheyHaveTwoFactor()
    {
        var account = await api.CreateConfirmedAccountAsync(Roles.Manager);
        var signedIn = await api.LoginAsync(account);

        Assert.True(signedIn.Body.GetProperty("twoFactorSetupRequired").GetBoolean());

        using var withoutMfa = api.CreateAuthorizedClient(signedIn.AccessToken);
        var refused = await withoutMfa.GetAsync("/test/catalog-write");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(ErrorCodes.Auth.MfaRequired, await ReadCode(refused));

        await EnableTwoFactorAsync(account, signedIn);

        using var anonymous = api.CreateClient();
        var refreshed = await SignedIn.FromResponseAsync(await anonymous.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", signedIn.RefreshToken)));

        using var withMfa = api.CreateAuthorizedClient(refreshed.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await withMfa.GetAsync("/test/catalog-write")).StatusCode);
    }

    [Fact]
    public async Task Permissions_AreEnforcedEvenWithMfa()
    {
        var account = await api.CreateConfirmedAccountAsync(Roles.Manager);
        var signedIn = await api.LoginAsync(account);
        await EnableTwoFactorAsync(account, signedIn);

        using var anonymous = api.CreateClient();
        var refreshed = await SignedIn.FromResponseAsync(await anonymous.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", signedIn.RefreshToken)));
        using var client = api.CreateAuthorizedClient(refreshed.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/test/refund")).StatusCode);
    }

    [Fact]
    public async Task Customer_WithoutThePermission_IsForbidden()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/test/catalog-write")).StatusCode);
    }

    [Fact]
    public async Task Staff_CannotTurnOffTwoFactor()
    {
        var account = await api.CreateConfirmedAccountAsync(Roles.Staff);
        var signedIn = await api.LoginAsync(account);
        var (key, _) = await EnableTwoFactorAsync(account, signedIn);

        using var anonymous = api.CreateClient();
        var refreshed = await SignedIn.FromResponseAsync(await anonymous.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", signedIn.RefreshToken)));
        using var client = api.CreateAuthorizedClient(refreshed.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/account/two-factor/disable", new { password = account.Password, code = FreshCode(key) });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.Auth.TwoFactorRequiredForStaff, await ReadCode(response));
    }

    [Fact]
    public async Task Customer_CanTurnOffTwoFactor_WithPasswordAndCode()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        var (key, _) = await EnableTwoFactorAsync(account, signedIn);

        using var anonymous = api.CreateClient();
        var refreshed = await SignedIn.FromResponseAsync(await anonymous.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", signedIn.RefreshToken)));
        using var client = api.CreateAuthorizedClient(refreshed.AccessToken);

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/account/two-factor/disable", new { password = "wrong-password-value", code = FreshCode(key) });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);

        var disabled = await client.PostAsJsonAsync("/api/v1/account/two-factor/disable", new { password = account.Password, code = FreshCode(key) });
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);

        var login = await SignedIn.FromResponseAsync(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = account.Password }));
        Assert.False(login.Body.GetProperty("twoFactorRequired").GetBoolean());
    }

    private async Task<(string Key, string[] Codes)> EnableTwoFactorAsync(TestAccount account, SignedIn? session = null)
    {
        session ??= await api.LoginAsync(account);
        using var client = api.CreateAuthorizedClient(session.AccessToken);

        var setup = await client.PostAsync("/api/v1/account/two-factor/setup", null);
        setup.EnsureSuccessStatusCode();

        var key = (await setup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sharedKey").GetString()!;

        var enable = await client.PostAsJsonAsync("/api/v1/account/two-factor/enable", new { code = Totp.Compute(key) });
        enable.EnsureSuccessStatusCode();

        var codes = (await enable.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codes")
            .EnumerateArray().Select(code => code.GetString()!).ToArray();

        return (key, codes);
    }

    private async Task<string> ChallengeAsync(TestAccount account)
    {
        using var client = api.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = account.Password });

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("challengeToken").GetString()!;
    }

    /// <summary>The enable step already used the current code, so sign-in needs the next time step's.</summary>
    private static string FreshCode(string key) => Totp.Compute(key, stepOffset: 1);

    private static async Task<string?> ReadCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}

[Collection(ApiCollection.Name)]
public sealed class PhoneVerificationTests(ApiFixture api)
{
    [Fact]
    public async Task VerifiedPhone_CanBeUsedToSignIn()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        var phone = NewPhone();
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber = phone })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/account/phone/verify", new { code = api.Sms.LastCode(phone) })).StatusCode);

        using var anonymous = api.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { login = phone, password = account.Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task UnverifiedPhone_CannotBeUsedToSignIn()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        var phone = NewPhone();
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber = phone });

        using var anonymous = api.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { login = phone, password = account.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Code_StopsWorkingAfterFiveWrongAttempts()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        var phone = NewPhone();
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber = phone });

        var real = api.Sms.LastCode(phone);
        var wrong = real == "000000" ? "000001" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/account/phone/verify", new { code = wrong })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/account/phone/verify", new { code = real })).StatusCode);
    }

    [Fact]
    public async Task ExpiredCode_IsRejected()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        var phone = NewPhone();
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber = phone });
        await api.ExecuteAsOwnerAsync("UPDATE phone_verification_codes SET expires_at = now() - interval '1 second' WHERE user_id = @id", ("id", account.UserId));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/account/phone/verify", new { code = api.Sms.LastCode(phone) })).StatusCode);
    }

    [Fact]
    public async Task SendCode_IsLimited_PerUserAndPerNumber()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        var phone = NewPhone();
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber = phone })).StatusCode);

        var tooSoon = await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber = phone });

        Assert.Equal(HttpStatusCode.TooManyRequests, tooSoon.StatusCode);

        // Past the resend interval, but with five codes for this number in the last hour.
        await api.ExecuteAsOwnerAsync(
            """
            INSERT INTO phone_verification_codes (id, user_id, phone_number, code_hash, created_at, expires_at, failed_attempts)
            SELECT gen_random_uuid(), @id, @phone, 'x', now() - interval '10 minutes', now(), 0 FROM generate_series(1, 5)
            """,
            ("id", account.UserId),
            ("phone", phone));

        var other = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var otherClient = api.CreateAuthorizedClient(other.AccessToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await otherClient.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber = phone })).StatusCode);
    }

    [Fact]
    public async Task Code_IsStoredHashed()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        var phone = NewPhone();
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber = phone });

        var stored = await api.QueryAsOwnerAsync<string>(
            "SELECT code_hash FROM phone_verification_codes WHERE user_id = @id",
            ("id", account.UserId));

        Assert.NotEqual(api.Sms.LastCode(phone), stored);
    }

    [Fact]
    public async Task AlreadyTakenNumber_IsOnlyReportedAfterTheCodeIsProven()
    {
        var phone = NewPhone();

        var owner = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var ownerClient = api.CreateAuthorizedClient(owner.AccessToken);
        await ownerClient.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber = phone });
        await ownerClient.PostAsJsonAsync("/api/v1/account/phone/verify", new { code = api.Sms.LastCode(phone) });

        var second = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var secondClient = api.CreateAuthorizedClient(second.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, (await secondClient.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber = phone })).StatusCode);

        var verify = await secondClient.PostAsJsonAsync("/api/v1/account/phone/verify", new { code = api.Sms.LastCode(phone) });

        Assert.Equal(HttpStatusCode.Conflict, verify.StatusCode);
    }

    [Fact]
    public async Task SmsProviderFailure_IsReported()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        api.Sms.Available = false;

        try
        {
            var response = await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber = NewPhone() });

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            api.Sms.Available = true;
        }
    }

    [Theory]
    [InlineData("65123456")]
    [InlineData("+0123")]
    [InlineData("not-a-phone")]
    public async Task InvalidPhoneNumbers_AreRejected(string phoneNumber)
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/account/phone/send-code", new { phoneNumber })).StatusCode);
    }

    private static string NewPhone() =>
        "+9936" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
