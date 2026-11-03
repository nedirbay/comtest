using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ComTest.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class SignInTests(ApiFixture api)
{
    [Fact]
    public async Task Login_BeforeEmailConfirmation_IsRefusedOnlyWithTheRightPassword()
    {
        var account = await api.RegisterAsync();
        using var client = api.CreateClient();

        var withRightPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = account.Password });
        var withWrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = "wrong-password-value" });

        Assert.Equal(HttpStatusCode.Forbidden, withRightPassword.StatusCode);
        Assert.Equal(ErrorCodes.Auth.EmailNotConfirmed, await ReadCode(withRightPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, withWrongPassword.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUserAndWrongPassword_AreIndistinguishable()
    {
        var account = await api.CreateConfirmedAccountAsync();
        using var client = api.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = "wrong-password-value" });
        var unknownUser = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = "nobody@example.test", password = "wrong-password-value" });

        var wrongPasswordCode = await ReadCode(wrongPassword);

        Assert.Equal(wrongPassword.StatusCode, unknownUser.StatusCode);
        Assert.Equal(wrongPasswordCode, await ReadCode(unknownUser));
        Assert.Equal(ErrorCodes.Auth.InvalidCredentials, wrongPasswordCode);
    }

    [Fact]
    public async Task Register_ExistingEmail_AnswersTheSameAndNotifiesTheOwner()
    {
        var account = await api.CreateConfirmedAccountAsync();
        var before = api.Emails.For(account.Email).Count;

        await api.RegisterAsync(account.Email);

        Assert.Equal(before + 1, api.Emails.For(account.Email).Count);
        Assert.Contains("already have an account", api.Emails.For(account.Email)[^1].Subject, StringComparison.Ordinal);
        Assert.Equal(1L, await api.QueryAsOwnerAsync<long>(
            "SELECT count(*) FROM users WHERE normalized_email = @email",
            ("email", account.Email.ToUpperInvariant())));
    }

    [Fact]
    public async Task AccessToken_IsAShortLivedEs256TokenWithAKeyId()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        var token = new JsonWebToken(signedIn.AccessToken);

        Assert.Equal("ES256", token.Alg);
        Assert.Equal(ApiFixture.JwtKeyId, token.Kid);
        Assert.Equal("at+jwt", token.Typ);
        Assert.Equal(ApiFixture.Issuer, token.Issuer);
        Assert.Contains(ApiFixture.Audience, token.Audiences);
        Assert.True(token.ValidTo - token.IssuedAt <= TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task RefreshCookie_HasEveryProtectiveAttribute()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        var cookie = signedIn.SetCookieHeader.ToLowerInvariant();

        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain(signedIn.RefreshToken, signedIn.Body.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProtectedEndpoint_RequiresAValidToken()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var anonymous = api.CreateClient();
        using var authorized = api.CreateAuthorizedClient(signedIn.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await authorized.GetAsync("/api/v1/account/me")).StatusCode);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("other-key")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("expired")]
    [InlineData("wrong-type")]
    [InlineData("tampered")]
    public async Task ForgedTokens_AreRejected(string attack)
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        var genuine = new JsonWebToken(signedIn.AccessToken);

        var forged = attack switch
        {
            "none" => UnsignedCopy(genuine),
            "other-key" => Sign(genuine, ECDsa.Create(ECCurve.NamedCurves.nistP256)),
            "wrong-issuer" => Sign(genuine, RealKey(), issuer: "https://attacker.test"),
            "wrong-audience" => Sign(genuine, RealKey(), audience: "another-api"),
            "expired" => Sign(genuine, RealKey(), expires: DateTime.UtcNow.AddMinutes(-5)),
            "wrong-type" => Sign(genuine, RealKey(), type: "JWT"),
            "tampered" => signedIn.AccessToken[..^4] + "AAAA",
            _ => throw new ArgumentOutOfRangeException(nameof(attack)),
        };

        using var client = api.CreateAuthorizedClient(forged);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/me")).StatusCode);
    }

    [Fact]
    public async Task GenuinelySignedToken_IsAccepted_SoTheAttacksAboveFailForTheirOwnReason()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(Sign(new JsonWebToken(signedIn.AccessToken), RealKey()));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/me")).StatusCode);
    }

    [Fact]
    public async Task Lockout_AfterFiveFailures_BlocksEvenTheRightPassword_WithTheSameAnswer()
    {
        var account = await api.CreateConfirmedAccountAsync();
        using var client = api.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = "wrong-password-value" });
        }

        var correct = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = account.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
        Assert.Equal(ErrorCodes.Auth.InvalidCredentials, await ReadCode(correct));

        Assert.Equal(1L, await CountEvents(account, "AccountLockedOut"));
        Assert.Equal(1L, await CountEvents(account, "LoginBlockedByLockout"));
        Assert.Equal(5L, await CountEvents(account, "LoginFailed"));
    }

    [Fact]
    public async Task SuccessfulLogin_IsAudited_AndClearsTheFailureCount()
    {
        var account = await api.CreateConfirmedAccountAsync();
        using var client = api.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = "wrong-password-value" });
        await api.LoginAsync(account);

        Assert.Equal(1L, await CountEvents(account, "LoginSucceeded"));
        Assert.Equal(0, await api.QueryAsOwnerAsync<int>(
            "SELECT access_failed_count FROM users WHERE id = @id",
            ("id", account.UserId)));
    }

    [Fact]
    public async Task FailedLoginForUnknownUser_StoresOnlyAHashOfTheLogin()
    {
        var login = $"ghost{Guid.NewGuid():N}@example.test";
        using var client = api.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/login", new { login, password = "wrong-password-value" });

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(login.ToUpperInvariant())));

        Assert.Equal(1L, await api.QueryAsOwnerAsync<long>(
            "SELECT count(*) FROM security_events WHERE identifier_hash = @hash AND type = 'LoginFailed' AND user_id IS NULL",
            ("hash", hash)));
    }

    [Fact]
    public async Task SecurityEvents_CannotBeChangedByTheApplicationRole()
    {
        await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        await using var connection = api.OpenApplicationConnection();

        foreach (var statement in new[] { "UPDATE security_events SET ip_address = 'x'", "DELETE FROM security_events", "TRUNCATE security_events" })
        {
            await using var command = new Npgsql.NpgsqlCommand(statement, connection);

            var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("42501", exception.SqlState);
        }
    }

    [Fact]
    public async Task ApplicationRole_CannotChangeTheSchemaOrBypassRowSecurity()
    {
        await using var connection = api.OpenApplicationConnection();

        foreach (var statement in new[] { "DROP TABLE users", "CREATE TABLE hacked (id int)", "SELECT * FROM \"__EFMigrationsHistory\"" })
        {
            await using var command = new Npgsql.NpgsqlCommand(statement, connection);

            var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("42501", exception.SqlState);
        }

        await using var attributes = new Npgsql.NpgsqlCommand(
            "SELECT rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole FROM pg_roles WHERE rolname = current_user",
            connection);

        Assert.False((bool)(await attributes.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task PasswordHash_UsesAtLeastTheOwaspIterationCount()
    {
        var account = await api.CreateConfirmedAccountAsync();

        var hash = await api.QueryAsOwnerAsync<string>("SELECT password_hash FROM users WHERE id = @id", ("id", account.UserId));
        var bytes = Convert.FromBase64String(hash!);

        // Identity V3 layout: version byte, PRF (uint32), iterations (uint32), all big-endian.
        var prf = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1, 4));
        var iterations = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4));

        Assert.Equal(0x01, bytes[0]);
        Assert.Equal(2u, prf);
        Assert.True(iterations >= 210_000, $"Only {iterations} iterations.");
    }

    [Theory]
    [InlineData("short-pw", "12")]
    [InlineData("password1234", "common")]
    public async Task Register_RejectsWeakPasswords(string password, string expectedInMessage)
    {
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "en");

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"weak{Guid.NewGuid():N}@example.test", password, fullName = "Weak" });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.Auth.WeakPassword, body.GetProperty("code").GetString());
        Assert.Contains(expectedInMessage, body.GetProperty("errors").GetProperty("password")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_RejectsBreachedPassword()
    {
        var password = ApiFixture.NewPassword();
        api.BreachedPasswords.MarkBreached(password);
        using var client = api.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"breach{Guid.NewGuid():N}@example.test", password, fullName = "Breach" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.Auth.WeakPassword, await ReadCode(response));
    }

    private ECDsa RealKey()
    {
        var key = ECDsa.Create();
        key.ImportFromPem(api.JwtPrivateKeyPem);

        return key;
    }

    private static string Sign(JsonWebToken source, ECDsa key, string? issuer = null, string? audience = null, DateTime? expires = null, string type = "at+jwt")
    {
        var claims = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var claim in new[] { "sub", "sid", "sstamp", "jti" })
        {
            claims[claim] = source.GetClaim(claim).Value;
        }

        var now = DateTime.UtcNow;
        var expiry = expires ?? now.AddMinutes(10);

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? ApiFixture.Issuer,
            Audience = audience ?? ApiFixture.Audience,
            IssuedAt = expiry.AddMinutes(-10),
            NotBefore = expiry.AddMinutes(-10),
            Expires = expiry,
            Claims = claims,
            TokenType = type,
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(key) { KeyId = ApiFixture.JwtKeyId }, SecurityAlgorithms.EcdsaSha256),
        });
    }

    private static string UnsignedCopy(JsonWebToken source)
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"at+jwt"}""");

        return $"{header}.{source.EncodedPayload}.";
    }

    private Task<long> CountEvents(TestAccount account, string type) =>
        api.QueryAsOwnerAsync<long>(
            "SELECT count(*) FROM security_events WHERE user_id = (SELECT id FROM users WHERE email = @email) AND type = @type",
            ("email", account.Email),
            ("type", type));

    private static async Task<string?> ReadCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}
