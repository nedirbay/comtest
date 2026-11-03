using System.Buffers;
using Serilog.Context;

namespace ComTest.Api.Hosting;

/// <summary>
/// Gives each request an ID shared by its response, logs and errors. An incoming ID is kept only
/// if it is short and uses safe characters, so that it cannot be used to forge log entries.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    private const int MaxLength = 64;
    private const string ItemKey = "CorrelationId";

    private static readonly SearchValues<char> AllowedCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_");

    public static string? Get(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items[ItemKey] as string;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsAcceptable(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;

            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(ItemKey, correlationId))
        {
            await next(context);
        }
    }

    private static bool IsAcceptable(string value) =>
        value.Length is > 0 and <= MaxLength && !value.AsSpan().ContainsAnyExcept(AllowedCharacters);
}
