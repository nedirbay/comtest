using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace ComTest.Api.Hosting;

public static class RequestLimits
{
    /// <summary>JSON bodies only: uploads go straight to object storage through presigned URLs.</summary>
    public const long MaxRequestBodyBytes = 1024 * 1024;

    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

    public static void ConfigureKestrel(KestrelServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
        options.Limits.MaxRequestHeadersTotalSize = 16 * 1024;
        options.Limits.MaxRequestLineSize = 8 * 1024;
        options.Limits.MaxRequestHeaderCount = 64;
        options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
        options.Limits.MaxConcurrentConnections = 10_000;
    }
}
