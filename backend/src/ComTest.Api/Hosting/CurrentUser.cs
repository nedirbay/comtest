using ComTest.Application.Abstractions;
using ComTest.Infrastructure.Identity;

namespace ComTest.Api.Hosting;

public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => ReadGuid(AppClaims.Subject);

    public Guid? SessionId => ReadGuid(AppClaims.SessionId);

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(claimType)?.Value, out var value) ? value : null;
}
