using System.ComponentModel.DataAnnotations;

namespace ComTest.Api.Hosting;

public sealed class CorsSettings : IValidatableObject
{
    public const string SectionName = "Cors";

    public IList<string> AllowedOrigins { get; } = new List<string>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var origin in AllowedOrigins)
        {
            var isOrigin = Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https"
                && uri.PathAndQuery == "/"
                && !origin.EndsWith('/')
                && !origin.Contains('*', StringComparison.Ordinal);

            if (!isOrigin)
            {
                yield return new ValidationResult(
                    $"'{origin}' is not an exact origin, such as https://shop.example.com. Wildcards are not allowed.",
                    [nameof(AllowedOrigins)]);
            }
        }
    }
}

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    [Range(1, int.MaxValue)]
    public int GlobalPerMinute { get; set; } = 300;

    /// <summary>Shared by everyone in one store, so one busy store cannot starve the others.</summary>
    [Range(1, int.MaxValue)]
    public int TenantPerMinute { get; set; } = 6000;

    [Range(1, int.MaxValue)]
    public int LoginPerMinute { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int AccountActionPerMinute { get; set; } = 5;

    [Range(1, int.MaxValue)]
    public int RefreshPerMinute { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int SmsPerHour { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int CheckoutPerMinute { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int TrackingPerMinute { get; set; } = 30;
}

/// <summary>Reverse proxies whose X-Forwarded-* headers may be believed. Empty means none.</summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public IList<string> KnownProxies { get; } = new List<string>();

    public IList<string> KnownNetworks { get; } = new List<string>();
}

public sealed class RefreshCookieSettings
{
    public const string SectionName = "Auth:RefreshCookie";

    /// <summary>The __Secure- prefix makes browsers refuse this cookie unless it is Secure.</summary>
    [Required]
    public string Name { get; set; } = "__Secure-comtest-refresh";

    /// <summary>The cookie is sent to the auth endpoints only, not with every API call.</summary>
    [Required]
    public string Path { get; set; } = "/api/v1/auth";

    public string? Domain { get; set; }
}
