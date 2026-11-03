using System.Diagnostics;
using ComTest.Api.Hosting;
using ComTest.Api.Localization;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace ComTest.Api.ErrorHandling;

internal static class ProblemFields
{
    public const string Code = "code";
    public const string CorrelationId = "correlationId";
    public const string TraceId = "traceId";
}

/// <summary>Gives every error response the same shape: localized title, stable code and correlation ID.</summary>
public static class ProblemDetailsEnricher
{
    public static void Enrich(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var problem = context.ProblemDetails;
        var http = context.HttpContext;

        problem.Extensions[ProblemFields.CorrelationId] = CorrelationIdMiddleware.Get(http);
        problem.Extensions[ProblemFields.TraceId] = Activity.Current?.Id ?? http.TraceIdentifier;

        if (!problem.Extensions.TryGetValue(ProblemFields.Code, out var existing) || existing is not string code)
        {
            code = CodeForStatus(problem.Status ?? http.Response.StatusCode);
            problem.Extensions[ProblemFields.Code] = code;
        }

        var localizer = http.RequestServices.GetRequiredService<IStringLocalizer<ErrorMessages>>();

        problem.Title = localizer[code];
        problem.Detail = null;
    }

    private static string CodeForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.Common.Validation,
        StatusCodes.Status401Unauthorized => ErrorCodes.Common.Unauthorized,
        StatusCodes.Status403Forbidden => ErrorCodes.Common.Forbidden,
        StatusCodes.Status404NotFound => ErrorCodes.Common.NotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorCodes.Common.MethodNotAllowed,
        StatusCodes.Status409Conflict => ErrorCodes.Common.Conflict,
        StatusCodes.Status413PayloadTooLarge => ErrorCodes.Common.PayloadTooLarge,
        StatusCodes.Status415UnsupportedMediaType => ErrorCodes.Common.UnsupportedMediaType,
        StatusCodes.Status429TooManyRequests => ErrorCodes.Common.TooManyRequests,
        StatusCodes.Status504GatewayTimeout => ErrorCodes.Common.Timeout,
        >= StatusCodes.Status500InternalServerError => ErrorCodes.Common.Unexpected,
        _ => ErrorCodes.Common.BadRequest,
    };
}

/// <summary>
/// Logs unhandled exceptions and answers with a generic response, so that exception messages,
/// stack traces, SQL and connection details never reach the client.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;

            return true;
        }

        var (status, code) = Classify(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception);
        }
        else
        {
            var exceptionType = exception.GetType().Name;
            LogRequestRejected(logger, exceptionType, status);
        }

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails { Status = status };
        problem.Extensions[ProblemFields.Code] = code;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    private static (int Status, string Code) Classify(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, ErrorCodes.Common.ConcurrencyConflict),
        DbUpdateException { InnerException: PostgresException { SqlState: UniqueViolation or ForeignKeyViolation } } =>
            (StatusCodes.Status409Conflict, ErrorCodes.Common.Conflict),
        BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
            (StatusCodes.Status413PayloadTooLarge, ErrorCodes.Common.PayloadTooLarge),
        BadHttpRequestException badRequest => (badRequest.StatusCode, ErrorCodes.Common.BadRequest),
        _ => (StatusCodes.Status500InternalServerError, ErrorCodes.Common.Unexpected),
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request rejected with {Status} after {ExceptionType}")]
    private static partial void LogRequestRejected(ILogger logger, string exceptionType, int status);
}

/// <summary>Turns a domain <see cref="Error"/> into its HTTP response.</summary>
public sealed class ErrorResultFactory(IStringLocalizer<ErrorMessages> localizer)
{
    public ObjectResult Create(HttpContext context, Error error)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);

        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status503ServiceUnavailable,
        };

        var factory = context.RequestServices.GetRequiredService<ProblemDetailsFactory>();

        ProblemDetails problem;

        if (error.Details is null)
        {
            problem = factory.CreateProblemDetails(context, status);
        }
        else
        {
            var modelErrors = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();

            foreach (var (field, messages) in error.Details)
            {
                foreach (var message in messages)
                {
                    modelErrors.AddModelError(field, localizer[message]);
                }
            }

            problem = factory.CreateValidationProblemDetails(context, modelErrors, status);
        }

        problem.Extensions[ProblemFields.Code] = error.Code;
        problem.Title = localizer[error.Code];

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}

/// <summary>Tells a signed-in staff member why they were refused when two-factor sign-in is what is missing.</summary>
public sealed class ProblemAuthorizationResultHandler(IProblemDetailsService problemDetails) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        var mfaMissing = authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailureReasons.Any(reason => reason.Message == ErrorCodes.Auth.MfaRequired) == true;

        if (!mfaMissing)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);

            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var problem = new ProblemDetails { Status = StatusCodes.Status403Forbidden };
        problem.Extensions[ProblemFields.Code] = ErrorCodes.Auth.MfaRequired;

        await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
