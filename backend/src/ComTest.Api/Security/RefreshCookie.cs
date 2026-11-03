using ComTest.Api.Hosting;
using Microsoft.Extensions.Options;

namespace ComTest.Api.Security;

/// <summary>
/// Keeps the refresh token where page scripts cannot read it. If an XSS bug ever appears, it
/// can at most use the short-lived access token, not steal the long-lived session.
/// </summary>
public sealed class RefreshCookie(IOptions<RefreshCookieSettings> options)
{
    public string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Cookies[options.Value.Name];
    }

    public void Write(HttpResponse response, string token, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(options.Value.Name, token, CreateOptions(expiresAt));
    }

    public void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(options.Value.Name, CreateOptions(null));
    }

    private CookieOptions CreateOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = options.Value.Path,
        Domain = options.Value.Domain,
        Expires = expiresAt,
        IsEssential = true,
    };
}
