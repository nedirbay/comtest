using System.Security.Cryptography;

namespace ComTest.IntegrationTests.Support;

/// <summary>An independent RFC 6238 implementation: what a user's authenticator app would produce.</summary>
public static class Totp
{
    private const int StepSeconds = 30;
    private const int Digits = 1_000_000;

    public static string Compute(string base32Key, int stepOffset = 0)
    {
        var step = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds) + stepOffset;
        var counter = BitConverter.GetBytes(step);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

#pragma warning disable CA5350 // RFC 6238 defines HMAC-SHA1, and authenticator apps use it.
        var hash = HMACSHA1.HashData(Base32Decode(base32Key), counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0F;
        var value = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];

        return (value % Digits).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string input)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        var clean = input.Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        var buffer = 0;
        var bits = 0;

        foreach (var character in clean)
        {
            buffer = (buffer << 5) | Alphabet.IndexOf(character, StringComparison.Ordinal);
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((buffer >> bits) & 0xFF));
            }
        }

        return [.. output];
    }
}
