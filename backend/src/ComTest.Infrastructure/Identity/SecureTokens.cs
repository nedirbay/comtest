using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace ComTest.Infrastructure.Identity;

internal static class SecureTokens
{
    private const int RefreshTokenBytes = 32;

    public static string NewRefreshToken() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenBytes));

    /// <summary>
    /// SHA-256 suits high-entropy random tokens, which cannot be guessed. It must not be used
    /// on its own for passwords.
    /// </summary>
    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string NewNumericCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    public static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
