using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ComTest.Infrastructure.Persistence;

/// <summary>Refuses to start outside Development with an unencrypted or superuser connection.</summary>
public static class ConnectionStringGuard
{
    private static readonly string[] SuperuserNames = ["postgres", "root", "admin"];

    public static void Validate(string connectionString, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (builder.SslMode is not (SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull))
        {
            throw new InvalidOperationException(
                "The database connection must use TLS outside Development (SSL Mode=VerifyFull is recommended).");
        }

        if (SuperuserNames.Contains(builder.Username, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The application must not connect as a superuser outside Development, because that bypasses row-level security.");
        }

        if (builder.IncludeErrorDetail)
        {
            throw new InvalidOperationException("Include Error Detail must be off outside Development.");
        }
    }
}
