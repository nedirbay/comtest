using System.ComponentModel.DataAnnotations;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ComTest.Application.Modules.Media;
using ComTest.Application.Modules.Media.Contracts;
using ComTest.Domain.Modules.Media;
using ComTest.Infrastructure.Persistence;
using ComTest.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace ComTest.Infrastructure.Media;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>S3 endpoint, such as a MinIO address. Empty means storage is switched off.</summary>
    [Url]
    public string? ServiceUrl { get; set; }

    public string Bucket { get; set; } = "comtest";

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    public string Region { get; set; } = "us-east-1";

    /// <summary>CDN address serving the bucket. Without one, visitors get signed URLs.</summary>
    [Url]
    public string? PublicBaseUrl { get; set; }

    public bool CreateBucketIfMissing { get; set; }
}

public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private const int MaxRetries = 2;

    private static readonly TimeSpan PublicUrlLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly StorageOptions _options;
    private readonly AmazonS3Client? _client;

    public S3FileStorage(IOptions<StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;

        if (string.IsNullOrEmpty(_options.ServiceUrl))
        {
            return;
        }

        _client = new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = _options.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = _options.Region,

                // A storage outage must fail quickly rather than tie up requests and background work.
                Timeout = RequestTimeout,
                MaxErrorRetry = MaxRetries,

                // S3-compatible servers do not all understand the newer trailing checksums.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }

    public bool IsConfigured => _client is not null;

    private AmazonS3Client Client => _client ?? throw new InvalidOperationException("File storage is not configured.");

    private Protocol Protocol => _options.ServiceUrl!.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ? Protocol.HTTPS : Protocol.HTTP;

    public Uri CreateUploadUrl(string key, string contentType, TimeSpan lifetime) => new(Client.GetPreSignedURL(new GetPreSignedUrlRequest
    {
        BucketName = _options.Bucket,
        Key = key,
        Verb = HttpVerb.PUT,
        ContentType = contentType,
        Protocol = Protocol,
        Expires = DateTime.UtcNow.Add(lifetime),
    }));

    public async Task<StoredFileInfo?> StatAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await Client.GetObjectMetadataAsync(_options.Bucket, key, cancellationToken);

            return new StoredFileInfo(metadata.ContentLength);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<byte[]?> ReadAsync(string key, long maxBytes, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Client.GetObjectAsync(_options.Bucket, key, cancellationToken);

            if (response.ContentLength > maxBytes)
            {
                return null;
            }

            using var memory = new MemoryStream((int)response.ContentLength);
            await response.ResponseStream.CopyToAsync(memory, cancellationToken);

            return memory.ToArray();
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task WriteAsync(string key, ReadOnlyMemory<byte> content, string contentType, string cacheControl, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content.ToArray(), writable: false);

        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            UseChunkEncoding = false,
        };

        request.Headers.CacheControl = cacheControl;

        await Client.PutObjectAsync(request, cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await Client.DeleteObjectAsync(_options.Bucket, key, cancellationToken);

    public Uri GetPublicUrl(string key)
    {
        if (!string.IsNullOrEmpty(_options.PublicBaseUrl))
        {
            return new Uri($"{_options.PublicBaseUrl.TrimEnd('/')}/{key}");
        }

        return new Uri(Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = Protocol,
            Expires = DateTime.UtcNow.Add(PublicUrlLifetime),
        }));
    }

    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Client.HeadBucketAsync(new HeadBucketRequest { BucketName = _options.Bucket }, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await Client.PutBucketAsync(_options.Bucket, cancellationToken);
        }
    }

    public void Dispose() => _client?.Dispose();
}

/// <summary>
/// Trusts nothing about the file except its bytes: the signature decides the type, the header is
/// checked before decoding so that a tiny file cannot expand to fill the memory, and only freshly
/// encoded pixels come out, so EXIF data and anything appended to the file are left behind.
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor
{
    private const int MaxDimension = 10_000;
    private const long MaxPixels = 50_000_000;
    private const int WebpQuality = 82;

    private static readonly (string Name, int MaxWidth)[] Sizes = [("thumb", 320), ("medium", 800), ("large", 1600)];

    public Result<ProcessedImage> Process(byte[] input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!HasImageSignature(input))
        {
            return Error.Validation(ErrorCodes.Media.UnsupportedType);
        }

        using var data = SKData.CreateCopy(input);
        using var codec = SKCodec.Create(data);

        if (codec is null)
        {
            return Error.Validation(ErrorCodes.Media.InvalidImage);
        }

        var info = codec.Info;

        if (info.Width <= 0 || info.Height <= 0 || info.Width > MaxDimension || info.Height > MaxDimension || (long)info.Width * info.Height > MaxPixels)
        {
            return Error.Validation(ErrorCodes.Media.TooLarge);
        }

        using var decoded = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));

        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
        {
            return Error.Validation(ErrorCodes.Media.InvalidImage);
        }

        using var oriented = ApplyOrientation(decoded, codec.EncodedOrigin);

        var source = oriented ?? decoded;
        var renditions = new List<ImageRendition>(Sizes.Length);

        foreach (var (name, maxWidth) in Sizes)
        {
            var rendition = Encode(source, name, maxWidth);

            if (rendition is null)
            {
                return Error.Validation(ErrorCodes.Media.InvalidImage);
            }

            renditions.Add(rendition);
        }

        return new ProcessedImage(renditions, source.Width, source.Height);
    }

    internal static bool HasImageSignature(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith<byte>([0xFF, 0xD8, 0xFF])
        || bytes.StartsWith<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
        || (bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8));

    private static ImageRendition? Encode(SKBitmap source, string name, int maxWidth)
    {
        var width = Math.Min(source.Width, maxWidth);
        var height = Math.Max(1, (int)Math.Round(source.Height * (double)width / source.Width));

        using var resized = source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.Mitchell));

        if (resized is null)
        {
            return null;
        }

        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);

        return encoded is null ? null : new ImageRendition(name, encoded.ToArray(), width, height);
    }

    /// <summary>The orientation flag is metadata and is about to be dropped, so turn the pixels instead.</summary>
    private static SKBitmap? ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft)
        {
            return null;
        }

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(new SKImageInfo(swap ? source.Height : source.Width, swap ? source.Width : source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));

        using var canvas = new SKCanvas(result);

        float width = source.Width;
        float height = source.Height;

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(height, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(height, width);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, width);
                canvas.RotateDegrees(-90);
                break;
            default:
                break;
        }

        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);

        return result;
    }
}

/// <summary>Removes uploads that were started and never finished, and rejected ones after a while.</summary>
public sealed partial class MediaHousekeeping(
    PlatformDataAccess platformData,
    IFileStorage storage,
    TimeProvider timeProvider,
    ILogger<MediaHousekeeping> logger)
{
    private const int BatchSize = 200;

    private static readonly TimeSpan RejectedRetention = TimeSpan.FromDays(7);

    public async Task<int> RemoveAbandonedAsync(CancellationToken cancellationToken)
    {
        if (!storage.IsConfigured)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        var rejectedBefore = now - RejectedRetention;

        return await platformData.RunAsync(async data =>
        {
            var assets = await data.MediaAssets
                .Where(asset => (asset.Status == MediaStatus.PendingUpload && asset.UploadExpiresAt < now)
                    || (asset.Status == MediaStatus.Rejected && asset.CreatedAt < rejectedBefore))
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var asset in assets)
            {
                await storage.DeleteAsync(asset.IncomingKey, cancellationToken);
                data.MediaAssets.Remove(asset);
            }

            await data.SaveChangesAsync(cancellationToken);

            if (assets.Count > 0)
            {
                LogRemoved(logger, assets.Count);
            }

            return assets.Count;
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed {Count} abandoned uploads")]
    private static partial void LogRemoved(ILogger logger, int count);
}

public sealed partial class MediaHousekeepingService(MediaHousekeeping housekeeping, ILogger<MediaHousekeepingService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await housekeeping.RemoveAbandonedAsync(stoppingToken);
            }
#pragma warning disable CA1031 // A failed run is retried at the next interval.
            catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
            {
                LogRunFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Media housekeeping failed")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}

/// <summary>
/// Creates the bucket for local work. It runs in the background, because storage being down
/// should cost image uploads only, not stop the whole API from starting.
/// </summary>
public sealed partial class StorageBucketInitializer(
    IFileStorage storage,
    IOptions<StorageOptions> options,
    ILogger<StorageBucketInitializer> logger) : BackgroundService
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.CreateBucketIfMissing || storage is not S3FileStorage { IsConfigured: true } s3)
        {
            return;
        }

        // Do not hold up host start-up while waiting on the network.
        await Task.Yield();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(Limit);

        try
        {
            await s3.EnsureBucketAsync(timeout.Token);
        }
#pragma warning disable CA1031 // Any failure here only means uploads are unavailable for now.
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            LogBucketUnavailable(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "File storage could not be prepared, so image uploads will fail until it is reachable")]
    private static partial void LogBucketUnavailable(ILogger logger, Exception exception);
}

public static class MediaServiceRegistration
{
    public static IServiceCollection AddMediaInfrastructure(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                options => !options.CreateBucketIfMissing || environment.IsDevelopment() || environment.IsEnvironment("Testing"),
                "Storage:CreateBucketIfMissing is for local work only: production buckets are set up deliberately.")
            .Validate(
                options => environment.IsDevelopment() || !string.IsNullOrEmpty(options.ServiceUrl),
                "Storage:ServiceUrl must be configured outside Development.")
            .Validate(
                options => string.IsNullOrEmpty(options.ServiceUrl) || (!string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey)),
                "Storage:AccessKey and Storage:SecretKey are required when Storage:ServiceUrl is set.")
            .ValidateOnStart();

        services.AddSingleton<S3FileStorage>();
        services.AddSingleton<IFileStorage>(provider => provider.GetRequiredService<S3FileStorage>());
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
        services.AddSingleton<MediaHousekeeping>();
        services.AddHostedService<StorageBucketInitializer>();
        services.AddHostedService<MediaHousekeepingService>();

        return services;
    }
}
