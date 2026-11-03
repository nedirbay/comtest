using ComTest.Shared.Domain;

namespace ComTest.Domain.Modules.Media;

public enum MediaStatus
{
    PendingUpload,
    Processing,
    Ready,
    Rejected,
}

public sealed record MediaUploaded(Guid MediaAssetId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// An uploaded image. The original never becomes public: it is checked, re-encoded into new
/// files and deleted, so nothing hidden inside the upload can reach a visitor.
/// </summary>
public sealed class MediaAsset : TenantOwnedEntity
{
    private MediaAsset()
    {
    }

    /// <summary>Only for showing to staff. Storage keys never use anything the client sent.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public long DeclaredBytes { get; private set; }

    public MediaStatus Status { get; private set; }

    public string IncomingKey { get; private set; } = string.Empty;

    public string? BaseKey { get; private set; }

    public long StoredBytes { get; private set; }

    public int? Width { get; private set; }

    public int? Height { get; private set; }

    public string? RejectionCode { get; private set; }

    public DateTimeOffset UploadExpiresAt { get; private set; }

    public static MediaAsset Begin(Guid id, string displayName, long declaredBytes, string incomingKey, DateTimeOffset uploadExpiresAt) => new()
    {
        Id = id,
        DisplayName = displayName,
        DeclaredBytes = declaredBytes,
        IncomingKey = incomingKey,
        UploadExpiresAt = uploadExpiresAt,
        Status = MediaStatus.PendingUpload,
    };

    public void MarkUploaded(DateTimeOffset now)
    {
        Status = MediaStatus.Processing;
        Raise(new MediaUploaded(Id, now));
    }

    public void MarkReady(string baseKey, long storedBytes, int width, int height)
    {
        Status = MediaStatus.Ready;
        BaseKey = baseKey;
        StoredBytes = storedBytes;
        Width = width;
        Height = height;
        RejectionCode = null;
    }

    public void Reject(string code)
    {
        Status = MediaStatus.Rejected;
        RejectionCode = code;
    }
}
