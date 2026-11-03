using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ComTest.Api.ErrorHandling;
using ComTest.Api.Hosting;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Identity;
using ComTest.Infrastructure.Identity.Authorization;
using ComTest.Infrastructure.Identity.Passwords;
using ComTest.Infrastructure.Persistence;
using ComTest.Infrastructure.Persistence.Outbox;
using ComTest.Shared.Domain;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Events;
using Serilog.Parsing;

namespace ComTest.UnitTests.Security;

public sealed class GlobalExceptionHandlerTests
{
    private const string SensitiveMessage = "internal-db-host:5432 connection failed";

    [Fact]
    public async Task TryHandleAsync_ReturnsProblemDetailsWithStatus500()
    {
        var (handled, context, _) = await HandleAsync(new InvalidOperationException(SensitiveMessage));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
    }

    [Fact]
    public async Task TryHandleAsync_DoesNotExposeExceptionDetails()
    {
        var (_, _, body) = await HandleAsync(new InvalidOperationException(SensitiveMessage));

        Assert.Contains(ErrorCodes.Common.Unexpected, body, StringComparison.Ordinal);
        Assert.DoesNotContain(SensitiveMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
    }

    private static async Task<(bool Handled, HttpContext Context, string Body)> HandleAsync(Exception exception)
    {
        await using var services = new ServiceCollection()
            .AddOptions()
            .AddLogging()
            .AddLocalization()
            .AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsEnricher.Enrich)
            .BuildServiceProvider();

        await using var responseBody = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = responseBody;

        var handler = new GlobalExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(),
            NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);

        return (handled, context, await reader.ReadToEndAsync());
    }
}

public sealed class SensitiveDataEnricherTests
{
    private static readonly MessageTemplateParser Parser = new();

    [Theory]
    [InlineData("Password", "hunter2")]
    [InlineData("refreshToken", "abc")]
    [InlineData("Authorization", "Bearer abc")]
    [InlineData("UserEmail", "a@b.test")]
    [InlineData("PhoneNumber", "+99365000000")]
    public void Enrich_MasksPropertiesWithSensitiveNames(string name, string value)
    {
        var logEvent = Enrich(new LogEventProperty(name, new ScalarValue(value)));

        Assert.Equal("\"***\"", logEvent.Properties[name].ToString());
    }

    [Theory]
    [InlineData("Host=db;Username=app;Password=hunter2", "hunter2")]
    [InlineData("Authorization: Bearer abc.def.ghi", "abc.def.ghi")]
    [InlineData("token eyJhbGciOiJFUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl", "eyJhbGciOiJFUzI1NiJ9")]
    [InlineData("failed for ayna@example.test now", "ayna@")]
    [InlineData("sms to +99365123456 failed", "+99365123456")]
    public void Enrich_MasksSensitiveShapesInsideOrdinaryText(string text, string secret)
    {
        var logEvent = Enrich(new LogEventProperty("Details", new ScalarValue(text)));

        Assert.DoesNotContain(secret, logEvent.Properties["Details"].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Enrich_MasksNestedStructures()
    {
        var logEvent = Enrich(new LogEventProperty("Request", new StructureValue(
        [
            new LogEventProperty("Name", new ScalarValue("Ayna")),
            new LogEventProperty("Password", new ScalarValue("hunter2")),
        ])));

        var rendered = logEvent.Properties["Request"].ToString();

        Assert.Contains("Ayna", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Enrich_LeavesOrdinaryValuesAlone()
    {
        var logEvent = Enrich(new LogEventProperty("OrderCount", new ScalarValue(12)));

        Assert.Equal("12", logEvent.Properties["OrderCount"].ToString());
    }

    [Fact]
    public void RecordsWithSecrets_DoNotPrintThemInToString()
    {
        Assert.DoesNotContain("hunter2", new LoginRequest("a@b.test", "hunter2").ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", new RegisterRequest("a@b.test", "hunter2", "A").ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", new ChangePasswordRequest("hunter2", "hunter2").ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", new AuthTokens("secret", default, "secret", default, false).ToString(), StringComparison.Ordinal);
    }

    private static LogEvent Enrich(LogEventProperty property)
    {
        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, Parser.Parse("test"), [property]);
        new SensitiveDataEnricher().Enrich(logEvent, null!);

        return logEvent;
    }
}

public sealed class ConnectionStringGuardTests
{
    private const string Secure = "Host=db;Database=d;Username=comtest_app;Password=x;SSL Mode=VerifyFull";

    [Fact]
    public void Production_AcceptsTlsWithALimitedRole() =>
        ConnectionStringGuard.Validate(Secure, new FakeEnvironment("Production"));

    [Theory]
    [InlineData("Host=db;Database=d;Username=comtest_app;Password=x")]
    [InlineData("Host=db;Database=d;Username=comtest_app;Password=x;SSL Mode=Disable")]
    [InlineData("Host=db;Database=d;Username=comtest_app;Password=x;SSL Mode=Prefer")]
    [InlineData("Host=db;Database=d;Username=postgres;Password=x;SSL Mode=VerifyFull")]
    [InlineData("Host=db;Database=d;Username=comtest_app;Password=x;SSL Mode=VerifyFull;Include Error Detail=true")]
    public void Production_RefusesUnsafeConnections(string connectionString) =>
        Assert.Throws<InvalidOperationException>(() =>
            ConnectionStringGuard.Validate(connectionString, new FakeEnvironment("Production")));

    [Fact]
    public void Development_AllowsLocalConnections() =>
        ConnectionStringGuard.Validate("Host=localhost;Username=postgres;Password=x", new FakeEnvironment("Development"));

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public sealed class CorsSettingsTests
{
    [Theory]
    [InlineData("https://shop.example.com")]
    [InlineData("http://localhost:5173")]
    public void Validate_AcceptsExactOrigins(string origin) => Assert.Empty(Validate(origin));

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.com")]
    [InlineData("https://shop.example.com/")]
    [InlineData("https://shop.example.com/path")]
    [InlineData("shop.example.com")]
    [InlineData("ftp://shop.example.com")]
    public void Validate_RejectsWildcardsAndNonOrigins(string origin) => Assert.NotEmpty(Validate(origin));

    private static List<ValidationResult> Validate(string origin)
    {
        var settings = new CorsSettings();
        settings.AllowedOrigins.Add(origin);

        return settings.Validate(new ValidationContext(settings)).ToList();
    }
}

public sealed class PasswordPolicyValidatorTests
{
    private readonly FakeBreachedChecker _checker = new();

    [Fact]
    public async Task Accepts_AStrongPassword()
    {
        var result = await Validate("correct horse battery staple");

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("password1234")]
    [InlineData("PASSWORD1234")]
    [InlineData("turkmenistan")]
    public async Task Rejects_CommonPasswords_InAnyCase(string password)
    {
        var result = await Validate(password);

        Assert.Contains(result.Errors, error => error.Code == ErrorCodes.Password.TooCommon);
    }

    [Fact]
    public async Task Rejects_APasswordContainingTheEmailName()
    {
        var result = await Validate("xx-AynaBeg-long-enough", email: "aynabeg@example.test");

        Assert.Contains(result.Errors, error => error.Code == ErrorCodes.Password.ContainsIdentity);
    }

    [Fact]
    public async Task Rejects_ABreachedPassword()
    {
        _checker.Breached = true;

        var result = await Validate("some-unusual-breached-phrase");

        Assert.Contains(result.Errors, error => error.Code == ErrorCodes.Password.Breached);
    }

    [Fact]
    public void ToValidationError_MapsIdentityCodes()
    {
        var error = PasswordErrors.ToValidationError(
            IdentityResult.Failed(
                new IdentityError { Code = nameof(IdentityErrorDescriber.PasswordTooShort) },
                new IdentityError { Code = ErrorCodes.Password.Breached },
                new IdentityError { Code = "SomethingElse" }),
            "password");

        Assert.Equal(ErrorCodes.Auth.WeakPassword, error.Code);
        Assert.Equal(
            [ErrorCodes.Password.TooShort, ErrorCodes.Password.Breached, ErrorCodes.Password.Rejected],
            error.Details!["password"]);
    }

    private Task<IdentityResult> Validate(string password, string email = "someone@example.test") =>
        new PasswordPolicyValidator(_checker).ValidateAsync(null!, new ApplicationUser { Email = email }, password);

    private sealed class FakeBreachedChecker : IBreachedPasswordChecker
    {
        public bool Breached { get; set; }

        public Task<bool> IsBreachedAsync(string password, CancellationToken cancellationToken) => Task.FromResult(Breached);
    }
}

public sealed class PermissionAuthorizationHandlerTests
{
    private const string Permission = Permissions.CatalogWrite;

    [Fact]
    public async Task Succeeds_ForAStaffMemberWithPermissionAndMfa()
    {
        Assert.True(await IsAllowed((AppClaims.Role, Roles.Manager), (Permissions.ClaimType, Permission), (AppClaims.Mfa, "true")));
    }

    [Fact]
    public async Task Fails_WithoutThePermission()
    {
        Assert.False(await IsAllowed((AppClaims.Role, Roles.Manager), (Permissions.ClaimType, Permissions.OrdersRead), (AppClaims.Mfa, "true")));
    }

    [Fact]
    public async Task Fails_ForStaffWithoutMfa_EvenWithThePermission()
    {
        Assert.False(await IsAllowed((AppClaims.Role, Roles.Manager), (Permissions.ClaimType, Permission)));
    }

    [Fact]
    public async Task Succeeds_ForNonStaffWithPermissionAndNoMfa()
    {
        Assert.True(await IsAllowed((AppClaims.Role, Roles.Customer), (Permissions.ClaimType, Permission)));
    }

    [Fact]
    public async Task PolicyProvider_BuildsPermissionPolicyAndDefersOtherNames()
    {
        var provider = new PermissionPolicyProvider(Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync(PermissionPolicy.Prefix + Permission);

        Assert.Contains(policy!.Requirements, requirement => requirement is PermissionRequirement { Permission: Permission });
        Assert.Null(await provider.GetPolicyAsync("unknown"));
    }

    private static async Task<bool> IsAllowed(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(claim => new Claim(claim.Type, claim.Value)), "test");
        var requirement = new PermissionRequirement(Permission);
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        return context.HasSucceeded;
    }
}

public sealed record RegisteredEvent(DateTimeOffset OccurredAt) : IDomainEvent;

public sealed class DomainEventRegistryTests
{
    [Fact]
    public void Find_ResolvesOnlyKnownEventTypes()
    {
        var registry = new DomainEventRegistry([typeof(RegisteredEvent).Assembly]);

        Assert.Equal(typeof(RegisteredEvent), registry.Find(typeof(RegisteredEvent).FullName!));
        Assert.Null(registry.Find("System.Diagnostics.Process"));
        Assert.Null(registry.Find(typeof(DomainEventRegistryTests).FullName!));
    }
}

public sealed class RolesAndPermissionsTests
{
    [Fact]
    public void EveryRole_HasAPermissionEntry()
    {
        foreach (var role in new[] { Roles.PlatformAdmin, Roles.StoreOwner, Roles.Manager, Roles.Staff, Roles.Customer })
        {
            Assert.True(Permissions.ByRole.ContainsKey(role), role);
        }
    }

    [Fact]
    public void Customers_HaveNoStaffPermissions() => Assert.Empty(Permissions.ByRole[Roles.Customer]);

    [Fact]
    public void Staff_CannotRefundOrSeeFinancialReports()
    {
        Assert.DoesNotContain(Permissions.PaymentsRefund, Permissions.ByRole[Roles.Staff]);
        Assert.DoesNotContain(Permissions.ReportsFinancial, Permissions.ByRole[Roles.Staff]);
        Assert.DoesNotContain(Permissions.PaymentsRefund, Permissions.ByRole[Roles.Manager]);
    }

    [Fact]
    public void AllRolesWithOthersDataAccess_RequireTwoFactor()
    {
        Assert.Contains(Roles.PlatformAdmin, Roles.RequireTwoFactor);
        Assert.Contains(Roles.StoreOwner, Roles.RequireTwoFactor);
        Assert.Contains(Roles.Manager, Roles.RequireTwoFactor);
        Assert.Contains(Roles.Staff, Roles.RequireTwoFactor);
        Assert.DoesNotContain(Roles.Customer, Roles.RequireTwoFactor);
    }
}

public sealed class JwtKeyRingTests
{
    private static string NewKeyPem(System.Security.Cryptography.ECCurve? curve = null)
    {
        using var key = System.Security.Cryptography.ECDsa.Create(curve ?? System.Security.Cryptography.ECCurve.NamedCurves.nistP256);

        return key.ExportPkcs8PrivateKeyPem();
    }

    private static string PublicPemOf(string privatePem)
    {
        using var key = System.Security.Cryptography.ECDsa.Create();
        key.ImportFromPem(privatePem);

        return key.ExportSubjectPublicKeyInfoPem();
    }

    private static JwtKeyRing Ring(string active, params JwtKeyOptions[] keys)
    {
        var options = new JwtOptions { ActiveKeyId = active, Issuer = "i", Audience = "a" };

        foreach (var key in keys)
        {
            options.SigningKeys.Add(key);
        }

        return new JwtKeyRing(Microsoft.Extensions.Options.Options.Create(options));
    }

    [Fact]
    public async Task Rotation_NewKeySigns_AndTokensFromTheOldKeyStillVerify()
    {
        var oldPem = NewKeyPem();
        var newPem = NewKeyPem();

        using var before = Ring("old", new JwtKeyOptions { KeyId = "old", PrivateKeyPem = oldPem });
        using var after = Ring(
            "new",
            new JwtKeyOptions { KeyId = "new", PrivateKeyPem = newPem },
            new JwtKeyOptions { KeyId = "old", PublicKeyPem = PublicPemOf(oldPem) });

        var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
        var oldToken = handler.CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = "i",
            Audience = "a",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = before.SigningCredentials,
        });

        var result = await handler.ValidateTokenAsync(oldToken, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidIssuer = "i",
            ValidAudience = "a",
            IssuerSigningKeys = after.ValidationKeys,
        });

        Assert.True(result.IsValid);
        Assert.Equal("new", after.SigningCredentials.Key.KeyId);
        Assert.Equal(2, after.ValidationKeys.Count);
    }

    [Fact]
    public void RemovedKey_NoLongerVerifies()
    {
        using var ring = Ring("new", new JwtKeyOptions { KeyId = "new", PrivateKeyPem = NewKeyPem() });

        Assert.DoesNotContain(ring.ValidationKeys, key => key.KeyId == "old");
    }

    [Fact]
    public void ActiveKey_MustExist_AndHaveAPrivateKey()
    {
        var pem = NewKeyPem();

        Assert.Throws<InvalidOperationException>(() => Ring("missing", new JwtKeyOptions { KeyId = "other", PrivateKeyPem = pem }));
        Assert.Throws<InvalidOperationException>(() => Ring("retired", new JwtKeyOptions { KeyId = "retired", PublicKeyPem = PublicPemOf(pem) }));
        Assert.Throws<InvalidOperationException>(() => Ring("empty", new JwtKeyOptions { KeyId = "empty" }));
    }

    [Fact]
    public void OnlyP256Keys_AreAccepted() =>
        Assert.Throws<InvalidOperationException>(() =>
            Ring("big", new JwtKeyOptions { KeyId = "big", PrivateKeyPem = NewKeyPem(System.Security.Cryptography.ECCurve.NamedCurves.nistP384) }));
}

public sealed class TenancyOptionsTests
{
    [Theory]
    [InlineData("Production", true, true)]
    [InlineData("Production", false, false)]
    [InlineData("Development", true, false)]
    public void TenantHeader_IsRefusedOutsideDevelopment(string environment, bool allowHeader, bool shouldFail)
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenancy:BaseDomain"] = "comtest.test",
                ["Tenancy:AllowTenantHeader"] = allowHeader.ToString(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
        ComTest.Infrastructure.Tenancy.TenancyServiceRegistration.AddTenancy(services, new TestEnvironment(environment));

        using var provider = services.BuildServiceProvider();

        var read = () => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComTest.Infrastructure.Tenancy.TenancyOptions>>().Value;

        if (shouldFail)
        {
            Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(read);
        }
        else
        {
            Assert.Equal("comtest.test", read().BaseDomain);
        }
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
