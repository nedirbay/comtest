using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ComTest.IntegrationTests.Support;

/// <summary>A store with an owner who has finished two-factor setup, so permissions work.</summary>
public sealed record TestStore(TestAccount Owner, string AccessToken, string RefreshToken, Guid TenantId, string Slug, string TotpKey);

public static partial class StoreHelpers
{
    public static HttpClient CreateStoreClient(this ApiFixture api, string slug, string? accessToken = null)
    {
        var client = api.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{slug}.{ApiFixture.BaseDomain}"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }

    public static HttpClient CreatePlatformClient(this ApiFixture api, string? accessToken = null) =>
        api.CreateStoreClient("api", accessToken);

    public static async Task<string> CreatePlatformAdminAsync(this ApiFixture api)
    {
        var account = await api.CreateConfirmedAccountAsync(ComTest.Application.Modules.Identity.Contracts.Roles.PlatformAdmin);
        var signedIn = await api.LoginAsync(account);
        var (accessToken, _, _) = await api.EnableMfaAsync(signedIn.AccessToken, signedIn.RefreshToken);

        return accessToken;
    }

    public static string NewSlug() => "store-" + Guid.NewGuid().ToString("N")[..12];

    public static async Task<TestStore> CreateStoreAsync(this ApiFixture api, string? slug = null)
    {
        slug ??= NewSlug();

        var owner = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(owner);

        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);
        var created = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "Store " + slug, slug });
        created.EnsureSuccessStatusCode();

        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = body.GetProperty("tenant").GetProperty("id").GetGuid();
        var token = body.GetProperty("accessToken").GetString()!;

        var (accessToken, refreshToken, key) = await api.EnableMfaAsync(token, signedIn.RefreshToken);

        return new TestStore(owner, accessToken, refreshToken, tenantId, slug, key);
    }

    /// <summary>Turns on two-factor, then refreshes, as a real client would, to get the mfa claim.</summary>
    public static async Task<(string AccessToken, string RefreshToken, string TotpKey)> EnableMfaAsync(this ApiFixture api, string accessToken, string refreshToken)
    {
        using var client = api.CreateAuthorizedClient(accessToken);

        var setup = await client.PostAsync("/api/v1/account/two-factor/setup", null);
        setup.EnsureSuccessStatusCode();

        var key = (await setup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sharedKey").GetString()!;

        var enable = await client.PostAsJsonAsync("/api/v1/account/two-factor/enable", new { code = Totp.Compute(key) });
        enable.EnsureSuccessStatusCode();

        using var anonymous = api.CreateClient();
        var refreshed = await anonymous.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", refreshToken));
        refreshed.EnsureSuccessStatusCode();

        var session = await SignedIn.FromResponseAsync(refreshed);

        return (session.AccessToken, session.RefreshToken, key);
    }

    /// <summary>Invites someone, has them accept and set up two-factor, and returns their working token.</summary>
    public static async Task<(TestAccount Account, string AccessToken, string RefreshToken)> AddStaffAsync(this ApiFixture api, TestStore store, string role)
    {
        var account = await api.CreateConfirmedAccountAsync();

        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);
        var invited = await owner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = account.Email, role });
        invited.EnsureSuccessStatusCode();

        var signedIn = await api.LoginAsync(account);

        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);
        var accepted = await client.PostAsJsonAsync("/api/v1/tenants/invitations/accept", new { token = api.ReadInvitationToken(account.Email) });
        accepted.EnsureSuccessStatusCode();

        var switched = await client.PostAsJsonAsync("/api/v1/account/tenant", new { tenantId = store.TenantId });
        switched.EnsureSuccessStatusCode();

        var token = (await switched.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var (accessToken, refreshToken, _) = await api.EnableMfaAsync(token, signedIn.RefreshToken);

        return (account, accessToken, refreshToken);
    }

    public static string ReadInvitationToken(this ApiFixture api, string email)
    {
        var match = InvitationLink().Match(api.Emails.For(email)[^1].TextBody);

        Assert.True(match.Success, "The last email has no invitation link.");

        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    [GeneratedRegex(@"accept-invitation#token=(\S+)")]
    private static partial Regex InvitationLink();
}
