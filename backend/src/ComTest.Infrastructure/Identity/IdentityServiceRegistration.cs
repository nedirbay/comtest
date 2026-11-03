using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Identity.Authorization;
using ComTest.Infrastructure.Identity.Passwords;
using ComTest.Infrastructure.Messaging;
using ComTest.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ComTest.Infrastructure.Identity;

public static class IdentityServiceRegistration
{
    /// <summary>OWASP's minimum for PBKDF2-HMAC-SHA512.</summary>
    public const int PasswordIterations = 210_000;

    private const int MinPasswordLength = 12;
    private const int MaxFailedAttempts = 5;
    private const int ClockSkewSeconds = 30;

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BreachCheckTimeout = TimeSpan.FromSeconds(3);
    private static readonly Uri BreachCheckBaseAddress = new("https://api.pwnedpasswords.com/");

    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        AddIdentityCore(services);
        AddJwtBearer(services);
        AddAuthorizationPolicies(services);
        AddBreachedPasswordCheck(services);
        AddEmailAndSms(services, environment);

        services.AddSingleton<JwtKeyRing>();
        services.AddScoped<AccessTokenIssuer>();
        services.AddScoped<SecurityEventLogger>();
        services.AddScoped<SessionManager>();
        services.AddScoped<SessionTokenValidator>();
        services.AddScoped<TwoFactorCodeVerifier>();
        services.AddScoped<ISignInService, SignInService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();
        services.AddScoped<IPhoneVerificationService, PhoneVerificationService>();
        services.AddScoped<ITenantSessionService, TenantSessionService>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddSingleton<AuthEmailComposer>();
        services.AddHostedService<IdentityCleanupService>();

        return services;
    }

    private static void AddIdentityCore(IServiceCollection services)
    {
        services.Configure<PasswordHasherOptions>(options =>
        {
            options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
            options.IterationCount = PasswordIterations;
        });

        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(30));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty;

                options.Password.RequiredLength = MinPasswordLength;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = MaxFailedAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;

                options.SignIn.RequireConfirmedEmail = true;
                options.Tokens.EmailConfirmationTokenProvider = EmailConfirmationTokenProviderOptions.ProviderName;
                options.Tokens.ChangeEmailTokenProvider = EmailConfirmationTokenProviderOptions.ProviderName;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddUserStore<ProtectedUserStore>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<EmailConfirmationTokenProvider>(EmailConfirmationTokenProviderOptions.ProviderName)
            .AddPasswordValidator<PasswordPolicyValidator>();
    }

    private static void AddJwtBearer(IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtKeyRing, IOptions<JwtOptions>>((bearer, keys, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKeys = keys.ValidationKeys,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                    ValidTypes = [AppClaims.AccessTokenType],
                    ClockSkew = TimeSpan.FromSeconds(ClockSkewSeconds),
                    NameClaimType = AppClaims.Subject,
                    RoleClaimType = AppClaims.Role,
                };
                bearer.Events = new JwtBearerEvents { OnTokenValidated = ValidateSessionAsync };
            });
    }

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var validator = context.HttpContext.RequestServices.GetRequiredService<SessionTokenValidator>();

        var isValid = Guid.TryParse(principal?.FindFirst(AppClaims.Subject)?.Value, out var userId)
            && Guid.TryParse(principal?.FindFirst(AppClaims.SessionId)?.Value, out var sessionId)
            && principal?.FindFirst(AppClaims.SecurityStamp)?.Value is { } stamp
            && await validator.IsValidAsync(userId, sessionId, stamp, ReadTenantId(principal), context.HttpContext.RequestAborted);

        if (!isValid)
        {
            context.Fail("The session is no longer valid.");
        }
    }

    private static Guid? ReadTenantId(System.Security.Claims.ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(AppClaims.TenantId)?.Value, out var tenantId) ? tenantId : null;

    private static void AddAuthorizationPolicies(IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Deny by default: an endpoint without any attribute still requires a signed-in user.
        services.AddAuthorizationBuilder().SetFallbackPolicy(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    }

    private static void AddBreachedPasswordCheck(IServiceCollection services)
    {
        services.AddHttpClient<IBreachedPasswordChecker, HibpBreachedPasswordChecker>(client =>
        {
            client.BaseAddress = BreachCheckBaseAddress;
            client.Timeout = BreachCheckTimeout;
        });
    }

    private static void AddEmailAndSms(IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<InMemoryEmailDispatcher>();
        services.AddSingleton<IEmailDispatcher>(provider => provider.GetRequiredService<InMemoryEmailDispatcher>());
        services.AddHostedService<EmailDeliveryService>();

        services.AddSingleton<IEmailSender>(provider =>
        {
            var hasSmtp = !string.IsNullOrEmpty(provider.GetRequiredService<IOptions<SmtpOptions>>().Value.Host);

            if (hasSmtp)
            {
                return ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider);
            }

            return environment.IsDevelopment()
                ? new ConsoleEmailSender()
                : throw new InvalidOperationException("Email:Smtp:Host must be configured outside Development.");
        });

        if (environment.IsDevelopment())
        {
            services.AddSingleton<ISmsSender, LoggingSmsSender>();
        }
        else
        {
            services.AddSingleton<ISmsSender, UnconfiguredSmsSender>();
        }
    }
}
