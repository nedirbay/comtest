using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Catalog.Contracts;
using ComTest.Application.Modules.Media.Contracts;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Domain.Modules.Media;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Media;

public static class MediaRules
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const string RenditionContentType = "image/webp";

    /// <summary>Hashed names make the files safe to cache forever: new content always gets a new name.</summary>
    public const string RenditionCacheControl = "public, max-age=31536000, immutable";

    public static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(15);

    /// <summary>No SVG: it can carry scripts. No client-chosen types at all beyond these.</summary>
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.Ordinal) { "image/jpeg", "image/png", "image/webp" };

    public static readonly string[] RenditionNames = ["thumb", "medium", "large"];

    public static string IncomingKey(Guid tenantId, Guid assetId) => $"tenants/{tenantId:N}/incoming/{assetId:N}";

    public static string BaseKey(Guid tenantId, Guid assetId) => $"tenants/{tenantId:N}/media/{assetId:N}";

    public static string RenditionKey(string baseKey, string name) => $"{baseKey}/{name}.webp";

    /// <summary>Keeps only a harmless file name for display. Storage keys never use it.</summary>
    public static string ToDisplayName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var name = fileName.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = new string(name.Where(character => !char.IsControl(character)).ToArray()).Trim();

        return name.Length == 0 ? "image" : name[..Math.Min(name.Length, 200)];
    }
}

public sealed record BeginUploadRequest(string FileName, string ContentType, long SizeBytes);

internal sealed class BeginUploadRequestValidator : AbstractValidator<BeginUploadRequest>
{
    public BeginUploadRequestValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SizeBytes).GreaterThan(0);
    }
}

public sealed record BeginUploadCommand(BeginUploadRequest Request) : ICommand<UploadTicketDto>;

public sealed record CompleteUploadCommand(Guid Id) : ICommand<MediaAssetDto>;

public sealed record GetMediaQuery(Guid Id) : IQuery<MediaAssetDto>;

public sealed record ListMediaQuery(PageRequest Page) : IQuery<PagedResult<MediaAssetDto>>;

public sealed record DeleteMediaCommand(Guid Id) : ICommand<bool>;

public sealed class MediaHandlers(
    IApplicationDbContext db,
    ITenantSettings tenantSettings,
    IFileStorage storage,
    ICatalogReader catalog,
    TimeProvider timeProvider) :
    ICommandHandler<BeginUploadCommand, UploadTicketDto>,
    ICommandHandler<CompleteUploadCommand, MediaAssetDto>,
    IQueryHandler<GetMediaQuery, MediaAssetDto>,
    IQueryHandler<ListMediaQuery, PagedResult<MediaAssetDto>>,
    ICommandHandler<DeleteMediaCommand, bool>,
    IMediaLibrary
{
    private const long BytesPerMegabyte = 1024 * 1024;

    public async Task<Result<UploadTicketDto>> HandleAsync(BeginUploadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;

        if (!storage.IsConfigured)
        {
            return Error.Failure(ErrorCodes.Media.StorageUnavailable);
        }

        if (!MediaRules.AllowedContentTypes.Contains(request.ContentType))
        {
            return Error.Validation(ErrorCodes.Media.UnsupportedType);
        }

        if (request.SizeBytes > MediaRules.MaxFileBytes)
        {
            return Error.Validation(ErrorCodes.Media.TooLarge);
        }

        var settings = await tenantSettings.GetCurrentAsync(cancellationToken);

        if (settings is null)
        {
            return Error.Validation(ErrorCodes.Tenant.Required);
        }

        var now = timeProvider.GetUtcNow();

        // Unfinished uploads count too, so that many parallel uploads cannot slip past the quota.
        var used = await db.MediaAssets
            .Where(asset => asset.Status == MediaStatus.Ready || asset.Status == MediaStatus.Processing || (asset.Status == MediaStatus.PendingUpload && asset.UploadExpiresAt > now))
            .SumAsync(asset => asset.Status == MediaStatus.Ready ? asset.StoredBytes : asset.DeclaredBytes, cancellationToken);

        if (used + request.SizeBytes > settings.StorageQuotaMegabytes * BytesPerMegabyte)
        {
            return Error.Conflict(ErrorCodes.Media.QuotaExceeded);
        }

        var id = Guid.CreateVersion7();
        var key = MediaRules.IncomingKey(settings.TenantId, id);
        var expiresAt = now + MediaRules.UploadLifetime;

        db.MediaAssets.Add(MediaAsset.Begin(id, MediaRules.ToDisplayName(request.FileName), request.SizeBytes, key, expiresAt));
        await db.SaveChangesAsync(cancellationToken);

        return new UploadTicketDto(id, storage.CreateUploadUrl(key, request.ContentType, MediaRules.UploadLifetime), request.ContentType, expiresAt);
    }

    public async Task<Result<MediaAssetDto>> HandleAsync(CompleteUploadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var asset = await db.MediaAssets.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (asset is null)
        {
            return Error.NotFound(ErrorCodes.Media.NotFound);
        }

        if (asset.Status != MediaStatus.PendingUpload)
        {
            return Error.Conflict(ErrorCodes.Media.InvalidState);
        }

        if (await storage.StatAsync(asset.IncomingKey, cancellationToken) is null)
        {
            return Error.Conflict(ErrorCodes.Media.UploadMissing);
        }

        asset.MarkUploaded(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(asset);
    }

    public async Task<Result<MediaAssetDto>> HandleAsync(GetMediaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var asset = await db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(item => item.Id == query.Id, cancellationToken);

        return asset is null ? Error.NotFound(ErrorCodes.Media.NotFound) : ToDto(asset);
    }

    public async Task<Result<PagedResult<MediaAssetDto>>> HandleAsync(ListMediaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var assets = db.MediaAssets.AsNoTracking().Where(item => item.Status == MediaStatus.Ready);
        var total = await assets.LongCountAsync(cancellationToken);

        var items = await assets
            .OrderByDescending(item => item.Id)
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MediaAssetDto>(items.Select(ToDto).ToList(), query.Page.Page, query.Page.PageSize, total);
    }

    public async Task<Result<bool>> HandleAsync(DeleteMediaCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var asset = await db.MediaAssets.FirstOrDefaultAsync(item => item.Id == command.Id, cancellationToken);

        if (asset is null)
        {
            return Error.NotFound(ErrorCodes.Media.NotFound);
        }

        if (await catalog.IsMediaInUseAsync(asset.Id, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Media.InUse);
        }

        await MediaFiles.DeleteAllAsync(storage, asset, cancellationToken);

        db.MediaAssets.Remove(asset);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyDictionary<Guid, MediaUrls>> GetReadyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, MediaUrls>();
        }

        var assets = await db.MediaAssets
            .AsNoTracking()
            .Where(item => ids.Contains(item.Id) && item.Status == MediaStatus.Ready)
            .ToListAsync(cancellationToken);

        return assets.ToDictionary(item => item.Id, item => ToUrls(item)!);
    }

    private MediaUrls? ToUrls(MediaAsset asset) =>
        asset is { Status: MediaStatus.Ready, BaseKey: { } baseKey }
            ? new MediaUrls(
                storage.GetPublicUrl(MediaRules.RenditionKey(baseKey, "thumb")),
                storage.GetPublicUrl(MediaRules.RenditionKey(baseKey, "medium")),
                storage.GetPublicUrl(MediaRules.RenditionKey(baseKey, "large")),
                asset.Width ?? 0,
                asset.Height ?? 0)
            : null;

    private MediaAssetDto ToDto(MediaAsset asset)
    {
        var urls = ToUrls(asset);

        return new MediaAssetDto(asset.Id, asset.DisplayName, asset.Status.ToString(), asset.RejectionCode, asset.StoredBytes, asset.Width, asset.Height, urls?.Thumbnail, urls?.Medium, urls?.Large);
    }
}

internal static class MediaFiles
{
    public static async Task DeleteAllAsync(IFileStorage storage, MediaAsset asset, CancellationToken cancellationToken)
    {
        await storage.DeleteAsync(asset.IncomingKey, cancellationToken);

        if (asset.BaseKey is { } baseKey)
        {
            foreach (var name in MediaRules.RenditionNames)
            {
                await storage.DeleteAsync(MediaRules.RenditionKey(baseKey, name), cancellationToken);
            }
        }
    }
}

/// <summary>
/// Runs from the outbox after an upload. Bad files are rejected for good, whereas storage
/// failures throw, so that the outbox tries again later.
/// </summary>
public sealed class MediaProcessor(
    IApplicationDbContext db,
    ITenantContext tenantContext,
    IFileStorage storage,
    IImageProcessor images) : IDomainEventSubscriber<MediaUploaded>
{
    public async Task HandleAsync(MediaUploaded domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var asset = await db.MediaAssets.FirstOrDefaultAsync(item => item.Id == domainEvent.MediaAssetId, cancellationToken);

        if (asset is not { Status: MediaStatus.Processing } || tenantContext.TenantId is not { } tenantId)
        {
            return;
        }

        var error = await ProcessAsync(asset, tenantId, cancellationToken);

        if (error is not null)
        {
            asset.Reject(error);
        }

        // The original has served its purpose either way, and must never stay around unchecked.
        await storage.DeleteAsync(asset.IncomingKey, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> ProcessAsync(MediaAsset asset, Guid tenantId, CancellationToken cancellationToken)
    {
        // A presigned upload cannot limit size in advance, so this is where the limit is enforced.
        var stat = await storage.StatAsync(asset.IncomingKey, cancellationToken);

        if (stat is null)
        {
            return ErrorCodes.Media.UploadMissing;
        }

        if (stat.SizeBytes > MediaRules.MaxFileBytes || stat.SizeBytes > asset.DeclaredBytes)
        {
            return ErrorCodes.Media.TooLarge;
        }

        var content = await storage.ReadAsync(asset.IncomingKey, MediaRules.MaxFileBytes, cancellationToken);

        if (content is null)
        {
            return ErrorCodes.Media.UploadMissing;
        }

        var processed = images.Process(content);

        if (processed.IsFailure)
        {
            return processed.Error.Code;
        }

        var baseKey = MediaRules.BaseKey(tenantId, asset.Id);
        long stored = 0;

        foreach (var rendition in processed.Value.Renditions)
        {
            await storage.WriteAsync(
                MediaRules.RenditionKey(baseKey, rendition.Name),
                rendition.Content,
                MediaRules.RenditionContentType,
                MediaRules.RenditionCacheControl,
                cancellationToken);

            stored += rendition.Content.Length;
        }

        asset.MarkReady(baseKey, stored, processed.Value.Width, processed.Value.Height);

        return null;
    }
}
