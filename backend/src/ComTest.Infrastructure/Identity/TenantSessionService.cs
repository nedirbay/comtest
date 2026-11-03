using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Tenancy.Contracts;
using ComTest.Domain.Modules.Tenancy;
using ComTest.Infrastructure.Messaging;
using ComTest.Infrastructure.Persistence;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Infrastructure.Identity;

public sealed class TenantSessionService(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    PlatformDataAccess platformData,
    ITenantDirectory directory,
    AccessTokenIssuer accessTokens) : ITenantSessionService
{
    public async Task<Result<SwitchedTenant>> SwitchAsync(Guid userId, Guid sessionId, Guid tenantId, CancellationToken cancellationToken)
    {
        var isMember = await platformData.RunAsync(
            data => data.TenantMembers.AnyAsync(member => member.TenantId == tenantId && member.UserId == userId, cancellationToken));

        if (!isMember)
        {
            return Error.Forbidden(ErrorCodes.Tenant.NotAMember);
        }

        var tenant = await directory.FindByIdAsync(tenantId, cancellationToken);

        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            return Error.Forbidden(ErrorCodes.Tenant.Suspended);
        }

        var session = await db.UserSessions.FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId, cancellationToken);
        var user = await users.FindByIdAsync(userId.ToString());

        if (session is null || user is null)
        {
            return Error.Unauthorized(ErrorCodes.Common.Unauthorized);
        }

        session.ActiveTenantId = tenantId;
        await db.SaveChangesAsync(cancellationToken);

        var access = await accessTokens.IssueAsync(user, session, cancellationToken);

        return new SwitchedTenant(access.Token, access.ExpiresAt);
    }

    public async Task<IReadOnlyList<UserMembership>> ListMembershipsAsync(Guid userId, CancellationToken cancellationToken) =>
        await platformData.RunAsync(async data => (IReadOnlyList<UserMembership>)await (
            from member in data.TenantMembers
            join tenant in data.Tenants on member.TenantId equals tenant.Id
            where member.UserId == userId
            orderby tenant.Name
            select new UserMembership(tenant.Id, tenant.Name, tenant.Slug, member.Role))
            .ToListAsync(cancellationToken));
}

public sealed class UserDirectory(AppDbContext db, UserManager<ApplicationUser> users) : IUserDirectory
{
    public async Task<UserSummary?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserSummary(user.Id, user.Email!, user.FullName, user.EmailConfirmed))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<UserSummary>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        await db.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new UserSummary(user.Id, user.Email!, user.FullName, user.EmailConfirmed))
            .ToListAsync(cancellationToken);

    public string NormalizeEmail(string email) => users.NormalizeEmail(email);

    public async Task InvalidateAccessTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());

        if (user is not null)
        {
            await users.UpdateSecurityStampAsync(user);
        }
    }

    public async Task DetachFromTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        await db.UserSessions
            .Where(session => session.UserId == userId && session.ActiveTenantId == tenantId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.ActiveTenantId, (Guid?)null), cancellationToken);

        await InvalidateAccessTokensAsync(userId, cancellationToken);
    }
}

public sealed class StaffInvitationMailer(AuthEmailComposer composer, IEmailDispatcher dispatcher) : IStaffInvitationMailer
{
    public void Send(string email, string tenantName, string token) =>
        dispatcher.TryEnqueue(composer.StaffInvitation(email, tenantName, token));
}
