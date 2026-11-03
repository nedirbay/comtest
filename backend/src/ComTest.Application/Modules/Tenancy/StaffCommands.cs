using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Domain.Modules.Tenancy;
using ComTest.Shared.Messaging;
using ComTest.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Application.Modules.Tenancy;

internal static class InvitationTokens
{
    private const int RandomBytes = 32;
    private const char Separator = '.';

    /// <summary>The tenant is part of the token, as row-level security needs it to find the invitation.</summary>
    public static (string Token, string Hash) Create(Guid tenantId)
    {
        var token = $"{tenantId:N}{Separator}{Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RandomBytes))}";

        return (token, Hash(token));
    }

    public static Guid? ReadTenantId(string token)
    {
        var separator = token.IndexOf(Separator, StringComparison.Ordinal);

        return separator > 0 && Guid.TryParse(token.AsSpan(0, separator), out var tenantId) ? tenantId : null;
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed record ListInvitationsQuery : IQuery<IReadOnlyList<StaffInvitationDto>>;

/// <summary>Invitations still waiting for an answer. Only the normalized address is stored, so it shows in lower case.</summary>
public sealed class ListInvitationsHandler(IApplicationDbContext db, TimeProvider timeProvider)
    : IQueryHandler<ListInvitationsQuery, IReadOnlyList<StaffInvitationDto>>
{
    public async Task<Result<IReadOnlyList<StaffInvitationDto>>> HandleAsync(ListInvitationsQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var pending = await db.StaffInvitations
            .AsNoTracking()
            .Where(invitation => invitation.AcceptedAt == null && invitation.RevokedAt == null && invitation.ExpiresAt > now)
            .OrderBy(invitation => invitation.ExpiresAt)
            .Select(invitation => new { invitation.Id, invitation.NormalizedEmail, invitation.Role, invitation.ExpiresAt })
            .ToListAsync(cancellationToken);

        return pending
            .Select(item => new StaffInvitationDto(item.Id, item.NormalizedEmail.ToLowerInvariant(), item.Role, item.ExpiresAt))
            .ToList();
    }
}

public sealed record ListStaffQuery : IQuery<IReadOnlyList<StaffMemberDto>>;

public sealed class ListStaffHandler(IApplicationDbContext db, IUserDirectory users)
    : IQueryHandler<ListStaffQuery, IReadOnlyList<StaffMemberDto>>
{
    public async Task<Result<IReadOnlyList<StaffMemberDto>>> HandleAsync(ListStaffQuery query, CancellationToken cancellationToken)
    {
        var members = await db.TenantMembers.AsNoTracking().OrderBy(member => member.CreatedAt).ToListAsync(cancellationToken);
        var people = (await users.FindManyAsync(members.Select(member => member.UserId).ToList(), cancellationToken))
            .ToDictionary(person => person.Id);

        return Result.Success<IReadOnlyList<StaffMemberDto>>(members
            .Where(member => people.ContainsKey(member.UserId))
            .Select(member =>
            {
                var person = people[member.UserId];

                return new StaffMemberDto(member.UserId, person.Email, person.FullName, member.Role, member.CreatedAt);
            })
            .ToList());
    }
}

public sealed record InviteStaffRequest(string Email, string Role);

internal sealed class InviteStaffRequestValidator : AbstractValidator<InviteStaffRequest>
{
    public InviteStaffRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Role).Must(StaffRoles.IsAssignable);
    }
}

internal static class StaffRoles
{
    /// <summary>Ownership cannot be given away by inviting or promoting someone.</summary>
    public static bool IsAssignable(string role) => role is Roles.Manager or Roles.Staff;
}

public sealed record InviteStaffCommand(Guid TenantId, InviteStaffRequest Invitation) : ICommand<StaffInvitationDto>;

public sealed class InviteStaffHandler(
    IApplicationDbContext db,
    IUserDirectory users,
    IStaffInvitationMailer mailer,
    TimeProvider timeProvider) : ICommandHandler<InviteStaffCommand, StaffInvitationDto>
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public async Task<Result<StaffInvitationDto>> HandleAsync(InviteStaffCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(item => item.Id == command.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Error.NotFound(ErrorCodes.Tenant.NotFound);
        }

        var now = timeProvider.GetUtcNow();
        var normalizedEmail = users.NormalizeEmail(command.Invitation.Email);

        var members = await db.TenantMembers.CountAsync(cancellationToken);
        var pending = await db.StaffInvitations.CountAsync(
            invitation => invitation.AcceptedAt == null && invitation.RevokedAt == null && invitation.ExpiresAt > now,
            cancellationToken);

        if (members + pending >= tenant.MaxStaff)
        {
            return Error.Conflict(ErrorCodes.Tenant.StaffLimitReached);
        }

        var (token, hash) = InvitationTokens.Create(command.TenantId);
        var invitation = StaffInvitation.Create(normalizedEmail, command.Invitation.Role, hash, now + Lifetime);

        db.StaffInvitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        mailer.Send(command.Invitation.Email, tenant.Name, token);

        return new StaffInvitationDto(invitation.Id, command.Invitation.Email, invitation.Role, invitation.ExpiresAt);
    }
}

public sealed record RevokeInvitationCommand(Guid InvitationId) : ICommand<bool>;

public sealed class RevokeInvitationHandler(IApplicationDbContext db, TimeProvider timeProvider)
    : ICommandHandler<RevokeInvitationCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invitation = await db.StaffInvitations.FirstOrDefaultAsync(item => item.Id == command.InvitationId, cancellationToken);

        if (invitation is null)
        {
            return Error.NotFound(ErrorCodes.Tenant.InvitationNotFound);
        }

        invitation.Revoke(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public sealed record AcceptInvitationRequest(string Token)
{
    public override string ToString() => nameof(AcceptInvitationRequest);
}

internal sealed class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
    }
}

public sealed record AcceptInvitationCommand(Guid UserId, string Token) : ICommand<Guid>
{
    public override string ToString() => nameof(AcceptInvitationCommand);
}

public sealed class AcceptInvitationHandler(
    IApplicationDbContext db,
    ITenantScope tenantScope,
    IUserDirectory users,
    TimeProvider timeProvider) : ICommandHandler<AcceptInvitationCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invalid = Error.Validation(ErrorCodes.Tenant.InvitationInvalid);

        if (InvitationTokens.ReadTenantId(command.Token) is not { } tenantId)
        {
            return invalid;
        }

        var user = await users.FindAsync(command.UserId, cancellationToken);

        if (user is not { EmailConfirmed: true })
        {
            return invalid;
        }

        tenantScope.Use(tenantId);

        var now = timeProvider.GetUtcNow();
        var hash = InvitationTokens.Hash(command.Token);
        var invitation = await db.StaffInvitations.FirstOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);

        // The invitation belongs to one mailbox: a forwarded link does not work for anyone else.
        if (invitation is null
            || !invitation.IsPending(now)
            || invitation.NormalizedEmail != users.NormalizeEmail(user.Email))
        {
            return invalid;
        }

        if (await db.TenantMembers.AnyAsync(member => member.UserId == command.UserId, cancellationToken))
        {
            return Error.Conflict(ErrorCodes.Tenant.AlreadyAMember);
        }

        invitation.Accept(now);
        db.TenantMembers.Add(TenantMember.Create(tenantId, command.UserId, invitation.Role));

        await db.SaveChangesAsync(cancellationToken);

        return tenantId;
    }
}

public sealed record ChangeStaffRoleRequest(string Role);

internal sealed class ChangeStaffRoleRequestValidator : AbstractValidator<ChangeStaffRoleRequest>
{
    public ChangeStaffRoleRequestValidator()
    {
        RuleFor(x => x.Role).Must(StaffRoles.IsAssignable);
    }
}

public sealed record ChangeStaffRoleCommand(Guid ActorId, Guid UserId, string Role) : ICommand<bool>;

public sealed class ChangeStaffRoleHandler(IApplicationDbContext db, IUserDirectory users)
    : ICommandHandler<ChangeStaffRoleCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(ChangeStaffRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ActorId == command.UserId)
        {
            return Error.Forbidden(ErrorCodes.Tenant.CannotChangeSelf);
        }

        var member = await db.TenantMembers.FirstOrDefaultAsync(item => item.UserId == command.UserId, cancellationToken);

        if (member is null)
        {
            return Error.NotFound(ErrorCodes.Tenant.MemberNotFound);
        }

        if (member.Role == Roles.StoreOwner)
        {
            return Error.Forbidden(ErrorCodes.Tenant.OwnerCannotBeChanged);
        }

        member.ChangeRole(command.Role);
        await db.SaveChangesAsync(cancellationToken);
        await users.InvalidateAccessTokensAsync(command.UserId, cancellationToken);

        return true;
    }
}

public sealed record RemoveStaffCommand(Guid ActorId, Guid TenantId, Guid UserId) : ICommand<bool>;

public sealed class RemoveStaffHandler(IApplicationDbContext db, IUserDirectory users)
    : ICommandHandler<RemoveStaffCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RemoveStaffCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ActorId == command.UserId)
        {
            return Error.Forbidden(ErrorCodes.Tenant.CannotChangeSelf);
        }

        var member = await db.TenantMembers.FirstOrDefaultAsync(item => item.UserId == command.UserId, cancellationToken);

        if (member is null)
        {
            return Error.NotFound(ErrorCodes.Tenant.MemberNotFound);
        }

        if (member.Role == Roles.StoreOwner)
        {
            return Error.Forbidden(ErrorCodes.Tenant.OwnerCannotBeChanged);
        }

        db.TenantMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        await users.DetachFromTenantAsync(command.UserId, command.TenantId, cancellationToken);

        return true;
    }
}
