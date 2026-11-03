using System.ComponentModel.DataAnnotations;
using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Infrastructure.Identity;
using ComTest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ComTest.Infrastructure.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    /// <summary>Stores live at {slug}.{BaseDomain}. For example, comtest.tm.</summary>
    [Required]
    public string BaseDomain { get; set; } = string.Empty;

    /// <summary>Accepts X-Tenant for local work, where subdomains are awkward. Refused in production.</summary>
    public bool AllowTenantHeader { get; set; }
}

public sealed class TenantDirectory(IServiceScopeFactory scopeFactory, IMemoryCache cache) : ITenantDirectory
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public Task<TenantInfo?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
        GetAsync($"tenant:slug:{slug}", db => db.Tenants.Where(tenant => tenant.Slug == slug), cancellationToken);

    public Task<TenantInfo?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
        GetAsync($"tenant:id:{tenantId:N}", db => db.Tenants.Where(tenant => tenant.Id == tenantId), cancellationToken);

    public Task<TenantInfo?> FindByHostnameAsync(string hostname, CancellationToken cancellationToken)
    {
        var host = hostname.ToLowerInvariant();

        return GetAsync(
            $"tenant:host:{host}",
            db => from domain in db.TenantDomains
                  join tenant in db.Tenants on domain.OwnerTenantId equals tenant.Id
                  where domain.Hostname == host && domain.VerifiedAt != null
                  select tenant,
            cancellationToken);
    }

    public void Invalidate(TenantInfo tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        cache.Remove($"tenant:slug:{tenant.Slug}");
        cache.Remove($"tenant:id:{tenant.Id:N}");
    }

    private async Task<TenantInfo?> GetAsync(
        string key,
        Func<AppDbContext, IQueryable<ComTest.Domain.Modules.Tenancy.Tenant>> query,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(key, out TenantInfo? cached))
        {
            return cached;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = await query(db)
            .AsNoTracking()
            .Select(item => new TenantInfo(item.Id, item.Slug, item.Name, item.Status))
            .FirstOrDefaultAsync(cancellationToken);

        cache.Set(key, tenant, CacheDuration);

        return tenant;
    }
}

public static class TenancyServiceRegistration
{
    public static IServiceCollection AddTenancy(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        services.AddMemoryCache();
        services.AddSingleton<ITenantDirectory, TenantDirectory>();
        services.AddSingleton<IStaffInvitationMailer, StaffInvitationMailer>();

        services.AddOptions<TenancyOptions>()
            .BindConfiguration(TenancyOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                options => !options.AllowTenantHeader || environment.IsDevelopment() || environment.IsEnvironment("Testing"),
                "Tenancy:AllowTenantHeader must be off outside Development: a client must not choose its own tenant.")
            .ValidateOnStart();

        return services;
    }
}
