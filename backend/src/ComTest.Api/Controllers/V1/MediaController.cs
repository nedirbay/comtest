using Asp.Versioning;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Media;
using ComTest.Application.Modules.Media.Contracts;
using ComTest.Infrastructure.Identity.Authorization;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using Microsoft.AspNetCore.Mvc;

namespace ComTest.Api.Controllers.V1;

/// <summary>
/// Images for the store. The file goes straight to object storage on a short-lived signed URL,
/// so the API never has to hold large uploads.
/// </summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/media")]
public sealed class AdminMediaController : TenantControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType<PagedResult<MediaAssetDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IQueryHandler<ListMediaQuery, PagedResult<MediaAssetDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListMediaQuery(PageRequests.From(page, pageSize)), cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.CatalogRead)]
    [ProducesResponseType<MediaAssetDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        Guid id,
        [FromServices] IQueryHandler<GetMediaQuery, MediaAssetDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetMediaQuery(id), cancellationToken));

    [HttpPost("uploads")]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType<UploadTicketDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> BeginUpload(
        BeginUploadRequest request,
        [FromServices] ICommandHandler<BeginUploadCommand, UploadTicketDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new BeginUploadCommand(request), cancellationToken));

    [HttpPost("{id:guid}/complete")]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType<MediaAssetDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteUpload(
        Guid id,
        [FromServices] ICommandHandler<CompleteUploadCommand, MediaAssetDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new CompleteUploadCommand(id), cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.CatalogWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteMediaCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteMediaCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}
