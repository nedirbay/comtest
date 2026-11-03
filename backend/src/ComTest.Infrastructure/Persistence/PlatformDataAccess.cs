using Microsoft.Extensions.DependencyInjection;

namespace ComTest.Infrastructure.Persistence;

/// <summary>
/// The one sanctioned way to read across tenants, such as a user's memberships during sign-in,
/// before any tenant is known. It runs in its own scope, so the wider access cannot leak into
/// the request's own work. A source test keeps platform access from being turned on elsewhere.
/// </summary>
public sealed class PlatformDataAccess(IServiceScopeFactory scopeFactory)
{
    public async Task<T> RunAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using var scope = scopeFactory.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<TenantContext>().UsePlatformAccess();

        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
