using ComTest.Domain.Modules.Media;
using ComTest.Shared.Results;

namespace ComTest.Application.Modules.Media.Contracts;

public sealed record StoredFileInfo(long SizeBytes);

/// <summary>Object storage. The bucket stays private: clients only ever hold short-lived signed URLs or CDN URLs.</summary>
public interface IFileStorage
{
    bool IsConfigured { get; }

    Uri CreateUploadUrl(string key, string contentType, TimeSpan lifetime);

    Task<StoredFileInfo?> StatAsync(string key, CancellationToken cancellationToken);

    /// <summary>Reads a whole object, refusing to hold more than <paramref name="maxBytes"/> in memory.</summary>
    Task<byte[]?> ReadAsync(string key, long maxBytes, CancellationToken cancellationToken);

    Task WriteAsync(string key, ReadOnlyMemory<byte> content, string contentType, string cacheControl, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);

    Uri GetPublicUrl(string key);
}

public sealed record ImageRendition(string Name, byte[] Content, int Width, int Height);

public sealed record ProcessedImage(IReadOnlyList<ImageRendition> Renditions, int Width, int Height);

/// <summary>Checks that bytes really are an image and re-encodes them, which also drops anything hidden inside.</summary>
public interface IImageProcessor
{
    Result<ProcessedImage> Process(byte[] input);
}

public sealed record MediaUrls(Uri Thumbnail, Uri Medium, Uri Large, int Width, int Height);

/// <summary>What other modules may ask about stored images.</summary>
public interface IMediaLibrary
{
    /// <summary>The images that are ready, by id. Others, including other stores' images, are absent.</summary>
    Task<IReadOnlyDictionary<Guid, MediaUrls>> GetReadyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

public sealed record UploadTicketDto(Guid Id, Uri UploadUrl, string ContentType, DateTimeOffset ExpiresAt)
{
    public override string ToString() => nameof(UploadTicketDto);
}

public sealed record MediaAssetDto(
    Guid Id,
    string DisplayName,
    MediaStatus Status,
    string? RejectionCode,
    long StoredBytes,
    int? Width,
    int? Height,
    Uri? Thumbnail,
    Uri? Medium,
    Uri? Large);
