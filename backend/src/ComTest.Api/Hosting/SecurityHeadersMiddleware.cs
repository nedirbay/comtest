using Microsoft.Net.Http.Headers;

namespace ComTest.Api.Hosting;

/// <summary>
/// Sets the security headers in OnStarting, so that error responses get them too: the exception
/// handler clears headers that were set earlier. The API returns only data, so CSP blocks all.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    private const string StrictContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    private static readonly PathString ApiReferencePath = new("/scalar");

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var isDevelopmentReference = environment.IsDevelopment()
            && context.Request.Path.StartsWithSegments(ApiReferencePath, StringComparison.OrdinalIgnoreCase);

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";

            // The Scalar page needs its own scripts. It exists only in Development.
            if (!isDevelopmentReference)
            {
                headers.ContentSecurityPolicy = StrictContentSecurityPolicy;
            }

            // Private by default. An endpoint that may be cached must say so explicitly.
            if (!headers.ContainsKey(HeaderNames.CacheControl))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
