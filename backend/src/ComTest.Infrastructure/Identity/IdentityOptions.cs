using System.ComponentModel.DataAnnotations;

namespace ComTest.Infrastructure.Identity;

public sealed class JwtKeyOptions
{
    [Required]
    public string KeyId { get; set; } = string.Empty;

    /// <summary>PKCS#8 PEM of a P-256 key. Leave empty on a retired key that only verifies old tokens.</summary>
    public string? PrivateKeyPem { get; set; }

    public string? PublicKeyPem { get; set; }
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 15)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Required]
    public string ActiveKeyId { get; set; } = string.Empty;

    [MinLength(1)]
    public IList<JwtKeyOptions> SigningKeys { get; } = new List<JwtKeyOptions>();
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>A refresh token left unused this long stops working.</summary>
    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>A session must sign in again after this long, however active it has been.</summary>
    [Range(1, 365)]
    public int SessionMaxDays { get; set; } = 30;

    /// <summary>
    /// Two tabs can refresh at the same moment with one token. Inside this window the loser gets
    /// a retry response without new tokens instead of triggering reuse detection and a logout.
    /// </summary>
    [Range(0, 60)]
    public int RefreshReuseGraceSeconds { get; set; } = 10;

    [Range(1, 15)]
    public int TwoFactorChallengeMinutes { get; set; } = 5;

    /// <summary>Base URL of the storefront, used to build the links in account emails.</summary>
    [Required]
    [Url]
    public string FrontendBaseUrl { get; set; } = string.Empty;

    public bool BreachedPasswordCheckEnabled { get; set; } = true;
}

public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    public string? Host { get; set; }

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "no-reply@comtest.local";

    public string FromName { get; set; } = "ComTest";
}
