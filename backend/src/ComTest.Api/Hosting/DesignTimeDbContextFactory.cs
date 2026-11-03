using ComTest.Application.Abstractions;
using ComTest.Infrastructure;
using ComTest.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ComTest.Api.Hosting;

/// <summary>
/// Used by `dotnet ef`. Migrations run with the owner connection, not the application's limited
/// one, and only as a separate deployment step: the application never migrates itself.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(DependencyInjection.MigrationsConnectionName)
            ?? throw new InvalidOperationException(
                $"Connection string '{DependencyInjection.MigrationsConnectionName}' is not configured. See backend/README.md.");

        var options = new DbContextOptionsBuilder<AppDbContext>().UseAppDatabase(connectionString);

        return new AppDbContext((DbContextOptions<AppDbContext>)options.Options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;

        public bool IsPlatformAccess => false;
    }
}
