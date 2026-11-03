using ComTest.Api.ErrorHandling;
using ComTest.Api.Hosting;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ComTest.Api.Security;

/// <summary>Protects endpoints authenticated by the refresh cookie from cross-site requests.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireCsrfHeaderAttribute() : TypeFilterAttribute(typeof(CsrfHeaderFilter));

/// <summary>
/// A custom header cannot be sent cross-site without a CORS preflight, which our allowlist
/// refuses. Together with SameSite=Strict and the Origin check, this covers the cookie endpoints.
/// </summary>
public sealed class CsrfHeaderFilter(IOptions<CorsSettings> corsSettings, ErrorResultFactory errors) : IAuthorizationFilter
{
    public const string HeaderName = "X-Requested-With";

    private const string RequiredValue = "ComTest";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.HttpContext.Request;
        var hasHeader = string.Equals(request.Headers[HeaderName], RequiredValue, StringComparison.Ordinal);

        if (!hasHeader || !IsOriginAllowed(request))
        {
            context.Result = errors.Create(context.HttpContext, Error.Forbidden(ErrorCodes.Auth.CsrfRejected));
        }
    }

    private bool IsOriginAllowed(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();

        // Browsers send Origin on POST, so a missing one means a non-browser client.
        if (string.IsNullOrEmpty(origin))
        {
            return true;
        }

        return corsSettings.Value.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
            || string.Equals(origin, $"{request.Scheme}://{request.Host}", StringComparison.OrdinalIgnoreCase);
    }
}
