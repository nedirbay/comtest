using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ComTest.Infrastructure.Identity;

/// <summary>
/// Loads the signing keys. The active key signs new tokens; every listed key can verify, so
/// keys rotate by adding a new one, making it active, and later removing the old one.
/// </summary>
public sealed class JwtKeyRing : IDisposable
{
    private const int RequiredKeySize = 256;

    private readonly List<ECDsa> _keys = [];

    public JwtKeyRing(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Value;
        var validationKeys = new List<SecurityKey>();

        foreach (var key in settings.SigningKeys)
        {
            var ecdsa = ECDsa.Create();
            _keys.Add(ecdsa);
            ecdsa.ImportFromPem(key.PrivateKeyPem ?? key.PublicKeyPem
                ?? throw new InvalidOperationException($"JWT key '{key.KeyId}' has no key material."));

            if (ecdsa.KeySize != RequiredKeySize)
            {
                throw new InvalidOperationException($"JWT key '{key.KeyId}' must be a P-256 key for ES256.");
            }

            var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = key.KeyId };
            validationKeys.Add(securityKey);

            if (string.Equals(key.KeyId, settings.ActiveKeyId, StringComparison.Ordinal))
            {
                if (key.PrivateKeyPem is null)
                {
                    throw new InvalidOperationException($"Active JWT key '{key.KeyId}' has no private key.");
                }

                SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);
            }
        }

        ValidationKeys = validationKeys;

        if (SigningCredentials is null)
        {
            throw new InvalidOperationException($"Active JWT key '{settings.ActiveKeyId}' is not in the key list.");
        }
    }

    public SigningCredentials SigningCredentials { get; }

    public IReadOnlyList<SecurityKey> ValidationKeys { get; }

    public void Dispose()
    {
        foreach (var key in _keys)
        {
            key.Dispose();
        }
    }
}
