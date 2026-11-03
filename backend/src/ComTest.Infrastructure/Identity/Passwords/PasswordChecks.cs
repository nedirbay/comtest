using System.Security.Cryptography;
using System.Text;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ComTest.Infrastructure.Identity.Passwords;

public interface IBreachedPasswordChecker
{
    Task<bool> IsBreachedAsync(string password, CancellationToken cancellationToken);
}

/// <summary>
/// Checks Have I Been Pwned using k-anonymity: only the first 5 hex characters of the SHA-1
/// leave this server, never the password or its full hash. If the service is unreachable the
/// check is skipped, because an outage elsewhere should not stop people from registering.
/// </summary>
public sealed partial class HibpBreachedPasswordChecker(
    HttpClient httpClient,
    IOptions<AuthOptions> options,
    ILogger<HibpBreachedPasswordChecker> logger) : IBreachedPasswordChecker
{
    private const int PrefixLength = 5;

    public async Task<bool> IsBreachedAsync(string password, CancellationToken cancellationToken)
    {
        if (!options.Value.BreachedPasswordCheckEnabled)
        {
            return false;
        }

#pragma warning disable CA5350 // SHA-1 is what this API defines; it is a lookup key here, not a stored hash.
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
#pragma warning restore CA5350
        var prefix = hash[..PrefixLength];
        var suffix = hash[PrefixLength..];

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"range/{prefix}", UriKind.Relative));
            request.Headers.Add("Add-Padding", "true");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = line.IndexOf(':', StringComparison.Ordinal);

                if (separator > 0
                    && line.AsSpan(0, separator).Equals(suffix, StringComparison.OrdinalIgnoreCase)
                    && !line.AsSpan(separator + 1).SequenceEqual("0"))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            LogCheckSkipped(logger, exception);

            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Breached password check skipped because the service is unavailable")]
    private static partial void LogCheckSkipped(ILogger logger, Exception exception);
}

/// <summary>
/// Follows NIST 800-63B: length and known-bad checks rather than composition rules. The small
/// built-in list still catches common passwords when the online check is unreachable.
/// </summary>
public sealed class PasswordPolicyValidator(IBreachedPasswordChecker breachedPasswords) : IPasswordValidator<ApplicationUser>
{
    private const int MinIdentityPartLength = 4;

    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012", "1234567890123", "12345678901234", "password1234", "password12345", "password123456",
        "passwordpassword", "qwertyuiop12", "qwertyuiop123", "qwertyuiopasdf", "qwerty123456", "qwerty1234567",
        "administrator", "administrator1", "adminadmin123", "admin1234567", "welcome12345", "welcome123456",
        "letmein123456", "iloveyou1234", "iloveyou12345", "changeme1234", "changeme12345", "111111111111",
        "000000000000", "aaaaaaaaaaaa", "abcdefghijkl", "abc123456789", "1q2w3e4r5t6y", "1qaz2wsx3edc",
        "zaq12wsxcde3", "asdfghjkl123", "turkmenistan", "turkmenistan1", "turkmenistan123", "ashgabat1234",
        "ashgabat12345", "comtest12345",
    };

    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(password))
        {
            return IdentityResult.Success;
        }

        var errors = new List<IdentityError>();

        if (CommonPasswords.Contains(password))
        {
            errors.Add(new IdentityError { Code = ErrorCodes.Password.TooCommon });
        }

        if (ContainsIdentity(password, user))
        {
            errors.Add(new IdentityError { Code = ErrorCodes.Password.ContainsIdentity });
        }

        if (errors.Count == 0 && await breachedPasswords.IsBreachedAsync(password, CancellationToken.None))
        {
            errors.Add(new IdentityError { Code = ErrorCodes.Password.Breached });
        }

        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]);
    }

    private static bool ContainsIdentity(string password, ApplicationUser user)
    {
        var localPart = user.Email?.Split('@')[0];

        return localPart is { Length: >= MinIdentityPartLength }
            && password.Contains(localPart, StringComparison.OrdinalIgnoreCase);
    }
}

public static class PasswordErrors
{
    /// <summary>Turns Identity's errors into our validation error. Details hold codes the API translates.</summary>
    public static Error ToValidationError(IdentityResult result, string field)
    {
        ArgumentNullException.ThrowIfNull(result);

        var codes = result.Errors.Select(error => error.Code switch
        {
            nameof(IdentityErrorDescriber.PasswordTooShort) => ErrorCodes.Password.TooShort,
            _ when error.Code.StartsWith("password.", StringComparison.Ordinal) => error.Code,
            _ => ErrorCodes.Password.Rejected,
        });

        return Error.Validation(
            ErrorCodes.Auth.WeakPassword,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = codes.Distinct().ToArray() });
    }
}
