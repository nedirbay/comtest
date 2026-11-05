namespace ComTest.Api.Hosting;

/// <summary>
/// `dotnet ComTest.Api.dll healthcheck`, for container health checks. The image has no shell or
/// curl to do this with, on purpose. It runs before the host is built, so it needs no settings.
/// </summary>
public static class HealthProbeCommand
{
    public const string Command = "healthcheck";

    private const string DefaultPort = "8080";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public static async Task<int> RunAsync()
    {
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(',', ';')[0] ?? DefaultPort;

        using var client = new HttpClient { Timeout = Timeout };

        try
        {
            using var response = await client.GetAsync(new Uri($"http://localhost:{port}/health/live"));

            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
