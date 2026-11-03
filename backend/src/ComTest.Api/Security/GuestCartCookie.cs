namespace ComTest.Api.Security;

/// <summary>Identifies a guest's cart. HttpOnly, so scripts cannot read it, and only its hash is stored.</summary>
public sealed class GuestCartCookie
{
    public const string Name = "__Secure-comtest-cart";

    private const string Path = "/api/v1";

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Cookies[Name];
    }

    public void Write(HttpResponse response, string token)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(Name, token, CreateOptions(DateTimeOffset.UtcNow.Add(Lifetime)));
    }

    public void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(Name, CreateOptions(null));
    }

    // Lax rather than Strict: a guest arriving from a link in a chat or a search result should still see their cart.
    private static CookieOptions CreateOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = Path,
        Expires = expires,
        IsEssential = true,
    };
}
