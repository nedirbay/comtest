using ComTest.Api.ErrorHandling;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Domain.Modules.Tenancy;
using ComTest.Infrastructure.Identity;
using ComTest.Infrastructure.Persistence;
using ComTest.Infrastructure.Tenancy;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace ComTest.Api.Hosting;

/// <summary>
/// Decides which tenant a request belongs to, from its host name and its token. When both name a
/// tenant they must agree, so a token from one firm is useless against another firm's address.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, IOptions<TenancyOptions> options)
{
    public const string TenantHeader = "X-Tenant";

    private static readonly PathString HealthPath = new("/health");

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, ITenantDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(directory);

        if (context.Request.Path.StartsWithSegments(HealthPath, StringComparison.Ordinal))
        {
            await next(context);

            return;
        }

        var cancellationToken = context.RequestAborted;
        var (fromHost, hostNamedATenant) = await ResolveFromHostAsync(context.Request, directory, cancellationToken);

        if (hostNamedATenant && fromHost is null)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, ErrorCodes.Tenant.NotFound);

            return;
        }

        var hasClaim = Guid.TryParse(context.User.FindFirst(AppClaims.TenantId)?.Value, out var fromToken);

        if (fromHost is not null && hasClaim && fromHost.Id != fromToken)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.Tenant.Mismatch);

            return;
        }

        var tenant = fromHost ?? (hasClaim ? await directory.FindByIdAsync(fromToken, cancellationToken) : null);

        if (tenant is null)
        {
            await next(context);

            return;
        }

        if (tenant.Status != TenantStatus.Active)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.Tenant.Suspended);

            return;
        }

        tenantContext.Use(tenant.Id);

        using (LogContext.PushProperty("TenantId", tenant.Id))
        {
            await next(context);
        }
    }

    private async Task<(TenantInfo? Tenant, bool HostNamedATenant)> ResolveFromHostAsync(
        HttpRequest request,
        ITenantDirectory directory,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (settings.AllowTenantHeader && request.Headers.TryGetValue(TenantHeader, out var slugFromHeader))
        {
            return (await directory.FindBySlugAsync(slugFromHeader.ToString(), cancellationToken), true);
        }

        var host = request.Host.Host;
        var suffix = "." + settings.BaseDomain;

        if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            var label = host[..^suffix.Length].ToLowerInvariant();

            // Only single-level names are stores. api.comtest.tm and the like are platform hosts.
            return label.Contains('.', StringComparison.Ordinal) || PlatformHosts.Contains(label)
                ? (null, false)
                : (await directory.FindBySlugAsync(label, cancellationToken), true);
        }

        if (string.Equals(host, settings.BaseDomain, StringComparison.OrdinalIgnoreCase))
        {
            return (null, false);
        }

        return (await directory.FindByHostnameAsync(host, cancellationToken), false);
    }

    private static readonly HashSet<string> PlatformHosts = new(StringComparer.Ordinal) { "api", "www", "admin", "platform" };

    private static async Task WriteProblemAsync(HttpContext context, int status, string code)
    {
        context.Response.StatusCode = status;

        var problem = new ProblemDetails { Status = status };
        problem.Extensions[ProblemFields.Code] = code;

        await context.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
