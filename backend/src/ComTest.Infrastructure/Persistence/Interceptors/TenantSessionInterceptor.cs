using System.Data.Common;
using ComTest.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ComTest.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Tells PostgreSQL which tenant this connection serves, for the row-level security policies.
/// The value is set on every open, so a pooled connection can never keep a previous tenant.
/// </summary>
public sealed class TenantSessionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    private const string SetTenantSql =
        "SELECT set_config('app.tenant_id', @tenant_id, false), set_config('app.platform_access', @platform_access, false)";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var command = CreateCommand(connection);

        await using (command)
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = SetTenantSql;

        AddParameter(command, "tenant_id", tenantContext.TenantId?.ToString("D") ?? string.Empty);
        AddParameter(command, "platform_access", tenantContext.IsPlatformAccess ? "on" : "off");

        return command;
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
