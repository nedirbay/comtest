using System.Security.Cryptography.X509Certificates;
using ComTest.Application;
using ComTest.Application.Abstractions;
using ComTest.Infrastructure.Catalog;
using ComTest.Infrastructure.Identity;
using ComTest.Infrastructure.Media;
using ComTest.Infrastructure.Ordering;
using ComTest.Infrastructure.Persistence;
using ComTest.Infrastructure.Persistence.Interceptors;
using ComTest.Infrastructure.Persistence.Outbox;
using ComTest.Infrastructure.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ComTest.Infrastructure;

public static class DependencyInjection
{
    public const string RuntimeConnectionName = "Default";
    public const string MigrationsConnectionName = "Migrations";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddSingleton(TimeProvider.System);

        AddPersistence(services, configuration, environment);
        AddDataProtectionKeys(services, configuration, environment);
        AddOutbox(services);

        services.AddTenancy(environment);
        services.AddCatalogInfrastructure();
        services.AddOrderingInfrastructure();
        services.AddMediaInfrastructure(environment);
        services.AddIdentityServices(environment);
        services.AddMessageHandlers(typeof(DependencyInjection).Assembly);

        return services;
    }

    /// <summary>The one place that sets provider options, shared with design-time tooling.</summary>
    public static DbContextOptionsBuilder UseAppDatabase(this DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseSnakeCaseNamingConvention();
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString(RuntimeConnectionName)
            ?? throw new InvalidOperationException(
                $"Connection string '{RuntimeConnectionName}' is not configured. See backend/README.md.");

        ConnectionStringGuard.Validate(connectionString, environment);

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantScope>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<PlatformDataAccess>();
        services.AddScoped<AuditingInterceptor>();
        services.AddScoped<TenantSessionInterceptor>();

        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseAppDatabase(connectionString)
            .AddInterceptors(
                provider.GetRequiredService<AuditingInterceptor>(),
                provider.GetRequiredService<TenantSessionInterceptor>()));
    }

    public static void AddDataProtectionKeys(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var builder = services.AddDataProtection()
            .SetApplicationName("ComTest")
            .PersistKeysToDbContext<AppDbContext>();

        var certificateBase64 = configuration["DataProtection:CertificatePfxBase64"];

        if (!string.IsNullOrEmpty(certificateBase64))
        {
#pragma warning disable CA2000 // The certificate must live as long as the application.
            var certificate = X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(certificateBase64),
                configuration["DataProtection:CertificatePassword"]);
#pragma warning restore CA2000

            builder.ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate);
        }
        else if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "DataProtection:CertificatePfxBase64 must be configured outside Development, so keys are encrypted at rest.");
        }
    }

    private static void AddOutbox(IServiceCollection services)
    {
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.SectionName);
        services.AddSingleton(new DomainEventRegistry([typeof(ComTest.Domain.AssemblyMarker).Assembly]));
        services.AddSingleton<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();
    }
}
