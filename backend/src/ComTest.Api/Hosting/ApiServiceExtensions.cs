using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using ComTest.Api.ErrorHandling;
using ComTest.Api.Localization;
using ComTest.Api.Security;
using ComTest.Application.Abstractions;
using ComTest.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ComTest.Api.Hosting;

public static class ApiServiceExtensions
{
    private const int HstsMaxAgeDays = 365;
    private const int DefaultHttpsPort = 443;

    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        AddSettings(services);
        AddControllersAndValidation(services);
        AddVersioningAndOpenApi(services);
        AddErrorHandling(services);
        AddLocalizationSupport(services);
        AddCorsPolicy(services);
        AddTransportSecurity(services, configuration, environment);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddSingleton<RefreshCookie>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemAuthorizationResultHandler>();
        services.AddApiRateLimiting();
        services.AddRequestTimeouts(options => options.DefaultPolicy = new RequestTimeoutPolicy
        {
            Timeout = RequestLimits.DefaultRequestTimeout,
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
        });

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: [HealthTags.Ready]);

        return services;
    }

    private static void AddSettings(IServiceCollection services)
    {
        services.AddOptions<CorsSettings>().BindConfiguration(CorsSettings.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<RateLimitSettings>().BindConfiguration(RateLimitSettings.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ForwardedHeadersSettings>().BindConfiguration(ForwardedHeadersSettings.SectionName);
        services.AddOptions<RefreshCookieSettings>().BindConfiguration(RefreshCookieSettings.SectionName).ValidateDataAnnotations().ValidateOnStart();
    }

    private static void AddControllersAndValidation(IServiceCollection services)
    {
        services
            .AddControllers(options => options.Filters.Add<FluentValidationFilter>())
            .AddJsonOptions(options =>
            {
                // Unknown fields fail instead of being ignored, so nothing extra can sneak in with a request.
                options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                options.AllowInputFormatterExceptionMessages = false;
            });

        ValidatorOptions.Global.LanguageManager = new ValidationLanguageManager();
        ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
            member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
            member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);
    }

    private static void AddVersioningAndOpenApi(IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = false;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
                options.ReportApiVersions = true;
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = true;
            })
            .AddOpenApi(options => options.Document.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
    }

    private static void AddErrorHandling(IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsEnricher.Enrich);
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddSingleton<ErrorResultFactory>();
    }

    private static void AddLocalizationSupport(IServiceCollection services)
    {
        services.AddLocalization();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            CultureInfo[] cultures = [new("tk"), new("ru"), new("en")];

            options.DefaultRequestCulture = new RequestCulture("tk");
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.FallBackToParentCultures = true;
            options.FallBackToParentUICultures = true;
            options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
        });
    }

    private static void AddCorsPolicy(IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
            .Configure<IOptions<CorsSettings>>((options, settings) => options.AddDefaultPolicy(policy => policy
                .WithOrigins([.. settings.Value.AllowedOrigins])
                .AllowCredentials()
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                .WithHeaders(
                    "Authorization",
                    "Content-Type",
                    "Accept-Language",
                    "Idempotency-Key",
                    CorrelationIdMiddleware.HeaderName,
                    CsrfHeaderFilter.HeaderName,
                    TenantResolutionMiddleware.TenantHeader)
                .WithExposedHeaders(CorrelationIdMiddleware.HeaderName, "Retry-After")
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
    }

    private static void AddTransportSecurity(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(HstsMaxAgeDays);
            options.IncludeSubDomains = true;
        });

        if (!environment.IsDevelopment())
        {
            // Without a known port the redirect middleware only logs a warning and lets HTTP through.
            services.AddHttpsRedirection(options =>
                options.HttpsPort = configuration.GetValue("HttpsRedirection:HttpsPort", DefaultHttpsPort));
        }

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ForwardedHeadersSettings>>((options, settings) =>
            {
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();

                // With both lists empty the middleware believes every peer, so without any
                // configured proxy forwarded headers must be switched off entirely.
                if (settings.Value.KnownProxies.Count == 0 && settings.Value.KnownNetworks.Count == 0)
                {
                    options.ForwardedHeaders = ForwardedHeaders.None;

                    return;
                }

                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;

                foreach (var proxy in settings.Value.KnownProxies)
                {
                    options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
                }

                foreach (var network in settings.Value.KnownNetworks)
                {
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                }
            });
    }
}

public static class HealthTags
{
    public const string Ready = "ready";
}
