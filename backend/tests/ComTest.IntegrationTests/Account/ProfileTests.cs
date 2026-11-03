using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ComTest.IntegrationTests.Support;
using Xunit;

namespace ComTest.IntegrationTests.Account;

[Collection(ApiCollection.Name)]
public sealed partial class ProfileTests(ApiFixture api)
{
    private HttpClient Authorized(string token)
    {
        var client = api.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private (string UserId, string Email, string Token) ReadChangeLink(string newEmail)
    {
        var match = ChangeLink().Match(api.Emails.For(newEmail)[^1].TextBody);

        Assert.True(match.Success, "The last email has no email change link.");

        return (
            match.Groups[1].Value,
            Uri.UnescapeDataString(match.Groups[2].Value),
            Uri.UnescapeDataString(match.Groups[3].Value));
    }

    [Fact]
    public async Task ProfileUpdate_ChangesNameAndLanguage()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = Authorized(signedIn.AccessToken!);

        var response = await client.PutAsJsonAsync("/api/v1/account/me", new { fullName = "  Aýna Meredowa  ", locale = "ru" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await ReadAsync(response);

        Assert.Equal("Aýna Meredowa", profile.GetProperty("fullName").GetString());
        Assert.Equal("ru", profile.GetProperty("locale").GetString());
    }

    [Fact]
    public async Task ProfileUpdate_KeepsLanguage_WhenNoneIsGiven()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = Authorized(signedIn.AccessToken!);

        await client.PutAsJsonAsync("/api/v1/account/me", new { fullName = "Bir", locale = "en" });

        var profile = await ReadAsync(await client.PutAsJsonAsync("/api/v1/account/me", new { fullName = "Iki" }));

        Assert.Equal("en", profile.GetProperty("locale").GetString());
    }

    [Theory]
    [InlineData("", "tk")]
    [InlineData("Aýna", "de")]
    public async Task ProfileUpdate_RejectsInvalidValues(string fullName, string locale)
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = Authorized(signedIn.AccessToken!);

        var response = await client.PutAsJsonAsync("/api/v1/account/me", new { fullName, locale });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProfileUpdate_NeedsSignIn()
    {
        using var client = api.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/account/me", new { fullName = "Aýna" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EmailChange_WorksThroughTheLinkSentToTheNewAddress()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = Authorized(signedIn.AccessToken!);
        var newEmail = $"new-{Guid.NewGuid():N}@example.test";

        var requested = await client.PostAsJsonAsync("/api/v1/account/email", new { password = account.Password, newEmail });

        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        // Nothing changes until the new address has proved it can read mail.
        var profile = await ReadAsync(await client.GetAsync(new Uri("/api/v1/account/me", UriKind.Relative)));

        Assert.Equal(account.Email, profile.GetProperty("email").GetString());

        var (userId, email, token) = ReadChangeLink(newEmail);
        using var anonymous = api.CreateClient();
        var confirmed = await anonymous.PostAsJsonAsync("/api/v1/auth/confirm-email-change", new { userId, newEmail = email, token });

        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);

        // The new address signs in, and the old one does not.
        var withNew = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { login = newEmail, password = account.Password });
        var withOld = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = account.Password });

        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withOld.StatusCode);

        // The old address hears about it, in case the owner did not do this.
        Assert.Contains(api.Emails.For(account.Email), message => !message.TextBody.Contains("confirm-email-change", StringComparison.Ordinal)
            && message.TextBody.Length > 0);
    }

    [Fact]
    public async Task EmailChange_EndsAllSessions()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = Authorized(signedIn.AccessToken!);
        var newEmail = $"new-{Guid.NewGuid():N}@example.test";

        await client.PostAsJsonAsync("/api/v1/account/email", new { password = account.Password, newEmail });

        var (userId, email, token) = ReadChangeLink(newEmail);
        using var anonymous = api.CreateClient();

        await anonymous.PostAsJsonAsync("/api/v1/auth/confirm-email-change", new { userId, newEmail = email, token });

        var afterwards = await client.GetAsync(new Uri("/api/v1/account/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task EmailChange_NeedsThePassword_SoAStolenSessionCannotTakeTheAccount()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = Authorized(signedIn.AccessToken!);
        var newEmail = $"new-{Guid.NewGuid():N}@example.test";

        var response = await client.PostAsJsonAsync("/api/v1/account/email", new { password = "not-the-password", newEmail });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(api.Emails.For(newEmail));
    }

    [Fact]
    public async Task EmailChange_ToATakenAddress_AnswersTheSameAndSendsNoLink()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var other = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = Authorized(signedIn.AccessToken!);

        var response = await client.PostAsJsonAsync("/api/v1/account/email", new { password = account.Password, newEmail = other.Email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain(api.Emails.For(other.Email), message => message.TextBody.Contains("confirm-email-change", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmailChange_LinkWorksOnlyForTheAddressItWasSentFor()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = Authorized(signedIn.AccessToken!);
        var newEmail = $"new-{Guid.NewGuid():N}@example.test";

        await client.PostAsJsonAsync("/api/v1/account/email", new { password = account.Password, newEmail });

        var (userId, _, token) = ReadChangeLink(newEmail);
        using var anonymous = api.CreateClient();
        var attacker = $"attacker-{Guid.NewGuid():N}@example.test";

        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/confirm-email-change", new { userId, newEmail = attacker, token });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmailChange_LinkCannotBeUsedTwice()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(account);
        using var client = Authorized(signedIn.AccessToken!);
        var newEmail = $"new-{Guid.NewGuid():N}@example.test";

        await client.PostAsJsonAsync("/api/v1/account/email", new { password = account.Password, newEmail });

        var (userId, email, token) = ReadChangeLink(newEmail);
        using var anonymous = api.CreateClient();

        await anonymous.PostAsJsonAsync("/api/v1/auth/confirm-email-change", new { userId, newEmail = email, token });

        var again = await anonymous.PostAsJsonAsync("/api/v1/auth/confirm-email-change", new { userId, newEmail = email, token });

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [GeneratedRegex(@"confirm-email-change#userId=([^&]+)&email=([^&]+)&token=(\S+)")]
    private static partial Regex ChangeLink();
}
