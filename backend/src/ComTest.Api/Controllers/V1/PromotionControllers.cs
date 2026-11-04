using Asp.Versioning;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Promotions;
using ComTest.Application.Modules.Promotions.Contracts;
using ComTest.Domain.Modules.Promotions;
using ComTest.Infrastructure.Identity.Authorization;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using Microsoft.AspNetCore.Mvc;

namespace ComTest.Api.Controllers.V1;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/promotions")]
[HasPermission(Permissions.PromotionsManage)]
public sealed class AdminPromotionsController : TenantControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<PromotionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? q,
        [FromServices] IQueryHandler<ListPromotionsQuery, PagedResult<PromotionDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListPromotionsQuery(PageRequests.From(page, pageSize), q), cancellationToken));

    /// <summary>A code nobody can guess. Staff can also type their own.</summary>
    [HttpGet("new-code")]
    [ProducesResponseType<NewPromotionCodeDto>(StatusCodes.Status200OK)]
    public IActionResult NewCode() => Ok(new NewPromotionCodeDto(PromotionCode.Generate()));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PromotionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        Guid id,
        [FromServices] IQueryHandler<GetPromotionQuery, PromotionDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new GetPromotionQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType<PromotionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        SavePromotionRequest request,
        [FromServices] ICommandHandler<CreatePromotionCommand, PromotionDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new CreatePromotionCommand(request), cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PromotionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        SavePromotionRequest request,
        [FromServices] ICommandHandler<UpdatePromotionCommand, PromotionDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new UpdatePromotionCommand(id, request), cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeletePromotionCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeletePromotionCommand(id), cancellationToken);

        return result.IsFailure ? Failure(result.Error) : NoContent();
    }
}
