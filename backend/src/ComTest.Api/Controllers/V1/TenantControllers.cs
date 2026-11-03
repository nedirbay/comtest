using Asp.Versioning;
using ComTest.Api.Hosting;
using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Tenancy;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Domain.Modules.Tenancy;
using ComTest.Infrastructure.Identity.Authorization;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComTest.Api.Controllers.V1;

/// <summary>Base for actions that work inside one tenant.</summary>
public abstract class TenantControllerBase : ApiControllerBase
{
    /// <summary>The tenant of this request, or null when it was made outside of any store.</summary>
    protected Guid? CurrentTenantId => HttpContext.RequestServices.GetRequiredService<ITenantContext>().TenantId;

    protected static Error TenantRequired => Error.Validation(ErrorCodes.Tenant.Required);
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/store")]
public sealed class StoreController : TenantControllerBase
{
    /// <summary>Public details of the store this request's address belongs to.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PublicStoreDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromServices] IQueryHandler<GetPublicStoreQuery, PublicStoreDto> handler,
        CancellationToken cancellationToken) =>
        CurrentTenantId is { } tenantId
            ? FromResult(await handler.HandleAsync(new GetPublicStoreQuery(tenantId), cancellationToken))
            : Failure(TenantRequired);
}

public sealed record CreatedTenantResponse(TenantDto Tenant, string AccessToken, DateTimeOffset ExpiresAt)
{
    public override string ToString() => nameof(CreatedTenantResponse);
}

public sealed record AcceptedInvitationResponse(Guid TenantId);

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/tenants")]
[Authorize]
public sealed class TenantsController(ITenantSessionService tenantSessions) : TenantControllerBase
{
    /// <summary>Registers a new firm with the caller as its owner, and switches the session to it.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType<CreatedTenantResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        CreateTenantRequest request,
        [FromServices] ICommandHandler<CreateTenantCommand, TenantDto> handler,
        CancellationToken cancellationToken)
    {
        var created = await handler.HandleAsync(
            new CreateTenantCommand(UserId, request.Name, request.Slug, request.DefaultCurrency, request.DefaultLanguage),
            cancellationToken);

        if (created.IsFailure)
        {
            return Failure(created.Error);
        }

        var switched = await tenantSessions.SwitchAsync(UserId, SessionId!.Value, created.Value.Id, cancellationToken);

        return switched.IsFailure
            ? Failure(switched.Error)
            : Created((string?)null, new CreatedTenantResponse(created.Value, switched.Value.AccessToken, switched.Value.ExpiresAt));
    }

    [HttpPost("invitations/accept")]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType<AcceptedInvitationResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptInvitation(
        AcceptInvitationRequest request,
        [FromServices] ICommandHandler<AcceptInvitationCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var accepted = await handler.HandleAsync(new AcceptInvitationCommand(UserId, request.Token), cancellationToken);

        return accepted.IsSuccess ? Ok(new AcceptedInvitationResponse(accepted.Value)) : Failure(accepted.Error);
    }
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/store")]
public sealed class AdminStoreController : TenantControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType<StoreSettingsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromServices] IQueryHandler<GetStoreSettingsQuery, StoreSettingsDto> handler,
        CancellationToken cancellationToken) =>
        CurrentTenantId is { } tenantId
            ? FromResult(await handler.HandleAsync(new GetStoreSettingsQuery(tenantId), cancellationToken))
            : Failure(TenantRequired);

    [HttpPut]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType<StoreSettingsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        UpdateStoreSettingsRequest request,
        [FromServices] ICommandHandler<UpdateStoreSettingsCommand, StoreSettingsDto> handler,
        CancellationToken cancellationToken) =>
        CurrentTenantId is { } tenantId
            ? FromResult(await handler.HandleAsync(new UpdateStoreSettingsCommand(tenantId, request), cancellationToken))
            : Failure(TenantRequired);
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/store/payment-settings")]
public sealed class AdminPaymentSettingsController : TenantControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType<PaymentSettingsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromServices] IQueryHandler<GetPaymentSettingsQuery, PaymentSettingsDto> handler,
        CancellationToken cancellationToken) =>
        CurrentTenantId is { } tenantId
            ? FromResult(await handler.HandleAsync(new GetPaymentSettingsQuery(tenantId), cancellationToken))
            : Failure(TenantRequired);

    [HttpPut]
    [HasPermission(Permissions.SettingsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        UpdatePaymentSettingsRequest request,
        [FromServices] ICommandHandler<UpdatePaymentSettingsCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        if (CurrentTenantId is not { } tenantId)
        {
            return Failure(TenantRequired);
        }

        var result = await handler.HandleAsync(new UpdatePaymentSettingsCommand(tenantId, request), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/staff")]
public sealed class AdminStaffController : TenantControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.StaffManage)]
    [ProducesResponseType<IReadOnlyList<StaffMemberDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListStaffQuery, IReadOnlyList<StaffMemberDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListStaffQuery(), cancellationToken));

    [HttpGet("invitations")]
    [HasPermission(Permissions.StaffManage)]
    [ProducesResponseType<IReadOnlyList<StaffInvitationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Invitations(
        [FromServices] IQueryHandler<ListInvitationsQuery, IReadOnlyList<StaffInvitationDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListInvitationsQuery(), cancellationToken));

    [HttpPost("invitations")]
    [HasPermission(Permissions.StaffManage)]
    [EnableRateLimiting(RateLimitPolicies.AccountAction)]
    [ProducesResponseType<StaffInvitationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Invite(
        InviteStaffRequest request,
        [FromServices] ICommandHandler<InviteStaffCommand, StaffInvitationDto> handler,
        CancellationToken cancellationToken) =>
        CurrentTenantId is { } tenantId
            ? FromResult(await handler.HandleAsync(new InviteStaffCommand(tenantId, request), cancellationToken))
            : Failure(TenantRequired);

    [HttpDelete("invitations/{id:guid}")]
    [HasPermission(Permissions.StaffManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeInvitation(
        Guid id,
        [FromServices] ICommandHandler<RevokeInvitationCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new RevokeInvitationCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    [HttpPut("{userId:guid}/role")]
    [HasPermission(Permissions.StaffManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangeRole(
        Guid userId,
        ChangeStaffRoleRequest request,
        [FromServices] ICommandHandler<ChangeStaffRoleCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ChangeStaffRoleCommand(UserId, userId, request.Role), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    [HttpDelete("{userId:guid}")]
    [HasPermission(Permissions.StaffManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(
        Guid userId,
        [FromServices] ICommandHandler<RemoveStaffCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        if (CurrentTenantId is not { } tenantId)
        {
            return Failure(TenantRequired);
        }

        var result = await handler.HandleAsync(new RemoveStaffCommand(UserId, tenantId, userId), cancellationToken);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }
}

public sealed record SetTenantStatusRequest(TenantStatus Status);

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/platform/tenants")]
public sealed class PlatformTenantsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.TenantsManage)]
    [ProducesResponseType<PagedResult<TenantDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] TenantStatus? status,
        [FromServices] IQueryHandler<ListTenantsQuery, PagedResult<TenantDto>> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new ListTenantsQuery(PageRequests.From(page, pageSize), status), cancellationToken));

    [HttpPut("{id:guid}/status")]
    [HasPermission(Permissions.TenantsManage)]
    [ProducesResponseType<TenantDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(
        Guid id,
        SetTenantStatusRequest request,
        [FromServices] ICommandHandler<SetTenantStatusCommand, TenantDto> handler,
        CancellationToken cancellationToken) =>
        FromResult(await handler.HandleAsync(new SetTenantStatusCommand(id, request.Status), cancellationToken));
}

internal static class PageRequests
{
    private const int DefaultPageSize = 20;

    /// <summary>Clamps client input, so a request cannot ask for an unbounded page.</summary>
    public static PageRequest From(int page, int pageSize) => new()
    {
        Page = Math.Max(page, 1),
        PageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, PageRequest.MaxPageSize),
    };
}
