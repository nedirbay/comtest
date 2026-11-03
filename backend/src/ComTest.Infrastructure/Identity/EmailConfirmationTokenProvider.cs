using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ComTest.Infrastructure.Identity;

public sealed class EmailConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public const string ProviderName = "EmailConfirmation";

    public EmailConfirmationTokenProviderOptions()
    {
        Name = ProviderName;
        TokenLifespan = TimeSpan.FromHours(24);
    }
}

/// <summary>Email links may wait in an inbox, so they get longer than the password reset default.</summary>
public sealed class EmailConfirmationTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<EmailConfirmationTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider, options, logger);
