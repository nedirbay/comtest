using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ComTest.Api.Hosting;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ComTest.IntegrationTests.Pipeline;

[Collection(ApiCollection.Name)]
public sealed class SecurityHeaderTests(ApiFixture api)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/no-such-route")]
    [InlineData("/test/throw")]
    public async Task EveryResponse_CarriesTheSecurityHeaders(string path)
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Contains("camera=()", Header(response, "Permissions-Policy"), StringComparison.Ordinal);
        Assert.Contains("default-src 'none'", Header(response, "Content-Security-Policy"), StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", Header(response, "Content-Security-Policy"), StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Hsts_IsSentOverHttpsOutsideDevelopment()
    {
        using var client = api.CreateClient(new() { BaseAddress = new Uri("https://api.comtest.test"), AllowAutoRedirect = false });

        var response = await client.GetAsync("/health/live");

        Assert.Contains("max-age=31536000", Header(response, "Strict-Transport-Security"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Http_IsRedirectedToHttps_ExceptHealthProbes()
    {
        using var client = api.CreateClient(new() { BaseAddress = new Uri("http://api.comtest.test"), AllowAutoRedirect = false });

        var api1 = await client.GetAsync("/api/v1/account/me");
        var health = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, api1.StatusCode);
        Assert.Equal("https", api1.Headers.Location!.Scheme);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : string.Empty;
}

[Collection(ApiCollection.Name)]
public sealed class CorsTests(ApiFixture api)
{
    [Fact]
    public async Task AllowedOrigin_GetsCorsHeadersWithCredentials()
    {
        var response = await PreflightAsync(ApiFixture.AllowedOrigin);

        Assert.Equal(ApiFixture.AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task OtherOrigin_GetsNoCorsHeaders()
    {
        var response = await PreflightAsync("https://attacker.test");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private async Task<HttpResponseMessage> PreflightAsync(string origin)
    {
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/refresh");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "X-Requested-With");

        return await client.SendAsync(request);
    }
}

[Collection(ApiCollection.Name)]
public sealed class ErrorResponseTests(ApiFixture api)
{
    [Fact]
    public async Task UnhandledException_ReturnsAGenericProblem_WithoutInternals()
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync("/test/throw");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains(ErrorCodes.Common.Unexpected, body, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-leak", body, StringComparison.Ordinal);
        Assert.DoesNotContain("internal-db", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrencyConflict_BecomesA409_WithoutInternals()
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync("/test/concurrency");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(ErrorCodes.Common.ConcurrencyConflict, body, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-leak", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tk", "Ulgama giriň.")]
    [InlineData("ru", "Выполните вход.")]
    [InlineData("en", "Please sign in.")]
    [InlineData("ru-RU", "Выполните вход.")]
    [InlineData("de", "Ulgama giriň.")]
    public async Task Errors_AreTranslated_FromAcceptLanguage(string language, string expected)
    {
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", language);

        var body = await (await client.GetAsync("/api/v1/account/me")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(expected, body.GetProperty("title").GetString());
        Assert.Equal(ErrorCodes.Common.Unauthorized, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnknownRoute_IsNotFound_ForASignedInUser_AndHiddenFromStrangers()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var authorized = api.CreateAuthorizedClient(signedIn.AccessToken);
        using var anonymous = api.CreateClient();

        var notFound = await authorized.GetAsync("/no-such-route");
        var body = await notFound.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(ErrorCodes.Common.NotFound, body.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/no-such-route")).StatusCode);
    }

    [Theory]
    [InlineData("tk", "boş")]
    [InlineData("ru", "должно")]
    [InlineData("en", "must not")]
    public async Task ValidationMessages_AreTranslated_AndListedByField(string language, string expected)
    {
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", language);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = string.Empty, password = "x" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.Common.Validation, body.GetProperty("code").GetString());
        Assert.Contains(expected, body.GetProperty("errors").GetProperty("login")[0].GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WeakPasswordMessages_AreTranslated()
    {
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "tk");

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"w{Guid.NewGuid():N}@example.test", password = "password1234", fullName = "W" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains("giňden ýaýran", body.GetProperty("errors").GetProperty("password")[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownJsonFields_AreRejected_SoNothingExtraCanSlipIn()
    {
        using var client = api.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = $"m{Guid.NewGuid():N}@example.test", password = ApiFixture.NewPassword(), fullName = "M", isAdmin = true, roles = new[] { "PlatformAdmin" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MalformedJson_DoesNotEchoParserDetails()
    {
        using var client = api.CreateClient();

        var response = await client.PostAsync("/api/v1/auth/login", new StringContent("{\"login\": <script>", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("<script>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("BytePositionInLine", body, StringComparison.Ordinal);
    }
}

[Collection(ApiCollection.Name)]
public sealed class CorrelationIdTests(ApiFixture api)
{
    [Fact]
    public async Task ValidIncomingId_IsKept()
    {
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "abc-123_XYZ");

        var response = await client.GetAsync("/no-such-route");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("abc-123_XYZ", response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
        Assert.Equal("abc-123_XYZ", body.GetProperty("correlationId").GetString());
    }

    [Theory]
    [InlineData("bad id with spaces")]
    [InlineData("forged<script>")]
    public async Task UnsafeIncomingId_IsReplaced(string incoming)
    {
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, incoming);

        var response = await client.GetAsync("/health/live");

        Assert.NotEqual(incoming, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task MissingId_IsGenerated()
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.False(string.IsNullOrEmpty(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single()));
    }
}

[Collection(ApiCollection.Name)]
public sealed class VersioningAndHealthTests(ApiFixture api)
{
    [Fact]
    public async Task UnsupportedApiVersion_IsRefused()
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        var response = await client.GetAsync("/api/v99/account/me");

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, response.StatusCode.ToString());
    }

    [Fact]
    public async Task Health_ReportsTheDatabase()
    {
        using var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task ApiDocumentation_IsNotServedOutsideDevelopment(string path)
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);
        using var anonymous = api.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public sealed class AuthorizationDefaultsTests(ApiFixture api)
{
    /// <summary>
    /// The complete list of what strangers may reach. A new anonymous endpoint must be added
    /// here on purpose, which makes it visible in review.
    /// </summary>
    private static readonly HashSet<string> KnownAnonymousEndpoints = new(StringComparer.Ordinal)
    {
        "POST api/v{version:apiVersion}/auth/register",
        "POST api/v{version:apiVersion}/auth/confirm-email",
        "POST api/v{version:apiVersion}/auth/confirm-email-change",
        "POST api/v{version:apiVersion}/auth/resend-confirmation",
        "POST api/v{version:apiVersion}/auth/login",
        "POST api/v{version:apiVersion}/auth/login/two-factor",
        "POST api/v{version:apiVersion}/auth/refresh",
        "POST api/v{version:apiVersion}/auth/logout",
        "POST api/v{version:apiVersion}/auth/forgot-password",
        "POST api/v{version:apiVersion}/auth/reset-password",
        "GET api/v{version:apiVersion}/store",
        "GET api/v{version:apiVersion}/catalog/brands",
        "GET api/v{version:apiVersion}/catalog/categories",
        "GET api/v{version:apiVersion}/catalog/products",
        "GET api/v{version:apiVersion}/catalog/products/{slug}",
        "GET api/v{version:apiVersion}/cart",
        "POST api/v{version:apiVersion}/cart/items",
        "PUT api/v{version:apiVersion}/cart/items/{variantId:guid}",
        "DELETE api/v{version:apiVersion}/cart/items/{variantId:guid}",
        "DELETE api/v{version:apiVersion}/cart",
        "POST api/v{version:apiVersion}/checkout/quote",
        "POST api/v{version:apiVersion}/checkout",
        "GET api/v{version:apiVersion}/shipping/regions",
        "POST api/v{version:apiVersion}/orders/track",
        "ANY health/live",
        "ANY health/ready",
    };

    [Fact]
    public async Task EndpointWithoutAttributes_RequiresSignIn()
    {
        using var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/test/no-attribute")).StatusCode);
    }

    [Fact]
    public void OnlyKnownEndpoints_AreAnonymous()
    {
        var sources = api.Factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();

        var anonymous = sources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Describe)
            .Where(name => !name.Contains("test/", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            KnownAnonymousEndpoints.Order(StringComparer.Ordinal),
            anonymous.Order(StringComparer.Ordinal));
    }

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
        var method = methods is { Count: > 0 } ? methods[0] : "ANY";

        return $"{method} {endpoint.RoutePattern.RawText?.TrimStart('/')}";
    }
}

[Collection(ApiCollection.Name)]
public sealed class LimitTests(ApiFixture api)
{
    [Fact]
    public async Task LoginRateLimit_Returns429_WithRetryAfter_AndAProblemBody()
    {
        using var factory = api.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<RateLimitSettings>(settings => settings.LoginPerMinute = 2)));

        using var client = factory.CreateClient(ApiFixture.DefaultClientOptions());

        HttpResponseMessage response = null!;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            response = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = "limit@example.test", password = "wrong-password-value" });
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));
        Assert.Equal(ErrorCodes.Common.TooManyRequests, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GlobalRateLimit_AppliesPerCaller_AndSkipsHealthProbes()
    {
        using var factory = api.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<RateLimitSettings>(settings => settings.GlobalPerMinute = 2)));

        using var client = factory.CreateClient(ApiFixture.DefaultClientOptions());

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            statuses.Add((await client.GetAsync("/no-such-route")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        }
    }

    [Fact]
    public async Task SlowRequest_TimesOutWith504()
    {
        using var factory = api.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddRequestTimeouts(options => options.DefaultPolicy = new RequestTimeoutPolicy
                {
                    Timeout = TimeSpan.FromMilliseconds(100),
                    TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
                })));

        using var client = factory.CreateClient(ApiFixture.DefaultClientOptions());

        var response = await client.GetAsync("/test/slow");

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public sealed class ForwardedHeaderTests(ApiFixture api)
{
    private const string SpoofedAddress = "203.0.113.9";

    [Fact]
    public async Task ForwardedFor_IsIgnored_FromAnUntrustedPeer()
    {
        var address = await LoginAndReadAuditAddressAsync(trustProxy: false);

        Assert.NotEqual(SpoofedAddress, address);
    }

    [Fact]
    public async Task ForwardedFor_IsBelieved_FromAConfiguredProxy()
    {
        var address = await LoginAndReadAuditAddressAsync(trustProxy: true);

        Assert.Equal(SpoofedAddress, address);
    }

    private async Task<string?> LoginAndReadAuditAddressAsync(bool trustProxy)
    {
        const string PeerAddress = "10.0.0.5";

        using var factory = api.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new RemoteAddressStartupFilter(PeerAddress));

                if (trustProxy)
                {
                    services.Configure<ForwardedHeadersSettings>(settings => settings.KnownProxies.Add(PeerAddress));
                }
            }));

        using var client = factory.CreateClient(ApiFixture.DefaultClientOptions());
        client.DefaultRequestHeaders.Add("X-Forwarded-For", SpoofedAddress);

        var login = $"forwarded{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/v1/auth/login", new { login, password = "wrong-password-value" });

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(login.ToUpperInvariant())));

        return await api.QueryAsOwnerAsync<string>(
            "SELECT ip_address FROM security_events WHERE identifier_hash = @hash",
            ("hash", hash));
    }

    private sealed class RemoteAddressStartupFilter(string address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(address);

                return nextMiddleware();
            });

            next(app);
        };
    }
}
