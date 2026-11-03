using ComTest.Api.ErrorHandling;
using ComTest.Application.Abstractions;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace ComTest.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ClientInfo Client => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString());

    private ICurrentUser CurrentUser => HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

    /// <summary>Only for authorized actions, where a missing user is a programming error.</summary>
    protected Guid UserId => CurrentUser.UserId
        ?? throw new InvalidOperationException("This action requires an authenticated user.");

    protected Guid? SessionId => CurrentUser.SessionId;

    protected IActionResult FromResult(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess ? NoContent() : Failure(result.Error);
    }

    protected IActionResult FromResult<T>(Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    protected IActionResult Failure(Error error) =>
        HttpContext.RequestServices.GetRequiredService<ErrorResultFactory>().Create(HttpContext, error);
}
