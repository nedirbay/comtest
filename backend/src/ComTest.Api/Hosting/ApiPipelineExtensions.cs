using ComTest.Api.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;

namespace ComTest.Api.Hosting;

public static class ApiPipelineExtensions
{
    private static readonly PathString HealthPath = new("/health");

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // First, so that everything below sees the real client address and scheme.
        app.UseForwardedHeaders();

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseSerilogRequestLogging(options =>
            options.GetLevel = (context, _, exception) => GetRequestLogLevel(context, exception));

        app.UseRequestLocalization();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        // Health probes come over plain HTTP from inside the network and must not be redirected.
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments(HealthPath, StringComparison.Ordinal),
            branch => branch.UseHttpsRedirection());

        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseRequestTimeouts();

        return app;
    }

    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapControllers();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous()
            .DisableRateLimiting();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(HealthTags.Ready),
        })
            .AllowAnonymous()
            .DisableRateLimiting();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().WithDocumentPerVersion().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        return app;
    }

    private static Serilog.Events.LogEventLevel GetRequestLogLevel(HttpContext context, Exception? exception)
    {
        if (exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return Serilog.Events.LogEventLevel.Error;
        }

        return context.Request.Path.StartsWithSegments(HealthPath, StringComparison.Ordinal)
            ? Serilog.Events.LogEventLevel.Verbose
            : Serilog.Events.LogEventLevel.Information;
    }
}
