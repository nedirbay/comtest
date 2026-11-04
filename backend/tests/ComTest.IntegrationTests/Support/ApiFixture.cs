using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure;
using ComTest.Infrastructure.Identity;
using ComTest.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ComTest.IntegrationTests.Support;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

/// <summary>One PostgreSQL container and one API host, shared by the tests in the collection.</summary>
public sealed partial class ApiFixture : IAsyncLifetime
{
    public const string JwtKeyId = "test-key";
    public const string Issuer = "https://api.comtest.test";
    public const string Audience = "comtest-api-test";
    public const string AllowedOrigin = "https://shop.comtest.test";
    public const string BaseDomain = "comtest.test";

    private const string AppPassword = "app-test-password";
    private const string DatabaseName = "comtest_test";
    private const string StorageUser = "comtest-test";
    private const string StoragePassword = "comtest-test-secret";
    private const ushort StoragePort = 9000;

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase(DatabaseName)
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly IContainer _storage = new ContainerBuilder("cgr.dev/chainguard/minio:latest")
        .WithEnvironment("MINIO_ROOT_USER", StorageUser)
        .WithEnvironment("MINIO_ROOT_PASSWORD", StoragePassword)
        .WithCommand("server", "/data")
        .WithPortBinding(StoragePort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(StoragePort).ForPath("/minio/health/live")))
        .Build();

    private string _ownerConnectionString = string.Empty;

    public ApiFactory Factory { get; private set; } = null!;

    public CapturingEmailDispatcher Emails { get; } = new();

    public CapturingSmsSender Sms { get; } = new();

    public StubBreachedPasswordChecker BreachedPasswords { get; } = new();

    public DomainEventRecorder DomainEvents { get; } = new();

    public string OwnerConnectionString => _ownerConnectionString;

    public string StorageUrl => $"http://{_storage.Hostname}:{_storage.GetMappedPublicPort(StoragePort)}";

    public string JwtPrivateKeyPem { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_database.StartAsync(), _storage.StartAsync());

        _ownerConnectionString = _database.GetConnectionString();

        await CreateApplicationRoleAsync();
        await MigrateAsync();

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        JwtPrivateKeyPem = key.ExportPkcs8PrivateKeyPem();

        var appConnection = new NpgsqlConnectionStringBuilder(_ownerConnectionString)
        {
            Username = MigrationBuilderExtensions.ApplicationRole,
            Password = AppPassword,
        };

        // Environment variables, because Program reads some settings before the host is built.
        Set("ASPNETCORE_ENVIRONMENT", "Testing");
        Set("ConnectionStrings__Default", appConnection.ConnectionString);
        Set("Jwt__Issuer", Issuer);
        Set("Jwt__Audience", Audience);
        Set("Jwt__ActiveKeyId", JwtKeyId);
        Set("Jwt__SigningKeys__0__KeyId", JwtKeyId);
        Set("Jwt__SigningKeys__0__PrivateKeyPem", JwtPrivateKeyPem);
        Set("Auth__FrontendBaseUrl", AllowedOrigin);
        Set("Email__Smtp__Host", "localhost");
        Set("Outbox__Enabled", "false");
        Set("Storage__ServiceUrl", StorageUrl);
        Set("Storage__AccessKey", StorageUser);
        Set("Storage__SecretKey", StoragePassword);
        Set("Storage__Bucket", "comtest-test");
        Set("Storage__CreateBucketIfMissing", "true");
        Set("Tenancy__BaseDomain", BaseDomain);
        Set("Tenancy__AllowTenantHeader", "false");
        Set("RateLimiting__TenantPerMinute", "1000000");
        Set("RateLimiting__CheckoutPerMinute", "1000000");
        Set("RateLimiting__TrackingPerMinute", "1000000");
        Set("RateLimiting__ReportExportPerMinute", "1000000");
        Set("Cors__AllowedOrigins__0", AllowedOrigin);
        Set("RateLimiting__GlobalPerMinute", "1000000");
        Set("RateLimiting__LoginPerMinute", "1000000");
        Set("RateLimiting__AccountActionPerMinute", "1000000");
        Set("RateLimiting__RefreshPerMinute", "1000000");
        Set("RateLimiting__SmsPerHour", "1000000");
        Set("Serilog__MinimumLevel__Default", "Warning");
        Set("HttpsRedirection__HttpsPort", "443");

        Factory = new ApiFactory(this);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _database.DisposeAsync();
        await _storage.DisposeAsync();
    }

    public HttpClient CreateClient(WebApplicationFactoryClientOptions? options = null) =>
        Factory.CreateClient(options ?? DefaultClientOptions());

    public static WebApplicationFactoryClientOptions DefaultClientOptions() => new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = false,
    };

    public async Task<T?> QueryAsOwnerAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(_ownerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();

        return result is null or DBNull ? default : (T)result;
    }

    public async Task ExecuteAsOwnerAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(_ownerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Connects as the application's own limited role, to check what the role itself may do.</summary>
    public NpgsqlConnection OpenApplicationConnection()
    {
        var builder = new NpgsqlConnectionStringBuilder(_ownerConnectionString)
        {
            Username = MigrationBuilderExtensions.ApplicationRole,
            Password = AppPassword,
        };

        var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();

        return connection;
    }

    public async Task<TestAccount> CreateConfirmedAccountAsync(params string[] extraRoles)
    {
        var account = await RegisterAsync();

        var (userId, token) = Emails.ReadLink(account.Email);
        using var client = CreateClient();

        var confirm = await client.PostAsJsonAsync("/api/v1/auth/confirm-email", new { userId, token });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, confirm.StatusCode);

        account.UserId = userId;

        if (extraRoles.Length > 0)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId.ToString());

            foreach (var role in extraRoles)
            {
                await users.AddToRoleAsync(user!, role);
            }
        }

        return account;
    }

    public async Task<TestAccount> RegisterAsync(string? email = null, string? password = null)
    {
        var account = new TestAccount(
            email ?? $"user{Guid.NewGuid():N}@example.test",
            password ?? NewPassword());

        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = account.Email, password = account.Password, fullName = "Test User", locale = "en" });

        Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);

        return account;
    }

    public static string NewPassword() => $"pw-{Guid.NewGuid():N}";

    public async Task<SignedIn> LoginAsync(TestAccount account)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { login = account.Email, password = account.Password });

        response.EnsureSuccessStatusCode();

        return await SignedIn.FromResponseAsync(response);
    }

    public HttpClient CreateAuthorizedClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    public static HttpRequestMessage CookieRequest(string path, string? refreshToken, bool withCsrfHeader = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);

        if (refreshToken is not null)
        {
            request.Headers.Add("Cookie", $"{SignedIn.CookieName}={refreshToken}");
        }

        if (withCsrfHeader)
        {
            request.Headers.Add("X-Requested-With", "ComTest");
        }

        return request;
    }

    private static void Set(string name, string value) => Environment.SetEnvironmentVariable(name, value);

    private async Task CreateApplicationRoleAsync()
    {
        var script = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "create-roles.sql"));
        await _database.CopyAsync(script, "/tmp/create-roles.sql");

        var result = await _database.ExecAsync(
            ["psql", "-U", "postgres", "-d", DatabaseName, "-v", "ON_ERROR_STOP=1", "-v", $"app_password={AppPassword}", "-f", "/tmp/create-roles.sql"]);

        Assert.True(result.ExitCode == 0, result.Stderr);
    }

    private async Task MigrateAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseAppDatabase(_ownerConnectionString);

        await using var db = new AppDbContext((DbContextOptions<AppDbContext>)options.Options, new NoTenant());
        await db.Database.MigrateAsync();
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;

        public bool IsPlatformAccess => false;
    }
}

public sealed class TestAccount(string email, string password)
{
    public string Email { get; } = email;

    public string Password { get; set; } = password;

    public Guid UserId { get; set; }
}

public sealed partial record SignedIn(string AccessToken, string RefreshToken, string SetCookieHeader, JsonElement Body)
{
    public const string CookieName = "__Secure-comtest-refresh";

    public static async Task<SignedIn> FromResponseAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal));
        var refresh = RefreshValue().Match(cookie).Groups[1].Value;

        return new SignedIn(body.GetProperty("accessToken").GetString()!, refresh, cookie, body);
    }

    [GeneratedRegex("^[^=]+=([^;]*)")]
    private static partial Regex RefreshValue();
}
