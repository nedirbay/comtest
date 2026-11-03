using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace ComTest.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class SessionTests(ApiFixture api)
{
    [Fact]
    public async Task Refresh_RotatesTheToken_AndTheNewOneWorks()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateClient();

        var first = await RefreshAsync(client, signedIn.RefreshToken);
        var rotated = await SignedIn.FromResponseAsync(first);
        var second = await RefreshAsync(client, rotated.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotEqual(signedIn.RefreshToken, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_IsStoredOnlyAsAHash()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        var rawMatches = await api.QueryAsOwnerAsync<long>(
            "SELECT count(*) FROM refresh_tokens WHERE token_hash = @raw",
            ("raw", signedIn.RefreshToken));

        Assert.Equal(0L, rawMatches);
    }

    [Fact]
    public async Task ReusingAnOldToken_EndsEverySessionOfThatUser()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var victim = await api.LoginAsync(account);
        var otherDevice = await api.LoginAsync(account);
        using var client = api.CreateClient();

        var rotated = await SignedIn.FromResponseAsync(await RefreshAsync(client, victim.RefreshToken));

        await BackdateConsumedTokensAsync(account);

        var replay = await RefreshAsync(client, victim.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(ErrorCodes.Auth.RefreshInvalid, await ReadCode(replay));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, rotated.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, otherDevice.RefreshToken)).StatusCode);

        using var withAccess = api.CreateAuthorizedClient(rotated.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await withAccess.GetAsync("/api/v1/account/me")).StatusCode);

        Assert.Equal(1L, await api.QueryAsOwnerAsync<long>(
            "SELECT count(*) FROM security_events WHERE user_id = @id AND type = 'RefreshTokenReuseDetected'",
            ("id", account.UserId)));
    }

    [Fact]
    public async Task ReuseWithinTheGraceWindow_IssuesNothingAndKeepsTheSession()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = api.CreateClient();

        var rotated = await SignedIn.FromResponseAsync(await RefreshAsync(client, signedIn.RefreshToken));
        var replay = await RefreshAsync(client, signedIn.RefreshToken);

        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal(ErrorCodes.Auth.RefreshConflict, await ReadCode(replay));
        Assert.False(replay.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(client, rotated.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRefreshes_ProduceExactlyOneNewToken()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RefreshAsync(client, signedIn.RefreshToken)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.All(
            responses.Where(response => response.StatusCode != HttpStatusCode.OK),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
    }

    [Fact]
    public async Task UnknownRefreshToken_IsRejected()
    {
        using var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, "not-a-real-token")).StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutTheCsrfHeader_IsRejected()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateClient();

        var response = await client.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", signedIn.RefreshToken, withCsrfHeader: false));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.Auth.CsrfRejected, await ReadCode(response));
    }

    [Theory]
    [InlineData("https://attacker.test", HttpStatusCode.Forbidden)]
    [InlineData(ApiFixture.AllowedOrigin, HttpStatusCode.OK)]
    public async Task Refresh_ChecksTheOrigin(string origin, HttpStatusCode expected)
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateClient();
        using var request = ApiFixture.CookieRequest("/api/v1/auth/refresh", signedIn.RefreshToken);
        request.Headers.Add("Origin", origin);

        Assert.Equal(expected, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSessionAtOnce()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateClient();
        using var withAccess = api.CreateAuthorizedClient(signedIn.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await withAccess.GetAsync("/api/v1/account/me")).StatusCode);

        var logout = await client.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/logout", signedIn.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await withAccess.GetAsync("/api/v1/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, signedIn.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Sessions_CanBeListedAndRevoked()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var phone = await api.LoginAsync(account);
        var laptop = await api.LoginAsync(account);
        using var client = api.CreateAuthorizedClient(laptop.AccessToken);

        var sessions = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/account/sessions");

        Assert.Equal(2, sessions!.Length);
        Assert.Equal(1, sessions.Count(session => session.GetProperty("isCurrent").GetBoolean()));

        var other = sessions.Single(session => !session.GetProperty("isCurrent").GetBoolean()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/account/sessions/{other}")).StatusCode);

        using var phoneClient = api.CreateAuthorizedClient(phone.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phoneClient.GetAsync("/api/v1/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/me")).StatusCode);
    }

    [Fact]
    public async Task RevokingAnotherUsersSession_LooksLikeNotFound_AndChangesNothing()
    {
        var victim = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        var attacker = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var victimClient = api.CreateAuthorizedClient(victim.AccessToken);
        var victimSession = (await victimClient.GetFromJsonAsync<JsonElement[]>("/api/v1/account/sessions"))![0].GetProperty("id").GetGuid();

        using var attackerClient = api.CreateAuthorizedClient(attacker.AccessToken);
        var attempt = await attackerClient.DeleteAsync($"/api/v1/account/sessions/{victimSession}");

        Assert.Equal(HttpStatusCode.NotFound, attempt.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await victimClient.GetAsync("/api/v1/account/me")).StatusCode);
    }

    [Fact]
    public async Task RevokeOtherSessions_KeepsOnlyTheCurrentOne()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var first = await api.LoginAsync(account);
        var second = await api.LoginAsync(account);
        var current = await api.LoginAsync(account);
        using var client = api.CreateAuthorizedClient(current.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/account/sessions")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/me")).StatusCode);

        using var firstClient = api.CreateAuthorizedClient(first.AccessToken);
        using var secondClient = api.CreateAuthorizedClient(second.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await firstClient.GetAsync("/api/v1/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await secondClient.GetAsync("/api/v1/account/me")).StatusCode);
    }

    [Fact]
    public async Task SecurityStampChange_InvalidatesAccessTokens_AndRefreshPicksUpNewRoles()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/me")).StatusCode);

        await using (var scope = api.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ComTest.Infrastructure.Identity.ApplicationUser>>();
            var user = await users.FindByIdAsync(account.UserId.ToString());
            await users.AddToRoleAsync(user!, "Manager");
            await users.UpdateSecurityStampAsync(user!);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/me")).StatusCode);

        using var anonymous = api.CreateClient();
        var refreshed = await SignedIn.FromResponseAsync(await RefreshAsync(anonymous, signedIn.RefreshToken));
        var token = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(refreshed.AccessToken);

        Assert.Contains(token.Claims, claim => claim.Type == "role" && claim.Value == "Manager");
        Assert.True(refreshed.Body.GetProperty("twoFactorSetupRequired").GetBoolean());
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string token) =>
        client.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", token));

    private Task BackdateConsumedTokensAsync(TestAccount account) =>
        api.ExecuteAsOwnerAsync(
            """
            UPDATE refresh_tokens SET consumed_at = now() - interval '1 hour'
            WHERE consumed_at IS NOT NULL
              AND session_id IN (SELECT id FROM user_sessions WHERE user_id = @id)
            """,
            ("id", account.UserId));

    private static async Task<string?> ReadCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}
